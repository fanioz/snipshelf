# PRD — SnipShelf
**Local-first snippet & AI-prompt vault for Windows · Avalonia UI + .NET 10 · Microsoft Store**

| | |
|---|---|
| **Status** | Draft **v1.1** — ready for build (stack revised: Avalonia + .NET 10; supersedes v1.0 WinUI 3 — see ADR-0001) |
| **Date** | 2026-10-02 (v1.1) |
| **Process** | "Neon 💠" Windows Product Architect (run in AutoClaw by the Creative Director agent) |
| **Platform** | Ships on Windows 10 (1809+) / Windows 11 · **developed on macOS** |
| **Stack** | Avalonia UI 11.x + FluentAvalonia 2.x · .NET 10 · C# · CommunityToolkit.Mvvm 8.4.2 |
| **Packaging** | MSIX built on macOS via Avalonia **Parcel** → Partner Center submission |
| **Scope** | Solo developer · 3 weeks + 1 week buffer (~1–2 h/day) |
| **Working title** | **SnipShelf** — reserve/verify in Partner Center before submission (§1.5) |

*Changelog — v1.1: stack revised to Avalonia UI + .NET 10 for macOS-first development; UI tech specs, AI prompt pack and pitfalls rewritten for Avalonia; packaging via Parcel; everything else carried over from v1.0. — v1.0: initial WinUI 3 spec.*

---

## 1. Product Discovery

### 1.1 Context of this run
- No Neon journal existed before this session (`.jules/neon-winui.md` created this run); no prior WinUI concepts, unfinished PRDs, or recurring themes found in the workspace → **idea selected autonomously**, per process.
- Selection filters applied: solo-shippable in 2–4 weeks · portfolio-demonstrable · AI-codegen-friendly · Fluent-style · low-risk APIs only · local-first.

### 1.2 Stack revision (v1.1) — the development environment drove the change
- **Constraint found after v1.0:** the developer works daily on **macOS**. WinUI 3 apps cannot be built *or run* on macOS — the entire build/dev loop (not just packaging) would have required a Windows VM.
- **Revision:** move to **Avalonia UI + .NET 10** (§6). Avalonia supports mac-native development; **FluentAvalonia** supplies WinUI-style controls (NavigationView, InfoBar, SettingsExpander, ContentDialog) so the experience pillars survive; **Parcel** builds the MSIX on macOS without the Windows SDK.
- **Rejected:** WinUI 3 + Windows VM (heavy daily loop — full analysis in ADR-0001); Uno Platform (tooling complexity for a solo build).
- The product idea, features, data model, privacy stance and monetization are **unchanged** from v1.0.

### 1.3 Ideas considered
| Candidate | Verdict | Why |
|---|---|---|
| Clipboard-history manager | Rejected | Windows' built-in Win+V undercuts the value prop; global hotkeys need platform-specific code (ask-first zone). |
| Rule-based file organizer (preview + undo) | Rejected | Destructive file operations are too risky for a 3-week solo MVP; harder to demo safely. |
| Pixel-art palette manager | Parked | Charming, but a narrow audience for a portfolio-first build. |
| **SnipShelf — snippet & prompt vault** | **Chosen** | Universal developer need; a 2026-relevant "local AI-prompt library" angle; zero network (maximum privacy, no auth); exercises exactly the patterns AI generates reliably. |

### 1.4 The idea, framed
- **Core fantasy:** your personal, instant, offline text arsenal — anything you reuse is two keystrokes away, forever.
- **Unique hook ("and also" test):** *"It's a snippet manager, AND ALSO your local AI-prompt library — everything you reuse lives in one searchable vault, offline, no account."*
- **Target aesthetics (MDA, ranked):** 1) **Sensation** — crisp, fast, polished; 2) **Expression** — curate your own library via tags & favorites; 3) **Submission** — zero-friction capture and retrieval.
- **Psychology (SDT):** **Competence** (mastery of your own knowledge base) + **Autonomy** (local-first, no lock-in, no sign-in).
- **Session emotional arc:** calm launch (state restored) → flow (search) → satisfaction (copy + confirmation) → delight (rediscovering your own library).
- **Anti-pillars — what SnipShelf is NOT:** not a cloud service; not an IDE or code editor; not a clipboard spy; not a syntax-highlighting playground (v1); not a team collaboration tool.

### 1.5 Naming — decision record
- Working title **SnipShelf**. Availability check (2026-10-02): no conflicting product surfaced.
- Rejected during check: **"SnipVault"** — an existing Microsoft Store app already ships under that name with a near-identical concept; **"SnipStash"** — already used by existing snippet/clipboard tools.
- **Action:** verify/reserve the name in Partner Center before Store submission; keep 2–3 backups (e.g. SnipDeck, Clipfolio).

### 1.6 Success criteria — "we'll know this was right if…"
- A Store-submittable MSIX exists ≤ 4 weeks from start (at ~1–2 h/day).
- You personally use it daily for 2 weeks after launch.
- ≥ 1 organic store review mentions "fast" or "clean / polished".
- AI codegen loop: ≤ 5 significant AI misses per sprint (kept low by the prompt pack in Appendix C).

---

