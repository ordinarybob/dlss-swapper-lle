# DLSS Swapper LLE for Linux

LLE adds native Linux support to DLSS Swapper: a desktop application and
command-line interface for library scanning, batch game import, automatic
launch-executable detection, DLL updates and restores, and Streamline management.
Both share one saved game library, including launcher settings, scan patterns,
artwork and history.

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

Choose HDD or standard storage settings on first launch. The initial Deep Scan
learns paths for later Fast Scans. Settings also provides launcher locations and
performance controls. Batch launch setup automatically scans imported games and
preselects suggested executables. Save the whole batch together, or adjust native
or Wine executables, arguments and working folders individually.

## Updates and restoration

Open a game or select several games to choose the latest or a specific DLL
version. The Library manages downloaded packages. Streamline provides historical
SDK selection, component comparison, updates, restore and recovery.

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
