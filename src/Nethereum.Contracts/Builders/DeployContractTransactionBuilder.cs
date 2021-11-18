using System;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionTypes;

namespace Nethereum.Contracts
{
    public class DeployContractTransactionBuilder
    {
       
        private readonly ConstructorCallEncoder _constructorCallEncoder;

        public DeployContractTransactionBuilder()
        {
            _constructorCallEncoder = new ConstructorCallEncoder();
            
        }

        public string GetData(string contractByteCode, string abi, params object[] values)
        {
            var contract = ABIDeserialiserFactory.DeserialiseContractABI(abi);
            return _constructorCallEncoder.EncodeRequest(contractByteCode,
                contract.Constructor.InputParameters, values);
        }

        public string GetData<TConstructorParams>(string contractByteCode, TConstructorParams inputParams)
        {
            return _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
        }

        private string BuildEncodedData(string abi, string contractByteCode, object[] values)
        {
            EnsureByteCodeDoesNotContainPlaceholders(contractByteCode);

            if (values == null || values.Length == 0)
                return _constructorCallEncoder.EncodeRequest(contractByteCode, "");
            var contract = ABIDeserialiserFactory.DeserialiseContractABI(abi);
            if (contract.Constructor == null)
                throw new Exception(
                    "Parameters supplied for a constructor but ABI does not contain a constructor definition");
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

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger gasPrice,
            HexBigInteger value, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
            return transaction;
        }

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger gasPrice,
            HexBigInteger value, HexBigInteger nonce, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
            transaction.Nonce = nonce;
            return transaction;
        }

        public TransactionInput BuildTransaction(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas,
            HexBigInteger value, HexBigInteger nonce, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(TransactionType.EIP1559.AsHexBigInteger(), encodedData, null, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
            transaction.Nonce = nonce;
            return transaction;
        }

        public TransactionInput BuildTransaction(HexBigInteger type, string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas,
            HexBigInteger value, HexBigInteger nonce, object[] values)
        {
            var encodedData = BuildEncodedData(abi, contractByteCode, values);
            var transaction = new TransactionInput(type, encodedData, null, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
            transaction.Nonce = nonce;
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
            HexBigInteger gas, HexBigInteger value, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from, gas, value);
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(encodedData, null, from, gas, gasPrice, value);
            transaction.Nonce = nonce;
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas,
            HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(TransactionType.EIP1559.AsHexBigInteger(), encodedData, null, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
            transaction.Nonce = nonce;
            return transaction;
        }

        public TransactionInput BuildTransaction<TConstructorParams>(HexBigInteger type, string contractByteCode, string from, HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas,
            HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams)
        {
            var encodedData = _constructorCallEncoder.EncodeRequest(inputParams, contractByteCode);
            var transaction = new TransactionInput(type, encodedData, null, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
            transaction.Nonce = nonce;
            return transaction;
        }

        public void EnsureByteCodeDoesNotContainPlaceholders(string byteCode)
        {
            ByteCodeLibraryLinker.EnsureDoesNotContainPlaceholders(byteCode);
        }
    }
}