## 2. Executive Summary
**SnipShelf is a fast, local-first Windows app where developers store, tag, search, and copy their code snippets and AI prompts — no account, no cloud, no friction.** (Built with Avalonia + .NET 10; developed natively on macOS.)

---

## 3. Target Audience
**One sentence:** developers and AI-assisted builders who constantly re-copy the same fragments (snippets, commands, prompts) from scattered notes, chats, and browser tabs.

- Persona sketch — "Maya, solo dev": juggles 3 projects; keeps snippets in scratch files and Slack DMs; wants one keyboard-driven place that never asks her to log in.

---

## 4. Core Philosophy
- **Solo-Optimized:** every feature must be buildable, testable, and shippable by one person. If it needs a QA team, it's cut from the MVP.
- **Native-Feeling (revised v1.1):** Fluent-style look via Avalonia's Fluent theme + FluentAvalonia WinUI-style controls; standard window chrome; system theme following; keyboard-first. Accepted trade-offs: no true Mica backdrop (approximated or solid), real system toasts deferred — the app should feel at home on Windows without being pixel-identical to WinUI 3.
- **Cross-platform-ready (new v1.1):** all code stays platform-neutral (no WinRT / Win32-only APIs) so a macOS build remains a cheap future option. v1 still ships Windows-only.
- **AI-Ready:** granular specs; one unit of work per prompt; no exotic APIs; every feature below carries a copy-paste **AI Prompt Hint**.

---

## 5. Features & MVP Scope

### Scope matrix
| # | Feature | MVP | v1.1 | Never (v1) |
|---|---|---|---|---|
| F1 | Shell, navigation, theming | ● | window size/pos restore | custom chrome kits |
| F2 | Vault list + live search + filters | ● | — | heavy third-party UI kits |
| F3 | Detail & editor + autosave | ● | syntax highlighting (read-only) | rich text editor |
| F4 | Copy flow + confirmation | ● | real Windows toasts | clipboard history listening |
| F5 | Tags (inline add/remove/filter) | ● | rename, colors, tag page | tag hierarchy |
| F6 | Favorites | ● | — | — |
| F7 | Import / export / backup (JSON) | ● | Markdown export, auto-backup | cloud sync |
| F8 | Settings | ● | advanced options | telemetry |
| F9 | States, keyboard map, accessibility | ● | — | — |
| F10 | First-run seeds & tips | ● | sample packs | nag screens |

### F1 — App shell, navigation & theming
- **User story:** As a user, I want an app that feels native and calm on launch, remembering where I left off.
- **Tech spec:** `MainWindow.axaml` with **FluentAvalonia `NavigationView`** (menu: **Vault / Favorites / Settings**, `PathIcon` glyphs) + content area (FA `Frame` page navigation). Theme: follow system by default, overridable to Light/Dark — applied via `Application.Current.RequestedThemeVariant` (`ThemeVariant.Default` = system), set from `ISettingsService`; all colors via theme tokens (`DynamicResource`). Standard window chrome. Persist and restore **last selected page** (MVP); window size/position restore → v1.1.
- **AI prompt hint:** *"Create an Avalonia 11 app shell: MainWindow with a FluentAvalonia NavigationView (items 'Vault', 'Favorites', 'Settings', PathIcon vectors) and FA Frame for page navigation; apply RequestedThemeVariant from ISettingsService; persist/restore the selected nav item. Follow FluentAvalonia docs for control setup — do not invent APIs."*
- **Constraints:** standard window chrome only (no custom chrome kits in MVP). No P/Invoke. Follow FA docs for NavigationView/Frame wiring; verify the shell renders on macOS (dev) and Windows (target).

### F2 — Vault: master list + live search + filters
- **User story:** I type a few letters and my fragment is on screen in under a second.
- **Tech spec:** Two-pane layout — left: search + filter chips (All / Favorites / per-tag) + virtualized `ListBox`; right: detail pane (F3). List item: title, 2-line preview, tag chips, relative updated time, favorite star (`PathIcon`). Search: `AutoCompleteBox` (Avalonia native), 200 ms debounce, repository query. States: loading / empty vault (teaching copy + "New snippet" button) / no results (distinct copy). Sort: Updated (default) / Title / Created. Item templates use `DataTemplate` + `x:DataType` **compiled bindings**.
- **AI prompt hint:** *"Build VaultView + VaultViewModel in Avalonia 11: two-column Grid (list 320 px + details pane). Left: AutoCompleteBox + virtualized ListBox bound to a SnippetListItemViewModel collection with compiled bindings (x:DataType). Search calls ISnippetRepository.SearchAsync(term) with a 200 ms debounce implemented in the ViewModel — no logic in code-behind. Three states: loading, empty-vault, no-results."*
- **Constraints:** no CommunityToolkit.WinUI.* packages (WinUI-only — wrong platform). No logic in code-behind beyond bindings.

