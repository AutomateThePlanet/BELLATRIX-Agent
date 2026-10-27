using System.Text;
using Bellatrix.Cli.Output;
using Bellatrix.Cli.Scaffolding;

namespace Bellatrix.Cli.Commands;

/// <summary>One option a command reads. <see cref="IsSwitch"/> flags take no value (presence = on).</summary>
internal sealed record FlagSpec(string Name, string Description)
{
    public string? Value { get; init; }

    public bool IsSwitch { get; init; }

    public bool Required { get; init; }

    public string? Default { get; init; }

    public IReadOnlyList<string>? Allowed { get; init; }

    /// <summary>Set on a legacy spelling that is still read (e.g. <c>--xpath</c> for <c>--locator</c>).</summary>
    public string? DeprecatedAliasOf { get; init; }
}

/// <summary>
/// One routed command: its domain (<c>null</c> = top-level), canonical name, aliases, the flags it reads and the
/// success JSON it prints. The router, <c>bellatrix help</c> and the drift tests all read these specs, so the verb
/// surface is declared exactly once.
/// </summary>
internal abstract record CommandSpec(string? Domain, string Name, string Summary)
{
    public IReadOnlyList<FlagSpec> Flags { get; init; } = Array.Empty<FlagSpec>();

    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    public string Output { get; init; } = "";

    /// <summary>Set on a command kept only for compatibility: what to use instead.</summary>
    public string? DeprecatedUse { get; init; }

    /// <summary>The handler method's name (drift tests map it back to the source that reads the flags).</summary>
    public abstract string HandlerName { get; }

    public IEnumerable<string> RoutedNames => new[] { Name }.Concat(Aliases);
}

/// <summary>A <see cref="CommandSpec"/> bound to a handler over its domain's context type.</summary>
internal sealed record CommandSpec<TContext>(string? Domain, string Name, string Summary, Func<TContext, Task<int>> Handler)
    : CommandSpec(Domain, Name, Summary)
{
    public override string HandlerName => Handler.Method.Name;
}

/// <summary>Flag factories and the flag groups shared by several commands.</summary>
internal static class Flags
{
    public static FlagSpec Value(string name, string value, string description, bool required = false, string? @default = null, params string[] allowed)
        => new(name, description) { Value = value, Required = required, Default = @default, Allowed = allowed.Length > 0 ? allowed : null };

    public static FlagSpec Switch(string name, string description) => new(name, description) { IsSwitch = true };

    public static FlagSpec AliasOf(FlagSpec canonical, string name)
        => canonical with { Name = name, Required = false, Default = null, DeprecatedAliasOf = canonical.Name, Description = $"Legacy spelling of --{canonical.Name}." };

    /// <summary>Accepted by every command and ignored: output is always JSON and the CLI never prompts.</summary>
    public static readonly IReadOnlyList<FlagSpec> Global = new[]
    {
        Switch("json", "Accepted for compatibility; output is always JSON."),
        Switch("no-interaction", "Accepted and ignored; the CLI never prompts."),
    };

    public static readonly FlagSpec Session = Value("session", "<name>",
        "Logical session name, persisted per domain in .bellatrix/sessions.db so later invocations reattach by id.", @default: "default");

    public static readonly FlagSpec Out = Value("out", "<file.png>",
        "Write the PNG to this file instead of .bellatrix/screenshots/.");

    public static readonly FlagSpec ScreenshotInline = Switch("inline",
        "Return the base64 PNG inline instead of saving a file (context-heavy; avoid in agents).");

    public const string ScreenshotOutput = "{path, width, height} (saved PNG, default .bellatrix/screenshots/) | --inline: {screenshot (base64 PNG)}";

    public const string SnapshotOutput = "{path, elementCount, summary} (saved file reference) | --inline: [nodes] | --grep/--interactive-only: {nodes[], count, query}";

    public const string FindOutput = "{nodes[], count, query{mode, value}}";

