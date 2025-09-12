using Nethereum.RPC.HostWallet;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Chain
{
    public static class ChainFeatureExtensions
    {
        public static ChainFeature ToChainFeature(this AddEthereumChainParameter param)
        {
            return ChainFeature.FromAddEthereumChainParameter(param);
        }
    }


    public class ChainFeature
    {
        public BigInteger ChainId { get; set; }
        public bool SupportEIP1559 { get; set; } = true;
        public bool SupportEIP155 { get; set; } = true;
        public bool IsTestnet { get; set; } = false;
        public NativeCurrency NativeCurrency { get; set; } = new NativeCurrency();
        public string ChainName { get; set; }
        public List<string> HttpRpcs { get; set; } = new List<string>();
        public List<string> WsRpcs { get; set; } = new List<string>();
        public List<string> Explorers { get; set; } = new List<string>();

        public AddEthereumChainParameter ToAddEthereumChainParameter()
        {
            return new AddEthereumChainParameter()
            {
                ChainId = new HexBigInteger(ChainId),
                BlockExplorerUrls = Explorers,
                ChainName = ChainName,
                IconUrls = new List<string>(),
                NativeCurrency = NativeCurrency.ToRPCNativeCurrency(),
                RpcUrls = HttpRpcs
            };
        }

        public static ChainFeature FromAddEthereumChainParameter(AddEthereumChainParameter param)
        {
            return new ChainFeature
            {
                ChainId = param.ChainId.Value,
                ChainName = param.ChainName,
                HttpRpcs = param.RpcUrls ?? new List<string>(),
                Explorers = param.BlockExplorerUrls ?? new List<string>(),
                NativeCurrency = new NativeCurrency
                {
                    Name = param.NativeCurrency?.Name,
                    Symbol = param.NativeCurrency?.Symbol,
                    Decimals = (int)(param.NativeCurrency?.Decimals ?? 18)
                }
            };
        }
    }

    public class NativeCurrency
    {
        public string Name { get; set; }
        public string Symbol { get; set; } 
        public int Decimals { get; set; }

        public RPC.HostWallet.NativeCurrency ToRPCNativeCurrency()
        {
            return new RPC.HostWallet.NativeCurrency()
            {
                Name = Name,
                Symbol = Symbol,
                Decimals = (uint)Decimals
            };
        }   
    }

    /// <summary>
    /// Chain categorization constants for testnet and L2 identification
    /// </summary>
    public static class ChainCategories
    {
        /// <summary>
        /// Known testnet chain IDs
        /// </summary>
        public static readonly BigInteger[] TestnetChainIds = 
        {
            new BigInteger(5),        // Goerli
            new BigInteger(11155111), // Sepolia
            new BigInteger(17000),    // Holesky
            new BigInteger(80001),    // Mumbai
            new BigInteger(80002),    // Amoy
            new BigInteger(97),       // BSC Testnet
            new BigInteger(421611),   // Arbitrum Rinkeby
            new BigInteger(421613),   // Arbitrum Goerli
            new BigInteger(421614),   // Arbitrum Sepolia
            new BigInteger(420),      // Optimism Goerli
            new BigInteger(11155420), // Optimism Sepolia
            new BigInteger(84531),    // Base Goerli
            new BigInteger(84532),    // Base Sepolia
            new BigInteger(43113),    // Fuji
            new BigInteger(4002),     // Fantom Testnet
            new BigInteger(10200),    // Gnosis Chiado
            new BigInteger(280),      // zkSync Era Testnet
            new BigInteger(300),      // zkSync Era Sepolia
            new BigInteger(59140),    // Linea Goerli
        };
        
        /// <summary>
        /// Known Layer 2 chain IDs
        /// </summary>
        public static readonly BigInteger[] L2ChainIds =
        {
            new BigInteger(10),       // Optimism
            new BigInteger(8453),     // Base
            new BigInteger(7777777),  // Zora
            new BigInteger(291),      // Orderly Network
            new BigInteger(42161),    // Arbitrum One
            new BigInteger(42170),    // Arbitrum Nova
            new BigInteger(137),      // Polygon
            new BigInteger(1101),     // Polygon zkEVM
            new BigInteger(324),      // zkSync Era
            new BigInteger(534352),   // Scroll
            new BigInteger(59144),    // Linea
            new BigInteger(5000),     // Mantle
            new BigInteger(81457),    // Blast
            new BigInteger(34443),    // Mode
            new BigInteger(1088),     // Metis Andromeda
            new BigInteger(13371),    // Immutable zkEVM
            new BigInteger(288),      // Boba Network
            new BigInteger(1284),     // Moonbeam
            new BigInteger(1285),     // Moonriver
            new BigInteger(42220),    // Celo
            new BigInteger(11297108109), // Palm Network
        };
        
        /// <summary>
        /// Checks if a chain ID represents a testnet
        /// </summary>
        public static bool IsTestnet(BigInteger chainId) => 
            TestnetChainIds.Contains(chainId);
            
        /// <summary>
        /// Checks if a chain ID represents a Layer 2 network
        /// </summary>
        public static bool IsL2(BigInteger chainId) => 
            L2ChainIds.Contains(chainId);
            
        /// <summary>
        /// Checks if a chain ID represents a mainnet (not testnet)
        /// </summary>
        public static bool IsMainnet(BigInteger chainId) => 
            !IsTestnet(chainId);
    }

}
