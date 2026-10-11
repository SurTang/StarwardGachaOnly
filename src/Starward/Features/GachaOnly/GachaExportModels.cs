// GachaOnly fork — data model + metric computation for the "export records as an image" feature.
//
// Reuses the existing gacha services: GachaLogService.GetGachaTypeStats(uid) already returns, per
// banner, the pity/average/UP statistics and the 5★ item list with Icon / Pity / IsUp filled in
// (the per-game overrides join the item info tables).
//
// This file is new in the fork; upstream never touches it.

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Core;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Starward.Features.Gacha;


internal sealed class GachaExportMetric
{
    public string Value { get; init; } = "";

    public string Label { get; init; } = "";

    /// <summary>Draw the value in the accent colour (win rate, averages).</summary>
    public bool Emphasize { get; init; }
}


internal sealed class GachaExportHistoryItem
{
    public ImageSource? Icon { get; set; }

    public string IconUrl { get; init; } = "";

    public string Name { get; init; } = "";

    public int Pity { get; init; }

    public bool IsUp { get; init; }

    /// <summary>Whether UP / off-banner is meaningful for this banner (event banners only).</summary>
    public bool ShowUpTag { get; init; }

    public int Year { get; init; }

    public string DateText { get; init; } = "";
}


internal sealed class GachaExportHistoryGroup
{
    public string YearText { get; init; } = "";

    public List<GachaExportHistoryItem> Items { get; init; } = [];
}


/// <summary>What the exported card shows: the pull records, or the per-item pull counts.</summary>
internal enum GachaExportMode
{
    Records,

    Counts,
}


/// <summary>One counted item in <see cref="GachaExportMode.Counts"/>.</summary>
internal sealed class GachaExportCountItem
{
    public ImageSource? Icon { get; set; }

    public string IconUrl { get; init; } = "";

    public string Name { get; init; } = "";

    public int Count { get; init; }

    public bool IsCharacter { get; init; }
}


internal sealed class GachaExportCountSection
{
    public string Title { get; init; } = "";

    public string Note { get; init; } = "";

    public List<GachaExportCountItem> Items { get; init; } = [];
}


internal sealed class GachaExportBannerBlock
{
    public string Title { get; init; } = "";

    public string CountText { get; init; } = "";

    public string TimeRangeText { get; init; } = "";

    public string Note { get; init; } = "";

    public List<GachaExportMetric> Metrics { get; init; } = [];

    public List<GachaExportHistoryGroup> History { get; init; } = [];
}


internal sealed class GachaExportModel
{
    public string Title { get; init; } = "";

    public string Subtitle { get; init; } = "";

    public string GameIcon { get; init; } = "";

    public string Footer { get; init; } = "";

    public List<GachaExportBannerBlock> Banners { get; init; } = [];

    /// <summary>Only filled in <see cref="GachaExportMode.Counts"/>.</summary>
    public List<GachaExportCountSection> CountSections { get; init; } = [];
}


/// <summary>
/// Localised strings for the export card. Kept out of the Starward.Language project on purpose so
/// the shared resx files stay untouched and keep merging cleanly from upstream.
/// </summary>
internal sealed class GachaExportStrings
{

