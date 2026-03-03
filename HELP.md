# DevModManager Help

## Main
- Use **Scan Game Folder** to discover current mods in the selected game installation.
- Use **Focus** on a row to open mod-specific workflow operations.
- Use bottom **Git Control** buttons for quick push/sync/pull actions.

## ModFocus
- Right-click action buttons to choose detailed sub-operations.
- Convert Type supports ESP/ESM/ESL conversion paths.
- Archive and icon operations include platform-specific options.
- Use **Open Stage Folder** (right-click) to open a stage-specific folder.
- Use **Git Control** for mod-level push/sync/pull.

## Planning Notes
- See `docs/mod-onboarding-stage-workflow.md` for the proposed onboarding, stage-branch, deployment, and submodule workflow model.


## GitHub Token Setup (PAT)
- DMM git automation should use a GitHub Personal Access Token (PAT) for private repo operations.
- Minimum recommended scopes for private mod repos:
  - **repo** (full control of private repositories)
  - optionally **read:org** if organization membership checks are needed.
- Create token in GitHub:
  1. Profile → **Settings** → **Developer settings** → **Personal access tokens**.
  2. Prefer **fine-grained token** when possible.
  3. Set expiration and restrict repository access to needed repos.
- Store token in DMM program settings for git operations.
- Security note: avoid broad scopes and rotate token periodically.


## Onboarding Guardrails
- Scan/Apply onboarding expects GitHub settings to be configured (`GitHubAccount`, `GitHubToken`, `GitHubModRootRepo`).
- For each mod, DMM should establish repo/submodule bootstrap before importing files.
- Import target path is local mod repo `loosefiles/Data` (copy-first).

- Program-wide operational settings are persisted in the local DMM database; `program-settings.json` is reserved for database bootstrap metadata only.


## New Machine Setup (Repo + Mods + Deploy Targets)
When moving to a new machine, set up the mod repo root first, then configure DMM settings before onboarding/sync.

1. Choose your local mod repo root folder (example: `D:\ModRepos` or `S:\devmods`).
2. Run the bootstrap script to clone or sync the root repo + submodules:
   - `pwsh ./scripts/setup-mod-root.ps1 -RepoRoot "D:\ModRepos" -RemoteUrl "https://github.com/<org>/<root-repo>.git"`
   - Add `-ForceReclone` if the local folder is broken and should be recreated.
   - Add `-SkipLfs` if the machine does not need LFS assets yet.
3. In DMM Program Settings set:
   - `Mod Repo Root` to the same folder you passed as `-RepoRoot`.
   - `GitHubAccount`, `GitHubToken`, and `GitHubModRootRepo`.
4. Verify game install path(s):
   - **GameRoot should be where the executable + `Data` live**.
   - For Xbox/GamePass layouts, this is typically `<Install>\Content` (not the outer library folder).
   - For Steam/Epic/GOG, this is usually the main game folder directly.
5. Run Scan/Sync/Onboarding in DMM.

If you need to wipe experiment repos and start clean:
- Preview: `pwsh ./scripts/purge-mod-repos.ps1 -ModRepoRoot "D:\ModRepos" -DryRun`
- Execute: `pwsh ./scripts/purge-mod-repos.ps1 -ModRepoRoot "D:\ModRepos" -Force`
