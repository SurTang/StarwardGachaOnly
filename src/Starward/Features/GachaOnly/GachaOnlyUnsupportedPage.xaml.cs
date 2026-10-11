// GachaOnly fork — notices page for games without gacha records.
//
// This file is new in the fork; upstream never touches it. Strings are resolved locally instead of
// being added to the Starward.Language project, so the shared resx files stay untouched and
// `git rebase upstream/main` keeps merging them cleanly.

using Starward.Core;
using Starward.Frameworks;
using System.Globalization;

namespace Starward.Features.Gacha;

public sealed partial class GachaOnlyUnsupportedPage : PageBase
{

    public GachaOnlyUnsupportedPage()
    {
        this.InitializeComponent();
    }


    protected override void OnLoaded()
    {
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        (string title, string message) = GetText(language);
        TextBlock_Title.Text = title;
        TextBlock_Message.Text = string.Format(message, GetGameName(language));
    }


    private string GetGameName(string language)
    {
        return CurrentGameBiz.Game switch
        {
            GameBiz.bh3 => language switch
            {
                "zh" => "崩坏3",
                "ja" => "崩壊3rd",
                _ => "Honkai Impact 3rd",
            },
            _ => CurrentGameBiz.Value,
        };
    }


    private static (string Title, string Message) GetText(string language)
    {
        return language switch
        {
            "zh" => ("该游戏没有抽卡记录",
                     "当前选择的游戏（{0}）没有抽卡记录功能。\n请在左上角切换到《原神》《崩坏：星穹铁道》或《绝区零》。"),
            "ja" => ("このゲームにはガチャ記録がありません",
                     "選択中のゲーム（{0}）にはガチャ記録機能がありません。\n左上で『原神』『崩壊：スターレイル』『ゼンレスゾーンゼロ』に切り替えてください。"),
            _ => ("This game has no gacha records",
                  "The selected game ({0}) has no gacha record feature.\nSwitch to Genshin Impact, Honkai: Star Rail or Zenless Zone Zero in the top left corner."),
        };
    }

}
