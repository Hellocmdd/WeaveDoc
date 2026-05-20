Write-Host "Building (Debug)..."
dotnet build -c Debug
$dll = Get-ChildItem -Path "bin/Debug/net10.0" -Filter *.dll -Recurse | Select-Object -First 1
if (-not $dll) {
    Write-Error "Could not find built dll in bin/Debug/net10.0"
    exit 1
}

Write-Host "To debug native crashes on Windows:"
Write-Host "- Install WinDBG (from Windows SDK) or Debugging Tools for Windows."
Write-Host "- Run: cdb -o -G -- dotnet $($dll.FullName)"
Write-Host "Or launch WinDBG and attach to the running dotnet process after starting the app."

Write-Host "You can also enable crash dumps via ProcDump:"
Write-Host "- Download ProcDump and run: procdump -ma -e -x . <pid>"
