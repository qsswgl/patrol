using System.Text;
using System.Text.Json;
using PatrolApp.Models;

namespace PatrolApp.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://your-api-endpoint.com"; // 请替换为实际的API地址

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<bool> UploadRecordAsync(PatrolRecord record)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                location = record.Location,
                nfcId = record.NfcId,
                checkInTime = record.CheckInTime,
                localId = record.Id
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/patrol/checkin", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API上传失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UploadRecordsAsync(List<PatrolRecord> records)
    {
        try
        {
            var json = JsonSerializer.Serialize(records.Select(r => new
            {
                location = r.Location,
                nfcId = r.NfcId,
                checkInTime = r.CheckInTime,
                localId = r.Id
            }));

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/patrol/batch-checkin", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"批量上传失败: {ex.Message}");
            return false;
        }
    }

    public bool IsNetworkAvailable()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }
}
