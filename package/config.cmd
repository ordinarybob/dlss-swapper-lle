@echo off

set app_version=1.0.0
set initial_directory=%cd%

set csproj_file=..\src\DLSS Swapper.csproj

set output_installer=Output\DLSS.Swapper-LLE-%app_version%-windows-x64-installer.exe
set output_zip=Output\DLSS.Swapper-LLE-%app_version%-windows-x64-portable.zip
