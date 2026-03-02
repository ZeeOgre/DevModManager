# Mod Onboarding and Stage Deployment Workflow

This document turns the current product vision into a concrete implementation plan for DevModManager (DMM).

## Goals

1. Manage the full mod lifecycle from game-folder discovery through staged promotion.
2. Keep users inside DMM (no manual shell/git scripting required).
3. Support independent per-mod git repositories under a configurable mod root.
4. Support targeted deployment of specific stages to specific game installs.
5. Use links/junctions where possible to avoid duplicate files in game folders.

## Core Terms

- **Mod Root**: User-selected base folder for per-mod repositories (example: `G:\gitrepos\ZeeOgre\GameMods`).
- **Mod Repo**: A dedicated git repo for one mod (example: `sta-DebugMenuFramework`).
- **Stage**: Environment snapshot (`dev`, `test`, `preflight`, `creations`, `nexus`, `prod`).
- **Inventory**: Generated dependency manifests (`*.achlist`, `dmmdeps-*.txt`, dependency JSON).
- **Deployment Target**: A specific game install path + channel (Steam/GamePass/etc.).

## Recommended Repository Layout

Use **one axis for stage isolation** to avoid duplication.

### Preferred model (recommended): stage branches only

```text
<ModRoot>/<gameAbbrev>-<modName>/
  distribution/
    creations/
    nexus/
  loosefiles/
    Data/
    XBox/Data/
    _steam/Source/TGATextures/
  media/
  inventory/
```

In this model, `dev/test/preflight/...` are represented by git branches, **not** by extra `stages/<name>/` folders.

### Why this is preferred

- Avoids duplicate trees and file churn (`stages/*` + branch copies).
- Keeps pathing simple (one stable layout at all times).
- Lets git history represent promotion across stages directly.

### Alternate model (only if needed): branch + stage folders

This should be treated as an advanced fallback only when non-git tooling strictly requires side-by-side stage folders.

## Branching Strategy

### Decision
Create all canonical stage branches at repo creation time, and use branches as the primary stage boundary.

Suggested branch names:
- `stage/dev`
- `stage/test`
- `stage/preflight`
- `stage/creations`
- `stage/nexus`
- `stage/prod`

### Rationale

- Branches are cheap and predictable.
- Eliminates uncertainty later during promotion.
- Gives users immediate visibility into lifecycle topology.

### Guardrail

Treat branch switching as a controlled operation when deployments are linked (see Link Lifecycle).


## FAQ: Branches vs Stage Folders

### Why `stage/*` branches if we already have stages?

Branches are the stage mechanism. The `stage/` prefix is just naming clarity in git, so `stage/dev` and `stage/test` are grouped and easy to filter.

### Is this for putting different stages into different game folders?

Yes. The stage-to-install mapping (Steam vs GamePass, etc.) points each install to a branch/stage, then deployment materializes that branch into that install.

### Will this duplicate files too much?

Not in normal git terms: branches share object storage internally and only diverging commits add data.
The bigger duplication risk comes from maintaining both branch separation and separate `stages/<name>/` directories. That is why branch-only staging is recommended first.

### Can I have a folder automatically tied to a specific branch?

Yes: use `git worktree`. Each worktree directory is pinned to one branch and updates as that branch changes.

Example concept:
- main repo working dir → `stage/dev`
- sibling worktree dir → `stage/test`

DMM can manage these worktrees so users do not run git manually.

### Can `git sync` push/pull game folders?

There is no built-in `git sync` command. In DMM, “sync” should be an orchestrated workflow made of:
1. git fetch/pull/push for repo state, and
2. deployment actions (copy/link/verify) between repo and game folder.

So yes to the workflow, but it is a DMM command that combines multiple git + filesystem operations.


## Program-Wide Git/GitHub Settings

Recommended settings to add (stored in DB-backed program settings):

- `RepoRootPath` (existing): local mod-root working folder.
- `GitHubAccount`: owner/user namespace (example: `ZeeOgre`).
- `GitHubToken`: PAT used for authenticated repo operations.
- `GitHubModRootRepo`: canonical master repo remote URL/path (example: `https://github.com/ZeeOgre/GameMods`).

