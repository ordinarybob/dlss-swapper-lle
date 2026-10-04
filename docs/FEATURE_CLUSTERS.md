# DLSS Swapper LLE features

LLE features grouped with their source files and commits, relative to upstream `v1.2.5.0`. See [upstream contributions](COMMITS.md#upstream-imports-and-adaptations) for imported changes.

## F02 Ultra fast library scanning and parsing

Windows and Linux.

Reads launcher records and scans game folders in parallel. First library scan takes ~15 seconds for a 4,000-game Steam library, with cached reloads instantaneous.

Adaptive Fast Scan checks known locations for supported game DLLs. Deep Scan searches complete game folders and remembers newly found DLL locations for later Fast Scans. Scan results and folder listings are reused so overlapping search patterns do not repeat the same work.

View and edit the learned folder patterns, or add custom paths for Fast Scan to search.

**Commits:** [19352e4](https://github.com/ordinarybob/dlss-swapper-lle/commit/19352e42a7d3f826738d9ac207b65dab7a2f2579), [95ede66](https://github.com/ordinarybob/dlss-swapper-lle/commit/95ede6689505fc1630ba893e8343d38ad9db6ecb), [f3fdb30](https://github.com/ordinarybob/dlss-swapper-lle/commit/f3fdb30f89d8d394af70fccf68f11934472ee945), [dc2237a](https://github.com/ordinarybob/dlss-swapper-lle/commit/dc2237affc2fb660beea90278cb9f3149ad4d824), [4021e63](https://github.com/ordinarybob/dlss-swapper-lle/commit/4021e638b462c0ed8b218353791c3b586d31066e), [182f6b9](https://github.com/ordinarybob/dlss-swapper-lle/commit/182f6b90ec6359f67236c9aa7e69a48a601d2341), [48b903a](https://github.com/ordinarybob/dlss-swapper-lle/commit/48b903a768469477400ed9eeb6ff7da0037deecc), [69cd628](https://github.com/ordinarybob/dlss-swapper-lle/commit/69cd628389469c7075ac6923354a9a7853751df4), [f435cd7](https://github.com/ordinarybob/dlss-swapper-lle/commit/f435cd7f7717bff0f20ff5f88903880855656052), [787e8b7](https://github.com/ordinarybob/dlss-swapper-lle/commit/787e8b75d350acf8b239a3dee1f8e73032b6444a), [1b2753a](https://github.com/ordinarybob/dlss-swapper-lle/commit/1b2753a8ce5c2d809a7275648d5760dbf7c6152e), [bb2d3bc](https://github.com/ordinarybob/dlss-swapper-lle/commit/bb2d3bcdead657f2069ebcde02bacda73228f47d), [ce1d055](https://github.com/ordinarybob/dlss-swapper-lle/commit/ce1d055cb6360aa46a332db6feb7b18919da1ced), [cb1389a](https://github.com/ordinarybob/dlss-swapper-lle/commit/cb1389a7b8dcf465e1b2931feb3ad368a17f716c), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Data/Steam/SteamLibrary.cs](../src/Data/Steam/SteamLibrary.cs); [src/Data/GameScanQueue.cs](../src/Data/GameScanQueue.cs); [src/Data/GameAssetPathIndex.cs](../src/Data/GameAssetPathIndex.cs); [src/Data/GameAssetCandidatePathIndex.cs](../src/Data/GameAssetCandidatePathIndex.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryScanService.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryScanService.cs).

## F21 Native Linux desktop support

Linux x64.

A native Linux desktop application for scanning game libraries, importing several games at once, setting their launch executables, downloading DLL versions and updating or restoring game files. Finds games in native/Flatpak Steam, Legendary/Heroic and supported launchers in configured Wine prefixes.

Includes list and cover-grid views, sorting and filtering, game details, parallel batch updates and per-game result reports. Streamline controls download a chosen SDK release, compare component versions and apply or restore them. Settings control simultaneous scans, cover loads and game updates; toolbars rearrange to fit narrow windows.

**Commits:** [32f7ed6](https://github.com/ordinarybob/dlss-swapper-lle/commit/32f7ed60a3769fcf4704639c73cce78cba213526), [e75a4e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/e75a4e72da1502ff4421339d599d6a10e676a525), [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206), [923ea5e](https://github.com/ordinarybob/dlss-swapper-lle/commit/923ea5e801e9084004cae9f67150b37ca16a8ad1), [509ac0b](https://github.com/ordinarybob/dlss-swapper-lle/commit/509ac0ba8a55124448b1c583ca7cc6c7d62395e3).

**Implementation references:** [linux/DlssSwapper.Linux.Gui/App.axaml.cs](../linux/DlssSwapper.Linux.Gui/App.axaml.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml); [linux/DlssSwapper.Linux.Gui/DlssSwapper.Linux.Gui.csproj](../linux/DlssSwapper.Linux.Gui/DlssSwapper.Linux.Gui.csproj).

## F20 Native Linux command-line support

Linux x64.

Discover and scan games, add or remove saved library entries, select exact DLL versions, update game files and restore originals from the terminal. Commands can target individual games, the game folders inside a parent directory, or the saved library. Dry-run previews show planned DLL changes before writing files.

Shares saved games, settings and history with the Linux desktop. Streamline commands inspect installed components, replace them from an extracted SDK package, restore originals or recover interrupted updates. A filesystem report identifies the storage used by game libraries. See the [command reference](../linux/DlssSwapper.Linux.Cli/README.md).

**Commits:** [efbe1a4](https://github.com/ordinarybob/dlss-swapper-lle/commit/efbe1a4af4b9ec66b18e03807bb9378fd54163a9), [58d7803](https://github.com/ordinarybob/dlss-swapper-lle/commit/58d7803b233a773be90041dbc3c20a458f6d852f), [4907f4c](https://github.com/ordinarybob/dlss-swapper-lle/commit/4907f4c7f185b6312e2ba4ec0457708fb8b7a9b9), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [linux/DlssSwapper.Linux.Cli/Program.cs](../linux/DlssSwapper.Linux.Cli/Program.cs); [linux/DlssSwapper.Linux.Cli/Cli.cs](../linux/DlssSwapper.Linux.Cli/Cli.cs).

## F05 Manual multi-folder and parent-folder batch game import

Windows and Linux.

Imports multiple games in one operation, scans their folders for supported DLLs and looks up their cover images:

- **Multi-folder import:** adds several individually selected game installation folders in one operation.
- **Parent-folder batch import:** adds each immediate child folder of a selected games directory as a separate manually added game.

Both skip duplicate entries. Choosing launch setup starts automatic executable detection for the entire imported batch, followed by one window for reviewing and saving the selections. The import notice and launch-setup prompt can remember their choices.

The Windows batch summary records added, already-present and failed imports, with details for each failed folder.

**Commits:** [32e4e1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/32e4e1f71403552d44a926c23ae6eb8e4247a2bf), [3d58a48](https://github.com/ordinarybob/dlss-swapper-lle/commit/3d58a484292e7ba7457ee1ff99ce94fc629da960), [281b4bb](https://github.com/ordinarybob/dlss-swapper-lle/commit/281b4bbeeb526fdabadf99475b14ab54cd1b88fe), [1c466d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/1c466d9eabfd695d6e6d208af65181801126b1ca), [d79ed42](https://github.com/ordinarybob/dlss-swapper-lle/commit/d79ed4214bb6db7ff5a37a818d205aaedac29b4a), [a4ea439](https://github.com/ordinarybob/dlss-swapper-lle/commit/a4ea43984c9ff4cd59fbdc8db456d8ca94a3504e), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Pages/GameGridPageModel.cs](../src/Pages/GameGridPageModel.cs); [linux/DlssSwapper.Linux.Cli/Core/ManualGameImportWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/ManualGameImportWorkflow.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryState.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryState.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs).

## F07 Automatic executable detection and batch launch setup

Windows and Linux.

Automatically finds and preselects suggested launch executables for the entire imported batch. All games appear together in one window, ready to save as a group or adjust individually.

Saved launch settings include executables, arguments and working folders. Linux supports native executables and Wine, with configurable Wine executable and prefix.

- **Bulk executable scan:** scans the entire imported set before selection begins, with parallel scanning, progress and cancellation. Searches nested game directories and preserves saved executable choices.
- **Ranked suggestions:** matches game titles, abbreviations, sequel numbers and executable metadata; filters helper processes and ranks the main application ahead of companion utilities.
- **Shared selection window:** one bounded, scrollable list with a game name, executable selector and Browse button on each row. Selectors show filenames when closed and full paths when open. Per-game launch arguments and working folders remain editable.
- **Batch save controls:** Apply saves selections without closing; Save and close saves and exits; Skip and close exits without saving pending changes. Unselected rows are highlighted but do not block saving the other games.

Saved launch settings can also be reopened for an individual manually added game.

**Commits:** [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [4a34d03](https://github.com/ordinarybob/dlss-swapper-lle/commit/4a34d0343e30edde54d4c63663e6a29de1b273e6), [c72346d](https://github.com/ordinarybob/dlss-swapper-lle/commit/c72346d16b48572c9ace234515064c6e3331cdda), [283184b](https://github.com/ordinarybob/dlss-swapper-lle/commit/283184bf48bbde2a4d8af209ec61493b431f6504), [2df6c1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/2df6c1f85827307452157da79d0e0d58bb564391), [e129dd9](https://github.com/ordinarybob/dlss-swapper-lle/commit/e129dd9eb46064ae3c27481ee767f6fceadae729), [d8bdc24](https://github.com/ordinarybob/dlss-swapper-lle/commit/d8bdc249daa24cfba42b5b2324f2a304b8fce499), [63805d4](https://github.com/ordinarybob/dlss-swapper-lle/commit/63805d4b7207331b59fbc836e25e9d772b205797).

**Implementation references:** [src/UserControls/ManualLaunchSetup.cs](../src/UserControls/ManualLaunchSetup.cs); [src/UserControls/ManualLaunchSetup.Bulk.cs](../src/UserControls/ManualLaunchSetup.Bulk.cs); [src/UserControls/ManualLaunchSetupDialog.cs](../src/UserControls/ManualLaunchSetupDialog.cs); [src/Data/GameManager.cs](../src/Data/GameManager.cs); [shared/DlssSwapper.Shared/ManualLaunch/ManualLaunchManifest.cs](../shared/DlssSwapper.Shared/ManualLaunch/ManualLaunchManifest.cs); [linux/DlssSwapper.Linux.Gui/ManualLaunchSetupWindow.cs](../linux/DlssSwapper.Linux.Gui/ManualLaunchSetupWindow.cs); [linux/DlssSwapper.Linux.Cli/Core/ManualGameLaunch.cs](../linux/DlssSwapper.Linux.Cli/Core/ManualGameLaunch.cs).

## F01 Game discovery with missing launcher records and Linux launcher support

Windows and Linux.

Linux discovery supports native and Flatpak Steam, Epic through Legendary/Heroic, GOG through Heroic, and Epic, GOG, Ubisoft Connect, EA App and Battle.net installations in configured Wine prefixes.

Finds installed games when Steam library indexes are missing or outdated. Keeps cached games visible when their launcher data is temporarily unavailable and uses available installation records when other metadata is missing.

**Commits:** [f42128f](https://github.com/ordinarybob/dlss-swapper-lle/commit/f42128f9a35a4b90cb65989a97dc89f17381f4d4), [16721dc](https://github.com/ordinarybob/dlss-swapper-lle/commit/16721dc7a14a2bdc507fe9fb4e60530fc451dc3a), [140543d](https://github.com/ordinarybob/dlss-swapper-lle/commit/140543d05cda54516dc7722466eb2858ed7f1ac3), [b5e4402](https://github.com/ordinarybob/dlss-swapper-lle/commit/b5e4402852055981a5ea8699061d0b13206dd071), [48b903a](https://github.com/ordinarybob/dlss-swapper-lle/commit/48b903a768469477400ed9eeb6ff7da0037deecc), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/Data/Steam/SteamLibrary.cs](../src/Data/Steam/SteamLibrary.cs); [linux/DlssSwapper.Linux.Cli/Platform/SteamDiscovery.cs](../linux/DlssSwapper.Linux.Cli/Platform/SteamDiscovery.cs); [linux/DlssSwapper.Linux.Cli/Platform/ProviderDiscovery.cs](../linux/DlssSwapper.Linux.Cli/Platform/ProviderDiscovery.cs).

## F03 Instant cached reloads and responsive background work

Windows and Linux.

The scanned library is cached for instantaneous reloads. Games are usable immediately while fresh scan results, artwork and metadata are processed in the background, without waiting for the slowest game or library.

Cover images load separately from game scans. Game entries are added to the screen in short groups, allowing keyboard, mouse and display updates between groups as the library fills.

On Windows, repeated database updates are combined into batched transactions. DLL hashes are reused when file size and modification time match; otherwise, full-file hashing is deferred until needed instead of reading every DLL during initial loading.

**Commits:** [0099e3f](https://github.com/ordinarybob/dlss-swapper-lle/commit/0099e3f88feedbbc4bb9b1e93d26b945a7a4ab78), [3e19518](https://github.com/ordinarybob/dlss-swapper-lle/commit/3e195186fbc83cd62c1272e4003c5faef600acf3), [bb657c7](https://github.com/ordinarybob/dlss-swapper-lle/commit/bb657c73b1136ffdeea00947e406e5abef6b9e13), [182f6b9](https://github.com/ordinarybob/dlss-swapper-lle/commit/182f6b90ec6359f67236c9aa7e69a48a601d2341), [afc9365](https://github.com/ordinarybob/dlss-swapper-lle/commit/afc9365245cee0b75b8efcb915c32bd3cd4f9868), [49d8ef3](https://github.com/ordinarybob/dlss-swapper-lle/commit/49d8ef3961d14170febfdfdc3ab80fc85db2d087), [c8b2058](https://github.com/ordinarybob/dlss-swapper-lle/commit/c8b20581967c19fe6b93febf5b7b368484e6bc58), [95703a6](https://github.com/ordinarybob/dlss-swapper-lle/commit/95703a68314e19b5b00a1d28842cf79f41db0578), [d2b825c](https://github.com/ordinarybob/dlss-swapper-lle/commit/d2b825c410b02b76993306ea6f61ba9bc5b52c64), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [8abfd44](https://github.com/ordinarybob/dlss-swapper-lle/commit/8abfd44ed8b58cfa14745ad4bb08ac5ca7e28f68), [19352e4](https://github.com/ordinarybob/dlss-swapper-lle/commit/19352e42a7d3f826738d9ac207b65dab7a2f2579), [b637dcb](https://github.com/ordinarybob/dlss-swapper-lle/commit/b637dcbe63cfd66c8e6b61f05488eb10519c22a6).

**Implementation references:** [src/Pages/GameGridPageModel.InitialLoad.cs](../src/Pages/GameGridPageModel.InitialLoad.cs); [src/Data/GameManager.cs](../src/Data/GameManager.cs); [src/Data/GameDatabaseWriteBatch.cs](../src/Data/GameDatabaseWriteBatch.cs); [src/Data/GameAsset.cs](../src/Data/GameAsset.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryStartup.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryStartup.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.Publication.cs](../linux/DlssSwapper.Linux.Gui/MainWindow.Publication.cs).

## F04 Adjustable scan, cover-loading and update limits

Windows and Linux.

Set how much work runs at once to suit the processor and storage. First-start HDD mode reduces simultaneous game scans from 15 to 2 and cover loads from 38 to 1 for mechanical drives. Leave HDD mode off for the standard SSD/NVMe settings, or enter individual limits in Settings.

| Control | What it changes | Range |
| --- | --- | --- |
| Concurrent game scans | Number of game folders scanned at once | 1–26 |
| Concurrent artwork loading | Number of cover images loaded at once | 1–64 |
| Concurrent game updates | Number of selected games updated at once | 1–26 |
| Games added per UI batch | Maximum game entries added to the screen before allowing other interface work | 10–1,000 |
| Database records per transaction (Windows) | Maximum saved game records grouped into one database write operation | 10–1,000 |

Windows applies scan and cover-loading limits to running queues. Linux reads those limits when starting a scan or cover-loading pass. Game-update limits apply when starting a batch update.

**Commits:** [19352e4](https://github.com/ordinarybob/dlss-swapper-lle/commit/19352e42a7d3f826738d9ac207b65dab7a2f2579), [7a26524](https://github.com/ordinarybob/dlss-swapper-lle/commit/7a26524163d1b25dd9545789ae957c9d0f9cad90), [159e230](https://github.com/ordinarybob/dlss-swapper-lle/commit/159e23075cda300a3cfcd7235b274e3d686d2c72), [40f3bf1](https://github.com/ordinarybob/dlss-swapper-lle/commit/40f3bf1b23867c2c4113df6fff50922cd5a4a794), [5c6780a](https://github.com/ordinarybob/dlss-swapper-lle/commit/5c6780a6aa9e24955cb85c3d770c72e62f249f29), [ba90b23](https://github.com/ordinarybob/dlss-swapper-lle/commit/ba90b23a98f975d444ca37c8eb48241581909297), [001fd5b](https://github.com/ordinarybob/dlss-swapper-lle/commit/001fd5b573cb0748c14cc060d69edc0174de3312), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Settings.cs](../src/Settings.cs); [src/UserControls/PerformanceNumberEditor.xaml](../src/UserControls/PerformanceNumberEditor.xaml); [linux/DlssSwapper.Linux.Cli/Core/LibraryState.Performance.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryState.Performance.cs).

## F06 Bulk removal with persistent launcher exclusions

Windows and Linux.

Remove several games from the library in one operation. Manually added entries are removed; launcher-discovered games are excluded so they stay hidden after a rescan. Restore the exclusions to show those launcher games again.

**Commits:** [32e4e1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/32e4e1f71403552d44a926c23ae6eb8e4247a2bf), [4238177](https://github.com/ordinarybob/dlss-swapper-lle/commit/4238177934f4a53f302b382ecdec708e45efd0f2), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Pages/GameGridPageModel.cs](../src/Pages/GameGridPageModel.cs); [src/UserControls/GameControlModel.Metadata.cs](../src/UserControls/GameControlModel.Metadata.cs); [linux/DlssSwapper.Linux.Cli/Core/LibraryRemovalWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/LibraryRemovalWorkflow.cs).

## F08 Automatic artwork for manual games and shared cover caching

Windows and Linux.

Finds covers for manually added games by matching their names to Steam titles, with a configurable MediaWiki lookup if Steam has no match. Neither lookup requires an API key. Reuses Steam's cached artwork and limits simultaneous image requests.

Cached covers are reused without another download. On Windows, portable copies of LLE can share a cover cache on the game-library drive. Available cached covers appear as soon as their game cards are displayed.

**Commits:** [7f4f0d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f4f0d94d8a8d4f05250c6e9a1ecd5f23d73e50a), [f24dc18](https://github.com/ordinarybob/dlss-swapper-lle/commit/f24dc18163afcc6d72d88de87f4bc89a991147b0), [b9ad50f](https://github.com/ordinarybob/dlss-swapper-lle/commit/b9ad50f68b6bec3a319d4a7cb8c3cb0cbf676784), [0f37990](https://github.com/ordinarybob/dlss-swapper-lle/commit/0f37990ca5324e4c3ff94044aafcda3d02fc6120), [44626e3](https://github.com/ordinarybob/dlss-swapper-lle/commit/44626e349b0627890451f631f8c210a50153debc), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/Data/Steam/SteamArtworkLookup.cs](../src/Data/Steam/SteamArtworkLookup.cs); [src/Data/ManuallyAdded/WikipediaArtworkLookup.cs](../src/Data/ManuallyAdded/WikipediaArtworkLookup.cs); [linux/DlssSwapper.Linux.Cli/Core/ArtworkService.cs](../linux/DlssSwapper.Linux.Cli/Core/ArtworkService.cs).

## F09 Sort the game library

Windows and Linux.

Sorts the library by game name or detected DLSS version and preserves the selected order alongside existing filters.

**Commits:** [d22493d](https://github.com/ordinarybob/dlss-swapper-lle/commit/d22493d2787868b57bff2e072d283a19108539b2), [4238177](https://github.com/ordinarybob/dlss-swapper-lle/commit/4238177934f4a53f302b382ecdec708e45efd0f2), [5286737](https://github.com/ordinarybob/dlss-swapper-lle/commit/528673714e001525acf1a68993daee73a503fd1a), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/Data/GameSortMode.cs](../src/Data/GameSortMode.cs); [src/Pages/GameGridPageModel.cs](../src/Pages/GameGridPageModel.cs); [linux/DlssSwapper.Linux.Cli/Core/GameViewPolicy.cs](../linux/DlssSwapper.Linux.Cli/Core/GameViewPolicy.cs).

## F10 Adjustable cover grid that fills the window

Windows and Linux.

Cover cards resize to fill the window width without stretching their images. Change the preferred card size to show more or fewer games per row. The Windows Steam grid includes a Card size selector beside the library heading.

**Commits:** [07a9b80](https://github.com/ordinarybob/dlss-swapper-lle/commit/07a9b8053c2170939781459a770d18b9bfb356aa), [f715332](https://github.com/ordinarybob/dlss-swapper-lle/commit/f715332fbe138de6dda108e205b7ac9937e09518), [cdb40cf](https://github.com/ordinarybob/dlss-swapper-lle/commit/cdb40cfb113cc72cd66f43304871ada5424f5445), [848df8d](https://github.com/ordinarybob/dlss-swapper-lle/commit/848df8de0b8ae14d05a2b3c9c8b27d07e1f9ec97), [4633a30](https://github.com/ordinarybob/dlss-swapper-lle/commit/4633a303d4166353e3789220ec34e2d36d66df23), [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/Pages/ResponsiveGameGridLayout.cs](../src/Pages/ResponsiveGameGridLayout.cs); [shared/DlssSwapper.Shared/ResponsiveGridLayout.cs](../shared/DlssSwapper.Shared/ResponsiveGridLayout.cs).

## F11 Windows interface redesign and Linux desktop layout

Windows and Linux.

The Windows redesign and native Linux desktop use the following layout:

- **Navigation:** compact vertical icon controls for Games, Library and Settings, leaving more space for page content.
- **Games:** redesigned header with game count, search and grouped icon-and-text commands; batch actions sit in their own row below the toolbar. Controls rearrange as the window narrows.
- **Library:** redesigned action bar, horizontally scrollable component tabs and compact version cards with aligned download, export and delete controls.
- **Game details and history:** compact, scrollable dialogs that fit the available window; game details adapt the cover panel and action-button layout to the window width.
- **Window behaviour:** display-aware default sizing and saved-window restoration that keeps the window on screen.

**Commits:** [9b59d85](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b59d8566cf9ea18f94a87d5bd9c32bd0757781c), [7f09c1d](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f09c1ddbf823cead6c3dd542d1fbd7daac5d2ae), [fb0caa3](https://github.com/ordinarybob/dlss-swapper-lle/commit/fb0caa31f5816b13058b53daf4cb5b0019966aaa), [6bd2e19](https://github.com/ordinarybob/dlss-swapper-lle/commit/6bd2e19a2389c94a57f04f15c90606b4ebe9d0b4), [10edb39](https://github.com/ordinarybob/dlss-swapper-lle/commit/10edb39c5eeabf4523482911718c9a7402c5bcaf), [269c65d](https://github.com/ordinarybob/dlss-swapper-lle/commit/269c65df43ebd8886659b30ba2c6bd8a8ab99344), [51738bf](https://github.com/ordinarybob/dlss-swapper-lle/commit/51738bff26e7a61bf50bfc5da57dd3ae91a012f8), [cdb40cf](https://github.com/ordinarybob/dlss-swapper-lle/commit/cdb40cfb113cc72cd66f43304871ada5424f5445), [7f4f0d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f4f0d94d8a8d4f05250c6e9a1ecd5f23d73e50a), [0f37990](https://github.com/ordinarybob/dlss-swapper-lle/commit/0f37990ca5324e4c3ff94044aafcda3d02fc6120), [f435cd7](https://github.com/ordinarybob/dlss-swapper-lle/commit/f435cd7f7717bff0f20ff5f88903880855656052), [e75a4e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/e75a4e72da1502ff4421339d599d6a10e676a525), [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c), [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [src/MainWindow.xaml](../src/MainWindow.xaml); [src/MainWindow.xaml.cs](../src/MainWindow.xaml.cs); [src/Pages/GameGridPage.xaml](../src/Pages/GameGridPage.xaml); [src/Pages/LibraryPage.xaml](../src/Pages/LibraryPage.xaml); [src/UserControls/GameControl.xaml](../src/UserControls/GameControl.xaml); [src/UserControls/GameControlModel.cs](../src/UserControls/GameControlModel.cs); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml).

## F12 Right-click game actions

Windows and Linux.

Open game actions from a labelled right-click menu without opening the details dialog, including the command to update all detected DLL types.

**Commits:** [ed42f95](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed42f95ed555ba59d12a9c5a43f9a9e84ffe596f), [3b01d2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/3b01d2eda24ca1c2fabb0edb1e1a54856c882809), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa).

**Implementation references:** [src/UserControls/GameControlModel.Metadata.cs](../src/UserControls/GameControlModel.Metadata.cs); [src/Pages/GameGridPage.xaml](../src/Pages/GameGridPage.xaml); [linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs](../linux/DlssSwapper.Linux.Gui/MainWindow.axaml.cs).

## F13 DLL replacement and backup checks

Windows and Linux.

Writes replacement files to a temporary location before replacing game DLLs, checks file identity and existing backups, and prevents repeated Apply clicks from starting duplicate operations. Linux reads version information directly from Windows DLLs.

**Commits:** [fb0caa3](https://github.com/ordinarybob/dlss-swapper-lle/commit/fb0caa31f5816b13058b53daf4cb5b0019966aaa), [6bd2e19](https://github.com/ordinarybob/dlss-swapper-lle/commit/6bd2e19a2389c94a57f04f15c90606b4ebe9d0b4), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [8069d8b](https://github.com/ordinarybob/dlss-swapper-lle/commit/8069d8b9d25db0016769659ba0934f8d84840bdc), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [src/UserControls/DLLPickerControlModel.Apply.cs](../src/UserControls/DLLPickerControlModel.Apply.cs); [src/UserControls/GameControl.xaml](../src/UserControls/GameControl.xaml); [src/Helpers/StagedFile.cs](../src/Helpers/StagedFile.cs); [linux/DlssSwapper.Linux.Cli/Core/DllRestoreWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/DllRestoreWorkflow.cs).

## F14 One-click update-all and parallel batch DLL updates

LLE extension of upstream PR #913; Windows and Linux.

Update all detected DLL types for one game, or update several selected games simultaneously. Select the latest versions with one click or choose a version for each DLL type. The selected packages are downloaded before use, and files already at the requested version are skipped.

The game-update limit in Settings controls how many games update at once. A downloaded DLL is verified once and reused across the batch instead of repeating verification for every game.

Results list changed files, already-current files, skipped items and errors for each game. Copy the report to the clipboard or save it as a text file.

**Commits:** [aa7ec4b](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa7ec4b0a2427fe79bf99a7d7642cd201844ffff), [2a6b54b](https://github.com/ordinarybob/dlss-swapper-lle/commit/2a6b54bda9aa3bf7f63cd92f0673ed5eab6edfbe), [b637dcb](https://github.com/ordinarybob/dlss-swapper-lle/commit/b637dcbe63cfd66c8e6b61f05488eb10519c22a6), [aa5f1bb](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa5f1bb4a77cc76dd5734711359e68b6cdebb912), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f), [8069d8b](https://github.com/ordinarybob/dlss-swapper-lle/commit/8069d8b9d25db0016769659ba0934f8d84840bdc).

**Implementation references:** [src/Data/DllUpdateWorkflow.cs](../src/Data/DllUpdateWorkflow.cs); [src/Helpers/VerifiedDllSource.cs](../src/Helpers/VerifiedDllSource.cs); [src/UserControls/BatchSwapSummaryControl.xaml.cs](../src/UserControls/BatchSwapSummaryControl.xaml.cs); [src/UserControls/EasyContentDialog.cs](../src/UserControls/EasyContentDialog.cs); [linux/DlssSwapper.Linux.Cli/Core/BatchUpdateWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/BatchUpdateWorkflow.cs); [linux/DlssSwapper.Linux.Gui/OperationReportWindow.cs](../linux/DlssSwapper.Linux.Gui/OperationReportWindow.cs).

## F15 NVIDIA batch preset checks and per-game results

Extends PR #913; Windows only.

Batch preset changes check each game for the matching DLSS component and an NVIDIA driver profile before changing the setting. Games without a matching profile are skipped. Results identify changed settings, settings already selected, skipped games and failures.

**Commits:** [aa7ec4b](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa7ec4b0a2427fe79bf99a7d7642cd201844ffff), [4beeead](https://github.com/ordinarybob/dlss-swapper-lle/commit/4beeead33b164cde0376266775b23fdc0835594f), [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7).

**Implementation references:** [src/Data/BatchPresetUpdateWorkflow.cs](../src/Data/BatchPresetUpdateWorkflow.cs); [src/UserControls/BatchPresetRowModel.cs](../src/UserControls/BatchPresetRowModel.cs).

## F16 Streamline version switching, rollback and interrupted-update recovery

Windows and Linux.

Choose and download an NVIDIA Streamline SDK release, then use its DLLs to replace existing Streamline components in one or several games. The comparison shows current, replacement and original versions, including upgrades, downgrades and different files with the same version number.

Select SDK versions from the Library, game component window or batch-update window. The first original-file backup is retained across later version changes, so Restore returns to the game's original components.

Components are updated as a group. Failed updates trigger rollback; interruptions and incomplete rollbacks leave recovery files for the Recover action. Updates to different games can run simultaneously, while writes to the same game folder are kept separate.

**Commits:** [5726d4d](https://github.com/ordinarybob/dlss-swapper-lle/commit/5726d4da4cc63a5fad0da2cc3f295bd955a3a7d0), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [5e15ecf](https://github.com/ordinarybob/dlss-swapper-lle/commit/5e15ecfe2c8d738ebecb0c6d0e86fcc2c8f984d1), [59031ac](https://github.com/ordinarybob/dlss-swapper-lle/commit/59031acf796f6e73e18454c2b4023e07591e1722), [708e7da](https://github.com/ordinarybob/dlss-swapper-lle/commit/708e7da5cc9165be43fd659f8c9f734c7bde3eb2), [a8a794e](https://github.com/ordinarybob/dlss-swapper-lle/commit/a8a794ee2ab9855c679a62a8968ab5bdf9d82853), [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [shared/DlssSwapper.Shared/Streamline/StreamlineSdkAcquisition.cs](../shared/DlssSwapper.Shared/Streamline/StreamlineSdkAcquisition.cs); [shared/DlssSwapper.Shared/Streamline/StreamlineComponentSet.cs](../shared/DlssSwapper.Shared/Streamline/StreamlineComponentSet.cs); [shared/DlssSwapper.Shared/Streamline/StreamlineDecisionPreview.cs](../shared/DlssSwapper.Shared/Streamline/StreamlineDecisionPreview.cs); [src/UserControls/StreamlineComponentsControl.xaml](../src/UserControls/StreamlineComponentsControl.xaml); [linux/DlssSwapper.Linux.Gui/StreamlineGameWindow.cs](../linux/DlssSwapper.Linux.Gui/StreamlineGameWindow.cs).

## F17 Combined Library and Streamline download progress

Windows and Linux.

The Windows Library header combines simultaneous downloads into one progress bar, including Download Latest, with percentage and received/total size. A Preparing files status follows the transfer while downloaded files are processed. Streamline downloads show progress on both platforms.

**Commits:** [ffa7f0c](https://github.com/ordinarybob/dlss-swapper-lle/commit/ffa7f0c67097bf81590e7d7b27da2d9f72c77000), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [04d3e64](https://github.com/ordinarybob/dlss-swapper-lle/commit/04d3e64264a98766d5b19ba2c6b287a520906b43), [3cf87f2](https://github.com/ordinarybob/dlss-swapper-lle/commit/3cf87f2e3afe3fa909f6fe91a5d01898bef21389), [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206).

**Implementation references:** [src/Pages/LibraryPageModel.DownloadProgress.cs](../src/Pages/LibraryPageModel.DownloadProgress.cs); [src/Helpers/LibraryDownloadProgress.cs](../src/Helpers/LibraryDownloadProgress.cs); [linux/DlssSwapper.Linux.Gui/LibraryPage.Downloads.cs](../linux/DlssSwapper.Linux.Gui/LibraryPage.Downloads.cs).

## F18 DLL archive validation and safe destination replacement

Windows and Linux.

Recognizes DLLs inside ZIP archives regardless of filename case and records whether imported DLLs are debug builds. Exported archives are completed and checked before replacing the destination file, leaving the existing file intact if export is cancelled or fails.

**Commits:** [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47), [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa), [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db).

**Implementation references:** [src/Helpers/DllArchiveExport.cs](../src/Helpers/DllArchiveExport.cs); [src/Pages/LibraryPageModel.cs](../src/Pages/LibraryPageModel.cs); [linux/DlssSwapper.Linux.Cli/Core/DllImportWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/DllImportWorkflow.cs); [linux/DlssSwapper.Linux.Cli/Core/DllExportWorkflow.cs](../linux/DlssSwapper.Linux.Cli/Core/DllExportWorkflow.cs).

## F19 Reset all LLE local application data

Windows and Linux.

Adds a full reset of saved library data, settings and application caches.

**Commits:** [1d9ab27](https://github.com/ordinarybob/dlss-swapper-lle/commit/1d9ab27dd1bafa7a680b9b75e4ee71f1f41f7666), [3e73147](https://github.com/ordinarybob/dlss-swapper-lle/commit/3e73147a7e4a546fa87ac1f4631d98a5990341f8), [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7), [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47).

**Implementation references:** [src/AppDataReset.cs](../src/AppDataReset.cs); [linux/DlssSwapper.Linux.Cli/Core/LocalDataResetService.cs](../linux/DlssSwapper.Linux.Cli/Core/LocalDataResetService.cs).
