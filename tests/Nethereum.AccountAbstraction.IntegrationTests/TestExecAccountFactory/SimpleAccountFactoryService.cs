using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory
{
    public partial class TestExecAccountFactoryService
    {

        public async Task<string> CreateAccountQueryAsync(string owner, BigInteger salt)
        {
            var createAccountFunction = new CreateAccountFunction();
            createAccountFunction.Owner = owner;
            createAccountFunction.Salt = salt;
            return await ContractHandler.QueryAsync<CreateAccountFunction, string>(createAccountFunction);
        }
    }
}
