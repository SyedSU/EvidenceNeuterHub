# Real-release operator acceptance preparation

Preparation does not enable publication or approve a release. Existing private milestone evidence is PR2 commit0e7033b, Windows run38149240893. The user reports the laptop checks passed; this is **user-reported**, not an independently observed new production-signed test. Record device/OS/DPI, versions, operator/date and evidence before counting it toward the public gate.

## Credential and trust inventory (2026-10-11)

Hub and distribution protected environments: API200, count0; required protected environments are absent. Repository variable/action-secret APIs:403, so existing saved keys/pins are **unknown**, not proved absent. Cloud runtime signing bindings are empty: no actual signer is available to this task. Public distribution releases count0. This task does not create environments or configure keys, certificates or credentials.

Public build inputs are GENESIS_RELEASE_PUBLIC_KEY_PEM, CATALOG_RELEASE_PUBLIC_KEY_PEM, CATALOG_URL, HUB_RELEASE_PUBLIC_KEY_PEM, HUB_PUBLISHER_SHA256. RSA public keys must be independently reviewed SPKI PEM (>=3072 bits). HUB_PUBLISHER_SHA256 is uppercase SHA256 over the signer's certificate DER bytes; a Windows SHA1 thumbprint is the wrong value. Signer/private metadata keys, HSM/service access, timestamp service, protected release credentials and out-of-band pin approval are separate operator prerequisites. Never pass private keys to the collector or installed client, commit them, or expose values in CI diagnostics.

## Build and sign in the correct order

The existing installer/build.ps1 is the unsigned private-test build pipeline, **not** a production signing pipeline. It packages immediately after publishing. Do not sign an installer built from an unsigned payload and assume its bundled bootstrapper was signed.

In a protected reviewed Windows build, freeze source SHA and genuine version first; run the same core/updater checks and publish Hub/bootstrap with the exact public build bindings described in RELEASE-OPERATIONS.md. Then:

1. Sign the final published EvidenceNeuterHub.exe and EvidenceHubBootstrap.exe with the independently approved managed Authenticode signer and trusted timestamp service. Any other publisher executables subject to the distribution policy need the same reviewed treatment. Verify `signtool verify /pa /all /v` and independently compare the actual certificate SHA256 with the approved pin. Do not use a self-signed/test certificate as production acceptance.
2. Copy the **signed** standalone bootstrap EXE into the final app directory. Generate packaged-uninstall.nsh from that final payload as build.ps1 does. Compile NSIS with warnings as errors. No executable modification is allowed after signing except copying the exact bytes.
3. Sign the resulting NSIS installer with the same approved publisher and timestamp service. Verify Authenticode again. Compute final installer size/SHA256 **after** signing; these final bytes are what the Hub manifest/catalog must bind. Signing changes hashes.
4. Have protected metadata signing tooling sign the Hub installer manifest and Genesis component manifests with their separate approved RSA keys. Use monotonic sequences, unexpired metadata, genuine source commits, exact platform/minimum requirements, immutable URLs and correct save epoch. Sign the catalog only after both product metadata bindings and public anonymous artifact availability are verified.
5. Collect and independently review acceptance evidence before approving catalog gate fields. No broad write credential or private signing key belongs in pull-request build jobs. This checklist grants no publication authorization.

The existing unsigned test Hub0.3 build with missing embedded pins cannot acquire trust merely by receiving signed metadata. First rebuild a genuine reviewed baseline with the approved public pins in **both Hub and bootstrap**, verify its publisher/build identity, and manually install it through the trusted operator path. Only then can a genuine strictly newer, separately reviewed source/version build with the approved pins demonstrate self-update. Do not invent0.3.1 or relabel the existing artifact.

Production build identities must differ: self-update deliberately rejects a same-version installer even if re-signed or given a higher sequence. A real Hub baseline and a genuine strictly newer candidate are required; do not relabel0.3.0 bytes as a later version. The tool uses the current0.3 client compatibility policy; future baselines need the matching reviewed collector/client source. Genuine Genesis update/rollback needs two releases with **the same saveSchema4**. Genesis0.3.0(epoch1)→0.4.0(epoch4) is a narrow forward-only reviewed transition; it cannot demonstrate compatible rollback.

## Read-only evidence collector

Build `dotnet build tools/acceptance/Acceptance.csproj -c Release`, then run:

```
dotnet tools/acceptance/bin/Release/net10.0/Acceptance.dll snapshot inputs.json new-evidence-directory
```

