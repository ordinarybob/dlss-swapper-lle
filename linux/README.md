# DLSS Swapper LLE for Linux

Scan game libraries, import games in batches, choose their launch executables,
and update or restore DLLs from a native Linux desktop application. Terminal
commands also provide game discovery, scanning, updates and restoration.
The desktop and command-line tool share saved games, launcher settings, scan
patterns, artwork and history.

## Install and start

The [Linux x64 archive](https://github.com/ordinarybob/dlss-swapper-lle/releases)
includes the desktop application, CLI and .NET runtime.

Requirements: x86-64, glibc 2.38 or newer, and a graphical desktop session for the GUI.
On Debian/Ubuntu, install the desktop libraries if they are not already present:

```sh
sudo apt install libice6 libsm6 libx11-6 libxrandr2 libxfixes3 libxcursor1 libxi6 libgl1 libfontconfig1
```

Minimal desktop sessions may also need `xdg-desktop-portal` and
`xdg-desktop-portal-gtk` for native file pickers.

Extract the archive into an empty folder, retaining executable permissions:

```sh
mkdir -p /path/to/lle
tar -xzf DLSS.Swapper-LLE-1.0.0-linux-x64.tar.gz -C /path/to/lle
cd /path/to/lle
./dlss-swapper-linux-gui
./dlss-swapper-linux --help
```

Run LLE as your normal user, not with `sudo`.

## Game discovery and setup

- Native and Flatpak Steam libraries are discovered automatically.
- Epic installations can be found through Legendary/Heroic, and GOG through Heroic.
- Configured Wine prefixes support Epic, GOG, Ubisoft Connect, EA App and Battle.net discovery.
- Manual import accepts individual folders, multiple folders or the immediate game folders inside a parent directory.

On first launch, enable the HDD option for games on a mechanical hard drive;
leave it off for SSD or NVMe storage. The initial Deep Scan searches game folders
and remembers DLL locations for later Fast Scans.

When importing games, choose launch setup to scan the whole imported batch and
preselect suggested executables. Review and save every selection in one window,
or adjust native/Wine executables, arguments and working folders individually.

Settings lets you set launcher locations, simultaneous game scans, cover loads
and game updates, and the number of game entries added to the screen together.
New scan and cover-loading limits take effect when the next operation starts.

## Updates and restoration

Open a game or select several games, choose DLL versions, then update the selected
games simultaneously. The Library lists versions available to download and
packages already downloaded.

For Streamline, choose an SDK release and compare its DLLs with the game's
installed components before upgrading or downgrading. Restore returns to the
original backed-up files; recovery handles interrupted component updates.

Close affected games before writing and keep the adjacent `.dlsss` backups.
For command-line use, start with an update or restore `--dry-run`, then repeat the
chosen operation with `--yes`. See the [CLI reference](DlssSwapper.Linux.Cli/README.md).

## Platform details

NVIDIA driver-profile presets, DLSS indicators and driver logging controls
are Windows-only.

The app reports backing filesystems and warns about NTFS/FUSE game libraries.

The package includes osslsigncode and its supporting libraries for signature
verification. Certificates remain app-local. See [verifier sources](Runtime/VERIFIER-SOURCES.md),
[trust bundles](Runtime/trust/README.md) and [acknowledgements](Acknowledgements).

For compilation and packaging, see [building from source](../docs/BUILDING.md).