    public static GachaExportStrings Current { get; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" ? Chinese : English;


    public required string Records { get; init; }

    public required string UidPrefix { get; init; }

    public required string TotalDraws { get; init; }

    public required string Overview { get; init; }

    public required string History5 { get; init; }

    public required string Pity5NotYet { get; init; }

    public required string Average5 { get; init; }

    public required string WinRate { get; init; }

    public required string Worst { get; init; }

    public required string Pity4NotYet { get; init; }

    public required string Average5Up { get; init; }

    public required string CostOfUp { get; init; }

    public required string Best { get; init; }

    public required string DrawUnit { get; init; }

    public required string YearSuffix { get; init; }

    public required string ExportedAt { get; init; }

    public required string CurrencyGenshin { get; init; }

    public required string CurrencyStarRail { get; init; }

    public required string CurrencyZZZ { get; init; }

    public required string SaveAsPng { get; init; }

    public required string CopyImage { get; init; }

    public required string Generating { get; init; }

    public required string NoRecords { get; init; }

    public required string SavedTo { get; init; }

    public required string ExportFailed { get; init; }

    public required string Copied { get; init; }

    public required string ExportAsImage { get; init; }

    public required string HistoryTruncated { get; init; }

    public required string BannerSuffix { get; init; }

    public required string TagUp { get; init; }

    public required string ExportCounts { get; init; }

    public required string CountsRecords { get; init; }

    public required string CountsUnit { get; init; }

    public required string Rarity5 { get; init; }

    public required string Rarity4 { get; init; }

    public required string CountItemsTruncated { get; init; }


    private static GachaExportStrings Chinese => new()
    {
        Records = "祈愿记录",
        UidPrefix = "UID",
        TotalDraws = "共 {0} 抽",
        Overview = "数据总览",
        History5 = "五星历史",
        Pity5NotYet = "未出五星",
        Average5 = "五星平均",
        WinRate = "小保底不歪",
        Worst = "最非",
        Pity4NotYet = "未出四星",
        Average5Up = "UP平均",
        CostOfUp = "UP花费{0}",
        Best = "最欧",
        DrawUnit = "抽",
        YearSuffix = "年",
        ExportedAt = "导出时间",
        CurrencyGenshin = "原石",
        CurrencyStarRail = "星琼",
        CurrencyZZZ = "菲林",
        SaveAsPng = "保存为 PNG",
        CopyImage = "复制到剪贴板",
        Generating = "正在生成…",
        NoRecords = "当前账号没有抽卡记录",
        SavedTo = "已保存到",
        ExportFailed = "导出失败",
        Copied = "已复制到剪贴板",
        ExportAsImage = "导出为图片",
        HistoryTruncated = "仅显示最近 {0} 个五星记录，{1} 个更早的已省略",
        BannerSuffix = "个卡池",
        TagUp = "UP",
        ExportCounts = "导出统计数量",
        CountsRecords = "抽卡数量统计",
        CountsUnit = "次",
        Rarity5 = "五星",
        Rarity4 = "四星",
        CountItemsTruncated = "仅显示前 {0} 项，其余 {1} 项已省略",
    };


    private static GachaExportStrings English => new()
    {
        Records = "Wish records",
        UidPrefix = "UID",
        TotalDraws = "{0} pulls",
        Overview = "Overview",
        History5 = "5★ history",
        Pity5NotYet = "5★ pity",
        Average5 = "5★ average",
        WinRate = "50/50 win rate",
        Worst = "Worst",
        Pity4NotYet = "4★ pity",
        Average5Up = "UP average",
        CostOfUp = "UP cost ({0})",
        Best = "Best",
        DrawUnit = "pulls",
        YearSuffix = "",
        ExportedAt = "exported",
        CurrencyGenshin = "Primogems",
        CurrencyStarRail = "Stellar Jade",
        CurrencyZZZ = "Polychrome",
        SaveAsPng = "Save as PNG",
        CopyImage = "Copy to clipboard",
        Generating = "Rendering…",
        NoRecords = "This account has no gacha records",
        SavedTo = "saved to",
        ExportFailed = "export failed",
        Copied = "copied to clipboard",
        ExportAsImage = "Export as image",
        HistoryTruncated = "showing the newest {0} 5★ records, {1} older ones omitted",
        BannerSuffix = " banners",
        TagUp = "UP",
        ExportCounts = "Export count statistics",
        CountsRecords = "Pull count statistics",
        CountsUnit = "times",
        Rarity5 = "5★",
        Rarity4 = "4★",
        CountItemsTruncated = "showing the first {0} items, {1} more omitted",
    };

}


internal static class GachaExportModelBuilder
{

    /// <summary>One pull costs this many primogems / stellar jade / polychrome in all three games.</summary>
    private const int DrawCost = 160;

    /// <summary>Guard rail: keeps the single-page column render inside the RenderTargetBitmap limit.
    /// Only reached by very long histories.</summary>
    private const int MaxHistoryItems = 160;

    /// <summary>Same guard rail for the count export.</summary>
    private const int MaxCountItems = 240;


