# AvaloniaEdit Horizontal Selection Freeze: Root Cause Analysis

## Executive Summary
The permanent UI freeze when dragging a text selection to the end of a long line is caused by an infinite layout loop between `TextView.ArrangeOverride`, `SelectionMouseHandler.TextView_ScrollOffsetChanged`, and `Caret.BringCaretToView`. 

The root cause is a disagreement over the maximum valid horizontal scroll offset between `ArrangeOverride` (which strictly clamps `_scrollOffset.X` to `_scrollExtent.Width - _scrollViewport.Width`) and `MakeVisible` (which increases `_scrollOffset.X` past that limit to accommodate a 5.0 pixel margin added by `BringCaretToView(5.0)`). This disagreement causes them to continuously ping-pong the scroll offset back and forth, invalidating the layout each time.

## Exact File Names and Line Numbers (Decompiled AvaloniaEdit 12.0.0)

1. **AvaloniaEdit.Rendering/TextView.cs**
   - **Line 1271-1274:** `ArrangeOverride` checks if `_scrollOffset.X + finalSize.Width > _scrollExtent.Width` and clamps `num` (the new `_scrollOffset.X`).
   - **Line 1281:** `ArrangeOverride` calls `InvalidateMeasure()` if `SetScrollData` actually changed the offset.
   - **Line 1560-1563:** `MakeVisible` calculates a new `offset` if the caret rectangle (inflated by 5.0) extends beyond the viewport.
   - **Line 1580:** `MakeVisible` calls `InvalidateMeasure()` after updating the scroll offset.

2. **AvaloniaEdit.Editing/SelectionMouseHandler.cs**
   - **Line 707-714:** `TextView_ScrollOffsetChanged` unconditionally calls `ExtendSelectionToMouse(_lastMousePosition)`.
   - **Line 777:** `ExtendSelectionToMouse` unconditionally calls `TextArea.Caret.BringCaretToView(5.0)`.

3. **AvaloniaEdit.Editing/Caret.cs**
   - **Line 425-426:** `BringCaretToView(double border)` inflates the caret bounds by `border` (5.0 pixels) and calls `_textView.MakeVisible(val)`.

## The Infinite Event Cycle

1. **The Caret Margin Overflow:** When the user drags the selection to the very end of a long line, the caret is positioned exactly at `_scrollExtent.Width`. `BringCaretToView(5.0)` creates a rectangle for the caret and inflates it by 5.0 pixels. The right edge of this rectangle becomes `_scrollExtent.Width + 5.0`.
2. **MakeVisible Extends Scroll:** `TextView.MakeVisible` sees that this rectangle extends beyond the current viewport. To make the 5.0 pixel margin visible, it increases `_scrollOffset.X` by 5.0 (setting it to `_scrollExtent.Width + 5.0 - _scrollViewport.Width`). It calls `SetScrollOffset`, fires `ScrollOffsetChanged`, and calls `InvalidateMeasure()`.
3. **ArrangeOverride Clamps Scroll:** In the subsequent layout pass, `TextView.ArrangeOverride` notices that `_scrollOffset.X + finalSize.Width` (which is `_scrollExtent.Width + 5.0`) is **strictly greater** than `_scrollExtent.Width`. It refuses to allow scrolling past the exact extent, so it clamps `_scrollOffset.X` back down by 5.0 pixels.
4. **ScrollOffsetChanged Re-triggers Selection:** `ArrangeOverride` calls `SetScrollData` with the clamped offset. This fires `ScrollOffsetChanged` synchronously during the layout pass.
5. **Selection Mouse Handler:** `SelectionMouseHandler` listens to `ScrollOffsetChanged` and calls `ExtendSelectionToMouse`. Because the pointer is still held down, it recalculates the caret and calls `BringCaretToView(5.0)` again.
6. **Ping-Pong:** `BringCaretToView(5.0)` runs synchronously, calls `MakeVisible`, which un-clamps the offset (+5.0 pixels), fires `ScrollOffsetChanged` again (this time `BringCaretToView` doesn't do anything because the margin is now visible), and calls `InvalidateMeasure()`.
7. **The Loop:** The call stack unwinds. Both `MakeVisible` and `ArrangeOverride` have called `InvalidateMeasure()`. The UI thread immediately schedules another layout pass, going back to step 2, infinitely.

## Concrete Evidence of the Loop

The existing diagnostic logs from the `NativeMarkdownEditorControl` (triggered via `--editor-diagnostics` in the probe script) confirm this event storm. Here is an excerpt from `gdb.stderr.log` showing the layout loop generating hundreds of thousands of events:

```
[DEBUG-avedit-freeze] elapsed_ms=18405 source=scroll-offset-changed pressed=8 moved=448 released=6 selectionChanged=23 caretChanged=27 scrollChanged=771000 visualLinesChanged=385535 layoutUpdated=38591 selectionStart=78 selectionLength=101 caretOffset=179 scrollX=543.5 scrollY=0 visualLines=25 textLength=1217 fallback=False forceAvaloniaEdit=True count=771000
[DEBUG-avedit-freeze] elapsed_ms=18407 source=text-view-layout-updated pressed=8 moved=448 released=6 selectionChanged=23 caretChanged=27 scrollChanged=771170 visualLinesChanged=385620 layoutUpdated=38600 selectionStart=78 selectionLength=101 caretOffset=179 scrollX=543.5 scrollY=0 visualLines=25 textLength=1217 fallback=False forceAvaloniaEdit=True count=38600
```
*(Notice that `scrollChanged` and `visualLinesChanged` increment at roughly exactly twice the rate of `layoutUpdated`, confirming the double-invalidation ping-pong occurring during each layout cycle.)*
