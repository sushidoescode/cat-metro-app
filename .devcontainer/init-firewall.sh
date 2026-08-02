#!/usr/bin/env bash
# Egress firewall for reduced-supervision runs: default-deny outbound, allowlist only what
# development needs. This container is the ONLY sanctioned home for permissive permission modes.
# Requires: run as root (postCreateCommand uses sudo). Uses iptables + dnsmasq-free resolution:
# we resolve allowlisted domains at start and pin them; re-run to refresh IPs.
set -euo pipefail
ALLOW_DOMAINS=(
  api.anthropic.com claude.ai statsig.anthropic.com sentry.io
  github.com api.github.com codeload.github.com objects.githubusercontent.com raw.githubusercontent.com
  registry.npmjs.org   # ← add your stack's registries here (forge-init's STACK_REGISTRY_DOMAINS)
)
command -v iptables >/dev/null || { apt-get update -qq && apt-get install -y -qq iptables dnsutils; }

iptables -F OUTPUT
iptables -A OUTPUT -o lo -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT   # DNS
for d in "${ALLOW_DOMAINS[@]}"; do
  for ip in $(dig +short "$d" A | grep -E '^[0-9.]+$'); do
    iptables -A OUTPUT -d "$ip" -j ACCEPT
  done
done
iptables -A OUTPUT -j REJECT
echo "forge firewall: default-deny egress; allowlisted ${#ALLOW_DOMAINS[@]} domains (re-run to refresh IPs)"
echo "forge firewall: verify with:  curl -m 5 https://example.com  (must fail)  ·  curl -m 5 https://api.github.com  (must succeed)"
