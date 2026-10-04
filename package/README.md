# Packaging

See [building from source](../docs/BUILDING.md) for SDK requirements and commands.
Run the Windows scripts from this directory:

- `build_Portable.cmd` compiles the portable application.
- `package_Portable.cmd` creates its ZIP in `Output/`, using PowerShell 7.
- `build_Installer.cmd` compiles the installer's application files;
  `package_Installer.cmd` creates the installer using NSIS.

The [Linux packaging script](../linux/Package-Portable.ps1) creates one archive
containing the desktop application, CLI and shared runtime.

See the [release notes](RELEASE_NOTES.md) for LLE V1.
