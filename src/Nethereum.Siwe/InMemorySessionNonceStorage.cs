using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Siwe.Core;

namespace Nethereum.Siwe;

public class InMemorySessionNonceStorage : ISessionStorage
{
    private ConcurrentDictionary<string, SiweMessage> _messages = new();

    public void AddOrUpdate(SiweMessage siweMessage)
    {
        _messages.AddOrUpdate(siweMessage.Nonce, siweMessage,
            (string nonce, SiweMessage oldSiweMessage) => siweMessage);
    }

    public SiweMessage GetSiweMessage(SiweMessage siweMesage)
    {
        if (_messages.ContainsKey(siweMesage.Nonce))
        {
            return _messages[siweMesage.Nonce];
        }

        return null;
    }

    public void Remove(SiweMessage siweMessage)
    {
        if (!_messages.TryRemove(siweMessage.Nonce, out SiweMessage siweMessageStored))
        {
            throw new Exception("Could not remove siwe message, try again");
        }
    }

    public void Remove(string nonce)
    {
        if (!_messages.TryRemove(nonce, out SiweMessage siweMessageStored))
        {
            throw new Exception("Could not remove siwe message, try again");
        }
    }
}