// Compatibility shim for the upstream "game management" refactor — NOT part of upstream Starward.
//
// Why this file exists
// --------------------
// Upstream commit 607a3077 "game management" (2026-10-08) removed the per-launcher overloads from
// Starward.Core.HoYoPlay.HoYoPlayClient (the ones taking a launcherId plus a language) and moved
// them, as launcher-bound methods, into the new Starward.GameManagement project. Starward.RPC and
// Starward/Features/HoYoPlay/HoYoPlayService.cs still call the old shapes, so at that commit the
// whole solution does not compile — upstream CI failed on it (Actions run #2322, "failure"), and
// the snapshot this fork was created from inherits the breakage.
//
// What it does
// ------------
// Re-declares the legacy overloads as extension methods, so no upstream file has to be edited.
// Each one routes the call to a HoYoPlayClient bound to the requested launcher: the launcher
// catalog is static in Core (LauncherConfig.ChinaOfficial / GlobalOfficial / Bilibili*), so this is
// a faithful implementation, not a stub — the game list, backgrounds, configs and branches all come
// from the correct per-launcher endpoint again.
//
// Notes
//   * Language is applied to the target client (the modern client reads LauncherConfig and
//     Language from its instance); the per-launcher clients are cached process-wide.
//   * The Sophon overloads accept the GameBranch the callers pass but only the GameBranchPackage is
//     used, exactly like the current Core implementation.
//   * Instance methods win over extension methods, so if upstream restores these overloads this
//     file silently becomes dead code; delete it then.
//
// Nothing here is exercised by the standalone gacha build beyond the game list that GameSelector
// needs, but the project has to compile.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Core.HoYoPlay;

public static class HoYoPlayClientLegacyOverloads
{

    private static readonly ConcurrentDictionary<string, HoYoPlayClient> _launcherClients = new();


    private static LauncherConfig? ToLauncherConfig(string launcherId)
    {
        return launcherId switch
        {
            LauncherId.ChinaOfficial => LauncherConfig.ChinaOfficial,
            LauncherId.GlobalOfficial => LauncherConfig.GlobalOfficial,
            LauncherId.BilibiliGenshin => LauncherConfig.BilibiliGenshin,
            LauncherId.BilibiliStarRail => LauncherConfig.BilibiliStarRail,
            LauncherId.BilibiliZZZ => LauncherConfig.BilibiliZZZ,
            _ => null,
        };
    }


