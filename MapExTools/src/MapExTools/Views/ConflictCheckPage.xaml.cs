using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Packages;
using MapExTools.Services;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Views;

/// <summary>模组列表单行绑定视图模型（供虚拟化 <see cref="ListBox"/> 零开销回收复用）。</summary>
public sealed class ModListRowViewModel
{
    public required string OrderText { get; init; }

    public required Brush DotBrush { get; init; }

    public required double RowOpacity { get; init; }

    public required string Title { get; init; }

    public required string VersionText { get; init; }

    public required Visibility VersionVisibility { get; init; }

    public required string MetadataText { get; init; }

    public required Visibility MetadataVisibility { get; init; }

    public required string ConflictBadgeText { get; init; }

    public required Brush ConflictBadgeBrush { get; init; }

    public required Visibility ConflictBadgeVisibility { get; init; }

    public required string StateBadgeText { get; init; }

    /// <summary>供 <c>App.xaml</c> 的 <c>ToneText</c> 样式按状态动态着色。</summary>
    public required PackageTone Tone { get; init; }

    public required string SourceText { get; init; }

    public string? ToolTipText { get; init; }
}

/// <summary>
/// 模组与冲突预检页：支持只读切换本地任意 Playset（含未激活播放集），
/// 提供「冲突预检」分级卡片（支持一键关闭可规避纯经济分组）与「模组列表」虚拟化清单双视图。
/// </summary>
public partial class ConflictCheckPage : UserControl
{
    private static readonly SolidColorBrush s_hardBadgeBrush = CreateFrozenBrush(0xDC, 0x26, 0x26);
    private static readonly SolidColorBrush s_warnBadgeBrush = CreateFrozenBrush(0xB4, 0x53, 0x09);
    private static readonly SolidColorBrush s_mitigatedBadgeBrush = CreateFrozenBrush(0x25, 0x63, 0xEB);
    private static readonly SolidColorBrush s_infoBadgeBrush = CreateFrozenBrush(0x6B, 0x72, 0x80);
    private static readonly SolidColorBrush s_goodDotBrush = CreateFrozenBrush(0x16, 0xA3, 0x4A);
    private static readonly SolidColorBrush s_mutedDotBrush = CreateFrozenBrush(0x9C, 0xA3, 0xAF);

    private bool _populatingPlaysetBox;
    private bool _populatingModFilterBox;
    private ModListFilterKind _modFilter = ModListFilterKind.All;

    public ConflictCheckPage()
    {
        InitializeComponent();
        ShowNotesCheckBox.IsChecked = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RefreshView();
    }

    private enum ModListFilterKind
    {
        All,
        Enabled,
        Disabled,
        CodeMods,
        NonCode,
        LocalMods,
        Attention,
    }

    private sealed record PlaysetChoice(string? PlaysetId, string Display);

    private sealed record ModFilterChoice(ModListFilterKind Kind, string Display);

    private static ConflictScanService ScanService => App.Services.Conflicts;

    private static CocSession Coc => App.Services.Coc;

    private static string CurrentLanguage => Loc.Instance.Language;

    private bool IsModListView => ModsViewToggle.IsChecked == true;

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

