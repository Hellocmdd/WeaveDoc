# AFD 模板扩展设计

## 目标

为 WeaveDoc AFD 模板系统新增 2 个模板 JSON 文件（课程报告、实验报告），解决目前仅有 `default-thesis.json` 导致模板数量不足的问题，满足验收标准 F-04"切换样式模板"的需求。

## 背景

- 当前仅有 `default-thesis.json`（学位论文模板），无法演示模板切换功能
- F-04 验收标准要求"能导入/管理 BibTeX 文献，并切换样式模板"
- 模板需通过 `AfdParser.Parse()` 验证（meta 非空、templateName 非空、defaults 非空、styles 非空且 fontSize > 0）

## 方案选择

选择方案 A（最大差异化）：两个新模板在页边距、标题字号、页眉页脚上与 default-thesis 形成明显差异，确保模板切换时视觉反馈直观。

## 文件结构

```
src/WeaveDoc.Converter/Config/TemplateSchemas/
├── default-thesis.json      # 已有：学位论文
├── course-report.json       # 新增：课程报告
└── lab-report.json          # 新增：实验报告
```

## 模板参数设计

### course-report.json — 课程报告

基于武汉大学及同类高校课程报告格式规范：

| 区域 | 参数 | 值 |
|------|------|-----|
| **meta** | templateName | "课程报告" |
| | description | "高校课程报告通用模板" |
| **defaults** | fontFamily | "宋体" |
| | fontSize | 12 |
| | lineSpacing | 1.5 |
| | pageSize | 210 × 297 (A4) |
| | margins | top:25, bottom:25, left:25, right:25 |
| **heading1** | fontFamily | "黑体" |
| | fontSize | 16 |
| | bold | true |
| | alignment | "center" |
| | spaceBefore / spaceAfter | 24 / 18 |
| **heading2** | fontFamily | "黑体" |
| | fontSize | 15 |
| | bold | true |
| | alignment | "left" |
| | spaceBefore / spaceAfter | 18 / 12 |
| **heading3** | fontFamily | "黑体" |
| | fontSize | 14 |
| | bold | true |
| | alignment | "left" |
| | spaceBefore / spaceAfter | 12 / 6 |
| **body** | fontFamily | "宋体" |
| | fontSize | 12 |
| | firstLineIndent | 24 |
| | lineSpacing | 1.5 |
| **header** | text | "课程报告" |
| | fontFamily | "宋体" |
| | fontSize | 10.5 |
| | alignment | "center" |
| **footer** | pageNumbering | true |
| | format | "arabic" |
| | alignment | "center" |
| | startPage | 1 |

### lab-report.json — 实验报告

基于理工科实验报告格式规范（参考北大、北科大、西电等标准）：

| 区域 | 参数 | 值 |
|------|------|-----|
| **meta** | templateName | "实验报告" |
| | description | "理工科实验报告通用模板" |
| **defaults** | fontFamily | "宋体" |
| | fontSize | 12 |
| | lineSpacing | 1.5 |
| | pageSize | 210 × 297 (A4) |
| | margins | top:25.4, bottom:25.4, left:31.7, right:31.7 |
| **heading1** | fontFamily | "黑体" |
| | fontSize | 18 |
| | bold | true |
| | alignment | "center" |
| | spaceBefore / spaceAfter | 24 / 18 |
| **heading2** | fontFamily | "黑体" |
| | fontSize | 15 |
| | bold | true |
| | alignment | "left" |
| | spaceBefore / spaceAfter | 18 / 12 |
| **heading3** | fontFamily | "黑体" |
| | fontSize | 12 |
| | bold | true |
| | alignment | "left" |
| | spaceBefore / spaceAfter | 12 / 6 |
| **body** | fontFamily | "宋体" |
| | fontSize | 12 |
| | firstLineIndent | 24 |
| | lineSpacing | 1.5 |
| **header** | text | "实验报告" |
| | fontFamily | "宋体" |
| | fontSize | 9 |
| | alignment | "center" |
| **footer** | pageNumbering | true |
| | format | "arabic" |
| | alignment | "center" |
| | startPage | 1 |

## 三模板对比

| 参数 | default-thesis | course-report | lab-report |
|------|---------------|---------------|------------|
| 上/下边距 | 25/25mm | 25/25mm | 25.4/25.4mm |
| 左/右边距 | 30/30mm | 25/25mm | 31.7/31.7mm |
| heading1 字号 | 16pt | 16pt | 18pt |
| heading2 字号 | 14pt | 15pt | 15pt |
| heading3 | 无 | 14pt | 12pt |
| 页眉文字 | 无 | 课程报告 | 实验报告 |
| 页眉字号 | 无 | 10.5pt | 9pt |
| 页脚 | 无 | 页码居中 | 页码居中 |

## 验证方式

1. 每个新模板 JSON 通过 `AfdParser.Parse()` 无异常
2. 每个新模板通过 `ReferenceDocBuilder.Build()` 生成有效 DOCX
3. 生成的 DOCX 通过 `OpenXmlStyleCorrector.ApplyAfdStyles()` 和 `ApplyHeaderFooter()` 正确应用样式
4. 导出的 DOCX 中页眉包含对应文字，页脚包含页码字段

## 范围

- 仅创建 2 个 JSON 模板文件
- 不修改任何现有代码
- 不修改现有测试
- 不添加新测试（模板是数据文件，由现有管线测试覆盖）
