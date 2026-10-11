// GachaOnly fork — preview + PNG export window for the current game / current account.
//
// The card is rendered with RenderTargetBitmap at 2x so text and icons stay crisp. Because a
// ScrollViewer clips its content, the window is temporarily enlarged to the full card size before
// rendering (otherwise a tall card would be cut off at the viewport edge).
//
// This file is new in the fork; upstream never touches it.

using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Core;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;

namespace Starward.Features.Gacha;

public sealed partial class GachaExportWindow : WindowEx
{

    private const double RenderScale = 2.0;

    /// <summary>RenderTargetBitmap refuses to render anything larger than this in one dimension.</summary>
    private const double MaxRenderPixels = 4000;


    private readonly ILogger<GachaExportWindow> _logger = AppConfig.GetLogger<GachaExportWindow>();

    private readonly GameBiz _biz;

    private long _uid;

    private GachaExportModel? _model;

    private readonly GachaExportMode _mode;


    internal GachaExportWindow(GameBiz biz, long uid, GachaExportMode mode = GachaExportMode.Records)
    {
        this.InitializeComponent();
        _biz = biz;
        _mode = mode;
        _uid = uid > 0 ? uid : GachaExportModelBuilder.GetLastUid(biz);
        GachaExportStrings s = GachaExportStrings.Current;
        string what = mode == GachaExportMode.Counts ? s.CountsRecords : s.Records;
        Title = $"{GachaExportModelBuilder.GetGameName(biz)} · {what}";
        Button_Save.Content = s.SaveAsPng;
        Button_Copy.Content = s.CopyImage;
        TextBlock_Status.Text = s.Generating;
        CenterInScreen(1240, 900);
    }


    /// <summary>Current game, or null when the caller did not resolve one yet.</summary>
    public GameBiz GameBiz => _biz;


    public long Uid => _uid;


    private async void Grid_Root_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            GachaExportStrings s = GachaExportStrings.Current;
            _model = _mode == GachaExportMode.Counts
                ? await GachaExportModelBuilder.BuildCountsAsync(_biz, _uid)
                : await GachaExportModelBuilder.BuildAsync(_biz, _uid);
            if (_model is null)
            {
                TextBlock_Status.Text = s.NoRecords;
                if (_autoExportPath is not null)
                {
                    _logger.LogError("No gacha records to export for {Biz} uid {Uid}", _biz.Value, _uid);
                    Environment.Exit(1);
                }
                return;
            }
            Card.SetModel(_model);
            TextBlock_Header.Text = _model.Title;
            await Task.Delay(250); // let the images and the layout settle
            ProgressRing_Busy.IsActive = false;
            ProgressRing_Busy.Visibility = Visibility.Collapsed;
            TextBlock_Status.Text = _model.Subtitle;
            Button_Save.IsEnabled = true;
            Button_Copy.IsEnabled = true;
            if (_autoExportPath is not null)
            {
                await ExportAndExitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build gacha export model");
            ProgressRing_Busy.IsActive = false;
            TextBlock_Status.Text = ex.Message;
        }
    }


    private async Task ExportAndExitAsync()
    {
        bool ok = await ExportToFileAsync(_autoExportPath!);
        Environment.Exit(ok ? 0 : 1);
    }


    private string? _autoExportPath;


    /// <summary>Set by the command line export path; when set the window exports and exits by itself.</summary>
    public void SetAutoExport(string path)
    {
        _autoExportPath = path;
        Button_Save.IsEnabled = true;
        Button_Copy.IsEnabled = true;
    }


    private async void Button_Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_model is null)
            {
                return;
            }
            GachaExportStrings s = GachaExportStrings.Current;
            string suggestName = _mode == GachaExportMode.Counts
                ? $"Starward_GachaCount_{_biz.Game}_{_uid}_{DateTime.Now:yyyyMMddHHmmss}.png"
                : $"Starward_Gacha_{_biz.Game}_{_uid}_{DateTime.Now:yyyyMMddHHmmss}.png";
            string? path = await FileDialogHelper.OpenSaveFileDialogAsync(Content.XamlRoot, suggestName, ("PNG", ".png"));
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
            TextBlock_Status.Text = s.Generating;
            if (await ExportToFileAsync(path))
            {
                TextBlock_Status.Text = s.SavedTo + " " + path;
                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(path);
                var options = new FolderLauncherOptions();
                options.ItemsToSelect.Add(storageFile);
                await Launcher.LaunchFolderAsync(await storageFile.GetParentAsync(), options);
            }
            else
            {
                TextBlock_Status.Text = s.ExportFailed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save gacha image");
            TextBlock_Status.Text = ex.Message;
        }
    }


    private async void Button_Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_model is null)
            {
                return;
            }
            string file = Path.Combine(Path.GetTempPath(), $"Starward_Gacha_{_biz.Game}_{_uid}.png");
            if (await ExportToFileAsync(file))
            {
                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(file);
                ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, new[] { storageFile });
                TextBlock_Status.Text = GachaExportStrings.Current.Copied;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy gacha image");
            TextBlock_Status.Text = ex.Message;
        }
    }


    /// <summary>
    /// Renders the card in one pass and writes it as a PNG file.
    /// RenderTargetBitmap is capped at 4096 px per dimension, so if the card is taller than that the
    /// whole card is scaled down just enough to fit — the layout is column based, which keeps that
    /// from happening for normal record sets, and unlike page stitching it never leaves a seam.
    /// Also used by the command line export.
    /// </summary>
    public async Task<bool> ExportToFileAsync(string path)
    {
        try
        {
            if (_model is null)
            {
                return false;
            }

            Card.SetModel(_model);
            Card.UpdateLayout();
            await Task.Delay(220); // let the icons and the layout settle
            _logger.LogInformation("[GachaExport] {Layout}", Card.DescribeLayout());

            double cardWidth = Card.CardWidth;
            double cardHeight = Card.ActualHeight;
            if (cardWidth < 10 || cardHeight < 10)
            {
                return false;
            }
            double scale = Math.Clamp(MaxRenderPixels / cardHeight, 0.5, RenderScale);

            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(Card, (int)Math.Round(cardWidth * scale), (int)Math.Round(cardHeight * scale));
            byte[] pixels = await ReadPixelsAsync(bitmap);

            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }
            // a plain FileStream works for the save picker (file already created) and for the
            // command line export (file does not exist yet)
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (IRandomAccessStream stream = fileStream.AsRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                                     (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
                await encoder.FlushAsync();
            }
            _logger.LogInformation("Gacha image exported to {Path} ({Width}x{Height}, layout {CardW}x{CardH}, scale {Scale:F2})",
                                   path, bitmap.PixelWidth, bitmap.PixelHeight, cardWidth, cardHeight, scale);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export gacha image to {Path}", path);
            return false;
        }
    }


    private static async Task<byte[]> ReadPixelsAsync(RenderTargetBitmap bitmap)
    {
        IBuffer buffer = await bitmap.GetPixelsAsync();
        byte[] pixels = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(pixels);
        return pixels;
    }

}
