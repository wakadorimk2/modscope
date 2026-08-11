# ModScope Core

- Canonical operational rules: `AGENTS.md`.
- Product definition and current design: `docs/design.md`.
- Future direction and deferred work: `docs/future-vision.md`.
- Read `AGENTS.md` and both design documents before changing design decisions.
- Current phase is design-only.
- Do not add implementation code, dependencies, manifests, GUI, browser engine, CLI, build setup, or MO2 writes without an explicit phase change.
- Treat `.serena/project.yml` and `.serena/.gitignore` as shared Serena configuration.
- Treat `.serena/project.local.yml` and `.serena/cache/` as local or regenerated data.
- Memories are reviewed operational aids. They never override `AGENTS.md` or `docs/`.
- Read `mem:tech_stack` for current implementation-state constraints.
- Read `mem:suggested_commands` for Windows and Serena verification commands.
- Read `mem:conventions` for evidence and boundary rules.
- Read `mem:task_completion` for the completion checklist.
- Read `mem:memory_maintenance` for memory graph and maintenance rules.
