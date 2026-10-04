# DLSS Swapper LLE V1

Large Library Edition for Windows and Linux: fast library loading, adaptive
scanning, a redesigned interface and bulk game-management workflows.

## Highlights

- Fast large-library startup, adaptive Fast Scan and Deep Scan, and background artwork loading.
- Storage profiles and live performance controls for different systems.
- Redesigned interface, responsive cover grid, sorting and direct game actions.
- Multi-folder and parent-folder game import, bulk executable scanning and one-window launch setup.
- Automatic artwork for manually added games and shared cover caching.
- One-click update-all and parallel batch DLL updates, extending the batch and preset foundation from PR #913.
- Streamline SDK version selection, component comparison, rollback and interrupted-update recovery.
- Combined Library download progress and bulk game removal with persistent exclusions.
- Native Linux desktop and command-line interfaces.

The [complete feature list](https://github.com/ordinarybob/dlss-swapper-lle/blob/main/docs/FEATURE_CLUSTERS.md)
includes platform details, upstream attribution and implementation references.

## Downloads

- Windows x64 portable: `DLSS.Swapper-LLE-1.0.0-windows-x64-portable.zip`.
- Linux x64 desktop and CLI: `DLSS.Swapper-LLE-1.0.0-linux-x64.tar.gz`.
- `SHA256SUMS.txt` contains checksums for both archives.

Extract the archive into its own folder. Windows requires Windows 10 build
19041 or newer. Linux requires glibc 2.38 or newer and the desktop libraries
listed in the [Linux guide](https://github.com/ordinarybob/dlss-swapper-lle/blob/main/linux/README.md).
Neither archive requires a separate .NET runtime installation.

Close affected games before swapping files and retain the original-file backups.

## Credits

Based on [DLSS Swapper v1.2.5.0](https://github.com/beeradmoore/dlss-swapper/releases/tag/v1.2.5.0), with selected fixes, DLL catalog updates and translations from later upstream development.
The batch-selection foundation comes from RafaelHGOliveira's
[PR #913](https://github.com/beeradmoore/dlss-swapper/pull/913).
Later upstream imports include the scan-loading fix, Japanese and Hebrew
translations, DLL catalog updates and Ray Reconstruction Preset F.
Upstream and third-party licenses and acknowledgements are included.
