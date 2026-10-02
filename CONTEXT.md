# SnipShelf

A local-first desktop vault where developers store, tag, search, and copy reusable text — code snippets and AI prompts — with no account and no network.

## Language

**Snippet**:
A stored, reusable text record — the only kind of item the vault holds.
_Avoid_: entry, note, clip

**Prompt**:
A Snippet intended to be pasted into an AI tool. Not a separate entity — a Kind of Snippet.
_Avoid_: AI snippet, template

**Kind**:
The discriminator on a Snippet: `snippet` or `prompt`. Affects presentation and filtering only, never storage shape.
_Avoid_: type, category

**Vault**:
The whole local collection — all Snippets, the database itself. Also the name of the main page that lists all of them.
_Avoid_: library, store

**Favorites**:
A quick-access flag on a Snippet, and the page listing Snippets where it is set. A view, not a folder — favorites never float inside Vault.
_Avoid_: pinned, starred (as a noun)

**Tag**:
A case-insensitive label, unique by name, many-to-many with Snippets, used for filtering.
_Avoid_: label, category

**Seed**:
A first-run Snippet created on initial launch to teach the app. A normal record — deletable, never re-created.
_Avoid_: sample, demo data

**Bundle**:
The JSON interchange file for import and export. The only sanctioned data-in/data-out format in v1.
_Avoid_: export file, backup (reserved for the database copy)

**Backup**:
A user-initiated byte copy of the SQLite database file, timestamped at creation. Distinct from a Bundle.
_Avoid_: dump, export
