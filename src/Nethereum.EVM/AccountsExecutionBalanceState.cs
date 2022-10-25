using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.EVM
{
    public class AccountsExecutionBalanceState
    {
        public Dictionary<string, AccountExecutionBalance> Balances { get; protected set; }

        public AccountsExecutionBalanceState()
        {
            Balances = new Dictionary<string, AccountExecutionBalance>();
        }

        public bool ContainsInitialChainBalanceForAddress(string address)
        {
            address = address.ToLower();
            return Balances.ContainsKey(address) && Balances[address].InitialChainBalance != null;
        }

        public BigInteger GetTotalBalance(string address)
        {
            address = address.ToLower();
            return Balances[address].GetTotalBalance();
        }

        public void SetInitialChainBalance(string address, BigInteger value)
        {
            address = address.ToLower();
            if (!Balances.ContainsKey(address))
            {
                Balances.Add(address, new AccountExecutionBalance() { Address = address });
            }

            Balances[address].SetInitialChainBalance(value);
        }

        public void UpsertInternalBalance(string address, BigInteger value)
        {
            address = address.ToLower();
            if (!Balances.ContainsKey(address))
            {
                Balances.Add(address, new AccountExecutionBalance() { Address = address });
            }
            Balances[address].UpdateExecutionBalance(value);
        }

        public string ToTraceString()
        {
            var sb = new StringBuilder();
            foreach (var balance in Balances)
            {
                sb.AppendLine(balance.Value.ToTraceString());
            }
            return sb.ToString();
        }
    }
}