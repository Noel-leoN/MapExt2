using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MapExTools.Core.Config;
using MapExTools.Core.Game;
using MapExTools.Core.Localization;
using MapExTools.Services;

namespace MapExTools;

public partial class MainWindow : Window
{
    /// <summary>游戏进程的检查间隔（计划 2.3：每 1.5 秒）。</summary>
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(1.5);

    private readonly DispatcherTimer _pollTimer;
    private bool? _gameRunning;

    // 填充下拉框时会触发 SelectionChanged，这段时间不当作用户操作
    private bool _populating;

    public MainWindow()
    {
        InitializeComponent();

        PopulateLanguageBox();
        PopulateThemeBox();
        Loc.Instance.LanguageChanged += OnUiLanguageChanged;

        _pollTimer = new DispatcherTimer { Interval = s_pollInterval };
        _pollTimer.Tick += (_, _) => RefreshGameState();
        _pollTimer.Start();
        RefreshGameState();
        RefreshUserDataText();

        Closed += (_, _) =>
        {
            _pollTimer.Stop();
            Loc.Instance.LanguageChanged -= OnUiLanguageChanged;
        };
    }

    private sealed record Choice<T>(T Value, string Display);

    // === 语言与主题 ===

    private void PopulateLanguageBox()
    {
        _populating = true;
        LanguageBox.ItemsSource = UiLanguage.All.Select(l => new Choice<string>(l, UiLanguage.GetNativeName(l))).ToList();
        LanguageBox.SelectedIndex = UiLanguage.All.ToList().IndexOf(Loc.Instance.Language);
        _populating = false;
    }

    private void PopulateThemeBox()
    {
        _populating = true;
        ThemePreference current = App.Services.Config.Theme;
        var items = Enum.GetValues<ThemePreference>()
            .Select(t => new Choice<ThemePreference>(t, Loc.Instance[$"Theme.{t}"]))
            .ToList();
        ThemeBox.ItemsSource = items;
        ThemeBox.SelectedIndex = items.FindIndex(c => c.Value == current);
        _populating = false;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || LanguageBox.SelectedItem is not Choice<string> choice)
            return;

        App.Services.Config.Language = choice.Value;
        App.Services.TrySaveConfig(this);
        Loc.Instance.SetLanguage(choice.Value);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || ThemeBox.SelectedItem is not Choice<ThemePreference> choice)
            return;

        App.Services.Config.Theme = choice.Value;
        App.Services.TrySaveConfig(this);
        ThemeService.Apply(choice.Value);
    }

    private void OnUiLanguageChanged(object? sender, EventArgs e)
    {
        // 代码里拼出的文字不走绑定，手动刷新
        PopulateThemeBox();
        _gameRunning = null;
        RefreshGameState();
        RefreshUserDataText();
    }

    // === 状态栏 ===

    private void RefreshGameState()
    {
        bool running = GameProcess.IsRunning();
        if (running == _gameRunning)
            return;

        _gameRunning = running;
        GameStateText.Text = Loc.Instance[running ? "Status.GameRunning" : "Status.GameNotRunning"];
        GameStateDot.Fill = running ? Brushes.OrangeRed : Brushes.SeaGreen;
    }

    private void RefreshUserDataText()
    {
        UserDataText.Text = Loc.Instance.Format("Status.UserData", App.Services.Paths.Root);
    }
}
