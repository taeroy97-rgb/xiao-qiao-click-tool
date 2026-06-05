using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace XiaoQiaoClickTool;

public partial class MainWindow : Window
{
    private const int HotkeyStartId = 601;
    private const int HotkeyPauseId = 602;
    private const int HotkeyStopId = 603;
    private const int WmHotkey = 0x0312;
    private const uint VkF6 = 0x75;
    private const uint VkF7 = 0x76;
    private const uint VkF8 = 0x77;

    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly Random _random = new();
    private CancellationTokenSource? _clickCts;
    private Task? _clickTask;
    private DateTime _startedAt;
    private DateTime? _pausedAt;
    private TimeSpan _pausedTotal = TimeSpan.Zero;
    private bool _isRunning;
    private bool _isPaused;
    private bool _uiReady;
    private bool _isPicking;
    private bool _isStopping;
    private bool _syncingRangeInputs;
    private int _clickedCount;
    private int _sendAttemptCount;
    private int _failedSendCount;
    private int _consecutiveFailCount;
    private Point _centerPoint = new(900, 500);
    private Point _lastClickPoint = new(900, 500);
    private RangeOverlayWindow? _overlay;
    private AppSettings _settings = AppSettings.Default();

    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XiaoQiaoClickTool");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");
    private static readonly string LogPath = Path.Combine(AppDataDir, "logs", "app.log");
    private static readonly string HistoryPath = Path.Combine(AppDataDir, "history.json");

    public MainWindow()
    {
        InitializeComponent();

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            CaptureSettingsFromUi();
            SaveSettings();
        };

        LoadSettings();
        ApplySettingsToUi();
        _uiReady = true;
        UpdateRangeUi();
        UpdateCoordinateText();
        SetPickPointState(PickPointState.Idle);
        SetStatus(AppStatus.Idle);
        UpdateButtons();
        UpdateProgress();
        _progressTimer.Start();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        Log("软件启动");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        await StartOrResumeAsync();
    }

    private async Task StartOrResumeAsync()
    {
        if (_isPaused)
        {
            ResumeClicking();
            return;
        }

        if (_isRunning)
        {
            return;
        }

        if (!TryValidateSettings(out var error))
        {
            SetStatus(AppStatus.Error, error);
            return;
        }

        CaptureSettingsFromUi();
        SaveSettings();
        ShowLongRunReminderIfNeeded();
        _clickCts = new CancellationTokenSource();
        _clickedCount = 0;
        _sendAttemptCount = 0;
        _failedSendCount = 0;
        _consecutiveFailCount = 0;
        _pausedTotal = TimeSpan.Zero;
        _pausedAt = null;
        _startedAt = DateTime.Now;
        _isRunning = true;
        _isPaused = false;
        SetStatus(AppStatus.Running);
        UpdateButtons();
        UpdateProgress();
        Log("开始点击");

        _clickTask = Task.Run(() => ClickLoopAsync(_clickCts.Token));
        try
        {
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log("开始失败: " + ex);
            SetStatus(AppStatus.Error, "启动失败");
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            ResumeClicking();
            return;
        }

        if (!_isRunning || _isPaused)
        {
            return;
        }

        _isPaused = true;
        _pausedAt = DateTime.Now;
        SetStatus(AppStatus.Paused);
        UpdateButtons();
        Log("暂停");
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        await StopClickingAsync("停止");
    }

    private async Task StopClickingAsync(string reason, bool completed = false, bool failed = false)
    {
        if (!_isRunning && !_isPaused)
        {
            SetStatus(AppStatus.Idle);
            UpdateProgress();
            return;
        }

        if (_isStopping)
        {
            return;
        }

        _isStopping = true;

        try
        {
            _clickCts?.Cancel();
            if (_clickTask is not null && !completed && !failed)
            {
                await Task.WhenAny(_clickTask, Task.Delay(1000));
            }
        }
        catch (Exception ex)
        {
            Log("停止异常: " + ex);
        }
        finally
        {
            var finalElapsed = GetElapsed();
            _isRunning = false;
            _isPaused = false;
            _isStopping = false;
            _pausedAt = null;
            _clickCts?.Dispose();
            _clickCts = null;
            _clickTask = null;
            AddHistory(reason, completed, failed, finalElapsed);
            SetStatus(completed ? AppStatus.Completed : failed ? AppStatus.Error : AppStatus.Idle, completed ? "已完成" : failed ? "异常" : "待机");
            UpdateButtons();
            UpdateProgress();
            Log(reason);

            if (completed)
            {
                TimeProgressBar.Value = 100;
                SystemSounds.Asterisk.Play();
                MessageBox.Show(this, $"任务已完成\n已点击 {_clickedCount} 次", "完成提示", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetRunState();
            }
            else if (failed)
            {
                SystemSounds.Hand.Play();
            }
            else
            {
                ResetRunState();
            }
        }
    }

    private void ResetRunState()
    {
        _clickedCount = 0;
        _sendAttemptCount = 0;
        _failedSendCount = 0;
        _consecutiveFailCount = 0;
        _pausedTotal = TimeSpan.Zero;
        _pausedAt = null;
        _startedAt = DateTime.Now;
        _isRunning = false;
        _isPaused = false;
        SetStatus(AppStatus.Idle);
        UpdateButtons();
        UpdateProgress();
        UpdateCoordinateText();
    }

    private void ResumeClicking()
    {
        if (!_isPaused)
        {
            return;
        }

        if (_pausedAt is not null)
        {
            _pausedTotal += DateTime.Now - _pausedAt.Value;
        }
        _pausedAt = null;
        _isPaused = false;
        SetStatus(AppStatus.Running);
        UpdateButtons();
        Log("继续");
    }

    private async Task ClickLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_isPaused)
                {
                    await Task.Delay(100, token);
                    continue;
                }

                var nextPoint = GetNextClickPoint();
                if (!await TrySendClickWithRetryAsync(nextPoint, token))
                {
                    continue;
                }

                _lastClickPoint = nextPoint;
                var count = Interlocked.Increment(ref _clickedCount);

                await Dispatcher.BeginInvoke(() =>
                {
                    UpdateCoordinateText();
                    UpdateProgress();
                });

                if (ShouldStop(count))
                {
                    await Dispatcher.BeginInvoke(async () => await StopClickingAsync("任务完成", completed: true));
                    return;
                }

                var delay = GetNextDelay();
                await DelayWithPauseAsync(delay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log("点击循环异常: " + ex);
                await Task.Delay(300, token);
            }
        }
    }

    private async Task<bool> TrySendClickWithRetryAsync(Point point, CancellationToken token)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _sendAttemptCount);
            if (NativeMethods.ClickAt((int)point.X, (int)point.Y))
            {
                Interlocked.Exchange(ref _consecutiveFailCount, 0);
                return true;
            }

            var failed = Interlocked.Increment(ref _failedSendCount);
            var consecutive = Interlocked.Increment(ref _consecutiveFailCount);
            Log($"点击发送失败，第 {attempt}/3 次，未计入成功点击；累计失败 {failed} 次，连续失败 {consecutive} 次");
            if (consecutive >= 30)
            {
                await Dispatcher.BeginInvoke(() => SetStatus(AppStatus.Error, $"连续失败 {consecutive} 次"));
            }
            if (attempt < 3)
            {
                await Task.Delay(200, token);
            }
        }

        Log("点击连续 3 次发送失败，跳过本轮，继续后续任务");
        await Dispatcher.BeginInvoke(UpdateProgress);
        return false;
    }

    private void ShowLongRunReminderIfNeeded()
    {
        if (_settings.StopMode != StopMode.Count || _settings.LimitCount < 1000)
        {
            return;
        }

        MessageBox.Show(this,
            "长时间点击任务提醒：\n\n请确认电脑不会休眠、锁屏或断开远程桌面。\n如果目标软件以管理员身份运行，本工具也需要管理员权限。\n任务过程中即使出现点击发送失败，软件也会记录日志并继续补够成功次数。",
            "防休眠提醒",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task DelayWithPauseAsync(TimeSpan delay, CancellationToken token)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero && !token.IsCancellationRequested)
        {
            if (_isPaused)
            {
                await Task.Delay(100, token);
                continue;
            }

            var step = remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100);
            await Task.Delay(step, token);
            remaining -= step;
        }
    }

    private bool ShouldStop(int count)
    {
        if (_settings.StopMode == StopMode.Count && count >= _settings.LimitCount)
        {
            return true;
        }

        if (_settings.StopMode == StopMode.Time)
        {
            var elapsed = GetElapsed();
            return elapsed >= TimeSpan.FromMinutes(_settings.LimitMinutes);
        }

        return false;
    }

    private TimeSpan GetNextDelay()
    {
        if (_settings.IntervalMode == IntervalMode.Fixed)
        {
            return TimeSpan.FromSeconds(Math.Max(0.05, _settings.FixedSeconds));
        }

        var values = _settings.RandomSeconds.Count == 0 ? [5.0] : _settings.RandomSeconds;
        return TimeSpan.FromSeconds(Math.Max(0.05, values[_random.Next(values.Count)]));
    }

    private Point GetNextClickPoint()
    {
        if (!_settings.RandomPointEnabled)
        {
            return _centerPoint;
        }

        if (_settings.RangeMode == RangeMode.Circle)
        {
            var radius = _settings.CircleRange;
            var angle = _random.NextDouble() * Math.PI * 2;
            var distance = Math.Sqrt(_random.NextDouble()) * radius;
            return new Point(_centerPoint.X + Math.Cos(angle) * distance, _centerPoint.Y + Math.Sin(angle) * distance);
        }

        var half = _settings.RectangleRange / 2.0;
        return new Point(
            _centerPoint.X + (_random.NextDouble() * 2 - 1) * half,
            _centerPoint.Y + (_random.NextDouble() * 2 - 1) * half);
    }

    private async void PickPoint_Click(object sender, RoutedEventArgs e)
    {
        if (_isPicking)
        {
            return;
        }

        _isPicking = true;
        CaptureSettingsFromUi();
        _overlay = new RangeOverlayWindow();
        _overlay.Show();
        SetStatus(AppStatus.Idle, "取点中");
        SetPickPointState(PickPointState.Picking);
        Log("取点开始");

        try
        {
            for (var i = 0; i < 30 && _isPicking; i++)
            {
                NativeMethods.GetCursorPos(out var p);
                _overlay.UpdatePreview(new Point(p.X, p.Y), _settings.RangeMode, GetActiveRangeSize(), _settings.RandomPointEnabled);
                CenterPointText.Text = $"取点中：鼠标 X {p.X} / Y {p.Y}";
                await Task.Delay(100);
            }

            if (_isPicking)
            {
                NativeMethods.GetCursorPos(out var p);
                _centerPoint = new Point(p.X, p.Y);
                _lastClickPoint = _centerPoint;
                UpdateCoordinateText();
                SetPickPointState(PickPointState.Picked);
                ScheduleSave();
                Log($"取点完成: {p.X},{p.Y}");
            }
        }
        finally
        {
            _overlay?.Close();
            _overlay = null;
            _isPicking = false;
            UpdateCoordinateText();
            SetStatus(AppStatus.Idle);
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isPicking)
        {
            _isPicking = false;
            _overlay?.Close();
            _overlay = null;
            UpdateCoordinateText();
            SetPickPointState(PickPointState.Cancelled);
            SetStatus(AppStatus.Idle);
            Log("取点取消");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AdvancedSettingsWindow(SettingsPath, Path.GetDirectoryName(LogPath) ?? AppDataDir, HistoryPath)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.ResetRequested)
        {
            _settings = AppSettings.Default();
            ApplySettingsToUi();
            UpdateRangeUi();
            UpdateCoordinateText();
            SaveSettings();
            UpdateProgress();
            Log("恢复默认");
        }
    }

    private void RangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady || _syncingRangeInputs)
        {
            return;
        }

        UpdateRangeUi();
        UpdateCoordinateText();
        ScheduleSave();
        if (_overlay is not null && NativeMethods.GetCursorPos(out var p))
        {
            CaptureSettingsFromUi();
            _overlay.UpdatePreview(new Point(p.X, p.Y), _settings.RangeMode, GetActiveRangeSize(), _settings.RandomPointEnabled);
        }
    }

    private void RangeMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        UpdateRangeUi();
        UpdateCoordinateText();
        ScheduleSave();
    }

    private void RangeValue_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady || _syncingRangeInputs || sender is not TextBox box)
        {
            return;
        }

        if (!int.TryParse(box.Text.Trim(), out var value))
        {
            return;
        }

        value = Math.Clamp(value, 10, 160);
        _syncingRangeInputs = true;
        if (ReferenceEquals(box, CircleRangeValue))
        {
            CircleRangeSlider.Value = value;
        }
        else if (ReferenceEquals(box, RectangleRangeValue))
        {
            RectangleRangeSlider.Value = value;
        }
        _syncingRangeInputs = false;

        UpdateRangeUi();
        UpdateCoordinateText();
        ScheduleSave();
    }

    private void RangeValue_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || e.Key is not (Key.Up or Key.Down))
        {
            return;
        }

        var current = int.TryParse(box.Text.Trim(), out var parsed)
            ? parsed
            : ReferenceEquals(box, CircleRangeValue)
                ? (int)Math.Round(CircleRangeSlider.Value)
                : (int)Math.Round(RectangleRangeSlider.Value);
        var next = Math.Clamp(current + (e.Key == Key.Up ? 1 : -1), 10, 160);

        _syncingRangeInputs = true;
        box.Text = next.ToString();
        if (ReferenceEquals(box, CircleRangeValue))
        {
            CircleRangeSlider.Value = next;
        }
        else if (ReferenceEquals(box, RectangleRangeValue))
        {
            RectangleRangeSlider.Value = next;
        }
        _syncingRangeInputs = false;

        box.SelectAll();
        UpdateRangeUi();
        UpdateCoordinateText();
        ScheduleSave();
        e.Handled = true;
    }

    private void CircleRangeRow_Click(object sender, MouseButtonEventArgs e)
    {
        CircleModeRadio.IsChecked = true;
        RectangleModeRadio.IsChecked = false;
    }

    private void RectangleRangeRow_Click(object sender, MouseButtonEventArgs e)
    {
        RectangleModeRadio.IsChecked = true;
        CircleModeRadio.IsChecked = false;
    }

    private void RandomPointToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        RangePanel.IsEnabled = RandomPointToggle.IsChecked == true;
        UpdateRangeUi();
        UpdateCoordinateText();
        ScheduleSave();
    }

    private void IntervalMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        if (ReferenceEquals(sender, FixedIntervalRadio))
        {
            RandomIntervalRadio.IsChecked = false;
        }
        else if (ReferenceEquals(sender, RandomIntervalRadio))
        {
            FixedIntervalRadio.IsChecked = false;
        }

        ScheduleSave();
    }

    private void StopMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        if (ReferenceEquals(sender, TimeStopRadio))
        {
            CountStopRadio.IsChecked = false;
            ManualStopRadio.IsChecked = false;
        }
        else if (ReferenceEquals(sender, CountStopRadio))
        {
            TimeStopRadio.IsChecked = false;
            ManualStopRadio.IsChecked = false;
        }
        else if (ReferenceEquals(sender, ManualStopRadio))
        {
            TimeStopRadio.IsChecked = false;
            CountStopRadio.IsChecked = false;
        }

        ScheduleSave();
        UpdateProgress();
    }

    private void Settings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }
        ScheduleSave();
        UpdateProgress();
    }

    private void UpdateRangeUi()
    {
        if (CircleRangeValue is null)
        {
            return;
        }

        var circle = (int)Math.Round(CircleRangeSlider.Value);
        var rectangle = (int)Math.Round(RectangleRangeSlider.Value);
        _syncingRangeInputs = true;
        CircleRangeValue.Text = circle.ToString();
        RectangleRangeValue.Text = rectangle.ToString();
        _syncingRangeInputs = false;
        var isCircle = CircleModeRadio.IsChecked == true;
        CircleRangeRow.BorderBrush = new SolidColorBrush(isCircle ? Color.FromRgb(90, 123, 255) : Color.FromRgb(193, 210, 255));
        CircleRangeRow.BorderThickness = new Thickness(isCircle ? 1.3 : 1);
        RectangleRangeRow.BorderBrush = new SolidColorBrush(!isCircle ? Color.FromRgb(90, 123, 255) : Color.FromRgb(193, 210, 255));
        RectangleRangeRow.BorderThickness = new Thickness(!isCircle ? 1.3 : 1);

        RangeDescription.Text = isCircle
            ? $"随机范围：半径 {circle} px，围绕中心点随机落点"
            : $"随机范围：矩形 {rectangle} × {rectangle} px，围绕中心点随机落点";

        UpdateFeedbackMonitors(circle, rectangle);
    }

    private void UpdateFeedbackMonitors(int circle, int rectangle)
    {
        var circleSize = Math.Clamp(circle / 160.0 * 36.0, 12.0, 36.0);
        CircleMonitorOuter.Width = circleSize;
        CircleMonitorOuter.Height = circleSize;
        Canvas.SetLeft(CircleMonitorOuter, 24 - circleSize / 2);
        Canvas.SetTop(CircleMonitorOuter, 24 - circleSize / 2);

        var circleSampleDistance = Math.Min(circleSize / 2 - 4, Math.Max(4, circleSize * 0.28));
        Canvas.SetLeft(CircleMonitorSample, 24 + circleSampleDistance - CircleMonitorSample.Width / 2);
        Canvas.SetTop(CircleMonitorSample, 24 - circleSampleDistance * 0.45 - CircleMonitorSample.Height / 2);

        var rectangleSize = Math.Clamp(rectangle / 160.0 * 34.0, 12.0, 34.0);
        RectangleMonitorOuter.Width = rectangleSize;
        RectangleMonitorOuter.Height = rectangleSize;
        Canvas.SetLeft(RectangleMonitorOuter, 24 - rectangleSize / 2);
        Canvas.SetTop(RectangleMonitorOuter, 24 - rectangleSize / 2);

        Canvas.SetLeft(RectangleMonitorSample, 24 + rectangleSize * 0.24 - RectangleMonitorSample.Width / 2);
        Canvas.SetTop(RectangleMonitorSample, 24 - rectangleSize * 0.28 - RectangleMonitorSample.Height / 2);
    }

    private void UpdateCoordinateText()
    {
        if (CenterPointText is null || FeedbackPointText is null)
        {
            return;
        }

        CenterPointText.Text = $"当前坐标：X {(int)_centerPoint.X} / Y {(int)_centerPoint.Y}";
        var mode = RectangleModeRadio.IsChecked == true ? "矩形" : "圆形";
        var enabled = RandomPointToggle.IsChecked == true;
        FeedbackPointText.Text = enabled
            ? $"反馈监控：中心点 {(int)_centerPoint.X}, {(int)_centerPoint.Y}；上次点击 {(int)_lastClickPoint.X}, {(int)_lastClickPoint.Y}；下一次会在{mode}范围内随机"
            : $"反馈监控：固定点击中心点 {(int)_centerPoint.X}, {(int)_centerPoint.Y}";
    }

    private void UpdateProgress()
    {
        var elapsed = _isRunning || _isPaused ? GetElapsed() : TimeSpan.Zero;
        var totalText = _settings.StopMode == StopMode.Time ? FormatTime(TimeSpan.FromMinutes(GetLimitMinutes())) : "--:--";
        var value = 0.0;
        if (_settings.StopMode == StopMode.Time)
        {
            var total = Math.Max(1, TimeSpan.FromMinutes(GetLimitMinutes()).TotalSeconds);
            value = Math.Clamp(elapsed.TotalSeconds / total * 100, 0, 100);
        }
        else if (_settings.StopMode == StopMode.Count)
        {
            value = Math.Clamp(_clickedCount / (double)Math.Max(1, GetLimitCount()) * 100, 0, 100);
            totalText = $"{GetLimitCount()} 次";
        }

        TimeProgressBar.Value = value;
        var failureRate = _sendAttemptCount == 0 ? 0 : _failedSendCount / (double)_sendAttemptCount;
        var qualityText = _sendAttemptCount == 0
            ? ""
            : $" / 失败 {_failedSendCount} 次 ({failureRate:P0})";
        if (_isRunning && _sendAttemptCount >= 20 && failureRate >= 0.1 && _consecutiveFailCount < 30)
        {
            SetStatus(AppStatus.Error, $"失败率 {failureRate:P0}");
        }
        ProgressText.Text = _settings.StopMode == StopMode.Count
            ? $"时间进度：已运行 {FormatTime(elapsed)} / 次数目标 {totalText} 成功 {_clickedCount} 次 / 发送 {_sendAttemptCount} 次{qualityText}"
            : $"时间进度：已运行 {FormatTime(elapsed)} / 总时长 {totalText} 成功 {_clickedCount} 次 / 发送 {_sendAttemptCount} 次{qualityText}";
    }

    private TimeSpan GetElapsed()
    {
        if (!_isRunning && !_isPaused)
        {
            return TimeSpan.Zero;
        }

        var now = _pausedAt ?? DateTime.Now;
        return now - _startedAt - _pausedTotal;
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private void SetStatus(AppStatus status, string? text = null)
    {
        var color = status switch
        {
            AppStatus.Running => Color.FromRgb(45, 194, 115),
            AppStatus.Error => Color.FromRgb(239, 79, 107),
            AppStatus.Completed => Color.FromRgb(40, 188, 116),
            AppStatus.Paused => Color.FromRgb(70, 130, 255),
            _ => Color.FromRgb(253, 186, 45)
        };
        StatusDot.Fill = new SolidColorBrush(color);
        StatusPill.Background = new SolidColorBrush(color);
        StatusText.Text = text ?? status switch
        {
            AppStatus.Running => "运行",
            AppStatus.Error => "异常",
            AppStatus.Completed => "已完成",
            AppStatus.Paused => "暂停",
            _ => "待机"
        };
    }

    private void SetPickPointState(PickPointState state)
    {
        PickPointButton.ApplyTemplate();
        if (PickPointButton.Template.FindName("PickPointCircle", PickPointButton) is not System.Windows.Shapes.Ellipse circle ||
            PickPointButton.Template.FindName("PickPointButtonText", PickPointButton) is not TextBlock text)
        {
            return;
        }

        var brushKey = state switch
        {
            PickPointState.Picking => "PickingBrush",
            PickPointState.Picked => "PickedBrush",
            PickPointState.Cancelled => "PickCancelBrush",
            _ => "PrimaryBrush"
        };

        circle.Fill = (Brush)FindResource(brushKey);
        text.Text = state switch
        {
            PickPointState.Picking => "取点中",
            PickPointState.Picked => "已选择",
            PickPointState.Cancelled => "已取消",
            _ => "选择点"
        };
    }

    private void UpdateButtons()
    {
        var canPauseOrStop = _isRunning || _isPaused;
        StartButton.Content = _isPaused ? "▶  继续" : _isRunning ? "▶  运行中" : "▶  开始";
        StartButton.IsEnabled = !_isRunning || _isPaused;
        PauseButton.Content = _isPaused ? "▶  继续" : "Ⅱ  暂停";
        PauseButton.IsEnabled = canPauseOrStop;
        PauseButton.Style = (Style)FindResource(canPauseOrStop ? "GradientButton" : "SoftButton");
        StopButton.Content = canPauseOrStop ? "▪  停止" : "已停止";
        StopButton.IsEnabled = canPauseOrStop;
    }

    private bool TryValidateSettings(out string error)
    {
        error = string.Empty;
        if (!double.TryParse(FixedSecondsBox.Text.Trim(), out var fixedSeconds) || fixedSeconds <= 0)
        {
            error = "固定时间必须是正数";
            return false;
        }

        if (!TryParseRandomSeconds(out _, out error))
        {
            return false;
        }

        if (!int.TryParse(LimitMinutesBox.Text.Trim(), out var minutes) || minutes <= 0)
        {
            error = "时间限制必须是正整数";
            return false;
        }

        if (!int.TryParse(LimitCountBox.Text.Trim(), out var count) || count <= 0)
        {
            error = "次数限制必须是正整数";
            return false;
        }

        return true;
    }

    private bool TryParseRandomSeconds(out List<double> values, out string error)
    {
        values = [];
        error = string.Empty;
        var parts = RandomSecondsBox.Text.Replace('，', ',').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 10)
        {
            error = "随机时间最多 10 个";
            return false;
        }

        foreach (var part in parts)
        {
            if (!double.TryParse(part, out var value) || value <= 0)
            {
                error = "随机时间只能包含正数";
                return false;
            }
            values.Add(value);
        }

        if (values.Count == 0)
        {
            error = "随机时间不能为空";
            return false;
        }

        return true;
    }

    private void CaptureSettingsFromUi()
    {
        TryParseRandomSeconds(out var randomValues, out _);
        _settings = new AppSettings
        {
            IntervalMode = RandomIntervalRadio.IsChecked == true ? IntervalMode.Random : IntervalMode.Fixed,
            FixedSeconds = double.TryParse(FixedSecondsBox.Text.Trim(), out var fixedSeconds) && fixedSeconds > 0 ? fixedSeconds : 5,
            RandomSeconds = randomValues.Count > 0 ? randomValues : [5, 6, 7, 8, 9, 10],
            StopMode = CountStopRadio.IsChecked == true ? StopMode.Count : ManualStopRadio.IsChecked == true ? StopMode.Manual : StopMode.Time,
            LimitMinutes = GetLimitMinutes(),
            LimitCount = GetLimitCount(),
            RandomPointEnabled = RandomPointToggle.IsChecked == true,
            CircleRange = (int)Math.Round(CircleRangeSlider.Value),
            RectangleRange = (int)Math.Round(RectangleRangeSlider.Value),
            RangeMode = RectangleModeRadio.IsChecked == true ? RangeMode.Rectangle : RangeMode.Circle,
            CenterX = (int)_centerPoint.X,
            CenterY = (int)_centerPoint.Y
        };
    }

    private void ApplySettingsToUi()
    {
        FixedIntervalRadio.IsChecked = _settings.IntervalMode == IntervalMode.Fixed;
        RandomIntervalRadio.IsChecked = _settings.IntervalMode == IntervalMode.Random;
        FixedSecondsBox.Text = _settings.FixedSeconds.ToString("0.###");
        RandomSecondsBox.Text = string.Join(",", _settings.RandomSeconds.Select(v => v.ToString("0.###")));
        TimeStopRadio.IsChecked = _settings.StopMode == StopMode.Time;
        CountStopRadio.IsChecked = _settings.StopMode == StopMode.Count;
        ManualStopRadio.IsChecked = _settings.StopMode == StopMode.Manual;
        LimitMinutesBox.Text = _settings.LimitMinutes.ToString();
        LimitCountBox.Text = _settings.LimitCount.ToString();
        RandomPointToggle.IsChecked = _settings.RandomPointEnabled;
        CircleRangeSlider.Value = _settings.CircleRange;
        RectangleRangeSlider.Value = _settings.RectangleRange;
        CircleModeRadio.IsChecked = _settings.RangeMode == RangeMode.Circle;
        RectangleModeRadio.IsChecked = _settings.RangeMode == RangeMode.Rectangle;
        RangePanel.IsEnabled = _settings.RandomPointEnabled;
        _centerPoint = new Point(_settings.CenterX, _settings.CenterY);
    }

    private int GetLimitMinutes()
    {
        return int.TryParse(LimitMinutesBox.Text.Trim(), out var minutes) && minutes > 0 ? minutes : 10;
    }

    private int GetLimitCount()
    {
        return int.TryParse(LimitCountBox.Text.Trim(), out var count) && count > 0 ? count : 100;
    }

    private int GetActiveRangeSize()
    {
        return _settings.RangeMode == RangeMode.Circle ? _settings.CircleRange : _settings.RectangleRange;
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (settings is not null)
                {
                    _settings = settings.Normalize();
                    Log("配置读取成功");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log("配置读取失败，使用默认值: " + ex.Message);
        }

        _settings = AppSettings.Default();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log("配置保存失败: " + ex.Message);
        }
    }

    private void AddHistory(string reason, bool completed, bool failed, TimeSpan duration)
    {
        if (_startedAt == default)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppDataDir);
            var records = LoadHistory();
            var range = !_settings.RandomPointEnabled
                ? "固定点"
                : _settings.RangeMode == RangeMode.Circle ? $"圆形 {_settings.CircleRange}px" : $"矩形 {_settings.RectangleRange}px";
            records.Insert(0, new RunHistoryRecord
            {
                FinishedAt = DateTime.Now,
                Reason = completed ? "完成" : failed ? "异常" : reason,
                Duration = FormatTime(duration),
                ClickCount = _clickedCount,
                AttemptCount = _sendAttemptCount,
                FailedCount = _failedSendCount,
                IntervalMode = _settings.IntervalMode == IntervalMode.Fixed ? "固定时间" : "随机时间",
                Range = range
            });
            records = records.Take(10).ToList();
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log("历史记录保存失败: " + ex.Message);
        }
    }

    public static List<RunHistoryRecord> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                return JsonSerializer.Deserialize<List<RunHistoryRecord>>(File.ReadAllText(HistoryPath)) ?? [];
            }
        }
        catch
        {
            return [];
        }

        return [];
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            Debug.WriteLine(message);
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        NativeMethods.RegisterHotKey(hwnd, HotkeyStartId, 0, VkF6);
        NativeMethods.RegisterHotKey(hwnd, HotkeyPauseId, 0, VkF7);
        NativeMethods.RegisterHotKey(hwnd, HotkeyStopId, 0, VkF8);
        Log("全局快捷键注册：F6 开始/继续，F7 暂停，F8 停止");
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.UnregisterHotKey(hwnd, HotkeyStartId);
        NativeMethods.UnregisterHotKey(hwnd, HotkeyPauseId);
        NativeMethods.UnregisterHotKey(hwnd, HotkeyStopId);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        var id = wParam.ToInt32();
        if (id == HotkeyStartId)
        {
            _ = StartOrResumeAsync();
        }
        else if (id == HotkeyPauseId)
        {
            Pause_Click(this, new RoutedEventArgs());
        }
        else if (id == HotkeyStopId)
        {
            _ = StopClickingAsync("快捷键停止");
        }

        return IntPtr.Zero;
    }
}

