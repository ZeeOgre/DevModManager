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
- **Yes**: Keep git orchestration fully inside DMM.
- **Yes**: Start with copy deployment, then add links once validated.
- **Yes**: Use safe-switch logic to handle link teardown/rebuild around branch changes.