    public static readonly IReadOnlyList<FlagSpec> SnapshotCapture = new[]
    {
        Switch("inline", "Return the full node array instead of the saved-file reference (context-heavy; avoid in agents)."),
        Value("grep", "<regex>", "Return only nodes whose attributes match the regex (filtered in-process)."),
        Switch("interactive-only", "Return only interactive nodes (filtered in-process)."),
    };

    public static FlagSpec SnapshotFile(string domain) => Value("snapshot", "<path>",
        $"Query a saved snapshot file instead of the live session (no driver needed). From `bellatrix {domain} snapshot`.");

    public static readonly FlagSpec FindGrep = Value("grep", "<regex>", "Nodes whose attributes match the regex.");
    public static readonly FlagSpec FindInteractive = Switch("interactive-only", "Only interactive nodes.");
    public static readonly FlagSpec FindText = Value("text", "<text>", "Nodes whose text contains the value.");

    private static readonly FlagSpec DesktopLocatorFlag = Value("locator", "<xpath>", "Element XPath (desktop locators are XPath only).", required: true);
    private static readonly FlagSpec WebLocatorFlag = Value("locator", "<selector>", "Element locator: a CSS selector or an XPath.", required: true);
    private static readonly FlagSpec MobileLocatorFlag = Value("locator", "<value>", "Element locator value for --strategy.", required: true);
    private static readonly FlagSpec WebStrategyFlag = Value("strategy", "<strategy>",
        "Locator strategy; default xpath when the locator starts with / or (/, else css.",
        allowed: new[] { "css", "xpath", "id", "class", "name", "tag", "linktext", "partiallinktext" });
    private static readonly FlagSpec MobileStrategyFlag = Value("strategy", "<strategy>",
        "Locator strategy; default xpath when the value starts with / or (/, else accessibilityid.",
        allowed: new[] { "accessibilityid", "id", "xpath", "classname", "androiduiautomator", "iospredicate", "iosclasschain" });

    public static readonly IReadOnlyList<FlagSpec> DesktopLocator = new[]
    {
        DesktopLocatorFlag,
        AliasOf(DesktopLocatorFlag, "xpath"),
        Value("strategy", "xpath", "Accepted for uniformity; only xpath is valid on desktop.", allowed: new[] { "xpath" }),
    };

    public static readonly IReadOnlyList<FlagSpec> WebLocator = new[] { WebLocatorFlag, AliasOf(WebLocatorFlag, "selector"), WebStrategyFlag };

    public static readonly IReadOnlyList<FlagSpec> MobileLocator = new[]
    {
        MobileLocatorFlag,
        AliasOf(MobileLocatorFlag, "locator-value"),
        MobileStrategyFlag,
        AliasOf(MobileStrategyFlag, "locator-type"),
    };

    public static IReadOnlyList<FlagSpec> Of(params object[] parts)
        => parts.SelectMany(p => p switch
        {
            FlagSpec f => new[] { f },
            IEnumerable<FlagSpec> many => many,
            _ => throw new ArgumentException("FlagSpec or FlagSpec list expected."),
        }).ToArray();
}

/// <summary>
/// The full command surface: top-level commands plus the desktop, web and mobile verbs. The router
/// (<see cref="CliRunner"/>) resolves every invocation here, and <c>bellatrix help [--json]</c> renders it.
/// </summary>
internal static class CommandRegistry
{
    public static readonly IReadOnlyList<string> Domains = new[] { "desktop", "web", "mobile" };

    public static readonly IReadOnlyDictionary<int, string> ExitCodes = new SortedDictionary<int, string>
    {
        [0] = "Success; the success JSON on stdout is the contract.",
        [1] = "Error; stdout is {\"error\":{\"code\",\"message\"}} (e.g. usage, unknown_verb, session_not_found, driver_unreachable).",
        [2] = "Usage error in agent or language selection (init: unknown/ambiguous --agent or --language).",
    };

