# Requirements local resolution research

## Position in the repository

This document records a read-only local-evidence follow-up to the Web-only
Requirements / Dependencies baseline for the Smorgasbord modlist.

This is a research snapshot. It does not implement a dependency resolver,
production schema, or runtime dependency assertion.

## Observation and provenance

- Observation time: `2026-08-14T10:46:58Z`
- Web-only baseline: external snapshot at
  `C:\ModScopeLab\analysis\requirements-research\dataset.csv`
- Local source: the MO2 instance resolved from
  `C:\ModScopeLab\wabbajack\smorgasbord`
- Requested `C:\ModScopeLab\downloads\smorgasbord`: not observed
- Configured download directory: `C:\ModScopeLab\wabbajack\smorgasbord\downloads`
- Local scan mode: read-only

The Web-only baseline raw CSV and JSON are not included in this repository.
The baseline path above is provenance for the external research snapshot.

## Scope

| Measure | Observed value |
|---|---:|
| Web-only unresolved target observations | 33 |
| Unique raw target names | 30 |
| Local MO2 packages | 504 |
| Packages with `ModInfo.xml` | 493 |
| Archives | 478 |
| MO2 `.meta` files | 475 |
| Packages with multiple Modlets | 5 |
| Explicit local dependency XML metadata | 0 |

## Resolution result

| Status | Count | Meaning |
|---|---:|---|
| `resolved` | 2 | Local ModInfo identity matched the target. |
| `partially_resolved` | 2 | A package matched, but Modlet identity was incomplete. |
| `ambiguous` | 2 | Multiple candidates or multiple Modlets remained. |
| `not_found_locally` | 9 | No exact local identity was observed. |
| `not_applicable` | 18 | The target was a condition, instruction, translation/author row, or text fragment. |

`not_applicable` does not mean that a dependency is absent.
`resolved` does not mean that runtime dependency behavior was confirmed.

Three observations received Nexus MOD/file IDs through local archive and
`.meta` evidence. The main identity examples are recorded in `findings.md`.

## Design implication

The local scan is useful for candidate identity resolution before dependency
interpretation. It must preserve the original source observation and retain
the following fields on a future dependency edge:

- `source`
- `evidence`
- `confidence`
- `resolution_status`
- `relationship_type`

Package presence, archive linkage, filename similarity, framework markers, and
co-presence do not prove a runtime dependency. Human review remains necessary
for hard versus optional meaning, package-to-Modlet one-to-many mapping,
patch/fork/bundle identity, and runtime necessity.

## Research artifacts

- [Before / after](requirements-local-resolution/before-after.md)
- [Findings](requirements-local-resolution/findings.md)
- [False-positive cases](requirements-local-resolution/false-positive-cases.md)
- [Resolution results](requirements-local-resolution/resolution-results.csv)
- [Resolution evidence](requirements-local-resolution/resolution-evidence.json)
- [Local inventory](requirements-local-resolution/local-inventory.json)

## Privacy and reproducibility boundary

The committed inventory uses relative local paths. The generated privacy
metadata records that absolute paths, external raw CDN URLs, cookies, API keys,
and user identifiers were omitted. Archives were inspected without extracting
their contents to disk.

The original local source tree, external Web-only baseline, and analysis script
remain outside this repository. The committed files preserve the observed
result and its evidence, not a complete local-environment reproduction.
