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
