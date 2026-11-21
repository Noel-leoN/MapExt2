using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MapExTools.Core.Coc;
using MapExTools.Core.Packages;
using MapExTools.Services;
using MapExtPDX;

namespace MapExTools.Views;

/// <summary>
/// 模组设置页（Master-Detail 全量配置中心）：涵盖 <see cref="StartupKeys.AllKeys"/> 全部 65 个 OptionUI 持久化配置，
/// 支持左侧 6 类导航、关键字实时检索、分组与单项一键重置、上下文联动提示以及 3×3 RCI 租金公式矩阵。
/// </summary>
public partial class ModSettingsPage : UserControl
{
    private static readonly OptionCategory[] s_categories =
    [
        OptionCategory.Core,
        OptionCategory.Performance,
        OptionCategory.Pathfinding,
        OptionCategory.EcoBehavior,
        OptionCategory.RentControl,
        OptionCategory.Interface,
    ];

    private static readonly OptionGroup[] s_groups =
    [
        OptionGroup.ModeAndConvert,
        OptionGroup.EcoToggles,
        OptionGroup.TerrainWater,
        OptionGroup.EntitiesTraffic,
        OptionGroup.EditorBrush,
        OptionGroup.VehicleRescue,
        OptionGroup.PathfindDaily,
        OptionGroup.PathfindSchool,
        OptionGroup.EcoThroughput,
        OptionGroup.EcoConsumption,
        OptionGroup.EcoCandidates,
        OptionGroup.LandValue,
        OptionGroup.RentMatrix,
        OptionGroup.PanelSize,
        OptionGroup.DashboardSections,
        OptionGroup.DebugExport,
    ];

    // CS2 经典 R/C/I 分区代表色（绿 / 蓝 / 琥珀橙）
    private static readonly SolidColorBrush s_resDotBrush = CreateFrozenBrush(0x3B, 0xA5, 0x5D);
    private static readonly SolidColorBrush s_comDotBrush = CreateFrozenBrush(0x3B, 0x82, 0xF6);
    private static readonly SolidColorBrush s_indDotBrush = CreateFrozenBrush(0xF5, 0x9E, 0x0B);

    private readonly Dictionary<OptionCategory, NavWidgets> _navItems = [];
    private readonly Dictionary<OptionGroup, GroupCardWidgets> _groupCards = [];
    private readonly Dictionary<string, KeyControlBinding> _bindings = new(StringComparer.Ordinal);
    private readonly List<TextBlock> _matrixTexts = [];
    private readonly HashSet<string> _dirtyNumberBoxes = new(StringComparer.Ordinal);

    private OptionCategory _selectedCategory = OptionCategory.Core;
    private bool _populating;
    private bool _updatingNav;

