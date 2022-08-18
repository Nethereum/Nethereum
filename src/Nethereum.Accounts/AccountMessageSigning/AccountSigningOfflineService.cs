using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using System;
using System.Threading.Tasks;

namespace Nethereum.Accounts.AccountMessageSigning
{

    public class AccountSigningOfflineService : IAccountSigningService
    {

        public IEthSignTypedDataV4 SignTypedDataV4 { get; }

        public IEthPersonalSign PersonalSign { get; }

        public AccountSigningOfflineService(EthECKey ethECKey)
        {
            SignTypedDataV4 = new EthSignTypedDataV4Offline(ethECKey);
            PersonalSign = new EthPersonalOfflineSign(ethECKey);
        }
    }
}
