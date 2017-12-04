echo off
SET VS=14.0
SET BUILDER=%ProgramFiles(x86)%\MSBuild\%VS%\Bin\MSBuild.exe
SET Target64=x64\Release
SET Target32=Win32\Release
SET DigiCertUtil=%USERPROFILE%\DESKTOP\DigiCertUtil.exe



:build32
echo %DATE% %TIME%: Cleaning vGen (x86) 
"%BUILDER%"  vGen.sln  /maxcpucount:1 /t:clean /p:Platform=x86;Configuration=Release
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

echo %DATE% %TIME%: Building vGen (x86)
"%BUILDER%"  vGen.sln  /maxcpucount:4  /p:Platform=x86;Configuration=Release
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

:build64
echo %DATE% %TIME%: Cleaning vGen (x64)
"%BUILDER%"  vGen.sln  /maxcpucount:1 /t:clean /p:Platform=x64;Configuration=Release
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail

echo %DATE% %TIME%: Building vGen (x64)
"%BUILDER%"  vGen.sln  /maxcpucount:4  /p:Platform=x64;Configuration=Release
set BUILD_STATUS=%ERRORLEVEL%
if not %BUILD_STATUS%==0 goto fail


:NOUTIL
echo %DATE% %TIME%: Could not find DigiCertUtil on the desktop
goto fail

:NOINNO
echo %DATE% %TIME%: Could not find Inno Setup Compiler
goto fail

:fail
exit /b 1
