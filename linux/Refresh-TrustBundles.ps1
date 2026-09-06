# Refresh public trust data only; never reads or changes the host certificate store.
$ErrorActionPreference = 'Stop'
$reportUrl = 'https://ccadb.my.salesforce-sites.com/microsoft/IncludedCACertificateReportForMSFTCSV'
$report = (Invoke-WebRequest $reportUrl).Content
$rows = @($report | ConvertFrom-Csv | Where-Object {
    $_.'Microsoft Status' -eq 'Included' -and
    ($_.'Microsoft EKUs'.Split(';') -contains 'Code Signing' -or
     $_.'Microsoft EKUs'.Split(';') -contains 'Time Stamping')
} | Sort-Object 'SHA-256 Fingerprint' -Unique)
if ($rows.Count -eq 0) { throw 'Microsoft returned no eligible roots.' }
$signers = [System.Text.StringBuilder]::new()
$timestamps = [System.Text.StringBuilder]::new()
foreach ($row in $rows) {
    $sha1 = $row.'SHA-1 Fingerprint'
    $sha256 = $row.'SHA-256 Fingerprint'
    if ($sha1 -notmatch '^[0-9A-F]{40}$' -or $sha256 -notmatch '^[0-9A-F]{64}$') {
        throw 'Invalid certificate fingerprint in the public report.'
    }
    # Microsoft's documented distribution URL is HTTP. Integrity is checked
    # against SHA-256 obtained independently in the HTTPS report above.
    $url = "http://ctldl.windowsupdate.com/msdownload/update/v3/static/trustedr/en/$sha1.crt"
    $bytes = (Invoke-WebRequest $url).Content
    if ([Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)) -ne $sha256) {
        throw "Certificate fingerprint mismatch: $sha1"
    }
    $cert = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificate($bytes)
    try {
        $pem = $cert.ExportCertificatePem() + "`n"
        if ($row.'Microsoft EKUs'.Split(';') -contains 'Code Signing') { [void]$signers.Append($pem) }
        if ($row.'Microsoft EKUs'.Split(';') -contains 'Time Stamping') { [void]$timestamps.Append($pem) }
    } finally { $cert.Dispose() }
}
# Generate only after every certificate has passed its published fingerprint.
$directory = Join-Path $PSScriptRoot 'Runtime/trust'
[void](New-Item -ItemType Directory -Force -Path $directory)
[System.IO.File]::WriteAllText((Join-Path $directory 'code-signing.pem'), $signers.ToString())
[System.IO.File]::WriteAllText((Join-Path $directory 'timestamp-signing.pem'), $timestamps.ToString())
$rows | Export-Csv (Join-Path $directory 'microsoft-roots.csv') -NoTypeInformation
Write-Output "Wrote bundles from $($rows.Count) fingerprint-checked public roots."
