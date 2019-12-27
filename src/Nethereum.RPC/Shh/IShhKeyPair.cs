using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Shh
{
    public interface IShhKeyPair
    {
        IShhNewKeyPair NewKeyPair { get; }
        IShhAddPrivateKey AddPrivateKey { get; }
    }
}
