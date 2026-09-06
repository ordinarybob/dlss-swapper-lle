# Code-signing and timestamp roots

Snapshot generated 2026-09-06 from Microsoft's public trusted-root program
[CCADB report](https://ccadb.my.salesforce-sites.com/microsoft/IncludedCACertificateReportForMSFTCSV).
Only rows marked **Included** are eligible. Code Signing and Time Stamping EKUs
select the two separate bundles. Disabled roots and TLS-only roots are excluded.

Each downloaded certificate's SHA-256 was checked against the HTTPS report.
`microsoft-roots.csv` records the selected identities, purposes and fingerprints.
`linux/Refresh-TrustBundles.ps1` reproduces this operation using public data only;
it never exports personal certificates or changes the host trust store.

These are static trust snapshots for osslsigncode chain checking, not a copy of
Windows' complete WinVerifyTrust policy or an online Windows root update service.
Native signed, unsigned, modified and timestamped DLL verification remains to be
tested. Bundling the roots is not evidence that those tests passed.
