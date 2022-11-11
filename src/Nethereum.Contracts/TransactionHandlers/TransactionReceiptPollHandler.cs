using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.TransactionHandlers
{
#if !DOTNET35
    public class TransactionReceiptPollHandler<TFunctionMessage> :
        TransactionHandlerBase<TFunctionMessage>, ITransactionReceiptPollHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        private readonly ITransactionSenderHandler<TFunctionMessage> _contractTransactionSender;

        public TransactionReceiptPollHandler(ITransactionManager transactionManager) : this(transactionManager,
            new TransactionSenderHandler<TFunctionMessage>(transactionManager))
        {

        }

        public TransactionReceiptPollHandler(ITransactionManager transactionManager,
            ITransactionSenderHandler<TFunctionMessage> contractTransactionSender) : base(transactionManager)
        {
            _contractTransactionSender = contractTransactionSender;
        }


        public async Task<TransactionReceipt> SendTransactionAsync(string contractAddress, TFunctionMessage functionMessage, CancellationToken cancellationToken)
        {
            if (functionMessage == null) functionMessage = new TFunctionMessage();
            SetEncoderContractAddress(contractAddress);
            var transactionHash = await _contractTransactionSender.SendTransactionAsync(contractAddress, functionMessage).ConfigureAwait(false);
            return await TransactionManager.TransactionReceiptService.PollForReceiptAsync(transactionHash, cancellationToken).ConfigureAwait(false);
        }

        public Task<TransactionReceipt> SendTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null, CancellationTokenSource cancellationTokenSource = null)
        {
            return cancellationTokenSource == null
               ? SendTransactionAsync(contractAddress, functionMessage, CancellationToken.None)
               : SendTransactionAsync(contractAddress, functionMessage, cancellationTokenSource.Token);
        }
    }
#endif
}