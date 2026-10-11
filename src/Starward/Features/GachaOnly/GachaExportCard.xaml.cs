// GachaOnly fork — renders the export model into a XAML card that can be captured as a PNG.
//
// Layout choice (this is the interesting part):
//   * one header + one footer + a two column "masonry" body:每 column is a stack of whole banner
//     blocks, packed greedily by height (tallest block first, then into the currently shorter
//     column);
//   * compared to one-column-per-banner (the layout other record tools use) this avoids the large
//     empty tail a short banner column leaves under a 90-item character banner, keeps the aspect
//     ratio around 3:4 (comfortable to share) and still leaves room for 7 larger icons per row;
//   * and compared to stacking one card per page it needs no stitching at all: the whole card is
//     rendered in one RenderTargetBitmap pass, so there is no seam and the header is not repeated.
//
// The body is built in code instead of data templates so the wrapping grids have a deterministic
// layout on the first pass.
//
// This file is new in the fork; upstream never touches it.

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace Starward.Features.Gacha;

public sealed partial class GachaExportCard : UserControl
{

    /// <summary>Icon cells per row in a column (two column layout reserves less width).</summary>
    private const int CellsPerRowMulti = 7;

    private const int CellsPerRowSingle = 14;

    private const double CellWidth = 96;

    private const double CellHeight = 118;

    /// <summary>Cell geometry for the count export (same width, slightly taller for the name line).</summary>
    private const double CountCellHeight = 126;

    private const double CountIconHeight = 96;

    private const int MetricColumnsMulti = 4;

    private const int MetricColumnsSingle = 8;

    /// <summary>Used only for balancing the columns, not for the final layout.</summary>
    private const int EstimateCellsPerRow = 7;


    private static readonly Color Accent = Color.FromArgb(255, 0x1E, 0x9E, 0x5A);
    private static readonly Color TextStrong = Color.FromArgb(255, 0x15, 0x1A, 0x23);
    private static readonly Color TextSecondary = Color.FromArgb(255, 0x6B, 0x72, 0x80);
    private static readonly Color TextFaint = Color.FromArgb(255, 0x9A, 0xA1, 0xAC);
    private static readonly Color LineColor = Color.FromArgb(255, 0xE4, 0xE7, 0xEE);
    private static readonly Color TileBackground = Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
    private static readonly Color IconBackground = Color.FromArgb(255, 0xF1, 0xF3, 0xF8);
    private static readonly Color UpColor = Color.FromArgb(255, 0x22, 0xC5, 0x5E);
    private static readonly Color PlainColor = Color.FromArgb(255, 0x64, 0x74, 0x8B);
    private static readonly Color CharacterCountColor = Color.FromArgb(255, 0x25, 0x63, 0xEB);
    private static readonly Color YearPillBackground = Color.FromArgb(255, 0xED, 0xF0, 0xF5);

    // pity pill background: 1-10 yellow, 11-40 blue, 41-75 purple, 76+ red
    private static readonly Color PityLucky = Color.FromArgb(255, 0xE0, 0xA8, 0x00);
    private static readonly Color PityNormal = Color.FromArgb(255, 0x3B, 0x82, 0xF6);
    private static readonly Color PityLate = Color.FromArgb(255, 0x8B, 0x5C, 0xF6);
    private static readonly Color PityWorst = Color.FromArgb(255, 0xEF, 0x44, 0x44);


    private static Color PityColorFor(int pity)
    {
        return pity switch
        {
            <= 10 => PityLucky,
            <= 40 => PityNormal,
            <= 75 => PityLate,
            _ => PityWorst,
        };
    }


    public GachaExportCard()
    {
        this.InitializeComponent();
    }


    /// <summary>Fixed layout width of the card (see GachaExportCard.xaml).</summary>
    public double CardWidth => Width;


    /// <summary>Diagnostic: what the layout actually came out as (used by the export log).</summary>
    internal string DescribeLayout()
    {
        var widths = Grid_Body.Children.OfType<FrameworkElement>().Select(x => $"{x.ActualWidth:F0}");
        var heights = Grid_Body.Children.OfType<FrameworkElement>().Select(x => $"{x.ActualHeight:F0}");
        return $"card={ActualWidth:F0}x{ActualHeight:F0} body={Grid_Body.ActualWidth:F0}x{Grid_Body.ActualHeight:F0} "
               + $"columnWidths=[{string.Join(",", widths)}] columnHeights=[{string.Join(",", heights)}]";
    }


