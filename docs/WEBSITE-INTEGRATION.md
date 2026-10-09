# Website handoff — no website edits in this task

The website was included in environment onboarding, but this implementation request explicitly prohibits website changes. This document is the integration handoff. No download button may be enabled until the real installer is built, tested on Windows and publicly published.

Future installer URL convention:
https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/hub-v<VERSION>/EvidenceNeuterHub-Setup-<VERSION>.exe

Future product manifest convention:
https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/catalog-v<VERSION>/catalog.json

These are proposed formats, not live downloads. Avoid mutable latest URLs in signed metadata. CI test artifacts require repository access and are not public release links.

Hub metadata: id evidence-neuter-hub; version 0.1.0-preview; Windows 10 x64 or Windows 11 x64; native .NET self-contained runtime; no account, AI API or developer tools. Runtime performance and minimum RAM/storage must be benchmarked on the Acer before publishing claims. Show actual installer byte size and SHA-256 from final signed artifact, not estimates. Published installer size changes with signing/build outputs.

Product metadata:
| ID | Display name | Description | Current status |
|---|---|---|---|
| genesis | Evidence Genesis | Teachable 2.5D AI companion game | Development preview; existing-folder integration |
| lab | Evidence Lab | Local voice cloning, speech and audio studio | In development |
| bot | Evidence Bot | Local wake-word device assistant | In development |
| academy | Evidence Academy | Interactive AI learning | In development |
| flow | Evidence Flow | Visual workflow and automation builder | In development |
| council | Evidence Council | Researcher, writer, reviewer and planner workspace | In development |

Required future page changes: prominent Download Hub for Windows action; verified version, exact size, system requirements and signing/publisher details; installation steps; six accurate product cards; support and checksums. Until publication show a noninteractive availability message, never a working-looking download action.

Instructions after publication: download installer from the verified release; run it; choose per-user destination and optional desktop shortcut; launch via Start Menu; extract the separate Genesis package; in Hub select Locate Genesis folder; choose the folder with EvidenceGenesis.exe; review trust prompt and Play. Preserve sibling brain/ and model/ folders. Hub does not install Genesis in this foundation. Explain Windows unsigned-app warnings for preview tests; do not tell users to disable security protection.

## 0.2.0 integration handoff

The Hub now implements signed Genesis installation/update transactions; the production public key and signed distribution catalog must be deployed before installation is available. Hub test version is 0.2.0. The product-specific signed locator is `https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/genesis-stable/genesis-stable.json`; it replaces the earlier proposed generic catalog URL. Components use immutable `genesis-v<VERSION>` releases and filenames with archive SHA-256. Website installation instructions may describe Install Genesis only after that deployment is verified. Installer size must come from the final published signed artifact. No website download action is enabled by this implementation.
