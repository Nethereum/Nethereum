using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor
{
    public partial class AppChainAnchorService: AppChainAnchorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, AppChainAnchorDeployment appChainAnchorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainAnchorDeployment>().SendRequestAndWaitForReceiptAsync(appChainAnchorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, AppChainAnchorDeployment appChainAnchorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<AppChainAnchorDeployment>().SendRequestAsync(appChainAnchorDeployment);
        }

        public static async Task<AppChainAnchorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, AppChainAnchorDeployment appChainAnchorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, appChainAnchorDeployment, cancellationTokenSource);
            return new AppChainAnchorService(web3, receipt.ContractAddress);
        }

        public AppChainAnchorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class AppChainAnchorServiceBase: ContractWeb3ServiceBase
    {

        public AppChainAnchorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> AnchorRequestAsync(AnchorFunction anchorFunction)
        {
             return ContractHandler.SendRequestAsync(anchorFunction);
        }

        public virtual Task<TransactionReceipt> AnchorRequestAndWaitForReceiptAsync(AnchorFunction anchorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(anchorFunction, cancellationToken);
        }

        public virtual Task<string> AnchorRequestAsync(BigInteger blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot)
        {
            var anchorFunction = new AnchorFunction();
                anchorFunction.BlockNumber = blockNumber;
                anchorFunction.StateRoot = stateRoot;
                anchorFunction.TxRoot = txRoot;
                anchorFunction.ReceiptRoot = receiptRoot;
            
             return ContractHandler.SendRequestAsync(anchorFunction);
        }

        public virtual Task<TransactionReceipt> AnchorRequestAndWaitForReceiptAsync(BigInteger blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot, CancellationTokenSource cancellationToken = null)
        {
            var anchorFunction = new AnchorFunction();
                anchorFunction.BlockNumber = blockNumber;
                anchorFunction.StateRoot = stateRoot;
                anchorFunction.TxRoot = txRoot;
                anchorFunction.ReceiptRoot = receiptRoot;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(anchorFunction, cancellationToken);
        }

        public virtual Task<AnchorsOutputDTO> AnchorsQueryAsync(AnchorsFunction anchorsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AnchorsFunction, AnchorsOutputDTO>(anchorsFunction, blockParameter);
        }

        public virtual Task<AnchorsOutputDTO> AnchorsQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var anchorsFunction = new AnchorsFunction();
                anchorsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AnchorsFunction, AnchorsOutputDTO>(anchorsFunction, blockParameter);
        }

        public Task<BigInteger> AppChainIdQueryAsync(AppChainIdFunction appChainIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AppChainIdFunction, BigInteger>(appChainIdFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> AppChainIdQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AppChainIdFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<GetAnchorOutputDTO> GetAnchorQueryAsync(GetAnchorFunction getAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetAnchorFunction, GetAnchorOutputDTO>(getAnchorFunction, blockParameter);
        }

        public virtual Task<GetAnchorOutputDTO> GetAnchorQueryAsync(BigInteger blockNumber, BlockParameter blockParameter = null)
        {
            var getAnchorFunction = new GetAnchorFunction();
                getAnchorFunction.BlockNumber = blockNumber;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetAnchorFunction, GetAnchorOutputDTO>(getAnchorFunction, blockParameter);
        }

        public Task<BigInteger> LatestBlockQueryAsync(LatestBlockFunction latestBlockFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LatestBlockFunction, BigInteger>(latestBlockFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> LatestBlockQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LatestBlockFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> SequencerQueryAsync(SequencerFunction sequencerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SequencerFunction, string>(sequencerFunction, blockParameter);
        }

        
        public virtual Task<string> SequencerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SequencerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> SetSequencerRequestAsync(SetSequencerFunction setSequencerFunction)
        {
             return ContractHandler.SendRequestAsync(setSequencerFunction);
        }

        public virtual Task<TransactionReceipt> SetSequencerRequestAndWaitForReceiptAsync(SetSequencerFunction setSequencerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSequencerFunction, cancellationToken);
        }

        public virtual Task<string> SetSequencerRequestAsync(string newSequencer)
        {
            var setSequencerFunction = new SetSequencerFunction();
                setSequencerFunction.NewSequencer = newSequencer;
            
             return ContractHandler.SendRequestAsync(setSequencerFunction);
        }

        public virtual Task<TransactionReceipt> SetSequencerRequestAndWaitForReceiptAsync(string newSequencer, CancellationTokenSource cancellationToken = null)
        {
            var setSequencerFunction = new SetSequencerFunction();
                setSequencerFunction.NewSequencer = newSequencer;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSequencerFunction, cancellationToken);
        }

        public Task<bool> VerifyAnchorQueryAsync(VerifyAnchorFunction verifyAnchorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyAnchorFunction, bool>(verifyAnchorFunction, blockParameter);
        }

        
        public virtual Task<bool> VerifyAnchorQueryAsync(BigInteger blockNumber, byte[] stateRoot, byte[] txRoot, byte[] receiptRoot, BlockParameter blockParameter = null)
        {
            var verifyAnchorFunction = new VerifyAnchorFunction();
                verifyAnchorFunction.BlockNumber = blockNumber;
                verifyAnchorFunction.StateRoot = stateRoot;
                verifyAnchorFunction.TxRoot = txRoot;
                verifyAnchorFunction.ReceiptRoot = receiptRoot;
            
            return ContractHandler.QueryAsync<VerifyAnchorFunction, bool>(verifyAnchorFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AnchorFunction),
                typeof(AnchorsFunction),
                typeof(AppChainIdFunction),
                typeof(GetAnchorFunction),
                typeof(LatestBlockFunction),
                typeof(SequencerFunction),
                typeof(SetSequencerFunction),
                typeof(VerifyAnchorFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AnchoredEventDTO),
                typeof(SequencerChangedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
