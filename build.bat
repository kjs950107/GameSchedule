@echo off
chcp 65001 > nul
cd /d "%~dp0"

where dotnet > nul 2>&1
if errorlevel 1 (
    echo [!] .NET 8 SDK가 설치되어 있지 않습니다.
    echo     https://dotnet.microsoft.com/download/dotnet/8.0 에서 "SDK x64"를 설치한 뒤 다시 실행하세요.
    pause
    exit /b 1
)

echo [1/2] 초기화 로직 테스트...
dotnet run --project tests\CoreTests\CoreTests.csproj -c Release
if errorlevel 1 (
    echo [!] 테스트 실패
    pause
    exit /b 1
)

echo.
echo [2/2] 앱 빌드 (단일 exe)...
dotnet publish src\GameHomework\GameHomework.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
if errorlevel 1 (
    echo [!] 빌드 실패
    pause
    exit /b 1
)

echo.
echo 완료! publish\GameHomework.exe 를 원하는 폴더로 옮겨서 실행하세요.
explorer publish
pause
