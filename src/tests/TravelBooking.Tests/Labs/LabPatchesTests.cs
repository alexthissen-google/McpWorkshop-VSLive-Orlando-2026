using System.Diagnostics;
using AwesomeAssertions.Execution;

namespace TravelBooking.Tests.Labs;

/// <summary>
/// Repository-integrity check, not a unit test of production code: the lab patches in
/// <c>labs/assets/prepare/</c> must keep reverse-applying to the source tree as it is
/// right now, uncommitted changes included. If one of these fails, a code change under
/// <c>src/</c> (or <c>Directory.Packages.props</c>) touched a file a lab patch also touches,
/// and the patch has to be regenerated — see <c>labs/assets/prepare/README.md</c>.
/// </summary>
public class LabPatchesTests
{
    private static readonly string[] LabPaths = ["src", "Directory.Packages.props"];

    public static TheoryData<int> Labs => new() { 1, 2, 3, 4, 5, 6 };

    [Theory]
    [MemberData(nameof(Labs))]
    public void PrepareLab_start_state_can_still_be_derived_from_the_working_tree(int lab)
    {
        var repo = FindRepoRoot();
        using var scratch = new ScratchTree(repo, LabPaths);
        SkipUnlessFinishedSolution(repo, scratch);

        using var _ = new AssertionScope($"start of Lab {lab}");
        foreach (var n in Enumerable.Range(lab, 7 - lab).Reverse())
        {
            var patch = Path.Combine(repo, "labs", "assets", "prepare", $"lab-0{n}.patch");
            File.Exists(patch).Should().BeTrue($"lab-0{n}.patch must exist");

            var (exitCode, output) = scratch.Git("apply", "-R", "--whitespace=nowarn", patch);
            exitCode.Should().Be(0,
                $"lab-0{n}.patch must reverse-apply on top of the current src/ (regenerate it after " +
                $"changing a file it touches — labs/assets/prepare/README.md). git said:\n{output}");
            if (exitCode != 0)
            {
                break; // later patches would fail for the same reason; report the first one only
            }
        }
    }

    [Fact]
    public void PrepareLab_patches_round_trip_back_to_the_working_tree()
    {
        var repo = FindRepoRoot();
        using var scratch = new ScratchTree(repo, LabPaths);
        SkipUnlessFinishedSolution(repo, scratch);
        var before = scratch.Snapshot();

        foreach (var n in new[] { 6, 5, 4, 3, 2, 1 })
        {
            scratch.Git("apply", "-R", "--whitespace=nowarn", Patch(repo, n)).ExitCode.Should().Be(0);
        }

        foreach (var n in new[] { 1, 2, 3, 4, 5, 6 })
        {
            scratch.Git("apply", "--whitespace=nowarn", Patch(repo, n)).ExitCode.Should().Be(0);
        }

        scratch.Snapshot().Should().Equal(before,
            "reverse-applying then re-applying every lab patch must reproduce the working tree exactly");
    }

    /// <summary>
    /// An attendee runs <c>dotnet test</c> in the middle of a lab, where the tree is a lab
    /// starting point plus their edits and these checks cannot apply. If the last lab's
    /// patch applies <em>forward</em>, the tree is at such a point: skip instead of failing.
    /// </summary>
    private static void SkipUnlessFinishedSolution(string repo, ScratchTree scratch)
    {
        if (scratch.Git("apply", "--check", "--whitespace=nowarn", Patch(repo, 6)).ExitCode == 0)
        {
            Assert.Skip("The working tree is at a lab starting point (labs/prepare-lab.sh), not the " +
                "finished solution; run ./labs/prepare-lab.sh solution to check the lab patches.");
        }
    }

    private static string Patch(string repo, int n) =>
        Path.Combine(repo, "labs", "assets", "prepare", $"lab-0{n}.patch");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TravelBooking.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("TravelBooking.slnx not found above the test output directory.");
    }

    /// <summary>
    /// A throw-away copy of the lab-relevant files from the working tree (tracked and
    /// untracked-but-not-ignored, so uncommitted edits count), inside a fresh git repo so
    /// that <c>git apply</c> behaves exactly as it does for an attendee.
    /// </summary>
    private sealed class ScratchTree : IDisposable
    {
        private readonly string _dir;

        public ScratchTree(string repo, string[] paths)
        {
            _dir = Path.Combine(Path.GetTempPath(), "labpatches-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);

            var (code, list) = Run(repo, "git", ["ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", .. paths]);
            if (code != 0)
            {
                throw new InvalidOperationException("git ls-files failed: " + list);
            }

            foreach (var rel in list.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                var source = Path.Combine(repo, rel);
                if (!File.Exists(source))
                {
                    continue; // deleted in the working tree
                }

                var target = Path.Combine(_dir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }

            Git("init", "-q");
        }

        public (int ExitCode, string Output) Git(params string[] args) => Run(_dir, "git", args);

        /// <summary>Relative path + content hash per file, so two snapshots compare by value.</summary>
        public List<string> Snapshot() =>
            Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => Path.GetRelativePath(_dir, f) + " " +
                             Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))))
                .ToList();

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private static (int ExitCode, string Output) Run(string cwd, string file, IEnumerable<string> args)
        {
            var psi = new ProcessStartInfo(file)
            {
                WorkingDirectory = cwd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException($"could not start {file}");
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, stdout + stderr);
        }
    }
}
