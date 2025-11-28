using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PatrolApp.Models;

namespace PatrolApp.Services;

/// <summary>
/// API 返回的巡更点信息
/// </summary>
public class PatrolPointInfo
{
    [JsonPropertyName("classNameID")]
    public int ClassNameID { get; set; }
    
    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = "";
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    
    [JsonPropertyName("cardNo")]
    public string CardNo { get; set; } = "";
    
    [JsonPropertyName("beginTime")]
    public DateTime? BeginTime { get; set; }
}

/// <summary>
/// API 响应包装类
/// </summary>
public class ApiResponse<T>
{
    [JsonPropertyName("data")]
    public List<T>? Data { get; set; }
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://tx.qsgl.net:5190/qsoft542/proceduer";

    public ApiService()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// 调用 get_card API 获取卡对应的巡更点
    /// </summary>
    /// <param name="cardNo">卡号</param>
    /// <returns>巡更点信息，如果卡不存在返回 null</returns>
    public async Task<PatrolPointInfo?> GetCardAsync(string cardNo)
    {
        try
        {
            var requestBody = new { cardNo = cardNo };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{BaseUrl}/get_card", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"get_card 响应: {responseJson}");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                // 尝试解析为数组格式
                var dataList = JsonSerializer.Deserialize<List<PatrolPointInfo>>(responseJson, options);
                if (dataList != null && dataList.Count > 0)
                {
                    return dataList[0];
                }
                
                return null; // 卡不存在
            }
            
            System.Diagnostics.Debug.WriteLine($"get_card 请求失败: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"get_card API调用失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 调用 insert_address API 添加新巡更点
    /// </summary>
    /// <param name="cardNo">卡号</param>
    /// <param name="locationName">巡更点位置名称</param>
    /// <returns>是否成功</returns>
    public async Task<bool> InsertAddressAsync(string cardNo, string locationName)
    {
        try
        {
            var requestBody = new 
            { 
                cardNo = cardNo,
                locationName = locationName
            };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{BaseUrl}/insert_address", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"insert_address 响应: {responseJson}");
                return true;
            }
            
            System.Diagnostics.Debug.WriteLine($"insert_address 请求失败: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"insert_address API调用失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 调用 insert_patrol API 插入巡更记录
    /// </summary>
    /// <param name="cardNo">卡号</param>
    /// <param name="locationName">巡更点位置名称</param>
    /// <returns>是否成功</returns>
    public async Task<bool> InsertPatrolAsync(string cardNo, string locationName)
    {
        try
        {
            var requestBody = new 
            { 
                cardNo = cardNo,
                locationName = locationName
            };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{BaseUrl}/insert_patrol", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"insert_patrol 响应: {responseJson}");
                return true;
            }
            
            System.Diagnostics.Debug.WriteLine($"insert_patrol 请求失败: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"insert_patrol API调用失败: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UploadRecordAsync(PatrolRecord record)
    {
        // 使用 insert_patrol 替代原有上传
        return await InsertPatrolAsync(record.NfcId, record.Location);
    }

    public async Task<bool> UploadRecordsAsync(List<PatrolRecord> records)
    {
        try
        {
            bool allSuccess = true;
            foreach (var record in records)
            {
                var result = await InsertPatrolAsync(record.NfcId, record.Location);
                if (!result)
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
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
