using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Nethereum.RPC.Eth;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.Contracts.Create2Deployment
{

    /// <summary>
    /// Deterministic Deployment Proxy Deployment support https://github.com/Arachnid/deterministic-deployment-proxy.git and extended to support EIP155
    /// 
    /// Use in combination with the Create2DeterministicDeploymentProxyService to create EIP155 create2 deployments
    /// 
    /// The default values are created using the deterministic-deployment-proxy tool
    /// git clone https://github.com/Arachnid/deterministic-deployment-proxy.git
    /// cd deterministic-deployment-proxy
    /// npm install
    /// npm run build 
    /// you can find the raw transaction and the bytecode in the output folder
    /// 
    /// EIP155 support is added by using the ChainId to calculate the V value and Legacy transaction signing
    /// </summary>

    public class Create2DeterministicDeploymentProxyDeployment
    {
        public const string DefaultRawTransaction = "f8a58085174876e800830186a08080b853604580600e600039806000f350fe7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf31ba02222222222222222222222222222222222222222222222222222222222222222a02222222222222222222222222222222222222222222222222222222222222222";
        public const string DefaultSignerAddress = "3fab184622dc19b6109349b94811493bf2a45362";
        public const string DefaultAddress = "4e59b44847b379578588920ca78fbf26c0b4956c";
        public const long DefaultGasPrice = 100000000000;
        public const long DefaultGasLimit = 100000;
        public static readonly byte DefaultV = 21;
        public static int DefaultRecId = 1;
        public static BigInteger CalculateVForChainId(BigInteger chainId)
        {
            return VRecoveryAndChainCalculations.CalculateV(chainId, DefaultRecId);
        }

        public static byte[] CalculateVForChainIdAsBytes(BigInteger chainId)
        {
            return CalculateVForChainId(chainId).ToBytesForRLPEncoding();  
        }
        public static readonly byte[] DefaultR = "2222222222222222222222222222222222222222222222222222222222222222".HexToByteArray();
        public static readonly byte[] DefaultS = "2222222222222222222222222222222222222222222222222222222222222222".HexToByteArray();
       
        public const string ByteCode = "604580600e600039806000f350fe7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf3";
        public const string RuntimeByteCode = "7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf3";
        public string RawTransaction { get; set; }
        public string SignerAddress { get; set; }
        public string Address { get; set; }
        public long GasPrice { get; set; } = DefaultGasPrice;
        public long GasLimit { get; set; } = DefaultGasLimit;
        public BigInteger? ChainId { get; set; }

        public static Create2DeterministicDeploymentProxyDeployment GetDefaultNoneEIP155Create2ContractDeployerService()
        {
            return new Create2DeterministicDeploymentProxyDeployment()
            {
                RawTransaction = DefaultRawTransaction,
                SignerAddress = DefaultSignerAddress,
                Address = DefaultAddress
            };
        }
    }
}