    internal static readonly IReadOnlyList<FlagSpec> InitFlags = Flags.Of(
        Flags.Value("agent", "<a[,b,...]>", "AI agent(s) to scaffold for, comma-separated (windsurf = devin, agy = antigravity)." +
                                         "Default: the agents already scaffolded, else the one detected from repo markers.",
            allowed: AgentSelection.Names.ToArray()),
        Flags.AliasOf(Flags.Value("agent", "<a[,b,...]>", ""), "target"),
        Flags.Value("language", "<language>", "BELLATRIX flavor. Default: the recorded language, else detected from build files, else csharp.",
            allowed: LanguageSelection.Known.ToArray()),
        Flags.Value("domains", "<d[,d,...]>", "Domains to scaffold.", @default: "desktop,web,mobile", allowed: new[] { "desktop", "web", "mobile" }),
        Flags.Value("out", "<dir>", "Repository root to scaffold into.", @default: "current directory"),
        Flags.Switch("regenerate", "Overwrite files you edited (otherwise they are skipped and reported)."),
        Flags.AliasOf(Flags.Switch("regenerate", ""), "force"),
        Flags.Switch("dry-run", "Report what would be written; change nothing."),
        Flags.Value("template-source", "<dir>", "Render your own template folder (same layout as the built-in templates) instead of the BELLATRIX templates."),
        Flags.Value("prefix", "<prefix>", "Namespace for --template-source output (skills, agents, fragment, manifests). Default: inferred from the folder."));

    private const string InitOutput = "{agents[], language, domains[], emitted[], skipped[], removed[], registered, mcpRemoved[], perAgent{}, decisions[], unsupported[], warnings[], dryRun, ...}";

    public static readonly IReadOnlyList<CommandSpec<CommandContext>> TopLevel = new CommandSpec<CommandContext>[]
    {
        new(null, "init", "Scaffold the BELLATRIX skills, subagents and instruction fragment for your AI agent(s) into this repo.", ScaffoldCommand.Run)
        {
            Aliases = new[] { "update-skills" }, Flags = InitFlags, Output = InitOutput,
        },
        new(null, "sessions", "List the persisted sessions of every domain, or prune stale ones.", SessionsCommand.Run)
        {
            Flags = Flags.Of(
                Flags.Switch("list", "Report the sessions (the default)."),
                Flags.Switch("prune", "Delete sessions idle longer than the keep-alive TTL and stop their local web drivers.")),
            Output = "{sessions[{domain, logicalName, sessionId, driverType, serverUrl, lastActivityUtc}], count} | --prune: {pruned[], count}",
        },
        new(null, "version", "Print the CLI version (also: bellatrix --version).", MetaCommands.Version)
        {
            Output = "{version}",
        },
        new(null, "help", "Describe the commands (bellatrix help, or bellatrix <domain> help). --json for machine-readable output.", MetaCommands.Help)
        {
            Output = "{version, usage, exitCodes, globalFlags[], domains[], commands[]}",
        },
    };

    /// <summary>Every command, top-level first, then desktop, web, mobile.</summary>
    public static IEnumerable<CommandSpec> All
        => TopLevel.Cast<CommandSpec>().Concat(DesktopCommands.Specs).Concat(WebCommands.Specs).Concat(MobileCommands.Specs);

    public static IEnumerable<CommandSpec> For(string? domain)
        => All.Where(s => string.Equals(s.Domain, domain, StringComparison.OrdinalIgnoreCase));

    /// <summary>The command a (domain, verb) pair routes to — canonical name or alias — or null.</summary>
    public static CommandSpec? Find(string? domain, string verb)
        => For(domain).FirstOrDefault(s => s.RoutedNames.Contains(verb, StringComparer.OrdinalIgnoreCase));

    /// <summary>Every routable name (canonical + aliases) of a domain, or of the top level for <c>null</c>.</summary>
    public static IReadOnlySet<string> RoutedNames(string? domain)
        => new HashSet<string>(For(domain).SelectMany(s => s.RoutedNames), StringComparer.OrdinalIgnoreCase);