### Can base repo be derived from account?

Usually yes (`https://github.com/<account>/GameMods`), but keep `GitHubModRootRepo` as explicit override for non-standard names/org layouts.

Note: `program-settings.json` is bootstrap-only (database path metadata); operational settings should be stored in the local database.


## Local Folder Strategy and Submodule Timing

Desired local working layout is supported:

```text
<ModRoot>/Starfield/<modName>/
```

(Equivalent for other games using their game folder key.)

### Important guardrail

Do **not** import mod files into a folder that has not yet been bootstrapped as a git repo/submodule working tree.

Recommended order:
1. Create remote per-mod repo (`{gameAbbrev}-{modName}`).
2. Add repo as submodule in master `GameMods`.
3. Sync master repo/submodules locally so `<ModRoot>/<Game>/<modName>` is a real git working tree.
4. Only then run copy-first onboarding import into `loosefiles/Data`.

## Onboard Transaction Sequence (Per Mod)

For each new mod `{gameAbbrev}-{modName}`:

1. Ensure/create remote mod repo under the configured account.
2. Ensure local mod repo exists under local mod root.
3. Seed folder structure in mod repo (`loosefiles/`, `inventory/`, `distribution/`, etc.).
4. Create stage branches (`stage/dev`, `stage/test`, ...).
5. Add mod repo to master `GameMods` as submodule and commit pointer update in master repo.
6. Sync/pull master repo so submodule folder is present locally.
7. Perform initial file import (copy-first) into mod repo.
8. Run `dmmdeps` for primary plugin; place outputs in `inventory/`.
9. Commit/push mod repo stage branch updates.
10. Commit/push master repo submodule pointer update.

This confirms your understanding is correct; the only key addition is explicit commit/push ordering for **both** repos (mod first, then master pointer update).

## End-to-End Onboarding Workflow

1. **Create mod repo from DMM**
   - Validate game abbreviation and mod slug.
   - Create local folder under Mod Root.
   - `git init`, set remote (if provided), initial commit.

2. **Seed standard folder structure**
   - Create all core folders listed above.
   - Commit “seed layout”.

3. **Create stage branches**
   - Create all `stage/*` branches.
   - Optionally base all branches from `main` seed commit.

4. **Set active stage context**
   - Checkout selected stage branch.
   - Persist active stage in DMM metadata.

5. **Generate inventory from dmmdeps**
   - DMM runs `dmmdeps` internally.
   - Store outputs in `inventory/`.

6. **Populate repo content**
   - Copy files listed by dependency output into `loosefiles/` (branch layout paths only).
   - Commit with inventory artifacts.

7. **Sync to remote**
   - Push current branch.
   - Optionally push all stage branches.

## Promotion Model (Stage-to-Stage)

Promotion should be explicit and auditable:

- Source stage selected by user.
- Target stage selected by user.
- DMM performs either:
  - merge/cherry-pick style promotion, or
  - deterministic file copy + generated commit in target stage.

Recommended initial implementation: deterministic copy + generated commit message with summary and file counts.

## Deployment Model (Stage-to-GameFolder)

Support per-install stage mapping, e.g.:

- Starfield Steam → `stage/dev`
- Starfield GamePass → `stage/test`

Store mappings in program settings and allow one-click deploy/sync per target.

## Link Lifecycle (Hardlinks/Junctions/Symlinks)

### Recommended sequence

1. Initial import copies files from game folder into repo.
2. Validation phase confirms parity and completeness.
3. DMM can replace copied game-folder files with links to stage content.

### Branch-switch safety

Linked deployments and branch switches can conflict. Use one of these approaches:

1. **Preferred**: one worktree per deployed stage, so no checkout is needed for active deployments.
2. **Fallback**: if using one working directory, before checkout to another stage branch:
   - remove or disable links for affected target(s),
   - perform branch switch,
   - re-establish links for the intended stage mapping.

This should be encapsulated in one “safe stage switch” command.

## GitHub Master Repo + Submodule Integration

For each new mod repo:

1. Ensure mod remote exists and is pushed.
2. Open/update master repo (`GameMods`).
3. Add mod repo as submodule at expected path.
4. Commit/push master repo submodule pointer update.

