using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory
{
    public partial class SimpleAccountFactoryService
    {
        public byte[] GetCreateAccountInitCode(BigInteger salt)
        {
            return GetCreateAccountInitCode(this.Web3.TransactionManager.Account.Address, salt);
        }

        public byte[] GetCreateAccountInitCode(string owner, BigInteger salt)
        {
            var createAccountFunction = new CreateAccountFunction();
            createAccountFunction.Owner = owner;
            createAccountFunction.Salt = salt;
            return this.ContractAddress.HexToByteArray().Concat(createAccountFunction.GetCallData()).ToArray();
        }

        public async Task<string> CreateAccountQueryAsync(string owner, BigInteger salt)
        {
            var createAccountFunction = new CreateAccountFunction();
            createAccountFunction.Owner = owner;
            createAccountFunction.Salt = salt;
            return await ContractHandler.QueryAsync<CreateAccountFunction, string>(createAccountFunction);
        }
    }
}