    /// <summary>Weapon-ish ItemType values across the games and the languages the game data can use.</summary>
    private static readonly HashSet<string> WeaponItemTypes =
    [
        "武器", "光锥", "音擎", "武器", "光円錐", "音動機",
        "Weapon", "Light Cone", "W-Engine",
    ];


    /// <summary>
    /// Standard (off-banner) 5★ pools for banners that Starward's GachaNoUp table does not cover.
    /// Upstream has entries for hk4e301, hkrpg11/12/21/22 and nap2/3 — the Genshin *weapon* banner
    /// (hk4e302) is missing, so the app cannot tell UP from off-banner there and the export used to
    /// show no UP data at all for it. The list below is the ten 5★ weapons of the permanent pool;
    /// it was validated against this account (the weapon banner shows 7 off-banner pulls out of 26,
    /// i.e. 27% against an expected 25%, and a 73.7% 50/50 win rate).
    /// Delete this entry if upstream ever adds hk4e302 to GachaNoUp.
    /// </summary>
    private static readonly HashSet<string> GenshinStandardWeapons =
    [
        "天空之刃", "天空之傲", "天空之翼", "天空之卷", "天空之脊",
        "狼的末路", "四风原典", "阿莫斯之弓", "和璞鸢", "风鹰剑",
    ];

    private static readonly Dictionary<string, HashSet<string>> ExtraStandardPools = new()
    {
        ["hk4e302"] = GenshinStandardWeapons,
    };


    private static bool TryGetExtraStandardPool(GameBiz biz, int gachaType, out HashSet<string> pool)
    {
        return ExtraStandardPools.TryGetValue($"{biz.Game}{gachaType}", out pool!);
    }


    public static string GetGameIcon(GameBiz biz)
    {
        return biz.Game switch
        {
            GameBiz.hk4e => "ms-appx:///Assets/Image/icon_ys.jpg",
            GameBiz.hkrpg => "ms-appx:///Assets/Image/icon_sr.jpg",
            GameBiz.nap => "ms-appx:///Assets/Image/icon_zzz.jpg",
            _ => "",
        };
    }


    public static GachaLogService? GetService(GameBiz biz)
    {
        return biz.Game switch
        {
            GameBiz.hk4e => AppConfig.GetService<GenshinGachaService>(),
            GameBiz.hkrpg => AppConfig.GetService<StarRailGachaService>(),
            GameBiz.nap => AppConfig.GetService<ZZZGachaService>(),
            _ => null,
        };
    }


    public static string GetGameName(GameBiz biz)
    {
        return biz.Game switch
        {
            GameBiz.hk4e => "原神 / Genshin Impact",
            GameBiz.hkrpg => "崩坏：星穹铁道 / Honkai: Star Rail",
            GameBiz.nap => "绝区零 / Zenless Zone Zero",
            _ => biz.Value,
        };
    }


    public static long GetLastUid(GameBiz biz)
    {
        return AppConfig.GetLastUidInGachaLogPage(biz.Game);
    }


