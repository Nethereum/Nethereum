using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nethereum.Reown.AppKit.Blazor;

[JsonConverter(typeof(JsonStringEnumConverter<AllWalletsOptions>))]
public enum AllWalletsOptions {
	SHOW,
	HIDE,
	ONLY_MOBILE,
}

[JsonConverter(typeof(JsonStringEnumConverter<SocialOptions>))]
public enum SocialOptions {
	google,
	x,
	discord,
	farcaster,
	github,
	apple,
	facebook,
}

[JsonConverter(typeof(JsonStringEnumConverter<ThemeModeOptions>))]
public enum ThemeModeOptions {
	dark,
	light,
}

[JsonConverter(typeof(JsonStringEnumConverter<CoinbasePreferenceOptions>))]
public enum CoinbasePreferenceOptions {
	all,
	smartWalletOnly,
	eoaOnly,
}

public class AppKitConfiguration {
	public required IEnumerable<Network> Networks { get; init; }
	public Network? DefaultNetwork { get; init; }
	public ThemeModeOptions? ThemeMode { get; set; } = null;
	public CoinbasePreferenceOptions CoinbasePreference { get; set; } = CoinbasePreferenceOptions.all;
	public ThemeVariables? ThemeVariables { get; init; } = null;

	public AllWalletsOptions AllWallets { get; init; } = AllWalletsOptions.SHOW;
	public required string ProjectId { get; init; }
	public string[]? FeaturedWalletIds { get; init; }
	public string[]? IncludeWalletIds { get; init; }
	public string[]? ExcludeWalletIds { get; init; }
	public string? TermsConditionsUrl { get; init; }
	public string? PrivacyPolicyUrl { get; init; }

	#region Metadata
	public required string Name { get; init; }
	public required string Description { get; init; }
	public required string Url { get; init; }
	public required string[] Icons { get; init; }
	#endregion

	public bool? DisableAppend { get; init; } = null;
	public bool? EnableWallets { get; init; } = null;
	public bool? EnableEIP6963 { get; init; } = null;
	public bool? EnableCoinbase { get; init; } = null;
	public bool? EnableInjected { get; init; } = null;
	public bool Debug { get; init; } = false;

	#region Features
	public bool Swaps { get; init; } = true;
	public bool Onramp { get; init; } = true;
	public bool Email { get; init; } = true;
	public bool EmailShowWallets { get; init; } = true;
	public HashSet<SocialOptions>? Socials { get; init; } = [
		SocialOptions.google,
		SocialOptions.x,
		SocialOptions.discord,
		SocialOptions.farcaster,
		SocialOptions.github,
		SocialOptions.apple,
		SocialOptions.facebook
	];
	public bool History { get; init; } = true;
	public bool Analytics { get; init; } = true;
	public bool LegalCheckbox { get; init; } = false;
	#endregion;

}

public struct ThemeVariables {
	public string W3mFontFamily { get; init; }
	public string W3mAccent { get; init; }
	public string W3mColorMix { get; init; }
	public int W3mColorMixStrength { get; init; }
	public string W3mFontSizeMaster { get; init; }
	public string W3mBorderRadiusMaster { get; init; }
	public int W3mZIndex { get; init; }
}