#!/usr/bin/env bash
# Gate views: render the human-gate evidence as a single self-contained HTML page.
# HTML is a VIEW, never a source of truth — sources stay markdown/JSON in git; views are
# regenerated on demand and gitignored (state/gate-views/). Agents never read views back.
#   ./scripts/forge-gate-view.sh spec|adr|review|release|retro [extra-file.md] [--open]
# Preference: skills consult state/gate-prefs (view=html|md|both) to decide whether to run this.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
gate="${1:?usage: forge-gate-view.sh spec|adr|review|release|retro [file] [--open]}"
extra="${2:-}"; [ "$extra" = "--open" ] && extra=""
mkdir -p state/gate-views
out="state/gate-views/${gate}-$(date +%Y-%m-%d).html"

GATE="$gate" EXTRA="$extra" OUT="$out" python3 - <<'PY'
import glob, html, json, os, re, datetime

gate, extra, out = os.environ["GATE"], os.environ["EXTRA"], os.environ["OUT"]

SECTIONS = {
  "spec":   [("Hypothesis","docs/prd/hypothesis.md"),("PRD","docs/prd/PRD.md"),
             ("Requirements","docs/prd/requirements.md"),("Risks","docs/prd/risks.md")],
  "adr":    [("Decision records (review status lines)", p) for p in sorted(glob.glob("docs/adr/*.md"))],
  "review": [("Handoff / findings", p) for p in sorted(glob.glob("state/handoffs/*.md"))[-3:]],
  "release":[("Release rubric","evals/release-rubric.md"),("Project state","state/PROJECT_STATE.md")],
  "retro":  [("Project state","state/PROJECT_STATE.md")],
}
secs = SECTIONS.get(gate, [])
if extra: secs = [("Attached", extra)] + secs
if gate in ("release","retro"):
    secs += [(f"Results: {os.path.basename(p)}", p) for p in sorted(glob.glob("evals/results/*.json"))[-6:]]

def md2html(src: str) -> str:
    # Deliberately-small markdown subset (headings, lists, tables, code, emphasis, links, quotes).
    # Kit-generated markdown stays inside this subset; anything else renders as literal text.
    out_lines, in_code, in_ul, in_ol, in_q, table = [], False, False, False, False, []
    def close():
        nonlocal in_ul, in_ol, in_q
        if in_ul: out_lines.append("</ul>"); in_ul=False
        if in_ol: out_lines.append("</ol>"); in_ol=False
        if in_q: out_lines.append("</blockquote>"); in_q=False
    def flush_table():
        nonlocal table
        if not table: return
        rows = [r for r in table if not re.match(r"^\s*\|?[\s:|-]+\|?\s*$", r)]
        out_lines.append("<table>")
        for i, r in enumerate(rows):
            cells = [c.strip() for c in r.strip().strip("|").split("|")]
            tag = "th" if i == 0 else "td"
            out_lines.append("<tr>" + "".join(f"<{tag}>{inline(c)}</{tag}>" for c in cells) + "</tr>")
        out_lines.append("</table>"); table = []
    def inline(s: str) -> str:
        s = html.escape(s)
        s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
        s = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", s)
        s = re.sub(r"\*([^*]+)\*", r"<em>\1</em>", s)
        s = re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", r'<a href="\2">\1</a>', s)
        s = re.sub(r"\b(PASS|OK|GO)\b", r'<span class="ok">\1</span>', s)
        s = re.sub(r"\b(FAIL|NO-GO|BLOCKED|CRITICAL|HIGH)\b", r'<span class="bad">\1</span>', s)
        s = re.sub(r"^(\s*)\[ \]", r"\1<span class='box'>☐</span>", s)
        s = re.sub(r"^(\s*)\[x\]", r"\1<span class='ok'>☑</span>", s, flags=re.I)
        return s
    for line in src.split("\n"):
        if line.strip().startswith("```"):
            flush_table(); close()
            out_lines.append("<pre><code>" if not in_code else "</code></pre>"); in_code = not in_code; continue
        if in_code: out_lines.append(html.escape(line)); continue
        if re.match(r"^\s*\|.*\|\s*$", line): table.append(line); continue
        flush_table()
        m = re.match(r"^(#{1,4})\s+(.*)$", line)
        if m: close(); out_lines.append(f"<h{len(m.group(1))+1}>{inline(m.group(2))}</h{len(m.group(1))+1}>"); continue
        if re.match(r"^\s*[-*]\s+", line):
            if not in_ul: close(); out_lines.append("<ul>"); in_ul=True
            out_lines.append(f"<li>{inline(re.sub(r'^\\s*[-*]\\s+','',line))}</li>"); continue
        if re.match(r"^\s*\d+\.\s+", line):
            if not in_ol: close(); out_lines.append("<ol>"); in_ol=True
            out_lines.append(f"<li>{inline(re.sub(r'^\\s*\\d+\\.\\s+','',line))}</li>"); continue
        if line.startswith(">"):
            if not in_q: close(); out_lines.append("<blockquote>"); in_q=True
            out_lines.append(inline(line[1:].strip())+"<br>"); continue
        if re.match(r"^\s*---+\s*$", line): close(); out_lines.append("<hr>"); continue
        if not line.strip(): close(); continue
        close(); out_lines.append(f"<p>{inline(line)}</p>")
    flush_table(); close()
    if in_code: out_lines.append("</code></pre>")
    return "\n".join(out_lines)

