using Bellatrix.Cli.Scaffolding;
using Bellatrix.Cli.Scaffolding.Emitters;
using Xunit;

namespace Bellatrix.Cli.Tests;

public class TemplateCatalogTests
{
    [Fact]
    public void Load_ReturnsAllVendoredTemplates()
    {
        var items = TemplateCatalog.Load();

        // 12 skills each for desktop + mobile + web; 2 desktop + 1 mobile + 1 web agents; 1 fragment.
        Assert.Equal(36, items.Count(i => i.Kind == TemplateKind.Skill));
        Assert.Equal(6, items.Count(i => i.Kind == TemplateKind.Agent));
        Assert.Equal(1, items.Count(i => i.Kind == TemplateKind.Fragment));
        Assert.Equal(43, items.Count);
    }

    [Fact]
    public void NoTemplateBody_ContainsDesktopUnderscoreTokens()
    {
        foreach (var item in TemplateCatalog.Load())
        {
            Assert.DoesNotContain("desktop_", item.Body);
        }
    }

    [Fact]
    public void NoTemplateBody_UsesGenericWebPage()
    {
        // BELLATRIX C# `WebPage` is non-generic (Bellatrix.Web/pageobjects/WebPage.cs); `WebPage<App>` won't compile.
        foreach (var item in TemplateCatalog.Load())
        {
            Assert.DoesNotContain("WebPage<", item.Body);
        }
    }

    [Fact]
    public void WebTemplates_UseLowercaseCreateByXpath()
    {
        // Bellatrix.Web exposes only `CreateByXpath`; `CreateByXPath` exists in desktop/mobile. The single allowed
        // web mention of `CreateByXPath<` is the WRONG→CORRECT row that also names `CreateByXpath<`.
        foreach (var item in TemplateCatalog.Load().Where(i => i.Slug.StartsWith("bellatrix-web-", StringComparison.Ordinal)))
        {
            foreach (var line in item.Body.Split('\n').Where(l => l.Contains("CreateByXPath<", StringComparison.Ordinal)))
            {
                Assert.Contains("CreateByXpath<", line);
            }
        }
    }

    [Fact]
    public void SkillsAndAgents_HaveKebabNameAndDescription()
    {
        foreach (var item in TemplateCatalog.Load().Where(i => i.Kind != TemplateKind.Fragment))
        {
            Assert.StartsWith("bellatrix-", item.Name);        // bellatrix-desktop-* / bellatrix-mobile-*
            Assert.DoesNotContain(" ", item.Name);            // kebab, never the space-cased source title
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
        }
    }

    [Fact]
    public void GeneratorAgent_IsRenamedToPrefixedUniqueSlug()
    {
        var agent = TemplateCatalog.Load().Single(i =>
            i.Kind == TemplateKind.Agent && i.Slug == "bellatrix-desktop-system-tests-generator");

        Assert.Equal("bellatrix-desktop-system-tests-generator", agent.Name);
        // Must not collide with Spectra's agent namespace.
        Assert.NotEqual("spectra-execution", agent.Name);
        Assert.NotEqual("spectra-generation", agent.Name);
        Assert.NotEqual("spectra-critic", agent.Name);
    }

    [Fact]
    public void Fragment_Claude_HasNoFrontmatter()
    {
        var fragment = TemplateCatalog.Load().Single(i => i.Kind == TemplateKind.Fragment);
        var content = new ClaudeEmitter().RenderFragment(TemplateCatalog.FilterDomainBlocks(fragment.Body, null), "bellatrix");

        Assert.False(content.StartsWith("---"), "fragment is @imported markdown, not a frontmatter skill");
        Assert.Contains("bellatrix desktop", content);
    }

    [Fact]
    public void Fragment_KeepsOnlySelectedDomains_AndStripsMarkers()
    {
        var fragment = TemplateCatalog.Load().Single(i => i.Kind == TemplateKind.Fragment);
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "web", "desktop" };

        var content = TemplateCatalog.FilterDomainBlocks(fragment.Body, domains);

