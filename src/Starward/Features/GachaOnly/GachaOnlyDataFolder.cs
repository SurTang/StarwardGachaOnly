// GachaOnly fork — resolves the user data folder that holds StarwardDatabase.db.
//
// Requirement for this fork: the standalone gacha app must reuse the data folder of the user's
// existing Starward install ("共用同一个数据目录"), so gacha records, UIGF archives and the
// cached gacha URLs are shared with it instead of being duplicated.
//
// Resolution order (first hit wins):
//   1. command line :  --data-folder <path>   or  --data-folder=<path>
//   2. environment  :  STARWARD_GACHA_DATA_FOLDER
//   3. gacha-data.ini next to the executable, key UserDataFolder=<path>
//   4. auto probe   : known Starward-family folders on this machine (see ProbeCandidates)
//   5. null         : fall back to upstream Starward resolution (registry / portable config.ini /
//                     first-run welcome dialog)
//
// Everything is validated: a candidate that does not exist (or, for the auto probe, has no
// database) is skipped, so a stale ini file can never leave the app without a database.
//
// This file is new in the fork; upstream never touches it.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Starward.Features.Gacha;

internal static class GachaOnlyDataFolder
{

    /// <summary>Config file next to the executable. Key name matches upstream's portable config.ini.</summary>
    public const string ConfigFileName = "gacha-data.ini";

    public const string EnvironmentVariable = "STARWARD_GACHA_DATA_FOLDER";

    public const string UserDataFolderKey = "UserDataFolder";

    public const string CommandLineOption = "--data-folder";

    public const string DatabaseFileName = "StarwardDatabase.db";


    private const string LogFileName = "GachaOnly-data-folder.log";


    private static bool _resolved;

    private static string? _folder;


    /// <summary>The folder that was accepted this run, or null when upstream resolution is used.</summary>
    public static string? ResolvedFolder => _folder;


    /// <summary>Where the accepted folder came from, for the breadcrumb log.</summary>
    public static string? ResolvedFrom { get; private set; }


    /// <summary>
    /// Returns the shared data folder, or null when the fork does not apply / nothing was configured.
    /// Called once per process from <c>AppConfig.CheckEnviromentAsync</c>.
    /// </summary>
    public static string? Resolve()
    {
        if (!GachaOnlyProfile.Enabled)
        {
            return null;
        }
        if (_resolved)
        {
            return _folder;
        }
        _resolved = true;

        var tried = new List<string>();
        foreach ((string? candidate, string source) in Sources())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }
            string path = Normalize(candidate);
            tried.Add($"{source} => {path}");
            if (!Directory.Exists(path))
            {
                continue;
            }
            if (!File.Exists(Path.Combine(path, DatabaseFileName)) && source.StartsWith("probe", StringComparison.Ordinal))
            {
                // An explicitly configured folder may legitimately be brand new (upstream creates the
                // database on first use), but a probe result without a database is useless.
                continue;
            }
            _folder = path;
            ResolvedFrom = source;
            WriteBreadcrumb(tried, path, source);
            return path;
        }

        WriteBreadcrumb(tried, null, null);
        return null;
    }


    private static IEnumerable<(string? Candidate, string Source)> Sources()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith(CommandLineOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                yield return (arg[(CommandLineOption.Length + 1)..], "command line");
            }
            else if (arg.Equals(CommandLineOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                yield return (args[i + 1], "command line");
            }
        }

        yield return (Environment.GetEnvironmentVariable(EnvironmentVariable), "environment");

        yield return (ReadConfigFile(), "config file " + ConfigFileName);

        foreach (string candidate in ProbeCandidates())
        {
            yield return (candidate, "probe");
        }
    }


    /// <summary>Known Starward-family user data folders, most likely first.</summary>
    private static IEnumerable<string> ProbeCandidates()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // upstream default location (holds a database only for non-portable installs)
        yield return Path.Combine(localAppData, "Starward");

        // portable installs: <drive>:\Mihoyo\Starward is where the official portable build lives
        foreach (string drive in FixedDrives())
        {
            yield return Path.Combine(drive, "Mihoyo", "Starward");
        }

        // the data folder used by the previous standalone gacha build (MihoyoWishBook)
        yield return Path.Combine(localAppData, "MihoyoWishBook");

        foreach (string drive in FixedDrives())
        {
            yield return Path.Combine(drive, "Starward");
        }

        // next to the executable (portable-in-place)
        yield return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }


    private static IEnumerable<string> FixedDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GachaOnlyDataFolder: enumerate drives failed: {ex.Message}");
            yield break;
        }
        foreach (DriveInfo drive in drives)
        {
            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
            {
                yield return drive.RootDirectory.FullName;
            }
        }
    }


    private static string? ReadConfigFile()
    {
        foreach (string file in ConfigFileCandidates())
        {
            string? value = ReadConfigValue(file);
            if (value is not null)
            {
                return value;
            }
        }
        return null;
    }


    /// <summary>
    /// Where the config file may live. Besides the folder next to the executable this checks the
    /// package root, because in the portable layout the launcher sits in the package root while the
    /// app runs from <c>app-&lt;version&gt;\</c>, and the config belongs next to the launcher.
    /// </summary>
    private static IEnumerable<string> ConfigFileCandidates()
    {
        string baseFolder = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        yield return Path.Combine(baseFolder, ConfigFileName);

        var directory = new DirectoryInfo(baseFolder);
        if (directory.Name.StartsWith("app-", StringComparison.OrdinalIgnoreCase) && directory.Parent is not null)
        {
            yield return Path.Combine(directory.Parent.FullName, ConfigFileName);
        }
    }


    private static string? ReadConfigValue(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }
                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }
                if (line[..index].Trim().Equals(UserDataFolderKey, StringComparison.OrdinalIgnoreCase))
                {
                    return line[(index + 1)..].Trim().Trim('"').Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GachaOnlyDataFolder: read {ConfigFileName} failed: {ex.Message}");
        }
        return null;
    }


    /// <summary>Expands environment variables, strips quotes and makes the path absolute.</summary>
    private static string Normalize(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"').Trim());
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }


    /// <summary>
    /// Breadcrumb in the (shared) log folder: which folder the app attached to, and which
    /// candidates were rejected. Purely diagnostic, keeps support questions answerable.
    /// </summary>
    private static void WriteBreadcrumb(List<string> tried, string? accepted, string? source)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Starward", "log");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, LogFileName);
            var info = new FileInfo(file);
            if (info.Exists && info.Length > 64 * 1024)
            {
                File.Delete(file);
            }
            var lines = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(accepted is null ? "no shared folder resolved, upstream resolution is used" : $"data folder = {accepted}  (from {source})")}",
            };
            lines.AddRange(tried.Select(x => "    tried " + x));
            File.AppendAllLines(file, lines);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GachaOnlyDataFolder: breadcrumb failed: {ex.Message}");
        }
    }

}
