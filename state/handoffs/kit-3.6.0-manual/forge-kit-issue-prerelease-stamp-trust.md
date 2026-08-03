# forge-upgrade: project stamped from a pre-release checkout has no upgrade path once the release tags (identifier-trust dead-end)

**Kit version:** v3.6.0 (`ac973be`) · **Component:** `template/scripts/forge-upgrade.sh`

## Symptom

A project initialized by forge-init from a development checkout (template stamped at
`template_version: 3.4.1`, `release: null`, catalog id `3345ff83…`) cannot run
`forge-upgrade plan` against kit 3.6.0:

```
forge-upgrade: REFUSED: project baseline 3345ff83… for '.claude/hooks/protect-bash.sh' is not trusted by this kit
```

## Root cause

- `catalog_id()` hashes `template_version` + `release` + `files`. A pre-release snapshot and
  its subsequent tag therefore get **different identifiers even when their file manifests are
  byte-for-byte identical**. Verified by recomputation on the affected project: flipping only
  `release: null → "v3.4.1"` on the project's catalog entry reproduces exactly the released id
  `b278e2c9…` that kit 3.6.0 trusts. All 60 manifest entries (path, mode, sha256) are identical.
- `load_baseline()` trusts a stamped ledger only by exact identifier membership in the kit
  catalog. `--from-version` is explicitly rejected for stamped projects, and the CLI surface is
  only `check-kit` / `plan` / `apply` — there is no sanctioned migration for
  stamped-but-untrusted.
- The shipped catalog carries unreleased entries for the *current* dev heads (`4a19563…` 3.5.0
  `release: null`, `515be2d…` 3.6.0 `release: null`) but drops superseded pre-release
  snapshots on release — which is precisely what orphans early adopters who initialized from a
  dev checkout between tags.

## Impact

Every project initialized from a non-tagged kit state hits a hard REFUSED on its first upgrade
after the corresponding release tags, with no documented recovery. The failure names one
arbitrary path, not the actual condition, so diagnosis requires reading the trust code.

## Suggested fixes (any one)

1. **Manifest-equality fallback (preferred):** when a stamped identifier is unknown but its
   catalog entry's `files` manifest is canonically identical to a trusted entry's manifest,
   remap to the trusted id and surface the remap explicitly in the plan output. Identity stays
   hash-based; trust falls back to content, which is what the check is actually protecting.
2. Retain superseded pre-release snapshots in the released catalog history.
3. An explicit `adopt`/`rebaseline` subcommand gated on human confirmation, mirroring the
   existing `--from-version` ceremony for legacy projects.

Failing all of those, at least print a diagnostic that names the condition (pre-release stamp,
nearest manifest-identical trusted baseline) instead of a per-path trust error.

## Workaround used (cat-metro-app)

Human-approved provenance surgery: repoint the stamp's `files` ledger and `current` from
`3345ff83…` to `b278e2c9…` (byte-identical manifests verified first; pre-release catalog entry
retained for history), committed on a branch, then plan/apply ran clean.