    public static async Task<GachaExportModel?> BuildAsync(GameBiz biz, long uid, CancellationToken cancellationToken = default)
    {
        GachaLogService? service = GetService(biz);
        if (service is null || uid <= 0)
        {
            return null;
        }
        var (stats, _) = await Task.Run(() => service.GetGachaTypeStats(uid), cancellationToken);
        GachaExportStrings s = GachaExportStrings.Current;

        var banners = new List<(int Count, bool SupportsUp, GachaExportBannerBlock Block)>();
        foreach (GachaTypeStats stat in stats)
        {
            if (stat.Count <= 0)
            {
                continue;
            }
            List<GachaLogItemEx> five = stat.List_5.Where(x => x.ItemId > 0).ToList();
            string note = five.Count > MaxHistoryItems
                ? string.Format(s.HistoryTruncated, MaxHistoryItems, five.Count - MaxHistoryItems)
                : "";
            // the app flags UP/off-banner itself whenever GachaNoUp knows the banner; for the Genshin
            // weapon banner (and any future gap) fall back to our own standard pool
            bool appKnowsUp = five.Exists(x => x.HasUpItem);
            HashSet<string>? standardPool = null;
            bool deriveUp = !appKnowsUp && TryGetExtraStandardPool(biz, stat.GachaType, out standardPool);
            bool supportsUp = appKnowsUp || deriveUp;
            bool IsUp(GachaLogItemEx item)
            {
                if (appKnowsUp)
                {
                    return item.IsUp;
                }
                return deriveUp && standardPool is not null && !standardPool.Contains(item.Name);
            }

            banners.Add((stat.Count, supportsUp, new GachaExportBannerBlock
            {
                Title = stat.GachaTypeText,
                CountText = string.Format(s.TotalDraws, stat.Count),
                TimeRangeText = $"{stat.StartTime:yyyy-MM-dd} ~ {stat.EndTime:yyyy-MM-dd}",
                Note = note,
                Metrics = BuildMetrics(biz, stat, five, s, supportsUp, IsUp),
                History = BuildHistory(five, s, supportsUp, IsUp),
            }));
        }

        if (banners.Count == 0)
        {
            return null;
        }

        // event banners first (most pulls first), the permanent banner last — for Genshin that reads
        // 角色池 / 武器池 / 常驻池, for Star Rail 角色/光锥/联动 … / 群星跃迁. A banner only counts as
        // an event banner when UP/off-banner is meaningful for it (see the UP resolution above), which
        // is exactly how the permanent banner is told apart without hardcoding type ids.
        List<GachaExportBannerBlock> ordered = banners
            .OrderBy(x => x.SupportsUp ? 0 : 1)
            .ThenByDescending(x => x.Count)
            .Select(x => x.Block)
            .ToList();
        var model = new GachaExportModel
        {
            Title = $"{GetGameName(biz)} · {s.Records}",
            Subtitle = $"{s.UidPrefix} {uid} · {banners.Min(x => x.Block.TimeRangeText[..10])} ~ {banners.Max(x => x.Block.TimeRangeText[^10..])} · "
                       + string.Format(s.TotalDraws, banners.Sum(x => x.Count)) + $" · {banners.Count}{s.BannerSuffix}",
            GameIcon = GetGameIcon(biz),
            Footer = $"Starward Gacha · {s.ExportedAt} {DateTime.Now:yyyy-MM-dd HH:mm} · github.com/Scighost/Starward",
            Banners = ordered,
        };

        await LoadIconsAsync(model, cancellationToken);
        return model;
    }


    public static async Task<GachaExportModel?> BuildCountsAsync(GameBiz biz, long uid, CancellationToken cancellationToken = default)
    {
        GachaLogService? service = GetService(biz);
        if (service is null || uid <= 0)
        {
            return null;
        }
        var (stats, itemStats) = await Task.Run(() => service.GetGachaTypeStats(uid), cancellationToken);
        GachaExportStrings s = GachaExportStrings.Current;

        var sections = new List<GachaExportCountSection>();
        foreach (IGrouping<int, GachaLogItemEx> group in itemStats
                     .Where(x => x.ItemId > 0 && x.RankType >= 4)
                     .GroupBy(x => x.RankType)
                     .OrderByDescending(x => x.Key))
        {
            // count first, then characters before weapons on a tie, then by name
            List<GachaLogItemEx> ordered = group
                .OrderByDescending(x => x.ItemCount)
                .ThenBy(x => IsCharacterItem(x.ItemType) ? 0 : 1)
                .ThenBy(x => x.Name, StringComparer.CurrentCulture)
                .ToList();
            string note = ordered.Count > MaxCountItems
                ? string.Format(s.CountItemsTruncated, MaxCountItems, ordered.Count - MaxCountItems)
                : "";
            sections.Add(new GachaExportCountSection
            {
                Title = $"{(group.Key == 5 ? s.Rarity5 : s.Rarity4)} · {ordered.Count}",
                Note = note,
                Items = ordered.Take(MaxCountItems).Select(x => new GachaExportCountItem
                {
                    Name = x.Name,
                    IconUrl = x.Icon,
                    Count = x.ItemCount,
                    IsCharacter = IsCharacterItem(x.ItemType),
                }).ToList(),
            });
        }

        if (sections.Count == 0)
        {
            return null;
        }

        var model = new GachaExportModel
        {
            Title = $"{GetGameName(biz)} · {s.CountsRecords}",
            Subtitle = $"{s.UidPrefix} {uid} · {string.Format(s.TotalDraws, stats.Where(x => x.Count > 0).Sum(x => x.Count))} · "
                       + string.Join(" / ", sections.Select(x => x.Title)),
            GameIcon = GetGameIcon(biz),
            Footer = $"Starward Gacha · {s.ExportedAt} {DateTime.Now:yyyy-MM-dd HH:mm} · github.com/Scighost/Starward",
            CountSections = sections,
        };

        await LoadIconsAsync(model, cancellationToken);
        return model;
    }


