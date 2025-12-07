using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Signer.Trezor.Maui.Services;

#if ANDROID || __ANDROID__
using Android.Hardware.Usb;
using Android.Content;
using Nethereum.Signer.Trezor.Maui.Platforms.Android;
#endif

namespace Nethereum.Signer.Trezor.Maui
{
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
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<ILoggerFactory>(_ => LoggerFactory.Create(cfg => cfg.AddDebug()));
            builder.Services.AddSingleton<ITrezorPromptHandler, MauiPromptHandler>();
            builder.Services.AddSingleton(provider =>
            {
                var platformProviders = new NethereumTrezorManagerBrokerFactory.PlatformDeviceFactoryProviders();
#if ANDROID || __ANDROID__
                var usbManager = (UsbManager)Android.App.Application.Context.GetSystemService(Context.UsbService);
                platformProviders.AndroidProvider = new TrezorAndroidDeviceFactoryProvider(usbManager, Android.App.Application.Context);
#endif
                return platformProviders;
            });
            builder.Services.AddSingleton<TrezorSigningService>();
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
