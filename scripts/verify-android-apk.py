#!/usr/bin/env python3
"""Fail closed unless the requested path is a non-empty APK-shaped ZIP."""

from pathlib import Path
import sys
import zipfile


def main() -> int:
    if len(sys.argv) != 2:
        return 2

    apk = Path(sys.argv[1])
    valid = (
        apk.is_file()
        and not apk.is_symlink()
        and apk.stat().st_size > 0
        and zipfile.is_zipfile(apk)
    )
    if valid:
        with zipfile.ZipFile(apk) as package:
            try:
                manifest = package.getinfo("AndroidManifest.xml")
            except KeyError:
                valid = False
            else:
                valid = manifest.file_size > 0
    return 0 if valid else 1


if __name__ == "__main__":
    raise SystemExit(main())
