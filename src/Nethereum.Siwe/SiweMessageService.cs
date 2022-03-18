using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Util;


namespace Nethereum.Siwe
{
    public class SiweMessageService
    {
        private readonly ISiweSessionNonceManagement _siweSessionNonceManagement;

        public SiweMessageService(ISiweSessionNonceManagement siweSessionNonceManagement)
        {
            _siweSessionNonceManagement = siweSessionNonceManagement;
        }

        public SiweMessageService()
        {
            _siweSessionNonceManagement = new InMemoryStorageSessionNonceManagement();
        }

        public string BuildMessageToSign(SiweMessage siweMessage)
        {
            if (string.IsNullOrEmpty(siweMessage.IssuedAt))
            {
                siweMessage.SetIssuedAtNow();
            }

            if (string.IsNullOrEmpty(siweMessage.Version))
            {
                siweMessage.Version = "1";
            }

            _siweSessionNonceManagement.AssignNewNonce(siweMessage);
            return SiweMessageStringBuilder.BuildMessage(siweMessage);

        }


        public bool IsMessageSignatureValid(SiweMessage siweMessage)
        {
            var builtMessage = SiweMessageStringBuilder.BuildMessage(siweMessage);
            var messageSigner = new EthereumMessageSigner();
            var accountRecovered = messageSigner.EncodeUTF8AndEcRecover(builtMessage, siweMessage.Signature);
            if (accountRecovered.IsTheSameAddress(siweMessage.Address)) return true;
            return false;
        }

        public bool IsMessageSessionNonceValid(SiweMessage siweMessage)
        {
            return _siweSessionNonceManagement.ValidateSiweMessageHasCorrectNonce(siweMessage);
        }

        public virtual bool IsValidMessage(SiweMessage siweMessage)
        {
            return HasMessageDateStartedAndNotExpired(siweMessage) && IsMessageSignatureValid(siweMessage) &&
                   IsMessageSessionNonceValid(siweMessage);
        }

        public virtual bool HasMessageDateStartedAndNotExpired(SiweMessage siweMessage)
        {
            return siweMessage.HasMessageDateStartedAndNotExpired();
        }

    }

}
