# Building DLSS Swapper LLE

Use the .NET 10 SDK selected by [global.json](../global.json). The applications
are self-contained when packaged; users do not need a separate .NET installation.

## Windows

Build on Windows x64:

```powershell
dotnet publish "src/DLSS Swapper.csproj" -c Release_Portable -r win-x64 --self-contained true
```

To create the portable ZIP, use the existing scripts from the `package` directory
with PowerShell 7 installed:

```powershell
Set-Location package
.\build_Portable.cmd
.\package_Portable.cmd
```

The archive is written to `package/Output`. NSIS is required only for the separate
installer scripts.

## Linux

```sh
dotnet build linux/DlssSwapper.Linux.Cli/DlssSwapper.Linux.Cli.csproj -c Release
dotnet build linux/DlssSwapper.Linux.Gui/DlssSwapper.Linux.Gui.csproj -c Release
dotnet run --project linux/DlssSwapper.Linux.Cli -- --help
dotnet run --project linux/DlssSwapper.Linux.Gui
```

Package the desktop application and CLI with one shared runtime using PowerShell 7.4 or newer:

```sh
dotnet restore linux/DlssSwapper.Linux.Gui/DlssSwapper.Linux.Gui.csproj -r linux-x64
pwsh -File linux/Package-Portable.ps1 -Dotnet dotnet -OutputPath ./DLSS.Swapper-LLE-1.0.0-linux-x64.tar.gz
```

The [Linux guide](../linux/README.md) lists desktop dependencies and runtime requirements.
