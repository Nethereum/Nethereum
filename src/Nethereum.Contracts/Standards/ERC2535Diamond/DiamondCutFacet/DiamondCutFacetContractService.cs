using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.Standards.ERC2535Diamond.DiamondCutFacet.ContractDefinition;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Runtime.CompilerServices;
using System;

namespace Nethereum.Contracts.Standards.ERC2535Diamond.DiamondCutFacet
{
    public enum FacetCutAction
    {
        Add = 0,
        Replace = 1,
        Remove = 2
    }

    public class DiamondCutFacetContractService: ContractServiceBase
    {

        public DiamondCutFacetContractService(IEthApiContractService ethApiContractService, string contractAddress)
        {
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

        public Task<string> DiamondCutRequestAsync(DiamondCutFunction diamondCutFunction)
        {
            return ContractHandler.SendRequestAsync(diamondCutFunction);
        }

        public Task<TransactionReceipt> DiamondCutRequestAndWaitForReceiptAsync(DiamondCutFunction diamondCutFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(diamondCutFunction, cancellationToken);
        }

        public Task<string> DiamondCutRequestAsync(List<FacetCut> diamondCut, string init, byte[] calldata)
        {
            var diamondCutFunction = new DiamondCutFunction();
            diamondCutFunction.DiamondCut = diamondCut;
            diamondCutFunction.Init = init;
            diamondCutFunction.Calldata = calldata;

            return ContractHandler.SendRequestAsync(diamondCutFunction);
        }

        public Task<string> DiamondCutAddRequestAsync(List<string> signatures, string address, string addressInit = null, byte[] callData = null)
        {
            var diamondCutFunction = new DiamondCutFunction();
            diamondCutFunction.DiamondCut = new List<FacetCut>() { ERC2535DiamondFacetCutFactory.CreateAddFacetCut(address, signatures.ToArray()) };

            InitialiseAddressAndCallData(addressInit, callData, diamondCutFunction);

            return ContractHandler.SendRequestAsync(diamondCutFunction);
        }

        public static void InitialiseAddressAndCallData(string addressInit, byte[] callData, DiamondCutFunction diamondCutFunction)
        {
            if (!string.IsNullOrEmpty(addressInit))
            {
                diamondCutFunction.Init = addressInit;
            }
            else
            {
                diamondCutFunction.Init = AddressUtil.ZERO_ADDRESS;
            }

            if (callData != null)
            {
                diamondCutFunction.Calldata = callData;
            }
            else
            {
                diamondCutFunction.Calldata = new byte[0];

            }
        }

        public Task<TransactionReceipt> DiamondCutRequestAndWaitForReceiptAsync(List<FacetCut> diamondCut, string init, byte[] calldata, CancellationTokenSource cancellationToken = null)
        {
            var diamondCutFunction = new DiamondCutFunction();
            diamondCutFunction.DiamondCut = diamondCut;
            diamondCutFunction.Init = init;
            diamondCutFunction.Calldata = calldata;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(diamondCutFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
           return new List<Type>
           {
               typeof(DiamondCutFunction)
           };
        }

        public override List<Type> GetAllEventTypes()
        {
           return new List<Type>
           {
               typeof(DiamondCutEventDTO)
           };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>();
        }
    }
}
