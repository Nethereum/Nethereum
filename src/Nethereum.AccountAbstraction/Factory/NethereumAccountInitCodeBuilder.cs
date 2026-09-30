using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.Contracts;

namespace Nethereum.AccountAbstraction.Factory
{
    public class NethereumAccountInitCodeBuilder : IAccountInitCodeBuilder
    {
        private readonly byte[] _salt;
        private readonly byte[] _initData;

        public NethereumAccountInitCodeBuilder(string factoryAddress, byte[] salt, byte[] initData)
        {
            FactoryAddress = factoryAddress;
            _salt = salt;
            _initData = initData;
        }

        public string FactoryAddress { get; }

        public byte[] BuildFactoryData()
        {
            var createAccountFunction = new CreateAccountFunction
            {
                Salt = _salt,
                InitData = _initData
            };
            return createAccountFunction.GetCallData();
        }
    }
}
