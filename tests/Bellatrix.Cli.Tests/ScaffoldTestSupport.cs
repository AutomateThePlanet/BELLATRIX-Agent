using Bellatrix.Cli.Scaffolding;

namespace Bellatrix.Cli.Tests;

/// <summary>A throwaway temp directory used as a scaffold target root; deleted on dispose.</summary>
internal sealed class TempRepo : IDisposable
{
    public string Root { get; }

    public TempRepo()
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bellatrix-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Path(string relative)
        => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Write(string relative, string content)
    {
        var full = Path(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public string Read(string relative) => File.ReadAllText(Path(relative));

    public bool Exists(string relative) => File.Exists(Path(relative));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

/// <summary>
/// A throwaway copy of the neutral custom template folder <c>tests/fixtures/custom-templates/</c> (prefix <c>acme</c>)
/// for <c>bellatrix init --template-source</c> tests; tests may edit the copy. Deleted on dispose.
/// </summary>
internal sealed class CustomTemplateFixture : IDisposable
{
    public const string Prefix = "acme";

    public CustomTemplateFixture()
    {
        var committed = CommittedFolder();
        Parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bellatrix-custom-templates-" + Guid.NewGuid().ToString("N"));
        Root = System.IO.Path.Combine(Parent, "templates");
        foreach (var file in Directory.GetFiles(committed, "*", SearchOption.AllDirectories))
        {
            var target = System.IO.Path.Combine(Root, System.IO.Path.GetRelativePath(committed, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <summary>The committed fixture folder (resolved from this source file, not the caller's).</summary>
    public static string CommittedFolder([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        => System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile)!, "..", "fixtures", "custom-templates"));

    /// <summary>The template folder (what <c>--template-source</c> points at).</summary>
    public string Root { get; }

    /// <summary>The directory containing <see cref="Root"/>.</summary>
    public string Parent { get; }

    /// <summary>Skill folder names in the fixture.</summary>
    public string[] SkillNames => Directory.GetDirectories(System.IO.Path.Combine(Root, "skills")).Select(System.IO.Path.GetFileName).ToArray()!;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Parent))
            {
                Directory.Delete(Parent, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

/// <summary>Runs the CLI in-process from a working directory, capturing stdout and stderr.</summary>
internal static class CliHarness
{
    public static (int Exit, string Output, string Errors) Run(string cwd, params string[] args)
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var captured = new StringWriter();
        var errors = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(cwd);
            Console.SetOut(captured);
            Console.SetError(errors);
            var exit = CliRunner.Run(args);
            return (exit, captured.ToString(), errors.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}
