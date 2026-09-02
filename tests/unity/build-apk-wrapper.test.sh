#!/usr/bin/env bash
# Behavioral proof for the development APK wrapper. Unity is replaced only behind an explicit
# test seam; the real wrapper still owns staging, exit propagation and publication.
set -eu
unset CM_APK_TEST_MODE CM_UNITY_BIN

repo="$(git rev-parse --show-toplevel)"
case_root="$(mktemp -d)"
trap 'rm -rf -- "$case_root"' EXIT
fail() { echo "build-apk-wrapper.test.sh: FAIL — $*" >&2; exit 1; }

mkdir -p "$case_root/scripts" "$case_root/unity/ProjectSettings" "$case_root/build"
cp "$repo/scripts/build-apk.sh" "$case_root/scripts/build-apk.sh"
printf 'm_EditorVersion: TEST-UNITY-NOT-INSTALLED\n' \
  > "$case_root/unity/ProjectSettings/ProjectVersion.txt"

fake_unity="$case_root/fake-unity"
cat > "$fake_unity" <<'EOF'
#!/usr/bin/env bash
set -eu
log=""
while [ "$#" -gt 0 ]; do
  if [ "$1" = "-logFile" ]; then
    shift
    log="$1"
  fi
  shift
done
[ -n "$log" ] || exit 90
printf 'CLI_BUILD_RESULT mode=%s\n' "${FAKE_UNITY_MODE:-success}" > "$log"
case "${FAKE_UNITY_MODE:-success}" in
  fail) exit 42 ;;
  no-output) exit 0 ;;
  success) printf 'fresh-apk-from-this-build\n' > "$CM_APK_OUT" ;;
  *) exit 91 ;;
esac
EOF
chmod +x "$fake_unity"

out="$case_root/CatMetro-dev.apk"
printf 'known-good-previous-apk\n' > "$out"
before_sha="$(shasum -a 256 "$out" | awk '{print $1}')"

# A failed rebuild preserves the previous APK, propagates Unity's status, and never prints
# sideload instructions or a checksum for that stale artifact.
set +e
CM_APK_TEST_MODE=1 CM_UNITY_BIN="$fake_unity" FAKE_UNITY_MODE=fail \
  bash "$case_root/scripts/build-apk.sh" "$out" > "$case_root/fail.log" 2>&1
failed_rc=$?
set -e
[ "$failed_rc" -eq 42 ] || fail "Unity failure status was not propagated (got $failed_rc)"
[ "$(shasum -a 256 "$out" | awk '{print $1}')" = "$before_sha" ] \
  || fail "failed build changed the previous APK"
if grep -Eq '^(APK:|sha256:|Install on)' "$case_root/fail.log"; then
  fail "failed build advertised the previous APK as newly built"
fi

# Unity exit 0 without a freshly staged artifact is also a failure; an old final path cannot make
# that invocation look successful.
set +e
CM_APK_TEST_MODE=1 CM_UNITY_BIN="$fake_unity" FAKE_UNITY_MODE=no-output \
  bash "$case_root/scripts/build-apk.sh" "$out" > "$case_root/no-output.log" 2>&1
missing_rc=$?
set -e
[ "$missing_rc" -ne 0 ] || fail "missing fresh APK returned success"
[ "$(shasum -a 256 "$out" | awk '{print $1}')" = "$before_sha" ] \
  || fail "missing-output build changed the previous APK"
if grep -Eq '^(APK:|sha256:|Install on)' "$case_root/no-output.log"; then
  fail "missing-output build advertised the previous APK"
fi

# Only a successful invocation that produced this run's staged APK may replace the prior file.
CM_APK_TEST_MODE=1 CM_UNITY_BIN="$fake_unity" FAKE_UNITY_MODE=success \
  bash "$case_root/scripts/build-apk.sh" "$out" > "$case_root/success.log" 2>&1 \
  || fail "successful fake Unity build was rejected"
grep -qxF 'fresh-apk-from-this-build' "$out" \
  || fail "successful build did not publish the freshly staged APK"
grep -q '^sha256:' "$case_root/success.log" || fail "successful build omitted its checksum"
grep -q '^Install on' "$case_root/success.log" || fail "successful build omitted sideload guidance"

# Production invocations cannot redirect the Unity executable through the test seam.
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-apk.sh" "$case_root/rejected.apk" \
  > "$case_root/rejected.log" 2>&1
override_rc=$?
set -e
[ "$override_rc" -ne 0 ] || fail "production invocation accepted CM_UNITY_BIN"
grep -q 'test seam' "$case_root/rejected.log" \
  || fail "production override rejection was not explicit"

echo "build-apk-wrapper.test.sh: OK"
