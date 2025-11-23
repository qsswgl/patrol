namespace PatrolApp.Services;

public class TextToSpeechService
{
    public async Task SpeakAsync(string text)
    {
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            
            // 优先选择中文语音
            var chineseLocale = locales.FirstOrDefault(l => 
                l.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase));

            var options = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f,
                Locale = chineseLocale
            };

            await TextToSpeech.Default.SpeakAsync(text, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"语音播报失败: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            TextToSpeech.Default.SpeakAsync(string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"停止语音失败: {ex.Message}");
        }
    }
}
