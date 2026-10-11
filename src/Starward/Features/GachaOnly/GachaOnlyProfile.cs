// GachaOnly fork — single switch controlling every behavioural difference from upstream Starward.
//
// Rules for keeping `git rebase upstream/main` cheap:
//   * upstream files receive at most one hook line each, and the hook reads `GachaOnlyProfile.Enabled`;
//   * all logic lives in this folder (Features/GachaOnly), never inside Features/Gacha/**;
//   * `Enabled` is a static readonly field, not a const, so the upstream build still compiles every
//     code path (no CS0162 unreachable warnings) and behaves exactly like upstream.
//
// This file is new in the fork; upstream never touches it, so it can never conflict.

using Starward.Features.GameLauncher;
using Starward.Features.ViewHost;
using System;

namespace Starward.Features.Gacha;

internal static class GachaOnlyProfile
{

#if GACHA_ONLY
    private const bool BuildIsGachaOnly = true;
#else
    private const bool BuildIsGachaOnly = false;
#endif


    /// <summary>
    /// True for the standalone gacha-record build (<c>-p:GachaOnly=true</c>), false for a plain
    /// Starward build of the same source tree.
    /// </summary>
    public static readonly bool Enabled = BuildIsGachaOnly;


    /// <summary>
    /// Human readable product name, used in window titles, logs and diagnostics.
    /// </summary>
    public static string ProductName => Enabled ? "Starward Gacha" : "Starward";


    /// <summary>
    /// The page MainView falls back to: upstream jumps to the game launcher, the gacha build jumps
    /// to the gacha record page (or to the "this game has no gacha record" notice page).
    /// </summary>
    public static Type GetFallbackPage(GameFeatureConfig? config)
    {
        if (!Enabled)
        {
            return typeof(GameLauncherPage);
        }
        if (config is null)
        {
            return typeof(BlankPage);
        }
        if (config.SupportedPages.Contains(nameof(GachaLogPage)))
        {
            return typeof(GachaLogPage);
        }
        if (config.SupportedPages.Contains(nameof(GenshinBeyondGachaPage)))
        {
            return typeof(GenshinBeyondGachaPage);
        }
        return typeof(GachaOnlyUnsupportedPage);
    }

}