DMM should provide:

- “Add to master repo as submodule” action during onboarding.
- “Full sync submodules” action for routine maintenance.


## Git Control Libraries / Tooling Recommendation

DMM can implement git control in two viable ways. Recommend a hybrid with CLI-first fallback safety:

### Option A: Embedded library (`LibGit2Sharp`)

Pros:
- Full in-process control (clone/branch/commit/fetch/push/worktree-related repository ops).
- Structured exceptions and API-level progress callbacks.
- Easier to unit test than shelling out for every operation.

Cons:
- Native dependency footprint (`libgit2`), version compatibility, and platform packaging concerns.
- Some advanced workflows can lag behind latest git CLI behavior.

### Option B: Git CLI orchestration (`git` process execution)

Pros:
- Matches user expectations exactly (same semantics as command-line git).
- Immediate access to newer git features.
- Simplifies troubleshooting by reproducing commands directly.

Cons:
- Requires robust process execution, stderr parsing, and credential handling.
- More brittle if command output formats change.

### Recommended implementation strategy

1. **Primary path**: use `LibGit2Sharp` for core repo lifecycle actions:
   - init/clone
   - branch create/checkout
   - commit/status/diff
   - fetch/pull/push
2. **Fallback/advanced path**: call git CLI for operations that are easier or more reliable via command line (initially `git worktree` flows and submodule edge cases).
3. Wrap both behind a DMM abstraction such as `IGitService` so the UI layer remains tool-agnostic.

### Supporting libraries/services to consider

- **Credential + secrets**:
  - system credential manager integration (Windows Credential Manager first)
  - optional PAT storage through secure OS-backed secrets
- **GitHub API** (optional but useful):
  - `Octokit` for repo bootstrap, remote existence checks, and submodule onboarding helpers
- **Process runner**:
  - hardened command runner with timeout, cancellation, structured logs, and redacted sensitive output

### Minimum capability matrix to implement first

1. `InitRepo`
2. `SetRemote`
3. `CreateStageBranches`
4. `CheckoutStage`
5. `CommitAll(message)`
6. `Fetch/Pull/Push`
7. `WorktreeAdd/WorktreeRemove`
8. `SubmoduleAdd/SubmoduleUpdate`

This gives enough surface area for onboarding + deployment while keeping the git subsystem intentionally narrow.

## DMM Feature Backlog (Programmatic Capabilities)

1. Create mod repository (+ optional remote bootstrap).
2. Seed canonical folder structure.
3. Create and manage stage branches.
4. Set active stage branch and persist stage context.
5. Run dmmdeps and capture inventory outputs.
6. Populate stage content from dependency output.
7. Push/pull/sync controls at mod and global level.
8. Safe branch switching with link teardown/rebuild.
9. Targeted deployment of stage to specific game installs.
10. Import/sync repos to a new machine.
11. Add/sync submodules in master repo.

## Suggested Delivery Order

### Milestone 1: Repo Lifecycle
- Create repo
- Seed folders
- Stage branch creation/checkouts
- Push/pull/sync operations

### Milestone 2: Inventory + Populate
- dmmdeps integration
- Inventory persistence
- Populate files into repo layout

### Milestone 3: Promotion + Deployment
- Stage promotion operations
- Target install stage mappings
- Copy-based deployment first

### Milestone 4: Link Optimization
- Link-based deployments
- Safe switch orchestration
- Link integrity verification and repair

### Milestone 5: Multi-Repo Coordination
- Submodule management in master repo
- New-machine bootstrap and mass sync

## Open Design Questions

1. Should promotion be merge-based, copy-based, or both?
2. Which deployment artifacts are stage-excluded by policy (if any)?
3. Should `inventory/` be committed always, or configurable per stage?
4. What is the canonical source of game-install stage mapping (global settings vs per-mod override)?

## Initial Recommendation Summary

- **Yes**: Create stage branches during repo creation.
- **Yes**: Use one consistent folder structure and keep stage isolation in branches (not duplicate `stages/*` trees).
- **Yes**: Keep git orchestration fully inside DMM, with a `LibGit2Sharp`-first abstraction and CLI fallback for worktrees/submodule edge cases.
- **Yes**: Start with copy deployment, then add links once validated.
- **Yes**: Use safe-switch logic to handle link teardown/rebuild around branch changes.



