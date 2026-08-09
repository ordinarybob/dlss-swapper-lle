@echo off

set app_version=1.2.5.1
set initial_directory=%cd%

set csproj_file=..\src\DLSS Swapper.csproj

set output_installer=Output\DLSS.Swapper-LLE-%app_version%-installer.exe
set output_zip=Output\DLSS.Swapper-LLE-%app_version%-portable.zip
