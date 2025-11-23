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
}
