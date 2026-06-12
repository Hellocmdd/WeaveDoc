#!/bin/bash
export DISPLAY=:0
dotnet run --project src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj &
APP_PID=$!
sleep 5
# Now the app is running with NO file opened.
# The user clicks "Open" or passes a file?
# Let's just run it, maybe they pass the file?
# Let's kill it and read the log
kill $APP_PID
cat WeaveDoc.MarkdownEditor.log | tail -n 20
