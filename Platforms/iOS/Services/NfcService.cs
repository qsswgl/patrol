using CoreNFC;
using Foundation;
using UIKit;

namespace PatrolApp.Services;

public partial class NfcService
{
    private NFCNDEFReaderSession? _session;
    private NFCTagReaderSession? _tagSession;

    partial void PlatformInit()
    {
        // iOS NFC初始化
    }

    partial void PlatformStartListening()
    {
        if (!NFCNDEFReaderSession.ReadingAvailable)
        {
            System.Diagnostics.Debug.WriteLine("NFC在此设备上不可用");
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _session = new NFCNDEFReaderSession(
                new NFCNDEFReaderSessionDelegate(this),
                null,
                true)
            {
                AlertMessage = "请将设备靠近NFC标签"
            };
            _session.BeginSession();
        });
    }

    partial void PlatformStopListening()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _session?.InvalidateSession();
            _session = null;
        });
    }

    partial void PlatformEnableBackgroundMode()
    {
        // iOS需要在Info.plist中配置Background Modes
        // 但iOS不支持真正的后台NFC读取,只能在前台或短暂后台
        PlatformStartListening();
    }

    private class NFCNDEFReaderSessionDelegate : NSObject, INFCNDEFReaderSessionDelegate
    {
        private readonly NfcService _service;

        public NFCNDEFReaderSessionDelegate(NfcService service)
        {
            _service = service;
        }

        public void DidDetect(NFCNDEFReaderSession session, NFCNDEFMessage[] messages)
        {
            if (messages.Length == 0)
                return;

            var message = messages[0];
            if (message.Records.Length == 0)
                return;

            var record = message.Records[0];
            var payload = record.Payload;
            
            string? location = null;
            if (payload != null && payload.Length > 3)
            {
                var languageCodeLength = payload[0] & 0x3F;
                var textBytes = payload.Skip(languageCodeLength + 1).ToArray();
                location = System.Text.Encoding.UTF8.GetString(textBytes);
            }

            // 使用标签的标识符
            var tagId = record.Identifier?.ToString() ?? Guid.NewGuid().ToString();
            
            session.AlertMessage = "读取成功!";
            session.InvalidateSession();

            _service.OnTagRead(tagId, location);
        }

        public void DidInvalidate(NFCNDEFReaderSession session, NSError error)
        {
            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"NFC会话失效: {error.LocalizedDescription}");
            }
        }
    }
}
