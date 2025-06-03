using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanBalanceMultiResponse : EtherscanBalanceResponse { }
}