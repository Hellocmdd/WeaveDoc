# 论文工具用户评价爬虫

这个项目提供一个可扩展的 Python 爬虫，用来抓取用户对论文工具的详细评价内容。当前已经内置 `Reddit` 搜索源，也保留了通用 CSS 选择器抓取模式。

## 当前能力

- 抓取 `Reddit` 搜索结果、帖子正文和评论
- 抓取 `V2EX` 节点列表页、主题正文和回复
- 抓取 `Product Hunt` 产品页里的产品简介和可见评价
- 抓取 `Capterra` 评论页里的产品描述和公开评论
- 按论文工具关键词筛选内容
- 按 `review / experience / pros / cons` 等评价词过滤
- 从帖子和评论里抽取更接近真实使用反馈的证据句
- 为每个工具输出 `pros / cons / recommendations / experience_reports`
- 生成按工具聚合的评价摘要和情绪分布
- 保留通用 HTML 列表页 / 详情页抓取模式
- 导出 `JSONL` 和 `CSV`

## 目录

- `main.py`：命令行入口
- `paper_tool_feedback_crawler/crawler.py`：抓取、解析、过滤、导出逻辑
- `config.example.json`：可直接运行的 Reddit 示例配置
- `tests/test_reddit_crawler.py`：Reddit 抓取离线测试

## 安装

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

## 使用

```bash
python3 main.py --config config.example.json --output-dir output
```

如果要启用更多线程，可以直接：

```bash
python3 main.py --config config.example.json --output-dir output --max-workers 8
```

运行时会持续输出进度日志，例如：

```text
[crawl] working | source=reddit-paper-tools | stage=search | detail=subreddit=academia query="Zotero" "review"
[crawl] progress 2/12 | remaining=10 records=1 | source=reddit-paper-tools | stage=search | detail=subreddit=academia query="Zotero" "review" hits=1
```

其中：

- `stage`：当前正在执行的步骤
- `progress x/y`：已完成步骤 / 当前已知总步骤
- `remaining`：当前还剩多少步未完成
- `records`：到目前为止已保留了多少条反馈记录
- 遇到搜索结果、评论、详情页这类运行中才发现的子任务时，会先输出 `discovered`，总步数会动态增加
- `max_workers`：全局线程数，默认配置示例里是 `4`
- 也可以在配置文件里给单个数据源单独设置 `max_workers`
- 运行结束会额外打印：当前运行目录、输出目录（绝对路径）和本次实际生成的文件绝对路径列表

结果会写入：

- `output/feedback.jsonl`
- `output/feedback.csv`
- `output/tool_summary.json`
- `output/tool_summary.md`
- `output/requirement_summary.json`
- `output/requirement_summary.md`
- `output/ml_requirement_analysis.json`
- `output/ml_requirement_analysis.md`
- `output/lean_requirements.json`
- `output/lean_requirements.md`
- `output/prd_requirements.md`

其中：

- `feedback.jsonl` / `feedback.csv`：原始记录，每条记录附带 `overall_sentiment`、`comment_count`、`is_question`、`tool_mentions`
- `tool_summary.json` / `tool_summary.md`：按工具聚合后的统计，包括情绪分布、常见优点、常见缺点、推荐信号、样例帖子链接
- `requirement_summary.json` / `requirement_summary.md`：按需求类别聚合的需求洞察，包括痛点、期望功能、使用场景、约束条件
- `ml_requirement_analysis.json` / `ml_requirement_analysis.md`：基于评论级证据做 TF-IDF 向量化和聚类后的分类结果，便于看“哪些评论被归到同一类需求”
- `lean_requirements.json` / `lean_requirements.md`：在 ML 分类结果基础上进一步压缩后的精简需求清单，优先保留需求、痛点和约束，不再把大量使用场景排在前面
- `prd_requirements.md`：把精简需求进一步转成中文 PRD 风格的需求列表，便于直接给产品或研发使用

如果你需要更大规模的数据覆盖，可以直接运行：

```bash
python3 main.py --config config.expanded.json --output-dir output_expanded
```

这份配置会额外覆盖：