### F3 — Snippet detail & editor (with autosave)
- **User story:** I can view, copy, or edit without mode anxiety — nothing is ever lost.
- **Tech spec:** Right pane shows read view (monospaced body, metadata "Updated 2 days ago") with **Edit / Copy / Delete** actions. Edit mode: Title `TextBox`; tags editor (chips + add via `AutoCompleteBox`); Kind `ComboBox` (Snippet / Prompt); body `TextBox` (`AcceptsReturn`, monospace font stack `Cascadia Mono, Consolas, Menlo, monospace`, spellcheck off). Autosave: 1 s debounce + Ctrl+S; dismissable **FA `InfoBar`** "Saved"; Delete → **FA `ContentDialog`** confirm (hosted in page XAML per FA docs); dirty-navigation guard.
- **AI prompt hint:** *"Create SnippetEditorViewModel (CommunityToolkit.Mvvm source generators) with debounced autosave (1 s) + Ctrl+S via KeyBindings. View: title TextBox, tag chips, Kind ComboBox, body TextBox (AcceptsReturn, monospace fallback stack). FluentAvalonia ContentDialog for delete confirmation (per FA docs); InfoBar for saved/failed. Guards: title + body non-empty."*
- **Constraints:** plain text only; no rich text; no syntax highlighting in v1 (documented v1.1 candidate); monospace only.

### F4 — Copy flow & feedback
- **User story:** One click, instant confirmation, zero doubt that it worked.
- **Tech spec:** Copy = `TopLevel.GetTopLevel(control).Clipboard.SetTextAsync(...)` — **UI thread only**. Feedback: transient **FA `InfoBar`** banner — "Copied — {title}" (Success severity, auto-dismiss ~3 s) — the cross-platform replacement for system toasts in MVP. Real Windows toast integration → v1.1 exploration. Clipboard busy: retry once after ~150 ms, then error InfoBar. Keyboard: **Enter on a list item = copy** (speed path), Copy button in detail, **Ctrl+Shift+C** = copy current item anywhere in-app (`KeyBindings`).
- **AI prompt hint:** *"Create ClipboardService.CopyTextAsync(string): Avalonia TopLevel.Clipboard, called on the UI thread only, retry once on exception. NotificationService shows a transient FluentAvalonia InfoBar 'Copied — {title}' that auto-dismisses. Never call the clipboard from a background thread."*
- **Constraints:** no global hotkeys in MVP (platform-specific registration — deferred, needs approval). In-app confirmations only; do not use WinRT toast APIs (not cross-platform).

### F5 — Tags & organization
- **User story:** I keep the vault tidy and filter to "just powershell stuff" in one click.
- **Tech spec:** Snippet↔tag many-to-many; chips on list items and detail; inline add/remove; suggestions from existing tags (`AutoCompleteBox`); case-insensitive, trimmed. Vault filter row shows top tags (+ "more"). Renaming tags, colors, a dedicated tag page → v1.1 (schema already supports them).
- **AI prompt hint:** *"SnippetRepository: AddTagToSnippet / RemoveTag / GetTagsWithCounts (SQLite, parameterized). Reusable TagChip control (rounded Border + close Button with AutomationProperties.Name). AutoCompleteBox suggests existing tag names; filter chip row binds to top tags with an 'All' chip."*
- **Constraints:** no tag hierarchy; no colors in MVP; no separate tag-management page in MVP.

### F6 — Favorites
- **User story:** I pin the ten things I use daily.
- **Tech spec:** `Favorite` flag with star toggle (list + detail). Favorites page = the same list filtered `Favorite = true`. Favorites do **not** float inside Vault (mental model: Vault = all, Favorites = view).
- **AI prompt hint:** *"Add ToggleFavoriteCommand (RelayCommand) to SnippetListItemViewModel flipping the flag via ISnippetRepository and notifying via IMessenger so Vault and Favorites lists refresh. Star = ToggleButton with PathIcon."*
- **Constraints:** none beyond MVVM rules.

### F7 — Import / export / backup
- **User story:** My data in, my data out — plain JSON, no lock-in, safety net included.
- **Tech spec:** Export → `IStorageProvider.SaveFilePickerAsync` → JSON bundle `{schemaVersion, exportedUtc, snippets[], tags[]}`. Import → `OpenFilePickerAsync` → validate → merge (dedupe on identical title+body → skip) → report dialog: "Imported 12 · updated 3 · skipped 2 · errors 0". Backup Now → copy DB to a chosen folder with timestamp. Settings shows DB path + counts. **Simplification from v1.0:** Avalonia's StorageProvider needs no window-handle interop — pickers work the same on macOS (dev) and Windows (target).
- **AI prompt hint:** *"Implement BackupService with Avalonia IStorageProvider pickers (TopLevel.StorageProvider). Serialize with System.Text.Json DTOs. Import runs inside a single SQLite transaction; collect per-item errors into a report dialog. Never overwrite silently; dedupe rule: same title+body → skip."*
- **Constraints:** no cloud. No destructive imports. JSON only in MVP; Markdown export → v1.1.

### F8 — Settings
- **User story:** I adjust the few things that matter and always know where my data lives.
- **Tech spec:** **FluentAvalonia `SettingsExpander`**-style rows (Win11-Settings look): **Appearance** (theme picker: System / Light / Dark — applies immediately), **Behavior** (confirm-before-delete toggle, default sort), **Data** (path display, Export / Import / Backup Now, counts), **About** (version, repo link, credits, "no data collected" statement). Prefs stored in **`settings.json`** in the app data dir via `ISettingsService` (cross-platform — replaces WinRT `ApplicationData.LocalSettings`).
- **AI prompt hint:** *"Build SettingsView + SettingsViewModel: FluentAvalonia SettingsExpander rows for theme (System/Light/Dark → RequestedThemeVariant, applies immediately), confirm-delete toggle, default sort, data folder display with open action, Export/Import/Backup buttons, About card. Persist in settings.json via ISettingsService."*
- **Constraints:** no telemetry settings (there is none). No update UI (Store handles updates).

