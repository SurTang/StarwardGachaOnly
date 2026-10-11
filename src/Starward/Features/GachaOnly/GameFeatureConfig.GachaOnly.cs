// GachaOnly fork — the per-game page table for the standalone gacha build.
//
// Hooked from GameFeatureConfig.FromGameId with a single line:
//     return ApplyGachaOnlyProfile(config, gameId.GameBiz);
// In a plain Starward build ApplyGachaOnlyProfile returns the upstream config untouched, so this
// file is inert there and the fork still builds stock Starward from the same source tree.
//
// Games with gacha records keep exactly the gacha pages; games without them (Honkai Impact 3rd)
// get the notice page instead of the launcher, so nothing in the app can navigate to a feature
// that the fork does not expose.
//
// This file is new in the fork; upstream never touches it.

using Starward.Core;
using Starward.Features.Gacha;

namespace Starward.Features;

internal partial class GameFeatureConfig
{


    /// <summary>
    /// Gacha-capable games (Genshin / Star Rail / Zenless Zone Zero, every server).
    /// Only the gacha record pages stay reachable; every other navigation item collapses.
    /// </summary>
    private static readonly GameFeatureConfig GachaOnly = new()
    {
        SupportedPages =
        [
            nameof(GachaLogPage),
            nameof(GenshinBeyondGachaPage),
        ],
        InGameNoticesWindow = false,
        SupportHardLink = false,
        SupportCloudGame = false,
        SupportGameAccountSwitcher = false,
        SupportDailyNote = false,
    };


    /// <summary>
    /// Games that have no gacha record (Honkai Impact 3rd, and anything unknown).
    /// </summary>
    private static readonly GameFeatureConfig GachaOnlyNoGachaGame = new()
    {
        SupportedPages =
        [
            nameof(GachaOnlyUnsupportedPage),
        ],
        InGameNoticesWindow = false,
        SupportHardLink = false,
        SupportCloudGame = false,
        SupportGameAccountSwitcher = false,
        SupportDailyNote = false,
    };


    /// <summary>
    /// The one hook called by <see cref="FromGameId(GameId?)"/>. Returns the upstream config
    /// unchanged unless this is the GachaOnly build.
    /// </summary>
    private static GameFeatureConfig ApplyGachaOnlyProfile(GameFeatureConfig upstream, GameBiz biz)
    {
        if (!GachaOnlyProfile.Enabled)
        {
            return upstream;
        }
        return biz.Game switch
        {
            GameBiz.hk4e or GameBiz.hkrpg or GameBiz.nap => GachaOnly,
            _ => GachaOnlyNoGachaGame,
        };
    }

}
