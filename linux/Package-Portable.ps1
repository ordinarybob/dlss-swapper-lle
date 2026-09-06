param([string]$Dotnet = (Join-Path $PSScriptRoot '../../.dotnet-sdk/dotnet.exe'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath((Join-Path $repo '../../outputs/DLSS.Swapper-LLE-test-linux-x64.tar.gz'))
$stage = Join-Path ([IO.Path]::GetTempPath()) ('lle-linux-package-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $stage)
foreach ($app in @(@('Cli', 'cli'), @('Gui', 'gui'))) {
    $project = Join-Path $PSScriptRoot "DlssSwapper.Linux.$($app[0])/DlssSwapper.Linux.$($app[0]).csproj"
    & $Dotnet publish $project -c Release -r linux-x64 --self-contained true --no-restore '-p:LlePackageFormat=tar.gz' -o (Join-Path $stage $app[1]) -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($app[0])" }
}
foreach ($app in @('cli', 'gui')) {
    foreach ($asset in @('tools/osslsigncode', 'tools/lib/libcrypto.so.3', 'tools/lib/libssl.so.3',
            'tools/lib/libz.so.1', 'tools/lib/libzstd.so.1', 'trust/code-signing.pem', 'trust/timestamp-signing.pem')) {
        if (!(Test-Path -LiteralPath (Join-Path $stage "$app/$asset") -PathType Leaf)) { throw "Missing $app/$asset" }
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $stage 'README.md')
# Notices in the GUI folder cover the entire archive, including the CLI/runtime.
if (!(Test-Path -LiteralPath (Join-Path $stage 'gui/Acknowledgements/osslsigncode/COPYING.txt'))) {
    throw 'Verifier license was not published.'
}
[void](New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent))
$temporary = $output + '.tmp'
$stream = [IO.File]::Create($temporary)
$gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionLevel]::Optimal)
$writer = [System.Formats.Tar.TarWriter]::new($gzip, $true)
try {
    foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse | Sort-Object FullName) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link: $($file.FullName)" }
        $name = [IO.Path]::GetRelativePath($stage, $file.FullName).Replace('\', '/')
        $type = if ($file.PSIsContainer) { [System.Formats.Tar.TarEntryType]::Directory } else { [System.Formats.Tar.TarEntryType]::RegularFile }
        $entry = [System.Formats.Tar.PaxTarEntry]::new($type, $name)
        $executable = $file.PSIsContainer -or $name -match '^(cli/dlss-swapper-linux|gui/dlss-swapper-linux-gui|(cli|gui)/tools/osslsigncode)$'
        $entry.Mode = [IO.UnixFileMode]$(if ($executable) { 493 } else { 420 }) # 0755 / 0644
        if ($file.PSIsContainer) { $writer.WriteEntry($entry); continue }
        $payloadStream = [IO.File]::OpenRead($file.FullName)
        try { $entry.DataStream = $payloadStream; $writer.WriteEntry($entry) } finally { $payloadStream.Dispose() }
    }
} finally { $writer.Dispose(); $gzip.Dispose(); $stream.Dispose() }
# Only replace the stable archive after publish and archive generation succeed.
[IO.File]::Move($temporary, $output, $true)
Get-FileHash -LiteralPath $output -Algorithm SHA256
Write-Output "Published $output; native Linux verification is still required."
Write-Output "Build staging retained at $stage"
