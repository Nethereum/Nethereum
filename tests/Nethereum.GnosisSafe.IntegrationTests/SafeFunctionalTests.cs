using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
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

            var encodedMessage = Eip712TypedDataEncoder.Current.EncodeTypedData(param, domain, "SafeTx");
            Assert.Equal(encoded.ToHex(), encodedMessage.ToHex());

        }

       

    }

    public class Test
    {
        [Fact]
        public void ShouldDecode()
        {
            var contractABI = ABIDeserialiserFactory.DeserialiseContractABI(abi);
            var function = contractABI.Functions.Where(f => f.Name == "createSplit").FirstOrDefault();
            var functionBuilder = new FunctionBuilder("", function);
            var decoded = functionBuilder.DecodeInput(json).ConvertToJObject().ToString();

        }

        [Fact]
        public void GenerateHash()
        {
            var hashBytes = GnosisSafeService.GetEncodedTransactionDataHash(json, 1, "0x2cF5869Eac6D7809DcF867c0e5cae1E9c5648e70");

            var hexBytes = hashBytes.ToHex();



        }

        public string json = "{\r\n  \"to\": \"0x5cbA88D55Cec83caD5A105Ad40C8c9aF20bE21d1\",\r\n  \"data\": \"0x2556fa3900000000000000000000000000000000000000000000000000000000000000600000000000000000000000002cf5869eac6d7809dcf867c0e5cae1e9c5648e7000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000038000000000000000000000000000000000000000000000000000000000000f4240000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000170000000000000000000000001990a6bcdb13d33463cba884a1ae6020292523e80000000000000000000000001a2d838c4bbd1e73d162d0777d142c1d783cb83100000000000000000000000026cd8de09cc721b13d30e4d31618f31275c884e70000000000000000000000002cf5869eac6d7809dcf867c0e5cae1e9c5648e700000000000000000000000002e11e3b40ca0c4aba93a2cd7c9046394b8dd75010000000000000000000000002e477a105f00ee818c9cf1f63d84ebc55c7083750000000000000000000000002f64996f0360507fdad59bd108ca46506b8c54800000000000000000000000003a45d3c998476af7191588332b8a49a8ad8cfbe6000000000000000000000000402f31e1d0ef18669a523ac17c06fd564c43869900000000000000000000000045334f41aaa464528cd5bc0f582acadc49eb0cd10000000000000000000000005597cd8d55d2db56b10ff4f8fe69c8922bf6c537000000000000000000000000643aa0a61eadcc9cc202d1915d942d35d005400c0000000000000000000000008cd78a945c6d209fe8c7ed8ee1712f43bec177810000000000000000000000009345946d10ba98525e33e263df9362ae7f9e5ea5000000000000000000000000b21170472acc742d2e788904641c9d4c76261a84000000000000000000000000b4f53bd85c00ef22946d24ae26bc38ac64f5e7b1000000000000000000000000b7fb92b17b25aa8ede37f80521bb383c9e203e6b000000000000000000000000cf6e68b4ddf458174dbc6e83ee6cd0c159ec951c000000000000000000000000d2135cfb216b74109775236e36d4b433f1df507b000000000000000000000000db54850bcf3e7b375ca6a96608fa237bc65eab06000000000000000000000000eeee5d271a56aa09c4f8862af514add3e882857c000000000000000000000000f62314278961f1bdf26cc0e7edd1048c26c01c2d000000000000000000000000fe22a47f78aea19489d953c1e1608ab2bd6aa48f000000000000000000000000000000000000000000000000000000000000001700000000000000000000000000000000000000000000000000000000000052770000000000000000000000000000000000000000000000000000000000005277000000000000000000000000000000000000000000000000000000000000f765000000000000000000000000000000000000000000000000000000000000c353000000000000000000000000000000000000000000000000000000000000a4ee000000000000000000000000000000000000000000000000000000000000f765000000000000000000000000000000000000000000000000000000000000a4ee0000000000000000000000000000000000000000000000000000000000005277000000000000000000000000000000000000000000000000000000000000527700000000000000000000000000000000000000000000000000000000000052770000000000000000000000000000000000000000000000000000000000005277000000000000000000000000000000000000000000000000000000000000a4ee000000000000000000000000000000000000000000000000000000000000a4ee00000000000000000000000000000000000000000000000000000000000052770000000000000000000000000000000000000000000000000000000000005277000000000000000000000000000000000000000000000000000000000000527700000000000000000000000000000000000000000000000000000000000052770000000000000000000000000000000000000000000000000000000000005277000000000000000000000000000000000000000000000000000000000000f7650000000000000000000000000000000000000000000000000000000000005277000000000000000000000000000000000000000000000000000000000000527700000000000000000000000000000000000000000000000000000000000482840000000000000000000000000000000000000000000000000000000000005277\",\r\n  \"value\": \"0\",\r\n  \"operation\": 0,\r\n  \"baseGas\": \"0\",\r\n  \"gasPrice\": \"0\",\r\n  \"gasToken\": \"0x0000000000000000000000000000000000000000\",\r\n  \"nonce\": 1,\r\n  \"refundReceiver\": \"0x0000000000000000000000000000000000000000\",\r\n  \"safeTxGas\": \"0\"\r\n}";

        public string abi = "[{\"inputs\":[{\"internalType\":\"address\",\"name\":\"_splitsWarehouse\",\"type\":\"address\"}],\"stateMutability\":\"nonpayable\",\"type\":\"constructor\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"address\",\"name\":\"split\",\"type\":\"address\"},{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"indexed\":false,\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"splitParams\",\"type\":\"tuple\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"owner\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"creator\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"bytes32\",\"name\":\"salt\",\"type\":\"bytes32\"}],\"name\":\"SplitCreated\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"address\",\"name\":\"split\",\"type\":\"address\"},{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"indexed\":false,\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"splitParams\",\"type\":\"tuple\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"owner\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"creator\",\"type\":\"address\"}],\"name\":\"SplitCreated\",\"type\":\"event\"},{\"inputs\":[],\"name\":\"SPLIT_WALLET_IMPLEMENTATION\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"_splitParams\",\"type\":\"tuple\"},{\"internalType\":\"address\",\"name\":\"_owner\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"_creator\",\"type\":\"address\"}],\"name\":\"createSplit\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"split\",\"type\":\"address\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"_splitParams\",\"type\":\"tuple\"},{\"internalType\":\"address\",\"name\":\"_owner\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"_creator\",\"type\":\"address\"},{\"internalType\":\"bytes32\",\"name\":\"_salt\",\"type\":\"bytes32\"}],\"name\":\"createSplitDeterministic\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"split\",\"type\":\"address\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"_splitParams\",\"type\":\"tuple\"},{\"internalType\":\"address\",\"name\":\"_owner\",\"type\":\"address\"},{\"internalType\":\"bytes32\",\"name\":\"_salt\",\"type\":\"bytes32\"}],\"name\":\"isDeployed\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"split\",\"type\":\"address\"},{\"internalType\":\"bool\",\"name\":\"exists\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"_hash\",\"type\":\"bytes32\"}],\"name\":\"nonces\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"_splitParams\",\"type\":\"tuple\"},{\"internalType\":\"address\",\"name\":\"_owner\",\"type\":\"address\"}],\"name\":\"predictDeterministicAddress\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"components\":[{\"internalType\":\"address[]\",\"name\":\"recipients\",\"type\":\"address[]\"},{\"internalType\":\"uint256[]\",\"name\":\"allocations\",\"type\":\"uint256[]\"},{\"internalType\":\"uint256\",\"name\":\"totalAllocation\",\"type\":\"uint256\"},{\"internalType\":\"uint16\",\"name\":\"distributionIncentive\",\"type\":\"uint16\"}],\"internalType\":\"struct SplitV2Lib.Split\",\"name\":\"_splitParams\",\"type\":\"tuple\"},{\"internalType\":\"address\",\"name\":\"_owner\",\"type\":\"address\"},{\"internalType\":\"bytes32\",\"name\":\"_salt\",\"type\":\"bytes32\"}],\"name\":\"predictDeterministicAddress\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";

    }
}
