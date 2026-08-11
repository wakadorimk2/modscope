# ModScope Conventions

- Separate `verified`, `inferred`, `uncertain`, and `diagnostic`.
- Preserve raw information when an operation, attribute, XML element, or site structure is unknown.
- Treat MO2 data as source of truth.
- Treat snapshots, indexes, caches, normalized metadata, search results, conflict results, and read models as regenerable derived data.
- Keep the Browsing Layer, Local Mod Knowledge, agent browser, MO2 Adapter, Game Adapter, Site Adapter, and write plane separate.
- Keep read-only behavior as the default for MO2 integration.
- Keep the Web page as the primary surface. Reveal local context progressively.
- Keep memories concise and operational. Do not copy product specifications into memories.
- Use relative repository paths in memory references.
- Use `mem:` references for links between memories.