### F9 — States, keyboard map & accessibility
- **User story:** Nothing ever dead-ends; everything is reachable by keyboard.
- **Tech spec:** Empty/loading/error visuals defined once and reused. Keyboard map (`KeyBindings`): **Ctrl+N** new · **Ctrl+F** focus search · **Ctrl+S** save · **Ctrl+Shift+C** copy · **Enter** (list) copy · **Delete** delete (confirm) · **Esc** clear search. `AutomationProperties.Name` on all icon-only buttons; visible focus; contrast checked in both themes; tooltips on icon buttons.
- **AI prompt hint:** *"Add Avalonia KeyBindings for Ctrl+N/F/S/Shift+C; AutomationProperties.Name on every icon-only Button; consistent empty/loading/error visuals reused by all lists; verify focus visuals in light and dark."*
- **Constraints:** standard controls only; no custom-drawn controls.

### F10 — First-run seeds & tips
- **User story:** The app teaches itself in 30 seconds on first launch.
- **Tech spec:** Seed one "Welcome — how to use SnipShelf" snippet + 2 example prompts; one-time dismissable InfoBar tip; seeds are normal records (deletable).
- **AI prompt hint:** *"On first launch (flag in settings.json), insert three seed rows: a Welcome snippet and two example prompts. Show a single dismissable InfoBar tip. Do not re-seed after deletion."*
- **Constraints:** No nag screens; no repeated tips.

### Explicitly OUT (never for v1)
Accounts / cloud sync · team sharing · browser extension · IDE plugin · plugin system · rich text · image attachments · OCR · AI generation features (v2 discussion) · mobile companions · macOS shipping build (kept as a cheap future option).

---

## 6. Technical Architecture

### Stack (pinned as of 2026-10)
| Concern | Choice | Notes |
|---|---|---|
| UI framework | **Avalonia UI 11.x** (latest stable patch) | Runs on macOS (dev) and Windows (target); .NET 10 supported |
| WinUI-style controls | **FluentAvalonia 2.x** (stable) | NavigationView, Frame, InfoBar, ContentDialog, SettingsExpander, TabView |
| Runtime | **.NET 10** (LTS) | Avalonia desktop baseline: .NET 8+ |
| MVVM | **CommunityToolkit.Mvvm 8.4.2** | Source generators: `ObservableProperty`, `RelayCommand`, `IMessenger` |
| Storage | **SQLite** via Microsoft.Data.Sqlite | DB in app data dir (see below) |
| Preferences | `settings.json` via `ISettingsService` | Cross-platform (replaces WinRT LocalSettings) |
| DI | Microsoft.Extensions.DependencyInjection | Minimal registrations |
| JSON | System.Text.Json | Import/export bundle |
| Tests | xUnit (+ optional Avalonia.Headless UI tests) | Run on macOS |
| Packaging | **Avalonia Parcel → MSIX** (built on macOS) | No Windows SDK needed; Store signs the submitted package |
| CI fallback | GitHub Actions `windows-latest` job | `dotnet publish` + packaging step if Parcel hiccups |

**Version pin rationale (v1.1):** pin the **fully-stable pair** — Avalonia 11.x + FluentAvalonia 2.x. Avalonia 12 is an LTS release but FluentAvalonia 3.0 for Avalonia 12 is still **preview** (as of 2026-10); adopt Av12 + FA3 once FA 3.0 ships stable — for our small shell the migration is expected to be minor. Verify the pairing on NuGet at project creation.

### WinUI → Avalonia control mapping (the v1.1 delta at a glance)
| v1.0 (WinUI 3) | v1.1 (Avalonia + FluentAvalonia) |
|---|---|
| NavigationView / Frame | FA NavigationView / FA Frame |
| InfoBar | FA InfoBar |
| ContentDialog (+ XamlRoot) | FA ContentDialog (host per FA docs) |
| SettingsCard | FA SettingsExpander rows |
| AutoSuggestBox | Avalonia AutoCompleteBox |
| ListView + DataGrid (avoided) | ListBox (virtualized) |
| ThemeResource | `DynamicResource` + `ThemeVariant` |
| `x:Bind` | `{CompiledBinding}` + `x:DataType` |
| Mica backdrop | none — solid theme background (accepted trade-off) |
| AppNotificationManager (toasts) | transient FA InfoBar banner (MVP); real toasts → v1.1 |
| File pickers + InitializeWithWindow | `IStorageProvider` pickers (no interop) |
| Clipboard (WinRT, UI thread) | `TopLevel.Clipboard` (UI thread) |
| LocalSettings | `settings.json` in app data dir |
| Segoe Fluent Icons / FontIcon | `PathIcon` with bundled vectors (portable) |

