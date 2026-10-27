using Bellatrix.Cli.Commands;
using Bellatrix.Cli.Output;

namespace Bellatrix.Cli;

/// <summary>
/// Root dispatcher: `bellatrix &lt;command&gt;` (init, sessions, version, help) and `bellatrix &lt;desktop|web|mobile&gt;
/// &lt;verb&gt;`. Every invocation is resolved through <see cref="CommandRegistry"/>, the single declaration of the
/// command surface that `bellatrix help` also renders. The model-free CLI holds no credentials and runs no model;
/// it only shapes arguments, calls the shared engine, and prints the JSON contract.
/// </summary>
public static class CliRunner
{
    /// <summary>Every routable desktop verb (canonical names + aliases), derived from the command registry.</summary>
    public static IReadOnlySet<string> KnownVerbs => CommandRegistry.RoutedNames("desktop");

    /// <summary>Top-level commands that are not part of a domain (init, sessions, version, help), from the registry.</summary>
    public static IReadOnlySet<string> TopLevelCommands => CommandRegistry.RoutedNames(null);

    /// <summary>
    /// The tool's version: the assembly's informational version (the csproj <c>&lt;Version&gt;</c>) without the
    /// <c>+&lt;source-revision&gt;</c> suffix the SDK appends, falling back to the assembly version.
    /// </summary>
    public static string Version
    {
        get
        {
            var assembly = typeof(CliRunner).Assembly;
            var informational = assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
    }

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            // Output is always JSON; `--json` and `--no-interaction` are accepted-and-ignored (the CLI
            // is non-interactive and JSON is the contract). They pass through ParseOptions and never error.
            return JsonOutput.Error("usage", "Usage: " + CommandRegistry.Usage + " | bellatrix --version. See: bellatrix help");
        }

        // `bellatrix --version`: handled before any domain routing (`bellatrix version` routes like any command).
        if (args[0] is "--version")
        {
            return JsonOutput.Success(new { version = Version });
        }

        if (args[0] is "--help" or "-h")
        {
            args = new[] { "help" }.Concat(args.Skip(1)).ToArray();
        }

        // `bellatrix help <command>` / `bellatrix help <domain> [<verb>]`.
        if (args[0] is "help" && args.Length > 1 && !args[1].StartsWith('-'))
        {
            var helpVerb = args.Length > 2 && !args[2].StartsWith('-') ? args[2] : null;
            return RenderHelpTopic(args[1], helpVerb, ParseOptions(args.Skip(1)));
        }

        // Top-level commands (no domain): init / update-skills, sessions, version, help.
        if (CommandRegistry.Find(null, args[0]) is CommandSpec<CommandContext> top)
        {
            // Help on a command is always printed, never run: `init --help` must not scaffold anything.
            if (WantsHelp(top, args.Skip(1)))
            {
                return MetaCommands.RenderCommand(top, ParseOptions(args.Skip(1)).ContainsKey("json"));
            }

            return Execute(top.Handler, ParseOptions(args.Skip(1)));
        }

        var domain = args[0].ToLowerInvariant();
        if (!CommandRegistry.Domains.Contains(domain))
        {
            return JsonOutput.Error("unknown_domain",
                $"Unknown domain '{args[0]}'. Supported domains: 'desktop', 'web', 'mobile'. See: bellatrix help");
        }

        if (args.Length < 2)
        {
            var example = domain == "desktop" ? "list-apps" : "list-sessions";
            return JsonOutput.Error("usage", $"Missing <verb>. Example: bellatrix {domain} {example}");
        }

        var verb = args[1];
        var options = ParseOptions(args.Skip(2));

        if (verb is "help" or "--help" or "-h")
        {
            return args.Length > 2 && !args[2].StartsWith('-')
                ? RenderHelpTopic(domain, args[2], options)
                : MetaCommands.Render(domain, options.ContainsKey("json"));
        }

        var spec = CommandRegistry.Find(domain, verb);
        if (spec is not null && WantsHelp(spec, args.Skip(2)))
        {
            // Before any context is built: a domain context opens .bellatrix/sessions.db.
            return MetaCommands.RenderCommand(spec, options.ContainsKey("json"));
        }

