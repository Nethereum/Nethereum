using Nethereum.Reown.AppKit.Blazor;
using Nethereum.Reown.AppKit.Blazor.Wagmi;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nethereum.Blazor.Reown.AppKit.Wagmi;

internal static partial class AppKitInterop {
	private const string ModuleName = "appKitModule";
	private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

	public static async Task InitializeAsync(AppKitConfiguration configuration) {
		if (!OperatingSystem.IsBrowser()) {
			throw new InvalidOperationException("AppKit is only supported in browser");
		}

		await JSHost.ImportAsync(ModuleName, "/_content/Nethereum.Reown.AppKit.Blazor/js/index.js").ConfigureAwait(false);
		string json = JsonSerializer.Serialize(configuration, options);
		await InternalInitializeAsync(json).ConfigureAwait(false);
	}

	public static async Task<GetAccountReturnType> EnableProviderAsync() {
		string value = await InternalEnableProviderAsync().ConfigureAwait(false);
		return JsonSerializer.Deserialize<GetAccountReturnType>(value, options)!;
	}

	public static GetAccountReturnType GetAccount() {
		string value = InternalGetAccount();
		return JsonSerializer.Deserialize<GetAccountReturnType>(value, options)!;
	}

	public static void WatchAccount(Action<GetAccountReturnType> callback) {
		InternalWatchAccount(value => {
			GetAccountReturnType account = JsonSerializer.Deserialize<GetAccountReturnType>(value, options)!;
			callback.Invoke(account);
		});
	}

	[JSImport(nameof(InitializeAsync), ModuleName)]
	private static partial Task InternalInitializeAsync([JSMarshalAs<JSType.String>] string config);

	[JSImport(nameof(Open), ModuleName)]
	public static partial void Open();

	[JSImport(nameof(Close), ModuleName)]
	public static partial void Close();

	[JSImport(nameof(Disconnect), ModuleName)]
	public static partial void Disconnect();

	[JSImport(nameof(WatchAccount), ModuleName)]
	private static partial void InternalWatchAccount([JSMarshalAs<JSType.Function<JSType.String>>] Action<string> callback);

	[JSImport(nameof(WatchChainId), ModuleName)]
	public static partial void WatchChainId([JSMarshalAs<JSType.Function<JSType.Number>>] Action<long> callback);

	[JSImport(nameof(EnableProviderAsync), ModuleName)]
	private static partial Task<string> InternalEnableProviderAsync();

	[JSImport(nameof(GetAccount), ModuleName)]
	private static partial string InternalGetAccount();

	[JSImport(nameof(SignMessageAsync), ModuleName)]
	public static partial Task<string> SignMessageAsync([JSMarshalAs<JSType.String>] string message);

	[JSImport(nameof(SendTransactionAsync), ModuleName)]
	public static partial Task<string> SendTransactionAsync([JSMarshalAs<JSType.Number>] int id, [JSMarshalAs<JSType.String>] string method, [JSMarshalAs<JSType.String>] string? parameters);
}