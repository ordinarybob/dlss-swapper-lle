# DLSS Swapper LLE changelog

## V1

### Ultra fast library scanning and parsing

- Parallel library parsing and game scanning: first library scan takes ~15 seconds for a 4,000-game Steam library, with cached reloads instantaneous.
- Adaptive Fast Scan learns reusable paths; Deep Scan searches complete game folders.
- Storage profiles and adjustable scan, artwork, update and interface workloads.
- Resilient launcher discovery, reusable artwork and automatic covers for manual games.

### Native Linux support

- LLE adds native x64 desktop and command-line applications sharing one saved library.
- Native/Flatpak Steam, Legendary/Heroic and configured Wine-prefix discovery.
- Library scanning, batch game import, automatic launch-executable detection, artwork, DLL and Streamline updates/restores, and performance controls.

### Batch game import and automatic executable detection

- Import all games inside a parent folder or select several game folders in one operation.
- Scan the entire imported batch and automatically preselect suggested launch executables.
- Review every game together and save all selections in one window, with per-game executable, argument and working-folder overrides.

### Interface and batch updates

- Redesigned Windows navigation, toolbars, responsive cover grids, sorting and dialogs.
- One-click update-all, specific DLL-version selection and parallel updates across games.
- Per-game preset applicability checks on Windows, with copyable and exportable batch reports.
- Bulk removal and persistent launcher exclusions.
- Combined Library download progress and application-data reset.

### Streamline

- Historical SDK browsing, package downloads and version selection in Library, game and batch workflows.
- Installed, available and original component comparison, including upgrade and downgrade previews.
- Coordinated component replacement, original-file restore and interrupted-update recovery.

See the [complete feature list](docs/FEATURE_CLUSTERS.md) for platform details and
source files, and [upstream contributions](docs/COMMITS.md#upstream-imports-and-adaptations) for imported changes and credits.
