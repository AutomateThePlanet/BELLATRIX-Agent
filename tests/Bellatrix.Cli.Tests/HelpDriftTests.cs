using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bellatrix.Cli.Commands;
using Xunit;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Drift guards over the command registry (<see cref="CommandRegistry"/>), the single declaration of the
/// command surface:
/// <list type="number">
/// <item>routed set == help set (every spec routes, names are unique, help lists every name and alias);</item>
/// <item>every flag a handler reads is declared, and every declared flag is read (source scan);</item>
/// <item>every <c>bellatrix &lt;domain&gt; &lt;verb&gt; --flag</c> written in the templates and the self-hosted
/// scaffold names a routed verb and a declared flag.</item>
/// </list>
/// </summary>
public class HelpDriftTests
{
    private static readonly string[] GlobalFlagNames = Flags.Global.Select(f => f.Name).ToArray();

    // ---------- 1. routed == help ----------

    [Fact]
    public void Registry_PinsTheSurface_PerDomain()
    {
        Assert.Equal(new[] { "init", "sessions", "version", "help" }, CommandRegistry.For(null).Select(s => s.Name));
        Assert.Equal(new[] { "show", "validate" }, CommandRegistry.For("config").Select(s => s.Name));
        Assert.Equal(20, CommandRegistry.For("desktop").Count()); // 19 verbs + deprecated scaffold
        Assert.Equal(24, CommandRegistry.For("web").Count());
        Assert.Equal(27, CommandRegistry.For("mobile").Count());
    }

    [Fact]
    public void Registry_RoutedNamesAreUnique_AndEverySpecHasAHandlerAndSummary()
    {
        foreach (var domain in new string?[] { null }.Concat(CommandRegistry.Groups))
        {
            var names = CommandRegistry.For(domain).SelectMany(s => s.RoutedNames).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        foreach (var spec in CommandRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(spec.HandlerName), spec.Name);
            Assert.False(string.IsNullOrWhiteSpace(spec.Summary), spec.Name);
            Assert.False(string.IsNullOrWhiteSpace(spec.Output), spec.Name);
            Assert.Equal(spec.Flags.Count, spec.Flags.Select(f => f.Name).Distinct().Count());
            Assert.All(spec.Flags.Where(f => f.DeprecatedAliasOf is not null),
                alias => Assert.Contains(spec.Flags, f => f.Name == alias.DeprecatedAliasOf));
        }
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("web")]
    [InlineData("mobile")]
    public void EveryRoutedVerb_IsInHelp_AndDispatchesToItsSpec(string domain)
    {
        var (exit, stdout) = Capture(() => CliRunner.Run(new[] { domain, "help", "--json" }));
        Assert.Equal(0, exit);
        var help = JsonDocument.Parse(stdout).RootElement.GetProperty("commands").EnumerateArray()
            .SelectMany(c => new[] { c.GetProperty("name").GetString()! }
                .Concat(c.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()!)))
            .ToHashSet();

