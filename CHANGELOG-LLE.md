# DLSS Swapper LLE changelog

## V1

### Library loading and performance

- Cached games become usable while fresh scan results, metadata and artwork load.
- Adaptive Fast Scan learns reusable paths; Deep Scan searches complete game folders.
- Storage profiles and adjustable scan, artwork, update and interface workloads.
- Resilient launcher discovery, reusable artwork and automatic covers for manual games.

### Interface and bulk workflows

- Redesigned navigation, toolbars, responsive cover grids, sorting and dialogs.
- Multi-folder and parent-folder manual game import, with duplicate detection and results.
- Bulk executable scanning and one-window launch setup with per-game selectors and Browse controls.
- One-click update-all, specific DLL-version selection and parallel updates across games.
- Batch NVIDIA preset controls on Windows, with copyable and exportable operation reports.
- Bulk removal and persistent launcher exclusions.
- Library download progress, DLL import/export and application-data reset.

### Streamline

- Historical SDK browsing, package downloads and version selection in Library, game and batch workflows.
- Installed, available and original component comparison, including upgrade and downgrade previews.
- Coordinated component replacement, original-file restore and interrupted-update recovery.

### Linux

- Native x64 desktop and command-line applications sharing one saved library.
- Native/Flatpak Steam, Legendary/Heroic and configured Wine-prefix discovery.
- Manual import, launch setup, artwork, scanning, DLL and Streamline update/restore workflows.

### Upstream contributions

- Batch-selection foundation from RafaelHGOliveira's [PR #913](https://github.com/beeradmoore/dlss-swapper/pull/913), extended for LLE's update workflows.
- Adapted scan-loading/UI-thread correction from upstream #933.
- Japanese translation improvements and Hebrew translation with right-to-left layout.
- Updated built-in DLL catalog through DLSS 310.9.1 and Ray Reconstruction Preset F.

See the [complete feature list](docs/FEATURE_CLUSTERS.md) for platform details and
implementation references, and [attribution](ATTRIBUTION.md) for source credits.
