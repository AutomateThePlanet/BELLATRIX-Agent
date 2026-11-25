using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>A <see cref="FactAttribute"/> that runs only on Windows (ConPTY); elsewhere it is reported as skipped.</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Needs the Windows pseudo console (ConPTY). On other systems run the manual check in docs/contributing/LIVE-VERIFICATION-CHECKLIST.md (section D).";
        }
    }
}

/// <summary>
/// Runs a process attached to a real Windows pseudo console (ConPTY, Windows 10 1809+ / Server 2019+), so that
/// for the child stdin, stdout and stderr are a terminal, not redirected pipes. Used to test what <c>bellatrix init</c>
/// does in a terminal: prompts, answers typed by the user, and Ctrl+C. No dependency beyond kernel32.
/// </summary>
internal sealed class PseudoTerminal : IDisposable
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const int UseStdHandles = 0x00000100;
    private static readonly IntPtr ProcThreadAttributePseudoConsole = (IntPtr)0x00020016;

    private readonly IntPtr _console;
    private readonly FileStream _input;
    private readonly FileStream _output;
    private readonly IntPtr _process;
    private readonly IntPtr _thread;
    private readonly StringBuilder _screen = new();
    private readonly Thread _reader;

    private PseudoTerminal(IntPtr console, FileStream input, FileStream output, IntPtr process, IntPtr thread)
    {
        _console = console;
        _input = input;
        _output = output;
        _process = process;
        _thread = thread;
        _reader = new Thread(ReadLoop) { IsBackground = true };
        _reader.Start();
    }

    /// <summary>Starts <paramref name="commandLine"/> in <paramref name="cwd"/> with exactly <paramref name="environment"/>.</summary>
    public static PseudoTerminal Start(string commandLine, string cwd, IDictionary<string, string> environment, short columns = 300, short rows = 50)
    {
        if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0) || !CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
        {
            throw new Win32Exception();
        }

        var hr = CreatePseudoConsole(new Coord { X = columns, Y = rows }, inputRead, outputWrite, 0, out var console);
        if (hr != 0)
        {
            throw new Win32Exception(hr);
        }

        // The pseudo console owns its ends now.
        inputRead.Dispose();
        outputWrite.Dispose();

        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var attributes = Marshal.AllocHGlobal(size);
        if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)
            || !UpdateProcThreadAttribute(attributes, 0, ProcThreadAttributePseudoConsole, console, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
        {
            throw new Win32Exception();
        }

        // STARTF_USESTDHANDLES with null handles: otherwise a child of a process whose std handles are redirected (the
        // test host) inherits those pipes instead of attaching to the pseudo console.
        var startup = new StartupInfoEx
        {
            StartupInfo = { cb = Marshal.SizeOf<StartupInfoEx>(), dwFlags = UseStdHandles },
            lpAttributeList = attributes,
        };
        var block = new StringBuilder();
        foreach (var (key, value) in environment.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(key).Append('=').Append(value).Append('\0');
        }

        block.Append('\0');
        var env = Marshal.StringToHGlobalUni(block.ToString());
        try
        {
            if (!CreateProcess(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, false,
                    ExtendedStartupInfoPresent | CreateUnicodeEnvironment, env, cwd, ref startup, out var info))
            {
                throw new Win32Exception();
            }

            return new PseudoTerminal(console, new FileStream(inputWrite, FileAccess.Write), new FileStream(outputRead, FileAccess.Read),
                info.hProcess, info.hThread);
        }
        finally
        {
            Marshal.FreeHGlobal(env);
            DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes);
        }
    }

    /// <summary>The environment of this process, changed by <paramref name="set"/> (a null value removes the variable).</summary>
    public static Dictionary<string, string> Environment(params (string Key, string? Value)[] set)
    {
        var env = System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in set)
        {
            if (value is null)
            {
                env.Remove(key);
            }
            else
            {
                env[key] = value;
            }
        }

        return env;
    }

    /// <summary>Everything the terminal showed so far, without VT escape sequences.</summary>
    public string Screen
    {
        get
        {
            lock (_screen)
            {
                return Regex.Replace(_screen.ToString(), @"\x1b\[[0-9;?]*[ -/]*[@-~]|\x1b\][^\x07]*\x07|\x1b[=>]", "");
            }
        }
    }

    /// <summary>Waits until the screen contains <paramref name="text"/> (whitespace-insensitive); false on timeout or exit.</summary>
    public bool WaitFor(string text, TimeSpan timeout)
    {
        var wanted = Regex.Replace(text, @"\s+", "");
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (Regex.Replace(Screen, @"\s+", "").Contains(wanted, StringComparison.Ordinal))
            {
                return true;
            }

            if (HasExited)
            {
                Thread.Sleep(300); // let the reader drain the last output
                return Regex.Replace(Screen, @"\s+", "").Contains(wanted, StringComparison.Ordinal);
            }

            Thread.Sleep(50);
        }

        return false;
    }

    /// <summary>Types <paramref name="keys"/> (use <c>\r</c> for Enter and <c>\x03</c> for Ctrl+C).</summary>
    public void Type(string keys)
    {
        var bytes = Encoding.UTF8.GetBytes(keys);
        _input.Write(bytes, 0, bytes.Length);
        _input.Flush();
    }

    public bool HasExited => WaitForSingleObject(_process, 0) == 0;

    /// <summary>Waits for the process to exit and returns its exit code; kills it on timeout.</summary>
    public int WaitForExit(TimeSpan timeout)
    {
        if (WaitForSingleObject(_process, (uint)timeout.TotalMilliseconds) != 0)
        {
            TerminateProcess(_process, 999);
            throw new TimeoutException("The process did not exit. Screen:\n" + Screen);
        }

        GetExitCodeProcess(_process, out var code);
        Thread.Sleep(300); // let the reader drain the last output
        return unchecked((int)code);
    }

    private void ReadLoop()
    {
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();
        var chars = new char[8192];
        try
        {
            int read;
            while ((read = _output.Read(buffer, 0, buffer.Length)) > 0)
            {
                var n = decoder.GetChars(buffer, 0, read, chars, 0);
                lock (_screen)
                {
                    _screen.Append(chars, 0, n);
                }
            }
        }
        catch (IOException)
        {
            // the pseudo console was closed
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (!HasExited)
        {
            TerminateProcess(_process, 999);
        }

        ClosePseudoConsole(_console); // also ends the reader (the output pipe breaks)
        _input.Dispose();
        _reader.Join(TimeSpan.FromSeconds(5));
        _output.Dispose();
        CloseHandle(_thread);
        CloseHandle(_process);
    }

    // ---------------------------------------------------------------- kernel32

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, IntPtr attributes, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(Coord size, SafeFileHandle input, SafeFileHandle output, uint flags, out IntPtr console);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr console);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string? application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref StartupInfoEx startup, out ProcessInformation info);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