public enum AppStatus
{
    Idle,
    Running,
    Paused,
    Completed,
    Error
}

public enum PickPointState
{
    Idle,
    Picking,
    Picked,
    Cancelled
}

public sealed class RunHistoryRecord
{
    public DateTime FinishedAt { get; set; }
    public string Reason { get; set; } = "";
    public string Duration { get; set; } = "";
    public int ClickCount { get; set; }
    public int AttemptCount { get; set; }
    public int FailedCount { get; set; }
    public string IntervalMode { get; set; } = "";
    public string Range { get; set; } = "";

    public override string ToString()
    {
        return $"{FinishedAt:MM-dd HH:mm}  {Reason}  {Duration}  成功{ClickCount}次/发送{AttemptCount}次/失败{FailedCount}次  {IntervalMode}  {Range}";
    }
}

public enum IntervalMode
{
    Fixed,
    Random
}

public enum StopMode
{
    Time,
    Count,
    Manual
}

public enum RangeMode
{
    Circle,
    Rectangle
}

public sealed class AppSettings
{
    public IntervalMode IntervalMode { get; set; }
    public double FixedSeconds { get; set; }
    public List<double> RandomSeconds { get; set; } = [];
    public StopMode StopMode { get; set; }
    public int LimitMinutes { get; set; }
    public int LimitCount { get; set; }
    public bool RandomPointEnabled { get; set; }
    public int CircleRange { get; set; }
    public int RectangleRange { get; set; }
    public RangeMode RangeMode { get; set; }
    public int CenterX { get; set; }
    public int CenterY { get; set; }

