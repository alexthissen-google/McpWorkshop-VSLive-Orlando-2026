#!/usr/bin/env bash
# prepare-lab.sh — put the source tree in the state a lab starts from.
#
#   ./labs/prepare-lab.sh 1          # start of Lab 1: empty Tools/, no MCP wiring
#   ./labs/prepare-lab.sh 4          # start of Lab 4: Labs 1-3 applied, 4-6 not
#   ./labs/prepare-lab.sh solution   # the finished solution (what is committed)
#   ./labs/prepare-lab.sh check 2    # diff your tree against the end of Lab 2
#
# The committed code is always the finished, tested solution. This script
# takes the finishing touches OUT again, as uncommitted changes to your working
# tree, by reverse-applying one patch per later lab (labs/assets/prepare/*.patch).
# Following a lab puts them back by hand; `check N` shows what still differs
# from the reference, and `./labs/prepare-lab.sh solution` (or plain
# `git restore src Directory.Packages.props`) gets you back to green.
#
# Switching labs resets src/ and Directory.Packages.props. Whatever was there is
# parked in a git stash first ("prepare-lab: ..."), so nothing is ever lost:
# `git stash list` shows it, `git stash show -p stash@{0}` reads it back.
#
# Options:
#   --force     do not park the current tree in a stash before resetting
#   --no-build  skip the dotnet build at the end
#   --strict    check: show every difference, including ordering, comments and whitespace
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
PATCH_DIR="labs/assets/prepare"
# Paths the lab patches touch. Everything else in the repo is left alone.
LAB_PATHS=(src Directory.Packages.props)

usage() {
  sed -n '2,25p' "$0" | sed 's/^# \{0,1\}//'
  exit 2
}

target=""
mode="switch"
force=false
build=true
strict=false
for arg in "$@"; do
  case "$arg" in
    --force) force=true ;;
    --no-build) build=false ;;
    --strict) strict=true ;;
    -h|--help) usage ;;
    check) mode="check" ;;
    [1-6]|solution) target="$arg" ;;
    *) echo "unknown argument: $arg" >&2; usage ;;
  esac
done
[[ -n "$target" ]] || usage
[[ "$mode" == check && "$target" == solution ]] && usage

bold() { printf '\033[1m%s\033[0m\n' "$*"; }

# Copy the lab paths of a tree into $1: tracked and untracked-but-not-ignored files
# (so uncommitted edits count), skipping files deleted in the working tree.
copy_lab_paths() {
  git ls-files -z --cached --others --exclude-standard -- "${LAB_PATHS[@]}" \
    | while IFS= read -r -d '' f; do [[ -e "$f" ]] && printf '%s\0' "$f"; done \
    | tar --null -T - -cf - | tar -xf - -C "$1"
}

# Rewrite every file under $1 into a canonical form, so that what an editor or a pasting
# hand does differently does not count: C# using directives sorted to the top, comment-only
# lines and trailing comments dropped, blank lines and trailing whitespace gone; project
# and props files reduced to their sorted lines, so an item in another group or at
# another position is not a finding; json files keep their order and lose only blank
# lines. Only the two copies being compared are rewritten.
normalize_tree() {
  local f
  while IFS= read -r -d '' f; do
    case "$f" in
      *.cs)
        {
          tr -d '\r' < "$f" | grep -E '^using [A-Za-z0-9_. =]+;[[:space:]]*$' | sed -E 's/[[:space:]]+$//' | sort -u || true
          tr -d '\r' < "$f" | grep -vE '^using [A-Za-z0-9_. =]+;[[:space:]]*$' \
            | sed -E 's#^([^"]*)[[:space:]]+//.*$#\1#; s/[[:space:]]+$//' \
            | grep -vE '^[[:space:]]*(//.*)?$' || true
        } > "$f.norm"
        ;;
      *.csproj|*.props)
        tr -d '\r' < "$f" | sed -E 's/[[:space:]]+$//' | grep -vE '^[[:space:]]*$' | sort > "$f.norm" || true
        ;;
      *.json)
        tr -d '\r' < "$f" | sed -E 's/[[:space:]]+$//' | grep -vE '^[[:space:]]*$' > "$f.norm" || true
        ;;
      *) continue ;;
    esac
    mv "$f.norm" "$f"
  done < <(find "$1" -type f -print0)
}

tree_diff() {
  git --no-pager -c core.quotepath=off -c core.safecrlf=false -c core.autocrlf=false \
    diff --no-index --ignore-all-space --ignore-blank-lines --src-prefix=a/ --dst-prefix=b/ -- "$1" "$2" 2>/dev/null \
    | sed -E 's#(^|[ /])([ab])/(expected|yours)(\.norm)?/#\1\2/#g' || true
}

