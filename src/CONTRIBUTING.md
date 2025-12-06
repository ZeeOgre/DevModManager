# CONTRIBUTING.md

## Project core story

This project is a development-focused mod manager to control and observe an in-development modding environment. The primary user journey (core story) that the project must support before v1.0:

- Set an Active Game Folder (per-session or persisted selection).
- View a grid of in-development mods and their current deployment state for the selected game profile.
- Identify a single mod as the actively monitored mod (radio button). When a mod is monitored, show monitored files and quick actions.
- For mods in "update mode", surface external IDs (Bethesda and Nexus) in the grid.
- Provide a side panel/window with mod-specific activities (clean audio, create BA2, update scan lists, promote stage, backup/restore, convert file types, view monitored files, view Git changes).

These behaviors form the minimum viable product (MVP) for v1.0 — they must be stable, discoverable and well-tested.

## 1.0 (MVP) - Must have

Low/Medium complexity items that should be completed before tagging v1.0:

- Active Game Folder selector and persistence (user preference per machine or profile).
- Mods grid that reflects deployment state for the selected folder/profile.
- Mark one mod as monitored (persist if needed). Only one monitored mod per profile.
- Show Bethesda/Nexus IDs when the mod is in update mode (data comes from `ExternalIds`).
- Side panel with quick actions wired to ViewModel commands:
  - Promote to stage
  - Backup/Restore (basic file copy with safe destination)
  - Clean audio files (simple pattern-based cleanup)
  - Create BA2 archives (invoke BA2 tool or internal packer if available)
  - Show monitored files (list of files for the monitored mod)
- Command-based UI (ICommand) on ViewModels; minimal code-behind (only for view-only concerns). No reflection-based command lookup in code-behind.
- Async operations for long-running tasks with progress/cancellation support.
- DB migration plan and a single SQL migration file for DB changes needed by MVP.
- Unit tests for key ViewModel logic and database access abstractions.

## 2.0 (Defer) - Nice to have / Advanced

Higher complexity features to defer to post-1.0 releases:

- Full Git integration showing diffs and staged/unstaged changes per mod.
- Live filesystem monitoring with high-frequency updates and robust debouncing.
- Deep integration with external editors/tools (in-app diffing, merge UI).
- Expandable plugin architecture for custom side-panel tasks.
- Advanced BA2 optimization and per-platform packaging automation.
- Distributed or cloud-backed sync of profiles and repos.

## Database guidance

- Current schema contains core tables: `ModItems`, `ExternalIds`, `FileInfo`, `FileStage`, `FileFolderDeploymentState`, etc. For the MVP add or confirm:
  - `ModItems.IsMonitored INTEGER` (0/1) or a separate `MonitoredMod` table keyed by GameProfileId (prefer latter if multi-profile support is needed).
  - Indexes on `ModItems(ModFolderPath)`, `ExternalIds(BethesdaId)`, `ExternalIds(NexusId)` for fast lookup.
  - Migration strategy: create idempotent SQL migration files per release. Keep PRs small and include SQL for both upgrade and downgrade where feasible.

## Architecture & implementation notes

- Prefer MVVM: all actions exposed as commands on `ModItemViewModel` or a higher-level `ModControlViewModel`.
- Avoid code-behind logic that uses reflection to discover commands. Explicitly expose `OpenFolderCommand`, `OpenStageFolderCommand`, and other commands in the VM where applicable.
- Use async/await for disk-bound and I/O tasks. Surface progress and allow cancellation tokens.
- Batch database updates inside transactions; keep the UI thread responsive by performing IO on background threads.
- Use dependency injection for services (file operations, DB access, git, BA2 packing) to enable unit testing and swapping implementations.
- Keep long-running tasks off the UI thread and report status via observable properties.
- Follow .editorconfig rules (project uses .NET 8 / C# 13) — add .editorconfig next if missing.

## UI/UX

- The Mods grid should be data-driven and operate purely via bindings to the `Mods` collection of `ModItemViewModel`.
- Side panel should be a `ModControlWindow` or flyout bound to a selected `ModItemViewModel` and provide clearly labeled commands.
- Use confirmation dialogs for destructive operations and undo where feasible for backups/restores.

## Developer workflow & PR checklist

Before opening a PR:

- Ensure changes compile and run on .NET 8.
- Add or update unit tests for ViewModel logic and service abstractions.
- Include DB migration SQL in `sql/migrations/` and document schema changes in the PR description.
- If adding commands or public properties on view models, update view XAML or add tests demonstrating bindings.
- Keep changes small and focused. Explain reasoning in the PR description and link to issue or design doc.

PR checklist (must be completed):
- [ ] Build passes locally (Release and Debug).
- [ ] Unit tests added/updated and pass.
- [ ] DB migration included and tested.
- [ ] No reflection-based UI command lookups in code-behind.
- [ ] UX has confirmation for destructive actions.

## Communication

For design questions or scope changes, open an issue titled "Design: ..." and link the PR. For emergency hotfixes, label the PR with `hotfix` and leave a short justification.


-- End of contributing guidelines