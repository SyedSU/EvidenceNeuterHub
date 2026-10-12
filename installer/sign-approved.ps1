# Production signing uses an independently provisioned certificate/HSM in the runner.
param([Parameter(Mandatory=$true)][string]$File)
$ErrorActionPreference='Stop'
if($env:HUB_SIGNING_CERT_SHA1 -notmatch '^[A-Fa-f0-9]{40}$' -or $env:HUB_PUBLISHER_SHA256 -notmatch '^[A-F0-9]{64}$'){throw 'Approved signing certificate selector and SHA256 publisher pin required'}
$certificate=Get-Item "Cert:\CurrentUser\My\$env:HUB_SIGNING_CERT_SHA1" -ErrorAction Stop
if(!$certificate.HasPrivateKey -or $certificate.NotAfter -le [DateTime]::UtcNow){throw 'Usable approved publisher certificate required'}
$fingerprint=[Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($certificate.RawData))
if($fingerprint -ne $env:HUB_PUBLISHER_SHA256){throw 'Signing certificate differs from independently approved publisher'}
$tool=Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe' | Sort-Object FullName -Descending | Select-Object -First 1
if(!$tool){throw 'Windows SDK signing tool required'}
& $tool.FullName sign /sha1 $env:HUB_SIGNING_CERT_SHA1 /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 $File
if($LASTEXITCODE){throw 'Authenticode signing failed'}
& $tool.FullName verify /pa /all $File
if($LASTEXITCODE){throw 'Windows publisher-chain verification failed'}
$signature=Get-AuthenticodeSignature $File
if($signature.Status -ne 'Valid' -or !$signature.SignerCertificate){throw 'Signed bytes failed Windows trust'}
$actual=[Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($signature.SignerCertificate.RawData))
if($actual -ne $env:HUB_PUBLISHER_SHA256){throw 'Signed publisher pin mismatch'}
