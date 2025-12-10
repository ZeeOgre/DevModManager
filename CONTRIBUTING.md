# Contributing Guidelines

## Purpose
This project follows strict source code hygiene rules to maintain cross-platform compatibility, consistent tooling behavior, and avoid accidental use of characters that look similar but are different (for example, Cyrillic vs Latin characters). This document includes the policy to disallow Cyrillic characters in source files and related artifacts.

## Guidelines
- Do not use Cyrillic (or any non-Latin) characters in identifiers, variable names, string literals, comments, or any source file content unless explicitly required for user-facing localization. Use only ASCII characters for identifiers and code.
- For user-facing text that must contain non-Latin characters, keep them in resource files or localization files and document the reason in a comment.

## Enforcement
- All pull requests should be reviewed for accidental non-Latin characters.
- CI will include a check that scans the repository for Cyrillic characters in source files. The check will fail if any are found.
- Developers are encouraged to install a pre-commit hook that rejects commits containing Cyrillic characters in code files.

## How to handle violations
- If a non-Latin character is found, replace it with the correct Latin character or ASCII equivalent.
- If characters are intentionally required, document the justification in the commit message and the file containing them.

## Examples
- BAD: `string pe?Rel = "...";`  // contains Cyrillic '?'
- GOOD: `string pexRel = "...";`

## Adding the check locally
Include a simple script in the repository (e.g. `tools/check-cyrillic.sh`) that scans text files for Cyrillic ranges and can be used in pre-commit hooks or CI.

## Contact
If unsure, open an issue or ask during code review for guidance.