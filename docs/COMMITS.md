# LLE V1 commit inventory

Based on upstream [v1.2.5.0](https://github.com/beeradmoore/dlss-swapper/commit/4c58e1969ea39b35f21754e4f81e4e3baed5f8ca).
The [feature list](FEATURE_CLUSTERS.md) groups these commits by functionality.

## LLE changes

1. [f42128f](https://github.com/ordinarybob/dlss-swapper-lle/commit/f42128f9a35a4b90cb65989a97dc89f17381f4d4) — Fix Steam discovery with stale library indexes.
2. [19352e4](https://github.com/ordinarybob/dlss-swapper-lle/commit/19352e42a7d3f826738d9ac207b65dab7a2f2579) — Scan games in parallel and batch display and database updates.
3. [32e4e1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/32e4e1f71403552d44a926c23ae6eb8e4247a2bf) — Add bulk game import and launcher exclusions.
4. [aa7ec4b](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa7ec4b0a2427fe79bf99a7d7642cd201844ffff) — Add update-to-latest commands to the batch workflow from PR #913.
5. [d22493d](https://github.com/ordinarybob/dlss-swapper-lle/commit/d22493d2787868b57bff2e072d283a19108539b2) — Add name/DLSS-version sorting and track games awaiting scan results.
6. [4238177](https://github.com/ordinarybob/dlss-swapper-lle/commit/4238177934f4a53f302b382ecdec708e45efd0f2) — Refine games browsing and bulk removal.
7. [fb0caa3](https://github.com/ordinarybob/dlss-swapper-lle/commit/fb0caa31f5816b13058b53daf4cb5b0019966aaa) — Make game details compact and responsive.
8. [9b59d85](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b59d8566cf9ea18f94a87d5bd9c32bd0757781c) — Use compact navigation and keep restored windows within the screen.
9. [7f09c1d](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f09c1ddbf823cead6c3dd542d1fbd7daac5d2ae) — Use compact DLL Library controls and version cards.
10. [7a26524](https://github.com/ordinarybob/dlss-swapper-lle/commit/7a26524163d1b25dd9545789ae957c9d0f9cad90) — Allow direct numeric entry of performance limits.
11. [40265c0](https://github.com/ordinarybob/dlss-swapper-lle/commit/40265c0e9fcca84fd7cf3d03f1dde6d978a6469a) — Remove upstream new-DLL issue workflow.
12. [efbe1a4](https://github.com/ordinarybob/dlss-swapper-lle/commit/efbe1a4af4b9ec66b18e03807bb9378fd54163a9) — Add the native Linux command-line application.
13. [32f7ed6](https://github.com/ordinarybob/dlss-swapper-lle/commit/32f7ed60a3769fcf4704639c73cce78cba213526) — Add the native Linux desktop application.
14. [16721dc](https://github.com/ordinarybob/dlss-swapper-lle/commit/16721dc7a14a2bdc507fe9fb4e60530fc451dc3a) — Restore Steam installed-game candidate filtering.
15. [0099e3f](https://github.com/ordinarybob/dlss-swapper-lle/commit/0099e3f88feedbbc4bb9b1e93d26b945a7a4ab78) — Keep unscanned games out of the supported-DLL filter and load covers separately.
16. [140543d](https://github.com/ordinarybob/dlss-swapper-lle/commit/140543d05cda54516dc7722466eb2858ed7f1ac3) — Discover conventional Steam libraries without client index.
17. [b5e4402](https://github.com/ordinarybob/dlss-swapper-lle/commit/b5e4402852055981a5ea8699061d0b13206dd071) — Preserve Steam cache during fallback discovery.
18. [159e230](https://github.com/ordinarybob/dlss-swapper-lle/commit/159e23075cda300a3cfcd7235b274e3d686d2c72) — Change the number of simultaneous game scans without restarting.
19. [95ede66](https://github.com/ordinarybob/dlss-swapper-lle/commit/95ede6689505fc1630ba893e8343d38ad9db6ecb) — Cache scans that find no supported DLLs.
20. [40f3bf1](https://github.com/ordinarybob/dlss-swapper-lle/commit/40f3bf1b23867c2c4113df6fff50922cd5a4a794) — Add the first-start HDD storage option.
21. [f3fdb30](https://github.com/ordinarybob/dlss-swapper-lle/commit/f3fdb30f89d8d394af70fccf68f11934472ee945) — Index game assets from NTFS metadata.
22. [dc2237a](https://github.com/ordinarybob/dlss-swapper-lle/commit/dc2237affc2fb660beea90278cb9f3149ad4d824) — Reuse game-folder listings across DLL scans.
23. [3e19518](https://github.com/ordinarybob/dlss-swapper-lle/commit/3e195186fbc83cd62c1272e4003c5faef600acf3) — Keep initial game loading responsive.
24. [5c6780a](https://github.com/ordinarybob/dlss-swapper-lle/commit/5c6780a6aa9e24955cb85c3d770c72e62f249f29) — Honor configured scan concurrency.
25. [bb657c7](https://github.com/ordinarybob/dlss-swapper-lle/commit/bb657c73b1136ffdeea00947e406e5abef6b9e13) — Display each game as its scan finishes.
26. [aa5f1bb](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa5f1bb4a77cc76dd5734711359e68b6cdebb912) — Read DLLs in larger chunks when calculating hashes.
27. [4021e63](https://github.com/ordinarybob/dlss-swapper-lle/commit/4021e638b462c0ed8b218353791c3b586d31066e) — Prioritize learned game asset paths.
28. [182f6b9](https://github.com/ordinarybob/dlss-swapper-lle/commit/182f6b90ec6359f67236c9aa7e69a48a601d2341) — Prioritize usable candidate library before fallback scan.
29. [48b903a](https://github.com/ordinarybob/dlss-swapper-lle/commit/48b903a768469477400ed9eeb6ff7da0037deecc) — Parallelize Steam manifest parsing and game registration.
30. [1d9ab27](https://github.com/ordinarybob/dlss-swapper-lle/commit/1d9ab27dd1bafa7a680b9b75e4ee71f1f41f7666) — Add complete local app data reset.
31. [07a9b80](https://github.com/ordinarybob/dlss-swapper-lle/commit/07a9b8053c2170939781459a770d18b9bfb356aa) — Resize cover cards to fill the grid and save the preferred card size.
32. [f715332](https://github.com/ordinarybob/dlss-swapper-lle/commit/f715332fbe138de6dda108e205b7ac9937e09518) — Draw card hover border around the cover instead of over it.
33. [3d58a48](https://github.com/ordinarybob/dlss-swapper-lle/commit/3d58a484292e7ba7457ee1ff99ce94fc629da960) — Clarify manual game folder prompts.
34. [afc9365](https://github.com/ordinarybob/dlss-swapper-lle/commit/afc9365245cee0b75b8efcb915c32bd3cd4f9868) — Optimize first-run library initialization.
35. [ba90b23](https://github.com/ordinarybob/dlss-swapper-lle/commit/ba90b23a98f975d444ca37c8eb48241581909297) — Set HDD mode to two simultaneous game scans and one cover load.
36. [281b4bb](https://github.com/ordinarybob/dlss-swapper-lle/commit/281b4bbeeb526fdabadf99475b14ab54cd1b88fe) — Make manual import notices explicit opt-outs.
37. [7f4f0d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/7f4f0d94d8a8d4f05250c6e9a1ecd5f23d73e50a) — Load covers for manually added games and align toolbar controls.
38. [f24dc18](https://github.com/ordinarybob/dlss-swapper-lle/commit/f24dc18163afcc6d72d88de87f4bc89a991147b0) — Find manual-game covers through MediaWiki without an API key.
39. [b9ad50f](https://github.com/ordinarybob/dlss-swapper-lle/commit/b9ad50f68b6bec3a319d4a7cb8c3cb0cbf676784) — Generalize and throttle manual artwork fallback.
40. [cdb40cf](https://github.com/ordinarybob/dlss-swapper-lle/commit/cdb40cfb113cc72cd66f43304871ada5424f5445) — Show the game count and adjustable cover-card sizes.
41. [49d8ef3](https://github.com/ordinarybob/dlss-swapper-lle/commit/49d8ef3961d14170febfdfdc3ab80fc85db2d087) — Throttle loading progress updates.
42. [c8b2058](https://github.com/ordinarybob/dlss-swapper-lle/commit/c8b20581967c19fe6b93febf5b7b368484e6bc58) — Show the usable game list before background metadata loading finishes.
43. [b637dcb](https://github.com/ordinarybob/dlss-swapper-lle/commit/b637dcbe63cfd66c8e6b61f05488eb10519c22a6) — Defer DLL hashing until required.
44. [69cd628](https://github.com/ordinarybob/dlss-swapper-lle/commit/69cd628389469c7075ac6923354a9a7853751df4) — Make deep scanning explicit and configurable.
45. [10edb39](https://github.com/ordinarybob/dlss-swapper-lle/commit/10edb39c5eeabf4523482911718c9a7402c5bcaf) — Refine compact header and default height.
46. [0f37990](https://github.com/ordinarybob/dlss-swapper-lle/commit/0f37990ca5324e4c3ff94044aafcda3d02fc6120) — Expose fallback art source and compact batch UI.
47. [f435cd7](https://github.com/ordinarybob/dlss-swapper-lle/commit/f435cd7f7717bff0f20ff5f88903880855656052) — Learn deep-scan paths and place batch controls below toolbar.
48. [ed42f95](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed42f95ed555ba59d12a9c5a43f9a9e84ffe596f) — Add text game context menu.
49. [6bd2e19](https://github.com/ordinarybob/dlss-swapper-lle/commit/6bd2e19a2389c94a57f04f15c90606b4ebe9d0b4) — Make history dialog responsive.
50. [269c65d](https://github.com/ordinarybob/dlss-swapper-lle/commit/269c65df43ebd8886659b30ba2c6bd8a8ab99344) — Reserve space for the Games toolbar before sizing the search area.
51. [2a6b54b](https://github.com/ordinarybob/dlss-swapper-lle/commit/2a6b54bda9aa3bf7f63cd92f0673ed5eab6edfbe) — Skip files already at the selected version during batch updates.
52. [787e8b7](https://github.com/ordinarybob/dlss-swapper-lle/commit/787e8b75d350acf8b239a3dee1f8e73032b6444a) — Optimize fast-scan pattern index.
53. [1b2753a](https://github.com/ordinarybob/dlss-swapper-lle/commit/1b2753a8ce5c2d809a7275648d5760dbf7c6152e) — Run one-time Deep Scan after first launch.
54. [95703a6](https://github.com/ordinarybob/dlss-swapper-lle/commit/95703a68314e19b5b00a1d28842cf79f41db0578) — Show initial scan results without waiting for every library.
55. [1c466d9](https://github.com/ordinarybob/dlss-swapper-lle/commit/1c466d9eabfd695d6e6d208af65181801126b1ca) — Clarify manual import folder choices.
56. [d79ed42](https://github.com/ordinarybob/dlss-swapper-lle/commit/d79ed4214bb6db7ff5a37a818d205aaedac29b4a) — Compact manual import notices.
57. [51738bf](https://github.com/ordinarybob/dlss-swapper-lle/commit/51738bff26e7a61bf50bfc5da57dd3ae91a012f8) — Keep header controls together at wide widths.
58. [bb2d3bc](https://github.com/ordinarybob/dlss-swapper-lle/commit/bb2d3bcdead657f2069ebcde02bacda73228f47d) — Save the Linux game library between runs and reuse learned DLL locations.
59. [ce1d055](https://github.com/ordinarybob/dlss-swapper-lle/commit/ce1d055cb6360aa46a332db6feb7b18919da1ced) — Use persistent adaptive scans in Linux GUI.
60. [001fd5b](https://github.com/ordinarybob/dlss-swapper-lle/commit/001fd5b573cb0748c14cc060d69edc0174de3312) — Add Linux scan, storage and library settings.
61. [44626e3](https://github.com/ordinarybob/dlss-swapper-lle/commit/44626e349b0627890451f631f8c210a50153debc) — Load Linux game artwork in the background.
62. [5286737](https://github.com/ordinarybob/dlss-swapper-lle/commit/528673714e001525acf1a68993daee73a503fd1a) — Add Linux library browsing controls.
63. [a4ea439](https://github.com/ordinarybob/dlss-swapper-lle/commit/a4ea43984c9ff4cd59fbdc8db456d8ca94a3504e) — Add native Linux manual import workflows.
64. [3b01d2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/3b01d2eda24ca1c2fabb0edb1e1a54856c882809) — Add Linux game context workflows.
65. [e75a4e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/e75a4e72da1502ff4421339d599d6a10e676a525) — Add the Linux DLL Library page and application navigation.
66. [3e73147](https://github.com/ordinarybob/dlss-swapper-lle/commit/3e73147a7e4a546fa87ac1f4631d98a5990341f8) — Add Linux filesystem reports and application-data reset.
67. [58d7803](https://github.com/ordinarybob/dlss-swapper-lle/commit/58d7803b233a773be90041dbc3c20a458f6d852f) — Update the Linux command-line application name.
68. [cb1389a](https://github.com/ordinarybob/dlss-swapper-lle/commit/cb1389a7b8dcf465e1b2931feb3ad368a17f716c) — Skip scan patterns already covered by existing patterns.
69. [45daf2e](https://github.com/ordinarybob/dlss-swapper-lle/commit/45daf2ee7df87ce8afc7924ca337246524a6796c) — Redesign Linux GUI shell and grid.
70. [bbb5f93](https://github.com/ordinarybob/dlss-swapper-lle/commit/bbb5f93d9399ca7e911dc5b9d7b870fd688e3dec) — Finalize LLE release identity and policy.
71. [3d80403](https://github.com/ordinarybob/dlss-swapper-lle/commit/3d8040300d61970338e6b528885d0bbc8d35e089) — Complete installer version metadata.
72. [a1f5711](https://github.com/ordinarybob/dlss-swapper-lle/commit/a1f57117fbfa40a320167b10f6ff8bdfc6977001) — Exclude debug symbols from release packages.
73. [e746c32](https://github.com/ordinarybob/dlss-swapper-lle/commit/e746c3229d4365b14dbbe6837563f2e467145f05) — Remove dead updater and support resources.
74. [23af5da](https://github.com/ordinarybob/dlss-swapper-lle/commit/23af5da8d47761e1c37022f0ae1ce4979be3df64) — Align Linux GUI with Windows workflows.
75. [4907f4c](https://github.com/ordinarybob/dlss-swapper-lle/commit/4907f4c7f185b6312e2ba4ec0457708fb8b7a9b9) — Validate native Linux library and state workflows.
76. [ed6a2e7](https://github.com/ordinarybob/dlss-swapper-lle/commit/ed6a2e77df0f818b58734bc08ee5452d612cffd7) — Harden Windows discovery, updates and state handling.
77. [ec48dd8](https://github.com/ordinarybob/dlss-swapper-lle/commit/ec48dd89fc0e6c58967ec25e84721f89099088db) — Handle Linux scan, saved-state and file-operation failures.
78. [2d515d7](https://github.com/ordinarybob/dlss-swapper-lle/commit/2d515d7f5635d3c247a62f29bcd12151bf8413c5) — Make installer packaging self-contained.
79. [848df8d](https://github.com/ordinarybob/dlss-swapper-lle/commit/848df8de0b8ae14d05a2b3c9c8b27d07e1f9ec97) — Simplify grid density to card size.
80. [4633a30](https://github.com/ordinarybob/dlss-swapper-lle/commit/4633a303d4166353e3789220ec34e2d36d66df23) — Prevent card size selector collision.
81. [ffa7f0c](https://github.com/ordinarybob/dlss-swapper-lle/commit/ffa7f0c67097bf81590e7d7b27da2d9f72c77000) — Restore reliable DLL downloads.
82. [d2b825c](https://github.com/ordinarybob/dlss-swapper-lle/commit/d2b825c410b02b76993306ea6f61ba9bc5b52c64) — Make cached launches immediately usable.
83. [4beeead](https://github.com/ordinarybob/dlss-swapper-lle/commit/4beeead33b164cde0376266775b23fdc0835594f) — Restore DLSS preset options to batch updates.
84. [5726d4d](https://github.com/ordinarybob/dlss-swapper-lle/commit/5726d4da4cc63a5fad0da2cc3f295bd955a3a7d0) — Update Streamline components as a group with rollback on failure.
85. [4f8f447](https://github.com/ordinarybob/dlss-swapper-lle/commit/4f8f4478d62f2c61f10d9930238d7196441d2e47) — Complete Linux scanning and library workflows.
86. [9b12179](https://github.com/ordinarybob/dlss-swapper-lle/commit/9b12179196f6a02e8d4e7847fb8873ba644fd6aa) — Harden discovery, downloads and game metadata.
87. [5e15ecf](https://github.com/ordinarybob/dlss-swapper-lle/commit/5e15ecfe2c8d738ebecb0c6d0e86fcc2c8f984d1) — Fix Streamline x64 SDK selection for multi-architecture releases.
88. [5595e31](https://github.com/ordinarybob/dlss-swapper-lle/commit/5595e317a4ecd4f75c2ef8e1d876d143e934954f) — Reduce batch swap validation, copy and database overhead.
89. [59031ac](https://github.com/ordinarybob/dlss-swapper-lle/commit/59031acf796f6e73e18454c2b4023e07591e1722) — Update Streamline in several games at once without overlapping writes to the same folder.
90. [8069d8b](https://github.com/ordinarybob/dlss-swapper-lle/commit/8069d8b9d25db0016769659ba0934f8d84840bdc) — Queue content dialogs to prevent download completion crashes during batch setup.
91. [708e7da](https://github.com/ordinarybob/dlss-swapper-lle/commit/708e7da5cc9165be43fd659f8c9f734c7bde3eb2) — Browse and download specific Streamline SDK releases.
92. [a8a794e](https://github.com/ordinarybob/dlss-swapper-lle/commit/a8a794ee2ab9855c679a62a8968ab5bdf9d82853) — List Streamline releases and select SDK versions in game and batch updates.
93. [04d3e64](https://github.com/ordinarybob/dlss-swapper-lle/commit/04d3e64264a98766d5b19ba2c6b287a520906b43) — Combine Library downloads, including Streamline, into one progress bar.
94. [4a34d03](https://github.com/ordinarybob/dlss-swapper-lle/commit/4a34d0343e30edde54d4c63663e6a29de1b273e6) — Accept suggested launch defaults for all imported games in one click.
95. [c72346d](https://github.com/ordinarybob/dlss-swapper-lle/commit/c72346d16b48572c9ace234515064c6e3331cdda) — Rank launch executables using game names, abbreviations and file metadata.
96. [283184b](https://github.com/ordinarybob/dlss-swapper-lle/commit/283184bf48bbde2a4d8af209ec61493b431f6504) — Scan all launch candidates before review and place bulk action in footer.
97. [2df6c1f](https://github.com/ordinarybob/dlss-swapper-lle/commit/2df6c1f85827307452157da79d0e0d58bb564391) — Refine launch suggestions using helper and companion patterns.
98. [3cf87f2](https://github.com/ordinarybob/dlss-swapper-lle/commit/3cf87f2e3afe3fa909f6fe91a5d01898bef21389) — Use canonical catalog records in Linux GUI cache fixtures.
99. [ae087f9](https://github.com/ordinarybob/dlss-swapper-lle/commit/ae087f9ad2134b0e85aa6f8ebdc24deb0d098206) — Fix Linux PE metadata, header layout and Streamline download feedback.
100. [8abfd44](https://github.com/ordinarybob/dlss-swapper-lle/commit/8abfd44ed8b58cfa14745ad4bb08ac5ca7e28f68) — Adapt upstream scan-thread handling to LLE's configurable concurrency, and import DLL metadata and translations.
101. [e129dd9](https://github.com/ordinarybob/dlss-swapper-lle/commit/e129dd9eb46064ae3c27481ee767f6fceadae729) — Replace sequential launch setup with one editable game list.
102. [d8bdc24](https://github.com/ordinarybob/dlss-swapper-lle/commit/d8bdc249daa24cfba42b5b2324f2a304b8fce499) — Show launch filenames collapsed and full paths in dropdowns.
103. [63805d4](https://github.com/ordinarybob/dlss-swapper-lle/commit/63805d4b7207331b59fbc836e25e9d772b205797) — Allow closing launch setup with unselected games.
104. [e7fdd36](https://github.com/ordinarybob/dlss-swapper-lle/commit/e7fdd36831d8002ec83b14e9e7924060a33d65d1) — Exclude unused Windows runtime payloads and share the Linux runtime between desktop and CLI.
105. [8200258](https://github.com/ordinarybob/dlss-swapper-lle/commit/820025868c5ad07616047fd310895c6e39f46052) — Package Windows and Linux portable releases.
106. [769a062](https://github.com/ordinarybob/dlss-swapper-lle/commit/769a062fd4de0360c8c4eb392f96de580b5dc5de) — Exclude local debug paths from release assemblies.
107. [831fad0](https://github.com/ordinarybob/dlss-swapper-lle/commit/831fad0e70f6662a51815afba766155757584f55) — Use reproducible source paths in release assemblies.
108. [923ea5e](https://github.com/ordinarybob/dlss-swapper-lle/commit/923ea5e801e9084004cae9f67150b37ca16a8ad1) — Align Linux game workflows and responsive controls with Windows.
109. [3d52a5d](https://github.com/ordinarybob/dlss-swapper-lle/commit/3d52a5d10f58f809e01b586e614a6eb0414853df) — Set LLE V1 version and package names.
110. [509ac0b](https://github.com/ordinarybob/dlss-swapper-lle/commit/509ac0ba8a55124448b1c583ca7cc6c7d62395e3) — Preserve settings edits on navigation and exercise native desktop events.
111. [1273888](https://github.com/ordinarybob/dlss-swapper-lle/commit/1273888ce08f836927f1774d82a5935984b8bb00) — Include the Git revision in Linux build information.
112. [7517294](https://github.com/ordinarybob/dlss-swapper-lle/commit/75172943214fa4890fe19a77b8c5268d97e3fdae) — Pass the Linux package revision as one MSBuild argument.

## Upstream imports and adaptations

1. [aa7ec4b](https://github.com/ordinarybob/dlss-swapper-lle/commit/aa7ec4b0a2427fe79bf99a7d7642cd201844ffff) — Batch selection, update execution and result reporting from RafaelHGOliveira's [PR #913](https://github.com/beeradmoore/dlss-swapper/pull/913), with LLE-specific extensions in the same commit.
2. [21adc8c](https://github.com/ordinarybob/dlss-swapper-lle/commit/21adc8c7c04f16120a0637e51957a0eff5b73b9b) — Official DLL catalog through DLSS 310.7.129 and XeLL 1.3.2.10 from upstream [9c757eb](https://github.com/beeradmoore/dlss-swapper/commit/9c757eb); Ray Reconstruction Preset F from [935534b](https://github.com/beeradmoore/dlss-swapper/commit/935534b).
3. [8abfd44](https://github.com/ordinarybob/dlss-swapper-lle/commit/8abfd44ed8b58cfa14745ad4bb08ac5ca7e28f68) — Scan/loading correction from [#933](https://github.com/beeradmoore/dlss-swapper/pull/933), eleven additional DLSS SR/RR/FG catalog records through 310.9.1, Japanese translations from [9015474](https://github.com/beeradmoore/dlss-swapper/commit/9015474) and [9c757eb](https://github.com/beeradmoore/dlss-swapper/commit/9c757eb), and Hebrew from [#950](https://github.com/beeradmoore/dlss-swapper/pull/950).

[Attribution](../ATTRIBUTION.md) · [Reusing LLE changes](../UPSTREAMING.md)
