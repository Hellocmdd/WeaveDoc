-- afd-heading-filter.lua
-- 将 Pandoc AST 中的标题标记为 AFD 兼容格式

return {
    Header = function(el)
        return el
    end,
}
