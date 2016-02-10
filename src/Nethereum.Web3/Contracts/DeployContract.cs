using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public class DeployContract
    {
        private readonly RpcClient client;
        private EthSendTransaction ethSendTransaction;
        private ConstructorCallEncoder constructorCallEncoder;
        private ABIDeserialiser abiDeserialiser;

        public DeployContract(RpcClient client)
        {
            this.client = client;
            this.ethSendTransaction = new EthSendTransaction(client);
            this.constructorCallEncoder = new ConstructorCallEncoder();
            this.abiDeserialiser = new ABIDeserialiser();
        }

        public async Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            params object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            var encodedData = constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);

            return await ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, gas, from));
        }

        public async Task<string> SendRequestAsync(string abi, string contractByteCode, string from,
            params object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            var encodedData = constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);

            return await ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, null, from));
        }

        public async Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas)
        {
            return await ethSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, gas, from));
        }

        public async Task<string> SendRequestAsync(string contractByteCode, string from)
        {
            return await ethSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, null, from));
        }

        public async Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var encodedData = constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            return await ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, null, from));
        }

        public async Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var encodedData = constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            return await ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, gas, from));
        }

        public string GetData(string contractByteCode, string abi, params object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            return constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);

        }


        public string GetData<TConstructorParams>(string contractByteCode, TConstructorParams inputParams)
        {
            return constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);

        }
    }
}