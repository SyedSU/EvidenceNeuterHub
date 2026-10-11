# Hub0.3 milestone validation — 2026-10-10

Current branch Linux validation: **16 core assertions and101 updater assertions pass**; two actual running-game process assertions are explicitly Windows-only and run in CI after building a test-only EvidenceGenesis apphost. Hub and standalone bootstrapper cross-target builds pass with zero warnings/errors. `git diff --check` passes. No Linux visual/Authenticode/laptop acceptance is claimed.

New coverage includes signed shared catalog parsing/required/unknown fields, blocked/unsigned/expired catalog authorization, safe root-relative/immutable asset URLs, manifest binding and invalidated release candidate, separate Hub key/publisher/expiry/replay/tamper/compatibility failures, unsigned/tampered installer rejection, actual measured speed, fresh saveSchema4 install/reuse, exact reviewed0.3.0 epoch1→0.4.0 epoch4 forward update with save preservation/backups/model reuse, forbidden cross-epoch rollback, low-disk policy, interrupted staging recovery, nested import reuse, lock-aware storage cleanup/malicious state and final activation authorization failure preserving pointer/sequence. A cancellation marker prevents a canceled bootstrap handoff from later installing on an unrelated Hub exit; an actual marker I/O failure regression verifies termination/wait of the owned child. ZIP fixtures use normalized timestamps to prevent wall-clock archive-byte drift.

Windows workflow builds a self-contained private installer/bootstrapper, exercises actual process-name locking, install/window/navigation/resize/focus/uninstall preservation, checks installer refusal while Hub owns its named installation mutex, and uploads four page screenshots plus a resized capture and test logs. Run/artifact evidence is recorded in the review PR after CI completes. Windows screenshots come from Windows; genuine Genesis workshop artwork is separately a Linux Godot4.7.2 llvmpipe/template development capture, not Windows gameplay evidence.

Windows reviewed commit c4c0909 passed build, native installer/bootstrap smoke and evidence upload in run38038874248. The added interactive Finish journey initially timed out in runs38039450562/38039720495: observed UIAutomation exposed the NSIS dialog but zero Button descendants. The smoke now reads the actual native IDOK caption/enabled state and invokes its Windows button click; it still requires the real Finish action to launch Hub and refuses uninstall while that Hub runs. Run38039996620 subsequently passed the real interactive Finish launch, then found guarded uninstall abort returned the default success exit code. Guarded aborts now explicitly set error level1 and the smoke still requires the installed executable to remain present. Final run evidence is recorded in PR2; these diagnostic failures are not product-success claims. Artifact downloads are denied by cloud storage-domain policy, so local visual inspection is unavailable.

Production metadata/publisher bindings are not supplied locally; repository configuration inspection was denied. No public assets, production-signed happy path, target laptop gameplay/model benchmark, high-DPI device acceptance, power-loss durability, TUF, root rotation/revocation, transactional Hub rollback or automatic save rewrite is claimed. Review [operations](RELEASE-OPERATIONS.md) before any public gate/publication.

## Historical0.2 foundation evidence

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

## Genesis updater milestone — Hub 0.2.0

Verified source branch remains a5cb2e9d83114e70e8e72ff444fe2095e58573e4. Upstream native Windows workflow 37816705243 succeeded and its 0.3.0 artifact exists (not a public release). Distribution repository is public and has zero published releases at inspection.

Executed on Linux: existing 14 foundation assertions and 50 updater assertions passed. Updater coverage includes cryptographic signature/key/schema/platform/version/expiry validation, safe URLs and ZIP paths, real filesystem component staging/activation, byte progress, cancellation/cleanup, archive and file corruption, concurrent locks, anti-replay/downgrade, unchanged runtime/model reuse, verified import without downloads, save backups/preservation, rollback and offline tampering. Real isolated loopback HTTPS transfers verify transport completion, cancellation mid-transfer and truncated-response rejection; certificate trust is pinned to an ephemeral test certificate only in the test project. Fixtures contain synthetic test executables and ephemeral metadata keys, not published Genesis packages.

Windows-target publish and release-tool compilation passed with zero warnings. Workflow YAML parsed successfully; actual GitHub workflow execution is recorded in the PR/run links after push. The publishing workflow has not been dispatched and no production signing keys are configured by this task.

Attempt to fetch the real native artifact was blocked by the environment egress policy for `productionresultssa10.blob.core.windows.net`. Therefore actual Genesis package import/launch, genuine game/model execution through the new Hub, real public GitHub component download, target-laptop performance, Authenticode signing and power-loss hardware fault injection remain unverified. These are not represented as successful installs. Prior upstream native tests do not establish the new Hub's genuine game integration.

The production trust key is intentionally absent until securely configured; unconfigured installers keep imported Play and refuse release installation. Windows CI uses synthetic package transactions plus native Hub installer/window/uninstall smoke. Windows 10 laptop acceptance and an actual signed public release are deployment prerequisites. Range-resume, TUF key rotation/revocation, cache garbage collection, automatic save migration and Hub self-updates are explicitly deferred.
