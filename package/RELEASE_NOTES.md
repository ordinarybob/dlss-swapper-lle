# DLSS Swapper LLE V1

Large Library Edition for Windows and Linux: ultra fast library loading, adaptive
scanning, a redesigned interface and bulk game-management workflows.

## Highlights

- Ultra fast library loading: a 4,000-game Steam library fully scans in approximately 15 seconds. Adaptive Fast Scan, Deep Scan and background artwork loading.
- Storage profiles and live performance controls for different systems.
- Redesigned interface, responsive cover grid, sorting and direct game actions.
- Multi-folder and parent-folder game import, bulk executable scanning and one-window launch setup.
- Automatic artwork for manually added games and shared cover caching.
- One-click update-all and parallel batch DLL updates, with per-game preset applicability checks on Windows.
- Streamline SDK version selection, component comparison, rollback and interrupted-update recovery.
- Combined Library download progress and bulk game removal with persistent exclusions.
- Native Linux desktop and command-line interfaces.

[Features](https://github.com/ordinarybob/dlss-swapper-lle/blob/main/docs/FEATURE_CLUSTERS.md) · [Workflow screenshots](https://github.com/ordinarybob/dlss-swapper-lle/blob/main/docs/SCREENSHOTS.md)

## Requirements

Windows 10 build 19041 or newer; Linux x64 with glibc 2.38 or newer and the
[desktop dependencies](https://github.com/ordinarybob/dlss-swapper-lle/blob/main/linux/README.md#install-and-start).
The .NET runtime is included.

Close affected games before swapping files and retain the original-file backups.

## Credits

Based on [DLSS Swapper v1.2.5.0](https://github.com/beeradmoore/dlss-swapper/releases/tag/v1.2.5.0), with selected fixes, DLL catalog updates and translations from later upstream releases.
The batch-selection foundation comes from RafaelHGOliveira's
[PR #913](https://github.com/beeradmoore/dlss-swapper/pull/913).
