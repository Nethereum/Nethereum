using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Personal;

namespace Nethereum.Web3
{
    public class DeployContract
    {
        private readonly IClient client;
        private readonly ABIDeserialiser abiDeserialiser;
        private readonly ConstructorCallEncoder constructorCallEncoder;
        private readonly EthSendTransaction ethSendTransaction;
        private readonly PersonalSignAndSendTransaction personalSignAndSendTransaction;
        

        public DeployContract(IClient client)
        {
            this.client = client;
            ethSendTransaction = new EthSendTransaction(client);
            personalSignAndSendTransaction = new PersonalSignAndSendTransaction(client);
            constructorCallEncoder = new ConstructorCallEncoder();
            abiDeserialiser = new ABIDeserialiser();
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, gas, values);
            return ethSendTransaction.SendRequestAsync(transaction);
        }

        public Task<string> SignAndSendRequestAsync(string password, string abi, string contractByteCode, string from, HexBigInteger gas,
           params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, gas, values);
            return personalSignAndSendTransaction.SendRequestAsync(transaction, password);
        }

        private TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas, object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            var encodedData = constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);
            var transaction = new TransactionInput(encodedData, gas, from);
            return transaction;
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from,
            params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, values);
            return ethSendTransaction.SendRequestAsync(transaction);
        }

        public Task<string> SignAndSendRequestAsync(string password, string abi, string contractByteCode, string from,
           params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, values);
            return personalSignAndSendTransaction.SendRequestAsync(transaction, password);
        }

        private TransactionInput BuildTransaction(string abi, string contractByteCode, string from, object[] values)
        {
            var contract = abiDeserialiser.DeserialiseContract(abi);
            var encodedData = constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);
            var transaction = new TransactionInput(encodedData, null, from);
            return transaction;
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas)
        {
            return ethSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, gas, from));
        }

        public Task<string> SignSendRequestAsync(string password, string contractByteCode, string from, HexBigInteger gas)
        {
            return personalSignAndSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, gas, from), password);
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from)
        {
            return ethSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, null, from));
        }

        public Task<string> SignAndSendRequestAsync(string password, string contractByteCode, string from)
        {
            return personalSignAndSendTransaction.SendRequestAsync(new TransactionInput(contractByteCode, null, from), password);
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, inputParams);
            return ethSendTransaction.SendRequestAsync(transaction);
        }

        public Task<string> SignAndSendRequestAsync<TConstructorParams>(string password, string contractByteCode, string from,
           TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, inputParams);
            return personalSignAndSendTransaction.SendRequestAsync(transaction, password);
        }

        private TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from, TConstructorParams inputParams)
        {
            var encodedData = constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from);
            return transaction;
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, gas, inputParams);
            return ethSendTransaction.SendRequestAsync(transaction);
        }

        public Task<string> SignAndSendRequestAsync<TConstructorParams>(string password, string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, gas, inputParams);
            return personalSignAndSendTransaction.SendRequestAsync(transaction, password);
        }

        private TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, TConstructorParams inputParams)
        {
            var encodedData = constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, gas, from);
            return transaction;
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