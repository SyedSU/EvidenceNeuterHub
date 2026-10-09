# Validation record — 2026-10-09

## Executed in Linux cloud machine

- Verified remote main/development/PR refs read-only; fetched development objects without switching or modifying Genesis checkout.
- .NET SDK 8.0.425: core suite passed 14 assertions (settings default/round trip/overwrite; missing folder/executable; invalid PE; x64 detection; missing/present runtime/model; explicit launch path and working directory with shell disabled; ambiguous layout; nested extraction; wrong architecture; corrupt settings preservation; temporary-file cleanup). Executable fixtures are synthetic and never launched.
- Published a self-contained net8.0-windows win-x64 app successfully, including Windows Desktop runtime. No end-user SDK required.
- NSIS 3.08 Debian build compiles a real Windows installer. Final compilation treats warnings as errors. Windows CI uses NSIS 3.11; that compiler version has not run here.
- No source modifications to website or Genesis. No release publishing, GitHub API credential embedding, network calls in Hub, or game/save modification.

## Not verified here

Linux cannot execute the native Windows interface or installer. This package is an unsigned test build, not a release-ready download. Windows CI is supplied but has not been dispatched/pushed; its install/window/uninstall smoke checks are unrun. No actual installed Genesis package was provided on this machine; real Play, AI startup, save preservation during gameplay and version 0.3.0 binary identity remain unverified. Source README claims are not runtime proof.

## Required Windows acceptance

1. Run installer in a standard Windows 10 x64 account, confirm Start Menu and optional desktop shortcuts and Apps uninstall entry. Test chosen destination with spaces and non-ASCII characters.
2. Open each page, keyboard Tab navigation, high-DPI scaling and narrow window. Check text legibility, errors, long paths and screen-reader labels. Verify no network dependence offline.
3. Locate the genuine extracted Genesis 0.3.0 package; launch, play and close. Reopen Hub and confirm library/settings persist. Move executable and verify actionable failure with no false success. Test missing brain/model warning and real template mode separately.
4. Verify canceling folder/trust dialogs changes nothing, corrupt settings are preserved, read-only storage reports save errors, and removing the library entry never deletes game files.
5. Uninstall and verify unrelated files, user settings and Genesis saves survive. Reinstall. Confirm installer cannot target unsupported architecture.
6. Benchmark idle/launch memory, CPU and responsiveness on target Acer. Do not claim hardware suitability before measuring. Validate Authenticode-signed final build before public publication.

The Hub reports installation detection, not authenticated product/version identity or successful game initialization. Downloads and updater are intentionally unavailable and accurately labeled. Public download URL, signed manifest catalog, measured requirements and website button remain future integration work.