    /// <summary>
    /// Returns a client bound to <paramref name="launcherId"/>, or <paramref name="fallback"/> when the
    /// launcher is unknown (then the caller's own client configuration is used).
    /// </summary>
    private static HoYoPlayClient Resolve(HoYoPlayClient fallback, string launcherId, string? lang)
    {
        HoYoPlayClient client;
        if (fallback.LauncherConfig?.Id == launcherId)
        {
            client = fallback;
        }
        else
        {
            LauncherConfig? config = ToLauncherConfig(launcherId);
            if (config is null)
            {
                client = fallback;
            }
            else
            {
                client = _launcherClients.GetOrAdd(launcherId, id => new HoYoPlayClient(CreateHttpClient())
                {
                    LauncherConfig = ToLauncherConfig(id)!,
                    Language = lang ?? "en-us",
                });
            }
        }
        if (!string.IsNullOrWhiteSpace(lang))
        {
            try
            {
                client.Language = lang;
            }
            catch (Exception)
            {
                // LanguageUtil rejects unknown values; keep the previous language in that case.
            }
        }
        return client;
    }


    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        })
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
    }


    #region list results (Starward/Features/HoYoPlay/HoYoPlayService.cs)


    public static async Task<List<GameInfo>> GetGameInfoAsync(this HoYoPlayClient client, string launcherId, string? lang, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGameInfoAsync(cancellationToken);
    }


    public static async Task<List<GameBackgroundInfo>> GetGameBackgroundAsync(this HoYoPlayClient client, string launcherId, string? lang, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGameBackgroundAsync(cancellationToken);
    }


    public static async Task<GameContent> GetGameContentAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGameContentAsync(gameId, cancellationToken);
    }


    public static async Task<List<GamePackage>> GetGamePackageAsync(this HoYoPlayClient client, string launcherId, string? lang, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGamePackageAsync(null, cancellationToken);
    }


    public static async Task<List<GameConfig>> GetGameConfigAsync(this HoYoPlayClient client, string launcherId, string? lang, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGameConfigAsync(null, cancellationToken);
    }


    public static async Task<List<GameChannelSDK>> GetGameChannelSDKAsync(this HoYoPlayClient client, string launcherId, string? lang, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGameChannelSDKAsync(null, cancellationToken);
    }


    public static async Task<List<GameBranch>> GetGameBranchAsync(this HoYoPlayClient client, string launcherId, string? lang, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetGameBranchAsync(null, cancellationToken);
    }


    public static async Task<List<GameDXConfig>> GetDXConfigsAsync(this HoYoPlayClient client, string launcherId, string? lang, IEnumerable<GameId>? gameIds, IEnumerable<GPUInfo> gpuInfos, CancellationToken cancellationToken = default)
    {
        return await Resolve(client, launcherId, lang).GetDXConfigsAsync(gpuInfos, gameIds, cancellationToken);
    }


    #endregion


    #region single results filtered by game id


    public static async Task<GameConfig?> GetGameConfigAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        List<GameConfig>? list = await Resolve(client, launcherId, lang).GetGameConfigAsync(new[] { gameId }, cancellationToken);
        return list?.FirstOrDefault(x => x.GameId == gameId);
    }


    public static async Task<GamePackage?> GetGamePackageAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        List<GamePackage>? list = await Resolve(client, launcherId, lang).GetGamePackageAsync(new[] { gameId }, cancellationToken);
        return list?.FirstOrDefault(x => x.GameId == gameId);
    }


    public static async Task<GameBranch?> GetGameBranchAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        List<GameBranch>? list = await Resolve(client, launcherId, lang).GetGameBranchAsync(new[] { gameId }, cancellationToken);
        return list?.FirstOrDefault(x => x.GameId == gameId);
    }


    public static async Task<GameChannelSDK?> GetGameChannelSDKAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        List<GameChannelSDK>? list = await Resolve(client, launcherId, lang).GetGameChannelSDKAsync(new[] { gameId }, cancellationToken);
        return list?.FirstOrDefault(x => x.GameId == gameId);
    }


    public static async Task<GameDeprecatedFileConfig?> GetGameDeprecatedFileConfigAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        List<GameDeprecatedFileConfig>? list = await Resolve(client, launcherId, lang).GetGameDeprecatedFileConfigAsync(new[] { gameId }, cancellationToken);
        return list?.FirstOrDefault(x => x.GameId == gameId);
    }


    public static async Task<WPFPackageInfo?> GetWPFPackageAsync(this HoYoPlayClient client, string launcherId, string? lang, GameId gameId, CancellationToken cancellationToken = default)
    {
        List<WPFPackageInfo>? list = await Resolve(client, launcherId, lang).GetWPFPackagesAsync(new[] { gameId }, cancellationToken);
        return list?.FirstOrDefault(x => x.GameId == gameId);
    }


    #endregion


    #region sophon (branch package only, the branch itself is context)


    public static Task<GameSophonChunkBuild> GetGameSophonChunkBuildAsync(this HoYoPlayClient client, GameBranch gameBranch, GameBranchPackage gameBranchPackage, string? tag = null, CancellationToken cancellationToken = default)
    {
        return client.GetGameSophonChunkBuildAsync(gameBranchPackage, tag, cancellationToken);
    }


    public static Task<GameSophonPatchBuild> GetGameSophonPatchBuildAsync(this HoYoPlayClient client, GameBranch gameBranch, GameBranchPackage gameBranchPackage, CancellationToken cancellationToken = default)
    {
        return client.GetGameSophonPatchBuildAsync(gameBranchPackage, cancellationToken);
    }


    #endregion

}
