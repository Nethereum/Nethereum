using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.AccountAbstraction.Factory
{
    public class SimpleAccountInitCodeBuilder : IAccountInitCodeBuilder
    {
        private readonly FactoryConfig _config;

        public SimpleAccountInitCodeBuilder(FactoryConfig config)
        {
            _config = config;
        }

        public string FactoryAddress => _config.FactoryAddress;

        public byte[] BuildFactoryData()
        {
            var createAccountFunction = new SimpleAccountFactoryCreateAccountFunction
            {
                Owner = _config.Owner,
                Salt = _config.Salt
            };
            return createAccountFunction.GetCallData();
        }

        [Function("createAccount", "address")]
        private class SimpleAccountFactoryCreateAccountFunction : FunctionMessage
        {
            [Parameter("address", "owner", 1)]
            public string Owner { get; set; }

            [Parameter("uint256", "salt", 2)]
            public BigInteger Salt { get; set; }
        }
    }
}