### Layers
```
Views (AXAML)       →  ViewModels (CommunityToolkit.Mvvm)  →  Services  →  Data
                        ↘  Models (POCO DTOs)                 (repos, clipboard, notifications, dialogs, backup)
```
- **Rules:** code-behind contains only `InitializeComponent`, event→command forwarding, and control references needed for Clipboard/TopLevel access. ViewModels never reference `Avalonia.Controls` (abstract via services: `IClipboardService`, `IDialogService`, `INotificationService`). All I/O async. Cross-VM refresh via `IMessenger` (`SnippetSavedMessage`, `TagsChangedMessage`).

### Data model (DDL in Appendix B)
- `Snippet` (Id, Title, Body, Kind [snippet|prompt], Favorite, CreatedUtc, UpdatedUtc)
- `Tag` (Id, Name unique, case-insensitive) · `SnippetTag` (many-to-many, cascade)
- `Meta` (schema_version) · prefs in `settings.json`
- **App data dir:** `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/SnipShelf/` — Windows: `%LOCALAPPDATA%\SnipShelf`; macOS (dev): user data dir. DB file: `snipvault.db`.

### Repository surface (interface sketch)
```csharp
Task<IReadOnlyList<Snippet>> SearchAsync(string term, SortMode sort, Guid? tagId, bool favoritesOnly);
Task<Snippet?> GetAsync(long id);
Task<long> UpsertAsync(Snippet s);
Task DeleteAsync(long id);
Task SetFavoriteAsync(long id, bool value);
Task AddTagAsync(long snippetId, string tagName);
Task RemoveTagAsync(long snippetId, long tagId);
Task<IReadOnlyList<TagCount>> GetTagsWithCountsAsync();
Task<ImportReport> ImportAsync(Stream json);   // single transaction
Task ExportAsync(Stream target);
Task BackupAsync(string folderPath);
```

### Folder structure
```
SnipShelf/
├─ SnipShelf.sln
├─ src/SnipShelf/
│  ├─ Program.cs                 # desktop entry point
│  ├─ App.axaml(.cs)             # DI bootstrap, theme init, first-run seeds
│  ├─ Views/                     # MainWindow, VaultView, SettingsView, Controls/ (TagChip…)
│  ├─ ViewModels/                # ShellViewModel, VaultViewModel, SnippetEditorViewModel, SettingsViewModel
│  ├─ Models/                    # Snippet, Tag, TagCount, ImportReport, ExportBundle
│  ├─ Services/                  # ISnippetRepository + Sqlite impl, ClipboardService, NotificationService,
│  │                             # DialogService, BackupService, SettingsService, DatabaseBootstrapper
│  └─ Assets/                    # icons (avares://), store art source
├─ tests/SnipShelf.Tests/        # xUnit (+ optional Avalonia.Headless)
└─ docs/                         # this PRD, ADR-0001, prompt-pack notes
```

### Threading & performance budgets
- All UI updates on the Avalonia UI thread (`Dispatcher.UIThread`); SQLite is synchronous — wrap DB calls in `Task.Run` inside repositories; await on the UI context.
- Budgets: cold launch < 1.5 s · search keystroke→results < 50 ms @ 1,000 snippets · list first paint < 300 ms · smooth scroll (virtualized list) · DB < few MB at 1k snippets.

---

## 7. Constraints & Exclusions
- **Process boundaries applied:** packaged MSIX only; no platform P/Invoke (none needed — pickers and clipboard are first-class in Avalonia); standard window chrome; single window.
- **Tech constraints:** no WPF / UWP / MAUI; no `CommunityToolkit.WinUI.*` (WinUI-only packages); no WinRT-only APIs (`ApplicationData`, `AppNotificationManager`, `Windows.UI.*`) — keep the codebase platform-neutral; no server components; **zero network calls in v1** (design constraint, enforced by review); no admin privileges; no registry edits.
- **Ask-first list (require explicit sign-off):** global hotkeys · frosted/acrylic backdrop experiments · custom window chrome · syntax-highlighting libraries · native Windows toast integration.

---

## 8. Development Milestones (solo, ~1–2 h/day)

| Milestone | Scope | Demo checkpoint |
|---|---|---|
| **M0 — Setup (day 1–2)** | Install .NET 10 SDK on macOS; `dotnet new install Avalonia.Templates`; scaffold the MVVM template; app runs on macOS; **de-risk packaging immediately: hello-world MSIX via Parcel + a Partner Center draft submission — in week 1, not week 4** | App runs locally; MSIX artifact produced on the Mac |
| **Week 1 — Foundation** | Shell (FA NavigationView, theme service) → DB bootstrap + DDL + seeds → repository + xUnit tests (CRUD/search) → Vault list read-only wired to DB | Window lists 20 seeded items; typing filters live |
| **Week 2 — Core UX** | Detail/editor + autosave → tags → favorites → copy + banner → delete confirm → empty/loading/error states → keyboard map | Full daily loop: capture → find → copy |
| **Week 3 — Polish & ship** | Settings page → import/export/backup → accessibility pass → perf pass (1k items) → icon + Store screenshots (Windows VM) → MSIX via Parcel → Partner Center submission | Submitted to Store |
| **Week 4 — Buffer** | Certification fixes, listing copy, launch, day-one patch if needed | Live on Store |

