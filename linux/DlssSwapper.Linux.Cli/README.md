# DLSS Swapper LLE Linux CLI

Manage the same saved game library used by the Linux desktop application.
Use `--help` to see the available commands; starting without arguments also
shows help. See the [Linux guide](../README.md) for installation and requirements.

Do not run it with `sudo`. Close every selected game before updating or
restoring files.

## Commands

```text
dlss-swapper-linux discover [--steam-root PATH ...]
dlss-swapper-linux filesystems [--path PATH ...] [--root PATH ...]

dlss-swapper-linux state show
dlss-swapper-linux state add-game /games/Example
dlss-swapper-linux state remove-game /games/Example
dlss-swapper-linux state add-steam-root /games/SteamLibrary
dlss-swapper-linux state remove-steam-root /games/SteamLibrary
dlss-swapper-linux state add-pattern 'Engine/Plugins/*/Binaries/Win64'
dlss-swapper-linux state remove-pattern 'Engine/Plugins/*/Binaries/Win64'
dlss-swapper-linux state restore-steam
dlss-swapper-linux state add-provider-prefix /games/wine-prefix
dlss-swapper-linux state add-legendary-config /path/to/legendary
dlss-swapper-linux state add-heroic-config /path/to/heroic
dlss-swapper-linux state restore-providers

dlss-swapper-linux scan
dlss-swapper-linux scan --app-id 123 --path /games/Example --root /games/groups

dlss-swapper-linux update --app-id 123 --dry-run
dlss-swapper-linux update --app-id 123 --yes
dlss-swapper-linux update --path /games/Example --family dlss \
  --version dlss=3.10.4.0 --dry-run

dlss-swapper-linux restore --path /games/Example --dry-run
dlss-swapper-linux restore --path /games/Example --yes
```

Options may be repeated:

- `--app-id ID` selects a game found by Steam discovery.
- `--path PATH` selects one explicit game directory.
- `--root PATH` selects each immediate child directory, never the root itself
  or grandchildren.
- `--steam-root PATH` adds a Steam root, `steamapps` directory, or
  `libraryfolders.vdf`.
- `--all` selects the saved library, including Steam, manual games and supported
  launcher sources, while respecting exclusions. It cannot be mixed
  with other game selectors.
- `--family KEY` limits update or restore to a manifest family.
- `--version KEY=VERSION` chooses an exact manifest version. If that version
  has multiple builds, use `KEY=VERSION@MD5PREFIX`.
- `--manifest PATH` overrides the bundled `Assets/static_manifest.json`.

`scan` with no game selector scans the saved library. DLL update and restore
commands require an explicit selector or `--all`, and require `--yes` unless
they are run with `--dry-run`. Filesystem roots such as `/` are rejected.

## Update and restore semantics

- Only families actually detected in each game are planned.
- Latest selection uses manifest `version_number` and excludes development or
  signature-invalid records.
- DLSS 1.x cannot be replaced with a 2.x-or-newer DLL, or vice versa.
- Files that already match the selected version are not downloaded or rewritten.
- A family/version payload is downloaded at most once per run and reused from
  `${XDG_CACHE_HOME:-~/.cache}/dlss-swapper-lle`.
- The ZIP size and MD5 and extracted DLL size and MD5 are checked against the
  manifest.
- Before replacing a DLL, the original is copied beside it as
  `<filename>.dlsss` only when that backup does not already exist.
- Restore moves the adjacent `.dlsss` file over the live DLL, consuming the backup.
- `--dry-run` uses the normal selection, scan, eligibility, compatibility, and
  planning logic, but performs no downloads, cache writes, backups, updates, or
  restores.

## Saved library and launcher locations

Steam discovery checks native, XDG and Flatpak locations and additional
libraries from `libraryfolders.vdf`. The GUI and `state` commands share manual
games, launcher locations, exclusions, scan patterns, preferences and history.

Use `state add-provider-prefix` for Wine installations, `add-legendary-config`
for Legendary and `add-heroic-config` for Heroic. Matching `remove-...` commands
remove those locations. `state set-heroic-executable PATH` chooses Heroic's
executable; `state clear-heroic-executable` clears that override. Paths must be
absolute. `state restore-steam` and `state restore-providers` clear the respective
launcher exclusions.

`filesystems` reports the backing filesystem and warns about NTFS/FUSE libraries.

## Streamline components

The CLI can inspect, update, restore, and recover installed Streamline components.
Its `--package` option accepts a **locally staged NVIDIA production folder**
(the SDK's `bin/x64` directory), not development binaries or a ZIP. The Linux
GUI also provides SDK downloads and version selection.

```sh
dlss-swapper-linux streamline inspect --path "/games/My Game"
dlss-swapper-linux streamline update --path "/games/My Game" --package "/packages/streamline/bin/x64" --dry-run
dlss-swapper-linux streamline update --path "/games/My Game" --package "/packages/streamline/bin/x64" --yes
dlss-swapper-linux streamline restore --path "/games/My Game" --yes
dlss-swapper-linux streamline recover --path "/games/My Game" --yes
```

Only already-installed supported components are replaced. Close the game before
updating or restoring. Dry-run lists the current components and recovery status;
it does not validate package contents, create backups, or write game/cache/state files.
The Linux GUI exposes the same operations under a game's **Streamline (experimental)**
context submenu in both list and grid views, with SDK version selection and
component comparison. Updates, restore and recovery require confirmation.

## Reset application data

`dlss-swapper-linux reset --yes` removes LLE's Linux configuration and application
cache, including the saved game library and settings. Artwork cached beside a
SteamLibrary and game-local backups are preserved.
