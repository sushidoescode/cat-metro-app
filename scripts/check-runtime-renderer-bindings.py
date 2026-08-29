#!/usr/bin/env python3
"""Fail unless every runtime renderer creation has a site-local material bind."""

from __future__ import annotations

import re
import sys
from pathlib import Path


PRIMITIVE = re.compile(
    r"(?:UnityEngine\s*\.\s*)?GameObject\s*\.\s*CreatePrimitive\s*\(",
    re.MULTILINE,
)
MESH_RENDERER = re.compile(
    r"\.\s*AddComponent\s*<\s*(?:UnityEngine\s*\.\s*)?MeshRenderer\s*>\s*\(",
    re.MULTILINE,
)
RENDERER_TYPE = r"(?:UnityEngine\s*\.\s*)?Renderer"


def mask_non_code(text: str) -> str:
    """Mask comments and literals while preserving offsets and newlines."""
    out = list(text)
    i = 0
    while i < len(text):
        if text.startswith("//", i):
            j = text.find("\n", i + 2)
            if j < 0:
                j = len(text)
            for k in range(i, j):
                out[k] = " "
            i = j
            continue
        if text.startswith("/*", i):
            j = text.find("*/", i + 2)
            if j < 0:
                j = len(text) - 2
            end = min(len(text), j + 2)
            for k in range(i, end):
                if text[k] != "\n":
                    out[k] = " "
            i = end
            continue
        if text[i] == '"':
            verbatim = i > 0 and text[i - 1] == "@"
            j = i + 1
            while j < len(text):
                if verbatim and text[j] == '"' and j + 1 < len(text) \
                        and text[j + 1] == '"':
                    j += 2
                    continue
                if text[j] == '"':
                    j += 1
                    break
                if not verbatim and text[j] == "\\":
                    j += 2
                    continue
                j += 1
            for k in range(i, min(j, len(text))):
                if text[k] != "\n":
                    out[k] = " "
            i = j
            continue
        if text[i] == "'":
            j = i + 1
            while j < len(text):
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == "'":
                    j += 1
                    break
                j += 1
            for k in range(i, min(j, len(text))):
                if text[k] != "\n":
                    out[k] = " "
            i = j
            continue
        i += 1
    return "".join(out)


def brace_pairs(code: str) -> list[tuple[int, int]]:
    stack: list[int] = []
    pairs: list[tuple[int, int]] = []
    for index, char in enumerate(code):
        if char == "{":
            stack.append(index)
        elif char == "}" and stack:
            pairs.append((stack.pop(), index))
    return pairs


def block_end(pairs: list[tuple[int, int]], position: int, fallback: int) -> int:
    enclosing = [pair for pair in pairs if pair[0] < position < pair[1]]
    if not enclosing:
        return fallback
    return max(enclosing, key=lambda pair: pair[0])[1]


def statement(code: str, position: int) -> str:
    start = max(code.rfind(";", 0, position), code.rfind("{", 0, position)) + 1
    end = code.find(";", position)
    if end < 0:
        end = len(code) - 1
    return code[start:end + 1]


def direct_object_bind(region: str, object_name: str) -> bool:
    object_rx = re.escape(object_name)
    direct = re.compile(
        rf"\b{object_rx}\s*\.\s*GetComponent\s*<\s*{RENDERER_TYPE}\s*>"
        rf"\s*\(\s*\)\s*\.\s*sharedMaterials?\s*=",
        re.MULTILINE,
    )
    if direct.search(region):
        return True

    alias = re.compile(
        rf"\b(?P<alias>[A-Za-z_]\w*)\s*=\s*{object_rx}\s*\.\s*GetComponent"
        rf"\s*<\s*{RENDERER_TYPE}\s*>\s*\(\s*\)",
        re.MULTILINE,
    )
    for match in alias.finditer(region):
        alias_rx = re.escape(match.group("alias"))
        bind = re.compile(
            rf"\b{alias_rx}\s*\.\s*sharedMaterials?\s*=", re.MULTILINE
        )
        if bind.search(region, match.end()):
            return True
    return False


