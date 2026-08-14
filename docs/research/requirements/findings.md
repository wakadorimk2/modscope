# Requirements research findings

## Position in the repository

This document records the Web-only Requirements / Dependencies research for the
Smorgasbord modlist. It is a research record, not a production schema.

The source snapshot was generated on 2026-08-14 at
`2026-08-14T10:10:00Z`. The manifest retrieval used the public Wabbajack parts
endpoint. The observed manifest SHA-256 was
`e74a9d3d37dad299c4d869f526dd5e2f1ea5ef3eb568d5aa8ec87e0b998dbe27`.

The raw `dataset.csv`, `dependency-edges.json`, archive, and execution logs
remain in the ModScopeLab research snapshot. This repository keeps the stable
findings, source registry, and representative evidence. It does not copy raw
research data into the product repository.

## Observed

### Scope and coverage

| Measure | Observed value |
|---|---:|
| Manifest archive count | 476 |
| Manifest 7DTD Nexus candidates | 468 |
| Deduplicated manifest MODs | 438 |
| Supplemental public Nexus references | 10 |
| Analyzed MOD subjects | 448 |
| Observation rows | 549 |
| Dependency candidate observations | 43 |
| Total classified edges | 67 |
| Nexus page access | 83 / 448 (18.5%) |
| Accessible pages with structured Requirements | 8 / 83 (9.6%) |
| Structured Requirements observations | 18 |
| Description dependency observations | 7 |
| README / author-document dependency observations | 18 |
| Structured-only share of dependency candidates | 18 / 43 (41.9%) |
| Description / README additional share | 25 / 43 (58.1%) |
| Explicit version constraints | 6 / 43 (14.0%) |
| Unresolved observations | 47 / 549 (8.6%) |
| Nexus ID resolution for MOD-target candidates | 0 / 33 |
| Contradicted observations | 0 / 549 |

The 67 classified edges include non-dependency relations. The 43 dependency
candidate observations are the denominator for the structured and free-text
coverage percentages.

These are observable values for the available public sources. They are not
true recall measurements. They do not describe the complete modding ecosystem.
The 18.5% page access rate includes the access limitations of this research
environment.

The research run marked local evidence as `not_observable`. Its local input
counts were zero for `ModInfo.xml`, MO2 `.meta`, and MO2 profiles. This means
that the run did not inspect local installation evidence. It does not mean that
the dependencies are absent.

### Relationship observations

The 67 classified edges had these relationship types:

| Relationship type | Count |
|---|---:|
| `hard_dependency` | 22 |
| `optional_dependency` | 13 |
| `environment_requirement` | 8 |
| `recommended_not_required` | 10 |
| `declared_conflict` | 4 |
| `game_version_requirement` | 3 |
| `framework_dependency` | 3 |
| `another_mod_requirement` | 2 |
| `credit_or_reference` | 2 |

The classification keeps these meanings separate. `requires`, `optional`,
`recommended`, `compatible with`, `credits`, `assets from`, `requires new
save`, `EAC disabled`, `client-side only`, and a game-version requirement are
not interchangeable dependency edges.

### Source registry

The research used the following public source families.

| Source kind | Source URL | Use |
|---|---|---|
| Wabbajack metadata / manifest | <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/modlists.json> | Candidate inventory and manifest context |
| GitHub README | <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/README.md> | Installation, version, environment, optional, and conflict statements |
| GitHub InstallationGuide | <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/InstallationGuide.md> | Prerequisite and environment statements |
| GitHub MOD_NOTES | <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/MOD_NOTES.md> | Author notes and requirement statements |
| Nexus page inventory | `https://www.nexusmods.com/7daystodie/mods/{numericId}` | Public page inventory for analyzed subjects |
| Nexus public HTML fallback | <https://www.nexusmods.com/7daystodie/mods/8405> | Structured Requirements and Description observations when direct fetch was blocked |

Every retained observation keeps `source_kind`, `source_url`,
`source_locator`, `evidence_excerpt`, `relation_phrase`, and `observed_at`.
Identity resolution does not overwrite these source fields.

### Representative evidence ledger

