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
    private readonly UpdateService _updateService;

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
    
    [ObservableProperty]
    private bool _isUpdating = false;
    
    [ObservableProperty]
    private double _updateProgress = 0;

    public ObservableCollection<PatrolRecord> Records { get; } = new();

    public MainViewModel(
        DatabaseService databaseService,
        NfcService nfcService,
        ApiService apiService,
        TextToSpeechService ttsService,
        UpdateService updateService)
    {
        _databaseService = databaseService;
        _nfcService = nfcService;
        _apiService = apiService;
        _ttsService = ttsService;
        _updateService = updateService;

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
            
            // 启动时有网络则自动缓存所有卡点
            await CacheAllCardPointsAsync();
            
            // 启动时检查并上传未同步记录
            await UploadUnsyncedRecordsOnStartupAsync();
            
            // 启动后台定期同步
            StartBackgroundSync();
            
            // 检查应用更新
            await CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("错误", $"初始化失败: {ex.Message}", "确定");
        }
    }
    
    /// <summary>
    /// 检查应用更新
    /// </summary>
    private async Task CheckForUpdateAsync()
    {
        try
        {
            var updateInfo = await _updateService.CheckForUpdateAsync();
            if (updateInfo != null)
            {
                var result = await Application.Current!.MainPage!.DisplayAlert(
                    "发现新版本",
                    $"版本: {updateInfo.Version}\n{updateInfo.Message}\n\n是否立即升级?",
                    "升级",
                    "稍后");

                if (result)
                {
                    await DownloadAndInstallUpdateAsync(updateInfo);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"检查更新失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 下载并安装更新
    /// </summary>
    private async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
    {
        try
        {
            // 检查安装权限
            if (!_updateService.CanInstallUnknownApps())
            {
                var grantPermission = await Application.Current!.MainPage!.DisplayAlert(
                    "需要权限",
                    "安装更新需要授予「安装未知应用」权限，是否前往设置?",
                    "去设置",
                    "取消");

                if (grantPermission)
                {
                    _updateService.RequestInstallPermission();
                }
                return;
            }

            IsUpdating = true;
            UpdateProgress = 0;
            
            await _ttsService.SpeakAsync("开始下载更新");

            var progress = new Progress<double>(p =>
            {
                UpdateProgress = p;
            });

            var apkPath = await _updateService.DownloadApkAsync(updateInfo.Url, progress);

            if (!string.IsNullOrEmpty(apkPath))
            {
                await _ttsService.SpeakAsync("下载完成，正在安装");
                _updateService.InstallApk(apkPath);
            }
            else
            {
                await _ttsService.SpeakAsync("下载失败");
                await Application.Current!.MainPage!.DisplayAlert("错误", "下载更新失败，请检查网络连接", "确定");
            }
        }
        catch (Exception ex)
        {
            await _ttsService.SpeakAsync("更新失败");
            await Application.Current!.MainPage!.DisplayAlert("错误", $"更新失败: {ex.Message}", "确定");
        }
        finally
        {
            IsUpdating = false;
            UpdateProgress = 0;
        }
    }
    
    /// <summary>
    /// 启动时缓存所有卡点到本地SQLite
    /// </summary>
    private async Task CacheAllCardPointsAsync()
    {
        try
        {
            // 检查是否有网络
            if (!_apiService.IsNetworkAvailable())
            {
                System.Diagnostics.Debug.WriteLine("无网络，跳过卡点缓存");
                return;
            }
            
            // 获取所有卡点
            var allCards = await _apiService.GetAllCardsAsync();
            if (allCards.Count > 0)
            {
                // 转换并保存到本地数据库
                var cardPoints = allCards.Select(c => new CardPoint
                {
                    CardNo = c.CardNo,
                    LocationName = c.LocationName,
                    Type = c.Type
                }).ToList();
                
                await _databaseService.SaveCardPointsAsync(cardPoints);
                System.Diagnostics.Debug.WriteLine($"已缓存 {cardPoints.Count} 个卡点");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"缓存卡点失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 启动时自动上传未同步记录
    /// </summary>
    private async Task UploadUnsyncedRecordsOnStartupAsync()
    {
        try
        {
            // 检查是否有网络
            if (!_apiService.IsNetworkAvailable())
            {
                var unsyncedRecords = await _databaseService.GetUnsyncedRecordsAsync();
                if (unsyncedRecords.Count > 0)
                {
                    await _ttsService.SpeakAsync($"有{unsyncedRecords.Count}条打卡记录待上传，请连接网络");
                }
                return;
            }
            
            var records = await _databaseService.GetUnsyncedRecordsAsync();
            if (records.Count == 0)
            {
                return;
            }
            
            int successCount = 0;
            foreach (var record in records)
            {
                try
                {
                    // 对于离线记录，先查询卡信息获取真实位置名
                    string locationName = record.Location;
                    if (record.Location.StartsWith("离线-"))
                    {
                        var patrolPoint = await _apiService.GetCardAsync(record.NfcId);
                        if (patrolPoint != null && !string.IsNullOrEmpty(patrolPoint.LocationName))
                        {
                            locationName = patrolPoint.LocationName;
                            record.Location = locationName;
                            await _databaseService.SaveRecordAsync(record);
                        }
                        else
                        {
                            // 卡未登记，跳过这条记录
                            continue;
                        }
                    }
                    
                    // 上传打卡记录
                    var success = await _apiService.InsertPatrolAsync(record.NfcId, locationName);
                    if (success)
                    {
                        await _databaseService.MarkAsSyncedAsync(record.Id);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"上传记录失败: {ex.Message}");
                }
            }
            
            // 刷新列表
            await LoadRecordsAsync();
            
            // 语音提示上传结果
            if (successCount > 0)
            {
                await _ttsService.SpeakAsync($"上传了{successCount}条打卡记录");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动时上传失败: {ex.Message}");
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
            // 检查15分钟内是否已打过卡
            var recentRecord = await _databaseService.GetRecentCheckInAsync(cardNo, 15);
            if (recentRecord != null)
            {
                var lastCheckInTime = recentRecord.CheckInTime.ToString("HH:mm");
                CurrentLocation = $"{locationName} (已打卡)";
                
                // 语音提示已打过卡
                await _ttsService.SpeakAsync($"{locationName}在{lastCheckInTime}已打卡，无需再打卡");
                return;
            }
            
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
            // 检查15分钟内是否已打过卡
            var recentRecord = await _databaseService.GetRecentCheckInAsync(cardNo, 15);
            if (recentRecord != null)
            {
                var lastCheckInTime = recentRecord.CheckInTime.ToString("HH:mm");
                var displayLocation = recentRecord.Location ?? "此卡点";
                CurrentLocation = $"{displayLocation} (已打卡)";
                
                // 语音提示已打过卡
                await _ttsService.SpeakAsync($"{displayLocation}在{lastCheckInTime}已打卡，无需再打卡");
                return;
            }
            
            // 优先从缓存的卡点信息中获取位置名
            var cachedCardPoint = await _databaseService.GetCardPointAsync(cardNo);
            string locationName;
            
            if (cachedCardPoint != null && !string.IsNullOrEmpty(cachedCardPoint.LocationName))
            {
                // 使用缓存的卡点信息
                locationName = cachedCardPoint.LocationName;
            }
            else
            {
                // 没有缓存，从历史打卡记录中查找
                var existingRecords = await _databaseService.GetRecordsAsync();
                var existingRecord = existingRecords.FirstOrDefault(r => r.NfcId == cardNo && !r.Location!.StartsWith("离线-"));
                
                if (existingRecord != null)
                {
                    // 使用历史记录中的位置名
                    locationName = existingRecord.Location!;
                }
                else
                {
                    // 都没有，使用卡号作为临时标识
                    locationName = $"离线-{cardNo.Substring(0, Math.Min(8, cardNo.Length))}";
                }
            }
            
            CurrentLocation = locationName;

            // 保存到本地数据库，标记为未同步
            var record = new PatrolRecord
            {
                Location = locationName,
                NfcId = cardNo,
                CheckInTime = readTime,
                IsSynced = false
            };
            await _databaseService.SaveRecordAsync(record);

            // 语音提示：打卡成功（如果有卡点名称则播报位置名）
            if (locationName.StartsWith("离线-"))
            {
                await _ttsService.SpeakAsync("打卡成功，无网暂未上传");
            }
            else
            {
                await _ttsService.SpeakAsync($"{locationName}打卡成功，无网暂未上传");
            }

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
        // 这个方法已被 StartBackgroundSync 替代
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 启动后台定期同步
    /// </summary>
    private void StartBackgroundSync()
    {
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
                            int successCount = 0;
                            foreach (var record in unsyncedRecords)
                            {
                                try
                                {
                                    string locationName = record.Location;
                                    if (record.Location.StartsWith("离线-"))
                                    {
                                        var patrolPoint = await _apiService.GetCardAsync(record.NfcId);
                                        if (patrolPoint != null && !string.IsNullOrEmpty(patrolPoint.LocationName))
                                        {
                                            locationName = patrolPoint.LocationName;
                                            record.Location = locationName;
                                            await _databaseService.SaveRecordAsync(record);
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                    
                                    var success = await _apiService.InsertPatrolAsync(record.NfcId, locationName);
                                    if (success)
                                    {
                                        await _databaseService.MarkAsSyncedAsync(record.Id);
                                        successCount++;
                                    }
                                }
                                catch { }
                            }

                            if (successCount > 0)
                            {
                                await MainThread.InvokeOnMainThreadAsync(async () =>
                                {
                                    await LoadRecordsAsync();
                                    await _ttsService.SpeakAsync($"后台上传了{successCount}条打卡记录");
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"后台同步失败: {ex.Message}");
                }
            }
        });
    }
}
