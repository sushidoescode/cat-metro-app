#!/usr/bin/env bash
# Runs the actual pure export transformer against synthetic Gradle projects; no Unity/Gradle.
set -eu
repo="$(git rev-parse --show-toplevel)"
compiled="$(mktemp -d)"
trap 'rm -rf -- "$compiled"' EXIT
dotnet build "$repo/tests/unity/fixtures/android-sdk-export/AndroidSdkExport.csproj" --nologo --verbosity quiet \
  --property:BaseOutputPath="$compiled/bin/" \
  --property:BaseIntermediateOutputPath="$compiled/obj/"
dotnet "$compiled/bin/Debug/net8.0/AndroidSdkExport.dll" "$@"
