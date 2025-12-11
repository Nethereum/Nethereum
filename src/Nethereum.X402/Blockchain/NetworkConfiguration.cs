using System;
using System.Collections.Generic;

namespace Nethereum.X402.Blockchain;

/// <summary>
/// Configuration for blockchain networks including RPC endpoints and USDC contract addresses.
/// </summary>
public class NetworkConfiguration
{
    private readonly Dictionary<string, string> _rpcEndpoints;
    private readonly Dictionary<string, string> _usdcAddresses;
    private readonly Dictionary<string, int> _chainIds;

    public NetworkConfiguration(
        Dictionary<string, string>? rpcEndpoints = null,
        Dictionary<string, string>? usdcAddresses = null,
        Dictionary<string, int>? chainIds = null)
    {
        _rpcEndpoints = rpcEndpoints ?? GetDefaultRpcEndpoints();
        _usdcAddresses = usdcAddresses ?? GetDefaultUSDCAddresses();
        _chainIds = chainIds ?? GetDefaultChainIds();
    }

    /// <summary>
    /// Gets the default network configuration with public RPC endpoints.
    /// </summary>
    public static NetworkConfiguration Default => new NetworkConfiguration();

    /// <summary>
    /// Gets the RPC endpoint for a network.
    /// </summary>
    public string GetRpcEndpoint(string network)
    {
        if (_rpcEndpoints.TryGetValue(network, out var url))
        {
            return url;
        }

        throw new ArgumentException(
            $"No RPC endpoint configured for network '{network}'. " +
            "Configure custom endpoints via NetworkConfiguration constructor.",
            nameof(network));
    }

    /// <summary>
    /// Gets the USDC contract address for a network.
    /// </summary>
    public string GetUSDCAddress(string network)
    {
        if (_usdcAddresses.TryGetValue(network, out var address))
        {
            return address;
        }

        throw new ArgumentException(
            $"No USDC address configured for network '{network}'. " +
            "Configure custom addresses via NetworkConfiguration constructor.",
            nameof(network));
    }

    /// <summary>
    /// Gets the chain ID for a network.
    /// </summary>
    public int GetChainId(string network)
    {
        if (_chainIds.TryGetValue(network, out var chainId))
        {
            return chainId;
        }

        throw new ArgumentException(
            $"No chain ID configured for network '{network}'. " +
            "Configure custom chain IDs via NetworkConfiguration constructor.",
            nameof(network));
    }

    private static Dictionary<string, string> GetDefaultRpcEndpoints()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Base
            ["base-sepolia"] = "https://sepolia.base.org",
            ["base-mainnet"] = "https://mainnet.base.org",
            ["base"] = "https://mainnet.base.org",

            // Ethereum
            ["ethereum-mainnet"] = "https://eth.llamarpc.com",
            ["ethereum"] = "https://eth.llamarpc.com",
            ["mainnet"] = "https://eth.llamarpc.com",
            ["ethereum-sepolia"] = "https://rpc.sepolia.org",
            ["sepolia"] = "https://rpc.sepolia.org",

            // Polygon
            ["polygon-mainnet"] = "https://polygon-rpc.com",
            ["polygon"] = "https://polygon-rpc.com",
            ["polygon-amoy"] = "https://rpc-amoy.polygon.technology",

            // Avalanche
            ["avalanche-mainnet"] = "https://api.avax.network/ext/bc/C/rpc",
            ["avalanche"] = "https://api.avax.network/ext/bc/C/rpc",
            ["avalanche-fuji"] = "https://api.avax-test.network/ext/bc/C/rpc",

            // Arbitrum
            ["arbitrum-mainnet"] = "https://arb1.arbitrum.io/rpc",
            ["arbitrum"] = "https://arb1.arbitrum.io/rpc",
            ["arbitrum-sepolia"] = "https://sepolia-rollup.arbitrum.io/rpc",

            // Optimism
            ["optimism-mainnet"] = "https://mainnet.optimism.io",
            ["optimism"] = "https://mainnet.optimism.io",
            ["optimism-sepolia"] = "https://sepolia.optimism.io"
        };
    }

    private static Dictionary<string, string> GetDefaultUSDCAddresses()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Base
            ["base-sepolia"] = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            ["base-mainnet"] = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913",
            ["base"] = "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913",

            // Ethereum
            ["ethereum-mainnet"] = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
            ["ethereum"] = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
            ["mainnet"] = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
            ["ethereum-sepolia"] = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",
            ["sepolia"] = "0x1c7D4B196Cb0C7B01d743Fbc6116a902379C7238",

            // Polygon
            ["polygon-mainnet"] = "0x3c499c542cEF5E3811e1192ce70d8cC03d5c3359",
            ["polygon"] = "0x3c499c542cEF5E3811e1192ce70d8cC03d5c3359",
            ["polygon-amoy"] = "0x41E94Eb019C0762f9Bfcf9Fb1E58725BfB0e7582",

            // Avalanche
            ["avalanche-mainnet"] = "0xB97EF9Ef8734C71904D8002F8b6Bc66Dd9c48a6E",
            ["avalanche"] = "0xB97EF9Ef8734C71904D8002F8b6Bc66Dd9c48a6E",
            ["avalanche-fuji"] = "0x5425890298aed601595a70AB815c96711a31Bc65",

            // Arbitrum
            ["arbitrum-mainnet"] = "0xaf88d065e77c8cC2239327C5EDb3A432268e5831",
            ["arbitrum"] = "0xaf88d065e77c8cC2239327C5EDb3A432268e5831",
            ["arbitrum-sepolia"] = "0x75faf114eafb1BDbe2F0316DF893fd58CE46AA4d",

            // Optimism
            ["optimism-mainnet"] = "0x0b2C639c533813f4Aa9D7837CAf62653d097Ff85",
            ["optimism"] = "0x0b2C639c533813f4Aa9D7837CAf62653d097Ff85",
            ["optimism-sepolia"] = "0x5fd84259d66Cd46123540766Be93DFE6D43130D7"
        };
    }

    private static Dictionary<string, int> GetDefaultChainIds()
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Base
            ["base-sepolia"] = 84532,
            ["base-mainnet"] = 8453,
            ["base"] = 8453,

            // Ethereum
            ["ethereum-mainnet"] = 1,
            ["ethereum"] = 1,
            ["mainnet"] = 1,
            ["ethereum-sepolia"] = 11155111,
            ["sepolia"] = 11155111,

            // Polygon
            ["polygon-mainnet"] = 137,
            ["polygon"] = 137,
            ["polygon-amoy"] = 80002,

            // Avalanche
            ["avalanche-mainnet"] = 43114,
            ["avalanche"] = 43114,
            ["avalanche-fuji"] = 43113,

            // Arbitrum
            ["arbitrum-mainnet"] = 42161,
            ["arbitrum"] = 42161,
            ["arbitrum-sepolia"] = 421614,

            // Optimism
            ["optimism-mainnet"] = 10,
            ["optimism"] = 10,
            ["optimism-sepolia"] = 11155420
        };
    }
}
