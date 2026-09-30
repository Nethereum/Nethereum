using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Client
{
    /// <summary>
    /// The EIP-7702 "your existing EOA becomes smart" on-ramp: builds the ECDSA-validator init data
    /// (validator ‖ owner, owner == the EOA itself - see <see cref="AccountInitDataBuilder.BuildEcdsa"/>)
    /// and wraps <paramref name="ownerKey"/>'s own address as the <see cref="NethereumSmartAccount"/>
    /// <see cref="IAAClient.ConfigureEip7702"/> drives through Account Abstraction. Unlike
    /// <see cref="IAAClient.CreateAccountAsync(EthECKey, byte[])"/>'s counterfactual CREATE2 tier, no
    /// address is derived here - the account IS the EOA - so this needs no factory query and is
    /// synchronous. Parallel to the WebAuthn package's <c>CreateWebAuthnAccountAsync</c>, but the 7702
    /// authorisation tuple is secp256k1-only (see <see cref="IEip7702AuthSigner"/>), so the owner is
    /// always a raw <see cref="EthECKey"/>, never an arbitrary <see cref="IAccountSigningService"/>.
    /// </summary>
    public static class Eip7702AAClientExtensions
    {
        public static NethereumSmartAccount CreateEip7702Account(
            this IAAClient client, EthECKey ownerKey, string ecdsaValidatorAddress)
        {
            var ownerAddress = ownerKey.GetPublicAddress();
            var signingService = new AccountSigningOfflineService(ownerKey);
            var validator = new EcdsaValidatorModule(ecdsaValidatorAddress);
            var initData = AccountInitDataBuilder.BuildEcdsa(ecdsaValidatorAddress, ownerAddress);

            return new NethereumSmartAccount(ownerAddress, signingService, validator, isDeployed: false, salt: null, initData: initData);
        }
    }
}
