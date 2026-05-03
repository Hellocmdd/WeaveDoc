using System.Text;
using RagAvalonia.Models;

namespace RagAvalonia.Services;

public sealed partial class LocalAiService
{
    private const string RagSystemPrompt = """
你是 WeaveDoc 的本地文档问答助手。你的职责是把用户问题与已提供的上下文块做严格对齐，生成准确、具体、可追溯的答案。

硬性规则：
1) 只能依据用户消息里的“上下文”回答，不得使用外部常识、训练记忆或猜测补全。
2) 上下文由多个信息块组成，需要你主动提炼、归纳和综合。即使信息分散在多个块中，没有单个块直接完整回答，你也必须尽力整合出完整答案。只有当上下文中确实找不到任何与问题相关的信息时，才能回答“根据当前文档，未找到相关信息”。
3) 必须优先遵循上下文块中的具体参数、代码实现、模块名、技术栈、字段名、协议名、算法名、工具名和专业术语；原文出现 vue 3、mybatis-plus、Spring Boot、STM32、JSON、MQTT 等术语时要按原词保留，不要改写成泛称。
4) 回答要先给结论，再按逻辑条理展开。流程题按步骤，架构/组成题按层次，模块实现题按数据流和控制链路，对比题按维度，摘要题按主题主线。
5) 不要泛泛而谈，不要输出“系统很重要”“提高效率”等没有被上下文支撑的空话；每个要点都应落到上下文中的具体事实、参数、实现或术语。
6) 每个自然段或每个要点末尾必须附 1-2 个稳定来源标签，且只能复制上下文中已经出现过的“来源标签”，例如 [doc/a.md | 方法设计 | c3]。禁止使用 [1]、[2]，禁止编造来源。
7) 多个上下文块能互补时，要综合组织答案；多个块互相冲突时，优先采用排序更靠前的证据，并说明差异来自不同来源。
8) 回答语言必须与用户问题一致。中文问题用中文回答，英文问题用英文回答。
9) 禁止直接输出论文题目、作者名单、单位信息、HTML/Markdown 标题块；不要把 title:、abstract:、content: 等字段名前缀原样抄进答案，除非用户明确询问这些字段。
""";

    private string BuildPrompt(
        string question,
        IReadOnlyList<ChatTurn> history,
        QueryProfile queryProfile,
        IReadOnlyList<ScoredChunk> rankedChunks,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("回答任务:");
        builder.AppendLine("你将基于下方上下文回答用户问题。系统消息中的规则优先级最高。");
        builder.AppendLine($"问题类型: {DescribeIntent(queryProfile.Intent)}");
        builder.AppendLine($"结构要求: {BuildAnswerStructureRule(queryProfile)}");
        builder.AppendLine($"证据综合要求: {BuildCrossSourceSynthesisRule(queryProfile)}");
        builder.AppendLine($"领域细节要求: {BuildDomainGuidance(queryProfile)}");
        builder.AppendLine("输出要求: 保留上下文中的关键术语、技术栈、参数、代码实现、模块名和专业名词；按逻辑条理组织，拒绝泛泛而谈。");
        if (!string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle) && targetFilePaths.Count > 0)
        {
            builder.AppendLine($"指定文档约束: 用户明确在问《{queryProfile.RequestedDocumentTitle}》相关内容。除非上下文明确要求跨文档补充，否则优先只使用该文档来源，不要跳到其他文档。");
        }
        builder.AppendLine();
        AppendConversationHistory(builder, history, question);
        builder.AppendLine("优先关注的证据:");
        for (var index = 0; index < rankedChunks.Count; index++)
        {
            var item = rankedChunks[index];
            builder.AppendLine($"- {BuildStableCitation(item.Chunk)} {item.Chunk.DocumentTitle} / {item.Chunk.SectionTitle}");
        }

