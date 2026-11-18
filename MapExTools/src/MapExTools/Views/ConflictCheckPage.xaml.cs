using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Packages;
using MapExTools.Services;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Views;

/// <summary>
/// 冲突预检页（v1.1）：只读展示当前激活 Playset 与本地 <c>Mods/</c> 的静态元数据冲突评估卡片，
/// 并支持对可通过关闭纯经济分组规避的 🟠 条件冲突执行一键关闭并保存。
/// </summary>
public partial class ConflictCheckPage : UserControl
{
    private static readonly SolidColorBrush s_hardBadgeBrush = CreateFrozenBrush(0xDC, 0x26, 0x26);
    private static readonly SolidColorBrush s_warnBadgeBrush = CreateFrozenBrush(0xD9, 0x77, 0x06);
    private static readonly SolidColorBrush s_mitigatedBadgeBrush = CreateFrozenBrush(0x25, 0x63, 0xEB);
    private static readonly SolidColorBrush s_infoBadgeBrush = CreateFrozenBrush(0x6B, 0x72, 0x80);

    public ConflictCheckPage()
    {
        InitializeComponent();
        ShowNotesCheckBox.IsChecked = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RefreshView();
    }

    private static ConflictScanService ScanService => App.Services.Conflicts;

    private static CocSession Coc => App.Services.Coc;

    private static string CurrentLanguage => Loc.Instance.Language;

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnStateChanged;
        ScanService.Changed += OnStateChanged;
        Coc.Changed += OnStateChanged;

        if (ScanService.Report == null && !ScanService.IsScanning && ScanService.LastScanError == null)
        {
            ScanService.TriggerScan();
        }

