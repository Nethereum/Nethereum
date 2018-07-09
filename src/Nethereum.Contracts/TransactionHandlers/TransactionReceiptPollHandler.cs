using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class TransactionReceiptPollHandler<TFunctionMessage> :
        TransactionHandlerBase<TFunctionMessage>, ITransactionReceiptPollHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        private ITransactionSenderHandler<TFunctionMessage> _contractTransactionSender;

        public TransactionReceiptPollHandler(ITransactionManager transactionManager) : this(transactionManager,
            new TransactionSenderHandler<TFunctionMessage>(transactionManager))
        {

        }

        public TransactionReceiptPollHandler(ITransactionManager transactionManager,
            ITransactionSenderHandler<TFunctionMessage> contractTransactionSender) : base(transactionManager)
        {
            _contractTransactionSender = contractTransactionSender;
        }


        public async Task<TransactionReceipt> SendTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null, CancellationTokenSource cancellationTokenSource = null)
        {
            if (functionMessage == null) functionMessage = new TFunctionMessage();
            SetEncoderContractAddress(contractAddress);
            var transactionHash = await _contractTransactionSender.SendTransactionAsync(contractAddress, functionMessage).ConfigureAwait(false);
            return await TransactionManager.TransactionReceiptService.PollForReceiptAsync(transactionHash, cancellationTokenSource).ConfigureAwait(false);
        }
    }
#endif
}