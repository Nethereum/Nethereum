using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using System;
using System.Numerics;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EvmSimulatorERC20Tests    
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EvmSimulatorERC20Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDeployToChain_CheckBalanceEvmSim_TransferSim()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var tokenDeployment = new TokenDeployment();
            tokenDeployment.InitialSupply = 10000;
            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TokenDeployment>().SendRequestAndWaitForReceiptAsync(tokenDeployment);
            var contractAddress = transactionReceiptDeployment.ContractAddress;
            var contractHandler = web3.Eth.GetContractHandler(contractAddress);

            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner =  EthereumClientIntegrationFixture.AccountAddress;
            var balanceOfFunctionReturn = await contractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction);
            Console.WriteLine(balanceOfFunctionReturn);

            //current block number
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var code = await web3.Eth.GetCode.SendRequestAsync(contractAddress); // runtime code;

            var callInput = balanceOfFunction.CreateCallInput(contractAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            var resultEncoded  = program.ProgramResult.Result;
            var result = new BalanceOfOutputDTO().DecodeOutput(resultEncoded.ToHex());

            var transferFunction = new TransferFunction();
            transferFunction.FromAddress = EthereumClientIntegrationFixture.AccountAddress;
            transferFunction.To = "0xd8da6bf26964af9d7eed9e03e53415d37aa96045";
            transferFunction.Value = 500;
            
            callInput = transferFunction.CreateCallInput(contractAddress);
            programContext = new ProgramContext(callInput, executionStateService);
            program = new Program(code.HexToByteArray(), programContext);
            await evmSimulator.ExecuteAsync(program);

            balanceOfFunction.Owner = "0xd8da6bf26964af9d7eed9e03e53415d37aa96045";
            callInput = balanceOfFunction.CreateCallInput(contractAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;
            
            programContext = new ProgramContext(callInput, executionStateService);
            program = new Program(code.HexToByteArray(), programContext);
            await evmSimulator.ExecuteAsync(program);
            resultEncoded = program.ProgramResult.Result;
            result = new BalanceOfOutputDTO().DecodeOutput(resultEncoded.ToHex());

            Assert.Equal(500, result.ReturnValue1);

        }


        public partial class TokenDeployment : TokenDeploymentBase
        {
            public TokenDeployment() : base(BYTECODE) { }
            public TokenDeployment(string byteCode) : base(byteCode) { }
        }

        public class TokenDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b5060405161061238038061061283398181016040528101906100329190610098565b8060008190555080600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002081905550506100eb565b600081519050610092816100d4565b92915050565b6000602082840312156100ae576100ad6100cf565b5b60006100bc84828501610083565b91505092915050565b6000819050919050565b600080fd5b6100dd816100c5565b81146100e857600080fd5b50565b610518806100fa6000396000f3fe608060405234801561001057600080fd5b50600436106100415760003560e01c806318160ddd1461004657806370a0823114610064578063a9059cbb14610094575b600080fd5b61004e6100c4565b60405161005b9190610393565b60405180910390f35b61007e600480360381019061007991906102ed565b6100cd565b60405161008b9190610393565b60405180910390f35b6100ae60048036038101906100a9919061031a565b610116565b6040516100bb9190610378565b60405180910390f35b60008054905090565b6000600160008373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020549050919050565b60008073ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff16141561015157600080fd5b600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000205482111561019d57600080fd5b81600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546101e89190610404565b600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000208190555081600160008573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000205461027691906103ae565b600160008573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055506001905092915050565b6000813590506102d2816104b4565b92915050565b6000813590506102e7816104cb565b92915050565b600060208284031215610303576103026104af565b5b6000610311848285016102c3565b91505092915050565b60008060408385031215610331576103306104af565b5b600061033f858286016102c3565b9250506020610350858286016102d8565b9150509250929050565b6103638161044a565b82525050565b61037281610476565b82525050565b600060208201905061038d600083018461035a565b92915050565b60006020820190506103a86000830184610369565b92915050565b60006103b982610476565b91506103c483610476565b9250827fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff038211156103f9576103f8610480565b5b828201905092915050565b600061040f82610476565b915061041a83610476565b92508282101561042d5761042c610480565b5b828203905092915050565b600061044382610456565b9050919050565b60008115159050919050565b600073ffffffffffffffffffffffffffffffffffffffff82169050919050565b6000819050919050565b7f4e487b7100000000000000000000000000000000000000000000000000000000600052601160045260246000fd5b600080fd5b6104bd81610438565b81146104c857600080fd5b50565b6104d481610476565b81146104df57600080fd5b5056fea26469706673582212200d8da631c5caac28cf4381f8bc52fa949c90fd86d5d7f3ad9fc86adfaced5b5764736f6c63430008070033";
            public TokenDeploymentBase() : base(BYTECODE) { }
            public TokenDeploymentBase(string byteCode) : base(byteCode) { }
            [Parameter("uint256", "_initialSupply", 1)]
            public virtual BigInteger InitialSupply { get; set; }
        }

        public partial class BalanceOfFunction : BalanceOfFunctionBase { }

        [Function("balanceOf", "uint256")]
        public class BalanceOfFunctionBase : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public virtual string Owner { get; set; }
        }

        public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

        [Function("totalSupply", "uint256")]
        public class TotalSupplyFunctionBase : FunctionMessage
        {

        }

        public partial class TransferFunction : TransferFunctionBase { }

        [Function("transfer", "bool")]
        public class TransferFunctionBase : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public virtual string To { get; set; }
            [Parameter("uint256", "_value", 2)]
            public virtual BigInteger Value { get; set; }
        }

        public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

        [FunctionOutput]
        public class BalanceOfOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public virtual BigInteger ReturnValue1 { get; set; }
        }

        public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

        [FunctionOutput]
        public class TotalSupplyOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("uint256", "", 1)]
            public virtual BigInteger ReturnValue1 { get; set; }
        }
    }
}