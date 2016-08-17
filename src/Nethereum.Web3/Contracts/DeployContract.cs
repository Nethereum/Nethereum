using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public class DeployContract
    {
        private readonly IClient client;
        private readonly ABIDeserialiser abiDeserialiser;
        private readonly ConstructorCallEncoder constructorCallEncoder;
        private readonly EthSendTransaction ethSendTransaction;

        public DeployContract(IClient client)
        {
            this.client = client;
            ethSendTransaction = new EthSendTransaction(client);
            constructorCallEncoder = new ConstructorCallEncoder();
            abiDeserialiser = new ABIDeserialiser();
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            params object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            var encodedData = constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);

            return ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, gas, from));
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from,
            params object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            var encodedData = constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);

            return ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, null, from));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas)
        {
            return ethSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, gas, from));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from)
        {
            return ethSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, null, from));
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var encodedData = constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            return ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, null, from));
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var encodedData = constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            return ethSendTransaction.SendRequestAsync(new TransactionInput(encodedData, gas, from));
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