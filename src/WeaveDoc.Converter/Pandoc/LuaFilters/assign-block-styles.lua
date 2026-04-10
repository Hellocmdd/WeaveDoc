-- assign-block-styles.lua
-- 为 blockquote 和 codeblock 注入 custom-style 属性
--
-- Pandoc 的 DOCX writer 原生支持 custom-style：
-- 当 Div 上设置 custom-style="X" 时，输出段落自动获得 <w:pStyle w:val="X"/>
-- 这样 OpenXmlStyleCorrector 可通过 styleId 反查 AFD 样式键并应用格式。
--
-- list（有序/无序列表）由 Pandoc 自动赋予 ListParagraph styleId，无需处理。

function BlockQuote(el)
  if FORMAT:match('docx') then
    return pandoc.Div(el.content, pandoc.Attr("", {}, {["custom-style"] = "Blockquote"}))
  end
end

function CodeBlock(el)
  if FORMAT:match('docx') then
    return pandoc.Div({el}, pandoc.Attr("", {}, {["custom-style"] = "CodeBlock"}))
  end
end
