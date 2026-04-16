using Nethereum.EVM.Types;
#if !EVM_SYNC
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
#endif
using Nethereum.Util;
using System;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class ProgramResult
    {
        public byte[] Result { get; set;}
        public byte[] LastCallReturnData { get; set; }
#if EVM_SYNC
        public List<EvmLog> Logs { get; set; } = new List<EvmLog>();
#else
        public List<FilterLog> Logs { get; set; } = new List<FilterLog>();
#endif
        public bool IsRevert { get; set; }
#if EVM_SYNC
        public byte[] GetRevertData() => Result;
#else
        public string GetRevertMessage()
        {
            if (!IsRevert) return null;
            if( Result == null ||  Result.Length < 5 ) return null;
            return new FunctionCallDecoder().DecodeFunctionErrorMessage(Result.ToHex(true));
        }
#endif
        public bool IsSelfDestruct { get; set; }
        public List<string> DeletedContractAccounts { get; set; } = new List<string>();
        public List<string> CreatedContractAccounts { get; set; } = new List<string>();
        public List<EvmCallContext> InnerCalls { get; set; } = new List<EvmCallContext>();
        public List<InnerCallResult> InnerCallResults { get; set; } = new List<InnerCallResult>();

        public Dictionary<string, List<ProgramInstruction>> InnerContractCodeCalls { get; set; } = new Dictionary<string, List<ProgramInstruction>>();

        public Exception Exception { get; set; }

        public void InsertInnerContractCodeIfDoesNotExist(string address, List<ProgramInstruction> programInstructions)
        {
            address = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();
            if(!InnerContractCodeCalls.ContainsKey(address))
            {
                InnerContractCodeCalls.Add(address, programInstructions);
            }
        }


    }
}
