using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using AndroidFileProvider = AndroidX.Core.Content.FileProvider;

namespace PatrolApp.Services;

public partial class UpdateService
{
    /// <summary>
    /// 安装 APK (Android 平台实现)
    /// </summary>
    public void InstallApk(string apkPath)
    {
        try
        {
            var context = Android.App.Application.Context;
            var file = new Java.IO.File(apkPath);

            Intent intent;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                // Android 7.0+ 使用 FileProvider
                var apkUri = AndroidFileProvider.GetUriForFile(
                    context,
                    $"{context.PackageName}.fileprovider",
                    file);

                intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent.AddFlags(ActivityFlags.NewTask);
            }
            else
            {
                // Android 7.0 以下
                var apkUri = Android.Net.Uri.FromFile(file);
                intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                intent.AddFlags(ActivityFlags.NewTask);
            }

            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"安装APK失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查是否有安装未知应用权限 (Android 8.0+)
    /// </summary>
    public bool CanInstallUnknownApps()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var context = Android.App.Application.Context;
            return context.PackageManager?.CanRequestPackageInstalls() ?? false;
        }
        return true;
    }

    /// <summary>
    /// 请求安装未知应用权限
    /// </summary>
    public void RequestInstallPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var context = Android.App.Application.Context;
            var intent = new Intent(
                Android.Provider.Settings.ActionManageUnknownAppSources,
                Android.Net.Uri.Parse($"package:{context.PackageName}"));
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
    }
}