    public static AppSettings Default() => new()
    {
        IntervalMode = IntervalMode.Fixed,
        FixedSeconds = 5,
        RandomSeconds = [5, 6, 7, 8, 9, 10],
        StopMode = StopMode.Time,
        LimitMinutes = 10,
        LimitCount = 100,
        RandomPointEnabled = true,
        CircleRange = 17,
        RectangleRange = 17,
        RangeMode = RangeMode.Circle,
        CenterX = 900,
        CenterY = 500
    };

    public AppSettings Normalize()
    {
        if (FixedSeconds <= 0) FixedSeconds = 5;
        if (RandomSeconds.Count == 0 || RandomSeconds.Count > 10 || RandomSeconds.Any(v => v <= 0)) RandomSeconds = [5, 6, 7, 8, 9, 10];
        if (LimitMinutes <= 0) LimitMinutes = 10;
        if (LimitCount <= 0) LimitCount = 100;
        if (CircleRange is 20 or 72) CircleRange = 17;
        if (RectangleRange is 20 or 63) RectangleRange = 17;
        CircleRange = Math.Clamp(CircleRange, 10, 160);
        RectangleRange = Math.Clamp(RectangleRange, 10, 160);
        if (CenterX <= 0) CenterX = 900;
        if (CenterY <= 0) CenterY = 500;
        return this;
    }
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal static bool ClickAt(int x, int y)
    {
        if (!SetCursorPos(x, y))
        {
            return false;
        }

        var inputs = new[]
        {
            new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } },
            new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } }
        };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
