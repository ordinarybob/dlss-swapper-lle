# Streamline component descriptions

The descriptions in `StreamlineComponentDescriptions` summarize the roles of existing game-local components, not Windows display drivers. Installing a newer plugin does not by itself add game integration or establish compatibility. DLSS Streamline plugins are distinct from the accompanying `nvngx_*` implementation DLLs.

Verified against NVIDIA documentation/source on 2026-09-04. The descriptions are original summaries; source links below document their basis.

| Component | Official source |
| --- | --- |
| `sl.common.dll` | [Shared resource, frame-constant, and NGX services in commonEntry.cpp](https://github.com/NVIDIA-RTX/Streamline/blob/main/source/plugins/sl.common/commonEntry.cpp) |
| `sl.deepdvc.dll` | [RTX Dynamic Vibrance guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDeepDVC.md) |
| `sl.directsr.dll` | [DirectSR guide: variant selection and DirectX 12 execution](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDirectSR.md) |
| `sl.dlss.dll` | [DLSS integration guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS.md), [Super Resolution and DLAA roles](https://developer.nvidia.com/rtx/dlss) |
| `sl.dlss_d.dll` | [Ray Reconstruction integration guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS_RR.md), [Ray Reconstruction role](https://developer.nvidia.com/rtx/dlss) |
| `sl.dlss_g.dll` | [Frame Generation guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideDLSS_G.md) |
| `sl.interposer.dll` | [Streamline guide: integration and feature lifecycle](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuide.md) |
| `sl.nis.dll` | [NVIDIA Image Scaling guide](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideNIS.md) |
| `sl.nvperf.dll` | [Streamline changelog: 2.3.0 adds the Nsight Perf SDK plugin](https://github.com/NVIDIA-RTX/Streamline/blob/main/changelog.txt), [Nsight Perf SDK measurement/profiling purpose](https://developer.nvidia.com/nsight-perf-sdk) |
| `sl.pcl.dll` | [PCL guide: timing measurements and migration from Reflex](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuidePCL.md) |
| `sl.reflex.dll` | [Reflex guide: low-latency control, frame limiting, and separate PCL statistics](https://github.com/NVIDIA-RTX/Streamline/blob/main/docs/ProgrammingGuideReflex.md) |

NVIDIA's [packaging script](https://github.com/NVIDIA-RTX/Streamline/blob/main/package.bat) distinguishes the Streamline plugin DLLs from the separate feature binaries. The application continues to restrict replacement to supported files already present in the selected game.
