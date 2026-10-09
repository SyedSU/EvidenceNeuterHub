# Genesis distribution milestone

Continue feat/hub-foundation; retain native Windows UI and existing-folder Play. Verified source commit a5cb2e9d83114e70e8e72ff444fe2095e58573e4, native workflow 37816705243 passed. Public SyedSU/EvidenceNeuter-Releases has zero releases at inspection.

Contract: bounded signed JSON envelope (RSA-PSS SHA-256 over exact payload bytes), build-pinned public key, increasing release sequence, expiry, schema/product/channel/platform/minimum-Hub-version checks, full package and per-file SHA-256/length checks. This limited single-role scheme is not TUF: key replacement requires a new trusted Hub build; no root rotation/revocation protocol is claimed. Public release requires security review and securely configured production key. No production key is generated or embedded by this task.

Separate game/runtime/model ZIPs with signed relative file maps. Download real streamed bytes, verify and extract to staging; cache by digest; reuse imported and installed files only after hashing. Reject unsafe ZIP paths, links, extra files, size mismatch and hash mismatch. No GitHub client credentials.

Fixed per-user managed root outside Genesis saves, process/installation locking, complete immutable version directories, atomic current/previous state file. Persist highest sequence in the same activation transaction. Verify before Play offline. Preserve old installation on cancel/failure. Retain one previous installation for explicit rollback, permit only equal save-schema; never automatically downgrade/migrate/restore saves. Back up existing Genesis user saves before activation without deleting/modifying originals. Health means file integrity and x64 detection, not game initialization; Windows real-game smoke is a separate check.

First version intentionally restarts canceled downloads instead of HTTP range resume; no self-update bootstrapper, automatic save migration, or unattended updater. Production trust/metadata absence must be visible, never fabricated. Packaging and manual protected-environment publish workflow are prepared but not published in this task.
