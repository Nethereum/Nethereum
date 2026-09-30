using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator.ContractDefinition
{
    public partial class WebAuthnCredential : WebAuthnCredentialBase { }

    public class WebAuthnCredentialBase 
    {
        [Parameter("uint256", "pubKeyX", 1)]
        public virtual BigInteger PubKeyX { get; set; }
        [Parameter("uint256", "pubKeyY", 2)]
        public virtual BigInteger PubKeyY { get; set; }
        [Parameter("bool", "requireUV", 3)]
        public virtual bool RequireUV { get; set; }
    }
}
