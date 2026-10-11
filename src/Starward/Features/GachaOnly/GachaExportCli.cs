// GachaOnly fork — command line entry for the headless image export.
//
//   Starward.GachaOnly.exe --export-gacha-image <out.png> [--uid <uid>] [--game hk4e|hkrpg|nap]
//
// Useful for automation (e.g. a scheduled snapshot of the account), and it is also how the export
// pipeline can be verified without clicking through the UI.
//
// This file is new in the fork; upstream never touches it.

using Starward.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Starward.Features.Gacha;

internal static class GachaExportCli
{

    private const string Option = "--export-gacha-image";

    private const string UidOption = "--uid";

    private const string GameOption = "--game";

    private const string CountsOption = "--export-gacha-counts";


    /// <summary>
    /// Runs the export when the command line asks for it. Returns false when it is not our command
    /// (then the caller continues with the normal start-up). The process exits by itself when done.
    /// </summary>
    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (!GachaOnlyProfile.Enabled || args.Length == 0)
        {
            return false;
        }

        string? outPath = null;
        long uid = 0;
        GameBiz? game = null;
        GachaExportMode mode = GachaExportMode.Records;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(Option, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outPath = args[++i];
            }
            else if (args[i].Equals(CountsOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outPath = args[++i];
                mode = GachaExportMode.Counts;
            }
            else if (args[i].Equals(UidOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                _ = long.TryParse(args[++i], out uid);
            }
            else if (args[i].Equals(GameOption, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                game = new GameBiz(args[++i]);
            }
        }

        if (string.IsNullOrWhiteSpace(outPath))
        {
            return false;
        }

        string path = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var window = new GachaExportWindow(game ?? AppConfig.CurrentGameBiz, uid, mode);
        window.SetAutoExport(path);
        window.Activate();
        await Task.CompletedTask;
        return true;
    }

}