    private void OnStateChanged(object? sender, EventArgs e)
    {
        ClearCopyNote();
        RefreshView();
    }

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        ClearCopyNote();
        App.Services.RefreshGameLocation();
        ScanService.TriggerScan();
    }

    private void OnPlaysetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populatingPlaysetBox || PlaysetBox.SelectedItem is not PlaysetChoice choice)
            return;

        ClearCopyNote();
        ScanService.SelectPlayset(choice.PlaysetId);
    }

    private void OnSubViewChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ClearCopyNote();
        ApplySubViewVisibility();
        RefreshView();
    }

    private void OnShowNotesChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            RefreshView();
    }

    private void OnModFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populatingModFilterBox || ModFilterBox.SelectedItem is not ModFilterChoice choice)
            return;

        _modFilter = choice.Kind;
        ClearCopyNote();
        if (ScanService.PreviewReport is { } report)
            PopulateModListRows(report, CurrentLanguage);
    }

    private void OnModKeywordChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ClearCopyNote();
        if (ScanService.PreviewReport is { } report)
            PopulateModListRows(report, CurrentLanguage);
    }

    private void OnCopyModListClick(object sender, RoutedEventArgs e)
    {
        ConflictReport? report = ScanService.PreviewReport;
        if (report == null)
            return;

        try
        {
            string markdown = ConflictText.FormatModListMarkdown(
                report.Workspace.Playset,
                report,
                report.Workspace.EnabledCodeModCount,
                CurrentLanguage);
            Clipboard.SetText(markdown);
            CopyModListNote.Text = Loc.Instance["Conflicts.Mods.Copied"];
            CopyModListNote.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                Loc.Instance.Format("Logs.CopyFailed", ex.Message),
                Loc.Instance["App.Title"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ClearCopyNote()
    {
        CopyModListNote.Text = string.Empty;
        CopyModListNote.Visibility = Visibility.Collapsed;
    }

    private void ApplySubViewVisibility()
    {
        bool modList = IsModListView;
        ConflictToolsPanel.Visibility = modList ? Visibility.Collapsed : Visibility.Visible;
        ConflictsContentHost.Visibility = modList ? Visibility.Collapsed : Visibility.Visible;
        ModListToolsPanel.Visibility = modList ? Visibility.Visible : Visibility.Collapsed;
        ModListContentHost.Visibility = modList ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshView()
    {
        string lang = CurrentLanguage;
        ApplySubViewVisibility();

        bool scanning = ScanService.IsPreviewScanning;
        ConflictReport? activeReport = ScanService.Report;
        ConflictReport? report = ScanService.PreviewReport;
        string? previewError = ScanService.LastPreviewError ?? (ScanService.SelectedPlaysetId == null ? ScanService.LastScanError : null);

        RescanButton.IsEnabled = !ScanService.IsScanning && !scanning;
        PopulatePlaysetBox(report?.Workspace.Playset ?? activeReport?.Workspace.Playset, lang, enabled: !ScanService.IsScanning);

        if (scanning && report == null)
        {
            BannerStatusText.DataContext = new ToneLine(Loc.Instance["Conflicts.Scanning"], PackageTone.Accent);
            ClearModeAndNoticeLines();
            HeaderMetaText.Text = string.Empty;
            RescanErrorText.Visibility = Visibility.Collapsed;
            CardsHost.Children.Clear();
            EmptyStateText.Visibility = Visibility.Collapsed;
            ModListBox.ItemsSource = null;
            ModListEmptyText.Visibility = Visibility.Collapsed;
            CopyModListButton.IsEnabled = false;
            UpdateSubViewHeaders(0, 0, lang);
            return;
        }

        if (report == null)
        {
            if (!string.IsNullOrEmpty(previewError))
            {
                BannerStatusText.DataContext = new ToneLine(
                    Loc.Instance.Format("Conflicts.ScanFailed", previewError),
                    PackageTone.Error);
            }
            else
            {
                BannerStatusText.DataContext = new ToneLine(Loc.Instance["Conflicts.Scanning"], PackageTone.Muted);
            }

            ClearModeAndNoticeLines();
            HeaderMetaText.Text = string.Empty;
            RescanErrorText.Visibility = Visibility.Collapsed;
            CopyModListButton.IsEnabled = false;
            UpdateSubViewHeaders(0, 0, lang);
            return;
        }

        PlaysetScanResult playset = report.Workspace.Playset;
        IReadOnlyList<PlaysetModListItem> allMods = playset.EffectiveAllModItems;
        CopyModListButton.IsEnabled = allMods.Count > 0;

        bool showNotes = ShowNotesCheckBox.IsChecked == true;
        List<ModConflictItem> visibleConflictItems = report.Items
            .Where(i => showNotes || i.Severity >= ConflictSeverity.Warning)
            .ToList();
        UpdateSubViewHeaders(report.HardConflictCount + report.WarningCount, allMods.Count, lang);

        // 摘要句以评估模式开头；下面按需给出适用范围、另一类模式结果、非激活预览说明、Off-disk 与未启用 MapExt 说明
        DetailLine banner = ConflictText.StatusBanner(report, lang);
        string bannerText = scanning
            ? $"{banner.Value} ({Loc.Instance["Conflicts.Scanning"]})"
            : banner.Value;
        BannerStatusText.DataContext = new ToneLine(bannerText, banner.Tone);
        ModeScopeText.Text = ConflictText.ModeScopeNote(report.Settings, lang);
        ModeScopeText.Visibility = Visibility.Visible;

        string? crossMode = ConflictText.CrossModeSummary(report, lang);
        CrossModeText.Text = crossMode ?? string.Empty;
        CrossModeText.Visibility = crossMode != null ? Visibility.Visible : Visibility.Collapsed;
        HeaderMetaText.Text = ConflictText.HeaderSummary(report, lang);

        SetOptionalToneLine(PreviewNoticeText, ConflictText.PreviewInactiveNote(playset, lang), PackageTone.Accent);
        SetOptionalToneLine(OffdiskNoticeText, ConflictText.OffdiskNote(playset, lang), PackageTone.Warning);
        SetOptionalToneLine(MapExtMissingText, ConflictText.MapExtMissingNote(playset, lang), PackageTone.Muted);

        if (Coc.HasUnsavedChanges)
        {
            ConflictReport? savedReport = Coc.State != null
                ? ScanService.EvaluatePreviewWith(ConflictEvalSettings.FromDictionary(Coc.State.GetAllValues()))
                : null;
            DetailLine note = ConflictText.UnsavedBasisNote(report, savedReport, lang);
            UnsavedBasisText.DataContext = new ToneLine(note.Value, note.Tone);
            UnsavedBasisText.Visibility = Visibility.Visible;
        }
        else
        {
            UnsavedBasisText.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrEmpty(playset.PlaysetConfigError))
        {
            PlaysetErrorText.DataContext = new ToneLine(
                Loc.Instance.Format("Conflicts.PlaysetReadError", playset.PlaysetConfigError),
                PackageTone.Warning);
            PlaysetErrorText.Visibility = Visibility.Visible;
        }
        else
        {
            PlaysetErrorText.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrEmpty(previewError))
        {
            RescanErrorText.DataContext = new ToneLine(
                Loc.Instance.Format("Conflicts.RescanFailed", previewError),
                PackageTone.Warning);
            RescanErrorText.Visibility = Visibility.Visible;
        }
        else
        {
            RescanErrorText.Visibility = Visibility.Collapsed;
        }

        PopulateConflictCards(report, visibleConflictItems, lang);
        PopulateModFilterBox(allMods, lang);
        PopulateModListRows(report, lang);
    }

    private void UpdateSubViewHeaders(int alertCount, int totalModCount, string lang)
    {
        ConflictsViewToggle.Content = string.Format(
            CultureInfo.InvariantCulture,
            Loc.Instance["Conflicts.View.Conflicts"],
            alertCount);
        ModsViewToggle.Content = string.Format(
            CultureInfo.InvariantCulture,
            Loc.Instance["Conflicts.View.ModList"],
            totalModCount);
    }

    private void PopulatePlaysetBox(PlaysetScanResult? playset, string lang, bool enabled)
    {
        _populatingPlaysetBox = true;
        try
        {
            IReadOnlyList<PlaysetSummary> available = playset?.EffectiveAvailablePlaysets ?? [];
            if (available.Count == 0)
            {
                var fallback = new PlaysetChoice(null, Loc.Instance["Conflicts.NoActivePlayset"]);
                PlaysetBox.ItemsSource = new[] { fallback };
                PlaysetBox.SelectedIndex = 0;
                PlaysetBox.IsEnabled = false;
                return;
            }

            var dupNames = available
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var choices = new List<PlaysetChoice>(available.Count + 1);
            bool hasActiveEntry = available.Any(p => p.IsActive);
            if (!hasActiveEntry)
            {
                // playset_config.json 未设激活项，但存有其他非激活播放集：首项代表当前激活态（仅本地 Mods）
                choices.Add(new PlaysetChoice(null, Loc.Instance["Conflicts.NoActivePlayset"]));
            }

            foreach (PlaysetSummary p in available)
            {
                // 选中当前激活项时内部记为 null（跟随外部激活切换），非激活项记其精确 Id
                string? choiceId = p.IsActive ? null : p.Id;
                string label = ConflictText.FormatPlaysetChoice(p, dupNames.Contains(p.Name), lang);
                choices.Add(new PlaysetChoice(choiceId, label));
            }

            string? selectedId = ScanService.SelectedPlaysetId;
            int idx = selectedId == null
                ? 0
                : choices.FindIndex(c => string.Equals(c.PlaysetId, selectedId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                idx = 0;

            PlaysetBox.ItemsSource = choices;
            PlaysetBox.SelectedIndex = idx;
            PlaysetBox.IsEnabled = enabled && choices.Count > 1;
        }
        finally
        {
            _populatingPlaysetBox = false;
        }
    }

    private void PopulateModFilterBox(IReadOnlyList<PlaysetModListItem> allMods, string lang)
    {
        _populatingModFilterBox = true;
        try
        {
            ModFilterChoice[] items =
            [
                new(ModListFilterKind.All, Loc.Instance.Format("Conflicts.Mods.Filter.All", allMods.Count)),
                new(ModListFilterKind.Enabled, Loc.Instance.Format("Conflicts.Mods.Filter.Enabled", allMods.Count(m => m.IsEnabled))),
                new(ModListFilterKind.Disabled, Loc.Instance.Format("Conflicts.Mods.Filter.Disabled", allMods.Count(m => !m.IsEnabled))),
                new(ModListFilterKind.CodeMods, Loc.Instance.Format("Conflicts.Mods.Filter.CodeMods", allMods.Count(m => m.LoadState == ModLoadState.LoadedCodeMod))),
                new(ModListFilterKind.NonCode, Loc.Instance.Format("Conflicts.Mods.Filter.NonCode", allMods.Count(m => m.LoadState == ModLoadState.AssetOrNonCode))),
                new(ModListFilterKind.LocalMods, Loc.Instance.Format("Conflicts.Mods.Filter.LocalMods", allMods.Count(m => m.Source == ModInstallSource.UserMods))),
                new(ModListFilterKind.Attention, Loc.Instance.Format("Conflicts.Mods.Filter.Attention", allMods.Count(IsAttentionModItem))),
            ];

            int idx = Array.FindIndex(items, x => x.Kind == _modFilter);
            ModFilterBox.ItemsSource = items;
            ModFilterBox.SelectedIndex = idx >= 0 ? idx : 0;
        }
        finally
        {
            _populatingModFilterBox = false;
        }
    }

    private void PopulateConflictCards(ConflictReport report, List<ModConflictItem> visibleItems, string lang)
    {
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

        bool isActiveTarget = report.Workspace.Playset.IsTargetActivePlayset;
        foreach (ModConflictItem item in visibleItems)
        {
            CardsHost.Children.Add(BuildConflictCard(item, report.Settings, isActiveTarget, lang));
        }
    }

    private void PopulateModListRows(ConflictReport report, string lang)
    {
        IReadOnlyList<PlaysetModListItem> allMods = report.Workspace.Playset.EffectiveAllModItems;
        if (allMods.Count == 0)
        {
            ModListBox.ItemsSource = null;
            ModListBox.Visibility = Visibility.Collapsed;
            ModListEmptyText.Text = Loc.Instance["Conflicts.Mods.Empty"];
            ModListEmptyText.Visibility = Visibility.Visible;
            return;
        }

        string keyword = ModKeywordBox.Text.Trim();
        var rows = new List<ModListRowViewModel>(allMods.Count);
        string localOrderLabel = Loc.Instance["Conflicts.Mods.OrderLocal"];

        foreach (PlaysetModListItem m in allMods)
        {
            if (!MatchesCategoryFilter(m, _modFilter))
                continue;
            if (!string.IsNullOrEmpty(keyword) && !MatchesKeyword(m, keyword))
                continue;

            ConflictSeverity sev = m.IsEnabled ? report.GetPackageSeverity(m.PackageKey) : ConflictSeverity.None;
            bool showConflictBadge = sev >= ConflictSeverity.Mitigated;

            Brush dotBrush = sev switch
            {
                ConflictSeverity.Hard => s_hardBadgeBrush,
                ConflictSeverity.Warning => s_warnBadgeBrush,
                _ => m.LoadState is ModLoadState.LoadedCodeMod or ModLoadState.AssetOrNonCode
                    ? s_goodDotBrush
                    : m.LoadState is ModLoadState.MissingDependencies or ModLoadState.UnreadableAssembly
                        ? s_warnBadgeBrush
                        : s_mutedDotBrush,
            };

            string verText = ConflictText.ModItemVersionLabel(m, lang);
            string metaLine = ConflictText.ModItemMetadataLine(m, lang);

            rows.Add(new ModListRowViewModel
            {
                OrderText = m.LoadOrder.HasValue
                    ? "#" + m.LoadOrder.Value.ToString(CultureInfo.InvariantCulture)
                    : localOrderLabel,
                DotBrush = dotBrush,
                RowOpacity = m.IsEnabled ? 1.0 : 0.65,
                Title = m.DisplayName,
                VersionText = verText,
                VersionVisibility = string.IsNullOrEmpty(verText) ? Visibility.Collapsed : Visibility.Visible,
                MetadataText = metaLine,
                MetadataVisibility = string.IsNullOrEmpty(metaLine) ? Visibility.Collapsed : Visibility.Visible,
                ConflictBadgeText = showConflictBadge ? ConflictText.SeverityBadge(sev, lang) : string.Empty,
                ConflictBadgeBrush = BadgeBrushFor(sev),
                ConflictBadgeVisibility = showConflictBadge ? Visibility.Visible : Visibility.Collapsed,
                StateBadgeText = ConflictText.LoadStateLabel(m.LoadState, lang),
                Tone = ConflictText.LoadStateTone(m.LoadState),
                SourceText = ConflictText.SourceLabel(m.Source, m.PdxModId, lang),
                ToolTipText = m.DirectoryPath,
            });
        }

        if (rows.Count == 0)
        {
            ModListBox.ItemsSource = null;
            ModListBox.Visibility = Visibility.Collapsed;
            ModListEmptyText.Text = Loc.Instance["Conflicts.Mods.NoMatch"];
            ModListEmptyText.Visibility = Visibility.Visible;
            return;
        }

        ModListEmptyText.Visibility = Visibility.Collapsed;
        ModListBox.Visibility = Visibility.Visible;
        ModListBox.ItemsSource = rows;
    }

    private static bool IsAttentionModItem(PlaysetModListItem m) =>
        m.LoadState is ModLoadState.PendingCloudDownload
            or ModLoadState.MissingDependencies
            or ModLoadState.ShadowedDuplicate
            or ModLoadState.UnreadableAssembly;

    private static bool MatchesCategoryFilter(PlaysetModListItem item, ModListFilterKind kind) => kind switch
    {
        ModListFilterKind.Enabled => item.IsEnabled,
        ModListFilterKind.Disabled => !item.IsEnabled,
        ModListFilterKind.CodeMods => item.LoadState == ModLoadState.LoadedCodeMod,
        ModListFilterKind.NonCode => item.LoadState == ModLoadState.AssetOrNonCode,
        ModListFilterKind.LocalMods => item.Source == ModInstallSource.UserMods,
        ModListFilterKind.Attention => IsAttentionModItem(item),
        _ => true,
    };

    private static bool MatchesKeyword(PlaysetModListItem item, string keyword)
    {
        if (item.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.PdxModId != null && item.PdxModId.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.Author != null && item.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;
        if (item.WinningAssemblyNames.Any(a => a.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (item.Tags.Any(t => t.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    private static void SetOptionalToneLine(TextBlock target, string? text, PackageTone tone)
    {
        if (string.IsNullOrEmpty(text))
        {
            target.DataContext = null;
            target.Visibility = Visibility.Collapsed;
            return;
        }

        target.DataContext = new ToneLine(text, tone);
        target.Visibility = Visibility.Visible;
    }

    private void ClearModeAndNoticeLines()
    {
        ModeScopeText.Text = string.Empty;
        ModeScopeText.Visibility = Visibility.Collapsed;
        CrossModeText.Text = string.Empty;
        CrossModeText.Visibility = Visibility.Collapsed;
        PreviewNoticeText.Visibility = Visibility.Collapsed;
        OffdiskNoticeText.Visibility = Visibility.Collapsed;
        MapExtMissingText.Visibility = Visibility.Collapsed;
        UnsavedBasisText.Visibility = Visibility.Collapsed;
        PlaysetErrorText.Visibility = Visibility.Collapsed;
    }

    private Border BuildConflictCard(ModConflictItem item, ConflictEvalSettings settings, bool isActiveTarget, string lang)
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
            else if (!isActiveTarget)
                actionBtn.ToolTip = Loc.Instance["Conflicts.Action.DisableGroupsPreviewTip"];
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
