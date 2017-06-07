using System;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts
{
    public class DeployContract
    {
        private readonly ABIDeserialiser _abiDeserialiser;
        private readonly ConstructorCallEncoder _constructorCallEncoder;

        public DeployContract(ITransactionManager transactionManager)
        {
            TransactionManager = transactionManager;
            _constructorCallEncoder = new ConstructorCallEncoder();
            _abiDeserialiser = new ABIDeserialiser();
        }

        public ITransactionManager TransactionManager { get; set; }

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

        public Task<HexBigInteger> EstimateGasAsync(string abi, string contractByteCode, string from,
           params object[] values)
        {
            var callInput = BuildTransaction(abi, contractByteCode, from, values);
            return TransactionManager.EstimateGasAsync(callInput);
        }

        public Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from, 
            TConstructorParams inputParams)
        {
            var callInput = BuildTransaction(contractByteCode, from, inputParams);
            return TransactionManager.EstimateGasAsync(callInput);
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

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice,
           HexBigInteger value,
           params object[] values)
        {
            var transaction = BuildTransaction(abi, contractByteCode, from, gas, gasPrice, value, values);
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

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, null, from, gas, gasPrice, value));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger value)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, null, from, gas, value));
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

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams)
        {
            var transaction = BuildTransaction(contractByteCode, from, gas, gasPrice, value, inputParams);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        private string BuildEncodedData(string abi, string contractByteCode, object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return _constructorCallEncoder.EncodeRequest(contractByteCode, "");
            }
            var contract = _abiDeserialiser.DeserialiseContract(abi);
            if(contract.Constructor == null) throw  new Exception("Parameters supplied for a constructor but ABI does not contain a constructor definition");
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

        private TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice,
            HexBigInteger value, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
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

        private TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from,  gas, gasPrice, value);
            return transaction;
        }
    }
}