using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Quorum
{
    public class QuorumTransactionManager : TransactionManager
    {
        public override BigInteger DefaultGas { get; set; } = Nethereum.Signer.SignedTransactionBase.DEFAULT_GAS_LIMIT;
        public BigInteger DefaultGasIncrement { get; set; } = 90000000;

        public QuorumTransactionManager(IClient client, string accountAddress) : base(client)
        {
            this.Account = new QuorumAccount(accountAddress, this);
            this.DefaultGasPrice = 0;
        }

        internal void SetAccount(QuorumAccount account)
        {
            Account = account;
        }

        public QuorumTransactionManager(IClient client, QuorumAccount account) : base(client)
        {
            this.Account = account;
            this.DefaultGasPrice = 0;
        }

        public override Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            transactionInput.From = Account.Address;
            transactionInput.Gas = new HexBigInteger(transactionInput.Gas.Value + DefaultGasIncrement);
            return base.SendTransactionAsync(transactionInput);
        }
    }
}
