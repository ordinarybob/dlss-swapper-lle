# Packaged Linux signature verifier

These are unmodified Debian amd64 package files, extracted locally without installing packages or executing Linux code. `linux/fetch-verifier-assets.ps1` downloads and SHA256-checks the pinned packages, extracts only the required regular files, and includes their copyright/license notices. The script does not install a Linux environment.

| Package | Official checksum source | SHA256 |
| --- | --- | --- |
| osslsigncode 2.14-1 | https://packages.debian.org/sid/amd64/osslsigncode/download | bb238246ce34a62385b9c32fc50ba04fa3d3cb87876d675aad67c91f7420127b |
| libssl3t64 3.6.4-1 | https://packages.debian.org/sid/amd64/libssl3t64/download | 440d869ef7c24af2e92602c156a6753587414bd1de30b1e55f722082bc157434 |
| libzstd1 1.5.7+dfsg-4 | https://packages.debian.org/sid/amd64/libzstd1/download | 93e7930e4c25b918f1dd980cc1c6d4487654a248f5fa71eacb976ed7bbe954bd |
| zlib1g 1.3.dfsg+really1.3.2-3 | https://packages.debian.org/sid/amd64/zlib1g/download | 52c585b07bea72ef36df9ddd5d1937f4739d3caec057d827954baec256292651 |

The download URLs and exact source archive checksums are pinned in the script. The complete osslsigncode source archive, Debian packaging archive, descriptor, GPL text, and linking exception are included under `Acknowledgements/osslsigncode`. OpenSSL includes its Apache 2.0 license; zstd uses its included BSD-3-clause option; zlib includes its license. These source archives are redistribution material, not runtime dependencies.

## Runtime boundary — not yet tested on Linux

- ELF64, little-endian, x86-64. Interpreter: `/lib64/ld-linux-x86-64.so.2`.
- `osslsigncode` needs `libssl.so.3`, `libcrypto.so.3`, `libz.so.1`, and `libc.so.6`.
- The app-local OpenSSL files additionally need `libzstd.so.1`; all non-glibc dependencies are supplied in `tools/lib`.
- The bundled OpenSSL imports require **glibc 2.38 or newer**. The verifier executable itself requires glibc 2.34. Do not describe this artifact as supporting older glibc systems or musl-only systems.
- This is a statically inspected requirement, **not a tested minimum Linux baseline or a compatibility guarantee**. Native startup, signed/unsigned/tampered DLL behavior, and certificate validation remain pending Linux verification.
- Set `LD_LIBRARY_PATH` to the app's absolute `tools/lib` directory only for the packaged verifier child process. Do not change the application's or host's global library path. Custom verifier paths must retain their own dependency handling.
- Archive packaging must mark `tools/osslsigncode` executable (0755); extracting on Windows does not establish Unix executable permissions.

No glibc, loader, package-manager configuration, OpenSSL configuration, or system trust store is installed or replaced. Trust bundles are supplied separately by the application.
