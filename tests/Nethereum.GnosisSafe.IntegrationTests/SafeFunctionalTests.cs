using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.GnosisSafe.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.GnosisSafe.IntegrationTests
{
       [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
        public class SafeFunctionalTests
        {
            private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

            public SafeFunctionalTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
            {
                _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
            }
            
            
            [Fact]
        public async void ShouldBeAbleToEncodeTheSameAsTheSmartContract()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Rinkeby);
            var signer = new Eip712TypedDataSigner();
            var gnosisSafeAddress = "0xa9C09412C1d93DAc6eE9254A51E97454588D3B88";
            var chainId = (int)Chain.Rinkeby;
            var service = new GnosisSafeService(web3, gnosisSafeAddress);
            var param = new EncodeTransactionDataFunction
            {
                To = "0x40A2aCCbd92BCA938b02010E17A5b8929b49130D",
                Value = 0,
                Data = "0x40A2aCCbd92BCA938b02010E17A5b8929b49130D".HexToByteArray(),
                Operation = (byte)ContractOperationType.Call,
                SafeTxGas = 0,
                BaseGas = 0,
                GasPrice = 0,
                GasToken = AddressUtil.AddressEmptyAsHex,
                RefundReceiver = AddressUtil.AddressEmptyAsHex,
                Nonce = 1
            };
            var encoded = await service.EncodeTransactionDataQueryAsync(param).ConfigureAwait(false);

            var domain = new GnosisSafeEIP712Domain
            {
                VerifyingContract = gnosisSafeAddress,
                ChainId = chainId
            };

            var encodedMessage = signer.EncodeTypedData(param, domain, "SafeTx");
            Assert.Equal(encoded.ToHex(), encodedMessage.ToHex());

        }
    }
}
