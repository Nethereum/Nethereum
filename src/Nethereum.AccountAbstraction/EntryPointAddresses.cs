using System;
using Nethereum.Util;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "EntryPointAddresses - the canonical EntryPoint deployments")]
    public static class EntryPointAddresses
    {
        public const string V09 = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";
        public const string V08 = "0x4337084d9e255ff0702461cf8895ce9e3b5ff108";
        public const string V07 = "0x0000000071727De22E5E9d8BAf0edAc6f37da032";
        public const string V06 = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";

        public static string Latest => V09;

        public static void ValidateSupportedUserOpHashVersion(string entryPointAddress)
        {
            string legacyVersion = null;
            if (entryPointAddress.IsTheSameAddress(V07)) legacyVersion = "v0.7";
            else if (entryPointAddress.IsTheSameAddress(V06)) legacyVersion = "v0.6";

            if (legacyVersion != null)
                throw new NotSupportedException(
                    $"EntryPoint {entryPointAddress} is {legacyVersion}, which uses the legacy keccak256 userOpHash. " +
                    "This library signs the EIP-712 userOpHash introduced in EntryPoint v0.8. " +
                    "Use EntryPointAddresses.V08 or V09.");
        }
    }
}
