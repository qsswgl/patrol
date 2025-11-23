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
        
        // 处理新的NFC Intent (息屏、锁屏后读卡会触发)
        HandleNfcIntent(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        
        // 启动NFC监听
        var nfcService = IPlatformApplication.Current?.Services.GetService<NfcService>();
        nfcService?.StartListening();
    }

    protected override void OnPause()
    {
        base.OnPause();
        
        // 不要停止NFC监听,以支持后台读卡
        // 如果需要支持息屏和锁屏读卡,这里不应该停止
    }

    private void HandleNfcIntent(Intent? intent)
    {
        if (intent == null)
            return;

        var nfcService = IPlatformApplication.Current?.Services.GetService<NfcService>();
        (nfcService as Services.NfcService)?.HandleIntent(intent);
    }
}
