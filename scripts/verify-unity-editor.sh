#!/usr/bin/env bash
# Authenticate the one Unity binary allowed to receive the licensed-local project path.
set -eu

fail() {
  echo "verify-unity-editor.sh: FAIL — $1" >&2
  exit 1
}

[ "$#" -eq 0 ] || fail "accepts no editor override"

editor="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
app="${editor%/Contents/MacOS/Unity}"
requirement='anchor apple generic and identifier "com.unity3d.UnityEditor5.x" and certificate leaf[subject.OU] = "9QW8UQUTAA"'

[ -f "$editor" ] && [ -x "$editor" ] && [ ! -L "$editor" ] \
  || fail "pinned Unity 6000.3.16f1 executable is unavailable or indirect"
[ -x /usr/bin/codesign ] || fail "Apple code-signature verifier is unavailable"
[ -x /usr/bin/plutil ] || fail "Apple bundle-version verifier is unavailable"

/usr/bin/codesign --verify --deep --strict -R="$requirement" "$app" >/dev/null 2>&1 \
  || fail "pinned Unity bundle signature, identifier, or team does not match"
version=$(/usr/bin/plutil -extract CFBundleVersion raw "$app/Contents/Info.plist" 2>/dev/null) \
  || fail "pinned Unity bundle version is unreadable"
[ "$version" = "6000.3.16f1" ] || fail "pinned Unity bundle version does not match 6000.3.16f1"

printf '%s\n' "$editor"
