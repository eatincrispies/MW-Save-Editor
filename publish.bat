@echo off
setlocal
cd /d "%~dp0"
set OUT=%~dp0publish\NFSMWSaveEditor
tasklist /fi "imagename eq NFSMWSaveEditor.exe" | find /i "NFSMWSaveEditor.exe" >nul
if not errorlevel 1 (
    echo Close NFSMW Save Editor before publishing.
    exit /b 1
)
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet build src\NFSMWSave.App\NFSMWSave.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false --no-incremental --nologo -v q
if errorlevel 1 exit /b 1
dotnet publish src\NFSMWSave.App\NFSMWSave.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false --no-build -o "%OUT%" --nologo -v q
if errorlevel 1 exit /b 1
copy /y docs\README.txt "%OUT%\" >nul
xcopy /e /i /y hashes "%OUT%\hashes" >nul
del "%OUT%\*.pdb" 2>nul
echo Published to %OUT%
