# DLSS Swapper LLE changelog

## V1

### Ultra fast library scanning and parsing

- Reads launcher records and scans game folders in parallel: first library scan takes ~15 seconds for a 4,000-game Steam library, with cached reloads instantaneous.
- Adaptive Fast Scan checks known DLL locations; Deep Scan searches complete game folders and learns new locations.
- Adjustable numbers of simultaneous game scans, cover loads and game updates, plus the number of entries added to the screen together. HDD mode reduces simultaneous scans and cover loads for mechanical drives.
- Finds installed games when Steam library indexes are missing or outdated and retains cached entries when a launcher is unavailable.
- Finds covers for manually added games and reuses images already cached by Steam or LLE.

### Native Linux support

- Native x64 desktop application for scanning libraries, importing games in batches, configuring launch executables, updating DLLs and restoring originals.
- Command-line discovery, scanning, DLL updates and restoration using the same saved library as the desktop.
- Finds games in native/Flatpak Steam, Legendary/Heroic and supported launchers in configured Wine prefixes.
- Desktop selection and download of Streamline SDK releases; desktop and terminal operations to inspect, replace, restore and recover game-local Streamline components.

### Manual multi-game import

- Import all games inside a parent folder or select several game folders in one operation.
- Skip duplicate library entries and scan the imported games for supported DLLs.
- Show added, already-present and failed import counts in the Windows import summary.

### Automatic executable detection and batch launch setup

- Optionally scan the entire imported batch and automatically preselect suggested launch executables.
- Review every game together and save all selections in one window, with per-game executable, argument and working-folder overrides.

### Interface and batch updates

- Vertical navigation, grouped Games commands and scrollable dialogs that fit smaller windows. Cover cards resize to fill the window; games can be sorted by name or DLSS version.
- One-click latest-version selection for all detected DLL types and simultaneous updates across selected games. Files already current are skipped; per-game results can be copied or saved.
- Windows batch preset changes check for the matching DLSS component and NVIDIA driver profile, then report changed, already-selected, skipped or failed settings.
- Remove several games from the library at once; excluded launcher games stay hidden after a rescan.
- One Windows Library progress bar combines simultaneous downloads, with percentage and received/total size.
- Reset saved library data, settings and application caches.

### Streamline

- Download a selected NVIDIA Streamline SDK release and apply its component DLLs to one or several games.
- Compare installed, replacement and original versions before upgrading or downgrading.
- Preserve original files across version changes, roll back a failed update and retain recovery files for interrupted updates.

See the [complete feature list](docs/FEATURE_CLUSTERS.md) for platform details and
source files, and [upstream contributions](docs/COMMITS.md#upstream-imports-and-adaptations) for imported changes and credits.
