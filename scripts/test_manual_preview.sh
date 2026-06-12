#!/bin/bash
dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj &
APP_PID=$!

# Wait for window to appear
sleep 4

# Use xdotool to find the window and click the "Preview" menu item.
# Assuming the menu bar is at the top. The Preview button is probably reachable via alt+... or just clicking it.
# Actually, xdotool key Alt+P might toggle preview if we have mnemonics, but we don't.
# Let's just click the coordinates where "Preview" menu item is.
WID=$(xdotool search --name "WeaveDoc" | head -n 1)
xdotool windowactivate $WID
sleep 0.5

# WeaveDoc has a menu: File, Edit, Format, View... Preview is a top-level menu item?
# Let's check MainWindow.axaml for the menu layout.