    public ModSettingsPage()
    {
        InitializeComponent();
        BuildNavigation();
        BuildAllCards();
        UpdateTexts();
        SyncFromSession();
        ApplyFilterAndVisibility();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private sealed record EnumOptionItem(string MemberName, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed class NavWidgets
    {
        public required OptionCategory Category { get; init; }
        public required ListBoxItem Container { get; init; }
        public required TextBlock Title { get; init; }
        public required Border BadgeBorder { get; init; }
        public required TextBlock BadgeText { get; init; }
    }

    private sealed class GroupCardWidgets
    {
        public required OptionGroup Group { get; init; }
        public required OptionCategory Category { get; init; }
        public required Border CardBorder { get; init; }
        public required TextBlock TitleText { get; init; }
        public required Border TimingBadge { get; init; }
        public required TextBlock TimingText { get; init; }
        public required Button ResetButton { get; init; }
        public required Border NoteBanner { get; init; }
        public required TextBlock NoteText { get; init; }
        public required List<KeyControlBinding> Items { get; init; }
    }

    private sealed class KeyControlBinding
    {
        public required StartupKeyInfo Key { get; init; }
        public required FrameworkElement RowElement { get; init; }
        public TextBlock? LabelText { get; init; }
        public Button? ResetButton { get; init; }
        public Dictionary<string, RadioButton>? ModeRadios { get; init; }
        public CheckBox? CheckBox { get; init; }
        public ComboBox? ComboBox { get; init; }
        public Slider? Slider { get; init; }
        public TextBox? NumberBox { get; init; }
    }

    private static CocSession Coc => App.Services.Coc;

    private static string CurrentLanguage => Loc.Instance.Language;

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    // === 生命周期 ===

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Coc.Changed += OnSessionChanged;
        UpdateTexts();
        SyncFromSession();
        ApplyFilterAndVisibility();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        Coc.Changed -= OnSessionChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateTexts();
        SyncFromSession();
        ApplyFilterAndVisibility();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        SyncFromSession();
    }

    // === 构建左侧分类导航 ===

    private void BuildNavigation()
    {
        _updatingNav = true;
        try
        {
            foreach (OptionCategory cat in s_categories)
            {
                var title = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                var badgeText = new TextBlock
                {
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var badgeBorder = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(7, 1, 7, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed,
                    Child = badgeText,
                };
                DockPanel.SetDock(badgeBorder, Dock.Right);

                var dock = new DockPanel { LastChildFill = true };
                dock.Children.Add(badgeBorder);
                dock.Children.Add(title);

                var item = new ListBoxItem { Content = dock, Tag = cat };
                CategoryList.Items.Add(item);
                _navItems[cat] = new NavWidgets
                {
                    Category = cat,
                    Container = item,
                    Title = title,
                    BadgeBorder = badgeBorder,
                    BadgeText = badgeText,
                };
            }

            CategoryList.SelectedIndex = 0;
        }
        finally
        {
            _updatingNav = false;
        }
    }

    // === 构建右侧 16 张分组卡片 ===

    private void BuildAllCards()
    {
        foreach (OptionGroup group in s_groups)
        {
            StartupKeyInfo[] keys = StartupKeys.AllKeys.Where(k => k.Group == group).ToArray();
            OptionCategory category = keys[0].Category;

            var titleText = new TextBlock
            {
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var timingText = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var timingBadge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = timingText,
            };
            if (StartupSettingsText.IsRestartGroup(group))
            {
                timingBadge.SetResourceReference(Border.BackgroundProperty, "SystemFillColorCautionBackgroundBrush");
                timingText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
            }
            else
            {
                timingBadge.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
                timingText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            }

            var resetButton = new Button
            {
                Tag = group,
                FontSize = 12,
                Padding = new Thickness(9, 2, 9, 2),
                MinHeight = 24,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            resetButton.Click += OnResetGroupClick;

            var headerRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            headerRight.Children.Add(timingBadge);
            headerRight.Children.Add(resetButton);
            DockPanel.SetDock(headerRight, Dock.Right);

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            header.Children.Add(headerRight);
            header.Children.Add(titleText);

            var noteText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            noteText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            var noteBanner = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed,
                Child = noteText,
            };
            noteBanner.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");

            var cardBody = new StackPanel();
            var groupBindings = new List<KeyControlBinding>();
            BuildGroupBody(group, keys, cardBody, groupBindings);

            var rootStack = new StackPanel();
            rootStack.Children.Add(header);
            rootStack.Children.Add(noteBanner);
            rootStack.Children.Add(cardBody);

            var cardBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15, 11, 15, 11),
                Margin = new Thickness(0, 0, 0, 8),
                Child = rootStack,
            };
            cardBorder.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            cardBorder.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");

            CardsHost.Children.Add(cardBorder);
            _groupCards[group] = new GroupCardWidgets
            {
                Group = group,
                Category = category,
                CardBorder = cardBorder,
                TitleText = titleText,
                TimingBadge = timingBadge,
                TimingText = timingText,
                ResetButton = resetButton,
                NoteBanner = noteBanner,
                NoteText = noteText,
                Items = groupBindings,
            };
        }
    }

    private void BuildGroupBody(OptionGroup group, StartupKeyInfo[] keys, StackPanel host, List<KeyControlBinding> groupBindings)
    {
        switch (group)
        {
            case OptionGroup.ModeAndConvert:
                BuildModeAndConvertBody(keys, host, groupBindings);
                break;
            case OptionGroup.EcoToggles:
                BuildEcoTogglesBody(keys, host, groupBindings);
                break;
            case OptionGroup.DashboardSections:
                BuildDashboardSectionsBody(keys, host, groupBindings);
                break;
            case OptionGroup.RentMatrix:
                BuildRentMatrixBody(keys, host, groupBindings);
                break;
            default:
                foreach (StartupKeyInfo key in keys)
                {
                    KeyControlBinding binding = CreateStandardRow(key);
                    host.Children.Add(binding.RowElement);
                    groupBindings.Add(binding);
                    _bindings[key.Name] = binding;
                }
                break;
        }
    }

    private void BuildModeAndConvertBody(StartupKeyInfo[] keys, StackPanel host, List<KeyControlBinding> groupBindings)
    {
        StartupKeyInfo modeKey = keys.First(k => k.Kind == StartupKeyKind.Mode);

        var modeRowStack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        var topLine = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        Button resetBtn = CreateItemResetButton(modeKey);
        DockPanel.SetDock(resetBtn, Dock.Right);
        var modeLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        topLine.Children.Add(resetBtn);
        topLine.Children.Add(modeLabel);
        modeRowStack.Children.Add(topLine);

        var radiosWrap = new WrapPanel();
        var radios = new Dictionary<string, RadioButton>(StringComparer.Ordinal);
        foreach (string mode in StartupKeys.ModeNames)
        {
            var rb = new RadioButton
            {
                GroupName = "OptionsPatchMode",
                Tag = mode,
                Margin = new Thickness(0, 1, 20, 2),
            };
            rb.Checked += OnModeRadioChecked;
            radios[mode] = rb;
            radiosWrap.Children.Add(rb);
        }
        modeRowStack.Children.Add(radiosWrap);

        var divider = CreateDivider();
        modeRowStack.Children.Add(divider);
        host.Children.Add(modeRowStack);

        var modeBinding = new KeyControlBinding
        {
            Key = modeKey,
            RowElement = modeRowStack,
            LabelText = modeLabel,
            ResetButton = resetBtn,
            ModeRadios = radios,
        };
        groupBindings.Add(modeBinding);
        _bindings[modeKey.Name] = modeBinding;

        // 其余 3 项布尔开关（地图扩展转换、停用背景世界地图、禁止读档校验）
        string[] order = [SaveGateRules.ConversionKey, SaveGateRules.BackdropKey, SaveGateRules.ValidationKey];
        foreach (string name in order)
        {
            StartupKeyInfo key = keys.First(k => k.Name == name);
            KeyControlBinding binding = CreateStandardRow(key);
            host.Children.Add(binding.RowElement);
            groupBindings.Add(binding);
            _bindings[key.Name] = binding;
        }
    }

