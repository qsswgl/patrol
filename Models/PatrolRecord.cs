using SQLite;

namespace PatrolApp.Models;

[Table("patrol_records")]
public class PatrolRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(50)]
    public string? NfcId { get; set; }

    public DateTime CheckInTime { get; set; }

    public bool IsSynced { get; set; } = false;

    public DateTime? SyncedTime { get; set; }
}

/// <summary>
/// 卡点信息缓存表
/// </summary>
[Table("card_points")]
public class CardPoint
{
    [PrimaryKey]
    [MaxLength(50)]
    public string CardNo { get; set; } = "";

    [MaxLength(100)]
    public string LocationName { get; set; } = "";

    [MaxLength(50)]
    public string Type { get; set; } = "";

    /// <summary>
    /// 缓存更新时间
    /// </summary>
    public DateTime UpdatedTime { get; set; }
}
