using System.Collections.ObjectModel;
using CuteSpace.Models;
using CuteSpace.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinRT;
using Microsoft.UI.Composition;

namespace CuteSpace;

public sealed partial class MainWindow : Window
{
    private const int CollapsedWidth = 92;
    private const int CollapsedHeight = 92;
    private const int DefaultExpandedWidth = 560;
    private const int DefaultExpandedHeight = 720;
    private const int MinExpandedWidth = 460;
    private const int MinExpandedHeight = 560;
    private const int MaxExpandedWidth = 860;
    private const int MaxExpandedHeight = 940;

    private readonly JsonDataStore _store = new();
    private readonly LaunchService _launcher = new();
    private readonly AppDiscoveryService _discovery = new();
    private readonly AutostartService _autostart = new();
    private readonly CuteSoundService _sounds = new();
    private readonly LocalizationService _localizer = new();
    private readonly NativeTrayIconService _tray = new();

    private readonly ObservableCollection<ModeProfile> _modes = [];
    private readonly ObservableCollection<LaunchItem> _selectedModeItems = [];
    private readonly ObservableCollection<LaunchItem> _shortcuts = [];
    private readonly ObservableCollection<ClipboardEntry> _clipboard = [];

    private AppState _state = new();
    private ClipboardHistoryService? _clipboardService;
    private AppWindow? _appWindow;
    private nint _hwnd;
    private bool _isExpanded;
    private bool _isDragging;
    private bool _dragMoved;
    private bool _isResizing;
    private bool _updatingStartupToggle;
    private Windows.Foundation.Point _dragStartPointer;
    private PointInt32 _dragStartCursor;
    private PointInt32 _dragStartWindow;
    private Windows.Foundation.Point _resizeStartPointer;
    private SizeInt32 _resizeStartSize;
    private object? _lastClickedModeItem;
    private object? _lastClickedShortcutItem;
    private PointInt32? _bubbleAnchorBeforeExpand;
    private DispatcherTimer? _topMostTimer;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointInt32 lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hWnd, ref Margins pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    private const int WmNclButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out WindowRect rect);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("gdi32.dll")]
    private static extern nint CreateEllipticRgn(int x1, int y1, int x2, int y2);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hWnd, nint hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);


    private sealed record VisualStyleOption(
        string Key,
        string Brand,
        string ShortBrand,
        string Font,
        string Surface,
        string Ink,
        string Muted,
        string Accent,
        string AccentSoft,
        string SecondarySoft,
        string BubbleOuter,
        string BubbleInner,
        string BubbleAccent,
        string BubbleStroke,
        string Eye,
        string Nose);

    private readonly VisualStyleOption[] _visualStyles =
    [
        new("cute", "CuteSpace", "Cute", "Comic Sans MS, Segoe UI Variable Text, Segoe UI", "#FFF5F9", "#2B1B3A", "#7A6B86", "#D1488A", "#FFD9EC", "#F0E6FF", "#FFB8D8", "#FFF5F0F8", "#FF8FBF", "#FF7AB3", "#3D2D4A", "#8A5A72"),
        new("forge", "ForgeSpace", "Forge", "Segoe UI Variable Display, Segoe UI", "#F2F5FA", "#0D1522", "#4A5568", "#2563EB", "#DBEAFE", "#E0F2FE", "#93C5FD", "#F8FAFC", "#6096E8", "#3B82F6", "#1E293B", "#334E7B"),
        new("flow", "FlowSpace", "Flow", "Segoe UI Variable Text, Segoe UI", "#F0F7F5", "#1A2626", "#4F6562", "#0D7377", "#CCFBF1", "#E6FEFA", "#A7E0D8", "#F7FDFB", "#6DBAB0", "#14948A", "#1C3432", "#4A706A")
    ];

    private readonly IconOption[] _icons =
    [
        new() { Name = "bear", Glyph = "🧸" },
        new() { Name = "star", Glyph = "⭐" },
        new() { Name = "heart", Glyph = "💗" },
        new() { Name = "work", Glyph = "💼" },
        new() { Name = "game", Glyph = "🎮" },
        new() { Name = "music", Glyph = "🎵" },
        new() { Name = "Web", Glyph = "🌐" },
        new() { Name = "folder", Glyph = "📁" },
        new() { Name = "file", Glyph = "📄" },
        new() { Name = "tool", Glyph = "🛠️" },
        new() { Name = "idea", Glyph = "💡" },
        new() { Name = "home", Glyph = "🏠" },
        new() { Name = "bolt", Glyph = "⚡" },
        new() { Name = "flower", Glyph = "🌸" },
        new() { Name = "coffee", Glyph = "☕" }
    ];

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        StartTray();
        StartTopMostTimer();
        WireCollections();
        Closed += OnClosed;
        _ = InitializeAsync();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _topMostTimer?.Stop();
        _focusTimer?.Stop();
        _counterTimer?.Stop();
        _clipboardService?.Stop();
        _tray.Dispose();
    }

    private void StartTray()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CuteSpace.ico");
        _tray.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            _tray.Hide();
            _appWindow?.Show();
            _state.Settings.BubbleHidden = false;
            CollapsePanel();
            KeepTopMost();
            _ = SaveAsync();
        });
        _tray.Show(iconPath);
    }

    private async Task InitializeAsync()
    {
        _state = await _store.LoadAsync();
        NormalizeLoadedState();
        PositionInitialBubble();
        _state.Settings.StartWithWindows = _autostart.IsEnabled();
        _sounds.Enabled = _state.Settings.PlayCuteSounds;
        SoundToggle.IsOn = _sounds.Enabled;

        await _localizer.LoadAsync(_state.Settings.LanguageCode);
        ApplyVisualStyle();
        ApplyLanguage();
        _state.Settings.BubbleHidden = false;

        foreach (var mode in _state.Modes.OrderBy(x => x.Name))
        {
            _modes.Add(mode);
        }

        foreach (var shortcut in _state.Shortcuts.OrderBy(x => x.Order))
        {
            _shortcuts.Add(shortcut);
        }

        foreach (var entry in _state.ClipboardHistory.OrderByDescending(x => x.CreatedAt).Take(40))
        {
            _clipboard.Add(entry);
        }

        ModePicker.SelectedIndex = _modes.Count > 0 ? 0 : -1;
        SelectSection(_state.Settings.PinnedSection);
        UpdateActionButtons();

        _clipboardService = new ClipboardHistoryService(_state.ClipboardHistory);
        _clipboardService.EntryAdded += ClipboardService_EntryAdded;
        _clipboardService.Start();

        if (!_state.Settings.FirstRunCompleted)
        {
            ExpandPanel();
        }

        await Task.Delay(350);
        await AskStyleIfNeededAsync();
        await AskFirstRunPermissionsAsync();
        await RunStartupModeIfNeededAsync();
    }

    private void ConfigureWindow()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new SizeInt32(CollapsedWidth, CollapsedHeight));
        _appWindow.IsShownInSwitchers = false;

        if (OverlappedPresenter.Create() is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            _appWindow.SetPresenter(presenter);
        }

        ExtendsContentIntoTitleBar = true;

        SystemBackdrop = null;

        var cornerPreference = DwmwcpDoNotRound;
        DwmSetWindowAttribute(_hwnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

        PositionInitialBubble();
        ApplyWindowRegionSoon();
    }

    private void ApplyWindowRegion()
    {
        if (_hwnd == 0 || !GetWindowRect(_hwnd, out var rect))
        {
            return;
        }

        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        nint region;

        if (_isExpanded)
        {
            var scaleX = _appWindow is null || _appWindow.Size.Width <= 0 ? 1.0 : width / (double)_appWindow.Size.Width;
            var scaleY = _appWindow is null || _appWindow.Size.Height <= 0 ? 1.0 : height / (double)_appWindow.Size.Height;
            var insetX = Math.Max(0, (int)Math.Round(9 * scaleX));
            var insetY = Math.Max(0, (int)Math.Round(9 * scaleY));
            var cornerW = Math.Max(1, (int)Math.Round(74 * scaleX));
            var cornerH = Math.Max(1, (int)Math.Round(74 * scaleY));
            region = CreateRoundRectRgn(insetX, insetY, width - insetX, height - insetY, cornerW, cornerH);
        }
        else
        {
            var diameter = Math.Max(1, Math.Min(width - 1, height - 3));
            var left = (width - diameter) / 2;
            var top = (height - diameter) / 2;
            region = CreateEllipticRgn(left + 2, top + 3, left + diameter, top + diameter);
        }

        if (region != 0 && SetWindowRgn(_hwnd, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    private void ApplyWindowRegionSoon()
    {
        ApplyWindowRegion();
        DispatcherQueue.TryEnqueue(ApplyWindowRegion);
    }

    private void StartTopMostTimer()
    {
        _topMostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _topMostTimer.Tick += (_, _) =>
        {
            if (_appWindow is not null && !_isDragging && !_isResizing)
            {
                KeepTopMost();
            }
        };
        _topMostTimer.Start();
    }

    private void PositionInitialBubble()
    {
        if (_appWindow is null)
        {
            return;
        }

        var size = _appWindow.Size;
        var display = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        var width = Math.Max(size.Width, CollapsedWidth);
        var height = Math.Max(size.Height, CollapsedHeight);

        if (_state.Settings.BubbleX >= 0 && _state.Settings.BubbleY >= 0)
        {
            var x = Math.Clamp(_state.Settings.BubbleX, work.X, Math.Max(work.X, work.X + work.Width - width));
            var y = Math.Clamp(_state.Settings.BubbleY, work.Y, Math.Max(work.Y, work.Y + work.Height - height));
            _appWindow.Move(new PointInt32(x, y));
            return;
        }

        _appWindow.Move(new PointInt32(work.X + work.Width - width - 220, work.Y + 180));
    }

    private void WireCollections()
    {
        ModePicker.ItemsSource = _modes;
        ModeItemsList.ItemsSource = _selectedModeItems;
        ShortcutsList.ItemsSource = _shortcuts;
        ClipboardList.ItemsSource = _clipboard;
    }

    private void NormalizeLoadedState()
    {
        foreach (var mode in _state.Modes)
        {
            mode.IconGlyph = NormalizeIcon(mode.IconGlyph, "🌸");
            foreach (var item in mode.Items)
            {
                item.IconGlyph = NormalizeIcon(item.IconGlyph, item.Type == LaunchItemType.Url ? "🌐" : "📦");
            }
        }

        foreach (var item in _state.Shortcuts)
        {
            item.IconGlyph = NormalizeIcon(item.IconGlyph, item.Type == LaunchItemType.Url ? "🌐" : "📦");
        }
    }

    private static string NormalizeIcon(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length == 1 && char.IsControl(value[0]))
        {
            return fallback;
        }

        var first = value[0];
        return first is >= '\uE000' and <= '\uF8FF' ? fallback : value;
    }

    private void ApplyLanguage()
    {
        var style = CurrentVisualStyle();
        TitleText.Text = style.Brand;
        SubtitleText.Text = _localizer[$"app.subtitle.{style.Key}"];
        ModesTab.Content = _localizer["tab.modes"];
        ShortcutsTab.Content = _localizer["tab.shortcuts"];
        ClipboardTab.Content = _localizer["tab.clipboard"];
        if (FocusTab is not null) FocusTab.Content = _localizer["tab.focus"];

        HowItWorksText.Text = _localizer["action.how"];
        RunModeText.Text = _localizer["action.runMode"];
        ClipboardHintText.Text = _localizer["clipboard.hint"];
        StatusText.Text = _localizer["status.ready"];
        StartupModeToggle.Header = _localizer["mode.runAtStartup"];
        LocalizeToggle(StartupModeToggle);
        LocalizeToggle(SoundToggle);

        if (ModePicker is not null) ModePicker.PlaceholderText = _localizer["mode.picker"];
        if (FocusMinutesBox is not null) FocusMinutesBox.PlaceholderText = _localizer["focus.minPlaceholder"];

        if (FocusTimerHeader is not null) FocusTimerHeader.Text = _localizer["focus.timer"];
        if (FocusMinutesLabel is not null) FocusMinutesLabel.Text = _localizer["focus.minutes"];
        if (StartFocusBtnText is not null) StartFocusBtnText.Text = _localizer["focus.start"];
        if (PauseFocusBtnText is not null) PauseFocusBtnText.Text = _focusPaused ? _localizer["focus.resume"] : _localizer["focus.pause"];
        if (FocusCounterHeader is not null) FocusCounterHeader.Text = _localizer["focus.counter"];
        if (StartCounterBtnText is not null) StartCounterBtnText.Text = _localizer["focus.start"];
        if (PauseCounterBtnText is not null) PauseCounterBtnText.Text = _counterPaused ? _localizer["focus.resume"] : _localizer["focus.pause"];

        ApplyTooltips();
        ApplyLaunchItemLocalization();
        RefreshSelectedModeItems();
        RefreshShortcuts();
    }

    private void LocalizeToggle(ToggleSwitch toggle)
    {
        toggle.OnContent = _localizer["common.on"];
        toggle.OffContent = _localizer["common.off"];
    }

    private void ApplyLaunchItemLocalization()
    {
        foreach (var item in _state.Modes.SelectMany(mode => mode.Items).Concat(_state.Shortcuts))
        {
            item.TypeLabelText = LocalizedTypeLabel(item.Type);
            item.DisplayTargetText = item.Type == LaunchItemType.Url && item.UrlTabCount > 1
                ? string.Format(_localizer["item.webTabs"], item.UrlTabCount)
                : item.Target;
        }
    }

    private string LocalizedTypeLabel(LaunchItemType type) => type switch
    {
        LaunchItemType.App => _localizer["item.type.app"],
        LaunchItemType.File => _localizer["item.type.file"],
        LaunchItemType.Folder => _localizer["item.type.folder"],
        LaunchItemType.Url => _localizer["item.type.web"],
        LaunchItemType.WindowsSetting => _localizer["item.type.windows"],
        LaunchItemType.Tool => _localizer["item.type.tool"],
        _ => _localizer["item.type.item"]
    };

    private VisualStyleOption CurrentVisualStyle()
    {
        return _visualStyles.FirstOrDefault(x => x.Key.Equals(_state.Settings.VisualStyle, StringComparison.OrdinalIgnoreCase))
               ?? _visualStyles[0];
    }

    private void ApplyVisualStyle()
    {
        var style = CurrentVisualStyle();
        _state.Settings.VisualStyle = style.Key;
        _sounds.Style = style.Key;

        // Dynamically update the font family resource so ThemeResource resolves it
        if (Application.Current.Resources.TryGetValue("CuteFont", out var _))
        {
            Application.Current.Resources["CuteFont"] = new FontFamily(style.Font);
        }

        SetBrush("CuteInkBrush", style.Ink);
        SetBrush("CuteMutedBrush", style.Muted);
        SetBrush("CutePinkBrush", style.AccentSoft);
        SetBrush("CuteLavenderBrush", style.SecondarySoft);
        SetBrush("CuteMintBrush", style.SecondarySoft);
        SetBrush("CuteSkyBrush", style.AccentSoft);
        SetBrush("CuteSurfaceBrush", style.Surface);

        // Update the border brush to be a semi-transparent version of Muted color
        var mutedColor = ParseColor(style.Muted);
        mutedColor.A = 32; // ~12% alpha
        if (Application.Current.Resources["CuteBorderBrush"] is SolidColorBrush borderBrush)
        {
            borderBrush.Color = mutedColor;
        }

        // Update the dynamic strong accent brush
        if (Application.Current.Resources["CuteAccentBrush"] is SolidColorBrush accentBrush)
        {
            accentBrush.Color = ParseColor(style.Accent);
        }

        RootGrid.Background = new SolidColorBrush(ParseColor("#00FFFFFF"));
        PanelShell.Background = new SolidColorBrush(ParseColor("#00FFFFFF"));
        PanelFrame.Background = new SolidColorBrush(ParseColor(style.Surface));
        var outerStrokeColor = ParseColor(style.Muted);
        outerStrokeColor.A = 90;
        PanelFrame.BorderBrush = new SolidColorBrush(outerStrokeColor);
        BubbleButton.BorderBrush = new SolidColorBrush(ParseColor(style.BubbleStroke));
        BubbleButton.Background = new SolidColorBrush(ParseColor(style.BubbleOuter));
        BubbleFace.Fill = new SolidColorBrush(ParseColor(style.BubbleInner));
        BubbleFace.Stroke = new SolidColorBrush(ParseColor(style.BubbleStroke));
        BubbleAccentLeft.Fill = new SolidColorBrush(ParseColor(style.BubbleAccent));
        BubbleAccentRight.Fill = new SolidColorBrush(ParseColor(style.BubbleAccent));
        BubbleEyeLeft.Fill = new SolidColorBrush(ParseColor(style.Eye));
        BubbleEyeRight.Fill = new SolidColorBrush(ParseColor(style.Eye));
        BubbleNose.Fill = new SolidColorBrush(ParseColor(style.Nose));
        BubbleBrandText.Text = style.ShortBrand;
        BubbleBrandText.Foreground = new SolidColorBrush(ParseColor(style.Accent));

        RunModeButton.Background = new SolidColorBrush(ParseColor(style.AccentSoft));
        Title = style.Brand;
        ApplyPetStyle();
    }
    private void ApplyPetStyle()
    {
        try
        {
            if (BubbleEarLeft is null || BubbleEarRight is null) return;
            var style = _state.Settings.PetStyle?.ToLowerInvariant() ?? "classic";
            
            BubbleEarLeft.Visibility = Visibility.Collapsed;
            BubbleEarRight.Visibility = Visibility.Collapsed;
            if (BubbleEarBearLeft is not null) BubbleEarBearLeft.Visibility = Visibility.Collapsed;
            if (BubbleEarBearRight is not null) BubbleEarBearRight.Visibility = Visibility.Collapsed;
            if (BubbleEarBunnyLeft is not null) BubbleEarBunnyLeft.Visibility = Visibility.Collapsed;
            if (BubbleEarBunnyRight is not null) BubbleEarBunnyRight.Visibility = Visibility.Collapsed;

            var visStyle = CurrentVisualStyle();
            var earBrush = new SolidColorBrush(ParseColor(visStyle.BubbleStroke));

            if (style == "cat")
            {
                BubbleEarLeft.Visibility = Visibility.Visible;
                BubbleEarRight.Visibility = Visibility.Visible;
                BubbleEarLeft.Fill = earBrush;
                BubbleEarRight.Fill = earBrush;
            }
            else if (style == "bear")
            {
                if (BubbleEarBearLeft is not null && BubbleEarBearRight is not null)
                {
                    BubbleEarBearLeft.Visibility = Visibility.Visible;
                    BubbleEarBearRight.Visibility = Visibility.Visible;
                    BubbleEarBearLeft.Fill = earBrush;
                    BubbleEarBearRight.Fill = earBrush;
                }
            }
            else if (style == "bunny")
            {
                if (BubbleEarBunnyLeft is not null && BubbleEarBunnyRight is not null)
                {
                    BubbleEarBunnyLeft.Visibility = Visibility.Visible;
                    BubbleEarBunnyRight.Visibility = Visibility.Visible;
                    BubbleEarBunnyLeft.Fill = earBrush;
                    BubbleEarBunnyRight.Fill = earBrush;
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), "ApplyPetStyle error: " + ex);
        }
    }

    private void TriggerHappyPet()
    {
        try
        {
            if (BubbleEyeLeft is null || BubbleEyeLeftHappy is null) return;

            // Show happy face
            BubbleEyeLeft.Visibility = Visibility.Collapsed;
            BubbleEyeRight.Visibility = Visibility.Collapsed;
            BubbleEyeLeftHappy.Visibility = Visibility.Visible;
            BubbleEyeRightHappy.Visibility = Visibility.Visible;
            BubbleMouthHappy.Visibility = Visibility.Visible;
            BubbleNose.Visibility = Visibility.Collapsed;

            // Show blush
            BubbleBlushLeft.Opacity = 0.7;
            BubbleBlushRight.Opacity = 0.7;
            
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                BubbleEyeLeft.Visibility = Visibility.Visible;
                BubbleEyeRight.Visibility = Visibility.Visible;
                BubbleEyeLeftHappy.Visibility = Visibility.Collapsed;
                BubbleEyeRightHappy.Visibility = Visibility.Collapsed;
                BubbleMouthHappy.Visibility = Visibility.Collapsed;
                BubbleNose.Visibility = Visibility.Visible;
                BubbleBlushLeft.Opacity = 0;
                BubbleBlushRight.Opacity = 0;
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), "TriggerHappyPet error: " + ex);
        }
    }

    private void ApplyTooltips()
    {
        ToolTipService.SetToolTip(DragHandle, _localizer["tip.move"]);
        ToolTipService.SetToolTip(SettingsButton, _localizer["tip.settings"]);
        ToolTipService.SetToolTip(HideBubbleButton, _localizer["tip.hide"]);
        ToolTipService.SetToolTip(ClosePanelButton, _localizer["tip.close"]);
        ToolTipService.SetToolTip(NewModeButton, _localizer["tip.newMode"]);
        ToolTipService.SetToolTip(DeleteModeButton, _localizer["tip.deleteMode"]);
        ToolTipService.SetToolTip(AddModeAppButton, _localizer["tip.addApp"]);
        ToolTipService.SetToolTip(AddModeExeButton, _localizer["tip.addExe"]);
        ToolTipService.SetToolTip(AddModeFileButton, _localizer["tip.addFile"]);
        ToolTipService.SetToolTip(AddModeUrlButton, _localizer["tip.addUrl"]);
        ToolTipService.SetToolTip(OpenModeItemButton, _localizer["tip.openNow"]);
        ToolTipService.SetToolTip(EditModeItemButton, _localizer["tip.edit"]);
        ToolTipService.SetToolTip(MoveModeItemUpButton, _localizer["tip.moveUp"]);
        ToolTipService.SetToolTip(MoveModeItemDownButton, _localizer["tip.moveDown"]);
        ToolTipService.SetToolTip(DeleteModeItemButton, _localizer["tip.delete"]);
        ToolTipService.SetToolTip(AddShortcutAppButton, _localizer["tip.addApp"]);
        ToolTipService.SetToolTip(AddShortcutExeButton, _localizer["tip.addExe"]);
        ToolTipService.SetToolTip(AddShortcutFileButton, _localizer["tip.addFile"]);
        ToolTipService.SetToolTip(AddShortcutUrlButton, _localizer["tip.addUrl"]);
        ToolTipService.SetToolTip(OpenShortcutButton, _localizer["tip.open"]);
        ToolTipService.SetToolTip(EditShortcutButton, _localizer["tip.edit"]);
        ToolTipService.SetToolTip(MoveShortcutUpButton, _localizer["tip.moveUp"]);
        ToolTipService.SetToolTip(MoveShortcutDownButton, _localizer["tip.moveDown"]);
        ToolTipService.SetToolTip(DeleteShortcutButton, _localizer["tip.delete"]);
        ToolTipService.SetToolTip(SoundToggle, _localizer["tip.sounds"]);
        ToolTipService.SetToolTip(ResizeGrip, _localizer["tip.resize"]);
    }

    private static void SetBrush(string key, string color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = ParseColor(color);
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        var offset = value.Length == 8 ? 2 : 0;
        var a = value.Length == 8 ? Convert.ToByte(value[..2], 16) : (byte)255;
        var r = Convert.ToByte(value.Substring(offset, 2), 16);
        var g = Convert.ToByte(value.Substring(offset + 2, 2), 16);
        var b = Convert.ToByte(value.Substring(offset + 4, 2), 16);
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }

    private async Task AskFirstRunPermissionsAsync()
    {
        if (_state.Settings.FirstRunCompleted)
        {
            return;
        }

        if (!_isExpanded)
        {
            ExpandPanel();
            await Task.Delay(180);
        }

        var result = await ShowDialogAsync(
            _localizer["startup.title"],
            _localizer["startup.message"],
            _localizer["common.yes"],
            _localizer["common.later"],
            _localizer["common.no"]);

        if (result == ContentDialogResult.Primary)
        {
            _state.Settings.StartWithWindows = _autostart.SetEnabled(true);
        }

        _state.Settings.FirstRunCompleted = true;
        await SaveAsync();
    }

    private async Task AskStyleIfNeededAsync()
    {
        if (_state.Settings.StyleFirstRunCompleted)
        {
            return;
        }

        if (!_isExpanded)
        {
            ExpandPanel();
            await Task.Delay(180);
        }

        await AskFirstRunStyleAsync();
        _state.Settings.StyleFirstRunCompleted = true;
        await SaveAsync();
    }

    private async Task AskFirstRunStyleAsync()
    {
        var stylePicker = CreateStylePicker(_state.Settings.VisualStyle);
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = _localizer["style.firstRunMessage"],
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = (Brush)Application.Current.Resources["CuteMutedBrush"]
        });
        panel.Children.Add(stylePicker);

        var result = await ShowDialogAsync(
            _localizer["style.firstRunTitle"],
            panel,
            _localizer["common.save"],
            _localizer["common.skip"]);

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        _state.Settings.VisualStyle = SelectedStyleKey(stylePicker);
        ApplyVisualStyle();
        ApplyLanguage();
        _sounds.Success();
    }

    private void ClipboardService_EntryAdded(object? sender, ClipboardEntry entry)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            _clipboard.Insert(0, entry);
            while (_clipboard.Count > 40)
            {
                _clipboard.RemoveAt(_clipboard.Count - 1);
            }

            TriggerHappyPet();
            await SaveAsync();
        });
    }

    private void DragSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_appWindow is null || _isResizing)
        {
            return;
        }

        if (ReferenceEquals(sender, DragHandle))
        {
            _sounds.Tap();
            ReleaseCapture();
            SendMessage(_hwnd, WmNclButtonDown, HtCaption, 0);
            KeepTopMost();
            return;
        }

        _isDragging = true;
        _dragMoved = false;
        _dragStartPointer = e.GetCurrentPoint(RootGrid).Position;
        GetCursorPos(out _dragStartCursor);
        _dragStartWindow = _appWindow.Position;

        if (sender is UIElement element)
        {
            element.CapturePointer(e.Pointer);
        }
    }

    private void DragSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _appWindow is null || _isResizing)
        {
            return;
        }

        GetCursorPos(out var cursor);
        var deltaX = cursor.X - _dragStartCursor.X;
        var deltaY = cursor.Y - _dragStartCursor.Y;

        if (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3)
        {
            _dragMoved = true;
        }

        var next = new PointInt32(_dragStartWindow.X + deltaX, _dragStartWindow.Y + deltaY);
        _appWindow.Move(next);
    }

    private async void DragSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        _isDragging = false;
        if (_appWindow is not null)
        {
            _state.Settings.BubbleX = _appWindow.Position.X;
            _state.Settings.BubbleY = _appWindow.Position.Y;
            await SaveAsync();
        }

        if (!_dragMoved && ReferenceEquals(sender, BubbleButton))
        {
            TriggerHappyPet();
            TogglePanel();
        }
    }

    private void TogglePanel()
    {
        if (_isExpanded)
        {
            CollapsePanel();
        }
        else
        {
            ExpandPanel();
        }
    }

    private void ExpandPanel()
    {
        _sounds.Pop();
        AnimateBubblePop();
        _isExpanded = true;
        BubbleOnly.Visibility = Visibility.Collapsed;
        PanelShell.Visibility = Visibility.Visible;
        AnimatePanelIn();
        var panelSize = GetSavedPanelSize();
        MovePanelToLeftOfBubble(panelSize);
        ResizePanelWindow(panelSize);
        ApplyWindowRegionSoon();
        KeepTopMost();
    }

    private void CollapsePanel()
    {
        _sounds.Tap();
        AnimateBubblePop();
        _isExpanded = false;
        PanelShell.Visibility = Visibility.Collapsed;
        PanelShell.Opacity = 0;
        BubbleOnly.Visibility = Visibility.Visible;
        _appWindow?.Resize(new SizeInt32(CollapsedWidth, CollapsedHeight));
        ApplyWindowRegionSoon();
        if (_appWindow is not null && _bubbleAnchorBeforeExpand is { } anchor)
        {
            _appWindow.Move(anchor);
            _bubbleAnchorBeforeExpand = null;
        }
        ApplyWindowRegionSoon();
        KeepTopMost();
    }

    private void MovePanelToLeftOfBubble(SizeInt32 panelSize)
    {
        if (_appWindow is null)
        {
            return;
        }

        _bubbleAnchorBeforeExpand = _appWindow.Position;
        var display = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd), DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        var x = _appWindow.Position.X - (panelSize.Width - CollapsedWidth);
        var y = _appWindow.Position.Y;

        x = Math.Clamp(x, work.X, work.X + work.Width - panelSize.Width);
        y = Math.Clamp(y, work.Y, work.Y + work.Height - panelSize.Height);
        _appWindow.Move(new PointInt32(x, y));
    }

    private void KeepTopMost()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }
    }

    private SizeInt32 GetSavedPanelSize()
    {
        return new SizeInt32(
            Math.Clamp(_state.Settings.PanelWidth <= 0 ? DefaultExpandedWidth : _state.Settings.PanelWidth, MinExpandedWidth, MaxExpandedWidth),
            Math.Clamp(_state.Settings.PanelHeight <= 0 ? DefaultExpandedHeight : _state.Settings.PanelHeight, MinExpandedHeight, MaxExpandedHeight));
    }

    private void ResizePanelWindow(SizeInt32 size)
    {
        if (_appWindow is null)
        {
            return;
        }

        var safeSize = new SizeInt32(
            Math.Clamp(size.Width, MinExpandedWidth, MaxExpandedWidth),
            Math.Clamp(size.Height, MinExpandedHeight, MaxExpandedHeight));

        _appWindow.Resize(safeSize);
        ApplyWindowRegionSoon();
        _state.Settings.PanelWidth = safeSize.Width;
        _state.Settings.PanelHeight = safeSize.Height;
    }

    private void ResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_appWindow is null || !_isExpanded)
        {
            return;
        }

        _isResizing = true;
        _resizeStartPointer = e.GetCurrentPoint(RootGrid).Position;
        _resizeStartSize = _appWindow.Size;
        ResizeGrip.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing || _appWindow is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(RootGrid).Position;
        var width = _resizeStartSize.Width + (int)(point.X - _resizeStartPointer.X);
        var height = _resizeStartSize.Height + (int)(point.Y - _resizeStartPointer.Y);
        ResizePanelWindow(new SizeInt32(width, height));
        e.Handled = true;
    }

    private async void ResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ResizeGrip.ReleasePointerCapture(e.Pointer);
        _isResizing = false;
        await SaveAsync();
        e.Handled = true;
    }

    private void AnimateBubblePop()
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateScaleAnimation(BubbleScale, "ScaleX", 1.0, 1.12, 130));
        storyboard.Children.Add(CreateScaleAnimation(BubbleScale, "ScaleY", 1.0, 1.12, 130));
        storyboard.AutoReverse = true;
        storyboard.Begin();
    }

    private void AnimatePanelIn()
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, PanelShell);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static DoubleAnimation CreateScaleAnimation(DependencyObject target, string property, double from, double to, double milliseconds)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private void ClosePanelButton_Click(object sender, RoutedEventArgs e) => CollapsePanel();

    private async void HideBubbleButton_Click(object sender, RoutedEventArgs e)
    {
        _sounds.Tap();
        _state.Settings.BubbleHidden = true;
        _appWindow?.Hide();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CuteSpace.ico");
        _tray.Show(iconPath);
        await SaveAsync();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var language = new ComboBox { MinWidth = 260 };
        foreach (var item in _localizer.SupportedLanguages)
        {
            language.Items.Add(new ComboBoxItem { Content = item.Value, Tag = item.Key });
        }

        language.SelectedIndex = _localizer.SupportedLanguages.Keys.ToList().IndexOf(_state.Settings.LanguageCode);
        if (language.SelectedIndex < 0)
        {
            language.SelectedIndex = 0;
        }

        var pinned = new ComboBox { MinWidth = 260 };
        pinned.Items.Add(new ComboBoxItem { Content = _localizer["tab.modes"], Tag = "modes" });
        pinned.Items.Add(new ComboBoxItem { Content = _localizer["tab.shortcuts"], Tag = "shortcuts" });
        pinned.Items.Add(new ComboBoxItem { Content = _localizer["tab.clipboard"], Tag = "clipboard" });
        pinned.SelectedIndex = _state.Settings.PinnedSection switch
        {
            "shortcuts" => 1,
            "clipboard" => 2,
            _ => 0
        };

        var start = new ToggleSwitch { Header = _localizer["settings.autostart"], IsOn = _state.Settings.StartWithWindows };
        var sounds = new ToggleSwitch { Header = _localizer["settings.sounds"], IsOn = _state.Settings.PlayCuteSounds };
        LocalizeToggle(start);
        LocalizeToggle(sounds);
        var stylePicker = CreateStylePicker(_state.Settings.VisualStyle);
        var startupMode = new ComboBox { Header = _localizer["settings.startupMode"], MinWidth = 260 };
        startupMode.Items.Add(new ComboBoxItem { Content = _localizer["settings.noStartupMode"], Tag = "" });
        foreach (var mode in _state.Modes.OrderBy(x => x.Name))
        {
            startupMode.Items.Add(new ComboBoxItem { Content = mode.Name, Tag = mode.Id });
        }

        startupMode.SelectedIndex = 0;
        for (var i = 0; i < startupMode.Items.Count; i++)
        {
            if ((startupMode.Items[i] as ComboBoxItem)?.Tag?.ToString() == (_state.Settings.StartupModeId ?? ""))
            {
                startupMode.SelectedIndex = i;
                break;
            }
        }

        var petStyle = new ComboBox { Header = _localizer["settings.petStyle"], MinWidth = 260 };
        petStyle.Items.Add(new ComboBoxItem { Content = _localizer["settings.petStyle.classic"], Tag = "classic" });
        petStyle.Items.Add(new ComboBoxItem { Content = _localizer["settings.petStyle.cat"], Tag = "cat" });
        petStyle.Items.Add(new ComboBoxItem { Content = _localizer["settings.petStyle.bear"], Tag = "bear" });
        petStyle.Items.Add(new ComboBoxItem { Content = _localizer["settings.petStyle.bunny"], Tag = "bunny" });
        petStyle.SelectedIndex = _state.Settings.PetStyle switch
        {
            "cat" => 1,
            "bear" => 2,
            "bunny" => 3,
            _ => 0
        };

        var btnExport = new Button { Content = _localizer["settings.export"] };
        btnExport.Click += async (_, _) => await ExportDataAsync();
        var btnImport = new Button { Content = _localizer["settings.import"] };
        btnImport.Click += async (_, _) => {
            if (await ImportDataAsync()) {
                StatusText.Text = _localizer["settings.importSuccess"];
            }
        };
        var dataPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        dataPanel.Children.Add(btnExport);
        dataPanel.Children.Add(btnImport);

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = _localizer["settings.language"] });
        panel.Children.Add(language);
        panel.Children.Add(new TextBlock { Text = _localizer["settings.pinned"] });
        panel.Children.Add(pinned);
        panel.Children.Add(new TextBlock { Text = _localizer["settings.style"] });
        panel.Children.Add(stylePicker);
        panel.Children.Add(petStyle);
        panel.Children.Add(startupMode);
        panel.Children.Add(start);
        panel.Children.Add(sounds);
        panel.Children.Add(new TextBlock { Text = _localizer["settings.backup"], Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(dataPanel);

        var result = await ShowDialogAsync(_localizer["settings.title"], panel, _localizer["common.save"], _localizer["common.cancel"]);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        _state.Settings.LanguageCode = ((ComboBoxItem)language.SelectedItem).Tag?.ToString() ?? "es";
        _state.Settings.PinnedSection = ((ComboBoxItem)pinned.SelectedItem).Tag?.ToString() ?? "modes";
        _state.Settings.VisualStyle = SelectedStyleKey(stylePicker);
        _state.Settings.PetStyle = ((ComboBoxItem)petStyle.SelectedItem).Tag?.ToString() ?? "classic";
        _state.Settings.StartupModeId = ((ComboBoxItem)startupMode.SelectedItem).Tag?.ToString();
        if (string.IsNullOrWhiteSpace(_state.Settings.StartupModeId))
        {
            _state.Settings.StartupModeId = null;
        }
        _state.Settings.StartWithWindows = _autostart.SetEnabled(start.IsOn);
        _state.Settings.PlayCuteSounds = sounds.IsOn;
        _sounds.Enabled = sounds.IsOn;
        SoundToggle.IsOn = sounds.IsOn;

        await _localizer.LoadAsync(_state.Settings.LanguageCode);
        ApplyVisualStyle();
        ApplyLanguage();
        SelectSection(_state.Settings.PinnedSection);
        await SaveAsync();
    }

    private async Task ExportDataAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("JSON Data", [".json"]);
            picker.SuggestedFileName = "CuteSpace_Backup.json";
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_state);
                await Windows.Storage.FileIO.WriteTextAsync(file, json);
                StatusText.Text = _localizer["settings.exportSuccess"];
            }
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), "Export error: " + ex);
        }
    }

    private async Task<bool> ImportDataAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker { SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                var newState = System.Text.Json.JsonSerializer.Deserialize<AppState>(json);
                if (newState != null)
                {
                    await _store.SaveAsync(newState);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), "Import error: " + ex);
        }
        return false;
    }

    private GridView CreateStylePicker(string selectedStyle)
    {
        var picker = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            MaxHeight = 220
        };

        picker.ItemsPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
        <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <ItemsWrapGrid Orientation="Horizontal" MaximumRowsOrColumns="1"/>
        </ItemsPanelTemplate>
        """);

        foreach (var style in _visualStyles)
        {
            var card = new Border
            {
                Width = 138,
                Height = 126,
                Margin = new Thickness(4),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(ParseColor(style.Surface)),
                BorderBrush = new SolidColorBrush(ParseColor(style.Accent)),
                BorderThickness = new Thickness(1),
                Tag = style.Key
            };

            var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(21),
                Background = new SolidColorBrush(ParseColor(style.BubbleOuter)),
                BorderBrush = new SolidColorBrush(ParseColor(style.BubbleStroke)),
                BorderThickness = new Thickness(2),
                Child = new TextBlock
                {
                    Text = style.ShortBrand[..1],
                    Foreground = new SolidColorBrush(ParseColor(style.Accent)),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            stack.Children.Add(new TextBlock
            {
                Text = style.Brand,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(ParseColor(style.Ink))
            });
            stack.Children.Add(new TextBlock
            {
                Text = _localizer[$"style.{style.Key}.label"],
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(ParseColor(style.Muted))
            });
            card.Child = stack;
            picker.Items.Add(card);

            if (style.Key.Equals(selectedStyle, StringComparison.OrdinalIgnoreCase))
            {
                picker.SelectedItem = card;
            }
        }

        picker.SelectedItem ??= picker.Items.FirstOrDefault();
        picker.ItemClick += (_, e) => picker.SelectedItem = e.ClickedItem;
        return picker;
    }

    private static string SelectedStyleKey(GridView stylePicker)
    {
        return stylePicker.SelectedItem is Border { Tag: string key } ? key : "cute";
    }

    private async void HowItWorksButton_Click(object sender, RoutedEventArgs e)
    {
        var key = CurrentSectionKey();
        await ShowDialogAsync(_localizer[$"help.{key}.title"], _localizer[$"help.{key}.body"], _localizer["common.ok"]);
    }

    private void SectionTab_Checked(object sender, RoutedEventArgs e)
    {
        if (ModesView is null || ShortcutsView is null || ClipboardView is null || FocusView is null)
        {
            return;
        }

        SelectSection(CurrentSectionKey());
    }

    private string CurrentSectionKey()
    {
        if (ShortcutsTab.IsChecked == true)
        {
            return "shortcuts";
        }

        if (ClipboardTab.IsChecked == true)
        {
            return "clipboard";
        }

        if (FocusTab.IsChecked == true)
        {
            return "focus";
        }

        return "modes";
    }

    private bool _isSelectingSection;
    private void SelectSection(string section)
    {
        if (_isSelectingSection) return;
        _isSelectingSection = true;

        ModesView.Visibility = section == "modes" ? Visibility.Visible : Visibility.Collapsed;
        ShortcutsView.Visibility = section == "shortcuts" ? Visibility.Visible : Visibility.Collapsed;
        ClipboardView.Visibility = section == "clipboard" ? Visibility.Visible : Visibility.Collapsed;
        FocusView.Visibility = section == "focus" ? Visibility.Visible : Visibility.Collapsed;

        ModesTab.IsChecked = section == "modes";
        ShortcutsTab.IsChecked = section == "shortcuts";
        ClipboardTab.IsChecked = section == "clipboard";
        FocusTab.IsChecked = section == "focus";
        _state.Settings.PinnedSection = section;
        _ = SaveAsync();

        _isSelectingSection = false;
    }

    private void ModePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedModeItems();
        UpdateStartupModeToggle();
    }

    private void UpdateStartupModeToggle()
    {
        _updatingStartupToggle = true;
        StartupModeToggle.IsEnabled = ModePicker.SelectedItem is ModeProfile;
        StartupModeToggle.IsOn = ModePicker.SelectedItem is ModeProfile mode && _state.Settings.StartupModeId == mode.Id;
        _updatingStartupToggle = false;
    }

    private async void StartupModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingStartupToggle || ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        if (StartupModeToggle.IsOn)
        {
            _state.Settings.StartupModeId = mode.Id;
            _state.Settings.StartWithWindows = _autostart.SetEnabled(true);
            _sounds.Success();
        }
        else if (_state.Settings.StartupModeId == mode.Id)
        {
            _state.Settings.StartupModeId = null;
            _sounds.Tap();
        }

        await SaveAsync();
    }

    private void RefreshSelectedModeItems()
    {
        ApplyLaunchItemLocalization();
        _selectedModeItems.Clear();
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        foreach (var item in mode.Items.OrderBy(x => x.Order))
        {
            _selectedModeItems.Add(item);
        }
    }

    private async void NewModeButton_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptTextAsync(_localizer["mode.new"], _localizer["mode.name"], "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var mode = new ModeProfile { Name = name.Trim(), IconGlyph = await PickIconAsync() };
        _state.Modes.Add(mode);
        _modes.Add(mode);
        ModePicker.SelectedItem = mode;
        await SaveAsync();
    }

    private async void DeleteModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        var result = await ShowDialogAsync(_localizer["mode.delete"], string.Format(_localizer["mode.deleteMessage"], mode.Name), _localizer["common.delete"], _localizer["common.cancel"]);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        _state.Modes.Remove(mode);
        _modes.Remove(mode);
        ModePicker.SelectedIndex = _modes.Count > 0 ? 0 : -1;
        await SaveAsync();
    }

    private async void RunModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        StatusText.Text = string.Format(_localizer["status.running"], mode.Name);
        _sounds.Pop();
        await _launcher.LaunchModeAsync(mode, item => DispatcherQueue.TryEnqueue(() => StatusText.Text = string.Format(_localizer["status.opening"], item)));
        StatusText.Text = _localizer["status.ready"];
    }

    private async void AddModeAppButton_Click(object sender, RoutedEventArgs e) => await AddDetectedAppToModeAsync();
    private async void AddModeExeButton_Click(object sender, RoutedEventArgs e) => await AddExeToModeAsync();
    private async void AddModeFileButton_Click(object sender, RoutedEventArgs e) => await AddFileOrFolderToModeAsync();
    private async void AddModeUrlButton_Click(object sender, RoutedEventArgs e) => await AddUrlToModeAsync();
    private async void AddShortcutAppButton_Click(object sender, RoutedEventArgs e) => await AddDetectedAppToShortcutsAsync();
    private async void AddShortcutExeButton_Click(object sender, RoutedEventArgs e) => await AddExeToShortcutsAsync();
    private async void AddShortcutFileButton_Click(object sender, RoutedEventArgs e) => await AddFileOrFolderToShortcutsAsync();
    private async void AddShortcutUrlButton_Click(object sender, RoutedEventArgs e) => await AddUrlToShortcutsAsync();

    private async Task AddDetectedAppToModeAsync()
    {
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        var app = await PickInstalledAppAsync();
        if (app is null)
        {
            return;
        }

        var item = await BuildLaunchItemAsync(app.Name, LaunchItemType.App, app.Target, "📦");
        AddToMode(mode, item);
        await SaveAsync();
    }

    private async Task AddExeToModeAsync()
    {
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        var path = await PickFileAsync([".exe"]);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddToMode(mode, await BuildLaunchItemAsync(Path.GetFileNameWithoutExtension(path), LaunchItemType.App, path, "📦"));
        await SaveAsync();
    }

    private async Task AddFileOrFolderToModeAsync()
    {
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        var item = await PickFileOrFolderItemAsync();
        if (item is null)
        {
            return;
        }

        AddToMode(mode, item);
        await SaveAsync();
    }

    private async Task AddUrlToModeAsync()
    {
        if (ModePicker.SelectedItem is not ModeProfile mode)
        {
            return;
        }

        var item = await PromptUrlItemAsync();
        if (item is null)
        {
            return;
        }

        AddToMode(mode, item);
        await SaveAsync();
    }

    private async Task AddDetectedAppToShortcutsAsync()
    {
        var app = await PickInstalledAppAsync();
        if (app is null)
        {
            return;
        }

        AddShortcut(await BuildLaunchItemAsync(app.Name, LaunchItemType.App, app.Target, "📦"));
        await SaveAsync();
    }

    private async Task AddExeToShortcutsAsync()
    {
        var path = await PickFileAsync([".exe"]);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddShortcut(await BuildLaunchItemAsync(Path.GetFileNameWithoutExtension(path), LaunchItemType.App, path, "📦"));
        await SaveAsync();
    }

    private async Task AddFileOrFolderToShortcutsAsync()
    {
        var item = await PickFileOrFolderItemAsync();
        if (item is null)
        {
            return;
        }

        AddShortcut(item);
        await SaveAsync();
    }

    private async Task AddUrlToShortcutsAsync()
    {
        var item = await PromptUrlItemAsync();
        if (item is null)
        {
            return;
        }

        AddShortcut(item);
        await SaveAsync();
    }

    private void AddToMode(ModeProfile mode, LaunchItem item)
    {
        item.Order = mode.Items.Count;
        mode.Items.Add(item);
        RefreshSelectedModeItems();
        _sounds.Success();
    }

    private void AddShortcut(LaunchItem item)
    {
        item.Order = _state.Shortcuts.Count;
        _state.Shortcuts.Add(item);
        _shortcuts.Add(item);
        _sounds.Success();
    }

    private async Task<LaunchItem> BuildLaunchItemAsync(string defaultName, LaunchItemType type, string target, string defaultIcon)
    {
        var name = await PromptTextAsync(_localizer["item.nameTitle"], _localizer["item.name"], defaultName);
        var item = new LaunchItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? defaultName : name.Trim(),
            Type = type,
            Target = target,
            IconGlyph = await PickIconAsync(defaultIcon)
        };
        
        if (type == LaunchItemType.App || type == LaunchItemType.File)
        {
            item.IconFileName = ExtractAndSaveIcon(target);
        }
        
        return item;
    }

    private string? ExtractAndSaveIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".exe" || ext == ".lnk" || ext == ".bat" || ext == ".ps1" || ext == ".com" || ext == ".msc")
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    var iconFileName = Guid.NewGuid().ToString("N") + ".png";
                    var iconFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CuteSpace", "icons");
                    Directory.CreateDirectory(iconFolder);
                    var savePath = Path.Combine(iconFolder, iconFileName);
                    using (var bitmap = icon.ToBitmap())
                    {
                        bitmap.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    return iconFileName;
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), "Icon extraction error: " + ex);
        }
        return null;
    }

    private async Task<LaunchItem?> PickFileOrFolderItemAsync()
    {
        var result = await ShowDialogAsync(_localizer["picker.kind"], _localizer["picker.kindMessage"], _localizer["picker.file"], _localizer["picker.folder"], _localizer["common.cancel"]);
        if (result == ContentDialogResult.None)
        {
            return null;
        }

        if (result == ContentDialogResult.Secondary)
        {
            var folder = await PickFolderAsync();
            return string.IsNullOrWhiteSpace(folder)
                ? null
                : await BuildLaunchItemAsync(Path.GetFileName(folder), LaunchItemType.Folder, folder, "📁");
        }

        var file = await PickFileAsync(["*"]);
        return string.IsNullOrWhiteSpace(file)
            ? null
            : await BuildLaunchItemAsync(Path.GetFileName(file), LaunchItemType.File, file, "📄");
    }

    private async Task<LaunchItem?> PromptUrlItemAsync()
    {
        var nameBox = new TextBox { Header = _localizer["item.name"], PlaceholderText = _localizer["item.name"], Text = "Web" };
        var urlPanel = new StackPanel { Spacing = 8 };
        AddUrlBox(urlPanel, "https://music.youtube.com");
        var addUrlButton = new Button { Content = _localizer["url.addUrl"] };
        addUrlButton.Click += (_, _) => AddUrlBox(urlPanel, "");

        var browserPicker = new ComboBox { Header = _localizer["url.browserPicker"], MinWidth = 330 };
        var browsers = await GetBrowserOptionsAsync();
        browserPicker.Items.Add(new ComboBoxItem { Content = _localizer["url.defaultBrowser"], Tag = "" });
        foreach (var browser in browsers)
        {
            browserPicker.Items.Add(new ComboBoxItem { Content = browser.ToString(), Tag = browser.Path });
        }

        browserPicker.SelectedIndex = 0;
        var argsBox = new TextBox { PlaceholderText = _localizer["url.argsOptional"] };
        var chooseBrowser = new Button { Content = _localizer["url.chooseBrowser"] };
        chooseBrowser.Click += async (_, _) =>
        {
            var browser = await PickFileAsync([".exe"]);
            if (!string.IsNullOrWhiteSpace(browser))
            {
                SaveBrowser(Path.GetFileNameWithoutExtension(browser), browser, "Manual");
                browserPicker.Items.Add(new ComboBoxItem { Content = $"🌐 {Path.GetFileNameWithoutExtension(browser)} - Manual", Tag = browser });
                browserPicker.SelectedIndex = browserPicker.Items.Count - 1;
            }
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = _localizer["url.urlsHelp"], TextWrapping = TextWrapping.WrapWholeWords, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CuteMutedBrush"] });
        panel.Children.Add(urlPanel);
        panel.Children.Add(addUrlButton);
        panel.Children.Add(browserPicker);
        panel.Children.Add(argsBox);
        panel.Children.Add(chooseBrowser);

        var result = await ShowDialogAsync(_localizer["url.title"], panel, _localizer["common.add"], _localizer["common.cancel"]);
        var urls = ReadUrlBoxes(urlPanel);

        if (result != ContentDialogResult.Primary || urls.Count == 0)
        {
            return null;
        }

        return new LaunchItem
        {
            Name = string.IsNullOrWhiteSpace(nameBox.Text) ? urls.First() : nameBox.Text.Trim(),
            Type = LaunchItemType.Url,
            Target = string.Join(Environment.NewLine, urls),
            BrowserExecutablePath = ((ComboBoxItem)browserPicker.SelectedItem).Tag?.ToString(),
            Arguments = argsBox.Text.Trim(),
            IconGlyph = await PickIconAsync("🌐")
        };
    }

    private void AddUrlBox(StackPanel urlPanel, string value)
    {
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } }, ColumnSpacing = 8 };
        var box = new TextBox { PlaceholderText = "https://...", Text = value };
        var remove = new Button { Content = "×", Width = 36, Height = 36, Padding = new Thickness(0) };
        remove.Click += (_, _) =>
        {
            if (urlPanel.Children.Count > 1)
            {
                urlPanel.Children.Remove(row);
            }
            else
            {
                box.Text = "";
            }
        };
        row.Children.Add(box);
        Grid.SetColumn(remove, 1);
        row.Children.Add(remove);
        urlPanel.Children.Add(row);
    }

    private async Task<List<BrowserOption>> GetBrowserOptionsAsync()
    {
        var browsers = new Dictionary<string, BrowserOption>(StringComparer.OrdinalIgnoreCase);

        foreach (var browser in _state.Settings.SavedBrowsers.Where(x => !string.IsNullOrWhiteSpace(x.Path) && File.Exists(x.Path)))
        {
            browsers.TryAdd(browser.Path, browser);
        }

        var candidates = new (string Name, string Path, string Icon)[]
        {
            ("Google Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"), "🌈"),
            ("Google Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"), "🌈"),
            ("Microsoft Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"), "🌀"),
            ("Microsoft Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"), "🌀"),
            ("Brave", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BraveSoftware", "Brave-Browser", "Application", "brave.exe"), "🦁"),
            ("Brave", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "Application", "brave.exe"), "🦁"),
            ("Mozilla Firefox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", "firefox.exe"), "🦊"),
            ("Mozilla Firefox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox", "firefox.exe"), "🦊"),
            ("Opera", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Opera", "opera.exe"), "⭕"),
            ("Vivaldi", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "Application", "vivaldi.exe"), "🔺")
        };

        foreach (var (name, path, icon) in candidates.Where(x => File.Exists(x.Path)))
        {
            browsers.TryAdd(path, new BrowserOption { Name = name, Path = path, Source = "Detectado", Icon = icon });
        }

        foreach (var app in await _discovery.FindInstalledAppsAsync())
        {
            var lower = app.Name.ToLowerInvariant();
            if (lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("brave") || lower.Contains("firefox") || lower.Contains("opera") || lower.Contains("vivaldi"))
            {
                browsers.TryAdd(app.Target, new BrowserOption { Name = app.Name, Path = app.Target, Source = app.Source, Icon = "🌐" });
            }
        }

        return browsers.Values.OrderBy(x => x.Name).ToList();
    }

    private void SaveBrowser(string name, string path, string source)
    {
        if (string.IsNullOrWhiteSpace(path) || _state.Settings.SavedBrowsers.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _state.Settings.SavedBrowsers.Add(new BrowserOption
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name,
            Path = path,
            Source = source,
            Icon = "🌐"
        });
    }

    private async Task RunStartupModeIfNeededAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.Settings.StartupModeId))
        {
            return;
        }

        var mode = _state.Modes.FirstOrDefault(x => x.Id == _state.Settings.StartupModeId);
        if (mode is null)
        {
            return;
        }

        StatusText.Text = string.Format(_localizer["status.running"], mode.Name);
        await _launcher.LaunchModeAsync(mode, item => DispatcherQueue.TryEnqueue(() => StatusText.Text = string.Format(_localizer["status.opening"], item)));
        StatusText.Text = _localizer["status.ready"];
    }

    private static string NormalizeUrl(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "";
        }

        return trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : $"https://{trimmed}";
    }

    private async Task<InstalledAppOption?> PickInstalledAppAsync()
    {
        StatusText.Text = _localizer["status.scanning"];
        var apps = (await _discovery.FindInstalledAppsAsync()).ToList();
        StatusText.Text = _localizer["status.ready"];

        var filtered = new ObservableCollection<InstalledAppOption>(apps);
        var search = new TextBox { PlaceholderText = _localizer["picker.search"] };
        var list = new ListView
        {
            ItemsSource = filtered,
            Height = 430,
            DisplayMemberPath = "Name",
            SelectionMode = ListViewSelectionMode.Single
        };
        search.TextChanged += (_, _) =>
        {
            filtered.Clear();
            foreach (var app in apps.Where(x => x.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add(app);
            }
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(search);
        panel.Children.Add(list);

        var result = await ShowDialogAsync(_localizer["picker.apps"], panel, _localizer["common.add"], _localizer["common.cancel"]);
        return result == ContentDialogResult.Primary ? list.SelectedItem as InstalledAppOption : null;
    }

    private async Task<string?> PromptTextAsync(string title, string label, string defaultValue)
    {
        var box = new TextBox { Header = label, Text = defaultValue };
        var result = await ShowDialogAsync(title, box, _localizer["common.ok"], _localizer["common.cancel"]);
        return result == ContentDialogResult.Primary ? box.Text : null;
    }

    private async Task<string> PickIconAsync(string? defaultIcon = null)
    {
        var list = new GridView
        {
            Height = 320,
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            ItemsSource = _icons
        };

        list.ItemTemplate = CreateIconTemplate();
        list.SelectedItem = _icons.FirstOrDefault(x => x.Glyph == defaultIcon) ?? _icons.First();
        list.ItemClick += (_, e) =>
        {
            list.SelectedItem = e.ClickedItem;
            _sounds.Tap();
        };

        var result = await ShowDialogAsync(_localizer["icon.title"], list, _localizer["common.ok"], _localizer["common.skip"]);
        return result == ContentDialogResult.Primary && list.SelectedItem is IconOption option
            ? option.Glyph
            : defaultIcon ?? "🌸";
    }

    private DataTemplate CreateIconTemplate()
    {
        const string xaml = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:models="using:CuteSpace.Models">
            <Border Width="92" Height="82" CornerRadius="16" Padding="8" Background="#FFFFFFFF" BorderBrush="#22A45D82" BorderThickness="1">
                <StackPanel Spacing="4" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <TextBlock Text="{Binding Glyph}" FontFamily="Segoe UI Emoji" FontSize="26" HorizontalAlignment="Center"/>
                    <TextBlock Text="{Binding Name}" FontSize="11" TextAlignment="Center" TextTrimming="CharacterEllipsis"/>
                </StackPanel>
            </Border>
        </DataTemplate>
        """;

        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    private async Task<string?> PickFileAsync(IEnumerable<string> extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, _hwnd);
        foreach (var extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, _hwnd);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async void OpenModeItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModeItemsList.SelectedItem is LaunchItem item)
        {
            _sounds.Pop();
            await _launcher.LaunchAsync(item);
        }
    }

    private async void EditModeItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModeItemsList.SelectedItem is not LaunchItem item)
        {
            return;
        }

        if (await EditLaunchItemAsync(item))
        {
            _sounds.Success();
            RefreshSelectedModeItems();
            ModeItemsList.SelectedItem = item;
            await SaveAsync();
        }
    }

    private async void DeleteModeItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModePicker.SelectedItem is not ModeProfile mode || ModeItemsList.SelectedItem is not LaunchItem item)
        {
            return;
        }

        mode.Items.Remove(item);
        _sounds.Tap();
        Reindex(mode.Items);
        RefreshSelectedModeItems();
        await SaveAsync();
    }

    private async void MoveModeItemUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModePicker.SelectedItem is ModeProfile mode && ModeItemsList.SelectedItem is LaunchItem item)
        {
            MoveItem(mode.Items, item, -1);
            RefreshSelectedModeItems();
            ModeItemsList.SelectedItem = item;
            await SaveAsync();
        }
    }

    private async void MoveModeItemDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModePicker.SelectedItem is ModeProfile mode && ModeItemsList.SelectedItem is LaunchItem item)
        {
            MoveItem(mode.Items, item, 1);
            RefreshSelectedModeItems();
            ModeItemsList.SelectedItem = item;
            await SaveAsync();
        }
    }

    private async void OpenShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is LaunchItem item)
        {
            _sounds.Pop();
            await _launcher.LaunchAsync(item);
        }
    }

    private async void EditShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is not LaunchItem item)
        {
            return;
        }

        if (await EditLaunchItemAsync(item))
        {
            _sounds.Success();
            RefreshShortcuts();
            ShortcutsList.SelectedItem = item;
            await SaveAsync();
        }
    }

    private async void DeleteShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is not LaunchItem item)
        {
            return;
        }

        _state.Shortcuts.Remove(item);
        _shortcuts.Remove(item);
        _sounds.Tap();
        Reindex(_state.Shortcuts);
        await SaveAsync();
    }

    private async void MoveShortcutUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is LaunchItem item)
        {
            MoveItem(_state.Shortcuts, item, -1);
            RefreshShortcuts();
            ShortcutsList.SelectedItem = item;
            await SaveAsync();
        }
    }

    private async void MoveShortcutDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is LaunchItem item)
        {
            MoveItem(_state.Shortcuts, item, 1);
            RefreshShortcuts();
            ShortcutsList.SelectedItem = item;
            await SaveAsync();
        }
    }

    private async void ClipboardList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ClipboardEntry entry && _clipboardService is not null)
        {
            await _clipboardService.RestoreAsync(entry);
            StatusText.Text = _localizer["clipboard.restored"];
            _sounds.Tap();
        }
    }

    private async void ModeItemsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ReferenceEquals(_lastClickedModeItem, e.ClickedItem))
        {
            ModeItemsList.SelectedItem = null;
            _lastClickedModeItem = null;
            return;
        }

        _lastClickedModeItem = e.ClickedItem;
        ModeItemsList.SelectedItem = e.ClickedItem;
        _sounds.Tap();
    }

    private async void ShortcutsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ReferenceEquals(_lastClickedShortcutItem, e.ClickedItem))
        {
            ShortcutsList.SelectedItem = null;
            _lastClickedShortcutItem = null;
            UpdateShortcutDetails(null);
            return;
        }

        _lastClickedShortcutItem = e.ClickedItem;
        ShortcutsList.SelectedItem = e.ClickedItem;
        UpdateShortcutDetails(e.ClickedItem as LaunchItem);
        _sounds.Tap();
    }

    private async void ShortcutTile_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LaunchItem item })
        {
            _lastClickedShortcutItem = item;
            ShortcutsList.SelectedItem = item;
            UpdateShortcutDetails(item);
            _sounds.Pop();
            e.Handled = true;
            await _launcher.LaunchAsync(item);
        }
    }

    private void UpdateShortcutDetails(LaunchItem? item)
    {
        ShortcutDetailsPanel.Visibility = item is null ? Visibility.Collapsed : Visibility.Visible;
        if (item is null)
        {
            return;
        }

        try
        {
            if (item.IconFileName != null && item.FullIconPath != null && File.Exists(item.FullIconPath))
            {
                ShortcutDetailImage.Source = new BitmapImage(new Uri(item.FullIconPath));
                ShortcutDetailImage.Visibility = Visibility.Visible;
                ShortcutDetailIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShortcutDetailImage.Visibility = Visibility.Collapsed;
                ShortcutDetailIcon.Visibility = Visibility.Visible;
                ShortcutDetailIcon.Text = item.IconGlyph;
            }
        }
        catch
        {
            ShortcutDetailImage.Visibility = Visibility.Collapsed;
            ShortcutDetailIcon.Visibility = Visibility.Visible;
            ShortcutDetailIcon.Text = item.IconGlyph;
        }
        ShortcutDetailName.Text = item.Name;
        ShortcutDetailTarget.Text = item.DisplayTargetText;
        ShortcutDetailType.Text = item.TypeLabelText;
    }

    private async void DeleteClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
        {
            return;
        }

        var entry = _state.ClipboardHistory.FirstOrDefault(x => x.Id == id);
        if (entry is null)
        {
            return;
        }

        _state.ClipboardHistory.Remove(entry);
        _clipboard.Remove(entry);
        TryDeleteClipboardImage(entry);
        _sounds.Tap();
        await SaveAsync();
    }

    private async void PreviewClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
        {
            return;
        }

        var entry = _state.ClipboardHistory.FirstOrDefault(x => x.Id == id);
        if (entry is null)
        {
            return;
        }

        FrameworkElement content;
        if (entry.IsImage)
        {
            content = new Image
            {
                Source = new BitmapImage(new Uri(entry.Content)),
                MaxHeight = 520,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
            };
        }
        else
        {
            content = new TextBox
            {
                Text = entry.Content,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 240,
                MaxHeight = 520
            };
        }

        await ShowDialogAsync(entry.Kind == "image" ? _localizer["clipboard.imagePreview"] : _localizer["clipboard.textPreview"], content, _localizer["common.ok"]);
        _sounds.Tap();
    }

    private static void TryDeleteClipboardImage(ClipboardEntry entry)
    {
        try
        {
            if (entry.Kind == "image" && File.Exists(entry.Content))
            {
                File.Delete(entry.Content);
            }
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), ex.ToString());
        }
    }

    private void RefreshShortcuts()
    {
        ApplyLaunchItemLocalization();
        _shortcuts.Clear();
        foreach (var shortcut in _state.Shortcuts.OrderBy(x => x.Order))
        {
            _shortcuts.Add(shortcut);
        }
    }

    private async Task<bool> EditLaunchItemAsync(LaunchItem item)
    {
        if (item.Type == LaunchItemType.Url)
        {
            return await EditUrlLaunchItemAsync(item);
        }

        var nameBox = new TextBox { Header = _localizer["item.name"], Text = item.Name };
        var targetBox = new TextBox
        {
            Header = item.Type == LaunchItemType.Url ? _localizer["url.urls"] : _localizer["item.target"],
            Text = item.Target,
            AcceptsReturn = item.Type == LaunchItemType.Url,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = item.Type == LaunchItemType.Url ? 110 : 0
        };
        var argsBox = new TextBox { Header = _localizer["item.args"], Text = item.Arguments };
        var browserBox = new TextBox { Header = _localizer["url.browserOptional"], Text = item.BrowserExecutablePath ?? "" };
        var typeBox = new ComboBox { Header = _localizer["item.type"], MinWidth = 260 };

        foreach (var type in Enum.GetValues<LaunchItemType>())
        {
            typeBox.Items.Add(new ComboBoxItem { Content = type.ToString(), Tag = type });
        }

        typeBox.SelectedIndex = (int)item.Type;
        var iconBox = new ComboBox { Header = _localizer["icon.title"], MinWidth = 260 };
        foreach (var iconOption in _icons)
        {
            iconBox.Items.Add(new ComboBoxItem { Content = iconOption.Glyph, Tag = iconOption.Glyph });
        }

        iconBox.SelectedIndex = Math.Max(0, Array.FindIndex(_icons, x => x.Glyph == item.IconGlyph));

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox);
        panel.Children.Add(typeBox);
        panel.Children.Add(targetBox);
        panel.Children.Add(argsBox);
        panel.Children.Add(browserBox);
        panel.Children.Add(iconBox);

        var result = await ShowDialogAsync(_localizer["item.nameTitle"], panel, _localizer["common.save"], _localizer["common.cancel"]);
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        item.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? item.Name : nameBox.Text.Trim();
        item.Type = (LaunchItemType)(((ComboBoxItem)typeBox.SelectedItem).Tag ?? item.Type);
        item.Target = targetBox.Text.Trim();
        item.Arguments = argsBox.Text.Trim();
        item.BrowserExecutablePath = string.IsNullOrWhiteSpace(browserBox.Text) ? null : browserBox.Text.Trim();
        item.IconGlyph = ((ComboBoxItem)iconBox.SelectedItem).Tag?.ToString() ?? item.IconGlyph;
        return true;
    }

    private async Task<bool> EditUrlLaunchItemAsync(LaunchItem item)
    {
        var nameBox = new TextBox { Header = _localizer["item.name"], Text = item.Name };
        var urlPanel = new StackPanel { Spacing = 8 };
        foreach (var url in SplitTargetUrls(item.Target))
        {
            AddUrlBox(urlPanel, url);
        }

        if (urlPanel.Children.Count == 0)
        {
            AddUrlBox(urlPanel, "");
        }

        var addUrlButton = new Button { Content = _localizer["url.addUrl"] };
        addUrlButton.Click += (_, _) => AddUrlBox(urlPanel, "");

        var browserPicker = new ComboBox { Header = _localizer["url.browserPicker"], MinWidth = 330 };
        var browsers = await GetBrowserOptionsAsync();
        browserPicker.Items.Add(new ComboBoxItem { Content = _localizer["url.defaultBrowser"], Tag = "" });
        foreach (var browser in browsers)
        {
            browserPicker.Items.Add(new ComboBoxItem { Content = browser.ToString(), Tag = browser.Path });
        }

        browserPicker.SelectedIndex = 0;
        for (var i = 0; i < browserPicker.Items.Count; i++)
        {
            if ((browserPicker.Items[i] as ComboBoxItem)?.Tag?.ToString()?.Equals(item.BrowserExecutablePath ?? "", StringComparison.OrdinalIgnoreCase) == true)
            {
                browserPicker.SelectedIndex = i;
                break;
            }
        }

        var argsBox = new TextBox { Header = _localizer["item.args"], Text = item.Arguments };
        var iconBox = new ComboBox { Header = _localizer["icon.title"], MinWidth = 260 };
        foreach (var iconOption in _icons)
        {
            iconBox.Items.Add(new ComboBoxItem { Content = iconOption.Glyph, Tag = iconOption.Glyph });
        }

        iconBox.SelectedIndex = Math.Max(0, Array.FindIndex(_icons, x => x.Glyph == item.IconGlyph));

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = _localizer["url.urlsHelp"], TextWrapping = TextWrapping.WrapWholeWords, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CuteMutedBrush"] });
        panel.Children.Add(urlPanel);
        panel.Children.Add(addUrlButton);
        panel.Children.Add(browserPicker);
        panel.Children.Add(argsBox);
        panel.Children.Add(iconBox);

        var result = await ShowDialogAsync(_localizer["item.nameTitle"], panel, _localizer["common.save"], _localizer["common.cancel"]);
        var urls = ReadUrlBoxes(urlPanel);
        if (result != ContentDialogResult.Primary || urls.Count == 0)
        {
            return false;
        }

        item.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? item.Name : nameBox.Text.Trim();
        item.Target = string.Join(Environment.NewLine, urls);
        item.BrowserExecutablePath = ((ComboBoxItem)browserPicker.SelectedItem).Tag?.ToString();
        item.Arguments = argsBox.Text.Trim();
        item.IconGlyph = ((ComboBoxItem)iconBox.SelectedItem).Tag?.ToString() ?? item.IconGlyph;
        return true;
    }

    private static IEnumerable<string> SplitTargetUrls(string target)
    {
        return target
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private List<string> ReadUrlBoxes(StackPanel urlPanel)
    {
        return urlPanel.Children.OfType<Grid>()
            .SelectMany(x => x.Children.OfType<TextBox>())
            .Select(x => NormalizeUrl(x.Text))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void MoveItem(List<LaunchItem> items, LaunchItem item, int direction)
    {
        var ordered = items.OrderBy(x => x.Order).ToList();
        var index = ordered.IndexOf(item);
        var newIndex = Math.Clamp(index + direction, 0, ordered.Count - 1);
        if (index == newIndex)
        {
            return;
        }

        ordered.RemoveAt(index);
        ordered.Insert(newIndex, item);
        items.Clear();
        items.AddRange(ordered);
        Reindex(items);
    }

    private static void Reindex(List<LaunchItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
    }

    private void SelectionChanged_UpdateButtons(object sender, SelectionChangedEventArgs e) => UpdateActionButtons();

    private void UpdateActionButtons()
    {
        var hasModeItem = ModeItemsList.SelectedItem is not null;
        ModeActionsBar.Visibility = hasModeItem ? Visibility.Visible : Visibility.Collapsed;
        OpenModeItemButton.IsEnabled = hasModeItem;
        EditModeItemButton.IsEnabled = hasModeItem;
        MoveModeItemUpButton.IsEnabled = hasModeItem;
        MoveModeItemDownButton.IsEnabled = hasModeItem;
        DeleteModeItemButton.IsEnabled = hasModeItem;

        var hasShortcut = ShortcutsList.SelectedItem is not null;
        ShortcutActionsBar.Visibility = hasShortcut ? Visibility.Visible : Visibility.Collapsed;
        UpdateShortcutDetails(ShortcutsList.SelectedItem as LaunchItem);
        OpenShortcutButton.IsEnabled = hasShortcut;
        EditShortcutButton.IsEnabled = hasShortcut;
        MoveShortcutUpButton.IsEnabled = hasShortcut;
        MoveShortcutDownButton.IsEnabled = hasShortcut;
        DeleteShortcutButton.IsEnabled = hasShortcut;
    }

    private void SoundToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _state.Settings.PlayCuteSounds = SoundToggle.IsOn;
        _sounds.Enabled = SoundToggle.IsOn;
        _ = SaveAsync();
    }

    private async Task SaveAsync() => await _store.SaveAsync(_state);

    // ── Timer (countdown) ──
    private DispatcherTimer? _focusTimer;
    private int _focusSeconds;
    private bool _focusPaused;

    private void StartFocusButton_Click(object sender, RoutedEventArgs e)
    {
        if (_focusTimer != null) return;
        if (!int.TryParse(FocusMinutesBox.Text?.Trim(), out var minutes) || minutes <= 0) minutes = 25;
        _focusSeconds = minutes * 60;
        _focusPaused = false;
        UpdateFocusText();
        _focusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _focusTimer.Tick += FocusTimer_Tick;
        _focusTimer.Start();
        StartFocusButton.IsEnabled = false;
        PauseFocusButton.IsEnabled = true;
        ResetFocusButton.IsEnabled = true;
        FocusMinutesBox.IsEnabled = false;
        PauseFocusBtnText.Text = _localizer["focus.pause"];
        PauseFocusIcon.Text = "\uE769";
        _sounds.Tap();
    }

    private void PauseFocusButton_Click(object sender, RoutedEventArgs e)
    {
        if (_focusTimer is null) return;
        _focusPaused = !_focusPaused;
        PauseFocusBtnText.Text = _focusPaused ? _localizer["focus.resume"] : _localizer["focus.pause"];
        PauseFocusIcon.Text = _focusPaused ? "\uE768" : "\uE769";
        _sounds.Tap();
    }

    private void ResetFocusButton_Click(object sender, RoutedEventArgs e)
    {
        StopFocusTimer();
        _focusSeconds = 0;
        UpdateFocusText();
        _sounds.Tap();
    }

    private void FocusTimer_Tick(object? sender, object e)
    {
        if (_focusPaused) return;
        if (_focusSeconds > 0)
        {
            _focusSeconds--;
            UpdateFocusText();
        }
        else
        {
            StopFocusTimer();
            _sounds.Ding();
            StatusText.Text = _localizer["focus.finished"];
            TriggerHappyPet();
        }
    }

    private void StopFocusTimer()
    {
        _focusTimer?.Stop();
        _focusTimer = null;
        _focusPaused = false;
        StartFocusButton.IsEnabled = true;
        PauseFocusButton.IsEnabled = false;
        ResetFocusButton.IsEnabled = false;
        FocusMinutesBox.IsEnabled = true;
    }

    private void UpdateFocusText()
    {
        FocusTimeText.Text = $"{_focusSeconds / 60:D2}:{_focusSeconds % 60:D2}";
    }

    // ── Counter (stopwatch) ──
    private DispatcherTimer? _counterTimer;
    private int _counterSeconds;
    private bool _counterPaused;

    private void StartCounterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_counterTimer != null) return;
        _counterSeconds = 0;
        _counterPaused = false;
        UpdateCounterText();
        _counterTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _counterTimer.Tick += CounterTimer_Tick;
        _counterTimer.Start();
        StartCounterButton.IsEnabled = false;
        PauseCounterButton.IsEnabled = true;
        ResetCounterButton.IsEnabled = true;
        PauseCounterBtnText.Text = _localizer["focus.pause"];
        PauseCounterIcon.Text = "\uE769";
        _sounds.Tap();
    }

    private void PauseCounterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_counterTimer is null) return;
        _counterPaused = !_counterPaused;
        PauseCounterBtnText.Text = _counterPaused ? _localizer["focus.resume"] : _localizer["focus.pause"];
        PauseCounterIcon.Text = _counterPaused ? "\uE768" : "\uE769";
        _sounds.Tap();
    }

    private void ResetCounterButton_Click(object sender, RoutedEventArgs e)
    {
        StopCounterTimer();
        _counterSeconds = 0;
        UpdateCounterText();
        _sounds.Tap();
    }

    private void CounterTimer_Tick(object? sender, object e)
    {
        if (_counterPaused) return;
        _counterSeconds++;
        UpdateCounterText();
    }

    private void StopCounterTimer()
    {
        _counterTimer?.Stop();
        _counterTimer = null;
        _counterPaused = false;
        StartCounterButton.IsEnabled = true;
        PauseCounterButton.IsEnabled = false;
        ResetCounterButton.IsEnabled = false;
    }

    private void UpdateCounterText()
    {
        var h = _counterSeconds / 3600;
        var m = (_counterSeconds % 3600) / 60;
        var s = _counterSeconds % 60;
        CounterTimeText.Text = h > 0 ? $"{h:D2}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
    }

    private async Task<ContentDialogResult> ShowDialogAsync(string title, string message, string primary, string? secondary = null, string? close = null)
    {
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords };
        return await ShowDialogAsync(title, text, primary, secondary, close);
    }

    private async Task<ContentDialogResult> ShowDialogAsync(string title, UIElement content, string primary, string? secondary = null, string? close = null)
    {
        var wrappedContent = content is ScrollViewer
            ? content
            : new ScrollViewer
            {
                Content = content,
                MaxHeight = 560,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = ElementTheme.Light,
            Title = title,
            Content = wrappedContent,
            PrimaryButtonText = primary,
            SecondaryButtonText = secondary ?? "",
            CloseButtonText = close ?? "",
            DefaultButton = ContentDialogButton.Primary
        };

        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(MainWindow), ex.ToString());
            return ContentDialogResult.None;
        }
    }
}