Use a dedicated disposable Windows account and dedicated managed/save roots containing deliberately created acceptance saves. Do not run destructive failure/cancel/rollback exercises against personal saves. Stop game writes before hashing. The collector does not lock the client or provide an atomic filesystem snapshot: pause activity, collect before/after at quiescent boundaries, and repeat if files changed during measurement. It opens files read-only, rejects reparse links, refuses existing/nested output destinations, writes hashes/lengths/process identity only, and never copies save contents or performs install/launch/approval. Reports still contain local paths/process identities; review/redact personal identifiers before sharing. Keep outputs outside Git.

Minimal inputs for a checkpoint:

```json
{"checkpoint":"before-fresh-install","roots":["C:\\acceptance\\EvidenceNeuterHub\\Genesis","C:\\acceptance\\EvidenceGenesis"]}
```

Roots must already exist. Snapshot-only mode records `metadataVerified:false`. For **actual signed** metadata verification add catalogEnvelope/catalogKey and genesisEnvelope/genesisKey paths, plus accepted catalogSequence/genesisSequence high-water marks. These are independently approved public-key files, not keys downloaded beside a release. The collector verifies signature/expiry/replay and approved catalog binding. For a real newer Hub candidate also supply hubEnvelope/hubKey/hubSequence, publisherSha256, and the actual installer path; Windows Authenticode/pinned publisher and signed size/hash are checked using the same client code. No CLI input changes installed Hub/bootstrap trust. Never lower high-water marks to manufacture replay acceptance. A tool snapshot is still not a completed update claim.

```json
{
  "checkpoint":"verified-candidate-before-handoff",
  "roots":["C:\\acceptance\\EvidenceNeuterHub\\Genesis","C:\\acceptance\\EvidenceGenesis"],
  "catalogEnvelope":"C:\\acceptance\\catalog.signed.json",
  "catalogKey":"C:\\approved-public-keys\\catalog.pem",
  "catalogSequence":1,
  "genesisEnvelope":"C:\\acceptance\\genesis.signed.json",
  "genesisKey":"C:\\approved-public-keys\\genesis.pem",
  "genesisSequence":1,
  "hubEnvelope":"C:\\acceptance\\hub.signed.json",
  "hubKey":"C:\\approved-public-keys\\hub.pem",
  "hubSequence":1,
  "publisherSha256":"REPLACE_WITH_INDEPENDENTLY_APPROVED_64_HEX_SHA256",
  "installer":"C:\\acceptance\\actual-signed-setup.exe"
}
```

This intentionally incomplete example fails closed; numeric marks must be replaced with the actual recorded baseline marks. Record original public URLs/envelope SHA256, build source/version, approval provenance and public-key fingerprints separately. The report records declared package bytes/hashes and measured local bytes; it **does not** measure network transfer or prove cache reuse from matching hashes alone. Capture actual download byte counters/speed and server/network logs where available, without secret tokens. Do not count synthetic fixtures as production evidence.

## Operator scenario record (each requires actual observations)

| Scenario | Required before/after evidence and outcome |
|---|---|
| Fresh public managed install | Approved signed catalog/product manifests, anonymous immutable URLs, measured package lengths/hash, blank dedicated root, actual download counters, verified version/readiness and real game launch |
| Unchanged component reuse | Same-schema genuine newer Genesis with identical model/runtime metadata; local before/after hashes plus actual zero network fetch for reused components; game component genuinely changes |
| Cancel/interruption | Real nonzero transfer, user cancel or controlled disposable interruption, actionable UI, no activation pointer/sequence change, partial cleanup and original save hashes unchanged; successful retry recorded separately |
| Save preservation | Quiescent test save tree hashes before update/after update; backup existence/hashes; actual Continue behavior. Explicit legacy import is a separate player action, originals retained |
| Compatible rollback | Two genuine sameSchema4 versions; explicit rollback, installed version changes, highest sequence remains, save hashes unchanged; offline Play after expired online metadata remains separately verified |
| Locks/errors | Game actually running blocks update/rollback/cleanup; real low-disk/read-only conditions only on disposable roots; actionable errors, no false completion |
| Approved-publisher self-update | Genuine older/newer Hub identities; baseline/candidate embedded trust pin evidence, actual installer SHA256/length/Authenticode, stage manifest/catalog, originating PID/start time/path, copied bootstrap identity, Hub exit before installer, actual newer ProductVersion and sequence after wizard completion |
| Self-update cancellation/rejection | Cancel handoff/installer in disposable account; no later unintended launch, no false success, error marker if applicable. Tampered/wrong publisher/expired/replayed candidate must fail closed; do not modify public production assets to run negative tests |

