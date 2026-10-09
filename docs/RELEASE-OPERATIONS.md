# Production deployment prerequisites

No release has been published by this change. The 0.2.0 test installer has no production trust key unless the repository variable below is configured. It preserves imported-game Play and explicitly refuses release checks without trust. A successful test installation is not public distribution evidence.

## Trust design

Schema 1 is a deliberately limited single-role RSA-PSS/SHA-256 scheme, with a pinned 3072-bit-or-larger public key, signed exact UTF-8 payload bytes encoded in Base64, explicit envelope signature, product/channel/platform/minimum-Hub version, 30-day publisher expiry, monotonically increasing release sequence, package lengths/digests and per-file digests. Source commit is signed provenance metadata, not independent proof of a reproducible build. Key rotation/revocation is **not** implemented; supply a new trusted Hub installer with a replaced root key. TUF multi-role root rotation, delegated trust and metadata refresh are deferred. Obtain independent security review before public deployment. Do not treat a checksum file or a key downloaded alongside metadata as authentication.

Offline Play verifies the stored signed release and all declared files while allowing stored metadata expiry. Online install rejects expired/replayed metadata and downgrades. Rollback is explicit, verifies retained files, preserves highest sequence and only permits identical save-schema. It does not claim save semantic compatibility beyond the publisher's schema declaration. No automatic migration or restore is performed.

## Configure securely

1. Generate an RSA 3072+ signing key under organizational control. Keep a separate offline backup/root custody; never commit/export the private key to clients. The first production key must be independently fingerprint-reviewed. Example key generation should be performed in your secure signing environment, not this chat.
2. Set **repository variable** `GENESIS_RELEASE_PUBLIC_KEY_PEM` in EvidenceNeuterHub to the reviewed public SPKI PEM. Windows build embeds this public key as an assembly resource. Existing 0.1.0 and unconfigured 0.2.0 installers do not acquire trust remotely. Build and distribute the matching Hub installer before activating the product catalog.
3. Configure protected GitHub environment `genesis-release`, required human reviewers, permitted deployment branches, and no untrusted PR execution. Store **environment secrets** `GENESIS_RELEASE_SIGNING_KEY_PEM` (PKCS#8 RSA PEM) and `DISTRIBUTION_WRITE_TOKEN` (contents write access only to EvidenceNeuter-Releases). Create separate protected `genesis-build` environment with `GENESIS_SOURCE_READ_TOKEN` (read-only contents access to private EvidenceGenesis). The signing job downloads the approved build artifact and never runs upstream Genesis build scripts; the build job has no signing/distribution secrets. Prefer short-lived GitHub App installation tokens with those exact repository scopes. No tokens are embedded in the app or manifests. The secrets are build/deployment prerequisites, not client settings.
4. Provision Authenticode signing through managed signing/HSM or secure certificate storage and a trusted timestamp service for the Hub installer and game binaries. This workflow signs **metadata**, not Windows executable publisher signatures. The test installer remains unsigned; Authenticode deployment is not configured or claimed.
5. Use the prepared `publish-genesis.yml` only after review and merge makes workflow_dispatch available. It checks an explicit full source commit, uses the upstream native build/tests (including bundled AI tests), splits game/runtime/model with license notices, checks signing/public-key match, checks previous signed sequence/version, publishes the complete immutable version release first, then updates the signed catalog locator. Do not re-run a partially published tag blindly: inspect and repair draft/catalog state first. No automated invocation occurs in this task.
6. First inputs: source commit `a5cb2e9d83114e70e8e72ff444fe2095e58573e4`, Genesis `0.3.0`, sequence `1`. Upstream builder currently identifies 0.3.0; later product versions must update the reviewed source builder first. The workflow intentionally refuses version mismatches.

Anonymous locator:
`https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/genesis-stable/genesis-stable.json`

Immutable component URL:
`https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/genesis-v<VERSION>/genesis-<game|runtime|model>-<SHA256>.zip`

A dedicated locator avoids Hub releases changing GitHub's global latest release. Package hashes include ZIP bytes; unchanged component contents with normalized timestamps produce stable hashes across version releases. Clients reuse verified installed/imported component files even if an archive's digest changes. Releases must contain required licenses; no model/runtime binaries in Git.

Metadata expires after 30 days. Before expiry, publish a fresh signed release with increasing sequence/version using the current workflow; a separately reviewed metadata-only refresh process is needed to renew the same product version. Until then expired metadata stops new installations while offline installed games continue to launch.

## Files and recovery

Managed files: `%LOCALAPPDATA%/EvidenceNeuterHub/Genesis/{versions,cache,staging,active.json,save-backups}`. Each staged version has its own game/brain/model tree. Copies preserve the unchanged model download but require extra local disk space. ZIPs are cached by digest; canceled partials are deleted and downloads restart, not resume. No cache/old-version garbage collector is included yet.

Activation replaces one flushed active/previous state file after all files pass verification and saves are backed up. Failure/cancellation before activation keeps the old pointer. Crash leftovers in GUID staging folders are cleaned under the install lock on the next operation. Complete orphaned version directories are harmless and retained; power-loss durability depends on Windows/filesystem behavior and has not been hardware fault-tested.

Original imported installations and `%APPDATA%/EvidenceGenesis` saves are never deleted or overwritten. Backups can include sensitive user material and remain local. Rollback switches binaries, not saves; manual save restore requires explicit user choice outside this milestone. Installation is blocked while a process named EvidenceGenesis is running; Hub operations share a file lock. External direct launches cannot be fully synchronized but old files are never replaced in place. Integrity checks are file health, not a guarantee that the game/AI initialized.

## Deployment acceptance

Run the new Windows test installer on the target laptop. With the production key and actual public catalog, test fresh install, import, model reuse, update, cancel, rollback, offline Play, long/non-ASCII paths, model tampering, no-disk/read-only storage and saves. A headless fixture test is not a laptop/gameplay/AI/performance benchmark. Only enable a website download button after signed installer publication and genuine Windows acceptance. Website source is untouched by this change. Hub self-updates, range-resume and full TUF remain future work.
