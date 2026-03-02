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
