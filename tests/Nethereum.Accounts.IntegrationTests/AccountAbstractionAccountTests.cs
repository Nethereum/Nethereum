using System;
using Nethereum.Accounts.AccountAbstraction;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.Accounts.ViewOnly;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    // Unit tests (no network) for AccountAbstractionAccount: it composes a read-only ViewOnlyAccount
    // with a real, injected IAccountSigningService - the shape an ERC-4337 smart account needs
    // (signs typed data / UserOperations, never sends a raw transaction). Pins the ViewOnlyAccount
    // "protected set" enabler that lets the signing service be supplied via the constructor.
    public class AccountAbstractionAccountTests
    {
        private const string SmartAccountAddress = "0x1234567890123456789012345678901234567890";

        [Fact]
        public void Injects_the_signing_service_and_stays_a_read_only_ViewOnlyAccount()
        {
            var signingService = new AccountSigningOfflineService(EthECKey.GenerateKey());

            var account = new AccountAbstractionAccount(SmartAccountAddress, signingService);

            Assert.Equal(SmartAccountAddress, account.Address);

            Assert.IsAssignableFrom<ViewOnlyAccount>(account);
            Assert.IsType<ViewOnlyAccountTransactionManager>(account.TransactionManager);

            Assert.Same(signingService, account.AccountSigningService);
            Assert.NotNull(account.AccountSigningService.SignTypedDataV4);
        }

        [Fact]
        public void Rejects_a_null_signing_service()
        {
            Assert.Throws<ArgumentNullException>(
                () => new AccountAbstractionAccount(SmartAccountAddress, null));
        }
    }
}
