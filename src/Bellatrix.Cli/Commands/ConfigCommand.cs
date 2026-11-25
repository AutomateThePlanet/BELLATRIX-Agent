using System.Text.Json.Nodes;
using Bellatrix.Cli.Config;
using Bellatrix.Cli.Output;

namespace Bellatrix.Cli.Commands;

/// <summary>
/// <c>bellatrix config show|validate</c>: read and check <c>.bellatrix/config.json</c> (the project settings
/// <c>bellatrix init</c> records). The BELLATRIX skills run <c>validate</c> before they build or write code; a user
/// runs it after editing the file.
/// </summary>
internal static class ConfigCommand
{
    public static readonly IReadOnlyList<CommandSpec<CommandContext>> Specs = new CommandSpec<CommandContext>[]
    {
        new("config", "show", "Print .bellatrix/config.json with the resolved values per domain (project folder, test-case folder).", Show)
        {
            Flags = Flags.Of(Flags.Value("out", "<dir>", "Repository root.", @default: "current directory")),
            Output = "{path, exists, valid, config, resolved{<domain>{project, projectDir, testCasesDir}}, errors[{path, message}], warnings[{path, message}]}",
        },
        new("config", "validate", "Check .bellatrix/config.json against its schema (exit 1 with the problems when it is missing or invalid).", Validate)
        {
            Flags = Flags.Of(Flags.Value("out", "<dir>", "Repository root.", @default: "current directory")),
            Output = "{valid, path, warnings[{path, message}]} | error config_missing / config_invalid with details{errors[{path, message}]}",
        },
    };

    private static Task<int> Show(CommandContext ctx)
    {
        var root = ctx.Opt("out") ?? Directory.GetCurrentDirectory();
        var loaded = BellatrixConfig.Load(root);
        var resolved = new JsonObject();
        if (loaded.Document is { } doc && loaded.Valid)
        {
            foreach (var domain in BellatrixConfig.Domains)
            {
                if (doc["domains"]?[domain] is null)
                {
                    continue;
                }

                var project = BellatrixConfig.ProjectOf(doc, domain);
                resolved[domain] = new JsonObject
                {
                    ["project"] = project?.ToJson(),
                    ["projectDir"] = project?.Dir,
                    ["testCasesDir"] = BellatrixConfig.TestCasesDirOf(doc, domain),
                };
            }
        }

        return Task.FromResult(JsonOutput.Success(new
        {
            path = BellatrixConfig.RelativePath,
            exists = loaded.Exists,
            valid = loaded.Valid,
            config = loaded.Document,
            resolved,
            errors = loaded.Errors,
            warnings = loaded.Valid ? BellatrixConfig.DiskWarnings(root, loaded.Document!) : Array.Empty<ConfigProblem>(),
        }));
    }

    private static Task<int> Validate(CommandContext ctx)
    {
        var root = ctx.Opt("out") ?? Directory.GetCurrentDirectory();
        var loaded = BellatrixConfig.Load(root);
        if (!loaded.Exists)
        {
            return Task.FromResult(JsonOutput.Error("config_missing",
                $"{BellatrixConfig.RelativePath} does not exist. Run `bellatrix init` (or ask the user for the test project and create it).",
                details: new { errors = loaded.Errors }));
        }

        if (!loaded.Valid)
        {
            return Task.FromResult(JsonOutput.Error("config_invalid",
                $"{BellatrixConfig.RelativePath} is invalid: " + string.Join("; ", loaded.Errors.Select(e => $"{e.path}: {e.message}")),
                details: new { errors = loaded.Errors }));
        }

        return Task.FromResult(JsonOutput.Success(new
        {
            valid = true,
            path = BellatrixConfig.RelativePath,
            warnings = BellatrixConfig.DiskWarnings(root, loaded.Document!),
        }));
    }
}
