# Hub foundation scope

Six independent products: Genesis (teachable 2.5D companion), Lab (local voice/audio), Bot (wake-word assistant), Academy (interactive learning), Flow (automation), Council (AI role workspace). Website and Hub are separate infrastructure. Five future products are explicitly in development. Genesis has a native development preview, not a verified public release.

Verified 2026-10-09: Genesis main 21186bab78f091117ae388ffd98b38ce0cd362cf contains a README. feat/native-game and PR #2 head both a5cb2e9d83114e70e8e72ff444fe2095e58573e4. Branch README describes v0.3.0, Godot 4.7.2, llama.cpp b11429 and Qwen2.5 0.5B. This verifies source claims, not an installed or published package. Hub begins with README only; no existing builds or tests. No applicable AGENTS.md in Hub or workspace.

Implementation plan, recorded before source edits:
1. Native .NET 8 Windows Forms, self-contained Windows x64 build, restrained cream/forest/brass visual system. No browser engine, AI service, or development tools for players.
2. Offline catalog/navigation; explicit folder selection, bounded known-executable detection, PE x64 validation, launch without shell and with correct working directory. Never infer ownership or installation from catalog entries.
3. Atomic user-local settings; preserve malformed files; actionable errors and revalidate before launch. No changes to Genesis saves or binaries.
4. Dependency-free core tests; Windows installer using NSIS with Start Menu and optional desktop shortcuts; Windows CI produces private test artifacts, no release publishing.
5. Document secure updater architecture and website integration only. No live download button, public publication, website edits, credential embedding or simulated progress.

Required validation: core tests, Windows-target compilation, installer compilation and artifact integrity. Windows UI, installation/uninstallation and real Genesis launch need Windows execution; distinguish unverified checks. Optional: performance benchmark on target Acer and signed installer release.
