# Contributing Guidelines

## Purpose
This project follows strict source code hygiene rules to maintain cross-platform compatibility, consistent tooling behavior, and avoid accidental use of characters that look similar but are different (for example, Cyrillic vs Latin characters). This document includes the policy to disallow Cyrillic characters in source files and related artifacts.

## Guidelines
- Do not use Cyrillic (or any non-Latin) characters in identifiers, variable names, string literals, comments, or any source file content unless explicitly required for user-facing localization. Use only ASCII characters for identifiers and code.
- For user-facing text that must contain non-Latin characters, keep them in resource files or localization files and document the reason in a comment.

## Naming conventions
- Avoid choosing type names that are identical to or easily confused with .NET BCL types (for example, avoid naming a type exactly `FileSystem` in a way that will commonly conflict with `System.*` APIs). This prevents accidental confusion and the need for frequent fully-qualified type names.
- Prefer the interface-first pattern for file/IO abstractions:
  - Interface: `IFileSystem` (define in `DMM.Core.IO`)  - Concrete implementations: prefer names that clarify intent or origin, such as `DefaultFileSystem`, `NativeFileSystem`, or `FileSystemImpl`. This reduces the chance of name collision with BCL types while keeping call sites clear: `IFieSystem` remains the injected contract.
- If you must keep a short concrete name (for example `FileSystem`), prefer placing it in a clear project-specific namespace such as `DMM.Core.IO` and use dependency injection to avoid referencing the concrete type directly.
- For call-site disambiguation prefer one of these approaches rather than renaming everywhere:
  - Use a namespace alias: `using SysIO = System.IO;` then call `SysIO.File` when you need the BCL type.
  - Fully qualify the BCL type where necessary: `global::System.IO.File.ReadAllText(...)`.
  - Register the concrete in DI with the interface (example: services.AddSingleton<IFileSystem, DefaultFileSystem>()), then consume `IFileSystem`.

## Enforcement
- All pull requests should be reviewed for accidental non-Latin characters and for naming collisions with BCL types.
- CI will include a check that scans the repository for Cyrillic characters in source files. The check will fail if any are found.
- Additive CI checks may also flag new types that match common BCL names (e.g., `FileSystem`) and recommend renaming or adding a namespace alias at the call site.
- Developers are encouraged to install a pre-commit hook that rejects commits containing Cyrillic characters in code files.

## How to handle violations
- If a non-Latin character is found, replace it with the correct Latin character or ASCII equivalent.
- If characters are intentionally required, document the justification in the commit message and the file containing them.
- If a new type collides with a BCL type and causes confusion, prefer renaming the project type to a clarification suffix (for example `DefaultFileSystem`) or add a namespace alias at call sites.

## Examples
- BAD: `string pe?Rel = "..."`;  // contains Cyrillic '?'
- GOOD: `string pexRel = "..."`
- BAD: declaring a widely-consumed concrete type named `FileSystem` in the global namespace that forces callers to write `global::System.IO.File` frequently.
- GOOD: define `IFileSystem` and `DefaultFileSystem` in `DMM.Core.IO` and register via DI so callers depend on `IFileSystem`.

## Adding the check locally
Include a simple script in the repository (e.g. `tools/check-cyrillic.sh`) that scans text files for Cyrillic ranges and can be used in pre-commit hooks or CI.

## Contact
If unsure, open an issue or ask during code review for guidance.