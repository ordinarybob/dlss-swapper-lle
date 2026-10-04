# Code-signing and timestamp roots

Snapshot generated 2026-09-06 from Microsoft's public trusted-root program
[CCADB report](https://ccadb.my.salesforce-sites.com/microsoft/IncludedCACertificateReportForMSFTCSV).
Only rows marked **Included** are eligible. Code Signing and Time Stamping EKUs
select the two separate bundles. Disabled roots and TLS-only roots are excluded.

Each downloaded certificate's SHA-256 was checked against the HTTPS report.
`microsoft-roots.csv` records the selected identities, purposes and fingerprints.
Regenerate the bundles with `linux/Refresh-TrustBundles.ps1`.

osslsigncode uses these static bundles for chain checking; they do not reproduce
Windows' complete WinVerifyTrust policy.
