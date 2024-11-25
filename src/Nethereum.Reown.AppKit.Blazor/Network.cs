using System.Collections.Generic;

namespace Nethereum.Reown.AppKit.Blazor;

public class Network {
	public required long Id { get; init; }
	public required string Name { get; init; }
	public required Currency NativeCurrency { get; init; }
	public BlockExplorers? BlockExplorers { get; init; }
	public required RpcUrls RpcUrls { get; init; }
	public bool Testnet { get; init; }

}

public record RpcUrls(RpcUrl Default);
public record RpcUrl(string[] Http, string? WebSocket);

public record BlockExplorers(BlockExplorer Default);
public record BlockExplorer(string Name, string Url);
public record Currency(string Name, string Symbol, int Decimals);


public static class NetworkConstants {
	public static class References {
		public const long Ethereum = 1;
		public const long Optimism = 10;
		public const long Ronin = 2020;
		public const long RoninSaigon = 2021;
		public const long Base = 8453;
		public const long Arbitrum = 42161;
		public const long Celo = 42220;
		public const long CeloAlfajores = 44787;
		public const long Polygon = 137;
		public const long Avalanche = 43114;
	}

	public static class Networks {
		public static readonly Network Ethereum = new() {
			Id = References.Ethereum,
			Name = "Ethereum",
			NativeCurrency = new("Ether", "ETH", 18),
			BlockExplorers = new(new("Etherscan", "https://etherscan.io")),
			RpcUrls = new(new(["https://cloudflare-eth.com"], null)),
			Testnet = false,
		};

		public static readonly Network Optimism = new() {
			Id = References.Optimism,
			Name = "Optimism",
			NativeCurrency = new("Ether", "ETH", 18),
			BlockExplorers = new(new("Optimistic Etherscan", "https://optimistic.etherscan.io")),
			RpcUrls = new(new(["https://mainnet.optimism.io"], null)),
			Testnet = false,
		};

		public static readonly Network Ronin = new() {
			Id = References.Ronin,
			Name = "Ronin",
			NativeCurrency = new("Ronin", "RON", 18),
			BlockExplorers = new(new("Ronin Explorer", "https://app.roninchain.com/")),
			RpcUrls = new(new(["https://api.roninchain.com/rpc"], null)),
			Testnet = false,
		};

		public static readonly Network RoninSaigon = new() {
			Id = References.RoninSaigon,
			Name = "Ronin Saigon",
			NativeCurrency = new("Ronin", "RON", 18),
			BlockExplorers = new(new("Ronin Explorer", "https://explorer.roninchain.com")),
			RpcUrls = new(new(["https://api-gateway.skymavis.com/rpc/testnet"], null)),
			Testnet = false,
		};

		public static readonly Network Arbitrum = new() {
			Id = References.Arbitrum,
			Name = "Arbitrum",
			NativeCurrency = new("Ether", "ETH", 18),
			BlockExplorers = new(new("Arbitrum Explorer", "https://arbiscan.io")),
			RpcUrls = new(new(["https://arb1.arbitrum.io/rpc"], null)),
			Testnet = false,
		};

		public static readonly Network Celo = new() {
			Id = References.Celo,
			Name = "Celo",
			NativeCurrency = new("Celo", "CELO", 18),
			BlockExplorers = new(new("Celo Explorer", "https://explorer.celo.org")),
			RpcUrls = new(new(["https://forno.celo.org"], null)),
			Testnet = false,
		};

		public static readonly Network CeloAlfajores = new() {
			Id = References.CeloAlfajores,
			Name = "Celo Alfajores",
			NativeCurrency = new("Celo", "CELO", 18),
			BlockExplorers = new(new("Celo Explorer", "https://alfajores-blockscout.celo-testnet.org")),
			RpcUrls = new(new(["https://alfajores-forno.celo-testnet.org"], null)),
			Testnet = true,
		};

		public static readonly Network Base = new() {
			Id = References.Base,
			Name = "Base",
			NativeCurrency = new("Ether", "ETH", 18),
			BlockExplorers = new(new("BaseScan", "https://basescan.org/")),
			RpcUrls = new(new(["https://mainnet.base.org"], null)),
			Testnet = false,
		};

		public static readonly Network Polygon = new() {
			Id = References.Polygon,
			Name = "Polygon",
			NativeCurrency = new("Polygon Ecosystem Token", "POL", 18),
			BlockExplorers = new(new("Polygon Explorer", "https://polygonscan.com")),
			RpcUrls = new(new(["https://polygon-rpc.com"], null)),
			Testnet = false,
		};

		public static readonly Network Avalanche = new() {
			Id = References.Avalanche,
			Name = "Avalanche",
			NativeCurrency = new("AVAX", "AVAX", 18),
			BlockExplorers = new(new("Avalanche Explorer", "https://snowtrace.io/")),
			RpcUrls = new(new(["https://api.avax.network/ext/bc/C/rpc"], null)),
			Testnet = false,
		};

		public static readonly IReadOnlyCollection<Network> All = [
			Ethereum,
			Optimism,
			Ronin,
			RoninSaigon,
			Arbitrum,
			Celo,
			CeloAlfajores,
			Base,
			Polygon,
			Avalanche
		];
	}
}