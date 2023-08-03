using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class ProgramResult
    {
        public byte[] Result { get; set;}
        public List<FilterLog> Logs { get; set; } = new List<FilterLog>();
        public bool IsRevert { get; set; }
        public string GetRevertMessage()
        {
            if (!IsRevert) return null;
            if( Result == null ||  Result.Length < 5 ) return null;
            return new FunctionCallDecoder().DecodeFunctionErrorMessage(Result.ToHex(true));
        }
        public bool IsSelfDestruct { get; set; }
        public List<string> DeletedContractAccounts { get; set; } = new List<string>();
        public List<string> CreatedContractAccounts { get; set; } = new List<string>();
        public List<CallInput> InnerCalls { get; set; } = new List<CallInput>();

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