- 更多 Reddit 子版块
- Hacker News
- Stack Overflow
- 更广的 V2EX 节点
- 更多 Product Hunt 产品页

如果你所在网络环境会屏蔽 Reddit/Product Hunt/Capterra，可使用可达源配置：

```bash
python3 main.py --config config.reachable.json --output-dir output_reachable
```

该配置优先使用当前已验证可访问的数据源（V2EX / HN / Stack Exchange 多站点）。

如果 Stack Exchange API 因限流不可用，可以使用 GitHub 讨论源：

```bash
python3 main.py --config config.github_expanded.json --output-dir output_github
```

该配置会抓取 GitHub Issues 里的工具使用反馈（标题+正文），并沿用同一套需求分类与精简清单输出。
- 汇总阶段会对近似重复的评价句做归并，并额外产出 `positive_highlights / negative_highlights / recommendation_highlights`
- 现在还会额外生成更具体的 `specific_positive_feedback / specific_negative_feedback`，尽量回答“具体喜欢什么方面、为什么喜欢 / 不喜欢”

如果你要基于 `doc/《软件计划项目书》.docx` 做项目需求抽取，并且扩大搜索源（GitHub + HN + Stack Overflow + V2EX），可直接运行：

```bash
python3 main.py --config config.weavedoc_doc.json --output-dir output_weavedoc_doc --max-workers 6
```

建议优先查看：

- `output_weavedoc_doc/lean_requirements.md`
- `output_weavedoc_doc/prd_requirements.md`

## Reddit 配置

`config.example.json` 默认使用 `reddit_search` 数据源。主要字段：

- `source_type`：填 `reddit_search`
- `subreddits`：要搜索的子版块列表
- `search_terms`：和论文工具关键词组合搜索的评价词
- `search_sort`：搜索排序，常用 `relevance` 或 `new`
- `search_limit`：每个查询最多取多少帖子
- `include_comments`：是否抓取评论
- `max_comments`：每个帖子最多保留多少条顶层评论
- `keyword_aliases`：可选，给工具名补充别名，例如 `Connected Papers` 和 `ConnectedPapers`
- 对名字本身有歧义的工具，建议把品牌化写法也一起配上，例如 `Consensus AI`、`Elicit AI`、`Scite.ai`，这样搜索阶段会少抓到“只是普通英文单词”的帖子

这类查询会按 `keywords × search_terms × subreddits` 组合搜索，然后再用本地关键词和评价词做二次过滤。

如果你要为具体产品做需求调研，可以参考仓库里的 `config.weavedoc.json`：

- `keywords`：放竞品或替代方案，如 `Zotero / Paperpile / Overleaf / Obsidian`
- `requirement_terms`：放需求导向词，如 `workflow / markdown / pdf / docx / offline / privacy`
- 输出里会额外生成 `requirement_summary.*`，把帖子里的痛点、想要的能力、使用场景、约束条件按类别聚合

## V2EX 配置

内置 `v2ex_topics` 数据源，适合抓中文讨论。主要字段：

- `source_type`：填 `v2ex_topics`
- `start_urls`：节点列表页或主题页 URL，例如 `https://www.v2ex.com/go/create`
- `search_limit`：每个起始 URL 最多抓多少个主题
- `max_pages`：列表页最多翻多少页，会自动请求 `?p=2`、`?p=3`
- `max_comments`：每个主题最多保留多少条回复

`v2ex_topics` 会自动：

- 从节点列表页发现主题链接
- 进入主题页抓正文和回复
- 复用同一套“真实评价”抽取逻辑
- 支持中文正负向、推荐、使用经历线索词

## Product Hunt 配置

内置 `producthunt_reviews` 数据源，适合抓偏产品化工具的用户评价。主要字段：

- `source_type`：填 `producthunt_reviews`
- `start_urls`：产品页或帖子页 URL
- `max_comments`：最多保留多少条可见评价

`producthunt_reviews` 会自动：

- 抓产品标题和简介
- 尝试提取页面里可见的 AI 汇总段落
- 提取公开可见的用户评价正文
- 把这些内容统一送进“真实评价”抽取逻辑

注意：

- Product Hunt 页面结构有变动风险，选择器是启发式的
- 某些评价可能依赖登录或前端动态加载，当前实现只抓首屏可见内容

