@echo off

call "%~dp0config.cmd"

mkdir Output > NUL 2>&1

echo.
echo ################################
echo Zipping app
echo ################################
echo.

pwsh.exe -ExecutionPolicy Bypass -Command Import-Module Microsoft.PowerShell.Archive; Compress-Archive -Force -Path "..\src\bin\publish\portable\*" -DestinationPath "%output_zip%" || goto :error

goto :end

:error
echo.
echo.
echo ERROR: Failed with error code %errorlevel%.
cd %initial_directory% > NUL 2>&1
exit /b %errorlevel%

:end
exit /b 0
