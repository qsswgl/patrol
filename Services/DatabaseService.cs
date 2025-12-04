using SQLite;
using PatrolApp.Models;

namespace PatrolApp.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public DatabaseService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "patrol.db3");
    }

    private async Task InitAsync()
    {
        if (_database != null)
            return;

        _database = new SQLiteAsyncConnection(_dbPath);
        await _database.CreateTableAsync<PatrolRecord>();
        await _database.CreateTableAsync<CardPoint>();
    }

    public async Task<List<PatrolRecord>> GetRecordsAsync()
    {
        await InitAsync();
        return await _database!.Table<PatrolRecord>()
            .OrderByDescending(r => r.CheckInTime)
            .ToListAsync();
    }

    public async Task<List<PatrolRecord>> GetUnsyncedRecordsAsync()
    {
        await InitAsync();
        return await _database!.Table<PatrolRecord>()
            .Where(r => !r.IsSynced)
            .ToListAsync();
    }

    public async Task<int> SaveRecordAsync(PatrolRecord record)
    {
        await InitAsync();
        
        if (record.Id != 0)
        {
            return await _database!.UpdateAsync(record);
        }
        else
        {
            return await _database!.InsertAsync(record);
        }
    }

    public async Task<int> DeleteRecordAsync(PatrolRecord record)
    {
        await InitAsync();
        return await _database!.DeleteAsync(record);
    }

    public async Task MarkAsSyncedAsync(int recordId)
    {
        await InitAsync();
        var record = await _database!.Table<PatrolRecord>()
            .Where(r => r.Id == recordId)
            .FirstOrDefaultAsync();
        
        if (record != null)
        {
            record.IsSynced = true;
            record.SyncedTime = DateTime.Now;
            await _database.UpdateAsync(record);
        }
    }

    #region 卡点缓存相关方法

    /// <summary>
    /// 保存或更新卡点信息（相同卡号不重复保存）
    /// </summary>
    public async Task SaveCardPointAsync(CardPoint cardPoint)
    {
        await InitAsync();
        var existing = await _database!.Table<CardPoint>()
            .Where(c => c.CardNo == cardPoint.CardNo)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            // 已存在，更新信息
            existing.LocationName = cardPoint.LocationName;
            existing.Type = cardPoint.Type;
            existing.UpdatedTime = DateTime.Now;
            await _database.UpdateAsync(existing);
        }
        else
        {
            // 不存在，插入新记录
            cardPoint.UpdatedTime = DateTime.Now;
            await _database.InsertAsync(cardPoint);
        }
    }

    /// <summary>
    /// 批量保存卡点信息
    /// </summary>
    public async Task SaveCardPointsAsync(List<CardPoint> cardPoints)
    {
        await InitAsync();
        foreach (var cardPoint in cardPoints)
        {
            await SaveCardPointAsync(cardPoint);
        }
    }

    /// <summary>
    /// 根据卡号获取卡点信息
    /// </summary>
    public async Task<CardPoint?> GetCardPointAsync(string cardNo)
    {
        await InitAsync();
        return await _database!.Table<CardPoint>()
            .Where(c => c.CardNo == cardNo)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// 获取所有缓存的卡点
    /// </summary>
    public async Task<List<CardPoint>> GetAllCardPointsAsync()
    {
        await InitAsync();
        return await _database!.Table<CardPoint>().ToListAsync();
    }

    #endregion

    #region 打卡频率限制相关方法

    /// <summary>
    /// 检查指定卡点在指定时间内是否已打卡
    /// </summary>
    /// <param name="cardNo">卡号</param>
    /// <param name="minutes">时间范围（分钟）</param>
    /// <returns>如果已打卡，返回上次打卡记录；否则返回null</returns>
    public async Task<PatrolRecord?> GetRecentCheckInAsync(string cardNo, int minutes = 15)
    {
        await InitAsync();
        var cutoffTime = DateTime.Now.AddMinutes(-minutes);
        
        return await _database!.Table<PatrolRecord>()
            .Where(r => r.NfcId == cardNo && r.CheckInTime > cutoffTime)
            .OrderByDescending(r => r.CheckInTime)
            .FirstOrDefaultAsync();
    }

    #endregion
}
