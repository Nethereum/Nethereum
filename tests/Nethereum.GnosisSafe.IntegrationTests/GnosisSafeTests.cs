using System.Numerics;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.GnosisSafe.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.EIP712;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using InfuraNetwork = Nethereum.XUnitEthereumClients.InfuraNetwork;

namespace Nethereum.GnosisSafe.Contracts.Testing
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class GnosisSafeTest
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public GnosisSafeTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        
        //[Fact] //replace private keys to avoid issues
        public async void ShouldDoMultiTransferTokensUsingARelayer()
        {
            var walletOwnerPrivateKey = "0xcf0d584dba3902252f3762d5161c4996f4b36";
            var accountRelayerReceiverPrivateKey = "0xa261c1c7f775c3423be58cdb8c24c6e29f898af56";
            var chainId = Chain.Rinkeby;
            var gnosisSafeAddress = "0xa9C09412C1d93DAc6eE9254A51E97454588D3B88";
            var daiAddress = "0x6a9865ade2b6207daac49f8bcba9705deb0b0e6d";
            var multiSendAddress = "0x40A2aCCbd92BCA938b02010E17A5b8929b49130D";

            var accountRelayerReceiver = new Account(accountRelayerReceiverPrivateKey, chainId);

            var web3Url = _ethereumClientIntegrationFixture.GetInfuraUrl(InfuraNetwork.Rinkeby);
            var web3 = new Web3.Web3(accountRelayerReceiver, web3Url);
            var service = new GnosisSafeService(web3, gnosisSafeAddress);

            var transfer = new TransferFunction()
            {
                To = accountRelayerReceiver.Address,
                Value = Web3.Web3.Convert.ToWei(1)
            };

            var multiSendFunction = new MultiSendFunction(new MultiSendFunctionInput<TransferFunction>(transfer, daiAddress),
                new MultiSendFunctionInput<TransferFunction>(transfer, daiAddress));
            
            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            var execTransactionFunction = await service.BuildTransactionAsync(
                new EncodeTransactionDataFunction() { To = multiSendAddress, Operation = (int)ContractOperationType.DelegateCall}, multiSendFunction, (int)chainId, false,
                walletOwnerPrivateKey);

            //legacy
            execTransactionFunction.GasPrice = gasPrice;
            var tokenService = new StandardTokenEIP20.StandardTokenService(web3, daiAddress);
            var balanceBefore = await tokenService.BalanceOfQueryAsync(accountRelayerReceiver.Address);



            var receipt = await service.ExecTransactionRequestAndWaitForReceiptAsync(execTransactionFunction);
            Assert.False(receipt.HasErrors());
            var balanceAfter = await tokenService.BalanceOfQueryAsync(accountRelayerReceiver.Address);
            Assert.Equal(Web3.Web3.Convert.FromWei(balanceBefore) + 2, Web3.Web3.Convert.FromWei(balanceAfter));

        }



        //[Fact] //replace private keys to avoid issues

        public async void ShouldTransferTokensUsingARelayer()
        {
            var walletOwnerPrivateKey = "0xcf0d584dba3902252f3762d5161c4996f4b364e6e7";
            var accountRelayerReceiverPrivateKey = "0xa261c1c7f775c3423be58cdb8c24c6e29f898af56";
            var chainId = Chain.Rinkeby;
            var gnosisSafeAddress = "0xa9C09412C1d93DAc6eE9254A51E97454588D3B88";
            var daiAddress = "0x6a9865ade2b6207daac49f8bcba9705deb0b0e6d";

            var accountRelayerReceiver = new Account(accountRelayerReceiverPrivateKey, chainId);

            var web3Url = _ethereumClientIntegrationFixture.GetInfuraUrl(InfuraNetwork.Rinkeby);
            var web3 = new Web3.Web3(accountRelayerReceiver, web3Url);
            var service = new GnosisSafeService(web3, gnosisSafeAddress);

            var transfer = new TransferFunction()
            {
                To = accountRelayerReceiver.Address,
                Value = Web3.Web3.Convert.ToWei(1)
            };

            var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();

            var execTransactionFunction = await service.BuildTransactionAsync(
                new EncodeTransactionDataFunction() { To = daiAddress }, transfer, (int)chainId, false,
                walletOwnerPrivateKey);

            //legacy
            execTransactionFunction.GasPrice = gasPrice;
            var tokenService = new StandardTokenEIP20.StandardTokenService(web3, daiAddress);
            var balanceBefore = await tokenService.BalanceOfQueryAsync(accountRelayerReceiver.Address);


            
            var receipt = await service.ExecTransactionRequestAndWaitForReceiptAsync(execTransactionFunction);
            Assert.False(receipt.HasErrors());
            var balanceAfter = await tokenService.BalanceOfQueryAsync(accountRelayerReceiver.Address);
            Assert.Equal(Web3.Web3.Convert.FromWei(balanceBefore) + 1, Web3.Web3.Convert.FromWei(balanceAfter));

        }


    }
}