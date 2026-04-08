# Lua Filter 设计：afd-heading-filter.lua

> 日期：2026-04-08
> 状态：已批准

## 背景

`afd-heading-filter.lua` 是 Pandoc Lua Filter，在 Pandoc 转换过程中拦截 Header 元素。当前实现为空壳（`return el`），仅原样返回标题。

## 分析

### 问题：Lua Filter 标记无法传递到 DOCX

Pandoc 的 DOCX writer **不保留** Lua Filter 中设置的 `attr.attributes`。在 Lua 中给 Header 添加 `afd-style` 属性后，生成的 DOCX 文件中该属性丢失。只有通过 AST JSON 中间步骤才能读取标记，但这会增加流程复杂度。

### 现有管道已完整

`OpenXmlStyleCorrector` 通过以下方式精确修正标题样式：

1. 遍历 DOCX 中所有段落
2. 读取 `ParagraphStyleId`（如 `Heading1`）
3. 通过 `AfdStyleMapper.MapToAfdStyleKey()` 反向映射为 AFD 样式键（如 `heading1`）
4. 从 `AfdTemplate.Styles` 查找样式定义
5. 修正段落属性（对齐、间距、缩进）和字符属性（字体、字号、加粗）

标题样式映射表（`default-thesis.json`）：

| 级别   | 字体 | 字号  | 加粗 | 对齐   | 段前  | 段后  |
|--------|------|-------|------|--------|-------|-------|
| 标题 1 | 黑体 | 16 pt | 是   | 居中   | 24 pt | 18 pt |
| 标题 2 | 黑体 | 14 pt | 是   | 左对齐 | 18 pt | 12 pt |

## 决策

**保持 Lua Filter 为空壳，作为未来扩展点保留。**

不填充 Lua Filter 的理由：
- 现有管道已完整处理标题样式，无需额外介入
- Lua Filter 属性无法传递到 DOCX 输出，标记机制不可行
- `custom-style` 方案会丢失标题大纲语义（影响目录生成）
- 遵循 YAGNI 原则，当前不需要增加复杂度

## 代码变更

仅更新 `afd-heading-filter.lua` 的注释，说明保留原因和未来扩展场景：

```lua
-- afd-heading-filter.lua
-- Pandoc Lua Filter：标题元素拦截扩展点
--
-- 当前状态：空壳（return el），标题样式由 OpenXmlStyleCorrector 后处理完成。
-- OpenXmlStyleCorrector 通过 styleId（Heading1/2/3）+ AfdStyleMapper 识别标题，
-- 无需 Lua Filter 介入。
--
-- 未来扩展场景：
--   - 标题自动编号（如 1.1, 1.2）
--   - 无编号标题（Markdown {-} 语法标记）
--   - 自定义标题格式（如 "第X章"）

return {
    Header = function(el)
        return el
    end,
}
```

无其他文件需要修改。

## 未来扩展

当以下需求出现时再填充 Lua Filter：

1. **标题自动编号**：根据层级生成章节号（1, 1.1, 1.1.1）
2. **无编号标题**：识别 `# 标题 {-}` 语法的无编号标题
3. **标题格式化**：将标题文本转换为特定格式（如中文数字章节号）