    public const string Usage = "bellatrix <init|sessions|version|help> [--flag value] | bellatrix <desktop|web|mobile> <verb> [--flag value] | bellatrix <domain> help";

    /// <summary>The help document: everything, or one domain's verbs.</summary>
    public static object HelpDocument(string? domain) => new
    {
        version = CliRunner.Version,
        usage = Usage,
        exitCodes = ExitCodes.ToDictionary(e => e.Key.ToString(), e => e.Value),
        globalFlags = Flags.Global.Select(FlagJson),
        domains = Domains,
        commands = (domain is null ? All : For(domain)).Select(CommandJson),
    };

    public static string HelpText(string? domain)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"bellatrix {CliRunner.Version} - model-free UI automation CLI (output is always JSON).");
        sb.AppendLine();
        sb.AppendLine("Usage: " + Usage);
        foreach (var group in (domain is null ? All : For(domain)).GroupBy(s => s.Domain))
        {
            sb.AppendLine();
            sb.AppendLine(group.Key is null ? "Commands:" : $"{group.Key} verbs (bellatrix {group.Key} <verb>):");
            foreach (var spec in group)
            {
                var name = (spec.Domain is null ? "" : spec.Domain + " ") + spec.Name;
                var aliases = spec.Aliases.Count > 0 ? $" (alias: {string.Join(", ", spec.Aliases)})" : "";
                var deprecated = spec.DeprecatedUse is null ? "" : $" [deprecated: use {spec.DeprecatedUse}]";
                sb.AppendLine($"  {name}{aliases}{deprecated}");
                sb.AppendLine($"      {spec.Summary}");
                foreach (var flag in spec.Flags)
                {
                    sb.AppendLine("      " + FlagText(flag));
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("Every command also accepts --json and --no-interaction (no effect).");
        sb.AppendLine("Exit codes: " + string.Join("; ", ExitCodes.Select(e => $"{e.Key} = {e.Value}")));
        sb.AppendLine("Machine-readable: bellatrix help --json (or bellatrix <domain> help --json).");
        return sb.ToString();
    }

    private static string FlagText(FlagSpec f)
    {
        var head = "--" + f.Name + (f.IsSwitch ? "" : " " + f.Value);
        var notes = new List<string>();
        if (f.Required) notes.Add("required");
        if (f.Default is not null) notes.Add("default: " + f.Default);
        if (f.Allowed is not null) notes.Add("one of: " + string.Join("|", f.Allowed));
        var suffix = notes.Count > 0 ? $" ({string.Join("; ", notes)})" : "";
        return $"{head,-30} {f.Description}{suffix}";
    }

    private static object FlagJson(FlagSpec f) => new
    {
        name = f.Name,
        value = f.Value,
        isSwitch = f.IsSwitch,
        required = f.Required,
        @default = f.Default,
        allowed = f.Allowed,
        deprecatedAliasOf = f.DeprecatedAliasOf,
        description = f.Description,
    };

    private static object CommandJson(CommandSpec s) => new
    {
        domain = s.Domain,
        name = s.Name,
        aliases = s.Aliases,
        deprecatedUse = s.DeprecatedUse,
        summary = s.Summary,
        flags = s.Flags.Select(FlagJson),
        output = s.Output,
    };
}

/// <summary>Top-level commands that only describe the CLI itself.</summary>
internal static class MetaCommands
{
    public static Task<int> Version(CommandContext ctx) => Task.FromResult(JsonOutput.Success(new { version = CliRunner.Version }));

    /// <summary>Top-level help (<c>bellatrix help</c>); the router passes the domain for <c>bellatrix &lt;domain&gt; help</c>.</summary>
    public static Task<int> Help(CommandContext ctx) => Task.FromResult(Render(null, ctx.Json));

    public static int Render(string? domain, bool json)
    {
        if (json)
        {
            return JsonOutput.Success(CommandRegistry.HelpDocument(domain));
        }

        Console.Out.Write(CommandRegistry.HelpText(domain));
        return 0;
    }
}
