# ADR-0001 — Stack revision: Avalonia UI + .NET 10 (supersedes WinUI 3)

- **Date:** 2026-10-02
- **Status:** Accepted
- **Deciders:** fanioz (project lead) — recommended via the "Neon 💠" process, run by the Creative Director agent
- **Supersedes:** PRD v1.0 stack section (WinUI 3 / Windows App SDK 2.4)
- **Applies to:** SnipShelf (Microsoft Store app)

## Context
- PRD v1.0 specified **WinUI 3 (Windows App SDK 2.4) + C#**.
- Post-PRD constraint surfaced: the developer works daily on **macOS**, and WinUI 3 apps **cannot be built or run on macOS** — the entire development loop (not just final packaging) would have required a Windows VM.
- Project requirements kept fixed: Microsoft Store distribution (MSIX), Fluent-style feel, AI-codegen-friendly stack, solo scope (3 weeks + buffer), local-first architecture.

## Decision
Move SnipShelf to:

- **Avalonia UI 11.x** (pinned fully-stable pair) + **FluentAvalonia 2.x** for WinUI-style controls (NavigationView, InfoBar, SettingsExpander, ContentDialog)
- **.NET 10** (LTS)
- CommunityToolkit.Mvvm 8.4.2 and Microsoft.Data.Sqlite (both unchanged — UI-agnostic)
- **MSIX packaging via Avalonia Parcel**, built directly on macOS (no Windows SDK required); the Microsoft Store signs the submitted package
- **v1 ships Windows-only**; the codebase stays platform-neutral so a macOS build is a cheap future option

## Consequences
**Positive**
- Mac-native development: run, debug, iterate on macOS; Windows needed only for final QA passes and Store screenshots.
- Full packaging pipeline on macOS → Partner Center submission.
- Cross-platform option value for later (macOS build of the same code, near-free).

**Negative / accepted risks (with mitigations)**
- No true Mica / native WinUI materials — Fluent styling is an approximation. *Accepted: dev tool; toolbar-level fidelity is fine.*
- FluentAvalonia for Avalonia 12 is still preview → pin the fully-stable Av11 + FA2 pair; upgrade to Avalonia 12 (LTS) + FA3 once FA 3.0 ships stable. *Verified on NuGet at project creation.*
- Parcel is a young (1.x) tool → keep a CI fallback (GitHub Actions `windows-latest` job) for MSIX. *Recipe written during M0.*
- Final runtime QA + Store screenshots still need a Windows environment → one lightweight Windows 11 ARM VM / cloud PC. *Planned, not a blocker.*

## Alternatives considered
1. **WinUI 3 + Windows 11 ARM VM (Parallels)** — maximum native fidelity; heaviest daily loop + licensing cost; rejected.
2. **Uno Platform** — WinUI XAML cross-platform, but more tooling complexity and edge cases than warranted for a solo build; rejected.
3. **.NET MAUI** — not desktop-first for this use case; rejected.

## PRD sections updated in v1.1
Header (stack/platform), §1.2 (stack revision note), §4 (philosophy), §5 (UI tech specs + prompt hints), §6 (architecture, mapping table, version pins), §7 (constraints), §8 (M0 packaging de-risk), §9 (testing), §12 (packaging/distribution), §13 (pitfalls + prompt pack), Appendices C & E. Everything else (features, data model, privacy, monetization) carried over.

## References
- Avalonia docs — docs.avaloniaui.net (supported platforms, Avalonia 12 notes)
- Avalonia Parcel — avaloniaui.net/parcel · docs.avaloniaui.net/tools/parcel/packaging-for-windows
- FluentAvalonia — github.com/amwx/FluentAvalonia · amwx.github.io/FluentAvaloniaDocs
- Current PRD — ../../PRD-SnipShelf.md