    private void BuildEcoTogglesBody(StartupKeyInfo[] keys, StackPanel host, List<KeyControlBinding> groupBindings)
    {
        StartupKeyInfo master = keys.First(k => k.Name == StartupSettingsText.EconomyMasterKey);
        KeyControlBinding masterBinding = CreateStandardRow(master);
        host.Children.Add(masterBinding.RowElement);
        host.Children.Add(CreateDivider());
        groupBindings.Add(masterBinding);
        _bindings[master.Name] = masterBinding;

        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (StartupKeyInfo sub in keys.Where(k => k.EcoGroup))
        {
            var cellDock = new DockPanel { Margin = new Thickness(4, 2, 12, 2) };
            Button resetBtn = CreateItemResetButton(sub);
            DockPanel.SetDock(resetBtn, Dock.Right);

            var cb = new CheckBox { Tag = sub.Name, VerticalAlignment = VerticalAlignment.Center };
            cb.Checked += OnBoolCheckChanged;
            cb.Unchecked += OnBoolCheckChanged;

            cellDock.Children.Add(resetBtn);
            cellDock.Children.Add(cb);
            grid.Children.Add(cellDock);

            var binding = new KeyControlBinding
            {
                Key = sub,
                RowElement = cellDock,
                CheckBox = cb,
                ResetButton = resetBtn,
            };
            groupBindings.Add(binding);
            _bindings[sub.Name] = binding;
        }

        host.Children.Add(grid);
    }

    private void BuildDashboardSectionsBody(StartupKeyInfo[] keys, StackPanel host, List<KeyControlBinding> groupBindings)
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
        foreach (StartupKeyInfo key in keys)
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 18, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var cb = new CheckBox { Tag = key.Name, VerticalAlignment = VerticalAlignment.Center };
            cb.Checked += OnBoolCheckChanged;
            cb.Unchecked += OnBoolCheckChanged;

            Button resetBtn = CreateItemResetButton(key);
            resetBtn.Margin = new Thickness(2, 0, 0, 0);

            itemPanel.Children.Add(cb);
            itemPanel.Children.Add(resetBtn);
            wrap.Children.Add(itemPanel);