        Assert.StartsWith("# BELLATRIX Automation", content);
        Assert.Contains("## Desktop domain", content);
        Assert.Contains("## Web domain", content);
        Assert.DoesNotContain("## Mobile domain", content);
        Assert.DoesNotContain("bellatrix-mobile-locator-specialist", content);
        Assert.DoesNotContain("<!-- bellatrix:", content);
        Assert.DoesNotContain("<!-- /bellatrix:", content);
        Assert.Contains("MANDATORY: delegate UI-state reads", content); // shared intro always kept
    }

    [Fact]
    public void Fragment_AllDomainsByDefault()
    {
        var fragment = TemplateCatalog.Load().Single(i => i.Kind == TemplateKind.Fragment);

        var content = TemplateCatalog.FilterDomainBlocks(fragment.Body, null);

        Assert.Contains("## Desktop domain", content);
        Assert.Contains("## Web domain", content);
        Assert.Contains("## Mobile domain", content);
        Assert.DoesNotContain("<!-- bellatrix:", content);
        Assert.DoesNotContain("\n\n\n", content); // removed markers leave no stray blank runs
    }

    [Fact]
    public void Fragment_IsProviderNeutral_AndMandatesFindFallback()
    {
        var body = TemplateCatalog.Load().Single(i => i.Kind == TemplateKind.Fragment).Body;

        Assert.DoesNotContain(".claude/", body);
        Assert.DoesNotContain("CLAUDE.md", body);
        Assert.Contains("whenever delegation is unavailable", body);
        Assert.Contains("Never open the snapshot file yourself", body);
    }

    [Fact]
    public void Templates_HaveNoProviderSpecificFileReferences()
    {
        foreach (var item in TemplateCatalog.Load())
        {
            Assert.DoesNotContain("#file:", item.Body);
            Assert.DoesNotContain(".github/prompts", item.Body);
        }
    }

    [Fact]
    public void Compose_WholeAndNamedSections_AreLiteralIncludes()
    {
        Assert.Equal("# T\nCODE\nend\n", TemplateCatalog.Compose("# T\n{{> lang}}\nend\n", "CODE\n"));

        const string lang = "<!-- lang:a -->\nAAA\n<!-- lang:b -->\nBBB\n";
        Assert.Equal("x\nAAA\ny\nBBB\n", TemplateCatalog.Compose("x\n{{> lang:a}}\ny\n{{> lang:b}}\n", lang));
        Assert.Throws<InvalidOperationException>(() => TemplateCatalog.Compose("{{> lang:missing}}\n", lang));
        Assert.Equal("agnostic\n", TemplateCatalog.Compose("agnostic\n", "ignored"));
    }

    [Fact]
    public void Load_UnknownLanguage_KeepsOnlyAgnosticTopics_AndReportsGaps()
    {
        var (items, unsupported) = TemplateCatalog.LoadWithGaps(language: "cobol");

        Assert.Contains(items, i => i.Slug == "bellatrix-desktop-execute-spectra-test");      // agnostic
        Assert.Contains(items, i => i.Slug == "bellatrix-web-locator-specialist");            // agnostic agent
        Assert.Contains("bellatrix-desktop-components", unsupported);                         // needs a lang part
        Assert.DoesNotContain(items, i => i.Slug == "bellatrix-desktop-components");
        Assert.All(items, i => Assert.DoesNotContain("{{> lang", i.Body));
    }

    [Fact]
    public void Load_CSharp_ResolvesEveryMarker()
    {
        var (items, unsupported) = TemplateCatalog.LoadWithGaps(language: "csharp");

        Assert.Empty(unsupported);
        Assert.Contains("csharp", TemplateCatalog.Languages);
        Assert.All(items, i => Assert.DoesNotContain("{{> lang", i.Body));
        Assert.All(items, i => Assert.DoesNotContain("<!-- lang:", i.Body));
    }

    [Fact]
    public void FilterDomainBlocks_TextWithoutMarkers_IsUnchanged()
    {
        const string custom = "# Custom templates\r\n\r\nNo domain markers here.\r\n";
        Assert.Equal(custom, TemplateCatalog.FilterDomainBlocks(custom, new HashSet<string> { "web" }));
    }
}
