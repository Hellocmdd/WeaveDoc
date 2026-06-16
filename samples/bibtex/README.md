# BibTeX 示例文献库

本目录提供示例 `.bib` 文件，用于演示 WeaveDoc 的文献库（BibTeX）功能。这些条目均为虚构/经典示例，非真实可引用文献。

## 文件说明

| 文件 | 主题 | 文献类型 | 说明 |
| --- | --- | --- | --- |
| `demo-classic-ai.bib` | 人工智能（经典） | article / book / inproceedings | **演示首选**。7 条全字段完整条目，导入后无 ⚠ 角标，便于演示干净的引用渲染闭环 |
| `ai-research.bib` | 深度学习与现代 AI | article / inproceedings | 7 条，含 2 条**故意缺字段**（缺 volume/pages、缺 journal），展示 ⚠ 缺著录项角标 |
| `software-engineering.bib` | 软件工程 | book / incollection / techreport | 8 条，含 2 条缺字段（缺 publisher、缺 institution） |
| `theses-and-online.bib` | 学位论文与电子文献 | phdthesis / mastersthesis / online / misc | 8 条，含 2 条缺字段（缺 school、缺 url） |

## 如何使用

1. 启动 WeaveDoc 桌面应用
2. 右侧 AI 辅助栏切换到 **「文献」** Tab
3. 点 **「+ 导入 .bib」**，选择上述任一文件
4. 列表展示条目（缺字段条目标 ⚠）
5. 在 Markdown 编辑器光标处，点条目右侧 **「插入引用」**，插入 `[@key]`
6. 点 **「导出」**，系统按 GB/T 7714-2015 顺序编码制自动生成参考文献表

## 演示引用闭环的推荐组合

在正文里依次插入这几条，导出后能看到按出现顺序自动编号的参考文献表：

```
深度学习的发展始于 [@shannon1948] 的信息论奠基。随后 [@rumelhart1988]
提出感知机模型，[@turing1950] 探讨了机器智能。现代深度学习的突破
[@lecun2015] [@krizhevsky2012] 推动了 [@devlin2019] 等预训练模型的兴起。
```

## 注意

- 这些文献元数据为演示目的，引用前请核对真实出处
- 故意缺字段的条目用于展示 ⚠ 角标与导出时的「引文校验」提示，实际使用时请补全
- `.bib` 文件为 UTF-8 编码，标准 BibTeX 格式


[@devlin2019]