| Subject / raw target | Source and locator | Evidence excerpt | Relation phrase | Classified result |
|---|---|---|---|---|
| TMO Zombies / `TMO-CORE` | Nexus structured Requirements, public HTML lines 362-366, <https://www.nexusmods.com/7daystodie/mods/8405> | `TMO-CORE \| Required to Function. (If ServerSide Use, only required on Server!)` | `public HTML observation` | `framework_dependency`, high confidence |
| TMO Zombies / `EAC disabled for local hosting` | Nexus Description lines 414-416, <https://www.nexusmods.com/7daystodie/mods/8405> | `Requires EAC OFF in order to load correctly.` | `public HTML observation` | `environment_requirement`, medium confidence |
| Shadow Balancer / `7 Days to Die` and `Harmony` | Nexus Description lines 415-425, <https://www.nexusmods.com/7daystodie/mods/11022> | `Requires: 7 Days to Die v3.0, Harmony (ships with the game)` | `public HTML observation` | Game-version and framework requirements |
| Shadow Balancer / `client-side only` | Nexus Description lines 431-435, <https://www.nexusmods.com/7daystodie/mods/11022> | `This mod must be installed on the client, not just the server.` | `public HTML observation` | `environment_requirement`, medium confidence |
| Smorgasbord / `new save` | README sentence 167, <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/README.md> | `Updating the Mod List may require a new save` | `environment condition` | Save/environment condition, not a MOD dependency |
| Smorgasbord / `Advanced Weapons Repair Kit` | MOD_NOTES sentence 29, <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/MOD_NOTES.md> | `Some Tier 3 and all Tier 4 Weapons require the Advanced Weapons Repair Kit.` | `explicit requirement` | Candidate with unresolved target identity |
| Smorgasbord / overhaul targets | MOD_NOTES sentence 72, <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/MOD_NOTES.md> | `This Mod List is not compatible with the main EFT Overhaul, Rebirth, Darkness Falls, nor most other Overhauls.` | `declared conflict` | Declared conflict, not a dependency |
| Smorgasbord / `VC++ Redistributable` | InstallationGuide, `Prerequisites`, <https://raw.githubusercontent.com/Fluffernuttersandwich/Smorgasbord/main/InstallationGuide.md> | `VC++ Redistributable` | `prerequisite` | Environment requirement, non-MOD target |

The Nexus fallback observations are separate from live direct HTTP extraction.
The fallback status is part of the evidence provenance.

## Inferred design knowledge

### Requirement Observation is not a Dependency Edge

The research requires this conceptual sequence:

```text
Source
  -> Requirement Observation
  -> Identity Resolution
  -> Relationship Classification
  -> Requirement Assertion / Dependency Edge
```

For example, observing `TMO-CORE | Required to Function` is not the same as
creating a resolved edge to a specific Nexus MOD. The raw target text and
source evidence remain valid even when target identity is unresolved.

An observation should retain, at minimum:

- source MOD
- raw target text
- source kind
- source URL and locator
- evidence excerpt
- raw relationship wording
- observation time

Derived resolution data should remain separate:

- resolved target identity
- relationship type
- raw and normalized version constraint
- coarse confidence
- resolution status
- unresolved reason

### Structured and free-text evidence have different roles

Structured Requirements are a strong first evidence source when they are
available. They are not the only source. In this run, Description and
README/author-document observations added 25 of 43 dependency candidates.

Free-text extraction should produce reviewable candidates. Automatic final
assertion requires an explicit requirement expression, a unique target
identity, no optional or conditional meaning, preserved source evidence, and
preserved version scope. `recommended`, `compatible with`, `designed for`,
`credits`, `assets from`, and similar wording do not automatically create a
dependency.

### Identity and target kind are separate concerns

The run resolved 0 of 33 MOD-target candidates to a Nexus MOD ID. A target name
can refer to a MOD, framework, game, tool, environment condition, manual step,
or unresolved reference. Names such as `Harmony` must not be forced into a
Nexus MOD identity.

The following are conceptual target kinds for future modeling, not a required
production enum:

```text
mod
framework
game
tool
environment
manual_step
unresolved_reference
```

Co-presence in the Smorgasbord list is membership evidence. It is not evidence
that one MOD requires another MOD.

### Review and UI direction

Human review should be organized by the decision needed:

- resolve target identity
- classify requirement type
- confirm optional or required meaning
- review conflicting evidence
- confirm local presence
- review version constraint

The user should not receive only a low-confidence count. The result should
show the unresolved reason and the evidence needed to resolve it.

At approximately 500 MODs, a per-MOD Requirements view is the primary UI
candidate. A large dependency graph remains a future exploration view. This
research does not define a UI schema or implement the UI.

## Uncertain and not verified

- The counts have no ground truth and do not measure true recall.
- A failed Nexus page fetch does not prove that no requirement exists.
- The Web-only run did not observe `ModInfo.xml`, package README files, MO2
  `.meta`, MO2 profile state, local XML/config, DLL presence, or Harmony
  presence.
- No 7DTD runtime behavior was verified.
- No target identity was automatically bound from a name alone.
- Zero contradiction groups were observed in this source set. This does not
  prove that the wider ecosystem has no contradictory sources.
- Confidence is a coarse evidence aid. It is not a probability of correctness.
- The relationship and target-kind lists are design knowledge. They are not a
  production API or persistent schema.

## Next research step

The next experiment is a separate local-evidence follow-up. It should use
read-only observations of:

- MO2 `.meta` package and Nexus identity
- `ModInfo.xml` name, display name, author, version, and Website
- package-to-Modlet mapping
- package README and local documentation
- local XML/config and relevant DLL or Harmony footprint
- MO2 profile, enabled state, and priority
- Wabbajack manifest identity mapping

It should compare the Web-only baseline of `0 / 33` with local package,
Modlet, Nexus MOD, and Nexus file resolution. It must preserve the original
Web-only record and write a separate follow-up result. The local evidence
experiment is not implemented or executed by this change.
