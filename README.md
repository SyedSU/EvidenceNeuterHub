# Evidence Neuter Hub

Native WinForms desktop Hub, **0.3.0 development preview**. No account, developer tools or paid AI API is required for imported/offline Play.

Discover features a genuine Genesis workshop capture, product information and availability from the shared schema1 catalog. My Library distinguishes signed managed versions from unverified imports and shows location/runtime/model readiness. Downloads reports actual bytes and measured speed, cancellation and actionable failures. Settings includes dark/light forest/cream/brass appearance, reduced motion, installation location for new managed installs and generated cache/staging cleanup. Ctrl+1–4 navigates the four main pages; Tab moves through native controls. High DPI uses PerMonitorV2; real laptop acceptance remains required.

## Availability and trust

The bundled catalog is explicitly **unsigned development data** and cannot authorize downloads. Online refresh requires separately build-pinned catalog public key and URL. An approved authenticated catalog must match the separately verified Genesis component manifest or Hub installer manifest. Missing trust configuration fails closed. Repository production configuration was not inspected successfully; local builds have no production key/publisher supplied.

Genesis keeps the exact 0.2 schema1 signed package format and separate game/runtime/model transfers, verified reuse, staged activation, save backups, offline verification and same-save-schema rollback. New Genesis 0.4 profiles use saveSchema4. The reviewed managed Genesis0.3.0 epoch1 →0.4.0 epoch4 transition backs up the entire save tree. In Genesis select **Import legacy workshop (keep original)** to create a separate profile; Continue does not automatically import old state. Other cross-epoch transitions and rollback remain blocked. Imported older games remain playable, explicitly unverified. Genesis performs read-only legacy profile imports; Hub does not rewrite them.

Hub self-update stages a signed, hashed, known-Authenticode-publisher installer. On explicit user confirmation, a **separate copied bootstrapper** authenticates the exact originating Hub process, waits for it to exit, reverifies metadata/installer/Windows trust and opens the installer wizard. The Hub never replaces its own running executable. The bootstrapper refuses missing production key/publisher, expired/replayed/incompatible metadata, another running installed Hub, signature/byte/publisher failure and an installed version mismatch. It never claims installation completion merely because staging or process launch succeeded.

## Use

Install the private test artifact `EvidenceNeuterHub-Setup-0.3.0.exe`. **My Library → Locate Genesis folder** connects an extracted installation without altering it. **Settings → Refresh signed product catalog** uses configured production trust; **Downloads → Check Genesis releases** requires an approved signed catalog. Play verifies managed files again offline; imports remain unverified. Rollback preserves personal saves and the highest accepted sequence.

Default managed game files: `%LOCALAPPDATA%/EvidenceNeuterHub/Genesis`. Preferences: `%LOCALAPPDATA%/EvidenceNeuterHub/settings.json`. Genesis profiles: `%APPDATA%/EvidenceGenesis`; whole-tree backups remain local under managed `save-backups`. Hub update stages: `%LOCALAPPDATA%/EvidenceNeuterHub/HubUpdates`. Settings corruption preserves the original file and disables preference writes. Storage cleanup never removes active/previous versions, original imports or personal saves.

## Build and tests

Windows build: .NET SDK8 and NSIS3.11, then `./installer/build.ps1`. Build-time public configuration only: `GENESIS_RELEASE_PUBLIC_KEY_PEM`, `CATALOG_RELEASE_PUBLIC_KEY_PEM`, `CATALOG_URL`, `HUB_RELEASE_PUBLIC_KEY_PEM`, `HUB_PUBLISHER_SHA256`. No remote key or publisher certificate pin is trusted. Signing private keys must remain in protected signing infrastructure.

```
dotnet run --project tests/CoreTests.csproj -c Release
dotnet run --project tests/UpdaterTests/UpdaterTests.csproj -c Release
dotnet build src/Hub.csproj -c Release
dotnet build tools/bootstrap/Bootstrap.csproj -c Release
```

Tests use ephemeral RSA keys, synthetic packages and a real loopback TLS transfer. They are not published releases or gameplay benchmarks. CI produces a private Windows installer, test logs and real screenshots of four pages plus resize; these are evidence of native startup/navigation, not proof of high-DPI laptop, production publisher or real gameplay acceptance. See [operations](docs/RELEASE-OPERATIONS.md) and [validation](docs/VALIDATION.md). Existing PR1 remains the foundation dependency; no merge, public release or deployment is performed.
