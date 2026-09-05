#!/usr/bin/env bash
# The privacy page must remain a self-contained GitHub Pages artifact whose
# visible policy is mirrored by the downloadable plain-text version.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit

python3 - <<'PY'
from functools import partial
from html.parser import HTMLParser
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from urllib.request import urlopen
import difflib
import re

root = Path.cwd()
html_path = root / "docs/privacy/index.html"
text_path = root / "docs/privacy/privacy-policy.txt"

for path in (html_path, text_path):
    if not path.is_file():
        raise SystemExit(f"privacy-policy: missing {path.relative_to(root)}")

html = html_path.read_text(encoding="utf-8")
plain = text_path.read_text(encoding="utf-8")


class PolicyParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.article_depth = 0
        self.article_text = []
        self.data_types = []
        self.providers = {}
        self.commitments = []
        self.time_dates = []
        self.mailtos = []
        self.remote_resources = []
        self.script_count = 0
        self.form_count = 0

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag == "article" and values.get("id") == "privacy-policy":
            self.article_depth = 1
        elif self.article_depth:
            self.article_depth += 1

        if tag == "script":
            self.script_count += 1
        if tag == "form":
            self.form_count += 1

        for name in ("href", "src"):
            target = values.get(name, "").strip()
            if target.startswith(("http://", "https://", "//")):
                self.remote_resources.append(target)

        if not self.article_depth:
            return
        privacy_type = values.get("data-privacy-type")
        if privacy_type:
            self.data_types.append(privacy_type)
        provider = values.get("data-provider")
        if provider:
            self.providers[provider] = values.get("data-when-enabled")
        commitment = values.get("data-commitment")
        if commitment:
            self.commitments.append(commitment)
        if tag == "time" and values.get("datetime"):
            self.time_dates.append(values["datetime"])
        if tag == "a" and values.get("href", "").startswith("mailto:"):
            self.mailtos.append(values["href"][len("mailto:"):])

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        if self.article_depth:
            self.article_depth -= 1

    def handle_endtag(self, tag):
        if self.article_depth:
            self.article_depth -= 1

    def handle_data(self, data):
        if self.article_depth:
            self.article_text.append(data)


parser = PolicyParser()
parser.feed(html)

required_types = {
    "purchase-history",
    "user-id",
    "device-id",
    "product-interaction",
    "other-diagnostics",
}
if set(parser.data_types) != required_types or len(parser.data_types) != len(required_types):
    raise SystemExit(f"privacy-policy: wrong five-type inventory: {parser.data_types}")

expected_providers = {
    "RevenueCat": None,
    "OneSignal": "true",
    "LevelPlay": "true",
}
if parser.providers != expected_providers:
    raise SystemExit(f"privacy-policy: wrong provider/enablement map: {parser.providers}")

expected_commitments = {"no-tracking", "no-sale", "no-accounts"}
if set(parser.commitments) != expected_commitments:
    raise SystemExit(f"privacy-policy: missing privacy commitments: {parser.commitments}")

if parser.time_dates != ["2026-09-03"]:
    raise SystemExit(f"privacy-policy: effective date drift: {parser.time_dates}")

if len(parser.mailtos) != 1:
    raise SystemExit(f"privacy-policy: expected one contact mailto, found {parser.mailtos}")
email = parser.mailtos[0]
if not re.fullmatch(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", email):
    raise SystemExit(f"privacy-policy: invalid contact email: {email!r}")
if any(token in email.lower() for token in ("noreply", "example.", "placeholder")):
    raise SystemExit(f"privacy-policy: non-public contact email: {email!r}")

if parser.remote_resources or parser.script_count or parser.form_count:
    raise SystemExit(
        "privacy-policy: page must make no remote requests and contain no scripts/forms: "
        f"resources={parser.remote_resources}, scripts={parser.script_count}, forms={parser.form_count}"
    )

collapse = lambda value: re.sub(r"\s+", " ", value).strip()
html_policy = collapse("".join(parser.article_text))
plain_policy = collapse(plain)
if html_policy != plain_policy:
    difference = "\n".join(
        difflib.unified_diff(
            html_policy.splitlines(),
            plain_policy.splitlines(),
            fromfile="HTML visible policy",
            tofile="plain-text mirror",
            lineterm="",
        )
    )
    raise SystemExit(
        "privacy-policy: HTML and plain-text visible policy differ\n" + difference
    )

if not html.lstrip().lower().startswith("<!doctype html>"):
    raise SystemExit("privacy-policy: HTML5 doctype missing")
if not re.search(r'<html\b[^>]*\blang="en"', html, re.IGNORECASE):
    raise SystemExit("privacy-policy: html lang=en missing")
if not re.search(r'<meta\b[^>]*\bcharset="utf-8"', html, re.IGNORECASE):
    raise SystemExit("privacy-policy: UTF-8 charset missing")
if not re.search(r'<meta\b[^>]*\bname="viewport"', html, re.IGNORECASE):
    raise SystemExit("privacy-policy: responsive viewport missing")


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_args):
        pass


server = ThreadingHTTPServer(
    ("127.0.0.1", 0), partial(QuietHandler, directory=str(root / "docs"))
)
thread = Thread(target=server.serve_forever, daemon=True)
thread.start()
try:
    base = f"http://127.0.0.1:{server.server_port}"
    with urlopen(base + "/privacy/", timeout=5) as response:
        if response.status != 200 or not response.headers.get_content_type() == "text/html":
            raise SystemExit("privacy-policy: /privacy/ is not served as HTML")
        if response.read() != html_path.read_bytes():
            raise SystemExit("privacy-policy: served HTML bytes differ from index.html")
    with urlopen(base + "/privacy/privacy-policy.txt", timeout=5) as response:
        if response.status != 200 or not response.headers.get_content_type() == "text/plain":
            raise SystemExit("privacy-policy: plain-text mirror has wrong response")
        if response.read() != text_path.read_bytes():
            raise SystemExit("privacy-policy: served text bytes differ from mirror")
finally:
    server.shutdown()
    server.server_close()
    thread.join(timeout=5)

print("privacy-policy: OK (Pages route, five types, providers, commitments, contact, mirror)")
PY
