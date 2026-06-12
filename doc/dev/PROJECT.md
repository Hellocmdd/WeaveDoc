# Project: WeaveDoc Fix

## Architecture
- WeaveDoc Markdown and PDF editors utilizing Avalonia WebView
- Issue on Linux GTK ExperimentalOffscreen mode causing 1x1 viewport

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | WebView Fix | Fix 1x1 viewport rendering issue in Markdown and PDF editors on Linux | none | DONE |
| 2 | AvaloniaEdit Infinite Loop Fix | Drag-selection freeze fix | 1 | READY_FOR_TEST |

## Code Layout
- Existing codebase in `/home/tby/桌面/WeaveDoc/test`
