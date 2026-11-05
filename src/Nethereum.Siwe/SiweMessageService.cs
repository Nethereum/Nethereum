using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Siwe.UserServices;
using Nethereum.Util;
using Org.BouncyCastle.Bcpg;


namespace Nethereum.Siwe
{
    public class SiweMessageService
    {
        private readonly ISessionStorage _sessionStorage;
        private readonly IEthereumUserService _ethereumUserService;
        private readonly Web3.Web3 _web3;
        private BigInteger? _nodeChainId;

        public SiweMessageService(ISessionStorage sessionStorage, 
            IEthereumUserService ethereumUserService = null,
            Web3.Web3 web3ForERC1271Validation = null)
        {
            _sessionStorage = sessionStorage;
            _ethereumUserService = ethereumUserService;
            _web3 = web3ForERC1271Validation;
        }

        public SiweMessageService()
        {
            _sessionStorage = new InMemorySessionNonceStorage();
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

            AssignNewNonce(siweMessage);
            return SiweMessageStringBuilder.BuildMessage(siweMessage);
        }


        public async Task<bool> IsMessageSignatureValid(SiweMessage siweMessage, string signature)
        {
            var builtMessage = SiweMessageStringBuilder.BuildMessage(siweMessage);
            var messageSigner = new EthereumMessageSigner();
            if (IsEoaSignatureLength(signature))
            {
                try
                {
                    var accountRecovered = messageSigner.EncodeUTF8AndEcRecover(builtMessage, signature);
                    if (accountRecovered.IsTheSameAddress(siweMessage.Address)) return true;
                }
                catch (Exception)
                {
                }
            }

            if (_web3 != null)
            {
                if (!await IsNodeOnMessageChain(siweMessage).ConfigureAwait(false)) return false;
                if (_web3.Eth.SignatureValidationPredeployContractERC6492.IsERC6492Signature(signature))
                {
                    var service = _web3.Eth.SignatureValidationPredeployContractERC6492;
                    return await service.IsValidSignatureAsync(siweMessage.Address,
                        messageSigner.HashPrefixedMessage(Encoding.UTF8.GetBytes(builtMessage)),
                        signature.HexToByteArray()).ConfigureAwait(false);
                }
                else
                {
                    var service = _web3.Eth.ERC1271.GetContractService(siweMessage.Address);
                    return await service.IsValidSignatureAndValidateReturnQueryAsync(
                        messageSigner.HashPrefixedMessage(Encoding.UTF8.GetBytes(builtMessage)),
                        signature.HexToByteArray()).ConfigureAwait(false);
                }
                
            }

            return false;
        }

        private static bool IsEoaSignatureLength(string signature)
        {
            try
            {
                return signature.HexToByteArray().Length == 65;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> IsNodeOnMessageChain(SiweMessage siweMessage)
        {
            if (!BigInteger.TryParse(siweMessage.ChainId, out var messageChainId)) return false;
            if (_nodeChainId == null)
            {
                var chainId = await _web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false);
                _nodeChainId = chainId.Value;
            }

            return _nodeChainId == messageChainId;
        }

       
        public virtual async Task<bool> IsValidMessage(SiweMessage siweMessage, string signature)
        {
            if (!(HasMessageDateStartedAndNotExpired(siweMessage) &&
                  IsMessageTheSameAsSessionStored(siweMessage)
                  && await IsMessageSignatureValid(siweMessage, signature).ConfigureAwait(false)))
            {
                return false;
            }

            try
            {
                _sessionStorage.Remove(siweMessage.Nonce);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public virtual Task<bool> IsUserAddressRegistered(SiweMessage siweMessage)
        {
            if (_ethereumUserService == null) return Task.FromResult(true);
            return _ethereumUserService.IsUserAddressRegistered(siweMessage.Address);
        }

        public virtual bool HasMessageDateStartedAndNotExpired(SiweMessage siweMessage)
        {
            return siweMessage.HasMessageDateStartedAndNotExpired();
        }


        public virtual SiweMessage AssignNewNonce(SiweMessage siweMessage)
        {
            if (string.IsNullOrEmpty(siweMessage.Nonce))
            {
                siweMessage.Nonce = RandomNonceBuilder.GenerateNewNonce();
                _sessionStorage.AddOrUpdate(siweMessage);
            }
            else
            {
                throw new Exception("Siwe message has an allocated nonce already");
            }

            return siweMessage;
        }


        public virtual bool IsMessageTheSameAsSessionStored(SiweMessage siweMessage)
        {
            var sessionMessage = _sessionStorage.GetSiweMessage(siweMessage);

            if (sessionMessage != null)
            {
                return sessionMessage.IsTheSame(siweMessage);
            }

            return false;
        }


        public virtual void InvalidateSession(SiweMessage siweMessage)
        {
            _sessionStorage.Remove(siweMessage.Nonce);
        }

    }
}
