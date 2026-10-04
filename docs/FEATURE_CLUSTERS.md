# DLSS Swapper LLE feature clusters

LLE additions and changes compared with upstream `v1.2.5.0` (`4c58e19`), with platform details and supporting commits. Selective upstream imports are recorded separately in the [commit inventory](COMMITS.md) and [attribution](../ATTRIBUTION.md).

## Feature groups

### F01 Resilient game discovery and Linux launcher integration

Windows and Linux.

Linux discovery supports native and Flatpak Steam, Epic through Legendary/Heroic, GOG through Heroic, and Epic, GOG, Ubisoft Connect, EA App and Battle.net installations in configured Wine prefixes.

Finds installed games despite stale or missing Steam library indexes, preserves cached entries when a source is unavailable, and handles missing launcher metadata.

**Commits:** [f42128f](https://github.com/ordinarybob/dlss-swapper-lle/commit/f42128f9a35a4b90cb65989a97dc89f17381f4d4), [16721dc](https://github.com/ordinarybob/dlss-swapper-lle/commit/16721dc7a14a2bdc507fe9fb4e60530fc451dc3a), [140543d](https://github.com/ordinarybob/dlss-swapper-lle/commit/140543d05cda54516dc7722466eb2858ed7f1ac3), [b5e4402](https://github.com/ordinarybob/dlss-swapper-lle/commit/b5e4402852055981a5ea8699061d0b13206dd071), [48b903a](https://github.com/ordinarybob/dlss-swapper-lle/commit/48b903a768469477400ed9eeb6ff7da0037deecc), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/Data/Steam/SteamLibrary.cs](../src/Data/Steam/SteamLibrary.cs); [linux/DlssSwapper.Linux.Cli/Platform/SteamDiscovery.cs](../linux/DlssSwapper.Linux.Cli/Platform/SteamDiscovery.cs); [linux/DlssSwapper.Linux.Cli/Platform/ProviderDiscovery.cs](../linux/DlssSwapper.Linux.Cli/Platform/ProviderDiscovery.cs).

### F02 Adaptive Fast Scan and Deep Scan

Windows and Linux.

Finds supported game DLLs using learned Fast Scan paths or an exhaustive Deep Scan. The first scan learns additional paths; later scans reuse them, share directory traversal and cache empty results to avoid repeated work.

Adds built-in scan-pattern display and editing for learned and custom Fast Scan paths.

**Commits:** [f3fdb30](https://github.com/ordinarybob/dlss-swapper-lle/commit/f3fdb30f89d8d394af70fccf68f11934472ee945), [dc2237a](https://github.com/ordinarybob/dlss-swapper-lle/commit/dc2237affc2fb660beea90278cb9f3149ad4d824), [4021e63](https://github.com/ordinarybob/dlss-swapper-lle/commit/4021e638b462c0ed8b218353791c3b586d31066e), [182f6b9](https://github.com/ordinarybob/dlss-swapper-lle/commit/182f6b90ec6359f67236c9aa7e69a48a601d2341), [95ede66](https://github.com/ordinarybob/dlss-swapper-lle/commit/95ede6689505fc1630ba893e8343d38ad9db6ecb), [69cd628](https://github.com/ordinarybob/dlss-swapper-lle/commit/69cd628389469c7075ac6923354a9a7853751df4), [f435cd7](https://github.com/ordinarybob/dlss-swapper-lle/commit/f435cd7f7717bff0f20ff5f88903880855656052), [787e8b7](https://github.com/ordinarybob/dlss-swapper-lle/commit/787e8b75d350acf8b239a3dee1f8e73032b6444a), [1b2753a](https://github.com/ordinarybob/dlss-swapper-lle/commit/1b2753a8ce5c2d809a7275648d5760dbf7c6152e), [cb1389a](https://github.com/ordinarybob/dlss-swapper-lle/commit/cb1389a7b8dcf465e1b2931feb3ad368a17f716c), [bb2d3bc](https://github.com/ordinarybob/dlss-swapper-lle/commit/bb2d3bcdead657f2069ebcde02bacda73228f47d), [ce1d055](https://github.com/ordinarybob/dlss-swapper-lle/commit/ce1d055cb6360aa46a332db6feb7b18919da1ced), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Data/GameAssetPathIndex.cs](../src/Data/GameAssetPathIndex.cs); [src/Data/GameAssetCandidatePathIndex.cs](../src/Data/GameAssetCandidatePathIndex.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryScanService.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryScanService.cs).

### F03 Fast large-library startup and responsive background loading

Windows and Linux.

Makes cached games usable before the full scan, artwork and metadata loading finish. Shows completed results progressively, reduces repeated status updates and clears loading state after errors. Includes the UI-thread/loading fix from upstream #933, adapted in [8abfd44](https://github.com/ordinarybob/dlss-swapper-lle/commit/8abfd44ed8b58cfa14745ad4bb08ac5ca7e28f68).

Artwork loads independently. Screen updates are applied in short, size-limited batches that yield between updates, keeping the interface responsive as the library fills.

On Windows, repeated database updates are combined into batched transactions. DLL hashes are reused when file size and modification time match; otherwise, full-file hashing is deferred until needed instead of reading every DLL during initial loading.

**Commits:** [0099e3f](https://github.com/ordinarybob/dlss-swapper-lle/commit/0099e3f88feedbbc4bb9b1e93d26b945a7a4ab78), [3e19518](https://github.com/ordinarybob/dlss-swapper-lle/commit/3e195186fbc83cd62c1272e4003c5faef600acf3), [bb657c7](https://github.com/ordinarybob/dlss-swapper-lle/commit/bb657c73b1136ffdeea00947e406e5abef6b9e13), [182f6b9](https://github.com/ordinarybob/dlss-swapper-lle/commit/182f6b90ec6359f67236c9aa7e69a48a601d2341), [afc9365](https://github.com/ordinarybob/dlss-swapper-lle/commit/afc9365245cee0b75b8efcb915c32bd3cd4f9868), [49d8ef3](https://github.com/ordinarybob/dlss-swapper-lle/commit/49d8ef3961d14170febfdfdc3ab80fc85db2d087), [c8b2058](https://github.com/ordinarybob/dlss-swapper-lle/commit/c8b20581967c19fe6b93febf5b7b368484e6bc58), [95703a6](https://github.com/ordinarybob/dlss-swapper-lle/commit/95703a68314e19b5b00a1d28842cf79f41db0578), [d2b825c](https://github.com/ordinarybob/dlss-swapper-lle/commit/d2b825c410b02b76993306ea6f61ba9bc5b52c64), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [8abfd44](https://github.com/ordinarybob/dlss-swapper-lle/commit/8abfd44ed8b58cfa14745ad4bb08ac5ca7e28f68), [19352e4](https://github.com/ordinarybob/dlss-swapper-lle/commit/19352e42a7d3f826738d9ac207b65dab7a2f2579), [b637dcb](https://github.com/ordinarybob/dlss-swapper-lle/commit/b637dcbe63cfd66c8e6b61f05488eb10519c22a6).

**Implementation references:** [src/Pages/GameGridPageModel.InitialLoad.cs](../src/Pages/GameGridPageModel.InitialLoad.cs); [src/Data/GameManager.cs](../src/Data/GameManager.cs); [src/Data/GameDatabaseWriteBatch.cs](../src/Data/GameDatabaseWriteBatch.cs); [src/Data/GameAsset.cs](../src/Data/GameAsset.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryStartup.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryStartup.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.Publication.cs](../linux/DlssSwapper.Linux.Gui/MainWindow.Publication.cs).

### F04 Performance profiles and live tuning for different systems

Windows and Linux.

Adds first-start storage profiles and independent performance controls for scanning, artwork, game updates and batching. Numeric controls support direct editing, with scan and artwork limits adjustable during operation.

HDD and standard profiles provide starting settings; individual controls let users reduce background work on slower systems or increase parallel work on faster hardware:

- Number of simultaneous game scans.
- Number of simultaneous artwork requests.
- Number of simultaneous game updates.
- Number of entries added to the interface per batch.
- Number of database writes per batch on Windows.

Adds performance-settings reset.

**Commits:** [19352e4](https://github.com/ordinarybob/dlss-swapper-lle/commit/19352e42a7d3f826738d9ac207b65dab7a2f2579), [7a26524](https://github.com/ordinarybob/dlss-swapper-lle/commit/7a26524163d1b25dd9545789ae957c9d0f9cad90), [159e230](https://github.com/ordinarybob/dlss-swapper-lle/commit/159e23075cda300a3cfcd7235b274e3d686d2c72), [40f3bf1](https://github.com/ordinarybob/dlss-swapper-lle/commit/40f3bf1b23867c2c4113df6fff50922cd5a4a794), [5c6780a](https://github.com/ordinarybob/dlss-swapper-lle/commit/5c6780a6aa9e24955cb85c3d770c72e62f249f29), [ba90b23](https://github.com/ordinarybob/dlss-swapper-lle/commit/ba90b23a98f975d444ca37c8eb48241581909297), [001fd5b](https://github.com/ordinarybob/dlss-swapper-lle/commit/001fd5b573cb0748c14cc060d69edc0174de3312), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Settings.cs](../src/Settings.cs); [src/UserControls/PerformanceNumberEditor.xaml](../src/UserControls/PerformanceNumberEditor.xaml); [linux/DlssSwapper.Linux.Cli/Core/LibraryState.Performance.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryState.Performance.cs).

### F05 Manual multi-folder and parent-folder batch game import

Windows and Linux.

Extends upstream's single-game import with two additional manual game-loading workflows:

- **Multi-folder import:** adds several individually selected game installation folders in one operation.
- **Parent-folder batch import:** adds each immediate child folder of a selected games directory as a separate manually added game.

Both validate folders, skip duplicate entries, save the new games and start DLL discovery and artwork loading. Imported games feed into the shared executable-selection workflow. Import notices and the launch-setup choice have saved preferences.

The Windows batch summary records added, already-present and failed imports, with details for each failed folder.

**Commits:** [32e4e1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/32e4e1f71403552d44a926c23ae6eb8e4247a2bf), [3d58a48](https://github.com/ordinarybob/dlss-swapper-lle/commit/3d58a484292e7ba7457ee1ff99ce94fc629da960), [281b4bb](https://github.com/ordinarybob/dlss-swapper-lle/commit/281b4bbeeb526fdabadf99475b14ab54cd1b88fe), [1c466d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/1c466d9eabfd695d6e6d208af65181801126b1ca), [d79ed42](https://github.com/ordinarybob/dlss-swapper-lle/commit/d79ed4214bb6db7ff5a37a818d205aaedac29b4a), [a4ea439](https://github.com/ordinarybob/dlss-swapper-lle/commit/a4ea43984c9ff4cd59fbdc8db456d8ca94a3504e), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Pages/GameGridPageModel.cs](../src/Pages/GameGridPageModel.cs); [linux/DlssSwapper.Linux.Cli/Core/ManualGameImportWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/ManualGameImportWorkflow.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryState.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryState.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs).

### F06 Bulk removal with persistent launcher exclusions

Windows and Linux.

Removes selected manual library entries or excludes launcher-discovered games. Exclusions survive rescans and can be restored. Failed saves preserve the affected entry and report the error.

**Commits:** [32e4e1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/32e4e1f71403552d44a926c23ae6eb8e4247a2bf), [4238177](https://github.com/ordinarybob/dlss-swapper-lle/commit/4238177934f4a53f302b382ecdec708e45efd0f2), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Pages/GameGridPageModel.cs](../src/Pages/GameGridPageModel.cs); [src/UserControls/GameControlModel.Metadata.cs](../src/UserControls/GameControlModel.Metadata.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryRemovalWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryRemovalWorkflow.cs).

### F07 Bulk executable scanning and selection for manually added games

Windows and Linux.

Adds launching for manually imported games using saved executables, arguments and working folders. Linux supports native executables and Wine, with configurable Wine executable and prefix.

- **Bulk executable scan:** scans the entire imported set before selection begins, with parallel scanning, progress and cancellation. Searches nested game directories and preserves saved executable choices.
- **Ranked suggestions:** matches game titles, abbreviations, sequel numbers and executable metadata; filters helper processes and ranks the main application ahead of companion utilities.
- **Shared selection window:** one bounded, scrollable list with a game name, executable selector and Browse button on each row. Selectors show filenames when closed and full paths when open. Per-game launch arguments and working folders remain editable.
- **Batch save controls:** Apply, Save and close, and Skip and close handle the whole list. Suggested and edited selections are saved together; unselected rows are highlighted, and partial saving can skip blank rows. Failed saves retain edits and identify the affected games.

Saved launch settings can also be reopened for an individual manually added game.

**Commits:** [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [4a34d03](https://github.com/ordinarybob/dlss-swapper-lle/commit/4a34d0343e30edde54d4c63663e6a29de1b273e6), [c72346d](https://github.com/ordinarybob/dlss-swapper-lle/commit/c72346d16b48572c9ace234515064c6e3331cdda), [283184b](https://github.com/ordinarybob/dlss-swapper-lle/commit/283184bf48bbde2a4d8af209ec61493b431f6504), [2df6c1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/2df6c1f85827307452157da79d0e0d58bb564391), [e129dd9](https://github.com/ordinarybob/dlss-swapper-lle/commit/e129dd9eb46064ae3c27481ee767f6fceadae729), [d8bdc24](https://github.com/ordinarybob/dlss-swapper-lle/commit/d8bdc249daa24cfba42b5b2324f2a304b8fce499), [63805d4](https://github.com/ordinarybob/dlss-swapper-lle/commit/63805d4b7207331b59fbc836e25e9d772b205797).

**Implementation references:** [src/UserControls/ManualLaunchSetup.cs](../src/UserControls/ManualLaunchSetup.cs); [src/UserControls/ManualLaunchSetup.Bulk.cs](../src/UserControls/ManualLaunchSetup.Bulk.cs); [src/UserControls/ManualLaunchSetupDialog.cs](../src/UserControls/ManualLaunchSetupDialog.cs); [src/Data/GameManager.cs](../src/Data/GameManager.cs); [shared/DlssSwapper.Shared/ManualLaunch/ManualLaunchManifest.cs](../shared/DlssSwapper.Shared/ManualLaunch/ManualLaunchManifest.cs); [linux/DlssSwapper.Linux.Gui/ManualLaunchSetupWindow.cs](../linux/DlssSwapper.Linux.Gui/ManualLaunchSetupWindow.cs); [linux/DlssSwapper.Linux.Cli/Core/ManualGameLaunch.cs](../linux/DlssSwapper.Linux.Cli/Core/ManualGameLaunch.cs).

### F08 Automatic artwork for manual games and shared cover caching

Windows and Linux.

Adds automatic covers for manually added games through Steam title matching and a configurable MediaWiki fallback requiring no API key. Expands Steam cache discovery and shared artwork reuse, limits concurrent requests and retries temporary misses.

Cached covers are reused without another download. On Windows, a shared artwork cache on the game-library drive makes those covers reusable across portable LLE instances, and available cached covers are attached before game cards appear.

**Commits:** [7f4f0d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f4f0d94d8a8d4f05250c6e9a1ecd5f23d73e50a), [f24dc18](https://github.com/ordinarybob/dlss-swapper-lle/commit/f24dc18163afcc6d72d88de87f4bc89a991147b0), [b9ad50f](https://github.com/ordinarybob/dlss-swapper-lle/commit/b9ad50f68b6bec3a319d4a7cb8c3cb0cbf676784), [0f37990](https://github.com/ordinarybob/dlss-swapper-lle/commit/0f37990ca5324e4c3ff94044aafcda3d02fc6120), [44626e3](https://github.com/ordinarybob/dlss-swapper-lle/commit/44626e349b0627890451f631f8c210a50153debc), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Data/Steam/SteamArtworkLookup.cs](../src/Data/Steam/SteamArtworkLookup.cs); [src/Data/ManuallyAdded/WikipediaArtworkLookup.cs](../src/Data/ManuallyAdded/WikipediaArtworkLookup.cs); [linux/DlssSwapper.Linux.Cli/Core/ArtworkService.cs](../linux/DlssSwapper.Linux.Cli/Core/ArtworkService.cs).

### F09 Sort the game library

Windows and Linux.

Sorts the library by game name or detected DLSS version and preserves the selected order alongside existing filters.

**Commits:** [d22493d](https://github.com/ordinarybob/dlss-swapper-lle/commit/d22493d2787868b57bff2e072d283a19108539b2), [4238177](https://github.com/ordinarybob/dlss-swapper-lle/commit/4238177934f4a53f302b382ecdec708e45efd0f2), [5286737](https://github.com/ordinarybob/dlss-swapper-lle/commit/528673714e001525acf1a68993daee73a503fd1a), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/Data/GameSortMode.cs](../src/Data/GameSortMode.cs); [src/Pages/GameGridPageModel.cs](../src/Pages/GameGridPageModel.cs); [linux/DlssSwapper.Linux.Cli/Core/GameViewPolicy.cs](../linux/DlssSwapper.Linux.Cli/Core/GameViewPolicy.cs).

### F10 Adjustable cover grid that fills the window

Windows and Linux.

Replaces fixed-width cover sizing with a responsive grid that fills the available width and preserves cover proportions. Adds an inline Card size selector to the Windows Steam grid.

**Commits:** [07a9b80](https://github.com/ordinarybob/dlss-swapper-lle/commit/07a9b8053c2170939781459a770d18b9bfb356aa), [f715332](https://github.com/ordinarybob/dlss-swapper-lle/commit/f715332fbe138de6dda108e205b7ac9937e09518), [cdb40cf](https://github.com/ordinarybob/dlss-swapper-lle/commit/cdb40cfb113cc72cd66f43304871ada5424f5445), [848df8d](https://github.com/ordinarybob/dlss-swapper-lle/commit/848df8de0b8ae14d05a2b3c9c8b27d07e1f9ec97), [4633a30](https://github.com/ordinarybob/dlss-swapper-lle/commit/4633a303d4166353e3789220ec34e2d36d66df23), [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/Pages/ResponsiveGameGridLayout.cs](../src/Pages/ResponsiveGameGridLayout.cs); [shared/DlssSwapper.Shared/ResponsiveGridLayout.cs](../shared/DlssSwapper.Shared/ResponsiveGridLayout.cs).

### F11 Redesigned Windows and Linux interface

Windows and Linux.

Reworks the application's navigation, page layouts and dialogs:

- **Navigation:** compact vertical icon controls for Games, Library and Settings, leaving more space for page content.
- **Games:** redesigned header with game count, search and grouped icon-and-text commands; batch actions sit in their own row below the toolbar. Controls rearrange as the window narrows.
- **Library:** redesigned action bar, horizontally scrollable component tabs and compact version cards with aligned download, export and delete controls.
- **Game details and history:** compact, scrollable dialogs that fit the available window; game details adapt the cover panel and action-button layout to the window width.
- **Window behaviour:** display-aware default sizing and saved-window restoration that keeps the window on screen.
- **Linux desktop:** corresponding navigation, toolbars, page styling and game-grid layout in the Avalonia interface.

**Commits:** [9b59d85](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b59d8566cf9ea18f94a87d5bd9c32bd0757781c), [7f09c1d](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f09c1ddbf823cead6c3dd542d1fbd7daac5d2ae), [fb0caa3](https://github.com/ordinarybob/dlss-swapper-lle/commit/fb0caa31f5816b13058b53daf4cb5b0019966aaa), [6bd2e19](https://github.com/ordinarybob/dlss-swapper-lle/commit/6bd2e19a2389c94a57f04f15c90606b4ebe9d0b4), [10edb39](https://github.com/ordinarybob/dlss-swapper-lle/commit/10edb39c5eeabf4523482911718c9a7402c5bcaf), [269c65d](https://github.com/ordinarybob/dlss-swapper-lle/commit/269c65df43ebd8886659b30ba2c6bd8a8ab99344), [51738bf](https://github.com/ordinarybob/dlss-swapper-lle/commit/51738bff26e7a61bf50bfc5da57dd3ae91a012f8), [cdb40cf](https://github.com/ordinarybob/dlss-swapper-lle/commit/cdb40cfb113cc72cd66f43304871ada5424f5445), [7f4f0d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f4f0d94d8a8d4f05250c6e9a1ecd5f23d73e50a), [0f37990](https://github.com/ordinarybob/dlss-swapper-lle/commit/0f37990ca5324e4c3ff94044aafcda3d02fc6120), [f435cd7](https://github.com/ordinarybob/dlss-swapper-lle/commit/f435cd7f7717bff0f20ff5f88903880855656052), [e75a4e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/e75a4e72da1502ff4421339d599d6a10e676a525), [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [src/MainWindow.xaml](../src/MainWindow.xaml); [src/MainWindow.xaml.cs](../src/MainWindow.xaml.cs); [src/Pages/GameGridPage.xaml](../src/Pages/GameGridPage.xaml); [src/Pages/LibraryPage.xaml](../src/Pages/LibraryPage.xaml); [src/UserControls/GameControl.xaml](../src/UserControls/GameControl.xaml); [src/UserControls/GameControlModel.cs](../src/UserControls/GameControlModel.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml).

### F12 Direct game actions through text context menus

Windows and Linux.

Adds a right-click text menu for direct access to game actions, including LLE's update-all command.

**Commits:** [ed42f95](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed42f95ed555ba59d12a9c5a43f9a9e84ffe596f), [3b01d2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/3b01d2eda24ca1c2fabb0edb1e1a54856c882809), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/UserControls/GameControlModel.Metadata.cs](../src/UserControls/GameControlModel.Metadata.cs); [src/Pages/GameGridPage.xaml](../src/Pages/GameGridPage.xaml); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs).

### F13 Staged DLL replacement and backup validation

Windows and Linux.

Adds staged writes, file-identity and backup checks, and duplicate-Apply protection to the existing DLL swap/restore operation. Linux reads Windows DLL version metadata directly.

**Commits:** [fb0caa3](https://github.com/ordinarybob/dlss-swapper-lle/commit/fb0caa31f5816b13058b53daf4cb5b0019966aaa), [6bd2e19](https://github.com/ordinarybob/dlss-swapper-lle/commit/6bd2e19a2389c94a57f04f15c90606b4ebe9d0b4), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [8069d8b](https://github.com/ordinarybob/dlss-swapper-lle/commit/8069d8b9d25db0016769659ba0934f8d84840bdc), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [src/UserControls/DLLPickerControlModel.Apply.cs](../src/UserControls/DLLPickerControlModel.Apply.cs); [src/UserControls/GameControl.xaml](../src/UserControls/GameControl.xaml); [src/Helpers/StagedFile.cs](../src/Helpers/StagedFile.cs); [linux/DlssSwapper.Linux.Cli/Core/DllRestoreWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/DllRestoreWorkflow.cs).

### F14 One-click update-all and parallel batch DLL updates

LLE extension of upstream PR #913; Windows and Linux.

Adds a per-game action to update all detected DLL families and parallel updates across selected games. Extends PR #913's batch selection, picker and result summary through a shared update workflow. Skips current versions, limits parallel work, reuses source verification, reduces copying/database writes and reports each result.

Adds one-click latest-version selection and individual version selection for each DLL family. Missing selected packages are downloaded before use. Results distinguish changed files, already-current files, skipped items and errors.

Adds clipboard copying and text-file export of batch reports. Fixes Windows crashes caused by download-completion dialogs opening over batch setup.

**Commits:** [aa7ec4b](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa7ec4b0a2427fe79bf99a7d7642cd201844ffff), [2a6b54b](https://github.com/ordinarybob/dlss-swapper-lle/commit/2a6b54bda9aa3bf7f63cd92f0673ed5eab6edfbe), [b637dcb](https://github.com/ordinarybob/dlss-swapper-lle/commit/b637dcbe63cfd66c8e6b61f05488eb10519c22a6), [aa5f1bb](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa5f1bb4a77cc76dd5734711359e68b6cdebb912), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f), [8069d8b](https://github.com/ordinarybob/dlss-swapper-lle/commit/8069d8b9d25db0016769659ba0934f8d84840bdc).

**Implementation references:** [src/Data/DllUpdateWorkflow.cs](../src/Data/DllUpdateWorkflow.cs); [src/Helpers/VerifiedDllSource.cs](../src/Helpers/VerifiedDllSource.cs); [src/UserControls/BatchSwapSummaryControl.xaml.cs](../src/UserControls/BatchSwapSummaryControl.xaml.cs); [src/UserControls/EasyContentDialog.cs](../src/UserControls/EasyContentDialog.cs); [linux/DlssSwapper.Linux.Cli/Core/BatchUpdateWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/BatchUpdateWorkflow.cs); [linux/DlssSwapper.Linux.Gui/OperationReportWindow.cs](../linux/DlssSwapper.Linux.Gui/OperationReportWindow.cs).

### F15 Batch preset applicability and per-game results

LLE restoration and extension of PR #913's preset foundation; Windows only.

Extends the imported batch preset workflow with matching-profile checks and per-game results that distinguish inapplicable, already-current, successful and failed changes.

**Commits:** [aa7ec4b](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa7ec4b0a2427fe79bf99a7d7642cd201844ffff), [4beeead](https://github.com/ordinarybob/dlss-swapper-lle/commit/4beeead33b164cde0376266775b23fdc0835594f), [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7).

**Implementation references:** [src/Data/BatchPresetUpdateWorkflow.cs](../src/Data/BatchPresetUpdateWorkflow.cs); [src/UserControls/BatchPresetRowModel.cs](../src/UserControls/BatchPresetRowModel.cs).

### F16 Streamline version switching, rollback and interrupted-update recovery

Windows and Linux.

Adds coordinated Streamline component updates with rollback and recovery after interrupted writes. Browses historical NVIDIA Streamline SDK releases, downloads and caches the selected x64 package, and compares it with installed components. Updates selected existing components across one or more games and restores pre-swap backups. Parallel updates use per-game-folder locking.

Version selection is available in the Library, individual-game and batch-update flows. The component view shows installed, available and original versions together; switching SDK versions preserves the first pre-swap backup for restoration.

Previews upgrades, downgrades and same-version file differences before applying a selection.

**Commits:** [5726d4d](https://github.com/ordinarybob/dlss-swapper-lle/commit/5726d4da4cc63a5fad0da2cc3f295bd955a3a7d0), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [5e15ecf](https://github.com/ordinarybob/dlss-swapper-lle/commit/5e15ecfe2c8d738ebecb0c6d0e86fcc2c8f984d1), [59031ac](https://github.com/ordinarybob/dlss-swapper-lle/commit/59031acf796f6e73e18454c2b4023e07591e1722), [708e7da](https://github.com/ordinarybob/dlss-swapper-lle/commit/708e7da5cc9165be43fd659f8c9f734c7bde3eb2), [a8a794e](https://github.com/ordinarybob/dlss-swapper-lle/commit/a8a794ee2ab9855c679a62a8968ab5bdf9d82853), [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [shared/DlssSwapper.Shared/Streamline/StreamlineSdkAcquisition.cs](../shared/DlssSwapper.Shared/Streamline/StreamlineSdkAcquisition.cs); [shared/DlssSwapper.Shared/Streamline/StreamlineComponentSet.cs](../shared/DlssSwapper.Shared/Streamline/StreamlineComponentSet.cs); [shared/DlssSwapper.Shared/Streamline/StreamlineDecisionPreview.cs](../shared/DlssSwapper.Shared/Streamline/StreamlineDecisionPreview.cs); [src/UserControls/StreamlineComponentsControl.xaml](../src/UserControls/StreamlineComponentsControl.xaml); [linux/DlssSwapper.Linux.Gui/StreamlineGameWindow.cs](../linux/DlssSwapper.Linux.Gui/StreamlineGameWindow.cs).

### F17 Combined Library and Streamline download progress

Windows and Linux.

Shows download and preparation progress, clears stale errors and supports retries. Includes aggregate Library progress on Windows and Streamline download progress on both platforms.

The Windows Library header combines simultaneous transfers into one progress bar, including Download Latest, with percentage and received/total size. File preparation has its own status after transfer completes.

**Commits:** [ffa7f0c](https://github.com/ordinarybob/dlss-swapper-lle/commit/ffa7f0c67097bf81590e7d7b27da2d9f72c77000), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [04d3e64](https://github.com/ordinarybob/dlss-swapper-lle/commit/04d3e64264a98766d5b19ba2c6b287a520906b43), [3cf87f2](https://github.com/ordinarybob/dlss-swapper-lle/commit/3cf87f2e3afe3fa909f6fe91a5d01898bef21389), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [src/Pages/LibraryPageModel.DownloadProgress.cs](../src/Pages/LibraryPageModel.DownloadProgress.cs); [src/Helpers/LibraryDownloadProgress.cs](../src/Helpers/LibraryDownloadProgress.cs); [linux/DlssSwapper.Linux.Gui/LibraryPage.Downloads.cs](../linux/DlssSwapper.Linux.Gui/LibraryPage.Downloads.cs).

### F18 DLL archive validation and safe destination replacement

Windows and Linux.

Improves existing DLL import/export with case-insensitive ZIP discovery, imported debug metadata and verified archive creation. Preserves existing destination files when an operation is cancelled or fails.

**Commits:** [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db).

**Implementation references:** [src/Helpers/DllArchiveExport.cs](../src/Helpers/DllArchiveExport.cs); [src/Pages/LibraryPageModel.cs](../src/Pages/LibraryPageModel.cs); [linux/DlssSwapper.Linux.Cli/Core/DllImportWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/DllImportWorkflow.cs); [linux/DlssSwapper.Linux.Cli/Core/DllExportWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/DllExportWorkflow.cs).

### F19 Reset all LLE local application data

Windows and Linux.

Adds a full reset of saved library data, settings and application caches.

**Commits:** [1d9ab27](https://github.com/ordinarybob/dlss-swapper-lle/commit/1d9ab27dd1bafa7a680b9b75e4ee71f1f41f7666), [3e73147](https://github.com/ordinarybob/dlss-swapper-lle/commit/3e73147a7e4a546fa87ac1f4631d98a5990341f8), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/AppDataReset.cs](../src/AppDataReset.cs); [linux/DlssSwapper.Linux.Cli/Core/LocalDataResetService.cs](../linux/DlssSwapper.Linux.Cli/Core/LocalDataResetService.cs).

### F20 Native Linux command line interface

Linux x64.

Provides native command-line discovery, library management, game selection, dry-run planning, DLL updates and restores. Shares saved application state with the Linux GUI.

Supports exact DLL-version selection, Streamline inspection, updates, restores and recovery, plus filesystem reports for game-library locations.

**Commits:** [efbe1a4](https://github.com/ordinarybob/dlss-swapper-lle/commit/efbe1a4af4b9ec66b18e03807bb9378fd54163a9), [58d7803](https://github.com/ordinarybob/dlss-swapper-lle/commit/58d7803b233a773be90041dbc3c20a458f6d852f), [4907f4c](https://github.com/ordinarybob/dlss-swapper-lle/commit/4907f4c7f185b6312e2ba4ec0457708fb8b7a9b9), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [linux/DlssSwapper.Linux.Cli/Program.cs](../linux/DlssSwapper.Linux.Cli/Program.cs); [linux/DlssSwapper.Linux.Cli/Cli.cs](../linux/DlssSwapper.Linux.Cli/Cli.cs).

### F21 Native Linux desktop interface

Linux x64.

Provides a native Avalonia desktop interface for the library, scanning, artwork, updates/restores, settings and launch setup, with native desktop integration and shared saved state.

Matches the Windows Games commands, filtering and sorting, click-to-open game details, DLL selection, batch results and immediate settings. Toolbars and selection controls adapt to narrow windows; download progress remains visible above the Library.

**Commits:** [32f7ed6](https://github.com/ordinarybob/dlss-swapper-lle/commit/32f7ed60a3769fcf4704639c73cce78cba213526), [e75a4e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/e75a4e72da1502ff4421339d599d6a10e676a525), [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206), [923ea5e](https://github.com/ordinarybob/dlss-swapper-lle/commit/923ea5e801e9084004cae9f67150b37ca16a8ad1), [509ac0b](https://github.com/ordinarybob/dlss-swapper-lle/commit/509ac0ba8a55124448b1c583ca7cc6c7d62395e3).

**Implementation references:** [linux/DlssSwapper.Linux.Gui/App.axaml.cs](../linux/DlssSwapper.Linux.Gui/App.axaml.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml); [linux/DlssSwapper.Linux.Gui/DlssSwapper.Linux.Gui.csproj](../linux/DlssSwapper.Linux.Gui/DlssSwapper.Linux.Gui.csproj).