        return spec switch
        {
            CommandSpec<CommandContext> desktop => Execute(desktop.Handler, options),
            CommandSpec<WebCommandContext> web => ExecuteWeb(web.Handler, options),
            CommandSpec<MobileCommandContext> mobile => ExecuteMobile(mobile.Handler, options),
            _ => JsonOutput.Error("unknown_verb", $"Unknown verb '{domain} {verb}'. See: bellatrix {domain} help"),
        };
    }

    /// <summary>
    /// Builds the per-invocation context and runs a handler under the shared error policy (driver
    /// unreachable → a "start it yourself" message, typed CLI errors, fail-loud otherwise).
    /// </summary>
    private static int Execute(Func<CommandContext, Task<int>> handler, Dictionary<string, string> options)
    {
        try
        {
            var ctx = new CommandContext(options);
            return handler(ctx).GetAwaiter().GetResult();
        }
        catch (CliException ex)
        {
            return JsonOutput.Error(ex.Code, ex.Message, ex.ExitCode);
        }
        catch (ArgumentException ex)
        {
            return JsonOutput.Error("bad_request", ex.Message);
        }
        catch (OpenQA.Selenium.WebDriverException ex)
        {
            // The driver/app is the user's responsibility — never start it.
            var serverUrl = Environment.GetEnvironmentVariable("McpSettings__ServerUrl") ?? "http://127.0.0.1:4723";
            return JsonOutput.Error("driver_unreachable",
                $"Could not reach the desktop driver. Start your driver at {serverUrl} and try again. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return JsonOutput.Error("error", ex.Message);
        }
    }

    /// <summary>
    /// Mobile counterpart of <see cref="Execute"/>: builds the per-invocation <see cref="MobileCommandContext"/>
    /// and runs a handler under the same error policy (Appium driver unreachable → a "start it yourself" message).
    /// Duplicate-per-domain by design — desktop's <see cref="Execute"/> is left untouched.
    /// </summary>
    private static int ExecuteMobile(Func<MobileCommandContext, Task<int>> handler, Dictionary<string, string> options)
    {
        try
        {
            var ctx = new MobileCommandContext(options);
            return handler(ctx).GetAwaiter().GetResult();
        }
        catch (CliException ex)
        {
            return JsonOutput.Error(ex.Code, ex.Message, ex.ExitCode);
        }
        catch (ArgumentException ex)
        {
            return JsonOutput.Error("bad_request", ex.Message);
        }
        catch (OpenQA.Selenium.WebDriverException ex)
        {
            return JsonOutput.Error("driver_unreachable",
                $"Could not reach the Appium server. Start your driver at {MobileCommandContext.DefaultAppiumUrl} and try again. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return JsonOutput.Error("error", ex.Message);
        }
    }

    /// <summary>
    /// Web counterpart of <see cref="Execute"/>: builds the per-invocation <see cref="WebCommandContext"/>
    /// and runs a handler under the same error policy (Selenium server unreachable → a "start it yourself" message).
    /// Duplicate-per-domain by design.
    /// </summary>
    private static int ExecuteWeb(Func<WebCommandContext, Task<int>> handler, Dictionary<string, string> options)
    {
        try
        {
            var ctx = new WebCommandContext(options);
            return handler(ctx).GetAwaiter().GetResult();
        }
        catch (CliException ex)
        {
            return JsonOutput.Error(ex.Code, ex.Message, ex.ExitCode);
        }
        catch (ArgumentException ex)
        {
            return JsonOutput.Error("bad_request", ex.Message);
        }
        catch (OpenQA.Selenium.WebDriverException ex)
        {
            return JsonOutput.Error("driver_unreachable",
                $"Could not reach the session's Selenium server or browser driver. If the session was started without --remote-url, its driver process has ended — start a new session; otherwise start your Selenium server and try again. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return JsonOutput.Error("error", ex.Message);
        }
    }

    /// <summary>
    /// True when a command's arguments ask for help: <c>--help</c> anywhere, or a bare <c>-h</c> / <c>help</c> that is
    /// not the value of a value-taking flag (so <c>type-text --text help</c> still types "help").
    /// </summary>
    internal static bool WantsHelp(CommandSpec spec, IEnumerable<string> tokens)
    {
        var switches = spec.Flags.Concat(Flags.Global).Where(f => f.IsSwitch).Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectsValue = false;
        foreach (var token in tokens)
        {
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (token.Equals("--help", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                expectsValue = !switches.Contains(token[2..]);
            }
            else if (!expectsValue && token is "-h" or "help")
            {
                return true;
            }
            else
            {
                expectsValue = false;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>bellatrix help &lt;command&gt;</c>, <c>bellatrix help &lt;domain&gt; [&lt;verb&gt;]</c> and
    /// <c>bellatrix &lt;domain&gt; help &lt;verb&gt;</c>: one command's or one domain's help, or an unknown-name error.
    /// </summary>
    private static int RenderHelpTopic(string topic, string? verb, Dictionary<string, string> options)
    {
        var json = options.ContainsKey("json");
        var domain = topic.ToLowerInvariant();
        if (CommandRegistry.Domains.Contains(domain))
        {
            if (verb is null)
            {
                return MetaCommands.Render(domain, json);
            }

            return CommandRegistry.Find(domain, verb) is { } verbSpec
                ? MetaCommands.RenderCommand(verbSpec, json)
                : JsonOutput.Error("unknown_verb", $"Unknown verb '{domain} {verb}'. See: bellatrix {domain} help");
        }

        return CommandRegistry.Find(null, topic) switch
        {
            { } spec => MetaCommands.RenderCommand(spec, json),
            null => JsonOutput.Error("unknown_domain",
                $"Unknown command or domain '{topic}'. See: bellatrix help"),
        };
    }

    /// <summary>
    /// Parses a flat `--key value` / `--flag` token stream into a case-insensitive map. A `--flag`
    /// with no following value (or followed by another `--key`) is recorded as "true".
    /// </summary>
    internal static Dictionary<string, string> ParseOptions(IEnumerable<string> tokens)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        foreach (var token in tokens)
        {
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (key is not null)
                {
                    result[key] = "true";
                }

                key = token[2..];
            }
            else if (key is not null)
            {
                result[key] = token;
                key = null;
            }
        }

        if (key is not null)
        {
            result[key] = "true";
        }

        return result;
    }
}