Record operator/date, checkpoint paths, evidence file SHA256, screenshots/video, expected/observed outcome and PASS/FAIL/UNRUN for every row. Preserve failure evidence. Installer cancellation, repair and power-loss durability remain separate: current Hub bootstrap is not transactional rollback. Rotation/revocation/threshold root metadata and TUF remain unimplemented. A maintained framework still needs a separate reviewed trust/format migration; no such integration is claimed.

## Phase1 signed Genesis core before the public catalog gate

Public managed acceptance must precede gate approval; do not assert Windows acceptance/publicDownloads merely to make the normal UI install a candidate. Phase1 publishes actual signed Genesis metadata and immutable packages through a separately authorized protected distribution operation while the public catalog remains blocked. The operator-only commands below call the **same InstallManager and production DownloadClient** directly using an independently approved public key. They do not use/change catalog flags, modify installed Hub trust, launch a game or approve a release. Phase2 catalog activation follows independent review of real Phase1 evidence and the remaining UI/game/publisher acceptance. This closes the managed-core testing dependency without claiming the normal public UI was already enabled.

Commands: `genesis-install`, `genesis-cancel`, `genesis-rollback`, followed by an inputs JSON path and a **new** external evidence directory. No import roots or default/personal save paths are accepted. The workspace must be a GUID child of the OS temporary `EvidenceNeuterAcceptance` directory, contain the exact marker below, and have an explicitly created `saves` child. The harness fixes its managed root to `workspace/managed` and backup source to `workspace/saves`. Place only disposable acceptance save fixtures there. Actual Godot Continue/play with its user directory still requires the dedicated Windows test account and separate evidence; the harness never launches a game or claims it did.

Operator setup in PowerShell (deliberate setup, not an approval):

```powershell
$caseRoot = Join-Path ([IO.Path]::GetTempPath()) ('EvidenceNeuterAcceptance\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory (Join-Path $caseRoot 'saves') -Force
Set-Content (Join-Path $caseRoot '.evidence-acceptance-isolated') 'DISPOSABLE GENESIS ACCEPTANCE v1'
```

Create config with the actual generated workspace path, independently approved public-key path and actual signed manifest downloaded anonymously from its intended public metadata URL. Keep those original URL/download/hash records as evidence. Example values below are placeholders and do not constitute a signed release:

```json
{"workspace":"REPLACE_WITH_GENERATED_GUID_WORKSPACE","genesisKey":"REPLACE_WITH_APPROVED_PUBLIC_KEY_FILE","genesisEnvelope":"REPLACE_WITH_REAL_SIGNED_MANIFEST_FILE","checkpoint":"fresh-public-signed-core","cancelAtBytes":0}
```

Run `dotnet Acceptance.dll genesis-install inputs.json NEW_EXTERNAL_EVIDENCE_DIRECTORY` for fresh install or a genuine newer sameSchema4 release. Use the built DLL's full path. Signed size/hash/expiry/replay/version/URL/archive/file policy and actual save backup/activation remain the production code's checks. The harness requires saveSchema4 for this milestone. It records the independent public-key SPKI SHA256, envelope hash, declared package URLs/bytes/hashes, before/after state and save hashes, and synchronous measured transfer/reuse events in `transfers.jsonl`. Inspect the actual counters and reuse events; a matching model hash alone does not establish zero network transfer.

For cancellation, set a positive `cancelAtBytes` and use `genesis-cancel` on a fresh workspace or genuinely changed package requiring transfer. Cancellation triggers only from measured `Downloading` bytes. Progress is sampled by the production stream (roughly100ms or package completion), so the observed cancellation may overshoot the requested threshold. A successful cancellation classification requires the threshold actually reached, an observed cancellation, unchanged activation/high-water state, unchanged saves and no partial files. If reuse prevents transfer or the threshold is too high, activation is deliberately blocked and the run fails; this is **not** cancellation acceptance. Preserve that failure report.

For rollback, use `genesis-rollback` after two different genuine sameSchema4 versions have actually been installed in this same workspace. It verifies the previous stored signature/files, changes the pointer through the production rollback code, preserves highest sequence and saves, and verifies the resulting current files. The new-manifest input may be omitted for rollback. Cross-epoch0.3→0.4 rollback cannot substitute for this scenario.

`operation.json` outcomes distinguish completed core operation, measured cancellation without activation, and failure. No report automatically approves Windows/game/UI/publisher acceptance or release gates. Only isolated synthetic tests inject a fake HTTP handler into the shared harness method; the CLI always creates the production HTTPS DownloadClient, with no transport/trust bypass options. No real operator command was run during preparation because approved keys and public assets are unavailable.
