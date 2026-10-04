# DLSS Swapper LLE V1

Large Library Edition adds ultra fast library scanning and parsing, native Linux
support, and batch game import with automatic launch-executable detection.

## Highlights

- **Ultra fast library scanning.** First library scan takes ~15 seconds for a 4,000-game Steam library, with cached reloads instantaneous. Parallel library parsing and game scanning, Adaptive Fast Scan and Deep Scan.
- **Native Linux support.** Desktop and command-line applications developed for LLE, with launcher discovery, scanning, batch import, launch setup, DLL updates/restores and Streamline management. Native/Flatpak Steam, Legendary/Heroic and configured Wine prefixes are supported.
- **Batch game import with automatic executable detection.** Add a whole games directory or multiple game folders. LLE scans the batch, preselects suggested launch executables and saves the selections together in one window.
- Storage profiles and live performance controls for different systems.
- Redesigned Windows interface, responsive cover grid, sorting and direct game actions.
- Automatic artwork for manually added games and shared cover caching.
- One-click update-all and parallel batch DLL updates, with per-game preset applicability checks on Windows.
- Streamline SDK version selection, component comparison, rollback and interrupted-update recovery.
- Combined Library download progress and bulk game removal with persistent exclusions.

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
