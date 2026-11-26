@echo off
if not exist "%~dp0src\MFIGamepadFeeder.Tester\bin\Release\net10.0-windows\Nimbus.InputTester.exe" (
    dotnet build "%~dp0src\MFIGamepadFeeder.Tester\MFIGamepadFeeder.Tester.csproj" -c Release
    if errorlevel 1 (
        echo Install the .NET 10 SDK and try again. See README.md for setup instructions.
        pause
        exit /b 1
    )
)
start "" "%~dp0src\MFIGamepadFeeder.Tester\bin\Release\net10.0-windows\Nimbus.InputTester.exe"
