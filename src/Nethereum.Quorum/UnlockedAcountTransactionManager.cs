using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Quorum
{
    public class UnlockedAcountTransactionManager : TransactionManager
    {
        public override BigInteger DefaultGas { get; set; } = Nethereum.Signer.SignedLegacyTransaction.DEFAULT_GAS_LIMIT;
        public BigInteger DefaultGasIncrement { get; set; } = 90000000;

        public UnlockedAcountTransactionManager(IClient client, string accountAddress) : base(client)
        {
            this.Account = new UnlockedAccount(accountAddress, this);
            this.DefaultGasPrice = 0;
        }

        internal void SetAccount(UnlockedAccount account)
        {
            Account = account;
        }

        public UnlockedAcountTransactionManager(IClient client, UnlockedAccount account) : base(client)
        {
            this.Account = account;
            this.DefaultGasPrice = 0;
        }

        public override Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            transactionInput.From = Account.Address;
            if(transactionInput.Gas == null) transactionInput.Gas = new HexBigInteger(0);
            transactionInput.Gas = new HexBigInteger(transactionInput.Gas.Value + DefaultGasIncrement);
            return base.SendTransactionAsync(transactionInput);
        }
    }
}