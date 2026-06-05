using System.Diagnostics;
using System.IO;
using System.Windows;

namespace XiaoQiaoClickTool;

public partial class AdvancedSettingsWindow : Window
{
    private readonly string _logDirectory;
    private readonly string _historyPath;

    public bool ResetRequested { get; private set; }

    public AdvancedSettingsWindow(string settingsPath, string logDirectory, string historyPath)
    {
        InitializeComponent();
        _logDirectory = logDirectory;
        _historyPath = historyPath;
        SettingsPathBox.Text = settingsPath;
        LogPathBox.Text = logDirectory;
        LoadHistory();
    }

    private void LoadHistory()
    {
        HistoryList.Items.Clear();
        var records = MainWindow.LoadHistory();
        if (records.Count == 0)
        {
            HistoryList.Items.Add("暂无记录");
            return;
        }

        foreach (var record in records)
        {
            HistoryList.Items.Add(record.ToString());
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_logDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _logDirectory,
            UseShellExecute = true
        });
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ResetRequested = true;
        DialogResult = true;
        Close();
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
