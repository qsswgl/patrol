using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using PatrolApp.Services;

namespace PatrolApp;

[Activity(
    Theme = "@style/Maui.SplashTheme", 
    MainLauncher = true,
    Label = "巡更打卡",
    Icon = "@mipmap/appicon",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | 
    ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | 
    ConfigChanges.Density,
    LaunchMode = LaunchMode.SingleTop,
    Exported = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // 处理启动时的NFC Intent
        HandleNfcIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent; // 更新当前Intent
        
        // 处理新的NFC Intent (息屏、锁屏后读卡会触发)
        HandleNfcIntent(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        
        // 每次恢复时都重新启动NFC监听 - 解决从后台恢复后NFC不工作的问题
        var nfcService = IPlatformApplication.Current?.Services.GetService<NfcService>();
        nfcService?.StartListening();
        
        System.Diagnostics.Debug.WriteLine("MainActivity.OnResume - NFC监听已启动");
    }

    protected override void OnPause()
    {
        base.OnPause();
        
        // 暂停时停止前台调度，让系统可以处理NFC
        // 这是Android前台调度的标准做法
        var nfcService = IPlatformApplication.Current?.Services.GetService<NfcService>();
        (nfcService as Services.NfcService)?.StopListening();
        
        System.Diagnostics.Debug.WriteLine("MainActivity.OnPause - NFC监听已停止");
    }

    private void HandleNfcIntent(Intent? intent)
    {
        if (intent == null)
            return;

        var nfcService = IPlatformApplication.Current?.Services.GetService<NfcService>();
        (nfcService as Services.NfcService)?.HandleIntent(intent);
    }
}