**Definition of Done:** all MVP features pass the manual checklist · no P0/P1 bugs · MSIX passes Store ingestion/validation · both themes pass contrast check · perf budgets met · README + privacy note in repo.
**Cross-OS discipline:** develop & smoke-test on macOS daily; do a **weekly pass on a Windows environment** (VM/cloud) — chrome, fonts, dialogs, file pickers.

**Cut order if behind (most cuttable first):** F10 seeds → F6 Favorites page (keep the flag) → F7 Import (keep Export + Backup) → F9 extra polish → F5 inline tag editing (fallback: comma-separated tag field).
**Never cut:** F2/F3/F4 (the core loop), theming, autosave.

---

## 9. Testing Strategy
- **Unit (xUnit, 30–50 tests, < 5 s, runs on macOS):** repository CRUD & search; import merge incl. malformed JSON; editor validation + autosave debounce; export roundtrip; tag counts. Optional: Avalonia.Headless UI smoke tests.
- **Manual smoke script (release checklist, ~20 steps):** launch (cold/warm) · search · copy (button + Enter) · confirmation banner · tags add/remove/filter · favorite toggle · edit + autosave + Ctrl+S · delete confirm · theme switch · import/export/backup roundtrip · small window.
- **Windows pass (weekly + pre-release):** run the published build on a Windows VM/cloud — chrome, Mica-less background, fonts, file pickers, installer/MSIX behavior.
- **Accessibility:** automation names + keyboard-only pass on macOS and Windows; contrast in both themes.
- **Packaging:** MSIX assembled by Parcel → validate locally → Partner Center ingestion check → Store certification; also verify the CI fallback path once.
- **Crash hygiene:** global unhandled-exception handlers → rolling log file in the app data dir + friendly dialog.

---

## 10. Error Handling
Principles: never fail silently · degrade gracefully · recoverable actions offer retry · user data is never destroyed without confirmation.

| Case | Behavior |
|---|---|
| DB unavailable / corrupt | Dialog with data-folder path + "Reset database (back up current first)" action |
| Migration failure | Auto-backup, then safe reset with notice |
| Import invalid | Per-item error report; single transaction — nothing partially written |
| Clipboard busy | 1 retry (~150 ms) → error InfoBar |
| Notification/banner failure | Silent fallback to an inline status message |
| Picker canceled / fails | No-op (cancel is not an error) |
| Autosave failure | Keep dirty state + persistent InfoBar with retry |
| Unexpected crash | Log + relaunch note; logs are local-only |

---

## 11. Privacy & Data Policy
- **All data local:** one SQLite DB + `settings.json` in the app data dir. **Zero network calls in v1** — verifiable (no `HttpClient` usage; review gate).
- **No telemetry, no analytics, no ads, no accounts.**
- Export/backup are user-initiated, plain JSON — no lock-in.
- **Store compliance:** MSIX packaged (Store signs the submitted package); ready-to-paste privacy statement: *"SnipShelf collects no data. All content stays on your device; there is no telemetry and no network communication."* Category: Developer tools; age rating: E.
- Uninstall removes app data (standard MSIX behavior) → Settings shows a "backup reminder" note.

---

## 12. Distribution & Monetization
- **Packaging (macOS-first, v1.1):** MSIX built with **Avalonia Parcel directly on macOS** — no Windows SDK required; the Microsoft Store signs the submitted package (no local code-signing certificate needed for Store submission). Flow: `dotnet publish` → Parcel build → MSIX → Partner Center upload. **Fallback:** a GitHub Actions `windows-latest` job performing the same steps (recipe written during M0). **Windows touchpoint:** one lightweight Windows 11 ARM VM (or cloud PC) for final runtime QA + Store screenshots — planned, not a blocker.
- **Primary channel:** Microsoft Store. **Free at launch** (portfolio-first: installs + reviews > revenue).
- **Secondary (v1.1, optional):** GitHub Releases with a signed MSIX — note signing certificate options before committing.
- **Monetization (later):** optional one-time "Supporter" Store add-on ($4.99) at v1.5+. **No licensing code in v1.**
- **Store listing kit to prepare:** title + subtitle; short description; description; 5 feature bullets; 7 search terms; 4–6 screenshots (vault light, vault dark, editor, search — captured on Windows); category; pricing = Free.
- **Launch checklist:** name reservation, privacy statement, age rating, screenshots, submission, respond to all reviews within 24 h for the first two weeks.
- **Update cadence:** monthly small updates; Store handles delivery.

---

## 13. AI Implementation Strategy
Rules of engagement for the coding AI:
1. **One layer per prompt** — never "build the whole app".
2. **Always attach:** relevant PRD section + DDL + folder tree + the pitfalls block below.
3. **Order:** AXAML → ViewModel → wire → build → test → commit. Compile after every step.
4. **Forbid speculation:** if the AI is unsure about an API, it must say so — then check the Avalonia / FluentAvalonia docs together.
5. **Keep this pitfalls block in every prompt:**

