# Lane 10 RELEASE-PREP — frozen contract (coordination-ADOPTED)

Frozen 2026-08-13 by the coordination session at main=`2fe2a2a` (SHA verified post-fetch).

**ADOPTION RECORD (ADDENDUM v2.3 clause 5, human-directed):** the human ruled in the
coordination chat 2026-08-13 in-session, selecting "Adopt Lane 13 + Lane 10" in answer to
the session's explicit ask (agent-relayed, H-1-class caveat, per the Lanes 3/9 precedent).
Lane verified unstarted/unpublished: no `docs/release-prep` branch on origin, no local
branch, no `docs/release/` tree on any ref. The adopted lane's PRs are authored, owned, and
— under Amendment 1 — merged by the coordination session as its own.

**Charter (ADDENDUM v2.1, binding):** owns `docs/release/**` (new) and
`docs/runbooks/play-closed-test.md` (new). Untouchable: `docs/store/**` (Lane 7's shipped
pack — cross-reference, never edit), `docs/plan/**` (read-only; the existing privacy page
is reconciled by FLAG-not-fix), all code, `unity/**`, `content/**`. State writes (v2.1
enumeration): this contract file (first commit) + ONE `state/PROJECT_STATE.md` row at merge
(140-line tripwire: file sits at 113). Gate expectation: RISKY via risk.diff-size or
fail-closed — the machine flag is NOT the authority; BOTH review legs are contract-mandated
ON the PR record; two-round cap. Docs-lane reading: truthfulness standard + both legs stand
in for TDD. START GATE: v2.1 on main — OPEN (verified). Driver: the Play closed-test clock
(~Aug 15, V-1) — the runbook must be usable by the human TODAY.

## Criteria

1. **Closed-test runbook** (`docs/runbooks/play-closed-test.md`): step-by-step Play Console
   closed-testing setup a human can follow in one sitting — track creation, tester roster
   (email list / Google Group), opt-in URL flow, build upload (AAB + signing realities of
   THIS repo: `scripts/build.sh`'s current state and the untracked
   `unity/Assets/Editor/CatMetroCliBuild.cs` shim are RECORDED DEBT — state them, do not
   paper over), version-code discipline, the personal-developer-account closed-test
   requirements (tester count + duration) VERIFIED against current Google documentation
   with retrieval date cited, promotion-to-production criteria, and the feedback loop.
   Human-only acts (Console actions, uploads, tags, spend) marked HUMAN at every step —
   `fastlane supply`/any Play upload stays NEVER-run-by-agents per AGENTS.md.
2. **Release-prep pack** (`docs/release/`): release checklist (versioning, signing,
   provenance/attestation debt as recorded — SSH signing + secret-scan TODOs cited
   truthfully), gate map (what check/test/build actually enforce today, incl. the
   `unity-editmode` remote-job gap recorded in Known debt), Play listing readiness
   cross-reference to `docs/store/**` (pointer only), and a tester-comms template.
3. **Flag-not-fix:** any discrepancy found in `docs/plan/**` (privacy page, spec claims) is
   FLAGGED in a dedicated section, never edited.
4. **Truthfulness:** no unbuilt automation described as existing; every repo claim cites
   its real file; every Google-policy claim carries a source + retrieval date; deferred/
   unknown items say so.
5. **Process:** both review legs posted ON the PR, every finding dispositioned; merge under
   Amendment 1 + v2.3 by the adopting session; census merge-record comment; ONE
   PROJECT_STATE row.

## STOP conditions

Any edit outside the two owned paths + enumerated state writes · any Play-policy claim that
cannot be sourced · any step that would have an agent perform a Console/upload/tag/spend
act · the 140-line state tripwire · docs/store or docs/plan edits of any kind.
