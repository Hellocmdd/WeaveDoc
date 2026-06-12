#!/bin/bash
Xvfb :99 -screen 0 1280x800x24 &
XVFB_PID=$!
sleep 2

export DISPLAY=:99
dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj -- tests/test_doc/markdown/test-symbols.md &
APP_PID=$!

# Wait for app to start and load preview
sleep 8

# Take screenshot
import -window root /home/tby/.gemini/antigravity/brain/412ccfc4-0048-4b61-8660-f2e5f8ff00e5/screen_fixed_test_debug.png
kill $APP_PID
kill $XVFB_PID
