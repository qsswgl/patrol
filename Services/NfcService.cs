namespace PatrolApp.Services;

public partial class NfcService
{
    public event EventHandler<NfcTagReadEventArgs>? TagRead;
    
    partial void PlatformInit();
    partial void PlatformStartListening();
    partial void PlatformStopListening();
    partial void PlatformEnableBackgroundMode();

    public NfcService()
    {
        PlatformInit();
    }

    public void StartListening()
    {
        PlatformStartListening();
    }

    public void StopListening()
    {
        PlatformStopListening();
    }

    public void EnableBackgroundMode()
    {
        PlatformEnableBackgroundMode();
    }

    protected void OnTagRead(string tagId, string? location = null)
    {
        TagRead?.Invoke(this, new NfcTagReadEventArgs(tagId, location));
    }
}

public class NfcTagReadEventArgs : EventArgs
{
    public string TagId { get; }
    public string? Location { get; }
    public DateTime ReadTime { get; }

    public NfcTagReadEventArgs(string tagId, string? location = null)
    {
        TagId = tagId;
        Location = location;
        ReadTime = DateTime.Now;
    }
}
