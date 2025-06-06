using System.Numerics;
using Nethereum.Web3;


namespace Nethereum.GnosisSafe
{
    public class SafeAccount : Nethereum.Web3.Accounts.Account, IContractServiceConfigurableAccount
    {
        public SafeAccount(string safeAddress, BigInteger chainId, string privateKey) : base(privateKey, chainId)
        {
            SafeAddress = safeAddress;
        }

        public string SafeAddress { get; }

        public void ConfigureContractHandler<T>(T contractService) where T : ContractWeb3ServiceBase
        {
            contractService.ChangeContractHandlerToSafeExecTransaction(SafeAddress, this.PrivateKey);
        }
    }
}
