using System;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.JsonDeserialisation;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public class DeployContractTransactionBuilder
    {
        private readonly ABIDeserialiser _abiDeserialiser;
        private readonly ConstructorCallEncoder _constructorCallEncoder;

        public DeployContractTransactionBuilder()
        {
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

        private string BuildEncodedData(string abi, string contractByteCode, object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return _constructorCallEncoder.EncodeRequest(contractByteCode, "");
            }
            var contract = _abiDeserialiser.DeserialiseContract(abi);
            if (contract.Constructor == null) throw new Exception("Parameters supplied for a constructor but ABI does not contain a constructor definition");
            var encodedData = _constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);
            return encodedData;
        }

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas,
            object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, gas, from);
            return transaction;
        }

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice,
            HexBigInteger value, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
            return transaction;
        }

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger value, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, from, gas, value);
            return transaction;
        }

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, null, from);
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from);
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, gas, from);
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
            return transaction;
        }
    }
}