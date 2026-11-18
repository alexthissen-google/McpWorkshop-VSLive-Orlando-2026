#!/usr/bin/env python3
"""Check that every C# code block in labs/lab-0N-*.md appears verbatim in the
source tree as it stands at the END of lab N.

Run with the working tree in that state (verify.sh does this). Blocks are
matched on whitespace-normalised lines, ignoring blank lines and trailing
`//` comments, so indentation and wrapping in the markdown do not matter and a
lab may annotate a line ("//<-- this line is added"), but every non-blank line
must occur, in order and contiguous, in one file under src/.

A block is skipped when:
  - it is shorter than MIN_LINES non-blank lines (snippets like a single using)
  - it contains an ellipsis ("..." or "…") marking elided code
  - it is immediately preceded by an HTML comment containing "verify: skip"
    (blocks the lab asks you to type and then undo) or "verify: intermediate"
    (blocks a later step in the same lab amends again)
A block made only of `using` directives is checked as a set: each line must
exist somewhere in one source file, not necessarily adjacent.
"""
import pathlib
import re
import sys

MIN_LINES = 3
ROOT = pathlib.Path(__file__).resolve().parents[3]


def norm(line: str) -> str:
    # A trailing `//` comment is decoration (lab annotations, source remarks), never code.
    return " ".join(line.split("//", 1)[0].split())


def source_files():
    for p in (ROOT / "src").rglob("*.cs"):
        if any(part in ("bin", "obj") for part in p.parts):
            continue
        yield p


def main(n: str) -> int:
    labs = sorted((ROOT / "labs").glob(f"lab-0{n}-*.md"))
    if not labs:
        print(f"no lab markdown for lab {n}", file=sys.stderr)
        return 2
    lab = labs[0]
    text = lab.read_text()

    # Pre-compute the normalised lines of every source file once.
    files = {}
    for p in source_files():
        lines = [norm(l) for l in p.read_text().splitlines()]
        files[p.relative_to(ROOT)] = [l for l in lines if l]

    pattern = re.compile(r"(<!--[^>]*?-->\s*)?```csharp\n(.*?)```", re.S)
    failures = 0
    checked = 0
    for m in pattern.finditer(text):
        marker, body = m.group(1) or "", m.group(2)
        if "verify: skip" in marker or "verify: intermediate" in marker:
            continue
        if "..." in body or "…" in body:
            continue
        wanted = [norm(l) for l in body.splitlines()]
        wanted = [l for l in wanted if l]
        if len(wanted) < MIN_LINES:
            continue
        checked += 1
        line_no = text[: m.start(2)].count("\n") + 1
        found = False
        usings_only = all(l.startswith("using ") for l in wanted)
        for rel, lines in files.items():
            if usings_only:
                found = all(l in lines for l in wanted)
            else:
                k = len(wanted)
                for i in range(len(lines) - k + 1):
                    if lines[i : i + k] == wanted:
                        found = True
                        break
            if found:
                break
        if not found:
            failures += 1
            print(f"{lab.name}:{line_no}: block not found verbatim in src/ "
                  f"(first line: {wanted[0][:70]!r})", file=sys.stderr)
    print(f"{lab.name}: {checked} blocks checked, {failures} mismatched")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
