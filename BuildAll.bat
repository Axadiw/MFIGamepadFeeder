echo off
SET VS=2022
SET BUILDER=%ProgramFiles%\Microsoft Visual Studio\%VS%\Community\Msbuild\Current\Bin\MSBuild.exe
SET InnoCompiler=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe


:build32
echo %DATE% %TIME%: Cleaning MFIGamepadFeeder (x86) 
"%BUILDER%"  MFIGamepadFeeder.sln  /maxcpucount:1 /t:clean /p:Platform=x86;Configuration="Release (Local)"
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

echo %DATE% %TIME%: Building MFIGamepadFeeder (x86)
"%BUILDER%"  MFIGamepadFeeder.sln  /maxcpucount:4  /p:Platform=x86;Configuration="Release (Local)"
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

:build64
echo %DATE% %TIME%: Cleaning MFIGamepadFeeder (x64)
"%BUILDER%"  MFIGamepadFeeder.sln  /maxcpucount:1 /t:clean /p:Platform=x64;Configuration="Release (Local)"
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

echo %DATE% %TIME%: Building MFIGamepadFeeder (x64)
"%BUILDER%"  MFIGamepadFeeder.sln  /maxcpucount:4  /p:Platform=x64;Configuration="Release (Local)"
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

:inno32
echo %DATE% %TIME%: Compiling the Inno Setup Script (x86)
IF NOT EXIST "%InnoCompiler%" GOTO NOINNO
"%InnoCompiler%" setup_x86.iss 
set INNO_STATUS=%ERRORLEVEL%
if not %INNO_STATUS%==0 goto fail
echo %DATE% %TIME%: Compiling the Inno Setup Script (x86) - OK

:inno64
echo %DATE% %TIME%: Compiling the Inno Setup Script (x64)
IF NOT EXIST "%InnoCompiler%" GOTO NOINNO
"%InnoCompiler%" setup_x64.iss 
set INNO_STATUS=%ERRORLEVEL%
if not %INNO_STATUS%==0 goto fail
echo %DATE% %TIME%: Compiling the Inno Setup Script (x64) - OK
exit /b 0

:NOINNO
echo %DATE% %TIME%: Could not find Inno Setup Compiler
goto fail

:fail
exit /b 1
