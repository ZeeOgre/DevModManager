using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DMM.Avalonia;

internal sealed class ModOnboardingGitService
{
    public bool HasRequiredGitHubSettings(ProgramWideSettings settings, out string missing)
    {
        var missingItems = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.GitHubAccount)) missingItems.Add(nameof(settings.GitHubAccount));
        if (string.IsNullOrWhiteSpace(settings.GitHubToken)) missingItems.Add(nameof(settings.GitHubToken));
        if (string.IsNullOrWhiteSpace(settings.GitHubModRootRepo)) missingItems.Add(nameof(settings.GitHubModRootRepo));

        missing = string.Join(", ", missingItems);
        return missingItems.Count == 0;
    }

    public bool IsGitWorkingTree(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            return false;
        }

        var dotGit = Path.Combine(repoPath, ".git");
        return Directory.Exists(dotGit) || File.Exists(dotGit);
    }

    public bool TryBootstrapModRepository(
        ProgramWideSettings settings,
        string repoRoot,
        string gameName,
        string modName,
        string modRepoRoot,
        out string error)
    {
        error = string.Empty;

        var masterRepoPath = repoRoot;
        if (!EnsureMasterRepositoryPresent(masterRepoPath, settings.GitHubModRootRepo, out error))
        {
            return false;
        }

        var gameSlug = ToSlug(gameName);
        var modSlug = ToSlug(modName);
        var modRepoName = $"{gameSlug}-{modSlug}";
        var remoteModUrl = $"https://github.com/{settings.GitHubAccount}/{modRepoName}.git";

        if (!EnsureGitHubRepository(settings.GitHubAccount, settings.GitHubToken, modRepoName, out error))
        {
            return false;
        }

        var relativeSubmodulePath = Path.Combine(SanitizePathSegment(gameName), SanitizePathSegment(modName))
            .Replace('\\', '/');
        var fullSubmodulePath = Path.Combine(masterRepoPath, SanitizePathSegment(gameName), SanitizePathSegment(modName));
        Directory.CreateDirectory(Path.GetDirectoryName(fullSubmodulePath) ?? masterRepoPath);

        if (!IsGitWorkingTree(fullSubmodulePath))
        {
            if (!RunGit(masterRepoPath, $"submodule add \"{remoteModUrl}\" \"{relativeSubmodulePath}\"", out error))
            {
                return false;
            }

            if (!RunGit(masterRepoPath, "add .gitmodules", out error))
            {
                return false;
            }

            _ = RunGit(masterRepoPath, $"add \"{relativeSubmodulePath}\"", out _);
            _ = RunGit(masterRepoPath, $"commit -m \"Add submodule {relativeSubmodulePath}\"", out _);
            _ = RunGit(masterRepoPath, "push", out _);
        }

        if (!RunGit(masterRepoPath, "submodule sync --recursive", out error) ||
            !RunGit(masterRepoPath, "submodule update --init --recursive", out error))
        {
            return false;
        }

        if (!EnsureCanonicalStageBranches(fullSubmodulePath, out error))
        {
            return false;
        }

        return true;
    }

    private bool EnsureMasterRepositoryPresent(string masterRepoPath, string remoteUrl, out string error)
    {
        error = string.Empty;
        if (IsGitWorkingTree(masterRepoPath))
        {
            return true;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(masterRepoPath) ?? masterRepoPath);
        return RunGit(Path.GetDirectoryName(masterRepoPath) ?? masterRepoPath, $"clone \"{remoteUrl}\" \"{Path.GetFileName(masterRepoPath)}\"", out error);
    }

    private bool EnsureGitHubRepository(string account, string token, string repoName, out string error)
    {
        error = string.Empty;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DevModManager/1.0");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var getResponse = client.GetAsync($"https://api.github.com/repos/{account}/{repoName}").GetAwaiter().GetResult();
        if (getResponse.StatusCode == HttpStatusCode.OK)
        {
            return true;
        }

        if (getResponse.StatusCode != HttpStatusCode.NotFound)
        {
            error = $"GitHub check failed ({(int)getResponse.StatusCode})";
            return false;
        }

        var payload = JsonSerializer.Serialize(new { name = repoName, @private = false, auto_init = true });
        var createResponse = client.PostAsync(
            "https://api.github.com/user/repos",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json")).GetAwaiter().GetResult();

        if (createResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
        {
            return true;
        }

        error = $"GitHub create repo failed ({(int)createResponse.StatusCode})";
        return false;
    }

    private bool EnsureCanonicalStageBranches(string repoPath, out string error)
    {
        error = string.Empty;
        if (!IsGitWorkingTree(repoPath))
        {
            error = "submodule repo missing after sync";
            return false;
        }

        var branches = new[] { "stage/dev", "stage/test", "stage/preflight", "stage/creations", "stage/nexus", "stage/prod" };
        foreach (var branch in branches)
        {
            if (RunGit(repoPath, $"show-ref --verify --quiet refs/heads/{branch}", out _))
            {
                continue;
            }

            if (!RunGit(repoPath, $"branch {branch}", out error))
            {
                return false;
            }

            _ = RunGit(repoPath, $"push -u origin {branch}", out _);
        }

        _ = RunGit(repoPath, "checkout stage/dev", out _);
        return true;
    }

    public bool EnsureBranchCheckedOut(string repoPath, string branch, out string error)
    {
        error = string.Empty;

        if (RunGit(repoPath, $"show-ref --verify --quiet refs/heads/{branch}", out _))
        {
            return RunGit(repoPath, $"checkout {branch}", out error);
        }

        if (RunGit(repoPath, $"ls-remote --exit-code --heads origin {branch}", out _))
        {
            return RunGit(repoPath, $"checkout -b {branch} --track origin/{branch}", out error);
        }

        if (!RunGit(repoPath, $"checkout -b {branch}", out error))
        {
            return false;
        }

        _ = RunGit(repoPath, $"push -u origin {branch}", out _);
        return true;
    }

    public bool CommitAndPushOnboardingChanges(
        string masterRepoPath,
        string modRepoPath,
        string relativeSubmodulePath,
        string stageBranch,
        string modName,
        out string error)
    {
        error = string.Empty;

        if (!HasPendingChanges(modRepoPath))
        {
            return true;
        }

        if (!RunGit(modRepoPath, "add .", out error) ||
            !RunGit(modRepoPath, $"commit -m \"Onboard {modName} into {stageBranch}\"", out error) ||
            !RunGit(modRepoPath, $"push -u origin {stageBranch}", out error))
        {
            return false;
        }

        if (!RunGit(masterRepoPath, $"add \"{relativeSubmodulePath}\"", out error))
        {
            return false;
        }

        if (HasPendingChanges(masterRepoPath))
        {
            if (!RunGit(masterRepoPath, $"commit -m \"Update submodule {relativeSubmodulePath}\"", out error) ||
                !RunGit(masterRepoPath, "push", out error))
            {
                return false;
            }
        }

        // Keep local parent/submodule metadata aligned after pointer updates.
        if (!RunGit(masterRepoPath, "submodule sync --recursive", out error) ||
            !RunGit(masterRepoPath, "submodule update --init --recursive", out error))
        {
            return false;
        }

        return true;
    }

    public bool HasPendingChanges(string repoPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "status --porcelain",
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return false;
        }

        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
    }

    public string ToStageBranch(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return "stage/dev";
        }

        return $"stage/{ToSlug(stage)}";
    }

    private bool RunGit(string workingDirectory, string arguments, out string error)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            error = "failed to launch git";
            return false;
        }

        var stdout = process.StandardOutput.ReadToEnd().Trim();
        var stderr = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode == 0)
        {
            error = string.Empty;
            return true;
        }

        error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return false;
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned;
    }

    public string ToSlug(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

}