        RefreshView();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnStateChanged;
        ScanService.Changed -= OnStateChanged;
        Coc.Changed -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => RefreshView();

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        App.Services.RefreshGameLocation();
        ScanService.TriggerScan();
    }

    private void OnShowNotesChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            RefreshView();
    }

    private void RefreshView()
    {
        string lang = CurrentLanguage;
        bool scanning = ScanService.IsScanning;
        ConflictReport? report = ScanService.Report;

        RescanButton.IsEnabled = !scanning;

        if (scanning && report == null)
        {
            BannerStatusText.DataContext = new ToneLine(Loc.Instance["Conflicts.Scanning"], PackageTone.Accent);
            HeaderMetaText.Text = string.Empty;
            UnsavedBasisText.Visibility = Visibility.Collapsed;
            PlaysetErrorText.Visibility = Visibility.Collapsed;
            CardsHost.Children.Clear();
            EmptyStateText.Visibility = Visibility.Collapsed;
            return;
        }

        if (report == null)
        {
            if (!string.IsNullOrEmpty(ScanService.LastScanError))
            {
                BannerStatusText.DataContext = new ToneLine(
                    Loc.Instance.Format("Conflicts.ScanFailed", ScanService.LastScanError),
                    PackageTone.Error);
            }
            else
            {
                BannerStatusText.DataContext = new ToneLine(Loc.Instance["Conflicts.Scanning"], PackageTone.Muted);
            }

            HeaderMetaText.Text = string.Empty;
            return;
        }

        DetailLine banner = ConflictText.StatusBanner(report, lang);
        string bannerText = scanning
            ? $"{banner.Value} ({Loc.Instance["Conflicts.Scanning"]})"
            : banner.Value;
        BannerStatusText.DataContext = new ToneLine(bannerText, banner.Tone);
        HeaderMetaText.Text = ConflictText.HeaderSummary(report, lang);

        if (Coc.HasUnsavedChanges)
        {
            // 一键关组只写入对应的键，其他未保存改动仍留在工作副本：另按已保存的设定评估一次，磁盘上仍冲突的模组在提示里点名
            ConflictReport? savedReport = Coc.State != null
                ? ConflictAnalyzer.Evaluate(report.Workspace, ConflictEvalSettings.FromDictionary(Coc.State.GetAllValues()))
                : null;
            DetailLine note = ConflictText.UnsavedBasisNote(report, savedReport, lang);
            UnsavedBasisText.DataContext = new ToneLine(note.Value, note.Tone);
            UnsavedBasisText.Visibility = Visibility.Visible;
        }
        else
        {
            UnsavedBasisText.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrEmpty(report.Workspace.Playset.PlaysetConfigError))
        {
            PlaysetErrorText.DataContext = new ToneLine(
                Loc.Instance.Format("Conflicts.PlaysetReadError", report.Workspace.Playset.PlaysetConfigError),
                PackageTone.Warning);
            PlaysetErrorText.Visibility = Visibility.Visible;
        }
        else
        {
            PlaysetErrorText.Visibility = Visibility.Collapsed;
        }

        bool showNotes = ShowNotesCheckBox.IsChecked == true;
        List<ModConflictItem> visibleItems = report.Items
            .Where(i => showNotes || i.Severity >= ConflictSeverity.Warning)
            .ToList();

        CardsHost.Children.Clear();
        if (visibleItems.Count == 0)
        {
            EmptyStateText.Text = Loc.Instance["Conflicts.EmptyList"];
            EmptyStateText.Visibility = Visibility.Visible;
            CardsScroll.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        CardsScroll.Visibility = Visibility.Visible;

        foreach (ModConflictItem item in visibleItems)
        {
            CardsHost.Children.Add(BuildConflictCard(item, report.Settings, lang));
        }
    }

    private Border BuildConflictCard(ModConflictItem item, ConflictEvalSettings settings, string lang)
    {
        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");

        var rootStack = new StackPanel();

        // --- 顶部标题行：徽章 + 商店显示名 + 版本信息 + 右侧来源与一键关闭按钮 ---
        var headerDock = new DockPanel { LastChildFill = true };

        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };

        var sourceTag = new TextBlock
        {
            Text = ConflictText.SourceLabel(item, lang),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, item.CanOneClickMitigate ? 10 : 0, 0),
        };
        sourceTag.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        rightPanel.Children.Add(sourceTag);

        if (item.CanOneClickMitigate)
        {
            var actionBtn = new Button
            {
                Content = ConflictText.OneClickButtonLabel(item.OneClickDisableKeys, lang),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = Coc.IsUsable && !Coc.GameRunning,
                Tag = item.OneClickDisableKeys,
            };
            actionBtn.SetResourceReference(StyleProperty, "AccentButtonStyle");
            if (Coc.GameRunning)
                actionBtn.ToolTip = Loc.Instance["Startup.Running"];
            actionBtn.Click += OnOneClickDisableGroups;
            rightPanel.Children.Add(actionBtn);
        }

        DockPanel.SetDock(rightPanel, Dock.Right);
        headerDock.Children.Add(rightPanel);

        var leftHeader = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };

        var badge = new Border
        {
            Background = BadgeBrushFor(item.Severity),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ConflictText.SeverityBadge(item.Severity, lang),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
            },
        };
        leftHeader.Children.Add(badge);

        var titleText = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        leftHeader.Children.Add(titleText);

        var versionText = new TextBlock
        {
            Text = ConflictText.VersionLabel(item, lang),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        versionText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        leftHeader.Children.Add(versionText);

        headerDock.Children.Add(leftHeader);
        rootStack.Children.Add(headerDock);

        // --- 中性观测事实明细 ---
        var findingsPanel = new StackPanel { Margin = new Thickness(2, 6, 0, 0) };
        foreach (ConflictFinding finding in item.Findings)
        {
            var bulletRow = new TextBlock
            {
                Text = "• " + ConflictText.DescribeFinding(finding, settings, lang),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
                DataContext = new ToneLine(string.Empty, ConflictText.ToneOf(finding.Severity)),
            };

            if (finding.Severity is ConflictSeverity.Hard or ConflictSeverity.Warning)
                bulletRow.SetResourceReference(StyleProperty, "ToneText");

            findingsPanel.Children.Add(bulletRow);
        }

        rootStack.Children.Add(findingsPanel);
        card.Child = rootStack;
        return card;
    }

    private void OnOneClickDisableGroups(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: IReadOnlyList<string> keys } || keys.Count == 0)
            return;

        var updates = new Dictionary<string, object>(keys.Count, StringComparer.Ordinal);
        foreach (string key in keys)
        {
            updates[key] = false;
        }

        Coc.SaveSpecificKeys(updates, Window.GetWindow(this));
    }

    private static SolidColorBrush BadgeBrushFor(ConflictSeverity severity) => severity switch
    {
        ConflictSeverity.Hard => s_hardBadgeBrush,
        ConflictSeverity.Warning => s_warnBadgeBrush,
        ConflictSeverity.Mitigated => s_mitigatedBadgeBrush,
        _ => s_infoBadgeBrush,
    };
}
