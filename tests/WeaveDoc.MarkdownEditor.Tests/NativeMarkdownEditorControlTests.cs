using System;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using AvaloniaEdit;
using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.MarkdownEditor.Tests;

[TestFixture]
public class NativeMarkdownEditorControlTests
{
    [AvaloniaTest]
    public void SetContent_UpdatesEditorAndStyledPropertyWithoutContentEdited()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        var eventCount = 0;
        control.ContentEdited += (_, _) => eventCount++;

        control.SetContent("# Loaded");
        control.SetContent("# Loaded");

        Assert.That(control.EditorContent, Is.EqualTo("# Loaded"));
        Assert.That(control.GetContent(), Is.EqualTo("# Loaded"));
        Assert.That(control.HasUnsyncedContent, Is.False);
        Assert.That(textEditor.Text, Is.EqualTo("# Loaded"));
        Assert.That(eventCount, Is.Zero);
    }

    [AvaloniaTest]
    public void UserTextChange_KeepsSnapshotAndMarksUnsyncedWithContentEdited()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        var eventCount = 0;
        EventArgs? eventArgs = null;
        control.SetContent("# Loaded");
        control.ContentEdited += (_, args) =>
        {
            eventCount++;
            eventArgs = args;
        };

        textEditor.Text = "# Edited";

        Assert.That(control.EditorContent, Is.EqualTo("# Loaded"));
        Assert.That(control.GetContent(), Is.EqualTo("# Edited"));
        Assert.That(control.HasUnsyncedContent, Is.True);
        Assert.That(eventArgs, Is.SameAs(EventArgs.Empty));
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void EditorContentSnapshotUpdate_AppliesTextAndClearsUnsyncedWithoutContentEdited()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        control.SetContent("# Loaded");
        textEditor.Text = "# Dirty";
        Assert.That(control.HasUnsyncedContent, Is.True);

        var eventCount = 0;
        control.ContentEdited += (_, _) => eventCount++;

        control.EditorContent = "# From binding";
        control.EditorContent = "# From binding";

        Assert.That(control.EditorContent, Is.EqualTo("# From binding"));
        Assert.That(textEditor.Text, Is.EqualTo("# From binding"));
        Assert.That(control.GetContent(), Is.EqualTo("# From binding"));
        Assert.That(control.HasUnsyncedContent, Is.False);
        Assert.That(eventCount, Is.Zero);
    }

    [AvaloniaTest]
    public void EditorContent_DefaultBindingModeIsOneWaySnapshot()
    {
        var metadata = NativeMarkdownEditorControl.EditorContentProperty
            .GetMetadata(typeof(NativeMarkdownEditorControl));

        Assert.That(metadata.DefaultBindingMode, Is.EqualTo(BindingMode.OneWay));
    }

    [AvaloniaTest]
    public void EditorChrome_UsesReadableDarkThemeColors()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);

        Assert.That((textEditor.Background as ISolidColorBrush)?.Color, Is.EqualTo(Color.Parse("#1E1E1E")));
        Assert.That((textEditor.Foreground as ISolidColorBrush)?.Color, Is.EqualTo(Color.Parse("#D4D4D4")));
    }

    [AvaloniaTest]
    public void WrapSelection_WrapsSelectedTextAndPreservesInnerSelection()
    {
        var control = new NativeMarkdownEditorControl();
        control.SetContent("hello world");
        control.SetSelection(6, 5);

        control.WrapSelection("**", "**");

        Assert.That(control.GetContent(), Is.EqualTo("hello **world**"));
        var selection = control.GetSelection();
        Assert.That(selection.Start, Is.EqualTo(8));
        Assert.That(selection.Length, Is.EqualTo(5));
        Assert.That(selection.Text, Is.EqualTo("world"));
    }

    [AvaloniaTest]
    public void CaretSelectionAndScrollMethods_ClampInvalidValues()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        control.SetContent("alpha\nbeta");

        Assert.DoesNotThrow(() => control.SetSelection(-5, 999));
        Assert.That(control.GetSelection().Start, Is.Zero);
        Assert.That(control.GetSelection().Length, Is.EqualTo(control.GetContent().Length));

        Assert.DoesNotThrow(() => control.SetCaretOffset(999));
        Assert.That(textEditor.CaretOffset, Is.EqualTo(control.GetContent().Length));

        Assert.DoesNotThrow(() => control.SetCaretPosition(99, 99));
        Assert.That(textEditor.CaretOffset, Is.EqualTo(control.GetContent().Length));

        Assert.DoesNotThrow(() => control.RevealLine(-10));
        Assert.DoesNotThrow(() => control.ScrollToPosition(2, 2, 99));
        Assert.That(control.GetSelection().Text, Is.EqualTo("eta"));

        control.SetContent(string.Empty);
        Assert.DoesNotThrow(() => control.ScrollToPosition(100, 100));
        Assert.That(textEditor.CaretOffset, Is.Zero);
    }

    [AvaloniaTest]
    public void IsReadOnly_SyncsToInnerEditorAndBlocksWrapCommand()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        control.SetContent("readonly");
        control.SetSelection(0, 8);

        control.IsReadOnly = true;
        control.WrapSelection("**", "**");

        Assert.That(textEditor.IsReadOnly, Is.True);
        Assert.That(control.GetContent(), Is.EqualTo("readonly"));
    }

    [AvaloniaTest]
    public void MarkdownGrammar_DefaultInitializationLeavesPlainEditingUsable()
    {
        var control = new NativeMarkdownEditorControl();

        control.SetContent("# Heading");

        Assert.That(control.IsMarkdownGrammarLoaded, Is.True);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("已加载"));
        Assert.That(control.GetContent(), Is.EqualTo("# Heading"));
    }

    [AvaloniaTest]
    public void MarkdownGrammarFailure_FallsBackToPlainTextEditing()
    {
        var control = new NativeMarkdownEditorControl(_ =>
            throw new InvalidOperationException("broken grammar"));

        control.SetContent("# Still editable");

        Assert.That(control.IsMarkdownGrammarLoaded, Is.False);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("broken grammar"));
        Assert.That(control.GetContent(), Is.EqualTo("# Still editable"));
    }

    [AvaloniaTest]
    public void LargeContent_UsesPlainNonWrappingPerformanceModeAndRestoresForSmallContent()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        var largeContent = "# Large task doc\n\n" + new string('x', 40_000);

        control.SetContent(largeContent);

        Assert.That(control.GetContent(), Is.EqualTo(largeContent));
        Assert.That(control.IsMarkdownGrammarLoaded, Is.False);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("大 Markdown 文件"));
        Assert.That(textEditor.WordWrap, Is.False);
        Assert.That(control.HasUnsyncedContent, Is.False);

        control.SetContent("# Small");

        Assert.That(control.GetContent(), Is.EqualTo("# Small"));
        Assert.That(textEditor.WordWrap, Is.True);
        Assert.That(control.IsMarkdownGrammarLoaded, Is.True);
        Assert.That(control.HasUnsyncedContent, Is.False);
    }

    [AvaloniaTest]
    public void MathMarkdown_UsesPlainHighlightingModeButKeepsWordWrap()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);

        control.SetContent("# Math\n\nInline formula: $x + y = z$");

        Assert.That(control.IsMarkdownGrammarLoaded, Is.False);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("LaTeX"));
        Assert.That(textEditor.WordWrap, Is.True);
        Assert.That(control.GetContent(), Does.Contain("$x + y = z$"));
        Assert.That(control.HasUnsyncedContent, Is.False);
    }

    [AvaloniaTest]
    public void UserTextChange_ToLargeContentUsesPerformanceModeWithoutUpdatingSnapshot()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        control.SetContent("# Small");
        var largeContent = "# Large task doc\n\n" + new string('x', 40_000);

        textEditor.Text = largeContent;

        Assert.That(control.EditorContent, Is.EqualTo("# Small"));
        Assert.That(control.GetContent(), Is.EqualTo(largeContent));
        Assert.That(control.HasUnsyncedContent, Is.True);
        Assert.That(control.IsMarkdownGrammarLoaded, Is.False);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("大 Markdown 文件"));
        Assert.That(textEditor.WordWrap, Is.False);
    }

    [AvaloniaTest]
    public void UserTextChange_AddsMathMarkdownUsesPerformanceModeWithoutUpdatingSnapshot()
    {
        var control = new NativeMarkdownEditorControl();
        var textEditor = FindInnerEditor(control);
        control.SetContent("# Small");

        textEditor.Text = "# Small\n\nInline formula: $x + y$";

        Assert.That(control.EditorContent, Is.EqualTo("# Small"));
        Assert.That(control.GetContent(), Does.Contain("$x + y$"));
        Assert.That(control.HasUnsyncedContent, Is.True);
        Assert.That(control.IsMarkdownGrammarLoaded, Is.False);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("LaTeX"));
        Assert.That(textEditor.WordWrap, Is.True);
    }

    [AvaloniaTest]
    public void Dispose_CanBeCalledRepeatedlyAcrossMultipleControls()
    {
        for (var i = 0; i < 3; i++)
        {
            using var control = new NativeMarkdownEditorControl();
            control.SetContent($"# Document {i}");

            Assert.DoesNotThrow(control.Dispose);
            Assert.DoesNotThrow(control.Dispose);
        }
    }

    [AvaloniaTest]
    public void DetachedFromVisualTree_ReleasesTextMateInstallation()
    {
        var control = new NativeMarkdownEditorControl();
        var window = new Window
        {
            Content = control,
            Width = 640,
            Height = 480
        };

        window.Show();
        control.SetContent("# Heading");

        Assert.That(control.IsMarkdownGrammarLoaded, Is.True);

        window.Close();

        Assert.That(control.IsMarkdownGrammarLoaded, Is.False);
        Assert.That(control.MarkdownGrammarStatusText, Does.Contain("已释放"));
    }

    private static TextEditor FindInnerEditor(NativeMarkdownEditorControl control)
    {
        return control.FindControl<TextEditor>("Editor")
            ?? throw new InvalidOperationException("Native Markdown editor inner TextEditor was not found.");
    }
}
