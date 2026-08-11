# ModScope Task Completion

- Read `AGENTS.md`, `docs/design.md`, and `docs/future-vision.md` before design work.
- Confirm the user-requested path allowlist before editing.
- For memory work, review every generated memory as a draft.
- Reject secrets, credentials, personal paths, raw logs, short-term history, product-spec duplication, and stale details.
- Confirm `AGENTS.md` and `docs/` remain the source of truth.
- Run `get_current_config` after Serena configuration changes.
- Confirm code-edit, shell, JetBrains mutation, and project-removal tools are not active.
- Run `serena memories check`.
- Run `git check-ignore -v` for local Serena configuration and generated directories.
- Run `git diff --check`.
- Verify that only explicitly selected memory paths changed.
- Stage only the reviewed memory paths.
- After publishing, verify that the remote branch SHA equals local HEAD.
