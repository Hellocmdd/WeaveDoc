-- semantic-block-styles.lua
-- Maps explicit Pandoc Div classes to Word custom styles.
--
-- Supported Markdown:
--   ::: {.abstract}
--   摘要正文
--   :::

local semantic_styles = {
  abstract = "Abstract",
  reference = "Reference",
  references = "Reference",
  caption = "Caption",
  footnote = "FootnoteText"
}

function Div(el)
  if not FORMAT:match('docx') then
    return nil
  end

  for _, class in ipairs(el.classes) do
    local style = semantic_styles[class:lower()]
    if style ~= nil then
      el.attributes["custom-style"] = style
      return el
    end
  end

  return nil
end
