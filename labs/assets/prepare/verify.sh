#!/usr/bin/env bash
# verify.sh — prove the lab patches and the lab texts still match src/.
#
# Works on a throw-away copy of the working tree (uncommitted changes included),
# so it is safe to run at any time, dirty tree or not. For every lab N (1..6):
#   1. ./labs/prepare-lab.sh N        must apply cleanly and build
#   2. git apply lab-0N..lab-06       must bring the tree back to the baseline exactly
#   3. check-lab-blocks.py N          every C# block in lab-0N.md must appear
#                                     verbatim in the tree at the END of lab N
# Then the finished solution is built and tested once.
#
# Run from anywhere inside the repo, with the .NET SDK and NuGet access.
# CI runs this on every pull request. Set KEEP_SCRATCH=1 to inspect the copy.
set -euo pipefail

if [[ "${1:-}" != "--in-place" ]]; then
  SRC="$(git rev-parse --show-toplevel)"
  WORK="$(mktemp -d)/repo"
  mkdir -p "$WORK"
  echo "==> Copying working tree to scratch: $WORK"
  # Tracked + untracked-but-not-ignored files: uncommitted edits count, bin/obj do not.
  # A tracked file deleted in the working tree (every lab starting point has one) is
  # skipped, or tar would fail and, with pipefail, end the script without a message.
  (cd "$SRC" && git ls-files -z --cached --others --exclude-standard \
      | while IFS= read -r -d '' f; do if [[ -e "$f" ]]; then printf '%s\0' "$f"; fi; done \
      | tar --null -T - -cf -) | tar -xf - -C "$WORK"
  cd "$WORK"
  git init -q
  git -c user.name=verify -c user.email=verify@localhost -c commit.gpgsign=false \
      add -A >/dev/null
  git -c user.name=verify -c user.email=verify@localhost -c commit.gpgsign=false \
      commit -q -m "baseline: working tree as verified"
  if [[ "${KEEP_SCRATCH:-}" != "1" ]]; then
    trap 'rm -rf "$(dirname "$WORK")"' EXIT
  fi
  exec bash labs/assets/prepare/verify.sh --in-place
fi

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
PATCH_DIR="labs/assets/prepare"
LAB_PATHS=(src Directory.Packages.props)

fail() { echo "VERIFY FAILED: $1" >&2; exit 1; }

for n in 1 2 3 4 5 6; do
  echo "==> Lab $n: starting point"
  ./labs/prepare-lab.sh "$n" --force || fail "prepare-lab.sh $n"

  # Re-apply this lab's patch and check the text against the end-of-lab tree.
  git apply --whitespace=nowarn "$PATCH_DIR/lab-0$n.patch" || fail "forward patch lab-0$n.patch"
  echo "==> Lab $n: code blocks in labs/lab-0$n-*.md"
  python3 "$PATCH_DIR/check-lab-blocks.py" "$n" || fail "lab-0$n.md has a code block that does not match src/"

  # Then the rest of the stack, which must land exactly on the baseline.
  for m in 2 3 4 5 6; do
    (( m > n )) || continue
    git apply --whitespace=nowarn "$PATCH_DIR/lab-0$m.patch" || fail "forward patch lab-0$m.patch after lab $n"
  done
  if [[ -n "$(git status --porcelain -- "${LAB_PATHS[@]}")" ]]; then
    git status --short -- "${LAB_PATHS[@]}" >&2
    fail "applying lab-0$n..lab-06 forward does not reproduce the working tree"
  fi
done

echo "==> Finished solution: build and test"
dotnet build TravelBooking.slnx --nologo -v q || fail "build"
dotnet test TravelBooking.slnx || fail "tests"

echo
echo "==============================================="
echo " VERIFY OK — every lab start builds, every lab"
echo " patch round-trips, every lab text matches src."
echo "==============================================="
