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
            var cardNo = e.TagId;

            // 检查网络并调用 API
            if (_apiService.IsNetworkAvailable())
            {
                await ProcessCardWithApiAsync(cardNo, e.ReadTime);
            }
            else
            {
                // 无网络，使用离线模式
                await ProcessCardOfflineAsync(cardNo, e.ReadTime);
            }
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

    /// <summary>
    /// 有网络时通过 API 处理卡
    /// </summary>
    private async Task ProcessCardWithApiAsync(string cardNo, DateTime readTime)
    {
        try
        {
            // 1. 调用 get_card API 获取巡更点信息
            var patrolPoint = await _apiService.GetCardAsync(cardNo);

            if (patrolPoint == null || string.IsNullOrEmpty(patrolPoint.LocationName))
            {
                // 卡不存在，弹窗让用户输入巡更点位置
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await HandleNewCardAsync(cardNo, readTime);
                });
            }
            else
            {
                // 卡存在，执行打卡
                await HandleExistingCardAsync(cardNo, patrolPoint.LocationName, readTime);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API处理失败: {ex.Message}");
            // API 失败时使用离线模式
            await ProcessCardOfflineAsync(cardNo, readTime);
        }
    }

    /// <summary>
    /// 处理新卡（卡不存在）- 弹窗输入位置
    /// </summary>
    private async Task HandleNewCardAsync(string cardNo, DateTime readTime)
    {
        try
        {
            // 语音提示需要添加新巡更点
            await _ttsService.SpeakAsync("该卡未登记，请输入巡更点位置");
            
            // 弹窗提示输入巡更点位置
            string? locationName = await Application.Current!.MainPage!.DisplayPromptAsync(
                "新巡更点",
                "请输入巡更点位置:",
                "确定",
                "取消",
                "例如: 义乌店0101",
                maxLength: 100,
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(locationName))
            {
                await _ttsService.SpeakAsync("已取消添加巡更点");
                return;
            }

            CurrentLocation = locationName;

            // 调用 insert_address API 添加巡更点
            var errorMsg = await _apiService.InsertAddressAsync(cardNo, locationName);

            if (errorMsg == null)
            {
                // 语音提示添加成功
                await _ttsService.SpeakAsync($"添加{locationName}巡更点成功，请重新打卡");

                // 保存到本地数据库（标记为已同步，因为是新添加的点）
                var record = new PatrolRecord
                {
                    Location = $"[新增] {locationName}",
                    NfcId = cardNo,
                    CheckInTime = readTime,
                    IsSynced = true
                };
                await _databaseService.SaveRecordAsync(record);
                await LoadRecordsAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[HandleNewCard] 添加巡更点失败: {errorMsg}");
                await _ttsService.SpeakAsync($"添加巡更点失败，{errorMsg}");
                
                // 显示详细错误
                await Application.Current!.MainPage!.DisplayAlert("添加失败", errorMsg, "确定");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"添加新巡更点失败: {ex.Message}");
            await _ttsService.SpeakAsync("添加巡更点失败");
        }
    }

    /// <summary>
    /// 处理已存在的卡 - 打卡
    /// </summary>
    private async Task HandleExistingCardAsync(string cardNo, string locationName, DateTime readTime)
    {
        try
        {
            CurrentLocation = locationName;

            // 保存到本地数据库
            var record = new PatrolRecord
            {
                Location = locationName,
                NfcId = cardNo,
                CheckInTime = readTime,
                IsSynced = false
            };
            await _databaseService.SaveRecordAsync(record);

            // 语音播报打卡成功
            await _ttsService.SpeakAsync($"{locationName}打卡成功");

            // 刷新列表
            await LoadRecordsAsync();

            // 调用 insert_patrol API 插入巡更记录
            var success = await _apiService.InsertPatrolAsync(cardNo, locationName);
            if (success)
            {
                await _databaseService.MarkAsSyncedAsync(record.Id);
                await LoadRecordsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打卡处理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 无网络时离线处理
    /// </summary>
    private async Task ProcessCardOfflineAsync(string cardNo, DateTime readTime)
    {
        try
        {
            // 离线模式：使用卡号前8位作为临时位置标识
            var tempLocation = $"离线-{cardNo.Substring(0, Math.Min(8, cardNo.Length))}";
            CurrentLocation = tempLocation;

            // 保存到本地数据库，标记为未同步
            var record = new PatrolRecord
            {
                Location = tempLocation,
                NfcId = cardNo,
                CheckInTime = readTime,
                IsSynced = false
            };
            await _databaseService.SaveRecordAsync(record);

            // 语音提示
            await _ttsService.SpeakAsync($"离线打卡成功，待网络恢复后自动同步");

            // 刷新列表
            await LoadRecordsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"离线处理失败: {ex.Message}");
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

            int successCount = 0;
            foreach (var record in unsyncedRecords)
            {
                // 对于离线记录，先查询卡信息
                if (record.Location.StartsWith("离线-"))
                {
                    var patrolPoint = await _apiService.GetCardAsync(record.NfcId);
                    if (patrolPoint != null && !string.IsNullOrEmpty(patrolPoint.LocationName))
                    {
                        // 更新位置名称
                        record.Location = patrolPoint.LocationName;
                        await _databaseService.SaveRecordAsync(record);
                        
                        // 插入巡更记录
                        var success = await _apiService.InsertPatrolAsync(record.NfcId, patrolPoint.LocationName);
                        if (success)
                        {
                            await _databaseService.MarkAsSyncedAsync(record.Id);
                            successCount++;
                        }
                    }
                }
                else
                {
                    // 正常记录，直接上传
                    var success = await _apiService.InsertPatrolAsync(record.NfcId, record.Location);
                    if (success)
                    {
                        await _databaseService.MarkAsSyncedAsync(record.Id);
                        successCount++;
                    }
                }
            }

            await LoadRecordsAsync();
            await Application.Current!.MainPage!.DisplayAlert("完成", $"已同步 {successCount}/{unsyncedRecords.Count} 条记录", "确定");
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
                            foreach (var record in unsyncedRecords)
                            {
                                if (record.Location.StartsWith("离线-"))
                                {
                                    var patrolPoint = await _apiService.GetCardAsync(record.NfcId);
                                    if (patrolPoint != null && !string.IsNullOrEmpty(patrolPoint.LocationName))
                                    {
                                        record.Location = patrolPoint.LocationName;
                                        await _databaseService.SaveRecordAsync(record);
                                        
                                        var success = await _apiService.InsertPatrolAsync(record.NfcId, patrolPoint.LocationName);
                                        if (success)
                                        {
                                            await _databaseService.MarkAsSyncedAsync(record.Id);
                                        }
                                    }
                                }
                                else
                                {
                                    var success = await _apiService.InsertPatrolAsync(record.NfcId, record.Location);
                                    if (success)
                                    {
                                        await _databaseService.MarkAsSyncedAsync(record.Id);
                                    }
                                }
                            }

                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await LoadRecordsAsync();
                            });
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
