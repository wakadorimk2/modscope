# ADR: Requirementsをevidence-backed observationとして扱う

- Status: Accepted
- Date: 2026-08-14
- Scope: Requirements and Dependencies

## Context

Requirements research produced source statements, candidate relationships, and
unresolved target names. These are different evidence states.

The Web-only baseline contained 43 dependency candidate observations and 67
classified edges. It resolved 0 of 33 MOD-target candidates to a Nexus MOD ID.
The run also had local evidence status `not_observable`.

If these results are compressed into a dependency graph, raw wording, source
provenance, target kind, version scope, and unresolved reasons are lost.

## Decision

ModScope treats Requirements as evidence-backed observations and derived
assertions. It uses this conceptual sequence:

```text
Source
  -> Requirement Observation
  -> Identity Resolution
  -> Relationship Classification
  -> Requirement Assertion / Dependency Edge
```

`Requirement Observation` and `Dependency Edge` are separate concepts.

An observation preserves the source MOD, raw target text, source kind, source
URL, locator, evidence excerpt, raw relationship wording, and observation time.
An assertion may additionally contain the resolved target identity,
relationship type, version constraint, confidence, verification state, and
resolution status. Derived data must not overwrite the raw observation.

`unresolved` means that evidence was observed but identity or meaning was not
resolved. `not_observable` means that the required source or input could not be
observed. Neither state means that a dependency is absent.

Structured Requirements are the preferred first evidence source. Description,
README, and author-document text may generate candidates. Free-text evidence
does not receive the same automatic certainty as structured data.

Names alone do not bind a target to a Nexus MOD. List membership or co-presence
does not create a dependency edge.

Requirements that target a framework, game, tool, environment, save state, or
manual step are not forced into ordinary MOD identity. Candidate target kinds
are conceptual only:

```text
mod
framework
game
tool
environment
manual_step
unresolved_reference
```

Relationship labels such as `hard_dependency`, `optional_dependency`,
`framework_dependency`, `game_version_requirement`,
`another_mod_requirement`, `compatibility_patch_requirement`,
`load_order_related_requirement`, `recommended_not_required`,
`declared_conflict`, `credit_or_reference`, and
`environment_requirement` describe different meanings. This list is design
knowledge. It is not a production enum.

Version expressions retain their raw form. A normalized form is optional and
must retain parsing status. Confidence remains coarse, such as `high`,
`medium`, or `low`; it is not a precise probability.

## Consequences

### Positive

- Source wording and provenance remain inspectable.
- Missing evidence is not presented as a negative dependency result.
- Non-MOD requirements remain visible without false Nexus bindings.
- Human review can focus on an unresolved reason.
- Future local evidence can improve identity resolution without rewriting the
  Web-only baseline.

### Cost

- One source statement may produce an observation without a resolved edge.
- A target can require human review even when its name looks familiar.
- Query and UI projections must expose evidence and resolution state.
- A future graph must handle multiple assertions and non-MOD targets.

## Rejected or deferred alternatives

### Directly convert every requirement to a dependency edge

Rejected. It loses raw wording, conditional meaning, and target identity state.

### Use only Structured Requirements

Rejected. The research found that Description and README/author documents added
25 of 43 dependency candidates.

### Bind the highest name-search result

Rejected. The baseline resolved 0 of 33 MOD-target candidates, and name
similarity is not sufficient identity evidence.

### Treat list co-presence as dependency evidence

Rejected. Membership does not establish a runtime requirement.

### Introduce a generic Evidence Graph framework now

Deferred. Requirements, Version, and Compatibility need more implementation
and verification experience before a shared framework is justified.

### Add a numeric confidence score

Deferred. A precise-looking number would imply a calibrated correctness
probability that this research does not provide.

## Follow-up

The next research task is a separate read-only local-evidence experiment. It
will compare the Web-only `0 / 33` baseline with `.meta`, `ModInfo.xml`, local
package, Modlet, and profile evidence. It will not modify this research record
or implement a resolver.