    /// <summary>
    /// ItemType comes from the game's own data and is localised, so the weapon kinds are matched by
    /// name (a character is anything that is not one of them; an unknown type therefore counts as a
    /// character, which is the safer default for the "characters first" ordering).
    /// </summary>
    private static bool IsCharacterItem(string? itemType)
    {
        return string.IsNullOrWhiteSpace(itemType) || !WeaponItemTypes.Contains(itemType);
    }


    private static List<GachaExportMetric> BuildMetrics(GameBiz biz, GachaTypeStats stat, List<GachaLogItemEx> five, GachaExportStrings s,
                                                        bool supportsUp, Func<GachaLogItemEx, bool> isUp)
    {
        // order by the game's own record id: imported archives often share the same minute-precision
        // timestamp, so ordering by Time would scramble the sequence and skew the guarantee tracking
        List<GachaLogItemEx> chronological = five.OrderBy(x => x.Id).ToList();
        int worst = chronological.Count > 0 ? chronological.Max(x => x.Pity) : 0;
        int best = chronological.Count > 0 ? chronological.Min(x => x.Pity) : 0;
        List<GachaLogItemEx> ups = chronological.Where(isUp).ToList();
        (int coinFlips, int coinWins) = CountCoinFlips(chronological, isUp);
        // the game's own average covers banners the app understands; otherwise compute it ourselves
        double average5Up = stat.Count_5_Up > 0 ? stat.Average_5_Up : (ups.Count > 0 ? ups.Average(x => x.Pity) : 0);
        Serilog.Log.Information("[GachaExport] {Title}: five={Five} up={Up} flips={Flips} wins={Wins} avgUp={AvgUp:F1} supportsUp={SupportsUp} types={Types}",
                                stat.GachaTypeText, chronological.Count, ups.Count, coinFlips, coinWins, average5Up, supportsUp,
                                string.Join(',', chronological.GroupBy(x => x.GachaType).Select(g => $"{g.Key}:{g.Count()}")));

        var metrics = new List<GachaExportMetric>
        {
            new() { Value = $"{stat.Pity_5}{s.DrawUnit}", Label = s.Pity5NotYet },
            new() { Value = $"{stat.Average_5:F1}{s.DrawUnit}", Label = s.Average5, Emphasize = true },
        };
        if (supportsUp && coinFlips > 0)
        {
            metrics.Add(new GachaExportMetric { Value = $"{(double)coinWins / coinFlips:P1}", Label = s.WinRate, Emphasize = true });
        }
        metrics.Add(new GachaExportMetric { Value = $"{worst}{s.DrawUnit}", Label = s.Worst });
        metrics.Add(new GachaExportMetric { Value = $"{stat.Pity_4}{s.DrawUnit}", Label = s.Pity4NotYet });
        if (supportsUp && ups.Count > 0)
        {
            string currency = biz.Game switch
            {
                GameBiz.hk4e => s.CurrencyGenshin,
                GameBiz.hkrpg => s.CurrencyStarRail,
                GameBiz.nap => s.CurrencyZZZ,
                _ => "",
            };
            metrics.Add(new GachaExportMetric { Value = $"{average5Up:F1}{s.DrawUnit}", Label = s.Average5Up, Emphasize = true });
            metrics.Add(new GachaExportMetric { Value = FormatCost(average5Up * DrawCost), Label = string.Format(s.CostOfUp, currency) });
        }
        metrics.Add(new GachaExportMetric { Value = $"{best}{s.DrawUnit}", Label = s.Best });
        return metrics;
    }


