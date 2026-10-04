# LLE workflows

## Ultra fast library scanning

First library scan takes ~15 seconds for a 4,000-game Steam library, with cached
reloads instantaneous. LLE parallelizes library parsing and game scans; Adaptive
Fast Scan reuses learned paths, while Deep Scan searches complete game folders.

![LLE Games page with Deep Scan, batch actions and the Steam Card size selector](images/workflow-game-library.png)

## Batch game import with automatic executable detection

Add a whole folder of games in one operation. LLE automatically scans every game
in the batch and preselects suggested launch executables. Save the entire batch
from one window, changing individual selections only where needed.

![LLE automatically selected launch executables for eleven imported games, ready to save together](images/workflow-manual-launch.png)

## One-click update-all and Streamline batch selection

Update selected games in parallel, with one-click latest-version selection for
their detected DLLs and a chosen Streamline SDK across the batch.

![LLE batch picker with latest DLL versions and Streamline v2.14.1 selected for 41 games](images/workflow-game-updates.png)

## Streamline package library

Browse historical Streamline SDK packages and download a chosen
release for use in individual-game or batch component updates.

![Streamline Library showing eight SDK releases available to download](images/workflow-streamline-library.png)

[Native Linux desktop and CLI](../linux/README.md) · [Overview](../README.md) · [LLE features](FEATURE_CLUSTERS.md)