        builder.AppendLine();
        builder.AppendLine("上下文:");

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"来源标签: {BuildStableCitation(chunk)}");
            builder.AppendLine($"来源说明: {chunk.DocumentTitle} / {chunk.SectionTitle} ({chunk.FilePath})");
            if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
            {
                builder.AppendLine($"结构路径: {chunk.StructurePath}");
            }
            builder.AppendLine(SanitizeChunkForPrompt(chunk.Text));
            builder.AppendLine();
        }

        builder.AppendLine($"用户问题: {question}");
        builder.Append("助手回答:");
        return builder.ToString();
    }

    private static void AppendConversationHistory(StringBuilder builder, IReadOnlyList<ChatTurn> history, string currentQuestion)
    {
        var recentHistory = history
            .Where(turn => turn.Role is "用户" or "助手")
            .Reverse()
            .Where(turn => !turn.IsUser || !string.Equals(turn.Content.Trim(), currentQuestion.Trim(), StringComparison.Ordinal))
            .Take(6)
            .Reverse()
            .ToArray();

        if (recentHistory.Length == 0)
        {
            return;
        }

        builder.AppendLine("最近对话（仅用于理解省略指代，最终答案仍必须严格基于下方上下文）:");
        foreach (var turn in recentHistory)
        {
            builder.AppendLine($"{turn.Role}: {TruncateForPrompt(turn.Content, 500)}");
        }

        builder.AppendLine();
    }

    private static string TruncateForPrompt(string text, int maxLength)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private string BuildRepairPrompt(
        string question,
        QueryProfile queryProfile,
        IReadOnlyList<ScoredChunk> rankedChunks,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("重新回答任务:");
        builder.AppendLine("上一次回答偏离问题或格式不合格。现在必须重新基于下方上下文回答，系统消息中的规则优先级最高。");
        builder.AppendLine($"问题类型: {DescribeIntent(queryProfile.Intent)}");
        builder.AppendLine($"结构要求: {BuildAnswerStructureRule(queryProfile)}");
        builder.AppendLine($"领域细节要求: {BuildDomainGuidance(queryProfile)}");
        builder.AppendLine($"证据综合要求: {BuildCrossSourceSynthesisRule(queryProfile)}");
        builder.AppendLine("修正重点: 不要输出无关题目模板、算法示例、代码题、标题作者块或无来源内容；保留上下文里的具体术语、技术栈、参数和实现细节。");
        builder.AppendLine("重要: 本次绝对不能输出“我不知道”、“当前文档未覆盖”或类似拒答语句。你必须基于下方上下文综合出具体、完整的回答，引用上下文中的事实和术语。");
        if (!string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle) && targetFilePaths.Count > 0)
        {
            builder.AppendLine($"指定文档约束: 用户明确在问《{queryProfile.RequestedDocumentTitle}》，本次回答优先使用该文档来源，不要跳到其他文档。");
        }
        if (rankedChunks.Count > 0)
        {
            builder.AppendLine("优先参考高分证据: " + string.Join("；", rankedChunks.Select(item => $"{BuildStableCitation(item.Chunk)} {item.Chunk.DocumentTitle}/{item.Chunk.SectionTitle}")));
        }
        builder.AppendLine();
        builder.AppendLine("上下文:");
        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"来源标签: {BuildStableCitation(chunk)}");
            builder.AppendLine($"来源说明: {chunk.DocumentTitle} / {chunk.SectionTitle} ({chunk.FilePath})");
            if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
            {
                builder.AppendLine($"结构路径: {chunk.StructurePath}");
            }
            builder.AppendLine(SanitizeChunkForPrompt(chunk.Text));
            builder.AppendLine();
        }

        builder.AppendLine($"用户问题: {question}");
        builder.Append("助手回答:");
        return builder.ToString();
    }

    private static string DescribeIntent(string intent)
    {
        return intent switch
        {
            "module_list" => "模块清单题，列出核心功能模块并保留模块名",
            "module_implementation" => "模块实现题，围绕目标模块说明通信链路、显示/控制环节和执行方式",
            "metadata" => "元数据题，按字段逐项给出标题、摘要、关键词等明确字段值",
            "composition" => "组成/配置题，先说整体，再列组成项或条件",
            "compare" => "对比题，先说主要差异，再分点比较",
            "explain" => "解释题，先说结论，再说明原因/机制",
            "procedure" => "流程题，按步骤说明",
            "usage" => "用途题，先说用途，再列关键点",
            "summary" => "总结题，先概括主题，再分层展开内容",
            "definition" => "定义题，先给定义，再补充特征或作用",
            _ => "一般问答，先结论后依据"
        };
    }
}
