param(
    [string]$Dotnet,
    [string]$OutputPath,
    [string]$SourceRevisionId
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$Dotnet) {
    $bundledDotnet = Join-Path $PSScriptRoot '../../.dotnet-sdk/dotnet.exe'
    $Dotnet = if (Test-Path -LiteralPath $bundledDotnet) { $bundledDotnet } else { 'dotnet' }
}
$output = if ($OutputPath) { [IO.Path]::GetFullPath($OutputPath) } else {
    [IO.Path]::GetFullPath((Join-Path $repo '../../outputs/DLSS.Swapper-LLE-1.0.0-linux-x64.tar.gz'))
}
$stageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'obj'))
$stage = Join-Path $stageRoot ('package-' + [Guid]::NewGuid().ToString('N'))
$temporary = $output + '.tmp'
if (Test-Path -LiteralPath $temporary) { throw "Existing package operation or unfinished archive: $temporary" }
[void](New-Item -ItemType Directory -Path $stage)
try {
    foreach ($app in @(@('Cli', 'cli'), @('Gui', 'app'))) {
        $project = Join-Path $PSScriptRoot "DlssSwapper.Linux.$($app[0])/DlssSwapper.Linux.$($app[0]).csproj"
        [string[]]$revisionArguments = @()
        if ($SourceRevisionId) { $revisionArguments = @("-p:SourceRevisionId=$SourceRevisionId") }
        & $Dotnet publish $project -c Release -r linux-x64 --self-contained true --no-restore '-p:LlePackageFormat=tar.gz' @revisionArguments -o (Join-Path $stage $app[1]) -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($app[0])" }
    }
    $package = Join-Path $stage 'app'
    # Keep one shared runtime. Never silently choose between different dependencies.
    $duplicates = 0
    $duplicateBytes = 0L
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $stage 'cli') -Recurse -File) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link: $($file.FullName)" }
        $name = [IO.Path]::GetRelativePath((Join-Path $stage 'cli'), $file.FullName)
        $target = Join-Path $package $name
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
                throw "Conflicting CLI and GUI dependency: $name"
            }
            $duplicates++
            $duplicateBytes += $file.Length
        } else {
            [void](New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent))
            Move-Item -LiteralPath $file.FullName -Destination $target
        }
    }
    Write-Output "Shared $duplicates identical files ($duplicateBytes bytes)."
    foreach ($asset in @('tools/osslsigncode', 'tools/lib/libcrypto.so.3', 'tools/lib/libssl.so.3',
            'tools/lib/libz.so.1', 'tools/lib/libzstd.so.1', 'trust/code-signing.pem', 'trust/timestamp-signing.pem',
            'dlss-swapper-linux', 'dlss-swapper-linux-gui')) {
        if (!(Test-Path -LiteralPath (Join-Path $package $asset) -PathType Leaf)) { throw "Missing $asset" }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $package 'README.md')
    # Notices cover the entire archive, including the CLI/runtime.
    if (!(Test-Path -LiteralPath (Join-Path $package 'Acknowledgements/osslsigncode/COPYING.txt'))) {
        throw 'Verifier license was not published.'
    }
    [void](New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent))
    $stream = [IO.File]::Create($temporary)
    $gzip = [IO.Compression.GZipStream]::new($stream, [IO.Compression.CompressionLevel]::Optimal)
    $writer = [System.Formats.Tar.TarWriter]::new($gzip, $true)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $package -Recurse | Sort-Object FullName) {
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link: $($file.FullName)" }
            $name = [IO.Path]::GetRelativePath($package, $file.FullName).Replace('\', '/')
            $type = if ($file.PSIsContainer) { [System.Formats.Tar.TarEntryType]::Directory } else { [System.Formats.Tar.TarEntryType]::RegularFile }
            $entry = [System.Formats.Tar.PaxTarEntry]::new($type, $name)
            $executable = $file.PSIsContainer -or $name -match '^(dlss-swapper-linux|dlss-swapper-linux-gui|tools/osslsigncode)$'
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
} finally {
    # This invocation owns only its new staging directory and temporary archive.
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if ([IO.Path]::GetDirectoryName($resolvedStage) -ne $stageRoot -or
        [IO.Path]::GetFileName($resolvedStage) -notmatch '^package-[0-9a-f]{32}$') {
        throw "Refusing cleanup outside package staging: $resolvedStage"
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}
