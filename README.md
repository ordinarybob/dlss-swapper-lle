# DLSS Swapper LLE — Large Library Edition

DLSS Swapper LLE adds ultra fast library scanning and parsing, native Linux
support, and batch game import with automatic launch-executable detection to
DLSS Swapper. Built for large game collections, it also brings a redesigned
Windows interface, parallel DLL updates and Streamline version management.

**LLE V1** is based on upstream **DLSS Swapper v1.2.5.0**, with selected fixes,
DLL catalog updates and translations from later upstream releases.

[Releases](https://github.com/ordinarybob/dlss-swapper-lle/releases) ·
[Complete feature list](docs/FEATURE_CLUSTERS.md) ·
[Individual commits](docs/COMMITS.md) ·
[Screenshots](docs/SCREENSHOTS.md) ·
[Linux guide](linux/README.md) · [Build from source](docs/BUILDING.md)

## LLE features

- **Ultra fast library scanning.** First library scan takes ~15 seconds for a
  4,000-game Steam library, with cached reloads instantaneous. LLE reads launcher
  records and scans game folders in parallel. Adaptive Fast Scan reuses known DLL
  locations; Deep Scan searches complete game folders and learns new locations.
- **Native Linux support — desktop and command line.** Scan game libraries,
  import games in batches, configure launch executables, and update or restore
  DLLs from a native Linux desktop application. Terminal commands support
  discovery, scanning, updates and restoration using the same saved library.
  Supports native/Flatpak Steam, Legendary/Heroic and launchers in configured
  Wine prefixes. [Linux guide](linux/README.md).
- **Batch game import with automatic executable detection.** Select several
  game folders or a parent folder containing them. Launch setup scans all imported
  games together and preselects suggested executables. Review, change and save
  every selection in one window.
- **Adjustable scan, cover-loading and update limits.** Set how many games scan
  or update at once, how many covers load at once, and how many game entries are
  added to the screen together. HDD mode lowers scan and cover-loading limits
  for mechanical drives.
- **Redesigned interface.** Vertical navigation, grouped toolbars and dialogs
  that fit smaller windows. Resize cover cards to fill the window, sort games by
  name or DLSS version, and access update-all from a game's right-click menu.
- **One-click update-all and parallel batch updates.** Select the latest versions
  for all detected DLL types at once, or choose versions separately. Update several
  games simultaneously, skip files already current, and copy or save per-game
  results.
- **Streamline version switching.** Download a chosen NVIDIA Streamline SDK
  release and use it to replace existing component DLLs in one or several games.
  Compare current and replacement versions before upgrading or downgrading;
  restore original files or recover interrupted updates.
- **Automatic game covers and shared caching.** Find cover images for manually
  added games and reuse covers already downloaded by Steam or LLE. Windows
  portable copies can reuse a shared cover cache on the game-library drive.

![DLSS Swapper LLE on Windows: default list view on the left and grid view on the right](docs/images/windows-main.png)

[Workflow screenshots](docs/SCREENSHOTS.md)

## Downloads

- **Windows x64 portable:** [Download ZIP](https://github.com/ordinarybob/dlss-swapper-lle/releases/download/v1.0.0/DLSS.Swapper-LLE-1.0.0-windows-x64-portable.zip)
- **Linux x64 desktop and CLI:** [Download tar.gz](https://github.com/ordinarybob/dlss-swapper-lle/releases/download/v1.0.0/DLSS.Swapper-LLE-1.0.0-linux-x64.tar.gz)

Both packages include the .NET runtime. Extract the Windows ZIP and run
`DLSS Swapper LLE.exe`; extract the Linux archive and run
`./dlss-swapper-linux-gui` or `./dlss-swapper-linux --help`.

## Using LLE's workflows

1. Start LLE. Enable the **HDD** option if your games are on a mechanical hard
   drive; leave it off for SSD or NVMe storage. LLE discovers installed games
   from enabled launchers and scans their folders for supported DLLs.
2. Use **Add Games → Add multiple game folders** or **Add a multi-game directory**
   to import games in a batch. Choose **Yes** for launch setup: LLE automatically
   scans the imported games and preselects suggested executables. Review or change
   any selection, then **Save and close** saves the whole batch.
3. For parallel updates, choose **Batch**, select the games and click
   **Apply updates**. Choose versions
   individually or click **Update detected DLLs to latest**, then **Apply**.
4. **Library → Streamline** lists historical SDK releases to download.
   The game's **Streamline components** window lets you select a
   release, compare its components and apply or restore them.

Close affected games before replacing files and keep their `.dlsss` backups.
Game updates, file verification and anti-cheat systems may reject modified DLLs.

## Platforms and game libraries

| Platform | Requirements | Discovery |
| --- | --- | --- |
| Windows x64 | Windows 10 build 19041 or newer | Steam, GOG, Epic, Ubisoft Connect, Xbox App, Battle.net and manual imports |
| Linux x64 | glibc 2.38 or newer and a desktop session for the GUI | Native/Flatpak Steam, Legendary/Heroic, supported launchers in configured Wine prefixes and manual imports |

See the [Linux guide](linux/README.md) for
dependencies, launcher setup and Windows-only controls.

## Release policy

LLE is a standalone release with no application updater or ongoing support commitment.
See [security and safe use](SECURITY.md).

## Credits and license

An unofficial fork of [beeradmoore/DLSS Swapper](https://github.com/beeradmoore/dlss-swapper).
The batch-selection foundation comes from RafaelHGOliveira's
[PR #913](https://github.com/beeradmoore/dlss-swapper/pull/913).

Distributed under [GPL-3.0](LICENSE). See [attribution](ATTRIBUTION.md) for
upstream and third-party credits, and the [changelog](CHANGELOG-LLE.md) for LLE V1.
