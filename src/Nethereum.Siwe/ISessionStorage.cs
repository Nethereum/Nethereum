using System.Collections.Generic;
using Nethereum.Siwe.Core;

namespace Nethereum.Siwe;

public interface ISessionStorage
{
    void AddOrUpdate(SiweMessage siweMessage);
    SiweMessage GetSiweMessage(SiweMessage siweMessage);
    void Remove(SiweMessage siweMessage);
    void Remove(string nonce);
}