## First-Launch / Brand-New Setup Flow (Detailed)

This section captures the expected end-to-end bootstrap sequence for a new machine/profile.

### 1) First launch + database bootstrap

- **Current phase assumption**: full preproduction. Schema/seed changes can be treated as baseline; migration scripts are not required yet because no fielded databases are expected.

- If local DB is missing, create it in `%LOCALAPPDATA%` using:
  - `database_schema.sql`
  - `database_seed.sql`
- Seed should include known game catalog entries and preferred abbreviations.

#### Recommended abbreviation seeds

**Bethesda focus**
- `FO4` → Fallout 4
- `SKYRIM` → Skyrim
- `STARFIELD` → Starfield

**Known/non-focus catalog**
- `CP2077` → Cyberpunk 2077
- `TLOU1` → The Last of Us Part I
- `TLOU2` → The Last of Us Part II
- `NMS` → No Man's Sky
- `CONTROL` → Control
- `MINECRAFT` → Minecraft
- `GTA5` → Grand Theft Auto V

### 2) First-run settings prompt (required)

If not already configured, prompt for:
- local `ModRoot` git folder (create if missing)
- `GitHubAccount`
- `GitHubToken`
- `GitHubModRootRepo`

### 3) Game catalog/install setup

Support adding/editing a game record with:
- game name
- supported game stores
- store app IDs (allow multi-value / aliases where needed)
- default relative modroot path
- game abbreviation

### 4) Create per-game folders under ModRoot

Example:
- `ModRoot/Fallout4`
- `ModRoot/Skyrim`
- `ModRoot/Starfield`

### 5) Scan installs + populate game-folder dropdown

- Populate install list from store scanners.
- Keep selection scoped by game for mod-level actions.
- Allow favorites and sorting preference (future enhancement).

### 6) Scan inside selected game folder for mods

- Exclude known base game plugins.
- Present candidates for onboarding stage assignment.

### 7) On save/apply for selected mods

For each selected mod:
1. Ensure remote per-mod repo exists (`{gameAbbrev}-{modName}`).
2. Ensure mod repo is added as submodule to master modroot repo.
3. Sync master/submodules locally so working tree exists.
4. Copy initial files into mod repo (`loosefiles/Data`, copy-first).
5. Run `dmmdeps` for primary plugin and store outputs in `inventory/`.
6. Commit/push mod repo updates.
7. Commit/push master repo submodule pointer update.
8. Create/check out active stage branch.


### Base-game `.mat` inventory strategy (for dmmdeps filtering)

Goal: avoid copying Creation Kit/base-game material files that already exist in base-game content.

Recommended approach:
1. **Fast catalog pass first**: at game-scan time, read file-path catalogs from:
   - `GAMEFOLDER\Tools\ContentResources.zip`
   - base archive index/catalog for `* - Materials.ba2` (and optional other base archives if needed)
2. Build an in-memory `HashSet<string>` of normalized base `.mat` paths (case-insensitive, `/` + `\` normalized).
3. When `dmmdeps` returns candidate material dependencies, include only paths **not** in that base set.
4. Persist discovered base `.mat` paths in DB (cache table keyed by game + source + signature/timestamp) so subsequent runs do not re-extract unless source changed.

Cache invalidation guidance:
- Rebuild cache only when `ContentResources.zip` or `* - Materials.ba2` signature changes (size + write-time or hash).
- In preproduction we can add this table directly to schema/seed baseline (no migration track required yet).

Fallback:
- If catalog extraction is slow/unavailable, use the DB cache as authoritative for that run and queue background refresh.

### Implementation state vs your checklist

- **Mostly in place now**: install scan, candidate scan, base-plugin exclusion, copy-first import path, settings scaffolding.
- **Pending/next**: automatic repo creation, submodule add/sync orchestration, dmmdeps execution pipeline (including base-game `.mat` filtering/cache), branch automation, favorites/clustered game dropdown UX.
