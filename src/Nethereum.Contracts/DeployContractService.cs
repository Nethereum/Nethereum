using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.TransactionManagers;


namespace Nethereum.Web3
{
    public class DeployContract
    {
        public ITransactionManager TransactionManager { get; set; }
        private readonly ABIDeserialiser _abiDeserialiser;
        private readonly ConstructorCallEncoder _constructorCallEncoder;

        public DeployContract(ITransactionManager transactionManager)
        {
            TransactionManager = transactionManager;
            _constructorCallEncoder = new ConstructorCallEncoder();
            _abiDeserialiser = new ABIDeserialiser();
        }

        public string GetData(string contractByteCode, string abi, params object[] values)
        {
            var contract = _abiDeserialiser.DeserialiseContract(abi);
            return _constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);
        }

        public string GetData<TConstructorParams>(string contractByteCode, TConstructorParams inputParams)
        {
            return _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, gas, values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger value,
            params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, gas, value, values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from,
            params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, gas, from));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, null, from));
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, inputParams);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, gas, inputParams);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        private string BuildEncodedData(string abi, string contractByteCode, object[] values)
        {
            var contract = _abiDeserialiser.DeserialiseContract(abi);
            var encodedData = _constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);
            return encodedData;
        }

        private TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas,
            object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, gas, from);
            return transaction;
        }

        private TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger value, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, from, gas, value);
            return transaction;
        }

        private TransactionInput BuildTransaction(string abi, string contractByteCode, string from, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, null, from);
            return transaction;
        }

        private TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from);
            return transaction;
        }

        private TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, gas, from);
            return transaction;
        }
    }
}