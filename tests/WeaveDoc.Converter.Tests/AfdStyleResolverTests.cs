using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;
using Xunit;

namespace WeaveDoc.Converter.Tests;

public class AfdStyleResolverTests
{
    [Fact]
    public void ResolveEffectiveStyle_AddsImplicitHeadingNumbering()
    {
        var style = new AfdStyleDefinition { DisplayName = "一级标题" };
        var numbering = new AfdNumbering
        {
            HeadingNumbering = new AfdHeadingNumbering
            {
                StartNumId = 1001,
                Levels = [new AfdNumberLevel { Level = 0 }]
            }
        };

        var resolved = AfdStyleResolver.ResolveEffectiveStyle("heading1", style, numbering);

        Assert.NotNull(resolved.Numbering);
        Assert.Equal(1001, resolved.Numbering!.NumId);
        Assert.Equal(0, resolved.Numbering.Level);
    }

    [Fact]
    public void ResolveEffectiveStyle_UsesHeadingInstanceWhenPresent()
    {
        var numbering = new AfdNumbering
        {
            HeadingNumbering = new AfdHeadingNumbering
            {
                StartNumId = 1001,
                Levels = [new AfdNumberLevel { Level = 1 }]
            },
            Instances =
            {
                ["heading"] = new AfdNumberingInstance { NumId = 41, Kind = "heading" }
            }
        };

        var resolved = AfdStyleResolver.ResolveEffectiveStyle("heading2", new AfdStyleDefinition(), numbering);

        Assert.Equal(41, resolved.Numbering!.NumId);
        Assert.Equal(1, resolved.Numbering.Level);
    }

    [Fact]
    public void ResolveEffectiveStyle_KeepsExplicitNumbering()
    {
        var style = new AfdStyleDefinition
        {
            Numbering = new AfdParagraphNumbering { NumId = 7, Level = 0 }
        };
        var numbering = new AfdNumbering
        {
            HeadingNumbering = new AfdHeadingNumbering
            {
                StartNumId = 1001,
                Levels = [new AfdNumberLevel { Level = 0 }]
            }
        };

        var resolved = AfdStyleResolver.ResolveEffectiveStyle("heading1", style, numbering);

        Assert.Equal(7, resolved.Numbering!.NumId);
    }

    [Fact]
    public void ResolveEffectiveStyle_DoesNotAddNumberingWhenLevelMissing()
    {
        var numbering = new AfdNumbering
        {
            HeadingNumbering = new AfdHeadingNumbering
            {
                StartNumId = 1001,
                Levels = [new AfdNumberLevel { Level = 0 }]
            }
        };

        var resolved = AfdStyleResolver.ResolveEffectiveStyle("heading2", new AfdStyleDefinition(), numbering);

        Assert.Null(resolved.Numbering);
    }
}
