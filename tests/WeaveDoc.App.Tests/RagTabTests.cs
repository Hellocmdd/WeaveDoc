using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using WeaveDoc.App.Views;
using Xunit;

namespace WeaveDoc.App.Tests;

public class RagTabTests
{
    [AvaloniaFact]
    public void RagTab_ConstructsWithoutInitialization()
    {
        var tab = new RagTab();
        var window = new Window { Content = tab };
        window.Show();

        Assert.NotNull(tab.FindControl<TextBox>("InputBox"));
        Assert.Single(tab.GetVisualDescendants().OfType<ListBox>());
    }
}