    internal void SetModel(GachaExportModel model)
    {
        TextBlock_Title.Text = model.Title;
        TextBlock_Subtitle.Text = model.Subtitle;
        TextBlock_Footer.Text = model.Footer;
        if (!string.IsNullOrWhiteSpace(model.GameIcon))
        {
            Image_GameIcon.Source = new BitmapImage(new Uri(model.GameIcon));
            Image_GameIcon.Visibility = Visibility.Visible;
        }
        else
        {
            Image_GameIcon.Visibility = Visibility.Collapsed;
        }

        Grid_Body.Children.Clear();
        Grid_Body.ColumnDefinitions.Clear();

        if (model.CountSections.Count > 0)
        {
            // counts layout: one full width column, a section per rarity
            Grid_Body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            FrameworkElement counts = BuildCounts(model.CountSections);
            Grid.SetColumn(counts, 0);
            Grid_Body.Children.Add(counts);
            return;
        }

        List<List<GachaExportBannerBlock>> columns = Pack(model.Banners);
        int cellsPerRow = columns.Count <= 1 ? CellsPerRowSingle : CellsPerRowMulti;
        int metricColumns = columns.Count <= 1 ? MetricColumnsSingle : MetricColumnsMulti;
        for (int i = 0; i < columns.Count; i++)
        {
            Grid_Body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            FrameworkElement column = BuildColumn(columns[i], cellsPerRow, metricColumns);
            Grid.SetColumn(column, i);
            Grid_Body.Children.Add(column);
        }
    }


