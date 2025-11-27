using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PatrolApp.Models;
using PatrolApp.Services;
using System.Collections.ObjectModel;

namespace PatrolApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly NfcService _nfcService;
    private readonly ApiService _apiService;
    private readonly TextToSpeechService _ttsService;

    [ObservableProperty]
    private string _currentLocation = "等待读卡...";

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    [ObservableProperty]
    private string _lastNfcId = "";

    [ObservableProperty]
    private bool _isNfcEnabled = false;

    [ObservableProperty]
    private int _unsyncedCount = 0;

    public ObservableCollection<PatrolRecord> Records { get; } = new();

    public MainViewModel(
        DatabaseService databaseService,
        NfcService nfcService,
        ApiService apiService,
        TextToSpeechService ttsService)
    {
        _databaseService = databaseService;
        _nfcService = nfcService;
        _apiService = apiService;
        _ttsService = ttsService;

        _nfcService.TagRead += OnNfcTagRead;
        
        // 启动定时器更新时间
        var timer = Application.Current?.Dispatcher.CreateTimer();
        if (timer != null)
        {
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            timer.Start();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsNfcEnabled = true;
            _nfcService.StartListening();
            _nfcService.EnableBackgroundMode();

            await LoadRecordsAsync();
            await CheckAndSyncRecordsAsync();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("错误", $"初始化失败: {ex.Message}", "确定");
        }
    }

    private async Task LoadRecordsAsync()
    {
        try
        {
            var records = await _databaseService.GetRecordsAsync();
            Records.Clear();
            foreach (var record in records.Take(20)) // 只显示最近20条
            {
                Records.Add(record);
            }

            var unsynced = await _databaseService.GetUnsyncedRecordsAsync();
            UnsyncedCount = unsynced.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载记录失败: {ex.Message}");
        }
    }

    private async void OnNfcTagRead(object? sender, NfcTagReadEventArgs e)
    {
        try
        {
            LastNfcId = e.TagId;
            CurrentLocation = e.Location ?? $"位置 {e.TagId.Substring(0, Math.Min(8, e.TagId.Length))}";

            // 保存到数据库
            var record = new PatrolRecord
            {
                Location = CurrentLocation,
                NfcId = e.TagId,
                CheckInTime = e.ReadTime,
                IsSynced = false
            };

            await _databaseService.SaveRecordAsync(record);

            // 语音播报 - 增加"巡更成功"
            var announcement = $"{CurrentLocation}, {e.ReadTime:HH点mm分}, 巡更成功";
            await _ttsService.SpeakAsync(announcement);

            // 刷新列表
            await LoadRecordsAsync();

            // 尝试上传
            if (_apiService.IsNetworkAvailable())
            {
                var uploaded = await _apiService.UploadRecordAsync(record);
                if (uploaded)
                {
                    await _databaseService.MarkAsSyncedAsync(record.Id);
                    await LoadRecordsAsync();
                }
            }

            // 已取消弹窗提示,改为静默打卡
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"处理NFC标签失败: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current!.MainPage!.DisplayAlert("错误", $"打卡失败: {ex.Message}", "确定");
            });
        }
    }

    [RelayCommand]
    private async Task SyncRecordsAsync()
    {
        try
        {
            if (!_apiService.IsNetworkAvailable())
            {
                await Application.Current!.MainPage!.DisplayAlert("提示", "无网络连接", "确定");
                return;
            }

            var unsyncedRecords = await _databaseService.GetUnsyncedRecordsAsync();
            if (unsyncedRecords.Count == 0)
            {
                await Application.Current!.MainPage!.DisplayAlert("提示", "没有待同步的记录", "确定");
                return;
            }

            var uploaded = await _apiService.UploadRecordsAsync(unsyncedRecords);
            if (uploaded)
            {
                foreach (var record in unsyncedRecords)
                {
                    await _databaseService.MarkAsSyncedAsync(record.Id);
                }

                await LoadRecordsAsync();
                await Application.Current!.MainPage!.DisplayAlert("成功", $"已同步 {unsyncedRecords.Count} 条记录", "确定");
            }
            else
            {
                await Application.Current!.MainPage!.DisplayAlert("失败", "同步失败,请稍后重试", "确定");
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("错误", $"同步失败: {ex.Message}", "确定");
        }
    }

    private async Task CheckAndSyncRecordsAsync()
    {
        // 后台定期检查并同步
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                try
                {
                    if (_apiService.IsNetworkAvailable())
                    {
                        var unsyncedRecords = await _databaseService.GetUnsyncedRecordsAsync();
                        if (unsyncedRecords.Count > 0)
                        {
                            var uploaded = await _apiService.UploadRecordsAsync(unsyncedRecords);
                            if (uploaded)
                            {
                                foreach (var record in unsyncedRecords)
                                {
                                    await _databaseService.MarkAsSyncedAsync(record.Id);
                                }

                                await MainThread.InvokeOnMainThreadAsync(async () =>
                                {
                                    await LoadRecordsAsync();
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"自动同步失败: {ex.Message}");
                }
            }
        });
    }
}