    /// <summary>
    /// 50/50 bookkeeping over the banner's 5★ sequence: after a non-UP 5★ the next one is the
    /// guaranteed one, so it is not a coin flip. Matches how the community tools present
    /// "小保底不歪" for a banner block.
    /// </summary>
    private static (int Flips, int Wins) CountCoinFlips(List<GachaLogItemEx> chronological, Func<GachaLogItemEx, bool> isUp)
    {
        int flips = 0, wins = 0;
        bool first = true;
        bool previousWasUp = false;
        foreach (GachaLogItemEx item in chronological)
        {
            // a coin flip is a 5★ pulled right after an UP 5★ (or the first recorded one); the 5★
            // following a non-UP one is guaranteed, so it must not be counted
            if (first || previousWasUp)
            {
                flips++;
                if (isUp(item))
                {
                    wins++;
                }
            }
            first = false;
            previousWasUp = isUp(item);
        }
        return (flips, wins);
    }


    private static string FormatCost(double value)
    {
        if (value >= 10000)
        {
            return $"{value / 10000:F2}w";
        }
        return $"{value:F0}";
    }


    private static List<GachaExportHistoryGroup> BuildHistory(List<GachaLogItemEx> five, GachaExportStrings s,
                                                             bool supportsUp, Func<GachaLogItemEx, bool> isUp)
    {
        return five.OrderByDescending(x => x.Id)
                   .Take(MaxHistoryItems)
                   .GroupBy(x => x.Time.Year)
                   .Select(g => new GachaExportHistoryGroup
                   {
                       YearText = $"{g.Key}{s.YearSuffix}",
                       Items = g.Select(x => new GachaExportHistoryItem
                       {
                           Name = x.Name,
                           IconUrl = x.Icon,
                           Pity = x.Pity,
                           IsUp = isUp(x),
                           ShowUpTag = supportsUp,
                           Year = x.Time.Year,
                           DateText = x.Time.ToString("MM-dd"),
                       }).ToList(),
                   })
                   .ToList();
    }


    private static async Task LoadIconsAsync(GachaExportModel model, CancellationToken cancellationToken)
    {
        List<GachaExportHistoryItem> historyItems = [.. model.Banners.SelectMany(x => x.History).SelectMany(x => x.Items)];
        List<GachaExportCountItem> countItems = [.. model.CountSections.SelectMany(x => x.Items)];
        List<string> urls = [.. historyItems.Select(x => x.IconUrl)
                                           .Concat(countItems.Select(x => x.IconUrl))
                                           .Where(x => !string.IsNullOrWhiteSpace(x))
                                           .Distinct()];
        if (urls.Count == 0)
        {
            return;
        }
        var index = new Dictionary<string, int>();
        for (int i = 0; i < urls.Count; i++)
        {
            index[urls[i]] = i;
        }

        // 1) download (or hit) the image cache on background threads
        string?[] files = new string?[urls.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, urls.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
            async (i, token) =>
            {
                try
                {
                    files[i] = await FileCache.GetFromCacheAsync(new Uri(urls[i], UriKind.Absolute), false, token);
                }
                catch (Exception)
                {
                    files[i] = null;
                }
            });

        // 2) decode them on the UI thread — BitmapImage is a XAML object and cannot be created
        //    on a thread pool thread
        var images = new ImageSource?[urls.Count];
        for (int i = 0; i < urls.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(files[i]))
            {
                images[i] = await DecodeIconAsync(files[i]!, cancellationToken);
            }
        }

        foreach (GachaExportHistoryItem item in historyItems)
        {
            if (index.TryGetValue(item.IconUrl, out int i))
            {
                item.Icon = images[i];
            }
        }
        foreach (GachaExportCountItem item in countItems)
        {
            if (index.TryGetValue(item.IconUrl, out int i))
            {
                item.Icon = images[i];
            }
        }
    }


    private static async Task<ImageSource?> DecodeIconAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            var bitmap = new BitmapImage();
            using IRandomAccessStream stream = await file.OpenReadAsync();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }


    /// <summary>Loads one icon from the Starward image cache (downloads once, then offline).</summary>
    public static async Task<ImageSource?> LoadIconAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        try
        {
            var uri = new Uri(url, UriKind.Absolute);
            string? file = await FileCache.GetFromCacheAsync(uri, false, cancellationToken);
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                return null;
            }
            return await DecodeIconAsync(file, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

}


