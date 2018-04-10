using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using StandardToken.MyContractName.DTOs;
namespace StandardToken.MyContractName.CQS
{
    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunction:ContractMessage
    {

    }
}
