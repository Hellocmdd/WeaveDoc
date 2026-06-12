#!/bin/bash
export DISPLAY=:1
if ! xset q &>/dev/null; then
    export DISPLAY=:0
fi

dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj -- tests/test_doc/markdown/test-symbols.md &
APP_PID=$!

sleep 8

# Find the window ID of WeaveDoc
WID=$(xdotool search --name "WeaveDoc Markdown Editor" | head -n 1)
if [ -n "$WID" ]; then
    import -window "$WID" /home/tby/.gemini/antigravity/brain/412ccfc4-0048-4b61-8660-f2e5f8ff00e5/screenshot_final_test.png
    
    # Generate markdown embedding to view it in artifacts
    cat << 'MD' > /home/tby/.gemini/antigravity/brain/412ccfc4-0048-4b61-8660-f2e5f8ff00e5/screenshot_final_test.md
![Screenshot](/home/tby/.gemini/antigravity/brain/412ccfc4-0048-4b61-8660-f2e5f8ff00e5/screenshot_final_test.png)
MD

fi

kill $APP_PID
