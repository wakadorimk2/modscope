# Suggested Commands

- Use PowerShell with `-LiteralPath` for known paths.
- List tracked files with `rg --files -g '! .git/**'`.
- Inspect scope with `git status -sb`.
- Review whitespace with `git diff --check`.
- Verify Serena local exclusions with `git check-ignore -v -- .serena/project.local.yml .serena/cache .serena/logs`.
- Verify memory references with `serena memories check`.
- Review Serena safety state with `get_current_config`.
- Add memories only with explicit paths, for example `git add -- .serena/memories/<reviewed-memory>.md`.
- No project build, test, lint, or format command exists during the design phase.
