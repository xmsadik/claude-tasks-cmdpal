# Claude Tasks for Command Palette

A PowerToys Command Palette extension that puts the progress of your local Claude Code projects in the **Dock**, read from each project's `tasks/todo.md`.

> Unofficial community project. Not affiliated with or endorsed by Anthropic or Microsoft.

- **In the Dock:** overall progress across all projects, e.g. `📋 78%  9 projects · 48 open` (✅ when nothing is open).
- **Click it:** a table of every project (progress bar, %, done/total, last update) and the open tasks of the project you're working on right now, grouped by section.
- **Browse all tasks:** a full Command Palette list of projects; open one to see its tasks grouped by `##` section, ☐ open / ✓ done. Open the `todo.md` or the project folder with one click.

The extension is **read-only**: it never writes to your files and makes no network calls.

## How projects are found

| Source | What it gives |
|---|---|
| `~/.claude.json` → `projects` | Every folder you've started Claude Code in |
| *Extra scan roots* setting (default `%USERPROFILE%`) | Immediate subfolders that contain `tasks/todo.md`, for projects you worked on from a parent folder |
| Newest transcript in `~/.claude/projects` | Which project is "active" (falls back to the most recently edited `todo.md`) |

Only folders that have a `tasks/todo.md` are shown. The root scan can also pick up a non-Claude folder that happens to have `tasks/todo.md`; remove the root or narrow it in the settings if that's unwanted. `CLAUDE_CONFIG_DIR` is honoured.

## todo.md format

Standard Markdown task lists:

```markdown
# Project title

## Phase 1
- [x] done task
- [ ] open task
### Sub-heading (stays inside "Phase 1")
1. [ ] numbered tasks work too
```

- `[x]` / `[X]` is done; any other single character (` `, `-`, `~`, `/`) counts as open.
- `##` headings are sections; `###` and deeper are labels inside the section. Tasks before the first `##` go under *General*.
- Checkboxes inside code fences or blockquotes are ignored. Nested tasks each count once.
- Percentages round down, so 100% means every task is done.

## Requirements

- Windows 10 19041+ / Windows 11, PowerToys with Command Palette **0.9 or later** (Dock support)
- Claude Code projects that keep a `tasks/todo.md` (see [todo.md format](#todomd-format))

## Installation

### 1. Turn on the Dock

1. Install or update [PowerToys](https://github.com/microsoft/PowerToys/releases) and make sure **Command Palette** is enabled in PowerToys Settings.
2. Open Command Palette (default <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Space</kbd>) → **Settings** → **Dock (Preview)** → turn on **Enable Dock**.

### 2. Download

From the [latest release](https://github.com/xmsadik/claude-tasks-cmdpal/releases/latest) download:
- `ClaudeTasksDev.cer`
- the package for your CPU: `ClaudeTasks_<version>_x64.msix` (Intel/AMD) or `ClaudeTasks_<version>_arm64.msix` (Arm, e.g. Snapdragon). Not sure? Run `$env:PROCESSOR_ARCHITECTURE` in PowerShell: `AMD64` → x64, `ARM64` → arm64.

### 3. Trust the certificate (once per machine)

The package is signed with a self-signed certificate, so Windows has to be told to trust it. In **PowerShell as Administrator**, in the download folder:

```powershell
Import-Certificate .\ClaudeTasksDev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

### 4. Install

In a normal PowerShell window (or double-click the `.msix` and choose *Install*):

```powershell
Add-AppxPackage .\ClaudeTasks_0.1.0.0_x64.msix
```

### 5. Show it in the Dock

1. Open Command Palette and run **Reload** so it picks up the new extension.
2. The *Claude Tasks* band usually appears in the Dock by itself. If it doesn't, search for **Claude Tasks** in Command Palette, open its context menu and run **Pin to Dock** (choose the side you like, e.g. *Right*).
3. Click the band for the summary; **Browse all tasks** opens the full list. Settings: search **Claude Tasks** → *Settings*.

### Update

Download the newer `.msix` and run `Add-AppxPackage` again; the certificate step isn't needed again. Then **Reload** Command Palette.

### Uninstall

```powershell
Get-AppxPackage ClaudeTasks | Remove-AppxPackage
# optional, as Administrator: remove the trusted certificate
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=ClaudeTasksDev' | Remove-Item
```

### Troubleshooting

| Symptom | Fix |
|---|---|
| `0x800B0109` / "the root certificate … is not trusted" on install | Step 3 was skipped or not run as Administrator. |
| `0x80073CFB` / "a package with the same identity is already installed" | A development build is registered: `Get-AppxPackage ClaudeTasks \| Remove-AppxPackage`, then install again. |
| Band doesn't appear | Check **Enable Dock** is on, run **Reload**, then use **Pin to Dock** as in step 5. |
| Band shows `—` / *No tasks found* | No project with `tasks/todo.md` was found. Projects come from `~/.claude.json` and the **Extra scan roots** setting; add the parent folder of your projects there. |
| A project is missing | It was never opened in Claude Code *from its own folder*. Add its parent folder to **Extra scan roots**. |
| An unrelated folder shows up | It has a `tasks/todo.md` under a scan root. Narrow **Extra scan roots**. |
| Clicking the band opens the palette instead of the flyout, or it stops updating | Command Palette host issues, see [Known host issues](#known-host-issues). **Reload** fixes both. |

## Settings

Command Palette → *Claude Tasks* → *Settings*:
- **Refresh interval**: 30 sec, 1, 2, 5 min (default 1 min). Only changed `todo.md` files are re-read.
- **Extra scan roots**: `;`-separated folders, environment variables allowed.
- **Show completed**: show finished projects and tasks (default on).

## Build from source

Needs the .NET 10 SDK. A full Windows SDK / Visual Studio is **not** required.

```powershell
dotnet test tests\ClaudeTasks.Tests -p:Platform=x64      # unit tests
.\scripts\dev-deploy.ps1                                 # build + register (Developer Mode on)
.\scripts\dev-deploy.ps1 -Remove                         # unregister
```

After deploying, run **Reload** in Command Palette. If the band does not appear by itself, use **Pin to Dock** (see [Installation](#5-show-it-in-the-dock)).

### MSIX package

```powershell
.\scripts\pack.ps1 -Sign        # dist\...\ClaudeTasks_<ver>_x64.msix + dist\ClaudeTasksDev.cer
```

`-Platform ARM64` builds the Arm package. The first `-Sign` run creates a self-signed `CN=ClaudeTasksDev` code-signing certificate in `Cert:\CurrentUser\My` and reuses it afterwards. Install the result as described in [Installation](#installation).

## Layout

```
src/ClaudeTasks/        Command Palette extension (Dock band, flyout, list pages, settings, TaskStore)
src/ClaudeTasks.Core/   Plain .NET library: todo.md parser, project discovery, active-project detection
tests/ClaudeTasks.Tests xUnit tests for Core
scripts/                dev-deploy.ps1, pack.ps1
```

## Known host issues

These are Command Palette bugs, not bugs in this extension:
- [#50367](https://github.com/microsoft/PowerToys/issues/50367): after the host releases an idle extension, clicking a band opens the palette instead of the flyout.
- [#49688](https://github.com/microsoft/PowerToys/issues/49688): bands stop repainting after roughly 41 hours of uptime.

Running **Reload** in Command Palette works around both.

## See also

[claude-usage-cmdpal](https://github.com/xmsadik/claude-usage-cmdpal): Claude subscription usage in the Dock.

## License

[MIT](LICENSE). Parts derived from the PowerToys extension template are © Microsoft, MIT; see [NOTICE](NOTICE).
