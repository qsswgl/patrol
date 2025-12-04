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
    private const string BaseUrl = "https://tx.qsgl.net:5190/qsoft542/procedure";

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
    /// 调用 get_all_cards API 获取所有巡更点
    /// </summary>
    /// <returns>所有巡更点列表</returns>
    public async Task<List<PatrolPointInfo>> GetAllCardsAsync()
    {
        try
        {
            // 调用存储过程获取所有卡点
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/get_all_cards", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"get_all_cards 响应: {responseJson}");
                
                try
                {
                    // 尝试解析为数组格式
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var dataList = JsonSerializer.Deserialize<List<PatrolPointInfo>>(responseJson, options);
                    if (dataList != null)
                    {
                        return dataList;
                    }
                }
                catch (JsonException)
                {
                    // 尝试解析为包装对象格式
                    try
                    {
                        using var doc = JsonDocument.Parse(responseJson);
                        if (doc.RootElement.TryGetProperty("data", out var dataElement))
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var dataList = JsonSerializer.Deserialize<List<PatrolPointInfo>>(dataElement.GetRawText(), options);
                            if (dataList != null)
                            {
                                return dataList;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // 解析失败
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"get_all_cards 请求失败: {response.StatusCode}");
            return new List<PatrolPointInfo>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"get_all_cards API调用失败: {ex.Message}");
            return new List<PatrolPointInfo>();
        }
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
            // 使用 JSON 格式
            var requestData = new { CardNo = cardNo };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{BaseUrl}/get_card", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"get_card 响应: {responseJson}");
                
                // API 返回格式: {"Result":"0","Message":"位置名"} 或 {"Result":"-1","Message":"错误"}
                // 如果卡不存在，Message 为空或返回 Result=-1
                try
                {
                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;
                    
                    // 检查 Result 字段
                    if (root.TryGetProperty("Result", out var resultElement))
                    {
                        var result = resultElement.GetString();
                        if (result == "0")
                        {
                            // 成功，获取 Message 作为位置名
                            if (root.TryGetProperty("Message", out var msgElement))
                            {
                                var locationName = msgElement.GetString();
                                if (!string.IsNullOrEmpty(locationName))
                                {
                                    return new PatrolPointInfo
                                    {
                                        CardNo = cardNo,
                                        LocationName = locationName,
                                        Type = "巡更点"
                                    };
                                }
                            }
                        }
                    }
                    
                    // 尝试解析为数组格式（兼容旧格式）
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var dataList = JsonSerializer.Deserialize<List<PatrolPointInfo>>(responseJson, options);
                    if (dataList != null && dataList.Count > 0)
                    {
                        return dataList[0];
                    }
                }
                catch (JsonException)
                {
                    // JSON 解析失败
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
    /// <returns>成功返回 null，失败返回错误信息</returns>
    public async Task<string?> InsertAddressAsync(string cardNo, string locationName)
    {
        try
        {
            Console.WriteLine($"[InsertAddress] 开始添加巡更点: CardNo={cardNo}, LocationName={locationName}");
            
            // 使用 JSON 格式
            var requestData = new { CardNo = cardNo, LocationName = locationName };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            Console.WriteLine($"[InsertAddress] 请求JSON: {json}");
            
            Console.WriteLine($"[InsertAddress] 发送请求到: {BaseUrl}/insert_address");
            var response = await _httpClient.PostAsync($"{BaseUrl}/insert_address", content);
            
            var responseJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[InsertAddress] HTTP状态码: {response.StatusCode}");
            Console.WriteLine($"[InsertAddress] 响应内容: {responseJson}");
            
            if (response.IsSuccessStatusCode)
            {
                // 检查响应中的 Result 字段
                try
                {
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("Result", out var resultElement))
                    {
                        var result = resultElement.GetString();
                        if (result == "-1")
                        {
                            // API 返回错误
                            var message = doc.RootElement.TryGetProperty("Message", out var msgElement) 
                                ? msgElement.GetString() 
                                : "未知错误";
                            Console.WriteLine($"[InsertAddress] API返回错误: {message}");
                            return message;
                        }
                    }
                }
                catch (JsonException)
                {
                    // JSON 解析失败，忽略
                }
                
                Console.WriteLine($"[InsertAddress] 添加成功");
                return null; // 成功返回 null
            }
            
            var errorMsg = $"HTTP {(int)response.StatusCode}: {responseJson}";
            Console.WriteLine($"[InsertAddress] 添加失败: {errorMsg}");
            return errorMsg;
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"网络错误: {ex.Message}";
            Console.WriteLine($"[InsertAddress] {errorMsg}");
            return errorMsg;
        }
        catch (TaskCanceledException ex)
        {
            var errorMsg = "请求超时，请检查网络";
            Console.WriteLine($"[InsertAddress] 超时: {ex.Message}");
            return errorMsg;
        }
        catch (Exception ex)
        {
            var errorMsg = $"未知错误: {ex.Message}";
            Console.WriteLine($"[InsertAddress] {errorMsg}");
            return errorMsg;
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
            // 使用 JSON 格式
            var requestData = new { CardNo = cardNo, LocationName = locationName };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{BaseUrl}/insert_patrol", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"insert_patrol 响应: {responseJson}");
                
                // 检查响应中的 Result 字段
                try
                {
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("Result", out var resultElement))
                    {
                        var result = resultElement.GetString();
                        if (result == "-1")
                        {
                            var message = doc.RootElement.TryGetProperty("Message", out var msgElement) 
                                ? msgElement.GetString() 
                                : "未知错误";
                            System.Diagnostics.Debug.WriteLine($"insert_patrol API返回错误: {message}");
                            return false;
                        }
                    }
                }
                catch (JsonException)
                {
                    // JSON 解析失败，忽略
                }
                
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
