# Lab preparation patches

The committed source tree is always the **finished, tested solution**. Each lab starts
from a point *before* its own changes exist, and `labs/prepare-lab.sh N` produces that
point by reverse-applying the patches in this folder — as uncommitted changes, so an
attendee can always get back to green with `./labs/prepare-lab.sh solution`.

| File | Forward direction (what the lab adds) |
|------|----------------------------------------|
| `lab-01.patch` | tool classes, CORS, MCP server registration (tools, resources, prompts), `MapMcp`, the six-tools test |
| `lab-02.patch` | logger + `McpException` in `BookingTools`, wire-logging middleware |
| `lab-03.patch` | `McpTelemetry`, spans and counters on the three state-changing tools, OTel source/meter registration |
| `lab-04.patch` | 1-minute holds, idempotent re-hold in `Flight.HoldSeat`, idempotent confirm, EF retry strategy, six new tests (and one rewritten), `customerId` |
| `lab-05.patch` | JWT bearer + MCP auth, pass-through scheme, `ScopeClaims`, `[Authorize]` on tools/resource/prompt and REST endpoints, `ClaimsPrincipal`, `UseIdentityServer: true`, strict `OwnershipRule`, and their tests |
| `lab-06.patch` | health endpoints mapped in every environment, public endpoints for the server and the identity provider |

Each patch is a plain `git diff` from the *start* of lab N to the *end* of lab N, where
"start of lab N" means: labs 1..N-1 applied, labs N..6 not. The patches therefore stack:
`prepare-lab.sh 3` reverse-applies 06, 05, 04 and 03 in that order, and
`git diff` then shows everything Labs 3 to 6 will add back (`prepare-lab.sh check 3`
narrows that to what differs from the end of Lab 3 alone). Before resetting, the script
parks the current lab paths in a `git stash` entry unless `--force` is given.

## Keeping them honest

Two layers, so drift is caught as early as possible:

**`LabPatchesTests`** (`src/tests/TravelBooking.Tests/Labs/`) runs with the normal unit
test suite, including inside the `create-pr` skill's validation step. It copies the
working tree's lab files to a scratch git repo and checks that every patch still
reverse-applies in sequence and round-trips back. It fails the moment a change under
`src/` or `Directory.Packages.props` touches a line a patch relies on, and names the
patch and file.

**`verify.sh`** (run by CI on every pull request, and safe to run on a dirty tree — it
works on a throw-away copy) additionally checks, for every lab:

1. `prepare-lab.sh N` applies cleanly and the result builds;
2. applying `lab-0N.patch` … `lab-06.patch` forward lands exactly on `HEAD`;
3. every C# block of at least three lines in `labs/lab-0N-*.md` appears verbatim in the
   tree at the end of lab N (`check-lab-blocks.py`). Blocks containing `...` are skipped;
   put `<!-- verify: skip -->` directly above a block that is meant to be typed and then
   undone (the "break it on purpose" exercises), and `<!-- verify: intermediate -->` above
   a block that a later step in the same lab amends again. Blocks made only of `using`
   directives are checked line by line, since they interleave with existing usings.

Any of those failing means the code and the labs have drifted apart.

## Regenerating the patches after a code change

The patches are diffs between neighbouring states, so a change to the solution usually
only invalidates the patches for the labs whose files it touches. The procedure is the
same either way:

```bash
# 1. Get to the start of the broken lab (fix any reject by hand in the working tree).
./labs/prepare-lab.sh N --force --no-build       # fails on the drifted patch
git apply -R --reject labs/assets/prepare/lab-0N.patch    # leaves *.rej files to resolve

# 2. Once the working tree is the correct "start of lab N", regenerate the patch
#    as the diff from that state to the "end of lab N" state. `git stash create`
#    snapshots the tree as a commit without touching it (`git add` new files first):
START=$(git stash create)                                # start-of-N
./labs/prepare-lab.sh $((N+1)) --force --no-build        # end-of-N == start-of-N+1
END=$(git stash create)                                  # for N=6 skip both lines: END=HEAD
git diff $START $END -- src Directory.Packages.props > labs/assets/prepare/lab-0N.patch
```

In practice the quickest route is a throwaway branch with one commit per state
("start of Lab 1" … "start of Lab 6", then the solution): regenerate every patch with
`git diff <start-of-N> <start-of-N+1> > lab-0N.patch`, run `verify.sh`, and delete the
branch. The states are small and the script tells you immediately which one is wrong.