def renderer_bind(region: str, renderer_name: str) -> bool:
    return re.search(
        rf"\b{re.escape(renderer_name)}\s*\.\s*sharedMaterials?\s*=",
        region,
        re.MULTILINE,
    ) is not None


def inspect_file(path: Path) -> tuple[int, list[str]]:
    text = path.read_text(encoding="utf-8")
    code = mask_non_code(text)
    pairs = brace_pairs(code)
    errors: list[str] = []
    site_count = 0

    for kind, pattern in (("primitive", PRIMITIVE), ("mesh renderer", MESH_RENDERER)):
        for match in pattern.finditer(code):
            site_count += 1
            line = text.count("\n", 0, match.start()) + 1
            stmt = statement(code, match.start())
            end = block_end(pairs, match.start(), len(code))
            region = code[match.end():end]

            if kind == "primitive":
                assigned = re.search(
                    r"\b(?P<name>[A-Za-z_]\w*)\s*=\s*"
                    r"(?:UnityEngine\s*\.\s*)?GameObject\s*\.\s*CreatePrimitive\s*\(",
                    stmt,
                    re.MULTILINE,
                )
                if assigned is None:
                    errors.append(
                        f"{path}:{line}: primitive creation is not assigned; "
                        "its material bind cannot be proven"
                    )
                    continue
                name = assigned.group("name")
                if not direct_object_bind(region, name):
                    errors.append(
                        f"{path}:{line}: primitive '{name}' has no site-local "
                        "sharedMaterial(s) assignment"
                    )
            else:
                assigned = re.search(
                    r"\b(?P<name>[A-Za-z_]\w*)\s*=\s*[A-Za-z_]\w*\s*\.\s*"
                    r"AddComponent\s*<\s*(?:UnityEngine\s*\.\s*)?MeshRenderer\s*>",
                    stmt,
                    re.MULTILINE,
                )
                if assigned is None:
                    errors.append(
                        f"{path}:{line}: MeshRenderer creation is not assigned; "
                        "its material bind cannot be proven"
                    )
                    continue
                name = assigned.group("name")
                if not renderer_bind(region, name):
                    errors.append(
                        f"{path}:{line}: MeshRenderer '{name}' has no site-local "
                        "sharedMaterial(s) assignment"
                    )

    return site_count, errors


def source_files(arguments: list[str]) -> list[Path]:
    files: list[Path] = []
    for value in arguments:
        path = Path(value)
        if path.is_dir():
            files.extend(sorted(path.rglob("*.cs")))
        elif path.is_file() and path.suffix == ".cs":
            files.append(path)
        else:
            raise ValueError(f"scan target is not a C# file or directory: {path}")
    return files


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: check-runtime-renderer-bindings.py <C# file-or-directory> [...]",
              file=sys.stderr)
        return 2
    try:
        files = source_files(sys.argv[1:])
    except (OSError, ValueError) as error:
        print(f"renderer-bindings: {error}", file=sys.stderr)
        return 2
    if not files:
        print("renderer-bindings: no C# files found (fail-closed)", file=sys.stderr)
        return 2

    total = 0
    errors: list[str] = []
    for path in files:
        try:
            count, file_errors = inspect_file(path)
        except (OSError, UnicodeError) as error:
            print(f"renderer-bindings: could not read {path}: {error}", file=sys.stderr)
            return 2
        total += count
        errors.extend(file_errors)

    if total == 0:
        print("renderer-bindings: no runtime renderer creations found (fail-closed)",
              file=sys.stderr)
        return 1
    if errors:
        for error in errors:
            print(f"renderer-bindings: {error}", file=sys.stderr)
        return 1
    print(f"renderer-bindings: OK ({total} site-local bindings)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
