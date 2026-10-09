# Evidence Neuter Hub

Native Windows desktop Hub, **0.2.0 preview**. End users need no developer tools, account, paid AI API, or GitHub credentials.

Discover, My Library, Downloads and Settings retain the 0.1.0 interface. Existing installations can still be located and played offline. Five future apps remain **In development**. Genesis installation/update now uses signed metadata and separate game/runtime/model packages, with byte progress, cancellation, verified file reuse, staging, saved version detection, save backups and explicit rollback.

## Availability and trust

No public Genesis releases currently exist. Production installation requires the release public key pinned in the Hub build and the matching signed public catalog. Without a configured key the Hub clearly refuses online installation rather than trusting remote checksums/keys. Existing imported games still launch and are explicitly marked version unverified. An unsigned local SHA256SUMS file is not authentication.

## Use

Install `EvidenceNeuterHub-Setup-0.2.0.exe`. Start Menu and optional desktop shortcuts are supported. **Locate/Import Genesis folder** selects a folder with `EvidenceGenesis.exe` and preserves the original files. **Downloads → Check Genesis releases** verifies deployed signed metadata. **Install/update** reuses matching imported or managed components, downloads only missing components, stages a complete version and switches the active pointer after verification and save backup. **Play** verifies managed files offline before launch. **Roll back** selects the retained previous same-save-schema version; it never restores or deletes your saves.

Game files and save backups: `%LOCALAPPDATA%/EvidenceNeuterHub/Genesis`. Genesis saves remain in `%APPDATA%/EvidenceGenesis`. Preferences remain in `%LOCALAPPDATA%/EvidenceNeuterHub/settings.json`. Corrupt preferences are preserved. Updates are blocked while Genesis runs. Close the game and wait for cancellation cleanup before closing Hub. Canceled transfers restart; copies of verified unchanged models require staging disk space but no repeated model download. Hub self-update and save-schema migrations are not implemented.

## Build/test

Build dependencies only: .NET SDK 8 and NSIS 3.11. Windows: `./installer/build.ps1`. Optional public trust variable: `GENESIS_RELEASE_PUBLIC_KEY_PEM` (public PEM only). It is embedded at build time, never accepted from downloaded metadata. CI creates a private downloadable Windows test installer and runs install/window/uninstall smoke checks.

`dotnet run --project tests/CoreTests.csproj -c Release`

`dotnet run --project tests/UpdaterTests/UpdaterTests.csproj -c Release`

`dotnet build tools/release/ReleaseTool.csproj -c Release`

Updater tests use ephemeral signing keys and synthetic test packages. Their test version numbers are not published Genesis releases. Optional real-package validation is documented in the validation record. Test installers remain unsigned unless a separate Authenticode signing service is configured.

See [release operations and secure configuration](docs/RELEASE-OPERATIONS.md), [implementation plan](docs/INSTALLER-PLAN.md), [validation](docs/VALIDATION.md), [website handoff](docs/WEBSITE-INTEGRATION.md), and [updater roadmap](docs/UPDATER.md). Website source is unchanged; no public release is published by this change.
