using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class GethCallTest
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public GethCallTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }
        //curl --data '{"method":"eth_call","params":[{"to":"0xd0828aeb00e4db6813e2f330318ef94d2bba2f60","input":"0x893d20e8"}, "latest", {"0xd0828aeb00e4db6813e2f330318ef94d2bba2f60": {"code":"0x6080604052348015600f57600080fd5b506004361060285760003560e01c8063893d20e814602d575b600080fd5b600054604080516001600160a01b039092168252519081900360200190f3fea2646970667358221220dbb42870b3edb8a876ba4948e51e4e2b8fe47ae467ed612734a355d3dcf676dc64736f6c63430008040033"}}],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X POST 192.168.2.153:8545
        [Fact]
        public async void ShouldBeAbleToReplaceContractToAccessState()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var account = EthereumClientIntegrationFixture.GetAccount();
                var web3 = new Web3Geth(account, _ethereumClientIntegrationFixture.GetHttpUrl());

                var deploymentMessage = new SimpleStorageDeployment()
                    {Owner = EthereumClientIntegrationFixture.AccountAddress};

                var deploymentHandler = web3.Eth.GetContractDeploymentHandler<SimpleStorageDeployment>();
                var deploymentReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage).ConfigureAwait(false);

                var stateChanges = new Dictionary<string, StateChange>();
                stateChanges.Add(deploymentReceipt.ContractAddress,
                    new StateChange() {Code = SimpleStorage2DeployedByteCode.EnsureHexPrefix()});
                var result = await web3.GethEth.Call.SendRequestAsync(
                    new GetOwnerFunction().CreateTransactionInput(deploymentReceipt.ContractAddress),
                    BlockParameter.CreateLatest(), stateChanges).ConfigureAwait(false);
                var output = new GetOwnerFunctionOutput();
                output = output.DecodeOutput(result);
                Assert.True(output.Owner.IsTheSameAddress(EthereumClientIntegrationFixture.AccountAddress));
            }
        }
        //Original contract
        /*
            pragma solidity >=0.5.0 <0.9.0;
            contract SimpleStorage {
                address private owner;
                constructor(address _owner)
                {
                    owner = _owner;
                }
            }

        */

        public class SimpleStorageDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE = "6080604052348015600f57600080fd5b5060405160c638038060c6833981016040819052602a91604e565b600080546001600160a01b0319166001600160a01b0392909216919091179055607a565b600060208284031215605e578081fd5b81516001600160a01b03811681146073578182fd5b9392505050565b603f8060876000396000f3fe6080604052600080fdfea26469706673582212206baffd7b6dc8e96c3a57d1dad9d24e01daa26556489015858d60e5ffdceaeead64736f6c63430008040033";
            public SimpleStorageDeployment() : base(BYTECODE) { }
            public SimpleStorageDeployment(string byteCode) : base(byteCode) { }
            [Parameter("address", "_owner", 1)]
            public virtual string Owner { get; set; }
        }

        //Replacement contract

        /*
        pragma solidity >=0.5.0 <0.9.0;
        contract SimpleStorage2 {
            address private owner;
            function getOwner() public view returns(address _owner) {
                return owner;
            }
        }

        */
        public static string SimpleStorage2DeployedByteCode =
            "6080604052348015600f57600080fd5b506004361060285760003560e01c8063893d20e814602d575b600080fd5b600054604080516001600160a01b039092168252519081900360200190f3fea2646970667358221220dbb42870b3edb8a876ba4948e51e4e2b8fe47ae467ed612734a355d3dcf676dc64736f6c63430008040033";

        [Function("getOwner", "address")]
        public class GetOwnerFunction : FunctionMessage
        {

        }

        [FunctionOutput]
        public class GetOwnerFunctionOutput : IFunctionOutputDTO
        {
            [Parameter("address", "_owner", 1)]
            public virtual string Owner { get; set; }
        }
    }
}