            var binding = new KeyControlBinding
            {
                Key = key,
                RowElement = itemPanel,
                CheckBox = cb,
                ResetButton = resetBtn,
            };
            groupBindings.Add(binding);
            _bindings[key.Name] = binding;
        }

        host.Children.Add(wrap);
    }

    private void BuildRentMatrixBody(StartupKeyInfo[] keys, StackPanel host, List<KeyControlBinding> groupBindings)
    {
        _matrixTexts.Clear();
        var matrix = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 122 });
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int r = 0; r < 4; r++)
            matrix.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 表头
        AddMatrixHeader(matrix, 0, "Options.Matrix.Metric", null);
        AddMatrixHeader(matrix, 1, "Options.Matrix.Res", s_resDotBrush);
        AddMatrixHeader(matrix, 2, "Options.Matrix.Com", s_comDotBrush);
        AddMatrixHeader(matrix, 3, "Options.Matrix.Ind", s_indDotBrush);

        (string RowKey, string[] Keys)[] rows =
        [
            ("Options.Matrix.RowRent", ["RentMultiplierResidential", "RentMultiplierCommercial", "RentMultiplierIndustrial"]),
            ("Options.Matrix.RowLandValue", ["LandValueFactorResidential", "LandValueFactorCommercial", "LandValueFactorIndustrial"]),
            ("Options.Matrix.RowLevel", ["LevelFactorResidential", "LevelFactorCommercial", "LevelFactorIndustrial"]),
        ];

        for (int r = 0; r < rows.Length; r++)
        {
            var rowLabel = new TextBlock
            {
                Tag = rows[r].RowKey,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 10, 4),
            };
            Grid.SetRow(rowLabel, r + 1);
            Grid.SetColumn(rowLabel, 0);
            matrix.Children.Add(rowLabel);
            _matrixTexts.Add(rowLabel);

            for (int c = 0; c < 3; c++)
            {
                StartupKeyInfo key = keys.First(k => k.Name == rows[r].Keys[c]);
                KeyControlBinding cellBinding = CreateMatrixCell(key);
                Grid.SetRow(cellBinding.RowElement, r + 1);
                Grid.SetColumn(cellBinding.RowElement, c + 1);
                matrix.Children.Add(cellBinding.RowElement);
                groupBindings.Add(cellBinding);
                _bindings[key.Name] = cellBinding;
            }
        }

        host.Children.Add(matrix);
    }

    private void AddMatrixHeader(Grid matrix, int column, string locKey, Brush? dotBrush)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(column == 0 ? 0 : 6, 0, 4, 6),
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (dotBrush != null)
        {
            panel.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = dotBrush,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var text = new TextBlock
        {
            Tag = locKey,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        panel.Children.Add(text);
        _matrixTexts.Add(text);

        Grid.SetRow(panel, 0);
        Grid.SetColumn(panel, column);
        matrix.Children.Add(panel);
    }

    private KeyControlBinding CreateMatrixCell(StartupKeyInfo key)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

        // 第一行：滑块独占整行，避免窄窗下被数字框遮挡
        var slider = CreateSlider(key);
        slider.Margin = new Thickness(0, 0, 0, 3);
        Grid.SetRow(slider, 0);
        Grid.SetColumnSpan(slider, 4);

        // 第二行：数字框 + % 单位 + 右侧重置按钮
        var box = CreateNumberBox(key, width: 54);
        Grid.SetRow(box, 1);
        Grid.SetColumn(box, 0);

        var unit = new TextBlock
        {
            Text = "%",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 2, 0),
        };
        unit.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        Grid.SetRow(unit, 1);
        Grid.SetColumn(unit, 1);

        Button reset = CreateItemResetButton(key);
        Grid.SetRow(reset, 1);
        Grid.SetColumn(reset, 3);

        grid.Children.Add(slider);
        grid.Children.Add(box);
        grid.Children.Add(unit);
        grid.Children.Add(reset);

        var cellBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 5, 6, 5),
            Margin = new Thickness(5, 3, 0, 3),
            Child = grid,
        };
        cellBorder.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorTertiaryBrush");

        return new KeyControlBinding
        {
            Key = key,
            RowElement = cellBorder,
            Slider = slider,
            NumberBox = box,
            ResetButton = reset,
        };
    }

    private KeyControlBinding CreateStandardRow(StartupKeyInfo key)
    {
        var rowGrid = new Grid { MinHeight = 30, Margin = new Thickness(0, 2, 0, 2) };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        var label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 14, 0),
        };
        Grid.SetColumn(label, 0);
        rowGrid.Children.Add(label);

        Button resetBtn = CreateItemResetButton(key);
        Grid.SetColumn(resetBtn, 2);
        rowGrid.Children.Add(resetBtn);

        CheckBox? checkBox = null;
        ComboBox? comboBox = null;
        Slider? slider = null;
        TextBox? numberBox = null;

        switch (key.Kind)
        {
            case StartupKeyKind.Bool:
            {
                checkBox = new CheckBox
                {
                    Tag = key.Name,
                    MinWidth = 28,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                checkBox.Checked += OnBoolCheckChanged;
                checkBox.Unchecked += OnBoolCheckChanged;
                Grid.SetColumn(checkBox, 1);
                rowGrid.Children.Add(checkBox);
                break;
            }
            case StartupKeyKind.Enum:
            {
                comboBox = new ComboBox
                {
                    Tag = key.Name,
                    Width = 236,
                    MinHeight = 28,
                    DisplayMemberPath = nameof(EnumOptionItem.Display),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                comboBox.SelectionChanged += OnEnumSelectionChanged;
                Grid.SetColumn(comboBox, 1);
                rowGrid.Children.Add(comboBox);
                break;
            }
            case StartupKeyKind.Int:
            case StartupKeyKind.Float:
            {
                var rightPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                slider = CreateSlider(key);
                slider.Width = 176;
                slider.Margin = new Thickness(0, 0, 8, 0);

                numberBox = CreateNumberBox(key, width: 66);
                rightPanel.Children.Add(slider);
                rightPanel.Children.Add(numberBox);

                if (key.Unit.Length > 0)
                {
                    var unitText = new TextBlock
                    {
                        Text = key.Unit,
                        Width = 48,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0),
                    };
                    unitText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                    rightPanel.Children.Add(unitText);
                }
                else
                {
                    numberBox.Margin = new Thickness(0, 0, 53, 0);
                }

                Grid.SetColumn(rightPanel, 1);
                rowGrid.Children.Add(rightPanel);
                break;
            }
        }

        return new KeyControlBinding
        {
            Key = key,
            RowElement = rowGrid,
            LabelText = label,
            ResetButton = resetBtn,
            CheckBox = checkBox,
            ComboBox = comboBox,
            Slider = slider,
            NumberBox = numberBox,
        };
    }

    private Slider CreateSlider(StartupKeyInfo key)
    {
        double min = Math.Round(key.Min * key.DisplayScale, 4);
        double max = Math.Round(key.Max * key.DisplayScale, 4);
        double step = Math.Round(key.Step * key.DisplayScale, 4);

        var slider = new Slider
        {
            Tag = key.Name,
            Minimum = min,
            Maximum = max,
            TickFrequency = step,
            SmallChange = step,
            LargeChange = step * 5,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.ValueChanged += OnSliderValueChanged;
        return slider;
    }

    private TextBox CreateNumberBox(StartupKeyInfo key, double width)
    {
        var box = new TextBox
        {
            Tag = key.Name,
            Width = width,
            MinHeight = 26,
            Padding = new Thickness(6, 2, 6, 2),
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        box.TextChanged += OnNumberBoxTextChanged;
        box.LostKeyboardFocus += OnNumberBoxLostFocus;
        box.KeyDown += OnNumberBoxKeyDown;
        return box;
    }

    private Button CreateItemResetButton(StartupKeyInfo key)
    {
        var btn = new Button
        {
            Tag = key.Name,
            Content = "↺",
            Width = 24,
            Height = 24,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Hidden,
        };
        btn.Click += OnResetItemClick;
        return btn;
    }

    private static Border CreateDivider()
    {
        var line = new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6) };
        line.SetResourceReference(Border.BackgroundProperty, "CardStrokeColorDefaultBrush");
        return line;
    }

    // === 多语言文案刷新 ===

    private void UpdateTexts()
    {
        string lang = CurrentLanguage;
        ResetAllButton.ToolTip = Loc.Instance.Format("Options.ResetAllTip", StartupKeys.AllKeys.Length);

        foreach ((OptionCategory cat, NavWidgets nav) in _navItems)
        {
            string label = StartupSettingsText.CategoryLabel(cat, lang);
            nav.Title.Text = label;
            AutomationProperties.SetName(nav.Container, label);
        }

        foreach ((OptionGroup group, GroupCardWidgets card) in _groupCards)
        {
            card.TitleText.Text = StartupSettingsText.GroupLabel(group, lang);
            card.TimingText.Text = StartupSettingsText.GroupTiming(group, lang);
            card.ResetButton.Content = Loc.Instance["Options.ResetGroup"];
            card.ResetButton.ToolTip = Loc.Instance["Options.ResetGroupTip"];
        }

        foreach (TextBlock text in _matrixTexts)
        {
            if (text.Tag is string locKey)
                text.Text = Loc.Instance[locKey];
        }

        _populating = true;
        try
        {
            foreach (KeyControlBinding b in _bindings.Values)
            {
                StartupKeyInfo key = b.Key;
                string label = StartupSettingsText.KeyLabel(key, lang);
                string defaultText = StartupSettingsText.Value(key, key.DefaultValue, lang);
                string tip = $"{StartupSettingsText.KeyTip(key, lang)}\n[{key.Name} · {Loc.Instance["Startup.Defaults"]}: {defaultText}]";

                b.RowElement.ToolTip = tip;
                if (b.LabelText != null)
                    b.LabelText.Text = label;
                if (b.ResetButton != null)
                {
                    string resetTip = Loc.Instance.Format("Options.ResetItemTip", defaultText);
                    b.ResetButton.ToolTip = resetTip;
                    AutomationProperties.SetName(b.ResetButton, $"{label} - {resetTip}");
                }

                if (b.ModeRadios != null)
                {
                    foreach ((string mode, RadioButton rb) in b.ModeRadios)
                        rb.Content = StartupSettingsText.ModeChoice(mode, lang);
                }

                if (b.CheckBox != null)
                {
                    // EcoToggles 与 DashboardSections 的复选框直接显示标题；标准行复选框在右侧无文字
                    if (b.LabelText == null)
                        b.CheckBox.Content = label;
                    AutomationProperties.SetName(b.CheckBox, label);
                }

                if (b.ComboBox != null && key.SelectableEnumNames != null)
                {
                    string current = Coc.GetString(key.Name);
                    var items = key.SelectableEnumNames
                        .Select(m => new EnumOptionItem(m, StartupSettingsText.EnumChoice(key, m, lang)))
                        .ToList();
                    b.ComboBox.ItemsSource = items;
                    b.ComboBox.SelectedIndex = Math.Max(0, items.FindIndex(x => x.MemberName == current));
                    AutomationProperties.SetName(b.ComboBox, label);
                }

                if (b.Slider != null)
                    AutomationProperties.SetName(b.Slider, label);
                if (b.NumberBox != null)
                    AutomationProperties.SetName(b.NumberBox, label);
            }
        }
        finally
        {
            _populating = false;
        }
    }

    // === 同步状态与控件显示 ===

    private void SyncFromSession()
    {
        string lang = CurrentLanguage;
        bool usable = Coc.IsUsable;
        bool running = Coc.GameRunning;

        _populating = true;
        try
        {
            foreach (KeyControlBinding b in _bindings.Values)
            {
                StartupKeyInfo key = b.Key;
                object val = Coc.GetValue(key.Name);
                bool enabled = Coc.IsKeyEnabled(key);
                bool isDefault = Coc.IsDefaultValue(key);

                if (b.LabelText != null)
                {
                    b.LabelText.FontWeight = isDefault ? FontWeights.Normal : FontWeights.SemiBold;
                    b.LabelText.SetResourceReference(TextBlock.ForegroundProperty,
                        isDefault ? "TextFillColorPrimaryBrush" : "AccentTextFillColorPrimaryBrush");
                }

                if (b.ResetButton != null)
                {
                    b.ResetButton.Visibility = isDefault ? Visibility.Hidden : Visibility.Visible;
                    b.ResetButton.IsEnabled = usable;
                }

                if (b.ModeRadios != null && val is string modeVal)
                {
                    foreach ((string mode, RadioButton rb) in b.ModeRadios)
                    {
                        rb.IsChecked = mode == modeVal;
                        rb.IsEnabled = enabled;
                    }
                }

                if (b.CheckBox != null && val is bool boolVal)
                {
                    b.CheckBox.IsChecked = boolVal;
                    b.CheckBox.IsEnabled = enabled;
                    if (b.LabelText == null)
                        b.CheckBox.FontWeight = isDefault ? FontWeights.Normal : FontWeights.SemiBold;
                }

                if (b.ComboBox != null && val is string enumVal)
                {
                    b.ComboBox.IsEnabled = enabled;
                    if (b.ComboBox.ItemsSource is IReadOnlyList<EnumOptionItem> items)
                    {
                        int idx = -1;
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (items[i].MemberName == enumVal)
                            {
                                idx = i;
                                break;
                            }
                        }

                        if (idx >= 0 && b.ComboBox.SelectedIndex != idx)
                            b.ComboBox.SelectedIndex = idx;
                    }
                }

                if (b.Slider != null)
                {
                    b.Slider.IsEnabled = enabled;
                    double scaled = key.Kind == StartupKeyKind.Int
                        ? (int)val
                        : Math.Round((float)val * key.DisplayScale, 4);
                    if (Math.Abs(b.Slider.Value - scaled) > 1e-6)
                        b.Slider.Value = scaled;
                }

                if (b.NumberBox != null)
                {
                    b.NumberBox.IsEnabled = enabled;
                    b.NumberBox.FontWeight = isDefault ? FontWeights.Normal : FontWeights.SemiBold;
                    if (!b.NumberBox.IsKeyboardFocusWithin)
                    {
                        b.NumberBox.Text = StartupSettingsText.FormatInputNumber(key, val);
                        _dirtyNumberBoxes.Remove(key.Name);
                    }
                }
            }
        }
        finally
        {
            _populating = false;
        }

        // 更新分组卡片的"重置本组"按钮与上下文智能提示横幅
        foreach ((OptionGroup group, GroupCardWidgets card) in _groupCards)
        {
            bool anyNonDefault = card.Items.Any(i => !Coc.IsDefaultValue(i.Key));
            card.ResetButton.IsEnabled = usable && anyNonDefault;

            (string? note, bool caution) = GetGroupContextNote(group);
            if (note == null)
            {
                card.NoteBanner.Visibility = Visibility.Collapsed;
            }
            else
            {
                card.NoteText.Text = note;
                card.NoteBanner.SetResourceReference(Border.BackgroundProperty,
                    caution ? "SystemFillColorCautionBackgroundBrush" : "SubtleFillColorSecondaryBrush");
                card.NoteText.SetResourceReference(TextBlock.ForegroundProperty,
                    caution ? "SystemFillColorCautionBrush" : "TextFillColorSecondaryBrush");
                card.NoteBanner.Visibility = Visibility.Visible;
            }
        }

        // 更新左侧分类徽章（优先显示未保存改动数 ● N，否则显示非默认项数 N）
        IReadOnlyList<StartupSettingChange> pending = usable ? Coc.GetPending() : [];
        var pendingByCat = pending.GroupBy(p => p.Key.Category).ToDictionary(g => g.Key, g => g.Count());
        foreach ((OptionCategory cat, NavWidgets nav) in _navItems)
        {
            int catPending = pendingByCat.GetValueOrDefault(cat, 0);
            int nonDefault = StartupKeys.AllKeys.Count(k => k.Category == cat && !Coc.IsDefaultValue(k));
            if (catPending > 0)
            {
                nav.BadgeText.Text = $"● {catPending}";
                nav.BadgeBorder.SetResourceReference(Border.BackgroundProperty, "SystemFillColorCautionBackgroundBrush");
                nav.BadgeText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
                nav.BadgeBorder.Visibility = Visibility.Visible;
            }
            else if (nonDefault > 0)
            {
                nav.BadgeText.Text = nonDefault.ToString(CultureInfo.InvariantCulture);
                nav.BadgeBorder.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
                nav.BadgeText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                nav.BadgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                nav.BadgeBorder.Visibility = Visibility.Collapsed;
            }
        }

        // 更新顶栏状态条与动作按钮
        List<ToneLine> statusLines = Coc.BuildStatusLines(lang);
        ToneLine primary = statusLines.Count > 0 ? statusLines[0] : new ToneLine(Loc.Instance["Startup.Ready"], PackageTone.Good);
        PrimaryStatusText.DataContext = primary;
        PrimaryStatusText.ToolTip = string.Join("\n", statusLines.Select(l => l.Text));

        if (!usable)
        {
            PendingStatusText.DataContext = new ToneLine(Loc.Instance.Format("Startup.File", Coc.CocPath), PackageTone.Muted);
            PendingStatusText.ToolTip = Coc.CocPath;
        }
        else if (pending.Count > 0)
        {
            PendingStatusText.DataContext = new ToneLine(
                Loc.Instance.Format("Startup.Pending", StartupSettingsText.ChangeListCompact(pending, lang)),
                PackageTone.Warning);
            PendingStatusText.ToolTip = Loc.Instance.Format("Startup.Pending", StartupSettingsText.ChangeList(pending, lang));
        }
        else
        {
            PendingStatusText.DataContext = new ToneLine(Loc.Instance["Startup.PendingNone"], PackageTone.Muted);
            PendingStatusText.ToolTip = Loc.Instance.Format("Startup.File", Coc.CocPath);
        }

        SaveButton.IsEnabled = usable && !running && (pending.Count > 0 || _dirtyNumberBoxes.Count > 0);
        ResetAllButton.IsEnabled = usable && StartupKeys.AllKeys.Any(k => !Coc.IsDefaultValue(k));

        string cocPath = Coc.CocPath;
        bool exists = File.Exists(cocPath);
        EditLink.IsEnabled = exists;
        ShowFileLink.IsEnabled = exists || Directory.Exists(Path.GetDirectoryName(cocPath));
    }

    private (string? Text, bool Caution) GetGroupContextNote(OptionGroup group)
    {
        bool ecoMaster = Coc.GetBool(StartupSettingsText.EconomyMasterKey);
        bool isVanillaMode = Coc.GetString(StartupKeys.ModeKey) == "None";
        return group switch
        {
            OptionGroup.ModeAndConvert when Coc.GetBool(SaveGateRules.ValidationKey) =>
                (Loc.Instance["Startup.ValidationOff"] + Environment.NewLine + Loc.Instance["Startup.MutualExclusion"], true),
            OptionGroup.ModeAndConvert =>
                (Loc.Instance["Startup.MutualExclusion"], false),
            OptionGroup.EcoToggles when !ecoMaster =>
                (Loc.Instance["Startup.EconomyOff"], false),
            OptionGroup.TerrainWater when isVanillaMode =>
                (Loc.Instance["Options.Note.VanillaTerrain"], true),
            OptionGroup.EntitiesTraffic =>
                (Loc.Instance["Options.Note.NoDogsPurgeInGame"], false),
            OptionGroup.PathfindDaily or OptionGroup.EcoThroughput or OptionGroup.EcoConsumption or OptionGroup.EcoCandidates
                when !ecoMaster =>
                (Loc.Instance["Options.Note.EcoMasterOff"], false),
            OptionGroup.PathfindSchool when !ecoMaster =>
                (Loc.Instance["Options.Note.EcoMasterOff"], false),
            OptionGroup.PathfindSchool when !Coc.GetBool("EnableDownstreamAIEcoSystem") =>
                (Loc.Instance["Options.Note.SchoolSubgroupOff"], false),
            OptionGroup.RentMatrix when isVanillaMode && !ecoMaster =>
                (Loc.Instance["Options.Note.EcoMasterOff"], false),
            OptionGroup.RentMatrix when isVanillaMode && !Coc.GetBool("EnableHouseholdPropertyEcoSystem") =>
                (Loc.Instance["Options.Note.RentMatrixVanillaOff"], false),
            _ => (null, false),
        };
    }

    // === 分类切换与搜索过滤 ===

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingNav || CategoryList.SelectedItem is not ListBoxItem { Tag: OptionCategory cat })
            return;

        _selectedCategory = cat;
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // 在搜索状态下点击左侧分类：清空搜索词并直接切入所点分类
            SearchBox.Text = string.Empty;
        }
        else
        {
            ApplyFilterAndVisibility();
            CardsScroll.ScrollToTop();
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        bool hasText = !string.IsNullOrEmpty(SearchBox.Text);
        SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilterAndVisibility();
        CardsScroll.ScrollToTop();
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SearchBox.Focus();
    }

    private void ApplyFilterAndVisibility()
    {
        string query = SearchBox.Text.Trim();
        string lang = CurrentLanguage;
        bool searching = query.Length > 0;
        int visibleCards = 0;

        foreach ((OptionGroup group, GroupCardWidgets card) in _groupCards)
        {
            if (!searching)
            {
                bool showCard = card.Category == _selectedCategory;
                card.CardBorder.Visibility = showCard ? Visibility.Visible : Visibility.Collapsed;
                if (showCard)
                {
                    visibleCards++;
                    foreach (KeyControlBinding item in card.Items)
                        item.RowElement.Visibility = Visibility.Visible;
                }
                continue;
            }

            bool groupTitleHit = StartupSettingsText.GroupLabel(group, lang).Contains(query, StringComparison.OrdinalIgnoreCase);
            bool anyItemHit = false;
            foreach (KeyControlBinding item in card.Items)
            {
                bool match = groupTitleHit
                             || item.Key.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                             || StartupSettingsText.KeyLabel(item.Key, lang).Contains(query, StringComparison.OrdinalIgnoreCase);

                // 3×3 矩阵保持整张表对齐展示，不隐藏单个格子
                if (group != OptionGroup.RentMatrix)
                    item.RowElement.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                if (match)
                    anyItemHit = true;
            }

            if (group == OptionGroup.RentMatrix && anyItemHit)
            {
                foreach (KeyControlBinding item in card.Items)
                    item.RowElement.Visibility = Visibility.Visible;
            }

            card.CardBorder.Visibility = anyItemHit ? Visibility.Visible : Visibility.Collapsed;
            if (anyItemHit)
                visibleCards++;
        }

        NoMatchText.Text = searching ? Loc.Instance.Format("Options.NoMatch", query) : string.Empty;
        NoMatchText.Visibility = searching && visibleCards == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // === 控件交互回调 ===

    private void OnModeRadioChecked(object sender, RoutedEventArgs e)
    {
        if (_populating || sender is not RadioButton { IsChecked: true, Tag: string mode })
            return;

        Coc.SetValue(StartupKeys.ModeKey, mode);
    }

    private void OnBoolCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_populating || sender is not CheckBox { Tag: string keyName } cb)
            return;

        Coc.SetValue(keyName, cb.IsChecked == true);
    }

    private void OnEnumSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || sender is not ComboBox { Tag: string keyName, SelectedItem: EnumOptionItem item })
            return;

        Coc.SetValue(keyName, item.MemberName);
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_populating || sender is not Slider { Tag: string keyName })
            return;

        StartupKeyInfo key = StartupKeys.FindKey(keyName)!;
        object newValue = key.Kind == StartupKeyKind.Int
            ? Math.Clamp((int)Math.Round(e.NewValue), (int)Math.Round(key.Min), (int)Math.Round(key.Max))
            : Math.Clamp((float)Math.Round(e.NewValue / key.DisplayScale, 6), (float)key.Min, (float)key.Max);

        if (_bindings.TryGetValue(keyName, out KeyControlBinding? binding)
            && binding.NumberBox is { IsKeyboardFocusWithin: false } box)
        {
            _populating = true;
            try
            {
                box.Text = StartupSettingsText.FormatInputNumber(key, newValue);
            }
            finally
            {
                _populating = false;
            }
        }

        Coc.SetValue(keyName, newValue);
    }

    /// <summary>提交当前尚未失焦的数字框输入（用于保存与关窗前确认）。</summary>
    internal void CommitPendingNumberBoxes()
    {
        if (_dirtyNumberBoxes.Count == 0)
            return;

        foreach (string keyName in _dirtyNumberBoxes.ToArray())
        {
            if (_bindings.TryGetValue(keyName, out KeyControlBinding? binding) && binding.NumberBox != null)
                CommitNumberBox(binding.NumberBox);
        }

        _dirtyNumberBoxes.Clear();
    }

    private void OnNumberBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_populating || sender is not TextBox { Tag: string keyName } box)
            return;

        if (StartupKeys.FindKey(keyName) is not { } key)
            return;

        string current = StartupSettingsText.FormatInputNumber(key, Coc.GetValue(keyName));
        if (!string.Equals(box.Text.Trim(), current, StringComparison.Ordinal))
            _dirtyNumberBoxes.Add(keyName);
        else
            _dirtyNumberBoxes.Remove(keyName);

        SaveButton.IsEnabled = Coc.IsUsable && !Coc.GameRunning && (Coc.HasUnsavedChanges || _dirtyNumberBoxes.Count > 0);
    }

    private void OnNumberBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box)
            return;

        CommitNumberBox(box);
        box.SelectAll();
        e.Handled = true;
    }

    private void OnNumberBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            CommitNumberBox(box);
    }

    private void CommitNumberBox(TextBox box)
    {
        if (_populating || box.Tag is not string keyName)
            return;

        _dirtyNumberBoxes.Remove(keyName);

        StartupKeyInfo key = StartupKeys.FindKey(keyName)!;
        string text = box.Text.Trim();
        if (key.Kind == StartupKeyKind.Int)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv) && double.IsFinite(dv))
            {
                int clamped = Math.Clamp((int)Math.Round(dv), (int)Math.Round(key.Min), (int)Math.Round(key.Max));
                Coc.SetValue(keyName, clamped);
            }
        }
        else if (key.Kind == StartupKeyKind.Float)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv) && double.IsFinite(dv))
            {
                // 兼容在 ShoppingTrafficReduction（×0.0001）框里直接输入 0.0003 或输入 3 两种习惯
                double rawVal = key.DisplayScale > 1d && Math.Abs(dv) > 0d && Math.Abs(dv) < 0.05d
                    ? dv
                    : dv / key.DisplayScale;
                float clamped = Math.Clamp((float)Math.Round(rawVal, 6), (float)key.Min, (float)key.Max);
                Coc.SetValue(keyName, clamped);
            }
        }

        _populating = true;
        try
        {
            box.Text = StartupSettingsText.FormatInputNumber(key, Coc.GetValue(keyName));
        }
        finally
        {
            _populating = false;
        }

        SaveButton.IsEnabled = Coc.IsUsable && !Coc.GameRunning && (Coc.HasUnsavedChanges || _dirtyNumberBoxes.Count > 0);
    }

    private void OnResetItemClick(object sender, RoutedEventArgs e)
    {
        if (Coc.IsUsable && sender is Button { Tag: string keyName } && StartupKeys.FindKey(keyName) is { } key)
        {
            _dirtyNumberBoxes.Remove(keyName);
            Coc.ResetKey(key);
        }
    }

    private void OnResetGroupClick(object sender, RoutedEventArgs e)
    {
        if (Coc.IsUsable && sender is Button { Tag: OptionGroup group })
        {
            _dirtyNumberBoxes.Clear();
            Coc.ResetGroup(group);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        CommitPendingNumberBoxes();
        Coc.Save(Window.GetWindow(this));
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        _dirtyNumberBoxes.Clear();
        Keyboard.ClearFocus();
        Coc.ReloadManual();
    }

    private void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        if (Coc.IsUsable)
        {
            _dirtyNumberBoxes.Clear();
            Keyboard.ClearFocus();
            Coc.ResetAllDefaults();
        }
    }

    private void OnEditClick(object sender, RoutedEventArgs e) => Coc.OpenInNotepad(Window.GetWindow(this));

    private void OnShowFileClick(object sender, RoutedEventArgs e) => Coc.ShowInExplorer(Window.GetWindow(this));
}
