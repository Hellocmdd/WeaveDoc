# 智能化辅助文档：反馈数据说明

## 1. 文档目的

本目录用于沉淀“学术写作/文献管理/PDF 工作流”相关用户反馈数据，支持以下工作：

- 从真实讨论中提取用户痛点与需求信号。
- 为产品需求分析、功能优先级排序提供证据。
- 作为后续规则抽取、分类建模、可视化分析的数据输入。

## 2. 文件清单

- `feedback.csv`：结构化表格数据，适合统计、筛选、透视分析。
- `feedback.jsonl`：逐条 JSON 记录，适合程序处理与二次抽取。
- `FinalResults.md`：当前阶段的核心发现总结（结合机器学习算法和大模型总结提炼）。

## 3. 数据来源与范围

- 来源类型：公开社区讨论（当前样本主要为 Reddit 相关主题抓取结果）。
- 主题范围：文献管理、PDF 标注、写作协作、AI 辅助科研流程等。
- 数据特点：
	- 含原始文本（标题/摘要/正文片段）。
	- 含结构化抽取结果（工具提及、情感倾向、需求信号）。
	- 中英文混合标签（例如需求分类为中文，原始语料多为英文）。

## 4. 字段字典

以下字段在 `feedback.csv` 与 `feedback.jsonl` 中保持语义一致。

| 字段名 | 类型 | 说明 |
|---|---|---|
| source | string | 数据来源标识（如抓取任务名） |
| url | string | 原帖链接 |
| title | string | 标题 |
| snippet | string | 摘要片段 |
| content | string | 合并后的正文文本（可能含评论片段） |
| author | string | 发帖用户 |
| published_at | datetime string | 发布时间（ISO8601） |
| matched_keywords | JSON array string / array | 命中的工具关键词 |
| matched_review_terms | JSON array string / array | 命中的评测/体验相关词 |
| matched_requirement_terms | JSON array string / array | 命中的需求词（如 pdf/citation/offline 等） |
| overall_sentiment | string | 整体情感（如 positive/neutral/mixed/negative） |
| comment_count | int | 评论数 |
| is_question | bool | 是否问句型帖子 |
| tool_mentions | JSON array | 工具级抽取结果（优缺点、建议、证据条数等） |
| requirement_signals | JSON array | 需求信号抽取结果（类别、信号类型、证据、工具关联） |

说明：

- 在 CSV 中，复杂结构字段通常以 JSON 字符串形式存储。
- 在 JSONL 中，同名字段通常为原生数组/对象，更适合程序直接消费。

## 5. 快速使用流程

### 5.1 读取与展开（推荐）

1. 先读取 `feedback.csv` 做全量概览。
2. 对 `tool_mentions`、`requirement_signals` 做 JSON 展开。
3. 按工具、需求类别、情感倾向做聚合统计。

示例（Python/Pandas）：

```python
import json
import pandas as pd

df = pd.read_csv("feedback.csv")

# 展开 requirement_signals
signals = []
for _, row in df.iterrows():
		raw = row.get("requirement_signals")
		if isinstance(raw, str) and raw.strip():
				try:
						arr = json.loads(raw)
				except json.JSONDecodeError:
						arr = []
		else:
				arr = []
		for item in arr:
				item["source_url"] = row.get("url")
				item["post_title"] = row.get("title")
				signals.append(item)

signals_df = pd.DataFrame(signals)

# 示例统计：按需求类别计数
summary = signals_df.groupby("category", dropna=False).size().sort_values(ascending=False)
print(summary.head(20))
```

### 5.2 从问题到结论的最小路径

- 问题示例：用户对“离线能力”最常见抱怨是什么？
- 操作步骤：
	1. 过滤 `matched_requirement_terms` 包含 `offline` 的记录。
	2. 联合 `requirement_signals.signal_type` 筛选 `pain_point`。
	3. 回看 `evidence` 与 `content` 做人工复核。

## 6. 质量与局限

- 抽取误差：自动抽取可能出现分类偏差、证据截断、标签漂移。
- 语义噪声：部分帖子为闲聊/反讽，情感与需求标签可能受上下文影响。
- 样本偏差：平台用户结构不等于目标用户全体，结论需结合访谈与业务数据验证。

建议：

- 对高优先级结论执行人工复核（至少抽样 10%-20%）。
- 关键结论需“原文证据 + 结构化统计”双重支持。

## 7. 合规与使用边界

- 仅用于教学/研究与产品需求分析，不用于骚扰、画像歧视或违规抓取。
- 避免输出可识别个人信息的二次扩散内容。
- 对外展示时优先使用聚合统计与匿名化示例。

## 8. 与需求文档的关系

本数据可为“PDF 查看器/学术工作流”需求提供用户侧证据，尤其适用于：

- 协作与版本管理体验痛点验证。
- PDF 阅读/批注/导出链路需求优先级排序。
- 本地优先、离线可用、跨端同步等场景的需求论证。
