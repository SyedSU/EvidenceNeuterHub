# Next milestone: verified releases

No updater executes in 0.1.0. Do not add simulated queues, progress or successful installation states.

Public binaries and manifests belong in SyedSU/EvidenceNeuter-Releases; private source remains in independently versioned product repositories. The Hub needs no GitHub credentials. Public HTTPS downloads must work anonymously. A release tag could be hub-v0.1.0 or genesis-v0.3.0, but these names are conventions, not existing releases.

Use a signed, versioned manifest with product ID, channel, semantic version, OS/architecture, minimum Hub version, release date, package sizes, SHA-256, signature, executable-relative path and component dependencies. Separate immutable game, runtime and model packages. Reference components by digest; reuse verified cached model/runtime files rather than fetching them for every game update. Store installation metadata separately from saves. Reject unknown schemas, unsupported combinations, expired metadata, downgrade/replay and path traversal. A hash served alongside an unsigned manifest is not authentication.

Before implementation choose a reviewed signed-metadata scheme (prefer TUF), ship trusted root keys, define rotation/revocation and expiry policies, and isolate offline root signing from build credentials. Verify signed metadata before downloading, then length, digest and artifact authenticity before extraction. Use HTTPS allowlisted release hosts and bounded redirects; no credentials in clients.

Download to per-user staging on the destination volume with byte-based progress, cancellation, bounded retries, disk-space checks and resumable downloads validated against the exact artifact identity. Never execute or install partial data. Defend against archive traversal, links, zip bombs and concurrent update races.

Stage complete versioned directories; acquire installation lock and require product exit before switching an atomic active-version pointer. Retain previous version for rollback after health checks. Keep game saves (Genesis typically APPDATA/EvidenceGenesis) outside managed files, back up before migrations, and never silently reverse incompatible save migrations. Uninstall must not remove saves/models without explicit consent. Model changes must disclose disk cost.

Hub self-update uses a separately verified bootstrapper after Hub exit; retain a previous executable and recovery path. Sign Windows installer and binaries with Authenticode and timestamp signatures before public distribution. Verify publisher identity, not merely presence of a signature. Test interrupted download/install, power loss, corrupt package, rollback, missing permissions, offline launch, signing-key rotation and save preservation on real Windows.
