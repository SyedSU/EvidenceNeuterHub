# Evidence Neuter Hub

Windows desktop foundation, version 0.1.0 preview. Native Windows Forms, no embedded browser, AI API, account or developer tools required for end users. Six products remain independently versioned; this milestone connects an already-installed Genesis game only.

## Use

Install with `EvidenceNeuterHub-Setup-0.1.0.exe`. The installer creates a Start Menu shortcut and offers an optional desktop shortcut. Select **Locate Genesis folder**, choose the extracted folder containing `EvidenceGenesis.exe`, review the trust prompt, and select **Play Genesis**. Offline library access requires no manifest server. The Hub rechecks the executable before launch. Executable detection is not publisher authentication. Game startup and AI readiness are not inferred from process creation.

The other five products are marked **In development**. Downloads explains that downloads/updates are unavailable. No artificial progress or installed products. Settings are stored in `%LOCALAPPDATA%/EvidenceNeuterHub/settings.json`; corrupt settings are preserved and writes disabled until repaired. Removing a library entry and uninstalling the Hub do not delete Genesis files or saves.

## Develop and build

Windows: .NET SDK 8 and NSIS 3.11 are build-only dependencies. Run `./installer/build.ps1` in PowerShell at the repository root. Output: `artifacts/EvidenceNeuterHub-Setup-0.1.0.exe` and SHA-256 record. The app includes its .NET runtime. CI builds a downloadable test artifact without publishing a release. Tests: `dotnet run --project tests/CoreTests.csproj -c Release`. Core tests also run on Linux; Windows compilation uses `dotnet publish src/Hub.csproj -c Release -r win-x64 --self-contained true -o artifacts/app`.

See [scope and plan](docs/PLAN.md), [updater milestone](docs/UPDATER.md), [website handoff](docs/WEBSITE-INTEGRATION.md) and [validation](docs/VALIDATION.md). No website changes or public release are made by this task.
