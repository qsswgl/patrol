using System.Text.Json;

namespace PatrolApp.Services;

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string Url { get; set; } = "";
    public string Message { get; set; } = "";
}

public partial class UpdateService
{
    private readonly HttpClient _httpClient;
    private const string VERSION_CHECK_URL = "https://file.qsgl.net:9443/patrol/version.json";
    
    // 当前版本号
    public const string CURRENT_VERSION = "251204.2";

    public UpdateService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(VERSION_CHECK_URL);
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (updateInfo != null && CompareVersions(updateInfo.Version, CURRENT_VERSION) > 0)
            {
                return updateInfo;
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"检查更新失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 比较版本号
    /// </summary>
    private int CompareVersions(string newVersion, string currentVersion)
    {
        try
        {
            // 版本格式: 251201.2
            var newParts = newVersion.Split('.');
            var currentParts = currentVersion.Split('.');

            // 比较主版本号 (日期部分)
            var newMain = int.Parse(newParts[0]);
            var currentMain = int.Parse(currentParts[0]);
            
            if (newMain != currentMain)
                return newMain.CompareTo(currentMain);

            // 比较次版本号
            var newSub = newParts.Length > 1 ? int.Parse(newParts[1]) : 0;
            var currentSub = currentParts.Length > 1 ? int.Parse(currentParts[1]) : 0;

            return newSub.CompareTo(currentSub);
        }
        catch
        {
            // 如果解析失败，使用字符串比较
            return string.Compare(newVersion, currentVersion, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 下载 APK 文件
    /// </summary>
    public async Task<string?> DownloadApkAsync(string url, IProgress<double>? progress = null)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var downloadPath = Path.Combine(FileSystem.CacheDirectory, "update.apk");

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)totalRead / totalBytes * 100);
                }
            }

            return downloadPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"下载APK失败: {ex.Message}");
            return null;
        }
    }
}
