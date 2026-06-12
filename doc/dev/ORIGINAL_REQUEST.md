# Original User Request

## Initial Request — 2026-06-12T12:45:57+08:00

# Teamwork Project Prompt — Draft

> Status: Launched
> Goal: Craft prompt → get user approval → delegate to teamwork_preview

Fix the permanent UI freeze bug in AvaloniaEdit caused by the infinite layout loop between `TextView.ArrangeOverride` and `Caret.BringCaretToView` when dragging a text selection to the end of a long line.

Working directory: /home/tby/桌面/WeaveDoc/test
Integrity mode: development

## Requirements

### R1. Fix the Infinite Layout Loop
Resolve the ping-pong conflict between `MakeVisible` (+5.0px margin) and `ArrangeOverride` (scroll clamping) that causes the `ScrollOffsetChanged` event storm. You may modify the locally cloned AvaloniaEdit source code or WeaveDoc's editor components to achieve this.

### R2. Preserve Core Functionality
Ensure that horizontal auto-scrolling during drag-selection still works correctly when the mouse moves to the edge of the viewport. Do not break any existing Markdown editing, layout, or syntax highlighting features.

### R3. Semi-Automated Validation
Use the existing `scripts/xtest_avedit_selection_probe.sh` script to verify the fix. Prompt the user ("提醒我") when you need to run the XTest probe so they can prepare their desktop environment.

## Acceptance Criteria

### Verification
- [ ] Running `scripts/xtest_avedit_selection_probe.sh --force-avaloniaedit` must pass successfully (the UI remains responsive after 10 seconds, and the script exits normally).
- [ ] `dotnet build` must succeed without new warnings.
- [ ] All existing automated tests (`dotnet test`) must pass without errors.
- [ ] No regression in the ability to drag-select text horizontally.
