using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using System;
using System.Numerics;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.EVM
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EvmSimulatorERC20Tests820PushZero
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public EvmSimulatorERC20Tests820PushZero(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
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
            balanceOfFunction.Owner = EthereumClientIntegrationFixture.AccountAddress;
            var balanceOfFunctionReturn = await contractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction);
            Console.WriteLine(balanceOfFunctionReturn);

            //current block number
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var code = await web3.Eth.GetCode.SendRequestAsync(contractAddress); // runtime code;

            var callInput = balanceOfFunction.CreateCallInput(contractAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;
            callInput.ChainId = new HexBigInteger(EthereumClientIntegrationFixture.ChainId);

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            var resultEncoded = program.ProgramResult.Result;
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
            public static string BYTECODE = "60a060405234801561000f575f80fd5b506040516104df3803806104df83398101604081905261002e91610047565b6080819052335f9081526020819052604090205561005e565b5f60208284031215610057575f80fd5b5051919050565b60805161046a6100755f395f60bd015261046a5ff3fe608060405234801561000f575f80fd5b506004361061007a575f3560e01c8063313ce56711610058578063313ce5671461011057806370a082311461012a57806395d89b4114610152578063a9059cbb14610174575f80fd5b806306fdde031461007e57806318160ddd146100b857806323b872dd146100ed575b5f80fd5b6100a260405180604001604052806005815260200164045524332360dc1b81525081565b6040516100af9190610313565b60405180910390f35b6100df7f000000000000000000000000000000000000000000000000000000000000000081565b6040519081526020016100af565b6101006100fb366004610379565b610187565b60405190151581526020016100af565b610118601281565b60405160ff90911681526020016100af565b6100df6101383660046103b2565b6001600160a01b03165f9081526020819052604090205490565b6100a26040518060400160405280600381526020016245524360e81b81525081565b6101006101823660046103d2565b610259565b6001600160a01b0383165f908152602081905260408120548211156101aa575f80fd5b6001600160a01b0384165f908152602081905260409020546101cd90839061040e565b6001600160a01b038086165f9081526020819052604080822093909355908516815220546101fc908390610421565b6001600160a01b038481165f818152602081815260409182902094909455518581529092918716917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef910160405180910390a35060019392505050565b335f90815260208190526040812054821115610273575f80fd5b335f9081526020819052604090205461028d90839061040e565b335f90815260208190526040808220929092556001600160a01b038516815220546102b9908390610421565b6001600160a01b0384165f81815260208181526040918290209390935551848152909133917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef910160405180910390a35060015b92915050565b5f6020808352835180828501525f5b8181101561033e57858101830151858201604001528201610322565b505f604082860101526040601f19601f8301168501019250505092915050565b80356001600160a01b0381168114610374575f80fd5b919050565b5f805f6060848603121561038b575f80fd5b6103948461035e565b92506103a26020850161035e565b9150604084013590509250925092565b5f602082840312156103c2575f80fd5b6103cb8261035e565b9392505050565b5f80604083850312156103e3575f80fd5b6103ec8361035e565b946020939093013593505050565b634e487b7160e01b5f52601160045260245ffd5b8181038181111561030d5761030d6103fa565b8082018082111561030d5761030d6103fa56fea264697066735822122001e3cb289326f5ec5fe4f3bb1e9f2250bd6a5926c1e0d624d683270888f930a664736f6c63430008140033";
            public TokenDeploymentBase() : base(BYTECODE) { }
            public TokenDeploymentBase(string byteCode) : base(byteCode) { }
            [Parameter("uint256", "total", 1)]
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