> **Avalonia 11 pitfalls (paste into prompts):** Use `Avalonia.*` namespaces — never `Microsoft.UI.Xaml.*` or `Windows.UI.Xaml.*` (model drift toward WinUI/UWP; wrong here). No `x:Bind`: use `{Binding}` / `{CompiledBinding}` + `x:DataType` on templates. Theming: `DynamicResource` tokens + `ThemeVariant` (there is no `ThemeResource`). Styling uses Avalonia selectors (`<Style Selector="…">`). FluentAvalonia controls live in `FluentAvalonia.UI.Controls` (NavigationView, Frame, ContentDialog, InfoBar, SettingsExpander) — check FA docs, do not invent APIs. Icons: `PathIcon` with bundled vectors (Segoe fonts won't render on macOS). Assets via `avares://` URIs. Clipboard via `TopLevel.Clipboard` on the UI thread. SQLite is synchronous — wrap in `Task.Run`. No logic in code-behind. `async void` only for event handlers. Never call WinRT APIs (`ApplicationData`, `AppNotificationManager`) — cross-platform code only.

- **Generation order (one prompt each — full texts in Appendix C):** C1 shell+DI → C2 data layer+tests → C3 Vault list → C4 editor → C5 clipboard+notification → C6 tags/favorites → C7 settings → C8 import/export/backup → C9 accessibility/states polish.
- **Verification loop per step:** build → run (macOS) → checklist → commit; weekly Windows pass for platform checks.
- **Context pack** to feed the AI with each prompt: this PRD (§5 feature + §6 architecture) + Appendix B DDL + folder tree + pitfalls block.

---

## Appendix A — Wireframes (ASCII)

**Main window — Vault (read view)** *(chrome is platform-native)*
```
┌─────────────────────────────────────────────────────────────────────┐
│ SnipShelf                                        ─   □   ✕          │
├──────────┬────────────────────────────┬─────────────────────────────┤
│ ⌂ Vault  │  [ 🔍 Search…          ]   │  UUID v4 generator     ★    │
│ ★ Favs   │  (All)(Favorites)(pwsh)    │  ─────────────────────────  │
│ ⚙ Sett.  │  ┌──────────────────────┐  │  kind: snippet · upd. 2d    │
│          │  │ UUID v4 generator ★  │  │                             │
│          │  │ const id = crypto.r… │  │  ┌───────────────────────┐  │
│          │  │ #js  #uuid           │  │  │ const id =            │  │
│          │  ├──────────────────────┤  │  │   crypto.randomUUID() │  │
│          │  │ Get-Location alias ★ │  │  └───────────────────────┘  │
│          │  │ Set-Location …       │  │   [ Copy ] [ Edit ] [ 🗑 ]  │
│          │  └──────────────────────┘  │                             │
└──────────┴────────────────────────────┴─────────────────────────────┘
```

**Empty state (first run after seeds deleted)**
```
│          │                            │   No snippets yet.          │
│          │   (  empty list area  )    │   Create your first one —   │
│          │                            │   or import a JSON backup.  │
│          │                            │   [ + New snippet ]         │
```

**Editor mode (right pane)** — Title field · Kind combo · Tag chips + add box · monospace body · status InfoBar "Saved".

---

## Appendix B — Data model DDL + seed
```sql
CREATE TABLE Meta (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);            -- schema_version
CREATE TABLE Snippets (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Title TEXT NOT NULL,
  Body TEXT NOT NULL,
  Kind TEXT NOT NULL DEFAULT 'snippet',            -- 'snippet' | 'prompt'
  Favorite INTEGER NOT NULL DEFAULT 0,
  CreatedUtc TEXT NOT NULL,                         -- ISO-8601
  UpdatedUtc TEXT NOT NULL
);
CREATE INDEX IX_Snippets_Updated ON Snippets(UpdatedUtc DESC);
CREATE TABLE Tags (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL UNIQUE COLLATE NOCASE
);
CREATE TABLE SnippetTags (
  SnippetId INTEGER NOT NULL REFERENCES Snippets(Id) ON DELETE CASCADE,
  TagId     INTEGER NOT NULL REFERENCES Tags(Id)     ON DELETE CASCADE,
  PRIMARY KEY (SnippetId, TagId)
);
-- Search: start with LIKE '%term%' across Title/Body/TagName (fine ≤ ~2k rows).
-- Upgrade path: FTS5 virtual table later without schema break (keep repo abstraction).
```
DB file lives in the app data dir (§6). Seed: `Welcome — how to use SnipShelf` snippet + 2 example prompts ("Explain this code" prompt, "Regex: semver" snippet).

---

## Appendix C — AI Prompt Pack (copy-paste, Avalonia edition)

> Prefix every prompt with: *"You are helping build SnipShelf, a cross-platform desktop app in C# on .NET 10 using Avalonia UI 11 with FluentAvalonia for WinUI-style controls, CommunityToolkit.Mvvm, and Microsoft.Data.Sqlite. [paste pitfalls block from §13]"* — then one of:

**C1 — Shell + DI**
> Create the app skeleton: Program.cs + App.axaml.cs with Microsoft.Extensions.DependencyInjection registrations (ISnippetRepository, IClipboardService, INotificationService, IDialogService, ISettingsService, all ViewModels) and ThemeVariant initialization from ISettingsService. MainWindow with FluentAvalonia NavigationView (Vault, Favorites, Settings) + FA Frame navigation. Acceptance: app launches on macOS and Windows; all three pages reachable; theme switch works.

**C2 — Data layer + tests**
> Implement DatabaseBootstrapper (creates snipvault.db in Environment.GetFolderPath(LocalApplicationData)/SnipShelf, applies DDL, sets schema_version, inserts first-run seeds) and SqliteSnippetRepository implementing ISnippetRepository (Search/Upsert/Delete/SetFavorite/Tags/Counts) with parameterized SQL wrapped in Task.Run. Add xUnit tests: CRUD, search matching (title/body/tag), tag counts, cascade delete. Acceptance: tests green on macOS.

**C3 — Vault view**
> Build VaultViewModel + VaultView: two-pane Grid; virtualized ListBox of SnippetListItemViewModel with compiled bindings (x:DataType), search via AutoCompleteBox with 200 ms debounce in the VM, filter chips (All/Favorites/tags), sort options (Updated/Title/Created); three visual states (loading/empty/no-results); selection drives the detail pane. Acceptance: typing filters live; 1k rows scroll smoothly.

**C4 — Editor**
> Build SnippetEditorViewModel + editor pane: title TextBox, kind ComboBox, tag chips (add/remove with AutoCompleteBox suggestions), monospace body TextBox (font fallback "Cascadia Mono, Consolas, Menlo, monospace"); 1 s debounced autosave + Ctrl+S KeyBinding; save-state InfoBar; validation guards; FluentAvalonia ContentDialog delete confirmation (per FA docs); dirty-navigation guard. Acceptance: edits survive app restart; delete asks first.

**C5 — Clipboard + notification**
> Implement ClipboardService (TopLevel clipboard, UI thread, single retry) and NotificationService showing a transient FluentAvalonia InfoBar ("Copied — {title}", auto-dismiss ~3 s). Wire Enter-on-list and Ctrl+Shift+C in KeyBindings. Acceptance: copy works from list and detail; banner shows; failure path shows an error InfoBar without crashing.

**C6 — Tags & favorites**
> Wire inline tag add/remove and star toggle commands (IMessenger notifications so Vault/Favorites refresh). Acceptance: tag filter and counts update live across pages.

**C7 — Settings page**
> Build SettingsViewModel + SettingsView with FluentAvalonia SettingsExpander rows: theme (System/Light/Dark — applies immediately via RequestedThemeVariant), confirm-before-delete, default sort, data folder display + open, Export/Import/Backup buttons, About card. Persist in settings.json via ISettingsService. Acceptance: every setting survives restart.

**C8 — Import / export / backup**
> Implement BackupService: export JSON bundle via IStorageProvider SaveFilePickerAsync; import via OpenFilePickerAsync with validation + single transaction + dedupe (title+body) + report dialog; backup-now copy with timestamp. Acceptance: roundtrip export→wipe→import restores everything; malformed file produces a readable error report.

**C9 — Polish pass**
> Add Avalonia KeyBindings (Ctrl+N/F/S/Shift+C), AutomationProperties.Name to all icon buttons, consistent empty/loading/error states, tooltips; run a keyboard-only run-through of capture→find→copy on macOS and fix findings. Acceptance: full flow keyboard-only; focus visuals visible in both themes.

---

## Appendix D — Solo feasibility check
- **Can one person build this in 2–4 weeks?** Yes — CRUD + search + settings, no server, no exotic APIs, one predictable core loop; the riskiest parts (pickers, clipboard, packaging) all have documented cross-platform paths or fallbacks.
- **Is the logic clear enough for an AI to generate the boilerplate?** Yes — mainstream MVVM, pinned package versions, a 9-prompt pack, and a pitfalls block targeted at Avalonia-specific AI failures.
- **Does it feel "Fluent"?** Yes — FluentAvalonia NavigationView/SettingsExpander/InfoBar give a Win11-Settings-adjacent look; no true Mica (accepted); system theming; keyboard-first.

**Decisions & open questions**
- Open: final name reservation (Partner Center); Avalonia/FluentAvalonia exact patch versions at project creation; Parcel dry-run outcome (M0); Windows VM/cloud choice for QA + screenshots.
- Decided: Av11 + FA2 stable pair now (upgrade path to Av12 LTS + FA3 when stable); no heavy UI kits; LIKE search first (FTS5 later behind repo); single window; standard chrome.

---

## Appendix E — References (verified during these sessions, 2026-10-02)
- Avalonia 12 release notes & LTS statement — avaloniaui.net/blog/avalonia-12 · avaloniaui.net/enterprise
- Avalonia supported platforms (desktop .NET 8+ baseline) — docs.avaloniaui.net/docs/supported-platforms
- Avalonia 12 breaking changes — docs.avaloniaui.net/docs/avalonia12-breaking-changes
- FluentAvalonia — github.com/amwx/FluentAvalonia · docs: amwx.github.io/FluentAvaloniaDocs (NavigationView, SettingsExpander pages)
- FluentAvalonia NuGet (2.x for Avalonia 11; 3.0.0-preview1 for Avalonia 12) — nuget.org/packages/FluentAvaloniaUI
- Avalonia Parcel — avaloniaui.net/parcel · docs.avaloniaui.net/tools/parcel (setup, command-line reference, packaging for Windows)
- CommunityToolkit.Mvvm 8.4.2 — nuget.org/packages/CommunityToolkit.Mvvm
- Name checks: existing "SnipVault" snippet manager on Microsoft Store; "SnipStash" in use by snippet tools.

*End of PRD v1.1.*