    /// <summary>
    /// The count-statistics card: one section per rarity, each a grid of icon cells carrying the
    /// pull count. Sorted by the caller (count desc, characters before weapons, then name).
    /// </summary>
    private static FrameworkElement BuildCounts(List<GachaExportCountSection> sections)
    {
        var panel = new StackPanel { Spacing = 18 };
        foreach (GachaExportCountSection section in sections)
        {
            var sectionPanel = new StackPanel { Spacing = 10 };
            sectionPanel.Children.Add(BuildSectionCaption(section.Title));
            var grid = new Grid();
            for (int i = 0; i < CellsPerRowSingle; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });
            }
            int rows = (section.Items.Count + CellsPerRowSingle - 1) / CellsPerRowSingle;
            for (int i = 0; i < rows; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CountCellHeight) });
            }
            for (int i = 0; i < section.Items.Count; i++)
            {
                FrameworkElement cell = BuildCountCell(section.Items[i]);
                Grid.SetRow(cell, i / CellsPerRowSingle);
                Grid.SetColumn(cell, i % CellsPerRowSingle);
                grid.Children.Add(cell);
            }
            sectionPanel.Children.Add(grid);
            if (!string.IsNullOrWhiteSpace(section.Note))
            {
                sectionPanel.Children.Add(new TextBlock
                {
                    Text = section.Note,
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(TextFaint),
                });
            }
            panel.Children.Add(new Border
            {
                Background = new SolidColorBrush(TileBackground),
                BorderBrush = new SolidColorBrush(LineColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Child = sectionPanel,
            });
        }
        return panel;
    }


    private static FrameworkElement BuildCountCell(GachaExportCountItem item)
    {
        GachaExportStrings strings = GachaExportStrings.Current;
        var cell = new Grid { Margin = new Thickness(3, 0, 3, 6) };
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CountIconHeight) });
        cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var frame = new Border
        {
            Background = new SolidColorBrush(IconBackground),
            CornerRadius = new CornerRadius(10),
        };
        if (item.Icon is not null)
        {
            frame.Child = new Image { Source = item.Icon, Stretch = Stretch.Uniform, Margin = new Thickness(3) };
        }
        else
        {
            frame.Child = new TextBlock
            {
                Text = item.Name,
                Margin = new Thickness(4),
                FontSize = 10.5,
                MaxLines = 3,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(TextSecondary),
            };
        }
        Grid.SetRow(frame, 0);
        cell.Children.Add(frame);

        var count = new Border
        {
            Background = new SolidColorBrush(item.IsCharacter ? CharacterCountColor : PlainColor),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(5, 0, 5, 1),
            Margin = new Thickness(3, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = $"{item.Count}{strings.CountsUnit}",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
        Grid.SetRow(count, 0);
        cell.Children.Add(count);

        var name = new TextBlock
        {
            Text = item.Name,
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 10.5,
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(TextSecondary),
        };
        Grid.SetRow(name, 1);
        cell.Children.Add(name);

        return cell;
    }


    /// <summary>
    /// Greedy two column packing that keeps the caller's banner order: the first banner opens the
    /// left column, every later banner goes into whichever column is currently shorter. With the
    /// record-count ordering that puts the biggest banner on the left and keeps the event banner
    /// above the permanent one on the right.
    /// </summary>
    private static List<List<GachaExportBannerBlock>> Pack(List<GachaExportBannerBlock> banners)
    {
        if (banners.Count == 0)
        {
            return [];
        }
        if (banners.Count == 1)
        {
            return [banners.ToList()];
        }

        int columnCount = 2;
        var columns = new List<List<GachaExportBannerBlock>>();
        var heights = new double[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            columns.Add([]);
        }
        foreach (GachaExportBannerBlock banner in banners)
        {
            int target = 0;
            for (int i = 1; i < columnCount; i++)
            {
                if (heights[i] < heights[target])
                {
                    target = i;
                }
            }
            columns[target].Add(banner);
            heights[target] += EstimateHeight(banner);
        }
        return columns.Where(x => x.Count > 0).ToList();
    }


    private static double EstimateHeight(GachaExportBannerBlock banner)
    {
        double height = 190; // title + metrics + captions + paddings
        foreach (GachaExportHistoryGroup group in banner.History)
        {
            height += 24 + Math.Ceiling(group.Items.Count / (double)EstimateCellsPerRow) * (CellHeight + 6);
        }
        return height;
    }


    private static FrameworkElement BuildColumn(List<GachaExportBannerBlock> blocks, int cellsPerRow, int metricColumns)
    {
        var panel = new StackPanel { Spacing = 16 };
        foreach (GachaExportBannerBlock block in blocks)
        {
            panel.Children.Add(BuildBannerBlock(block, cellsPerRow, metricColumns));
        }
        return panel;
    }


    private static FrameworkElement BuildBannerBlock(GachaExportBannerBlock banner, int cellsPerRow, int maxMetricColumns)
    {
        int metricColumns = MetricColumnsFor(banner.Metrics.Count, maxMetricColumns);
        var panel = new StackPanel { Spacing = 10 };

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = banner.Title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(TextStrong),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var count = new TextBlock
        {
            Text = $"{banner.CountText} · {banner.TimeRangeText}",
            Margin = new Thickness(8, 0, 0, 2),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Bottom,
            Foreground = new SolidColorBrush(TextFaint),
        };
        head.Children.Add(title);
        Grid.SetColumn(count, 1);
        head.Children.Add(count);
        panel.Children.Add(head);

        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(TileBackground),
            BorderBrush = new SolidColorBrush(LineColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 8),
            Child = BuildMetrics(banner.Metrics, metricColumns),
        });

        if (banner.History.Count > 0)
        {
            panel.Children.Add(BuildSectionCaption(GachaExportStrings.Current.History5));
            foreach (GachaExportHistoryGroup group in banner.History)
            {
                panel.Children.Add(BuildYearGroup(group, cellsPerRow));
            }
        }

        if (!string.IsNullOrWhiteSpace(banner.Note))
        {
            panel.Children.Add(new TextBlock
            {
                Text = banner.Note,
                Margin = new Thickness(0, 2, 0, 0),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(TextFaint),
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(TileBackground),
            BorderBrush = new SolidColorBrush(LineColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            VerticalAlignment = VerticalAlignment.Top,
            Child = panel,
        };
    }


    private static FrameworkElement BuildSectionCaption(string text)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new Microsoft.UI.Xaml.Shapes.Rectangle { Height = 1, Fill = new SolidColorBrush(LineColor), VerticalAlignment = VerticalAlignment.Center };
        var right = new Microsoft.UI.Xaml.Shapes.Rectangle { Height = 1, Fill = new SolidColorBrush(LineColor), VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock
        {
            Text = text,
            Margin = new Thickness(10, 0, 10, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(TextSecondary),
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(left);
        grid.Children.Add(label);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }


    /// <summary>Picks a tile count per row that divides the metric count evenly when possible
    /// (5-6 tiles in a 4-column layout would leave a lonely tile on the second row).</summary>
    private static int MetricColumnsFor(int count, int max)
    {
        int columns = count switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 or 6 => 3,
            _ => max,
        };
        return Math.Min(columns, max);
    }


    private static Grid BuildMetrics(List<GachaExportMetric> metrics, int metricColumns)
    {
        var grid = new Grid { RowSpacing = 6 };
        for (int i = 0; i < metricColumns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        int rows = (metrics.Count + metricColumns - 1) / metricColumns;
        for (int i = 0; i < rows; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (int i = 0; i < metrics.Count; i++)
        {
            GachaExportMetric metric = metrics[i];
            var tile = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center };
            tile.Children.Add(new TextBlock
            {
                Text = metric.Value,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(metric.Emphasize ? Accent : TextStrong),
            });
            tile.Children.Add(new TextBlock
            {
                Text = metric.Label,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(TextSecondary),
            });
            Grid.SetRow(tile, i / metricColumns);
            Grid.SetColumn(tile, i % metricColumns);
            grid.Children.Add(tile);
        }
        return grid;
    }


    private static FrameworkElement BuildYearGroup(GachaExportHistoryGroup group, int cellsPerRow)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(YearPillBackground),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 2, 10, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = group.YearText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextSecondary),
            },
        });

        var grid = new Grid();
        for (int i = 0; i < cellsPerRow; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });
        }
        int rows = (group.Items.Count + cellsPerRow - 1) / cellsPerRow;
        for (int i = 0; i < rows; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellHeight) });
        }
        for (int i = 0; i < group.Items.Count; i++)
        {
            FrameworkElement cell = BuildHistoryCell(group.Items[i]);
            Grid.SetRow(cell, i / cellsPerRow);
            Grid.SetColumn(cell, i % cellsPerRow);
            grid.Children.Add(cell);
        }
        panel.Children.Add(grid);
        return panel;
    }


    private static FrameworkElement BuildHistoryCell(GachaExportHistoryItem item)
    {
        var cell = new Grid { Margin = new Thickness(3, 0, 3, 6) };
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
        cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var frame = new Border
        {
            Background = new SolidColorBrush(IconBackground),
            CornerRadius = new CornerRadius(10),
        };
        if (item.Icon is not null)
        {
            frame.Child = new Image { Source = item.Icon, Stretch = Stretch.Uniform, Margin = new Thickness(3) };
        }
        else
        {
            frame.Child = new TextBlock
            {
                Text = item.Name,
                Margin = new Thickness(4),
                FontSize = 10.5,
                MaxLines = 3,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(TextSecondary),
            };
        }
        Grid.SetRow(frame, 0);
        cell.Children.Add(frame);

        var pity = new Border
        {
            Background = new SolidColorBrush(PityColorFor(item.Pity)),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(5, 0, 5, 1),
            Margin = new Thickness(3, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = item.Pity.ToString(),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
        Grid.SetRow(pity, 0);
        cell.Children.Add(pity);

        // event banners tag UP pulls; off-banner pulls carry no tag at all, the permanent banner none
        if (item.ShowUpTag && item.IsUp)
        {
            var tag = new Border
            {
                Background = new SolidColorBrush(UpColor),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4, 0, 4, 1),
                Margin = new Thickness(0, 3, 3, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = GachaExportStrings.Current.TagUp,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                },
            };
            Grid.SetRow(tag, 0);
            cell.Children.Add(tag);
        }

        var date = new TextBlock
        {
            Text = item.DateText,
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 10.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(TextFaint),
        };
        Grid.SetRow(date, 1);
        cell.Children.Add(date);

        return cell;
    }

}
