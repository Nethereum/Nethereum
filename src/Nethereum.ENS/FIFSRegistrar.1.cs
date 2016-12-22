using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;

namespace DefaultNamespace
{
   public class FIFSRegistrarService
   {
        private readonly Web3 web3;

        public static string ABI = @"[{'constant':false,'inputs':[{'name':'subnode','type':'bytes32'},{'name':'owner','type':'address'}],'name':'register','outputs':[],'payable':false,'type':'function'},{'inputs':[{'name':'ensAddr','type':'address'},{'name':'node','type':'bytes32'}],'type':'constructor'}]";

        public static string BYTE_CODE = "0x60606040818152806101c4833960a0905251608051600080546c0100000000000000000000000080850204600160a060020a0319909116179055600181905550506101768061004e6000396000f3606060405260e060020a6000350463d22057a9811461001e575b610002565b34610002576100f4600435602435600154604080519182526020808301859052815192839003820183206000805494830181905283517f02571be3000000000000000000000000000000000000000000000000000000008152600481018390529351879592949193600160a060020a03909316926302571be3926024808201939182900301818787803b156100025760325a03f11561000257505060405151915050600160a060020a038116158015906100ea575033600160a060020a031681600160a060020a031614155b156100f657610002565b005b60008054600154604080517f06ab5923000000000000000000000000000000000000000000000000000000008152600481019290925260248201899052600160a060020a03888116604484015290519216926306ab59239260648084019382900301818387803b156100025760325a03f11561000257505050505050505056";

        public static Task<string> DeployContractAsync(Web3 web3, string addressFrom, string ensAddr, byte[] node, HexBigInteger gas = null, HexBigInteger valueAmount = null) 
        {
            return web3.Eth.DeployContract.SendRequestAsync(ABI, BYTE_CODE, addressFrom, gas, valueAmount , ensAddr, node);
        }

        private Contract contract;

        public FIFSRegistrarService(Web3 web3, string address)
        {
            this.web3 = web3;
            this.contract = web3.Eth.GetContract(ABI, address);
        }

        public Function GetFunctionRegister() {
            return contract.GetFunction("register");
        }



        public Task<string> RegisterAsync(string addressFrom, byte[] subnode, string owner, HexBigInteger gas = null, HexBigInteger valueAmount = null) {
            var function = GetFunctionRegister();
            return function.SendTransactionAsync(addressFrom, gas, valueAmount, subnode, owner);
        }



    }



}

