// 文件：tests/WeaveDoc.MarkdownEditor.Tests/ColumnAlignmentWhiteBoxTests.cs
using NUnit.Framework;
using Markdig.Extensions.Tables;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Tests;

/// <summary>
/// 被测方法：MarkdigMarkdownRenderService.GetColumnAlignment()
/// （已添加 InternalsVisibleTo 使 internal 方法可被测试项目访问）
/// 测试总数：XX
/// 预期发现缺陷：（若有，列出）
/// </summary>
[TestFixture]
public class ColumnAlignmentWhiteBoxTests
{
    private Table CreateTable(int columnCount, params TableColumnAlign?[] alignments)
    {
        var table = new Table();
        for (int i = 0; i < columnCount; i++)
        {
            var def = new TableColumnDefinition();
            if (i < alignments.Length && alignments[i] is { } a)
                def.Alignment = a;
            table.ColumnDefinitions.Add(def);
        }
        return table;
    }

    // ======== 语句覆盖 ========

    [Test]
    public void GetColumnAlignment_SC01_IndexNegative_ReturnsEmpty()
    {
        // 输入：columnIndex = -1
        // 期望：按规格返回 ""
        var table = CreateTable(3, TableColumnAlign.Left, TableColumnAlign.Center, TableColumnAlign.Right);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, -1);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_SC02_IndexValidLeftAlign_ReturnsLeft()
    {
        var table = CreateTable(3, TableColumnAlign.Left);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo("left"));
    }

    // ======== 边界值 ========

    [Test]
    public void GetColumnAlignment_BV01_IndexAtMinusOne_ReturnsEmpty()
    {
        var table = CreateTable(3);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, -1);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_BV02_IndexAtZero_ReturnsValid()
    {
        var table = CreateTable(3, TableColumnAlign.Left);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo("left"));
    }

    [Test]
    public void GetColumnAlignment_BV05_IndexAtUpperBound_ReturnsValid()
    {
        var table = CreateTable(3, TableColumnAlign.Left, TableColumnAlign.Center, TableColumnAlign.Right);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 2);
        Assert.That(result, Is.EqualTo("right"));
    }

    [Test]
    public void GetColumnAlignment_BV06_IndexBeyondUpperBound_ReturnsEmpty()
    {
        var table = CreateTable(3);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 3);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    // ======== 条件覆盖 ========

    [Test]
    public void GetColumnAlignment_CC01_D1True_ReturnsEmpty()
    {
        // D1: columnIndex < 0 → True
        var table = CreateTable(3);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, -1);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_CC02_D1FalseD2FalseD3True_ReturnsLeft()
    {
        // D1=F, D2=F, D3(Left)=T
        var table = CreateTable(3, TableColumnAlign.Left);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo("left"));
    }

    [Test]
    public void GetColumnAlignment_CC05_D2True_ReturnsEmpty()
    {
        // D2: columnIndex >= Count → True (Count=0, columnIndex=0)
        var table = CreateTable(0);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    // ======== 条件组合覆盖 ========

    [Test]
    public void GetColumnAlignment_MCC01_D1TrueD2True_ReturnsEmpty()
    {
        // D1=T, D2=T: columnIndex < 0 && Count == 0
        var table = CreateTable(0);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, -1);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_MCC02_D1TrueD2False_ReturnsEmpty()
    {
        // D1=T, D2=F: columnIndex < 0, Count > 0
        var table = CreateTable(3);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, -1);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_MCC03_D1FalseD2True_ReturnsEmpty()
    {
        // D1=F, D2=T: columnIndex >= 0, Count == 0 → columnIndex >= Count 为 T
        var table = CreateTable(0);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_MCC04_D1FalseD2FalseAlignmentLeft_ReturnsLeft()
    {
        // D1=F, D2=F, Alignment=Left
        var table = CreateTable(3, TableColumnAlign.Left);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo("left"));
    }

    // ======== 基础路径覆盖 ========

    [Test]
    public void GetColumnAlignment_BPC_P1_D1True_ReturnsEmpty()
    {
        // P1: N1→N2→N4→N12 (columnIndex < 0)
        var table = CreateTable(3);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, -1);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_BPC_P2_D1FalseD2True_ReturnsEmpty()
    {
        // P2: N1→N2→N3→N4→N12 (columnIndex >= Count)
        var table = CreateTable(3);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 5);
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetColumnAlignment_BPC_P3_ValidIndexLeftAlign_ReturnsLeft()
    {
        // P3: N1→N2→N3→N5→N8→N12
        var table = CreateTable(3, TableColumnAlign.Left);
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo("left"));
    }

    [Test]
    public void GetColumnAlignment_BPC_P6_ValidIndexNoneAlign_ReturnsEmpty()
    {
        // P6: N1→N2→N3→N5→N6→N7→N11→N12
        var table = CreateTable(3); // default Alignment = None
        var result = MarkdigMarkdownRenderService.GetColumnAlignment(table, 0);
        Assert.That(result, Is.EqualTo(string.Empty));
    }
}