using Android.App;
using Android.Content;
using Android.Nfc;
using Android.OS;
using Android.Runtime;
using Application = Android.App.Application;

namespace PatrolApp.Services;

public partial class NfcService
{
    private NfcAdapter? _nfcAdapter;
    private PendingIntent? _pendingIntent;
    private IntentFilter[]? _intentFilters;
    private Activity? _activity;

    partial void PlatformInit()
    {
        _activity = Platform.CurrentActivity;
        _nfcAdapter = NfcAdapter.GetDefaultAdapter(_activity);
        
        if (_nfcAdapter == null)
        {
            System.Diagnostics.Debug.WriteLine("NFC不可用");
            return;
        }

        // 创建PendingIntent
        var intent = new Intent(_activity, _activity.GetType())
            .AddFlags(ActivityFlags.SingleTop);
        
        var flags = Build.VERSION.SdkInt >= BuildVersionCodes.S 
            ? PendingIntentFlags.Mutable 
            : PendingIntentFlags.UpdateCurrent;
            
        _pendingIntent = PendingIntent.GetActivity(_activity, 0, intent, flags);

        // 设置IntentFilter
        var ndefDetected = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
        ndefDetected.AddDataType("*/*");
        
        var tagDetected = new IntentFilter(NfcAdapter.ActionTagDiscovered);
        var techDetected = new IntentFilter(NfcAdapter.ActionTechDiscovered);

        _intentFilters = new[] { ndefDetected, tagDetected, techDetected };
    }

    partial void PlatformStartListening()
    {
        if (_nfcAdapter == null || _activity == null)
            return;

        try
        {
            _nfcAdapter.EnableForegroundDispatch(
                _activity,
                _pendingIntent,
                _intentFilters,
                null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动NFC监听失败: {ex.Message}");
        }
    }

    partial void PlatformStopListening()
    {
        if (_nfcAdapter == null || _activity == null)
            return;

        try
        {
            _nfcAdapter.DisableForegroundDispatch(_activity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"停止NFC监听失败: {ex.Message}");
        }
    }

    partial void PlatformEnableBackgroundMode()
    {
        // Android通过Foreground Dispatch已经支持后台读卡
        PlatformStartListening();
    }

    public void HandleIntent(Intent? intent)
    {
        if (intent == null)
            return;

        var action = intent.Action;
        if (action != NfcAdapter.ActionNdefDiscovered && 
            action != NfcAdapter.ActionTagDiscovered && 
            action != NfcAdapter.ActionTechDiscovered)
            return;

        var tag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
        if (tag == null)
            return;

        var tagId = BitConverter.ToString(tag.GetId()).Replace("-", "");
        
        // 尝试从NFC标签中读取位置信息
        string? location = null;
        try
        {
            var ndef = Android.Nfc.Tech.Ndef.Get(tag);
            if (ndef != null)
            {
                ndef.Connect();
                var ndefMessage = ndef.NdefMessage;
                if (ndefMessage != null && ndefMessage.GetRecords().Length > 0)
                {
                    var payload = ndefMessage.GetRecords()[0].GetPayload();
                    if (payload != null && payload.Length > 3)
                    {
                        // NDEF文本记录格式: 第一个字节是状态字节,后续是语言代码和文本
                        var languageCodeLength = payload[0] & 0x3F;
                        location = System.Text.Encoding.UTF8.GetString(
                            payload, 
                            languageCodeLength + 1, 
                            payload.Length - languageCodeLength - 1);
                    }
                }
                ndef.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取NFC内容失败: {ex.Message}");
        }

        OnTagRead(tagId, location);
    }
}
