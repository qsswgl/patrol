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
