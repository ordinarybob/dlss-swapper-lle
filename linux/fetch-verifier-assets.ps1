$ErrorActionPreference = 'Stop'
# Download/extract only. Does not install packages or execute Linux binaries.
$packages = @(
    @('o/osslsigncode/osslsigncode_2.14-1_amd64.deb', 'bb238246ce34a62385b9c32fc50ba04fa3d3cb87876d675aad67c91f7420127b'),
    @('o/openssl/libssl3t64_3.6.4-1_amd64.deb', '440d869ef7c24af2e92602c156a6753587414bd1de30b1e55f722082bc157434'),
    @('libz/libzstd/libzstd1_1.5.7+dfsg-4_amd64.deb', '93e7930e4c25b918f1dd980cc1c6d4487654a248f5fa71eacb976ed7bbe954bd'),
    @('z/zlib/zlib1g_1.3.dfsg+really1.3.2-3_amd64.deb', '52c585b07bea72ef36df9ddd5d1937f4739d3caec057d827954baec256292651')
)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('lle-verifier-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$toolsDir = Join-Path $PSScriptRoot 'Runtime/tools'
$libDir = Join-Path $toolsDir 'lib'
New-Item -ItemType Directory -Force -Path $libDir | Out-Null
foreach ($package in $packages) {
    $filename = [IO.Path]::GetFileName($package[0])
    $download = Join-Path $scratch $filename
    Invoke-WebRequest -Uri ('https://deb.debian.org/debian/pool/main/' + $package[0]) -OutFile $download
    if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $package[1]) { throw "SHA256 mismatch: $filename" }
    $unpacked = Join-Path $scratch ($filename + '.contents')
    New-Item -ItemType Directory -Path $unpacked | Out-Null
    & tar -xf $download -C $unpacked
    if ($LASTEXITCODE -ne 0) { throw "Cannot extract $filename" }
    $payload = Get-ChildItem -LiteralPath $unpacked -Filter 'data.tar.*' | Select-Object -First 1
    # Select regular files only: do not create package symlinks on the host.
    $members = & tar -tf $payload.FullName
    $selected = @($members | Where-Object { $_ -match '^\./usr/bin/osslsigncode$|^\./usr/lib/x86_64-linux-gnu/(libcrypto\.so\.3|libssl\.so\.3|libzstd\.so\.1\.5\.7|libz\.so\.1\.3\.2)$|^\./usr/share/doc/[^/]+/copyright$' })
    & tar -xf $payload.FullName -C $unpacked @selected
    if ($LASTEXITCODE -ne 0) { throw "Cannot extract payload: $filename" }
    foreach ($member in $selected) {
        $source = Join-Path $unpacked $member
        $leaf = [IO.Path]::GetFileName($source)
        if ($leaf -eq 'copyright') {
            $name = Split-Path (Split-Path $source -Parent) -Leaf
            $destination = Join-Path $PSScriptRoot "Acknowledgements/$name"
            New-Item -ItemType Directory -Force -Path $destination | Out-Null
            Copy-Item -LiteralPath $source -Destination (Join-Path $destination 'copyright')
        } elseif ($leaf -eq 'osslsigncode') {
            Copy-Item -LiteralPath $source -Destination $toolsDir
        } else {
            $soname = switch ($leaf) { 'libzstd.so.1.5.7' { 'libzstd.so.1' }; 'libz.so.1.3.2' { 'libz.so.1' }; default { $leaf } }
            Copy-Item -LiteralPath $source -Destination (Join-Path $libDir $soname)
        }
    }
}
$sourceDir = Join-Path $PSScriptRoot 'Acknowledgements/osslsigncode'
foreach ($source in @(
    @('osslsigncode_2.14-1.dsc', '805a8d756e338ff47e41b394c0dcbfb078e3712c4648ca33a72bd1fa4c509036'),
    @('osslsigncode_2.14.orig.tar.gz', 'bdf249cbf23a84262dd30bb9b3a96a17109ff8de5ea30212e26af173cc8b2195'),
    @('osslsigncode_2.14-1.debian.tar.xz', 'bddad976e45d6a74a5ffc853307e24c1317a7d12b2ed9a22540d52e15ee75a09')
)) {
    $destination = Join-Path $sourceDir $source[0]
    Invoke-WebRequest -Uri ('https://deb.debian.org/debian/pool/main/o/osslsigncode/' + $source[0]) -OutFile $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $source[1]) { throw "Source SHA256 mismatch: $($source[0])" }
}
& tar -xf (Join-Path $sourceDir 'osslsigncode_2.14.orig.tar.gz') -C $sourceDir --strip-components 1 'mtrojnar-osslsigncode-65a4c40/COPYING.txt' 'mtrojnar-osslsigncode-65a4c40/LICENSE.txt'
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract verifier license' }
$apache = Join-Path $PSScriptRoot 'Acknowledgements/libssl3t64/LICENSE-2.0.txt'
Invoke-WebRequest -Uri 'https://www.apache.org/licenses/LICENSE-2.0.txt' -OutFile $apache
if ((Get-FileHash -LiteralPath $apache -Algorithm SHA256).Hash -ne 'cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30') { throw 'Apache license SHA256 mismatch' }
Write-Output "Verified package downloads retained at $scratch"
