# DLSS Swapper LLE V1

Large Library Edition adds ultra fast library scanning and parsing, native Linux
support, manual multi-game import, and batch launch setup.

## Highlights

- **Ultra fast library scanning.** First library scan takes ~15 seconds for a 4,000-game Steam library, with cached reloads instantaneous. Launcher records and game folders are processed in parallel. Adaptive Fast Scan checks known DLL locations; Deep Scan searches complete folders and learns new locations.
- **Native Linux support.** A desktop application for library scanning, batch game import, launch setup, DLL updates and restoration. Terminal commands provide discovery, scanning, updates and restoration using the same saved library. Supports native/Flatpak Steam, Legendary/Heroic and launchers in configured Wine prefixes.
- **Manual multi-game import.** Select several game installation folders, or a parent directory containing them, to add the games in one operation. Duplicate entries are skipped and imported games are scanned for supported DLLs.
- **Automatic executable detection and batch launch setup.** After import, choose launch setup to scan all added games for executables. Review and change suggestions in one window, set per-game launch arguments and working folders, then save the whole batch.
- **Adjustable scan, cover-loading and update limits.** Choose how many games scan or update at once, how many covers load at once, and how many game entries are added to the screen together. HDD mode reduces simultaneous scans and cover loads.
- **Redesigned interface.** Vertical navigation, grouped toolbars, resizable cover cards and dialogs that fit smaller windows. Sort games by name or DLSS version and access update-all from a game's right-click menu.
- **Automatic game covers.** Find covers for manually added games and reuse images already downloaded by Steam or LLE. Windows portable copies can share a cover cache on the game-library drive.
- **One-click update-all and parallel batch updates.** Select the latest versions for all detected DLL types at once and update several games simultaneously. Skip files already current and copy or save per-game results. Windows preset changes check for matching DLSS components and NVIDIA driver profiles.
- **Streamline version switching.** Download a selected SDK release, compare current and replacement component versions, then upgrade or downgrade one or several games. Restore original files or recover interrupted updates.
- **Bulk game removal.** Remove several games from the library at once; excluded launcher games stay hidden after a rescan.
- **Combined download progress.** One Windows Library progress bar shows simultaneous downloads, with percentage and received/total size.

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
