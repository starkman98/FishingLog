using FishingLog.Mobile.Configuration;
using Microsoft.Extensions.Logging;

namespace FishingLog.Mobile;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Load and register app settings
		var appSettings = AppSettings.Load();
		builder.Services.AddSingleton(appSettings);
		builder.Services.AddSingleton(appSettings.Api);
		builder.Services.AddSingleton(appSettings.Sync);
		builder.Services.AddSingleton(appSettings.Database);

		return builder.Build();
	}
}