        Assert.True(CommandRegistry.RoutedNames(domain).SetEquals(help));
        var known = domain switch
        {
            "desktop" => CliRunner.KnownVerbs,
            "web" => WebCommands.KnownVerbs,
            _ => MobileCommands.KnownVerbs,
        };
        Assert.True(known.SetEquals(help));
        foreach (var name in help)
        {
            Assert.Same(CommandRegistry.Find(domain, name), CommandRegistry.Find(domain, name.ToUpperInvariant()));
            Assert.NotNull(CommandRegistry.Find(domain, name));
        }
    }

    [Fact]
    public void TopLevelHelp_ListsEveryCommand_AndIsValidJson()
    {
        var (exit, stdout) = Capture(() => CliRunner.Run(new[] { "help", "--json" }));
        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(stdout).RootElement;

        Assert.Equal(CliRunner.Version, root.GetProperty("version").GetString());
        Assert.Equal(new[] { "0", "1", "2" }, root.GetProperty("exitCodes").EnumerateObject().Select(p => p.Name));
        Assert.Equal(CommandRegistry.All.Count(), root.GetProperty("commands").GetArrayLength());
        Assert.Equal(GlobalFlagNames, root.GetProperty("globalFlags").EnumerateArray().Select(f => f.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlainHelp_RendersTheSameCommands()
    {
        var (exit, text) = Capture(() => CliRunner.Run(new[] { "help" }));
        Assert.Equal(0, exit);
        foreach (var spec in CommandRegistry.All)
        {
            Assert.Contains((spec.Domain is null ? "" : spec.Domain + " ") + spec.Name, text);
        }

        var (_, domainText) = Capture(() => CliRunner.Run(new[] { "web", "--help" }));
        Assert.Contains("web navigate", domainText);
        Assert.DoesNotContain("desktop start-session", domainText);
    }

    [Fact]
    public void Help_ListsNoHiddenCommands()
    {
        var (_, json) = Capture(() => CliRunner.Run(new[] { "help", "--json" }));
        var (_, text) = Capture(() => CliRunner.Run(new[] { "help" }));
        foreach (var output in new[] { json, text })
        {
            Assert.DoesNotContain("-init", output.Replace("bellatrix init", ""), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"source\"", output);
        }
    }

    [Fact]
    public void TopLevelCommands_DoNotCreateTheSessionStore()
    {
        var original = Directory.GetCurrentDirectory();
        var temp = Directory.CreateTempSubdirectory("bellatrix-help-").FullName;
        try
        {
            Directory.SetCurrentDirectory(temp);
            Assert.Equal(0, Capture(() => CliRunner.Run(new[] { "help", "--json" })).exit);
            Assert.Equal(0, Capture(() => CliRunner.Run(new[] { "web", "help" })).exit);
            Assert.Equal(0, Capture(() => CliRunner.Run(new[] { "init", "--agent", "claude", "--language", "csharp", "--dry-run" })).exit);

            Assert.False(File.Exists(Path.Combine(temp, ".bellatrix", "sessions.db")));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(temp, recursive: true);
        }
    }

    // ---------- 2. flags read == flags declared ----------

    /// <summary>Context helpers and the options they read, per context type (verified by the next test).</summary>
    private static readonly Dictionary<string, Dictionary<string, string[]>> HelperReads = new()
    {
        ["CommandContext"] = new()
        {
            ["Locator"] = new[] { "locator", "xpath", "strategy" },
            ["SessionName"] = new[] { "session" },
            ["MountSession"] = new[] { "session" },
            ["Json"] = new[] { "json" },
        },
        ["WebCommandContext"] = new()
        {
            ["Locator"] = new[] { "locator", "selector" },
            ["Strategy"] = new[] { "strategy", "locator", "selector" },
            ["SessionName"] = new[] { "session" },
            ["MountSession"] = new[] { "session" },
            ["Json"] = new[] { "json" },
        },
        ["MobileCommandContext"] = new()
        {
            ["LocatorValue"] = new[] { "locator", "locator-value" },
            ["LocatorType"] = new[] { "strategy", "locator-type", "locator", "locator-value" },
            ["SessionName"] = new[] { "session" },
            ["MountSession"] = new[] { "session" },
            ["Json"] = new[] { "json" },
        },
    };

    [Theory]
    [InlineData("CommandContext")]
    [InlineData("WebCommandContext")]
    [InlineData("MobileCommandContext")]
    public void ContextHelperMap_CoversEveryOptionTheContextReads(string context)
    {
        var source = File.ReadAllText(Path.Combine(CliSource(), context + ".cs"));
        var read = OptionReads(source).ToHashSet();

        Assert.True(read.SetEquals(HelperReads[context].Values.SelectMany(v => v)),
            $"{context} reads [{string.Join(", ", read.Order())}]; update HelperReads.");
    }

    public static TheoryData<string, string> Commands()
    {
        var data = new TheoryData<string, string>();
        foreach (var spec in CommandRegistry.All)
        {
            data.Add(spec.Domain ?? "", spec.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryFlagAHandlerReads_IsDeclared_AndViceVersa(string domain, string name)
    {
        var spec = CommandRegistry.Find(domain == "" ? null : domain, name)!;
        var (file, context) = spec switch
        {
            CommandSpec<CommandContext> when spec.HandlerName == "Run" && name is "init" or "scaffold" => ("Commands/ScaffoldCommand.cs", "CommandContext"),
            CommandSpec<CommandContext> when name == "sessions" => ("Commands/SessionsCommand.cs", "CommandContext"),
            CommandSpec<CommandContext> when domain == "config" => ("Commands/ConfigCommand.cs", "CommandContext"),
            CommandSpec<CommandContext> when domain == "" => ("Commands/CommandRegistry.cs", "CommandContext"),
            CommandSpec<CommandContext> => ("Commands/DesktopCommands.cs", "CommandContext"),
            CommandSpec<WebCommandContext> => ("Commands/WebCommands.cs", "WebCommandContext"),
            _ => ("Commands/MobileCommands.cs", "MobileCommandContext"),
        };
        var source = File.ReadAllText(Path.Combine(CliSource(), file)).Replace("\r\n", "\n");
        var body = file.EndsWith("ScaffoldCommand.cs", StringComparison.Ordinal) ? source : MethodBody(source, spec.HandlerName);

        var read = OptionReads(body).ToHashSet();
        foreach (var helper in HelperReads[context])
        {
            if (Regex.IsMatch(body, @"ctx\." + helper.Key + @"\b"))
            {
                read.UnionWith(helper.Value);
            }
        }

        read.ExceptWith(GlobalFlagNames);
        var declared = spec.Flags.Select(f => f.Name).ToHashSet();
        if (name == "sessions")
        {
            read.Add("list"); // accepted no-op: the default already lists
        }

        Assert.True(read.SetEquals(declared),
            $"{domain} {name}: reads [{string.Join(", ", read.Order())}] but declares [{string.Join(", ", declared.Order())}].");
    }

    // ---------- 3. templates use only routed verbs and declared flags ----------

    private static readonly Regex CommandUse = new(
        @"bellatrix\s+(?:(?<domain>desktop|web|mobile|config)\s+(?<verb>[a-z][a-z-]*)|(?<verb>init|update-skills|sessions|version|help))\b(?<rest>[^`\n]*)",
        RegexOptions.Compiled);

    public static TheoryData<string> ScannedFiles()
    {
        var root = RepoRoot();
        var data = new TheoryData<string>();
        var dirs = new[]
        {
            Path.Combine(root, "src", "Bellatrix.Cli", "Templates"),
            Path.Combine(root, "src", "Bellatrix.Cli", "Package"),
            Path.Combine(root, "docs"),
        };
        foreach (var file in dirs.Where(Directory.Exists)
                     .SelectMany(d => Directory.EnumerateFiles(d, "*.md", SearchOption.AllDirectories))
                     .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                     .Order(StringComparer.Ordinal))
        {
            data.Add(file);
        }

        foreach (var file in new[] { "README.md", "CONTRIBUTING.md", "AGENTS.md" })
        {
            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ScannedFiles))]
    public void EveryDocumentedCommand_UsesARoutedVerbAndDeclaredFlags(string relativePath)
    {
        var problems = new List<string>();
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), relativePath));
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (Match m in CommandUse.Matches(lines[i]))
            {
                var domain = m.Groups["domain"].Success ? m.Groups["domain"].Value : null;
                var verb = m.Groups["verb"].Value;
                if (verb == "help")
                {
                    continue;
                }

                var spec = CommandRegistry.Find(domain, verb);
                if (spec is null)
                {
                    problems.Add($"{relativePath}:{i + 1}: unknown command 'bellatrix {domain} {verb}'".Replace("  ", " "));
                    continue;
                }

                // A legacy spelling is fine only where the line says it is one ("legacy", "still works", "deprecated", "alias").
                var explainsLegacy = Regex.IsMatch(lines[i], @"legacy|still works|deprecated|alias", RegexOptions.IgnoreCase);
                if (spec.DeprecatedUse is not null && !explainsLegacy)
                {
                    problems.Add($"{relativePath}:{i + 1}: 'bellatrix {domain} {verb}' is deprecated; use {spec.DeprecatedUse}".Replace("  ", " "));
                }

                // Flags up to the next command on the same line; skip <placeholders> and quoted values.
                var rest = m.Groups["rest"].Value;
                var next = rest.IndexOf("bellatrix ", StringComparison.Ordinal);
                if (next >= 0)
                {
                    rest = rest[..next];
                }

                rest = Regex.Replace(rest, "\"[^\"]*\"|'[^']*'|<[^>]*>", " ");
                foreach (Match f in Regex.Matches(rest, @"(?<![\w-])--([a-z][a-z-]*)"))
                {
                    var flag = f.Groups[1].Value;
                    var declared = spec.Flags.FirstOrDefault(x => x.Name == flag);
                    if (!GlobalFlagNames.Contains(flag) && declared is null)
                    {
                        problems.Add($"{relativePath}:{i + 1}: 'bellatrix {domain} {verb}' has no --{flag}".Replace("  ", " "));
                    }
                    else if (declared?.DeprecatedAliasOf is { } canonical && !explainsLegacy)
                    {
                        problems.Add($"{relativePath}:{i + 1}: --{flag} is a legacy spelling; use --{canonical}");
                    }
                }

                // The parser reads only `--key value`: `--key=value` and bare `key=value` words are silently dropped.
                var expectsValue = false;
                foreach (var token in rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token.StartsWith("--", StringComparison.Ordinal))
                    {
                        if (token.Contains('='))
                        {
                            problems.Add($"{relativePath}:{i + 1}: '{token}': the CLI reads only '--flag value', not '--flag=value'");
                        }

                        var name = token[2..].Split('=')[0];
                        expectsValue = spec.Flags.Concat(Flags.Global).FirstOrDefault(x => x.Name == name) is { IsSwitch: false };
                        continue;
                    }

                    if (!expectsValue && Regex.IsMatch(token, @"^[A-Za-z][\w.-]*="))
                    {
                        problems.Add($"{relativePath}:{i + 1}: bare '{token}': the CLI ignores key=value words; use '--flag value'");
                    }

                    expectsValue = false;
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("web")]
    [InlineData("mobile")]
    public void Fragment_DomainSection_NamesEveryCanonicalVerb(string domain)
    {
        var fragment = File.ReadAllText(Path.Combine(CliSource(), "Templates", "bellatrix-instructions.md"));
        var section = Regex.Match(fragment, $"<!-- bellatrix:domain={domain} -->(?<body>.*?)<!-- /bellatrix:domain -->", RegexOptions.Singleline);
        Assert.True(section.Success, domain);
        var missing = CommandRegistry.For(domain)
            .Where(s => s.DeprecatedUse is null)
            .Select(s => s.Name)
            .Where(verb => !Regex.IsMatch(section.Groups["body"].Value, $@"(?:[`/]|bellatrix {domain} ){Regex.Escape(verb)}[`/ ]"))
            .ToList();

        Assert.True(missing.Count == 0, $"{domain} section of the fragment omits: {string.Join(", ", missing)}");
    }

    // ---------- helpers ----------

    private static IEnumerable<string> OptionReads(string source)
        => Regex.Matches(source, @"(?:Opt|RequireOpt)\(""([\w-]+)""\)|(?:ContainsKey|TryGetValue)\(""([\w-]+)""")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);

    /// <summary>The body of <c>static … Task&lt;int&gt; Name(…)</c>: a braced block or an expression body.</summary>
    private static string MethodBody(string source, string method)
    {
        var m = Regex.Match(source, @"static (?:async )?Task<int> " + Regex.Escape(method) + @"\(\w+ ctx\)\s*(?<kind>=>|\{)");
        Assert.True(m.Success, $"handler '{method}' not found");
        var start = m.Groups["kind"].Index;
        if (m.Groups["kind"].Value == "=>")
        {
            return source[start..source.IndexOf(";\n", start, StringComparison.Ordinal)];
        }

        var depth = 0;
        for (var i = start; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source[start..(i + 1)];
            }
        }

        throw new InvalidOperationException("unbalanced braces in " + method);
    }

    private static (int exit, string stdout) Capture(Func<int> run)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            return (run(), writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string CliSource() => Path.Combine(RepoRoot(), "src", "Bellatrix.Cli");

    private static string RepoRoot([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
