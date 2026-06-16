// 文件：tests/WeaveDoc.MarkdownEditor.Tests/EscapeCharWhiteBoxTests.cs
using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Tests;

/// <summary>
/// 被测方法：MarkdigMarkdownRenderService.EscapeChar(char c)
/// 测试总数：13
/// 预期发现缺陷：控制字符 \0 的未定义行为；单引号转义遗漏风险
/// </summary>
[TestFixture]
public class EscapeCharWhiteBoxTests
{
    // ======== 语句覆盖 ========

    [Test]
    public void EscapeChar_SC01_LessThan_ReturnsLt()
    {
        var result = MarkdigMarkdownRenderService.EscapeChar('<');
        Assert.That(result, Is.EqualTo("&lt;"));
    }

    [Test]
    public void EscapeChar_SC02_DefaultChar_ReturnsSameChar()
    {
        var result = MarkdigMarkdownRenderService.EscapeChar('a');
        Assert.That(result, Is.EqualTo("a"));
    }

    // ======== 等价类 / 边界值 ========

    [Test]
    public void EscapeChar_EC_AllHtmlSpecials_AreEscaped()
    {
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('<'), Is.EqualTo("&lt;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('>'), Is.EqualTo("&gt;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('&'), Is.EqualTo("&amp;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('"'), Is.EqualTo("&quot;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('\''), Is.EqualTo("&#39;"));
    }

    [Test]
    public void EscapeChar_EC_OrdinaryChars_ReturnedAsIs()
    {
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('a'), Is.EqualTo("a"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('0'), Is.EqualTo("0"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar(' '), Is.EqualTo(" "));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('中'), Is.EqualTo("中"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('.'), Is.EqualTo("."));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('\n'), Is.EqualTo("\n"));
    }

    // ======== 错误推测 ========

    [Test]
    public void EscapeChar_EG01_NullChar_ReturnsAsIs()
    {
        // EG-01: 空字符 \0 的行为
        var result = MarkdigMarkdownRenderService.EscapeChar('\0');
        Assert.That(result, Is.EqualTo("\0"));
    }

    [Test]
    public void EscapeChar_EG06_Newline_ReturnsAsIs()
    {
        // EG-06: 换行符在 HTML 上下文中的行为
        var result = MarkdigMarkdownRenderService.EscapeChar('\n');
        Assert.That(result, Is.EqualTo("\n"));
    }

    [Test]
    public void EscapeChar_EG06_CarriageReturn_ReturnsAsIs()
    {
        // EG-06 补充: 回车符 \r
        var result = MarkdigMarkdownRenderService.EscapeChar('\r');
        Assert.That(result, Is.EqualTo("\r"));
    }

    // ======== 条件覆盖 ========

    [Test]
    public void EscapeChar_CC_P1_LessThanTrue_OthersFalse()
    {
        // D1=T, D2～D5 不执行（短路）
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('<'), Is.EqualTo("&lt;"));
    }

    [Test]
    public void EscapeChar_CC_P2_GreaterThanTrue_OthersFalse()
    {
        // D1=F, D2=T
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('>'), Is.EqualTo("&gt;"));
    }

    [Test]
    public void EscapeChar_CC_P3_AmpersandTrue_OthersFalse()
    {
        // D1=F, D2=F, D3=T
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('&'), Is.EqualTo("&amp;"));
    }

    [Test]
    public void EscapeChar_CC_P4_DoubleQuoteTrue_OthersFalse()
    {
        // D1～D3=F, D4=T
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('"'), Is.EqualTo("&quot;"));
    }

    [Test]
    public void EscapeChar_CC_P5_AllFalseUntilSingleQuoteTrue()
    {
        // D1～D4=F, D5=T
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('\''), Is.EqualTo("&#39;"));
    }

    [Test]
    public void EscapeChar_CC_P6_AllFalse_Default()
    {
        // D1～D5 全部 F
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('x'), Is.EqualTo("x"));
    }

    // ======== 基础路径覆盖 ========
    // P1: c=='<'  → "&lt;"   : 见 CC_P1
    // P2: c=='>'  → "&gt;"   : 见 CC_P2
    // P3: c=='&'  → "&amp;"  : 见 CC_P3
    // P4: c=='"'  → "&quot;" : 见 CC_P4
    // P5: c=='\'' → "&#39;"  : 见 CC_P5
    // P6: default            : 见 CC_P6

    [Test]
    public void EscapeChar_BPC_AllSixPaths_Verified()
    {
        // 基础路径覆盖验证：逐路径独立断言
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('<'), Is.EqualTo("&lt;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('>'), Is.EqualTo("&gt;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('&'), Is.EqualTo("&amp;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('"'), Is.EqualTo("&quot;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('\''), Is.EqualTo("&#39;"));
        Assert.That(MarkdigMarkdownRenderService.EscapeChar('x'), Is.EqualTo("x"));
    }
}