# `check N`: diff the working tree against the reference "end of Lab N" state,
# which is the committed solution minus the patches of every later lab. The diff is
# taken on normalised copies (see normalize_tree), so only differences in substance
# show; `--strict` shows the raw diff instead.
if [[ "$mode" == check ]]; then
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' EXIT
  mkdir -p "$tmp/expected" "$tmp/yours"
  git archive HEAD -- "${LAB_PATHS[@]}" | tar -xf - -C "$tmp/expected"
  copy_lab_paths "$tmp/yours"
  (
    cd "$tmp/expected" && git init -q
    for n in 6 5 4 3 2; do
      (( n > target )) || continue
      git apply -R --whitespace=nowarn "$ROOT/$PATCH_DIR/lab-0$n.patch"
    done
    rm -rf .git
  )
  cd "$tmp"
  raw_diff="$(tree_diff expected yours)"
  if [[ -z "$raw_diff" ]]; then
    bold "Your tree matches the end of Lab $target exactly."
    exit 0
  fi
  if [[ "$strict" == true ]]; then
    bold "All differences between the end of Lab $target (a/) and your tree (b/):"
    echo "$raw_diff"
    exit 1
  fi
  cp -R expected expected.norm
  cp -R yours yours.norm
  normalize_tree expected.norm
  normalize_tree yours.norm
  norm_diff="$(tree_diff expected.norm yours.norm)"
  raw_files="$(grep -c '^diff --git' <<<"$raw_diff" || true)"
  norm_files="$(grep -c '^diff --git' <<<"$norm_diff" || true)"
  if [[ -z "$norm_diff" ]]; then
    bold "Your tree matches the end of Lab $target."
    echo "($raw_files file(s) differ only in ordering, comments or whitespace; --strict shows them.)"
    exit 0
  fi
  bold "Differences that matter between the end of Lab $target (a/) and your tree (b/):"
  echo "(shown on a normalised copy: usings sorted to the top, comments and blank lines dropped)"
  echo "$norm_diff"
  if (( raw_files > norm_files )); then
    echo
    echo "(plus $((raw_files - norm_files)) file(s) that differ only in ordering, comments or whitespace; --strict shows everything.)"
  fi
  exit 1
fi

# 1. Never throw work away: park whatever is in the lab paths in a stash first.
if [[ -n "$(git status --porcelain -- "${LAB_PATHS[@]}")" && "$force" != true ]]; then
  git stash push --quiet --include-untracked \
    -m "prepare-lab: tree before switching to '$target' ($(date '+%Y-%m-%d %H:%M'))" \
    -- "${LAB_PATHS[@]}"
  echo "Parked your previous edits under src/ in a stash (see 'git stash list')."
fi

# 2. Back to the committed (finished) solution.
git restore --staged --worktree -- "${LAB_PATHS[@]}"

if [[ "$target" == "solution" ]]; then
  bold "Source tree is the finished solution."
else
  # 3. Reverse-apply the patches for this lab and every later one, latest first,
  #    so each patch meets exactly the tree it was generated against.
  patches=()
  for n in 6 5 4 3 2 1; do
    (( n >= target )) || continue
    p="$PATCH_DIR/lab-0$n.patch"
    [[ -f "$p" ]] || { echo "missing $p" >&2; exit 1; }
    patches+=("$p")
  done
  for p in "${patches[@]}"; do
    git apply --check -R --whitespace=nowarn "$p" \
      || { echo "cannot apply $p: the lab patches have drifted from src/ (see $PATCH_DIR/README.md)" >&2; exit 1; }
    git apply -R --whitespace=nowarn "$p"
  done
  bold "Source tree is now at the start of Lab $target."
  echo "Removed from the finished solution (what Lab $target and later labs will add back):"
  git status --short -- "${LAB_PATHS[@]}" | sed 's/^/  /'
fi

# 4. Prove the starting point compiles.
if [[ "$build" == true ]]; then
  echo
  echo "Building..."
  set +e
  out="$(dotnet build TravelBooking.slnx --nologo -v q 2>&1)"
  rc=$?
  set -e
  echo "$out" | grep -E " error |Build succeeded|Build FAILED" | sort -u || true
  if (( rc != 0 )); then
    echo "The starting point does not build. That is a bug in the lab patches, not in your work." >&2
    exit "$rc"
  fi
fi

if [[ "$target" != "solution" ]]; then
  echo
  echo "Check your work:   ./labs/prepare-lab.sh check $target"
  echo "Undo at any time:  ./labs/prepare-lab.sh solution"
fi
