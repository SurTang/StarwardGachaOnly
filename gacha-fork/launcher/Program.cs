// GachaOnly fork — portable-package launcher.
//
// Mirrors upstream's src/Starward.Launcher (C++): read version.ini, start
// app-<version>\Starward.exe with the same command line, then delete any other app-* folder that an
// in-place update left behind. The C++ project cannot be built on a machine without the MSVC
// toolchain, hence this equivalent.
//
// Not part of Starward.slnx, so upstream merges never see it.

using System.Diagnostics;
using System.Runtime.InteropServices;

const uint MB_OKCANCEL = 0x00000001;
const uint MB_ICONWARNING = 0x00000030;
const int IDOK = 1;

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

[DllImport("user32.dll")]
static extern bool SetProcessDPIAware();

[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
static extern IntPtr ShellExecuteW(IntPtr hwnd, string? op, string file, string? parameters, string? directory, int showCmd);

string baseFolder = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)!;
string? runExe = null;

// 1) version.ini -> app-<version>\Starward.exe
string versionFile = Path.Combine(baseFolder, "version.ini");
if (File.Exists(versionFile))
{
    string? version = ReadIniValue(versionFile, "version");
    if (!string.IsNullOrWhiteSpace(version))
    {
        string candidate = Path.Combine(baseFolder, "app-" + version, "Starward.exe");
        if (File.Exists(candidate))
        {
            runExe = candidate;
        }
    }
}

// 2) otherwise the newest Starward*.exe under any app-* folder — a packaged build ships
//    app-<version>\Starward.exe, a development one Starward.GachaOnly.exe
if (runExe is null)
{
    runExe = Directory.EnumerateDirectories(baseFolder, "app-*")
                     .SelectMany(dir => Directory.EnumerateFiles(dir, "Starward*.exe"))
                     .Where(file => Path.GetFileName(file) is "Starward.exe" or "Starward.GachaOnly.exe")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .FirstOrDefault();
}

if (runExe is not null)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = runExe,
        WorkingDirectory = Path.GetDirectoryName(runExe)!,
        UseShellExecute = false,
    };
    foreach (string arg in Environment.GetCommandLineArgs().Skip(1))
    {
        startInfo.ArgumentList.Add(arg);
    }
    Process.Start(startInfo);

    // drop the app folders an in-place update superseded
    string current = Path.GetFileName(Path.GetDirectoryName(runExe)!);
    foreach (string dir in Directory.EnumerateDirectories(baseFolder, "app-*"))
    {
        if (!string.Equals(Path.GetFileName(dir), current, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception)
            {
                // a running or read-only folder is not worth failing the launch over
            }
        }
    }
}
else
{
    SetProcessDPIAware();
    const string url = "https://github.com/Scighost/Starward";
    int choice = MessageBoxW(IntPtr.Zero,
                             $"Starward files not found.\r\nWould you like to visit the project page?\r\n{url}",
                             "Starward Gacha",
                             MB_ICONWARNING | MB_OKCANCEL);
    if (choice == IDOK)
    {
        ShellExecuteW(IntPtr.Zero, null, url, null, null, 1);
    }
}


static string? ReadIniValue(string path, string key)
{
    // version.ini only ever holds one global key, so a full INI parser is not needed
    foreach (string raw in File.ReadAllLines(path))
    {
        string line = raw.Trim();
        int separator = line.IndexOf('=');
        if (separator > 0 && line[..separator].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            return line[(separator + 1)..].Trim();
        }
    }
    return null;
}
