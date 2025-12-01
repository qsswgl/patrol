using Android.App;
using Android.Content;
using Android.Nfc;
using Android.Nfc.Tech;
using Android.OS;
using Android.Runtime;
using Application = Android.App.Application;

namespace PatrolApp.Services;

public partial class NfcService
{
    private NfcAdapter? _nfcAdapter;
    private PendingIntent? _pendingIntent;
    private IntentFilter[]? _intentFilters;
    private string[][]? _techLists;
    private Activity? _activity;
    private bool _isListening = false;

    partial void PlatformInit()
    {
        InitializeNfc();
    }
    
    private void InitializeNfc()
    {
        _activity = Platform.CurrentActivity;
        
        if (_activity == null)
        {
            System.Diagnostics.Debug.WriteLine("Activity为空，无法初始化NFC");
            return;
        }
        
        _nfcAdapter = NfcAdapter.GetDefaultAdapter(_activity);
        
        if (_nfcAdapter == null)
        {
            System.Diagnostics.Debug.WriteLine("NFC不可用");
            return;
        }

        // 创建PendingIntent - 每次都重新创建以确保使用最新的Activity
        var intent = new Intent(_activity, _activity.GetType())
            .AddFlags(ActivityFlags.SingleTop);
        
        var flags = Build.VERSION.SdkInt >= BuildVersionCodes.S 
            ? PendingIntentFlags.Mutable 
            : PendingIntentFlags.UpdateCurrent;
            
        _pendingIntent = PendingIntent.GetActivity(_activity, 0, intent, flags);

        // 设置IntentFilter - 优先使用 TAG_DISCOVERED 确保兼容性
        var tagDetected = new IntentFilter(NfcAdapter.ActionTagDiscovered);
        var techDetected = new IntentFilter(NfcAdapter.ActionTechDiscovered);
        var ndefDetected = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
        try
        {
            ndefDetected.AddDataType("*/*");
        }
        catch { }

        _intentFilters = new[] { tagDetected, techDetected, ndefDetected };
        
        // 添加所有NFC技术类型支持 - 关键：华为等设备需要明确指定tech-list
        _techLists = new string[][] {
            new string[] { Java.Lang.Class.FromType(typeof(NfcA)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(NfcB)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(NfcF)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(NfcV)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(IsoDep)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(MifareClassic)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(MifareUltralight)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(Ndef)).Name },
            new string[] { Java.Lang.Class.FromType(typeof(NdefFormatable)).Name },
        };
        
        System.Diagnostics.Debug.WriteLine("NFC初始化完成");
    }

    partial void PlatformStartListening()
    {
        try
        {
            // 获取当前Activity，确保使用最新的
            var currentActivity = Platform.CurrentActivity;
            
            // 如果Activity变化了，需要重新初始化
            if (currentActivity != _activity || _pendingIntent == null)
            {
                System.Diagnostics.Debug.WriteLine("Activity已变化，重新初始化NFC");
                
                // 先停止旧的监听
                if (_isListening && _nfcAdapter != null && _activity != null)
                {
                    try
                    {
                        _nfcAdapter.DisableForegroundDispatch(_activity);
                    }
                    catch { }
                    _isListening = false;
                }
                
                // 重新初始化
                InitializeNfc();
            }
            
            if (_nfcAdapter == null || _activity == null || _pendingIntent == null)
            {
                System.Diagnostics.Debug.WriteLine("NFC未正确初始化");
                return;
            }

            // 使用 tech-list 确保所有类型的 NFC 标签都能被捕获
            _nfcAdapter.EnableForegroundDispatch(
                _activity,
                _pendingIntent,
                _intentFilters,
                _techLists);
            
            _isListening = true;
            System.Diagnostics.Debug.WriteLine("NFC前台调度已启用");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动NFC监听失败: {ex.Message}");
            _isListening = false;
        }
    }

    partial void PlatformStopListening()
    {
        if (_nfcAdapter == null || _activity == null)
            return;

        try
        {
            _nfcAdapter.DisableForegroundDispatch(_activity);
            _isListening = false;
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
