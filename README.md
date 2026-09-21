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

## Settings

Command Palette → *Claude Tasks* → *Settings*:
- **Refresh interval**: 30 sec, 1, 2, 5 min (default 1 min). Only changed `todo.md` files are re-read.
- **Extra scan roots**: `;`-separated folders, environment variables allowed.
- **Show completed**: show finished projects and tasks (default on).

## Requirements

- Windows 10 19041+ / Windows 11, PowerToys with Command Palette **0.9 or later** (Dock support)

## Build from source

Needs the .NET 10 SDK. A full Windows SDK / Visual Studio is **not** required.

```powershell
dotnet test tests\ClaudeTasks.Tests -p:Platform=x64      # unit tests
.\scripts\dev-deploy.ps1                                 # build + register (Developer Mode on)
.\scripts\dev-deploy.ps1 -Remove                         # unregister
```

After deploying, run **Reload** in Command Palette. If the band does not appear by itself, add it from the Dock's edit mode.

### MSIX package

```powershell
.\scripts\pack.ps1 -Sign        # dist\...\ClaudeTasks_<ver>_x64.msix + dist\ClaudeTasksDev.cer
```

The package is signed with a self-signed certificate (`CN=ClaudeTasksDev`). On each target machine, trust it once from an elevated prompt, then install:

```powershell
Import-Certificate .\ClaudeTasksDev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage .\ClaudeTasks_0.1.0.0_x64.msix
```

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