body = []
for title, path in secs:
    body.append(f'<section><h2>{html.escape(title)}</h2>')
    if not os.path.exists(path):
        body.append(f'<p class="bad">missing: {html.escape(path)}</p></section>'); continue
    if path.endswith(".json"):
        try: body.append("<pre><code>"+html.escape(json.dumps(json.load(open(path)), indent=2))+"</code></pre>")
        except Exception: body.append("<pre><code>"+html.escape(open(path).read())+"</code></pre>")
    else:
        body.append(md2html(open(path).read()))
    body.append(f'<p class="src">source: <code>{html.escape(path)}</code></p></section>')

doc = f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>forge gate: {html.escape(gate)}</title><style>
:root {{ --fg:#1a1a1e; --bg:#fdfdfc; --mut:#6b6b76; --line:#e4e4e0; --ok:#0a7a3d; --bad:#b3261e; --card:#f5f5f2; }}
@media (prefers-color-scheme: dark) {{ :root {{ --fg:#e8e8e4; --bg:#141416; --mut:#9a9aa2; --line:#2c2c30; --ok:#4cc38a; --bad:#ff6b61; --card:#1d1d21; }} }}
body {{ margin:0 auto; max-width:860px; padding:2.5rem 1.25rem 5rem; font:16px/1.6 system-ui,-apple-system,sans-serif; color:var(--fg); background:var(--bg); }}
h1 {{ font-size:1.6rem }} h2 {{ font-size:1.15rem; border-bottom:1px solid var(--line); padding-bottom:.3rem; margin-top:2.2rem }}
h3,h4,h5 {{ font-size:1rem }} table {{ border-collapse:collapse; width:100%; margin:.8rem 0; font-size:.92rem; display:block; overflow-x:auto }}
th,td {{ border:1px solid var(--line); padding:.4rem .6rem; text-align:left; vertical-align:top }}
th {{ background:var(--card) }} code {{ background:var(--card); padding:.1rem .35rem; border-radius:4px; font-size:.9em }}
pre {{ background:var(--card); padding: .9rem; border-radius:8px; overflow-x:auto }} pre code {{ background:none; padding:0 }}
blockquote {{ border-left:3px solid var(--line); margin:.6rem 0; padding:.2rem .9rem; color:var(--mut) }}
.ok {{ color:var(--ok); font-weight:600 }} .bad {{ color:var(--bad); font-weight:600 }}
.src {{ color:var(--mut); font-size:.8rem; margin:-.2rem 0 0 }} .meta {{ color:var(--mut); font-size:.9rem }}
hr {{ border:0; border-top:1px solid var(--line) }} a {{ color:inherit }}
</style></head><body>
<h1>Gate view: {html.escape(gate)}</h1>
<p class="meta">Generated {datetime.date.today()} · derived view — the sources of truth are the files cited below, in git.
The decision this page supports is yours; nothing here self-approves.</p>
{''.join(body)}
</body></html>"""
open(out, "w").write(doc)
print(f"gate view → {out}")
PY

for a in "$@"; do [ "$a" = "--open" ] && { command -v open >/dev/null && open "$out" || xdg-open "$out" 2>/dev/null || true; }; done
exit 0
