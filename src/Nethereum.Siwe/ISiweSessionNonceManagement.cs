using Nethereum.Siwe.Core;

namespace Nethereum.Siwe;

public interface ISiweSessionNonceManagement
{
    SiweMessage AssignNewNonce(SiweMessage siweMessage);
    bool ValidateSiweMessageHasCorrectNonce(SiweMessage siweMessage);
    void InvalidateSession(SiweMessage siweMessage);
    void InvalidateAddressSessions(string address);
    void RemoveExpiredSessions();
}