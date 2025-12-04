using Microsoft.Extensions.Logging;
using PatrolApp.Services;
using PatrolApp.ViewModels;
using PatrolApp.Views;

namespace PatrolApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<NfcService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<TextToSpeechService>();
        builder.Services.AddSingleton<UpdateService>();
        
        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        
        // Register Views
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
