# Rampage.CryptedNotex (秘密档案)

![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4) ![C#](https://img.shields.io/badge/C%23-12-239120) ![Platform](https://img.shields.io/badge/Platform-Windows-0078D6) ![Storage](https://img.shields.io/badge/Storage-SQLite-003B57) ![License](https://img.shields.io/badge/License-MIT-yellow)

**English | [简体中文](Readme_cn.md)**

Rampage.CryptedNotex (Chinese display name: 秘密档案, "Secret Archive") is a **local, fully offline encrypted notebook for Windows**. Note contents and attachments are encrypted with a key derived from your password **before** they ever touch the SQLite database — copying the database file away gives an attacker ciphertext, not your notes. No network, no telemetry, no accounts in the cloud.

Built on the open-source [Rampage framework](https://github.com/XTiger1986/Rampage) (WinForms + .NET 8 + self-built ORM), it covers the full note-taking loop: multi-user local accounts, categories with two encryption modes, three note types (plain text / rich text / Markdown with live preview), encrypted attachments, a trash bin, auto-lock and system-tray residency, and 11 built-in UI themes you can extend with plain JSON files.

> **There is no back door.** Content keys exist only as derivatives of your password. Forgetting the password means the data is unrecoverable — by design.

---

## Key Features

- **Zero-plaintext storage** — note bodies and attachments are stored as AES-256-GCM ciphertext; only titles, category names and timestamps remain readable for listing/search
- **Password-derived keys** — PBKDF2-SHA256 (600,000 iterations, per-user random salt) derives the 256-bit content key; the login hash uses an independent salt, so one can never reveal the other
- **Change password = full re-encryption** — every affected note (including trashed ones) and attachment is decrypted and re-encrypted inside one transaction; a failure rolls back and the old password keeps working
- **Per-category independent passwords** — a category can optionally use its own password instead of (or switched back to) the account password; each transition re-encrypts that category's notes
- **Three note types** — plain text, rich text (RTF with a themed formatting toolbar), and Markdown with side-by-side live preview (offline `marked` + `highlight.js` via WebView2; degrades gracefully to editing-only when the WebView2 runtime is absent)
- **Encrypted attachments** — files are encrypted into the database; images preview from memory only (no temp files), other types export on demand
- **Trash bin, pinning, title search** — soft delete with restore / purge / empty-trash (attachments cascade), pinned notes first, case-insensitive LIKE search with wildcard escaping
- **Auto-lock & tray** — configurable idle lock (also fires on system lock / session switch) clears all in-memory keys back to the login screen; optional minimize-to-tray residency
- **Multi-user** — multiple local accounts with isolated notes; brute-force lockout (5 failures / 5 minutes, configurable)
- **11 built-in themes** — light / dark / forest / parchment / violet / high-contrast plus themed chrome skins (metal textures, wallpapers); drop your own JSON into `themes\` to override or add

## Security Design

The full threat model and crypto specification live in [`documents/ProductDocuments/CryptedNotex.ProductDocument.md`](documents/ProductDocuments/CryptedNotex.ProductDocument.md). The essentials:

| Item | Design |
| --- | --- |
| Key derivation | PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte random salt, 32-byte output; parameters stored per user so future iteration bumps upgrade at the next password change |
| Content cipher | AES-256-GCM, fresh random 96-bit nonce per save |
| Blob format | `version(1) ‖ nonce(12) ‖ ciphertext(N) ‖ tag(16)` — a version byte leaves room for future algorithm migration |
| Domain separation | Note bodies and attachments encrypt with different AAD constants, so ciphertext cannot be swapped across tables |
| Login | PBKDF2 hash with its own salt (constant-time verify); transparent upgrade of legacy hashes on successful login |
| In-memory keys | Derived at login, held only by the UI session, zeroed on lock/logout/exit |
| What stays plaintext | Titles, category names, attachment file names, times, word counts — this is what makes listing and title search work; the product document states the boundary explicitly |

## Solution Layout

| Project | Description |
| --- | --- |
| **Rampage.CryptedNotex.Model** | Entities (`Po\`), enums, DTOs |
| **Rampage.CryptedNotex.Common** | Crypto primitives (`CryptoTool`: PBKDF2 derivation, AES-GCM encrypt/decrypt, blob format) |
| **Rampage.CryptedNotex.DAL** | Data access on `SuperDao`; SQL lives only here; `SchemaInitializer` bootstraps the database |
| **Rampage.CryptedNotex.BLL** | Business services (user / category / note / attachment / settings); transaction boundaries |
| **Rampage.CryptedNotex.WinForm** | Forms, three note editors, theme engine, tray, crypto session, auto-lock |

The solution references the [Rampage framework](https://github.com/XTiger1986/Rampage) projects **as sibling-directory project references** (`..\..\Rampage\...`), so clone the two repositories side by side to build.

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later (to build); .NET 8 Desktop Runtime (x64) on target machines (to run the published exe)
- Windows 10 1809+ / Windows 11
- WebView2 Runtime — optional; without it only the Markdown preview is disabled

### Build and Run from Source

```bash
git clone https://github.com/XTiger1986/Rampage.git
git clone https://github.com/XTiger1986/Rampage.CryptedNotex.git
cd Rampage.CryptedNotex/Rampage.CryptedNotex.Net
dotnet run -c Debug --project Rampage.CryptedNotex.WinForm
```

On first launch the schema initializer creates `data\cryptednotex.db` (WAL mode) beside the exe and the first-run wizard guides you through creating an account.

### Publish

Single-file, framework-dependent publish (one ~20 MB exe plus `config\` and `Assets\Markdown\`; `data\` and `__Logs\` are created at runtime):

```powershell
cd installer
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

- Output lands in `Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\bin\Release\net8.0-windows\win-x64\publish\`
- For portable use keep the exe in a writable directory (the database and logs are created next to it); the Inno Setup script `installer\秘密档案.iss` builds a per-user installer without that constraint
- Do not enable `PublishTrimmed` / `PublishAot` — the underlying framework relies on runtime reflection and Reflection.Emit

### Configuration

`config\app.json` beside the exe:

```json
{
  "DatabaseFactory": {
    "DataAccessTypeName": "Rampage.Dapper.DapperDataAccessor, Rampage.Dapper",
    "Clients": [
      { "Name": "CryptedNotex", "Type": "Sqlite", "ConnectionString": "Data Source=data/cryptednotex.db" }
    ]
  },
  "LoginProtection": { "Enabled": true, "MaxFailuresPerAccount": 5, "WindowSeconds": 300 }
}
```

## Data, Backup and Migration

- Everything user-generated lives in `data\cryptednotex.db` — copying that file (or the whole folder) is a complete backup; restoring is copying it back
- **The password is not stored anywhere.** A backed-up database opens only with the original password
- Database schema scripts are versioned under [`documents/DatabaseTablesDesign/`](documents/DatabaseTablesDesign), embedded into the assembly, and applied exactly once each through the `schema_history` ledger at startup (append new timestamped scripts to evolve the schema; the initializer also self-heals if the ledger ever drifts from the actual schema)

## Theming

Eleven themes ship embedded (light, dark, forest, parchment, violet, high-contrast, plus themed-chrome skins such as darkstar/xenomorph with metal textures and wallpapers). To customize, drop a JSON file next to the exe under `themes\` — same `id` overrides a built-in theme, a new `id` adds one. See the product document §8 for the full token list (colors, fonts, category palette).

## License

This project is open sourced under the [MIT License](LICENSE).

## Feedback

- Website: [http://www.1986z.com/](http://www.1986z.com/)
- GitHub: [@XTiger1986](https://github.com/XTiger1986)
- Issues and suggestions are welcome via the [issue tracker](https://github.com/XTiger1986/Rampage.CryptedNotex/issues)
