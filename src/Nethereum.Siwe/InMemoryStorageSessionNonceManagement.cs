using System;
using System.Collections.Concurrent;
using System.Linq;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Util;

namespace Nethereum.Siwe;

public class RandomNonceBuilder
{
    public static string GenerateNewNonce()
    {
        var currentChallenge = DateTime.Now.ToString("O") + "- Challenge";
        //creating a random key, hashing, signing and hashing again to be truly random
        var key = EthECKey.GenerateKey();
        var currentKey = key.GetPrivateKey(); // random key to sign message
        var signer = new MessageSigner();
        return Util.Sha3Keccack.Current.CalculateHash(signer.HashAndSign(currentChallenge, currentKey));
    }
}

public class InMemoryStorageSessionNonceManagement : ISiweSessionNonceManagement
{
    private ConcurrentDictionary<string, SiweMessage> _messages = new ConcurrentDictionary<string, SiweMessage>();

    public virtual SiweMessage AssignNewNonce(SiweMessage siweMessage)
    {
        if (string.IsNullOrEmpty(siweMessage.Nonce))
        {
            siweMessage.Nonce = RandomNonceBuilder.GenerateNewNonce();
            _messages.AddOrUpdate(siweMessage.Nonce, siweMessage,
                (string nonce, SiweMessage oldSiweMessage) => siweMessage);
        }
        else
        {
            throw new Exception("Siwe message has a nonce already");
        }

        return siweMessage;
    }

    

    public virtual bool ValidateSiweMessageHasCorrectNonce(SiweMessage siweMessage)
    {
        if (_messages.ContainsKey(siweMessage.Nonce))
        {
            var currentMessage = SiweMessageStringBuilder.BuildMessage(siweMessage);
            var existingMessage = SiweMessageStringBuilder.BuildMessage(_messages[siweMessage.Nonce]);
            if (currentMessage == existingMessage)
            {
                return true;
            }
        }

        return false;
    }

    public virtual void InvalidateSession(SiweMessage siweMessage)
    {
        if (_messages.ContainsKey(siweMessage.Nonce))
        {
            if (!_messages.TryRemove(siweMessage.Nonce, out SiweMessage siweMessageStored))
            {
                throw new Exception("Could not invalidate stored session, try again");
            }
        }
    }

    public virtual void InvalidateAddressSessions(string address)
    {
        var messages = _messages.Where(x => x.Value.Address.IsTheSameAddress(address));
        foreach (var message in messages)          
        {
            if (!_messages.TryRemove(message.Key, out SiweMessage siweMessageStored))
            {
                throw new Exception("Could not invalidate stored session, try again");
            }
        }
    }

    public virtual void RemoveExpiredSessions()
    {
        var messages = _messages.Where(x => x.Value.HasMessageDateExpired());
        foreach (var message in messages)
        {
            if (!_messages.TryRemove(message.Key, out SiweMessage siweMessageStored))
            {
                throw new Exception("Could not remove expired sessions, try again");
            }
        }
    }
}