## Capterra 配置

内置 `capterra_reviews` 数据源，适合抓更标准化的软件评论页。主要字段：

- `source_type`：填 `capterra_reviews`
- `start_urls`：产品评论页 URL
- `max_comments`：最多保留多少条公开评论

`capterra_reviews` 会自动：

- 抓产品标题和页面描述
- 尝试抓页面里可见的优缺点摘要
- 提取公开评论正文
- 统一进入现有的评价证据抽取层

注意：

- Capterra 页面结构会调整，当前实现是启发式选择器
- 有些评论块会夹带评分、公司规模等 UI 信息，代码里已经做了基础噪音过滤

## 输出字段说明

`tool_mentions` 会把一条帖子拆成按工具维度的证据：

- `tool`：工具名
- `sentiment_label`：`positive / negative / mixed / neutral`
- `pros`：偏正向的证据句
- `cons`：偏负向的证据句
- `recommendations`：带推荐倾向的证据句
- `experience_reports`：明确带有使用经历的证据句
- `evidence_count`：当前工具在该帖子里命中的证据数量

这能把“只是提到了工具”和“真的在分享使用体验”区分开一些。它仍然是启发式抽取，不是严格的情感分析模型。

`tool_summary` 里的 `top_pros / top_cons / top_recommendations / top_experience_reports` 还会额外带：

- `count`：合并后的出现次数
- `variants`：被归并进去的近似句变体数量

`tool_summary` 还会额外产出：

- `specific_positive_feedback`：更具体的正向评价拆解，每项包括 `aspect / reason / count / examples`
- `specific_negative_feedback`：更具体的负向评价拆解，每项包括 `aspect / reason / count / examples`
- `specific_positive_highlights`：适合直接浏览的一行式正向摘要
- `specific_negative_highlights`：适合直接浏览的一行式负向摘要
- `specific_positive_summaries`：更自然的中文正向评论句式，例如“用户主要喜欢它的引用/写作流程，因为……”
- `specific_negative_summaries`：更自然的中文负向评论句式，例如“用户主要不满意它的协作，因为……”
- 当规则没法很好翻译时，摘要会优先保留原始问题点，不再退化成“整体体验有短板”这种泛化描述
- `tool_summary.md` 现在也会把每条 `specific_*_feedback` 的 `examples` 一起写出来，方便直接看到原始证据句，而不只是看一句泛化总结

例如：

```json
{
  "aspect": "引用/写作流程",
  "reason": "easy to use and saves me time when citing in Google Docs.",
  "count": 2,
  "summary_zh": "用户主要喜欢 Paperpile 的引用/写作流程，因为在 Google Docs 里插入引用时很容易上手，而且更省时间。",
  "examples": [
    "Paperpile is easy to use and saves me time when citing in Google Docs."
  ]
}
```

## 通用站点配置

如果你后面要抓其他论坛，可以继续使用通用模式：

- `source_type`：留空或填 `generic_css`
- `start_urls`：列表页 URL
- `item_selector`：列表页中每条内容的容器
- `title_selector`：标题
- `link_selector`：详情页链接
- `snippet_selector`：摘要或预览内容
- `author_selector`：作者
- `published_at_selector`：发布时间
- `detail_content_selector`：详情页正文
- `next_page_selector`：下一页按钮

## 建议的数据源

如果目标是“用户对于各种论文工具的详细评价”，优先抓这些来源：

- `Reddit`：适合抓长帖和评论，讨论密度高
- `V2EX`：适合中文工具体验帖
- `Product Hunt`：适合偏产品化工具的评论和定位描述
- `Capterra`：适合抓结构化软件评论页
- `知乎`：适合长回答，但通常需要额外处理登录和反爬
- `Product Hunt / G2 / Capterra`：适合软件评价，但结构更垂直

## 注意事项

- 先确认目标站点的 `robots.txt`、服务条款和频率限制
- 某些平台依赖登录、JS 渲染或反爬策略，需要接浏览器自动化
- `Reddit` 查询结果会受站内搜索召回影响，本地二次过滤只能提高精度，不能替代完整搜索索引
