using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts
{
    public class DeployContract
    {
        private readonly DeployContractTransactionBuilder _deployContractTransactionBuilder;

        public DeployContract(ITransactionManager transactionManager)
        {
            TransactionManager = transactionManager;
            _deployContractTransactionBuilder = new DeployContractTransactionBuilder();
        }

        public ITransactionManager TransactionManager { get; set; }

        public string GetData(string contractByteCode, string abi, params object[] values)
        {
            return _deployContractTransactionBuilder.GetData(contractByteCode, abi, values);
        }

        public string GetData<TConstructorParams>(string contractByteCode, TConstructorParams inputParams)
        {
            return _deployContractTransactionBuilder.GetData(contractByteCode, inputParams);
        }

        public Task<HexBigInteger> EstimateGasAsync(string abi, string contractByteCode, string from,
            params object[] values)
        {
            var callInput = _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, values);
            return TransactionManager.EstimateGasAsync(callInput);
        }

        public Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var callInput = _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, inputParams);
            return TransactionManager.EstimateGasAsync(callInput);
        }

        public Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas,
            TConstructorParams inputParams)
        {
            var callInput =
                _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, gas, inputParams);
            return TransactionManager.EstimateGasAsync(callInput);
        }

        public Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger value,
            TConstructorParams inputParams)
        {
            var callInput =
                _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, gas, null, value,
                    inputParams);
            return TransactionManager.EstimateGasAsync(callInput);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            params object[] values)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, gas, values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger value,
            params object[] values)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, gas, value, values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger gasPrice,
            HexBigInteger value,
            params object[] values)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, gas, gasPrice, value,
                    values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string abi, string contractByteCode, string from,
            params object[] values)
        {
            var transaction = _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, values);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, gas, from));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger gasPrice, HexBigInteger value)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, null, from, gas,
                gasPrice, value));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, null, from, gas,
                value));
        }

        public Task<string> SendRequestAsync(string contractByteCode, string from)
        {
            return TransactionManager.SendTransactionAsync(new TransactionInput(contractByteCode, null, from));
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            TConstructorParams inputParams)
        {
            var transaction = _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, inputParams);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, TConstructorParams inputParams)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, gas, inputParams);
            return TransactionManager.SendTransactionAsync(transaction);
        }

        public Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, gas, gasPrice, value,
                    inputParams);
            return TransactionManager.SendTransactionAsync(transaction);
        }

#if !DOTNET35
        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode,
            string from, HexBigInteger gas, CancellationToken token = default(CancellationToken),
            params object[] values)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, gas, values);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode,
            string from, HexBigInteger gas,
            HexBigInteger value, CancellationToken token = default(CancellationToken),
            params object[] values)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, gas, value, values);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode,
            string from, HexBigInteger gas, HexBigInteger gasPrice,
            HexBigInteger value, CancellationToken token = default(CancellationToken),
            params object[] values)
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, gas, gasPrice, value,
                    values);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode,
            string from, CancellationToken token = default(CancellationToken),
            params object[] values)
        {
            var transaction = _deployContractTransactionBuilder.BuildTransaction(abi, contractByteCode, from, values);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from,
            HexBigInteger gas, CancellationToken token = default(CancellationToken))
        {
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(
                new TransactionInput(contractByteCode, gas, from), token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value,
            CancellationToken token = default(CancellationToken))
        {
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(
                new TransactionInput(contractByteCode, null, from, gas, gasPrice, value),
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger value, CancellationToken token = default(CancellationToken))
        {
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(
                new TransactionInput(contractByteCode, null, from, gas, value), token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from,
            CancellationToken token = default(CancellationToken))
        {
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(
                new TransactionInput(contractByteCode, null, from), token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode,
            string from,
            TConstructorParams inputParams, CancellationToken token = default(CancellationToken))
        {
            var transaction = _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, inputParams);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode,
            string from,
            HexBigInteger gas, TConstructorParams inputParams,
            CancellationToken token = default(CancellationToken))
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, gas, inputParams);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode,
            string from,
            HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams,
            CancellationToken token = default(CancellationToken))
        {
            var transaction =
                _deployContractTransactionBuilder.BuildTransaction(contractByteCode, from, gas, gasPrice, value,
                    inputParams);
            return TransactionManager.TransactionReceiptService.DeployContractAndWaitForReceiptAsync(transaction,
                token);
        }
#endif
    }
}