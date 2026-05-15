using System.Text.RegularExpressions;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

internal static class LocalAiQuestionAnalyzer
{
    private static readonly Regex QueryTokenRegex = new("[a-z0-9]+|[\\u4e00-\\u9fff]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingGreetingRegex = new("^(?:\\s*(?:你好|您好|嗨|哈喽|hello|hi|hey|请问|麻烦问一下|我想问一下|想请教一下|请教一下)\\s*[,，.。!！?？:：;；/+|、-]*)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BoundaryNoiseRegex = new("^[\\s,，.。!！?？:：;；/+|、-]+|[\\s,，.。!！?？:：;；/+|、-]+$", RegexOptions.Compiled);
    private static readonly Regex TechnicalEntityRegex = new("(?<![A-Za-z0-9])(?:[A-Z][A-Za-z0-9]*(?:[-+./_][A-Za-z0-9]+)*(?:\\s+[A-Z][A-Za-z0-9+./_-]*){0,3}|[A-Za-z]+(?:[-+./_][A-Za-z0-9]+)+)(?![A-Za-z0-9])", RegexOptions.Compiled);
    private static readonly Regex ChineseEntityRegex = new("[\\u4e00-\\u9fffA-Za-z0-9+./_-]{2,24}(?:模块|系统|平台|框架|架构|数据库|接口|协议|算法|控制器|传感器|芯片|处理器|显示屏|屏幕|设备|组件|服务|工具|引擎|模型|网络|电源|电机|阀|终端|客户端|服务器|层|端)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UsageSubjectRegex = new("(?:^|[，。；;：:\\s])(?:在.+?[里中内]\\s*)?(?<subject>[a-z0-9\\-_/+\\.\\u4e00-\\u9fff]{2,24}?)(?:有?什么作用|有什么用途|有什么功能|有什么用|作用是什么|用途是什么|功能是什么)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> FocusStopWords =
    [
        "什么", "哪些", "哪个", "那个", "这个", "一下", "一些", "其中", "对于", "相关", "关于", "有关", "内容",
        "文档", "资料", "介绍", "说明", "总结", "概述", "一下子", "请问", "你好", "您好", "可以", "是否",
        "怎么", "如何", "为什么", "原因", "原理", "机制", "作用", "用途", "功能", "区别", "比较", "对比",
        "流程", "步骤", "实现", "定义", "概念", "方法", "情况", "一下吗", "一下呢",
        "补充要求", "详细", "具体", "展开", "继续", "细一点", "详细一点", "具体一点"
    ];

    private static readonly string[] ExplanationSignals = ["原因", "因为", "导致", "所以", "由于", "为了", "通过", "原理", "机制"];
    private static readonly string[] CompareSignals = ["区别", "不同", "差异", "相比", "而", "则", "一方面", "另一方面"];
    private static readonly string[] CompositionSignals = ["组成", "构成", "包括", "包含", "模块", "架构", "技术栈", "前后端", "配置", "条件", "环境", "硬件", "软件", "接口"];
    private static readonly string[] DefinitionSignals = ["是一种", "是指", "作为", "选用", "采用", "负责", "用于", "核心", "模块"];
    private static readonly string[] ProcedureSignals = ["步骤", "流程", "首先", "然后", "最后", "实现", "方法", "过程"];
    private static readonly string[] ModuleImplementationSignals = ["连接", "通信", "上传", "下发", "展示", "显示", "设定", "触发", "指令", "链路", "协议", "服务器", "app", "json"];
    private static readonly string[] UsageSignals = ["用于", "作用", "用途", "负责", "实现", "控制", "采集", "识别", "管理"];
    private static readonly string[] DetailSignals = ["详细", "具体", "展开", "细说", "详述", "全面", "细一点", "详细一点", "具体一点", "展开讲", "展开说", "多说", "讲讲", "说说"];
    private static readonly string[] SupplementalRequestSignals = ["补充要求", "详细一点", "具体一点", "展开一点", "展开说", "展开讲", "细一点", "继续", "再说", "多说", "讲讲", "说说"];
    private static readonly string[] EnglishMetadataSignals = ["英文标题", "英文摘要", "英文关键词", "english title", "english abstract", "english keywords"];
    private static readonly string[] CompositionQuestionSignals = ["组成", "构成", "包括哪些", "包含哪些", "由哪些", "有哪些部分", "有哪些模块", "硬件组成", "软件组成", "硬件条件", "软件条件", "系统配置", "部署环境", "实验环境", "运行环境", "实施条件", "技术栈", "总体架构", "架构和技术栈", "前后端分离", "采用了什么架构", "采用了哪些技术"];
    private static readonly string[] CompositionContextSignals = ["硬件", "软件", "架构", "技术栈", "前后端", "模块", "组成", "构成", "配置", "条件", "环境", "接口", "参数", "依赖"];
    private static readonly HashSet<string> GenericEntityStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "系统", "模块", "功能模块", "这个系统", "该系统", "本文", "文中", "论文", "研究", "设计", "实现", "方法",
        "主要模块", "总体架构", "系统架构", "技术栈", "组成", "包括", "包含", "数据", "信息", "内容", "平台", "工具"
    };
    private static readonly string[] NonCompositionAttributeSignals = ["效果", "性能", "表现", "体验", "评价", "口碑", "结论"];
    private static readonly string[] ProcedureQuestionSignals = ["如何", "步骤", "流程", "怎么做", "怎么实现", "怎么控制", "怎么完成", "怎么处理", "怎么设计", "怎么搭建", "怎么部署", "怎么进行", "怎么安装", "怎么使用", "怎么办"];

    public static QueryProfile BuildProfile(string question)
    {
        var requestedDocumentTitle = ExtractRequestedDocumentTitle(question);
        var focusTerms = BuildRetrievalQueryTokens(question, requestedDocumentTitle)
            .Where(IsMeaningfulFocusTerm)
            .OrderBy(token => token.Length)
            .ThenByDescending(token => token.Any(char.IsAsciiLetterOrDigit))
            .Take(8)
            .ToArray();

        var intent = DetectIntent(question);
        var wantsDetailedAnswer = WantsDetailedAnswer(question);
        return new QueryProfile(
            question,
            focusTerms,
            intent,
            wantsDetailedAnswer,
            IsFollowUpExpansionRequest(question),
            requestedDocumentTitle,
            RequestsEnglishMetadata(question),
            AvoidsEnglishMetadata(question),
            DisallowKeywordLikeLeadChunksForSummary(question),
            PreferFallbackOverUnknown(question),
            ExtractCompareSubjects(question));
    }

    internal static string NormalizeQuestionForRetrieval(string question, IReadOnlyList<ChatTurn> history)
    {
        var normalized = question.Trim();
        normalized = LeadingGreetingRegex.Replace(normalized, string.Empty);
        normalized = BoundaryNoiseRegex.Replace(normalized, string.Empty);
        normalized = StripQuestionBoilerplate(normalized);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (ShouldAugmentWithPreviousQuestion(normalized))
        {
            var previousQuestion = history
                .Where(turn => turn.IsUser)
                .Select(turn => turn.Content.Trim())
                .Where(content => !string.IsNullOrWhiteSpace(content))
                .Where(content => !string.Equals(content, question.Trim(), StringComparison.Ordinal))
                .Reverse()
                .FirstOrDefault(content => ExtractQueryTokens(content).Count > 0);

            if (!string.IsNullOrWhiteSpace(previousQuestion))
            {
                var previousAssistantAnswer = history
                    .Where(turn => !turn.IsUser)
                    .Select(turn => turn.Content.Trim())
                    .Where(content => !string.IsNullOrWhiteSpace(content) && content.Length > 10)
                    .Reverse()
                    .FirstOrDefault();

                var anchorTerms = ExtractAnchorTerms(previousAssistantAnswer, previousQuestion);
                var augmented = $"{previousQuestion}；补充要求：{normalized}";
                if (anchorTerms.Count > 0)
                {
                    augmented = $"{augmented}。重点关注：{string.Join("、", anchorTerms)}";
                }

                return augmented;
            }
        }

        return normalized.Trim();
    }

    public static bool AvoidsEnglishMetadata(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var hasNegativeDirective = question.Contains("不要", StringComparison.Ordinal)
            || question.Contains("别", StringComparison.Ordinal)
            || question.Contains("无需", StringComparison.Ordinal)
            || question.Contains("不用", StringComparison.Ordinal);
        if (!hasNegativeDirective)
        {
            return false;
        }

        return EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase))
            || question.Contains("英文摘要", StringComparison.Ordinal)
            || question.Contains("英文关键词", StringComparison.Ordinal)
            || question.Contains("英文", StringComparison.OrdinalIgnoreCase)
            || question.Contains("english", StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequestsEnglishMetadata(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase))
            || question.Contains("英文标题", StringComparison.Ordinal)
            || question.Contains("英文摘要", StringComparison.Ordinal)
            || question.Contains("英文关键词", StringComparison.Ordinal)
            || question.Contains("englishTitle", StringComparison.OrdinalIgnoreCase)
            || question.Contains("englishAbstract", StringComparison.OrdinalIgnoreCase)
            || question.Contains("englishKeywords", StringComparison.OrdinalIgnoreCase);
    }

    public static bool DisallowKeywordLikeLeadChunksForSummary(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("不要直接抄英文摘要", StringComparison.Ordinal)
            || question.Contains("不要直接抄关键词", StringComparison.Ordinal)
            || question.Contains("不要直接抄英文摘要或关键词", StringComparison.Ordinal)
            || question.Contains("不要抄英文摘要", StringComparison.Ordinal)
            || question.Contains("不要抄关键词", StringComparison.Ordinal);
    }

    public static bool PreferFallbackOverUnknown(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var intent = DetectIntent(question);
        return intent is "procedure" or "composition" or "explain" or "definition" or "compare";
    }

    public static IReadOnlyList<string> ExtractCompareSubjects(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return [];
        }

        var normalized = NormalizeQuestionForSubjectExtraction(RemoveRequestedDocumentMentions(question, ExtractRequestedDocumentTitle(question)));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = question.Trim();
        }

        var patterns = new[]
        {
            @"(?<left>[^，。！？?;；]{2,80}?)(?:和|与|跟|及|以及|、|/|\\|vs\.?|versus)(?<right>[^，。！？?;；]{2,80}?)(?:有什么区别|有何区别|区别是什么|有什么不同|有何不同|差异是什么|的区别|的差异|区别|差异|不同|对比|比较)",
            @"(?:between|compare)\s+(?<left>[A-Za-z0-9_\-+./\s]{2,80}?)\s+(?:and|vs\.?|versus)\s+(?<right>[A-Za-z0-9_\-+./\s]{2,80})",
            @"(?<left>[A-Za-z0-9_\-+./\s]{2,80}?)\s+(?:vs\.?|versus)\s+(?<right>[A-Za-z0-9_\-+./\s]{2,80})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var left = CleanCompareSubject(match.Groups["left"].Value);
            var right = CleanCompareSubject(match.Groups["right"].Value);
            if (!string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && !string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return [left, right];
            }
        }

        return [];
    }

    public static bool IsMeaningfulFocusTerm(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (FocusStopWords.Contains(token))
        {
            return false;
        }

        if (IsSupplementalRequestToken(token))
        {
            return false;
        }

        var hasAsciiLetterOrDigit = token.Any(character => char.IsAsciiLetterOrDigit(character));
        if (!hasAsciiLetterOrDigit && token.Length <= 2)
        {
            return false;
        }

        return true;
    }

    public static IReadOnlyList<string> BuildRetrievalQueryTokens(string question, string? requestedDocumentTitle = null)
    {
        var focusSource = BuildFocusTermSourceText(question);
        var baseQuestion = string.IsNullOrWhiteSpace(focusSource) ? question : focusSource;
        baseQuestion = RemoveRequestedDocumentMentions(baseQuestion, requestedDocumentTitle);
        baseQuestion = StripRetrievalDirectives(baseQuestion);
        var tokens = new HashSet<string>(ExtractQueryTokens(baseQuestion), StringComparer.Ordinal);
        var subject = ExtractQuestionSubject(baseQuestion);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            tokens.Add(subject.ToLowerInvariant());
            foreach (var token in ExtractQueryTokens(subject))
            {
                tokens.Add(token);
            }
        }

        var expansionSource = string.IsNullOrWhiteSpace(question) ? baseQuestion : RemoveRequestedDocumentMentions(question, requestedDocumentTitle);
        expansionSource = StripRetrievalDirectives(expansionSource);
        var lowerQuestion = expansionSource.ToLowerInvariant();
        var intent = DetectIntent(expansionSource);

        foreach (var expansion in ExpandRetrievalTerms(expansionSource, lowerQuestion, intent))
        {
            if (!string.IsNullOrWhiteSpace(expansion))
            {
                tokens.Add(expansion.ToLowerInvariant());
            }
        }

        var stopWordsByLengthDesc = FocusStopWords
            .Where(word => word.Length >= 2)
            .OrderByDescending(word => word.Length)
            .ToArray();

        var tokensToRemove = new List<string>();
        var tokensToAdd = new List<string>();
        foreach (var token in tokens.ToList())
        {
            if (token.Length <= 6 || token.Any(char.IsAsciiLetterOrDigit))
            {
                continue;
            }

            var working = token;
            foreach (var stopWord in stopWordsByLengthDesc)
            {
                working = working.Replace(stopWord, "\n");
            }

            var fragments = working.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(fragment => fragment.Length >= 2)
                .ToArray();

            if (fragments.Length > 0)
            {
                tokensToRemove.Add(token);
                tokensToAdd.AddRange(fragments);
            }
        }

        foreach (var token in tokensToRemove)
        {
            tokens.Remove(token);
        }

        foreach (var token in tokensToAdd)
        {
            tokens.Add(token);
        }

        return tokens.ToArray();
    }

    public static string DetectIntent(string question)
    {
        if (IsSummaryQuestion(question))
        {
            return "summary";
        }

        if (question.Contains("区别", StringComparison.Ordinal) || question.Contains("对比", StringComparison.Ordinal) || question.Contains("比较", StringComparison.Ordinal))
        {
            return "compare";
        }

        if (question.Contains("为什么", StringComparison.Ordinal) || question.Contains("原因", StringComparison.Ordinal) || question.Contains("原理", StringComparison.Ordinal) || question.Contains("机制", StringComparison.Ordinal))
        {
            return "explain";
        }

        if (EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            return "metadata";
        }

        if (IsModuleQuestion(question))
        {
            return "module_list";
        }

        if (IsModuleImplementationQuestion(question))
        {
            return "module_implementation";
        }

        if (IsCompositionQuestion(question))
        {
            return "composition";
        }

        if (IsProcedureQuestion(question))
        {
            return "procedure";
        }

        if (question.Contains("作用", StringComparison.Ordinal) || question.Contains("用途", StringComparison.Ordinal) || question.Contains("功能", StringComparison.Ordinal) || question.Contains("有什么用", StringComparison.Ordinal))
        {
            return "usage";
        }

        if (question.Contains("是什么", StringComparison.Ordinal) || question.Contains("指什么", StringComparison.Ordinal) || question.Contains("定义", StringComparison.Ordinal))
        {
            return "definition";
        }

        return "general";
    }

    public static bool WantsDetailedAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return DetailSignals.Any(signal => question.Contains(signal, StringComparison.Ordinal))
            || question.Contains("详细介绍", StringComparison.Ordinal)
            || question.Contains("详细说明", StringComparison.Ordinal)
            || question.Contains("展开说明", StringComparison.Ordinal)
            || question.Contains("主要写什么", StringComparison.Ordinal)
            || question.Contains("主要内容", StringComparison.Ordinal)
            || question.Contains("主要讲", StringComparison.Ordinal)
            || question.Contains("从哪些方面", StringComparison.Ordinal);
    }

    public static bool IsFollowUpExpansionRequest(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = question.Trim();
        var focusTerms = ExtractQueryTokens(normalized).Where(IsMeaningfulFocusTerm).ToArray();
        if (focusTerms.Length >= 2)
        {
            return false;
        }

        return SupplementalRequestSignals.Any(signal => normalized.Contains(signal, StringComparison.Ordinal))
            || normalized.Contains("详细一点", StringComparison.Ordinal)
            || normalized.Contains("具体一点", StringComparison.Ordinal)
            || normalized.Contains("展开一点", StringComparison.Ordinal)
            || normalized.Contains("展开说", StringComparison.Ordinal)
            || normalized.Contains("展开讲", StringComparison.Ordinal)
            || normalized.Contains("细一点", StringComparison.Ordinal)
            || normalized.Contains("继续", StringComparison.Ordinal)
            || normalized.Contains("再说", StringComparison.Ordinal)
            || normalized.Contains("具体呢", StringComparison.Ordinal);
    }

    public static string? ExtractRequestedDocumentTitle(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var guillemetMatch = Regex.Match(question, "《(?<title>[^》]{2,120})》");
        if (guillemetMatch.Success)
        {
            return guillemetMatch.Groups["title"].Value.Trim();
        }

        var quoteMatch = Regex.Match(question, "[“\"](?<title>[^”\"]{2,120})[”\"]");
        if (quoteMatch.Success)
        {
            return quoteMatch.Groups["title"].Value.Trim();
        }

        var tailMatch = Regex.Match(
            question,
            @"(?<title>[\p{IsCJKUnifiedIdeographs}A-Za-z0-9_\-：:（）()\s]{4,120}?)(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料)|文档里|文中)",
            RegexOptions.IgnoreCase);
        return tailMatch.Success ? CleanRequestedDocumentTitle(tailMatch.Groups["title"].Value) : null;
    }

    internal static IReadOnlyList<string> ExtractQueryTokens(string text)
    {
        return EnumerateQueryTokens(text)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static int CountGenericAnswerEntities(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        count += TechnicalEntityRegex.Matches(text)
            .Select(match => NormalizeExtractedEntity(match.Value))
            .Where(IsUsefulExtractedEntity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        count += ChineseEntityRegex.Matches(text)
            .Select(match => NormalizeExtractedEntity(match.Value))
            .Where(IsUsefulExtractedEntity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        return count;
    }

    internal static bool IsIntentExpansionOnlyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        return DefinitionSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || ExplanationSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || CompareSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || CompositionSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || ProcedureSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || UsageSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || ModuleImplementationSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || token is "原因" or "机制" or "原理" or "定义" or "方法" or "步骤" or "流程" or "架构";
    }

    internal static IReadOnlyList<string> ExtractQuestionSubjectTerms(string question)
    {
        var subject = ExtractQuestionSubject(question);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return [];
        }

        var terms = new List<string> { subject.ToLowerInvariant() };
        terms.AddRange(ExtractQueryTokens(subject)
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm));

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static string StripQuestionBoilerplate(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question.Trim();
        var patterns = new[]
        {
            @"^\s*(?:对于|关于)\s*[^，。,.\n]{2,80}?(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料))\s*[,，:：]?\s*",
            @"^\s*在\s*[^，。,.\n]{2,80}?(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料)|文中|文档里)\s*[,，:：]?\s*",
            @"^\s*针对\s*[^，。,.\n]{2,80}?(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料))\s*[,，:：]?\s*"
        };

        foreach (var pattern in patterns)
        {
            normalized = Regex.Replace(normalized, pattern, string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        return BoundaryNoiseRegex.Replace(normalized, string.Empty).Trim();
    }

    internal static bool IsSummaryQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("总结", StringComparison.Ordinal)
            || question.Contains("概述", StringComparison.Ordinal)
            || question.Contains("综述", StringComparison.Ordinal)
            || question.Contains("主要写什么", StringComparison.Ordinal)
            || question.Contains("主要写的是什么", StringComparison.Ordinal)
            || question.Contains("主要写的是啥", StringComparison.Ordinal)
            || question.Contains("主要内容", StringComparison.Ordinal)
            || question.Contains("主要讲", StringComparison.Ordinal)
            || question.Contains("论文主要写", StringComparison.Ordinal)
            || question.Contains("文章主要写", StringComparison.Ordinal)
            || question.Contains("文档主要写", StringComparison.Ordinal)
            || question.Contains("讲了什么", StringComparison.Ordinal)
            || question.Contains("说了什么", StringComparison.Ordinal);
    }

    internal static bool IsModuleQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("模块", StringComparison.Ordinal)
            || question.Contains("功能模块", StringComparison.Ordinal)
            || question.Contains("有哪些功能", StringComparison.Ordinal)
            || question.Contains("系统功能", StringComparison.Ordinal);
    }

    internal static bool IsModuleImplementationQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return (question.Contains("模块", StringComparison.Ordinal)
                || question.Contains("功能", StringComparison.Ordinal)
                || question.Contains("接口", StringComparison.Ordinal)
                || question.Contains("链路", StringComparison.Ordinal)
                || question.Contains("通信", StringComparison.Ordinal))
            && (question.Contains("怎么", StringComparison.Ordinal)
                || question.Contains("如何", StringComparison.Ordinal)
                || question.Contains("实现", StringComparison.Ordinal)
                || question.Contains("流程", StringComparison.Ordinal)
                || question.Contains("设计", StringComparison.Ordinal));
    }

    internal static bool ShouldAugmentWithPreviousQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var focusTerms = ExtractQueryTokens(question).Where(IsMeaningfulFocusTerm).ToArray();
        if (focusTerms.Length >= 2)
        {
            return false;
        }

        return IsFollowUpExpansionRequest(question)
            || IsSummaryQuestion(question);
    }

    internal static bool IsCompositionQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        if (ContainsAny(question, CompositionQuestionSignals))
        {
            return true;
        }

        return (question.Contains("怎么样", StringComparison.Ordinal) || question.Contains("怎样的", StringComparison.Ordinal))
            && ContainsAny(question, CompositionContextSignals)
            && !ContainsAny(question, NonCompositionAttributeSignals);
    }

    internal static bool IsProcedureQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        if (ContainsAny(question, ProcedureQuestionSignals))
        {
            return true;
        }

        return question.Contains("怎么", StringComparison.Ordinal)
            && !question.Contains("怎么样", StringComparison.Ordinal)
            && !question.Contains("怎样的", StringComparison.Ordinal);
    }

    internal static int CountComparisonSignals(string text)
    {
        return CompareSignals.Count(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    internal static string NormalizeLookupText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || (character >= '\u4e00' && character <= '\u9fff'))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    internal static int ComputeRequestedDocumentMatchScore(string normalizedTarget, string? candidate)
    {
        var normalizedCandidate = NormalizeLookupText(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return 0;
        }

        if (string.Equals(normalizedTarget, normalizedCandidate, StringComparison.Ordinal))
        {
            return 6;
        }

        if (normalizedCandidate.Contains(normalizedTarget, StringComparison.Ordinal) || normalizedTarget.Contains(normalizedCandidate, StringComparison.Ordinal))
        {
            return 4;
        }

        var sharedLength = ExtractQueryTokens(normalizedTarget)
            .Intersect(ExtractQueryTokens(normalizedCandidate), StringComparer.Ordinal)
            .Sum(token => token.Length);

        return sharedLength >= 8 ? 2 : 0;
    }

    private static IReadOnlyList<string> ExtractAnchorTerms(string? previousAssistantAnswer, string previousUserQuestion)
    {
        if (string.IsNullOrWhiteSpace(previousAssistantAnswer))
        {
            return [];
        }

        var answerTokens = ExtractQueryTokens(previousAssistantAnswer)
            .Where(IsMeaningfulFocusTerm)
            .ToHashSet(StringComparer.Ordinal);
        if (answerTokens.Count == 0)
        {
            return [];
        }

        var previousUserTokens = ExtractQueryTokens(previousUserQuestion)
            .Where(IsMeaningfulFocusTerm)
            .ToHashSet(StringComparer.Ordinal);

        var techEntities = TechnicalEntityRegex.Matches(previousAssistantAnswer)
            .Select(match => match.Value)
            .Where(token => token.Length is >= 2 and <= 8);
        var chineseEntities = ChineseEntityRegex.Matches(previousAssistantAnswer)
            .Select(match => match.Value)
            .Where(token => token.Length is >= 2 and <= 8);

        return techEntities
            .Concat(chineseEntities)
            .Concat(answerTokens)
            .Where(token => !IsSupplementalRequestToken(token))
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(token => !previousUserTokens.Contains(token))
            .Where(token => token.Length <= 8)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token.Length)
            .ThenByDescending(token => token.Any(char.IsAsciiLetterOrDigit))
            .Take(6)
            .ToArray();
    }

    private static bool IsSupplementalRequestToken(string token)
    {
        return SupplementalRequestSignals.Any(signal =>
            token.Contains(signal, StringComparison.Ordinal)
            || signal.Contains(token, StringComparison.Ordinal));
    }

    private static string BuildFocusTermSourceText(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var markerIndex = question.IndexOf("；补充要求：", StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return question;
        }

        return StripRetrievalDirectives(question[..markerIndex].Trim());
    }

    private static string RemoveRequestedDocumentMentions(string question, string? requestedDocumentTitle)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question.Trim();
        if (string.IsNullOrWhiteSpace(requestedDocumentTitle))
        {
            return normalized;
        }

        var title = requestedDocumentTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return normalized;
        }

        var escapedTitle = Regex.Escape(title);
        normalized = Regex.Replace(normalized, $"《\\s*{escapedTitle}\\s*》", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, $"[“\"]\\s*{escapedTitle}\\s*[”\"]", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, escapedTitle, string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料))", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s{2,}", " ");
        normalized = BoundaryNoiseRegex.Replace(normalized, string.Empty);
        return normalized.Trim();
    }

    private static string StripRetrievalDirectives(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question.Trim();
        normalized = Regex.Replace(
            normalized,
            @"(?:[，,；;。]\s*|\s+)(?:不要|别|无需|不用)(?:再)?[^，,；;。!?！？]*",
            string.Empty,
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s{2,}", " ");
        normalized = BoundaryNoiseRegex.Replace(normalized, string.Empty);
        return normalized.Trim();
    }

    private static IEnumerable<string> ExpandRetrievalTerms(string question, string lowerQuestion, string intent)
    {
        var expansions = new HashSet<string>(StringComparer.Ordinal);

        if (intent == "summary" || question.Contains("主要写了什么", StringComparison.Ordinal) || question.Contains("主要内容", StringComparison.Ordinal))
        {
            expansions.Add("overview");
            expansions.Add("summary");
            expansions.Add("abstract");
            expansions.Add("摘要");
            expansions.Add("概述");
            expansions.Add("引言");
            expansions.Add("结论");
            expansions.Add("主要内容");
        }

        if (question.Contains("创新点", StringComparison.Ordinal)
            || question.Contains("创新", StringComparison.Ordinal)
            || question.Contains("贡献", StringComparison.Ordinal))
        {
            expansions.Add("创新点");
            expansions.Add("创新");
            expansions.Add("贡献");
            expansions.Add("提出");
            expansions.Add("改进");
            expansions.Add("优势");
        }

        if (question.Contains("局限", StringComparison.Ordinal)
            || question.Contains("不足", StringComparison.Ordinal)
            || question.Contains("挑战", StringComparison.Ordinal)
            || question.Contains("问题", StringComparison.Ordinal)
            || question.Contains("启示", StringComparison.Ordinal)
            || question.Contains("未来", StringComparison.Ordinal)
            || question.Contains("反思", StringComparison.Ordinal))
        {
            expansions.Add("局限");
            expansions.Add("不足");
            expansions.Add("挑战");
            expansions.Add("问题");
            expansions.Add("未来");
            expansions.Add("改进");
            expansions.Add("研究局限");
            expansions.Add("反思");
        }

        if (question.Contains("架构", StringComparison.Ordinal)
            || question.Contains("技术栈", StringComparison.Ordinal)
            || question.Contains("总体设计", StringComparison.Ordinal)
            || question.Contains("前后端", StringComparison.Ordinal))
        {
            expansions.Add("架构");
            expansions.Add("系统架构");
            expansions.Add("总体架构");
            expansions.Add("总体设计");
            expansions.Add("技术栈");
            expansions.Add("前后端分离");
            expansions.Add("后端");
            expansions.Add("前端");
            expansions.Add("数据库");
            expansions.Add("可视化");
            expansions.Add("模块");
            expansions.Add("接口");
        }

        if (question.Contains("硬件", StringComparison.Ordinal))
        {
            expansions.Add("硬件设计");
            expansions.Add("主控");
            expansions.Add("主控芯片");
            expansions.Add("传感器");
            expansions.Add("通信");
            expansions.Add("显示");
            expansions.Add("电源");
            expansions.Add("接口");
        }

        if (question.Contains("软件", StringComparison.Ordinal)
            || question.Contains("运行环境", StringComparison.Ordinal)
            || question.Contains("部署环境", StringComparison.Ordinal))
        {
            expansions.Add("软件设计");
            expansions.Add("运行环境");
            expansions.Add("部署环境");
            expansions.Add("依赖");
            expansions.Add("模块");
            expansions.Add("配置");
        }

        if (intent == "definition"
            || question.Contains("是什么", StringComparison.Ordinal)
            || question.Contains("指什么", StringComparison.Ordinal)
            || question.Contains("定义", StringComparison.Ordinal))
        {
            expansions.Add("定义");
            expansions.Add("是指");
            expansions.Add("是一种");
            expansions.Add("作为");
            expansions.Add("用于");
            expansions.Add("负责");
            expansions.Add("核心");
        }

        if (intent == "explain"
            || question.Contains("为什么", StringComparison.Ordinal)
            || question.Contains("原理", StringComparison.Ordinal)
            || question.Contains("机制", StringComparison.Ordinal))
        {
            expansions.Add("原因");
            expansions.Add("机制");
            expansions.Add("原理");
            expansions.Add("由于");
            expansions.Add("因为");
            expansions.Add("因此");
            expansions.Add("影响");
            expansions.Add("动态调节");
        }

        if (intent == "compare"
            || question.Contains("区别", StringComparison.Ordinal)
            || question.Contains("对比", StringComparison.Ordinal)
            || question.Contains("比较", StringComparison.Ordinal))
        {
            expansions.Add("区别");
            expansions.Add("差异");
            expansions.Add("不同");
            expansions.Add("对比");
            expansions.Add("相比");
            expansions.Add("阶段");
        }

        if (intent == "procedure")
        {
            expansions.Add("步骤");
            expansions.Add("流程");
            expansions.Add("策略");
            expansions.Add("机制");
            expansions.Add("算法");
            expansions.Add("反馈");
            expansions.Add("输入");
            expansions.Add("输出");
            expansions.Add("执行");
        }

        if (IsModuleQuestion(question))
        {
            expansions.Add("模块");
            expansions.Add("功能模块");
            expansions.Add("功能");
            expansions.Add("组成");
            expansions.Add("职责");
        }

        if (EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            expansions.Add("englishtitle");
            expansions.Add("englishabstract");
            expansions.Add("englishkeywords");
            expansions.Add("english");
        }

        if (intent == "module_implementation"
            || question.Contains("远程", StringComparison.Ordinal)
            || question.Contains("状态显示", StringComparison.Ordinal)
            || question.Contains("控制模块", StringComparison.Ordinal))
        {
            expansions.Add("接口");
            expansions.Add("协议");
            expansions.Add("通信");
            expansions.Add("链路");
            expansions.Add("上传");
            expansions.Add("下发");
            expansions.Add("显示");
            expansions.Add("控制");
        }

        if (intent == "usage"
            || question.Contains("作用", StringComparison.Ordinal)
            || question.Contains("用途", StringComparison.Ordinal)
            || question.Contains("功能", StringComparison.Ordinal))
        {
            expansions.Add("用于");
            expansions.Add("负责");
            expansions.Add("显示");
            expansions.Add("控制");
            expansions.Add("采集");
        }

        return expansions;
    }

    internal static IEnumerable<string> EnumerateQueryTokens(string text)
    {
        foreach (Match match in QueryTokenRegex.Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length < 2)
            {
                continue;
            }

            yield return token;
            if (LocalAiService.ContainsCjk(token) && token.Length > 3)
            {
                for (var index = 0; index < token.Length - 1; index++)
                {
                    yield return token.Substring(index, 2);
                }
            }
        }
    }

    private static string NormalizeExtractedEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Trim('，', '。', '；', '：', '、', '(', ')', '（', '）', '[', ']', '【', '】', '"', '\'');
    }

    private static bool IsUsefulExtractedEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length < 2 || normalized.Length > 48)
        {
            return false;
        }

        return !GenericEntityStopWords.Contains(normalized)
            && !FocusStopWords.Contains(normalized)
            && !IsIntentExpansionOnlyToken(normalized);
    }

    internal static bool IsUsageQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("作用", StringComparison.Ordinal)
            || question.Contains("用途", StringComparison.Ordinal)
            || question.Contains("功能", StringComparison.Ordinal)
            || question.Contains("有什么用", StringComparison.Ordinal);
    }

    private static string NormalizeQuestionForSubjectExtraction(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = StripQuestionBoilerplate(BuildFocusTermSourceText(question).Trim());
        normalized = normalized
            .Replace("请问", string.Empty, StringComparison.Ordinal)
            .Replace("麻烦问一下", string.Empty, StringComparison.Ordinal)
            .Replace("我想问一下", string.Empty, StringComparison.Ordinal)
            .Replace("想请教一下", string.Empty, StringComparison.Ordinal)
            .Trim();

        var patterns = new[]
        {
            "在.*?中起什么作用.*$",
            "在.*?中有什么作用.*$",
            "在.*?中有什么用.*$",
            "起什么作用.*$",
            "有什么作用.*$",
            "有什么用途.*$",
            "有什么功能.*$",
            "有什么用.*$",
            "作用是什么.*$",
            "用途是什么.*$",
            "功能是什么.*$"
        };

        foreach (var pattern in patterns)
        {
            normalized = Regex.Replace(normalized, pattern, string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        normalized = normalized.Trim('，', '。', '？', '?', '！', '!', '；', ';', '：', ':', '、', ' ');
        return normalized;
    }

    private static string? ExtractQuestionSubject(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        if (TryExtractUsageQuestionSubject(question, out var usageSubject))
        {
            return usageSubject;
        }

        if (TryExtractDirectQuestionSubject(question, out var directSubject))
        {
            return directSubject;
        }

        var normalized = NormalizeQuestionForSubjectExtraction(question);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = BuildFocusTermSourceText(question).Trim();
        }

        var matches = QueryTokenRegex.Matches(normalized.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length >= 2)
            .Where(token => token is not "什么" and not "作用" and not "用途" and not "功能" and not "有什么" and not "一下" and not "请问" and not "详细一点" and not "具体一点")
            .Where(token => !token.Contains("作用", StringComparison.Ordinal))
            .Where(token => !token.Contains("用途", StringComparison.Ordinal))
            .Where(token => !token.Contains("功能", StringComparison.Ordinal))
            .Where(token => !token.Contains("补充要求", StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        var exactEntity = matches
            .Where(token => !LocalAiService.ContainsCjk(token) || token.Length <= 12)
            .OrderByDescending(token => token.Any(char.IsAsciiLetterOrDigit))
            .ThenByDescending(token => token.Length)
            .FirstOrDefault();

        return exactEntity ?? matches.OrderByDescending(token => token.Length).First();
    }

    private static bool TryExtractDirectQuestionSubject(string question, out string subject)
    {
        subject = string.Empty;
        var normalized = NormalizeQuestionForSubjectExtraction(question);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var patterns = new[]
        {
            @"(?:里|中|内)?(?<subject>[\u4e00-\u9fffA-Za-z0-9_\-+/\.]{2,32})(?:是指什么|指的是什么|指什么|是什么|定义是什么)",
            @"(?:为什么|为何)(?:需要|要|使用|采用|引入|配置)?(?<subject>[\u4e00-\u9fffA-Za-z0-9_\-+/\.]{2,32})",
            @"(?<subject>[\u4e00-\u9fffA-Za-z0-9_\-+/\.]{2,32})(?:包括哪些|包含哪些|由哪些|有哪些部分|有哪些组成|怎么做|怎么实现|如何实现|如何设计)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var candidate = CleanExtractedQuestionSubject(match.Groups["subject"].Value);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                subject = candidate;
                return true;
            }
        }

        return false;
    }

    private static string CleanExtractedQuestionSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return string.Empty;
        }

        var cleaned = subject.Trim()
            .Trim('，', '。', '？', '?', '！', '!', '；', ';', '：', ':', '、', ' ')
            .Replace("文档里", string.Empty, StringComparison.Ordinal)
            .Replace("文档中", string.Empty, StringComparison.Ordinal)
            .Replace("文中", string.Empty, StringComparison.Ordinal)
            .Replace("这篇论文里", string.Empty, StringComparison.Ordinal)
            .Replace("这篇文章里", string.Empty, StringComparison.Ordinal)
            .Replace("这份材料里", string.Empty, StringComparison.Ordinal)
            .Trim();

        foreach (var marker in new[] { "里", "中", "内", "的" })
        {
            var markerIndex = cleaned.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0 && markerIndex < cleaned.Length - marker.Length)
            {
                cleaned = cleaned[(markerIndex + marker.Length)..].Trim();
            }
        }

        if (FocusStopWords.Contains(cleaned) || IsIntentExpansionOnlyToken(cleaned))
        {
            return string.Empty;
        }

        cleaned = Regex.Replace(cleaned, "(?:是|为|指|指的|定义)$", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (cleaned.Length < 2 || FocusStopWords.Contains(cleaned) || IsIntentExpansionOnlyToken(cleaned))
        {
            return string.Empty;
        }

        return cleaned;
    }

    private static bool TryExtractUsageQuestionSubject(string question, out string subject)
    {
        subject = string.Empty;
        if (!IsUsageQuestion(question))
        {
            return false;
        }

        var normalized = StripQuestionBoilerplate(BuildFocusTermSourceText(question).Trim());
        var match = UsageSubjectRegex.Match(normalized);
        if (!match.Success)
        {
            return false;
        }

        subject = CleanExtractedQuestionSubject(match.Groups["subject"].Value);
        return !string.IsNullOrWhiteSpace(subject);
    }

    private static string CleanCompareSubject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = StripQuestionBoilerplate(value.Trim());
        cleaned = Regex.Replace(cleaned, @"^(?:在|于)?(?:.+?[里中内的])", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"(?:有什么区别|有何区别|区别是什么|有什么不同|有何不同|差异是什么|区别|差异|不同|对比|比较).*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = cleaned.Trim('，', '。', '？', '?', '！', '!', '；', ';', '：', ':', '、', ' ', '"', '\'', '“', '”');
        cleaned = BoundaryNoiseRegex.Replace(cleaned, string.Empty).Trim();

        if (cleaned.Length < 2 || FocusStopWords.Contains(cleaned) || IsIntentExpansionOnlyToken(cleaned))
        {
            return string.Empty;
        }

        return cleaned;
    }

    private static string? CleanRequestedDocumentTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = StripQuestionBoilerplate(value.Trim());
        cleaned = Regex.Replace(
            cleaned,
            @"^(?:请|帮我|麻烦|能不能|可以)?(?:总结|概述|介绍|说明|分析|讲讲|说说|看一下|一下|请问|关于)\s*(?:一下|下)?",
            string.Empty,
            RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^(?:这篇|这个|该|关于)\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = BoundaryNoiseRegex.Replace(cleaned, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static bool ContainsAny(string text, params string[] signals)
    {
        return signals.Any(signal => text.Contains(signal, StringComparison.Ordinal));
    }
}
