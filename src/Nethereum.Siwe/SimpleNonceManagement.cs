using System;
using Nethereum.Siwe.Core;

namespace Nethereum.Siwe;

public class SimpleNonceManagement : ISiweSessionNonceManagement
{
    public SiweMessage AssignNewNonce(SiweMessage siweMessage)
    {
        if (string.IsNullOrEmpty(siweMessage.Nonce))
        {
            siweMessage.Nonce = RandomNonceBuilder.GenerateNewNonce();
        }
        return siweMessage;
    }

    public bool ValidateSiweMessageHasCorrectNonce(SiweMessage siweMessage)
    {
        return !string.IsNullOrEmpty(siweMessage.Nonce);
    }

    public void InvalidateSession(SiweMessage siweMessage)
    {
        throw new NotImplementedException();
    }

    public void InvalidateAddressSessions(string address)
    {
        throw new NotImplementedException();
    }

    public void RemoveExpiredSessions()
    {
        throw new NotImplementedException();
    }
}