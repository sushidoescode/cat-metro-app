# Cat Metro — Claude Code layer

All project instructions live in the universal file — read it as part of this one:
@AGENTS.md

## Claude Code specifics (on top of AGENTS.md)
- This repo runs the Forge workflow: lifecycle skills `/forge-build`, `/forge-review`, `/forge-release`, etc. (plugin `forge`), with role subagents (implementer, code-reviewer, security-reviewer, test-author, …) — delegate per the skills, don't improvise the process.
- Enforcement here is layered: permission rules + PreToolUse hooks (`.claude/hooks/`) + the universal git pre-commit hook. If a hook denies you, that's the system working — report it, don't route around it.
- Reviews run in fresh-context subagents; never review your own diff in-session.
