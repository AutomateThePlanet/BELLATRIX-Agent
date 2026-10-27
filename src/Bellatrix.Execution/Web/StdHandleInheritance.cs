using System.Runtime.InteropServices;

namespace Bellatrix.Execution.Web;

/// <summary>
/// Keeps this process's stdin/stdout/stderr out of the detached browser-driver process. The driver outlives
/// the CLI invocation by design; if it inherits the CLI's stdout pipe, whoever captures <c>bellatrix web
/// start-session</c> output through a pipe (a shell <c>$(…)</c>, an AI agent's tool runner, the E2E harness)
/// never sees EOF and hangs until the session is closed. Found by the v.1 live E2E. Windows only: there the
/// child inherits every inheritable handle, so clearing <c>HANDLE_FLAG_INHERIT</c> on the three standard
/// handles is enough. The CLI process exits right after, so the flags are not restored.
/// </summary>
internal static class StdHandleInheritance
{
    private const int StdInput = -10;
    private const int StdOutput = -11;
    private const int StdError = -12;
    private const uint HandleFlagInherit = 0x00000001;

    public static void DisableForChildren()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var std in new[] { StdInput, StdOutput, StdError })
        {
            var handle = GetStdHandle(std);
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                SetHandleInformation(handle, HandleFlagInherit, 0); // best effort; failure keeps old behavior
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);
}
