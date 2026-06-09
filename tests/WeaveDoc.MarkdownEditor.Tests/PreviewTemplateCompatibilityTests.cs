using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Controls.Web;

namespace WeaveDoc.MarkdownEditor.Tests;

[TestFixture]
public class PreviewTemplateCompatibilityTests
{
    [Test]
    public void PreviewTemplate_AvoidsWebKitGtkUnsupportedHelpers()
    {
        var templatePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Assets",
            "preview-template.html");
        var template = File.ReadAllText(templatePath);

        Assert.That(template, Does.Not.Contain(".forEach(function"));
        Assert.That(template, Does.Not.Contain(".closest("));
        Assert.That(template, Does.Not.Contain("Object.assign"));
        Assert.That(template, Does.Contain("function forEachNode"));
        Assert.That(template, Does.Contain("function closestElement"));
        Assert.That(template, Does.Contain("function mergeOptions"));
    }

    [Test]
    public void WebViewBridge_BuildReceiveScript_AvoidsOptionalChaining()
    {
        var script = WebViewBridge.BuildReceiveScript("{\"Type\":\"setContent\",\"Data\":\"# Test\"}");

        Assert.That(script, Does.Not.Contain("?."));
        Assert.That(script, Does.Contain("receiveFromHost"));
        Assert.That(script, Does.Contain("typeof globalThis.weaveDocBridge.receiveFromHost === \"function\""));
    }
}
