# Production acceptance preparation implementation plan

> Execute directly with superpowers:executing-plans; unattended scope already authorized.

**Goal:** Prepare credential-independent, read-only real-release evidence collection and an exact signing/acceptance checklist.
**Architecture:** A separate .NET console tool reuses metadata verification without modifying installed Hub trust. It creates new evidence directories only, hashes files without copying personal save contents, and never installs, launches or approves releases.
**Tech stack:** .NET8, existing RSA/Authenticode validators, manual Windows operator workflow.
**Spec:** Parent production-preparation task, 2026-10-11.

## Constraints and review focus

No invented versions/certificates/pins/public assets; no changed client behavior. Keep private keys/saves out of Git. Reject linked inputs/output, existing report destinations and private key material. Record missing metadata as unverified, never acceptance. Require distinct genuine Hub versions and same-epoch Genesis update pair for rollback. User laptop result is user-reported.

- [x] Add collector with rejecting-link/existing-output tests and synthetic read-only snapshot test.
- [x] Add separate signature-verification mode using independent public keys and approved catalog/product binding; prove unsigned fixture rejected.
- [x] Document signing order, credential inventory, separate snapshot checkpoints and manual evidence requirements.
- [x] Run collector checks and existing regression suites, update PR2; no publication/merge.
