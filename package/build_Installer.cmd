@echo off

call "%~dp0config.cmd"

rmdir /s /q ..\src\bin\publish\installer\
rmdir /s /q ..\src\obj\

mkdir Output > NUL 2>&1

echo.
echo ################################
echo Compiling app
echo ################################
echo.

dotnet publish "%csproj_file%" ^
	--runtime win-x64 ^
    --self-contained ^
    --configuration Release ^
    -p:PublishDir=bin\publish\installer\ || goto :error

goto :end

:error
echo.
echo.
echo ERROR: Failed with error code %errorlevel%.
cd %initial_directory% > NUL 2>&1
exit /b %errorlevel%

:end
exit /b 0
