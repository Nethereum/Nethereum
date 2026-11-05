using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Signer.Trezor;
using Nethereum.Wallet;
using Nethereum.Wallet.Trezor;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.UnitTests
{
    public class TrezorWalletAccountTests
    {
        private const string DeviceId = "trezor-device-1";
        private const string Address = "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979";

        private sealed class NoDeviceSessionProvider : ITrezorSessionProvider
        {
            public Task<TrezorSessionExternalSigner> CreateSignerAsync(
                uint index, string deviceId, string? knownAddress = null, CancellationToken cancellationToken = default)
                => throw new NotSupportedException("No Trezor attached.");
        }

        private sealed class RecordingDiscoveryService : ITrezorDeviceDiscoveryService
        {
            public List<(string DeviceId, uint StartIndex, uint Count)> Calls { get; } = new();

            public Task<IReadOnlyList<TrezorDerivationPreview>> DiscoverAsync(
                string deviceId, uint startIndex, uint count, CancellationToken cancellationToken = default)
            {
                Calls.Add((deviceId, startIndex, count));
                return Task.FromResult<IReadOnlyList<TrezorDerivationPreview>>(
                    new[] { new TrezorDerivationPreview(startIndex, Address) });
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.WalletUI, "trezor-wallet-accounts", "Add a known Trezor address to the vault and persist it", Order = 1)]
        public async Task ShouldAddAKnownTrezorAddressToTheVaultAndRestoreItFromTheEncryptedVault()
        {
            var sessionProvider = new NoDeviceSessionProvider();
            var discovery = new RecordingDiscoveryService();
            var vault = new WalletVault(new BouncyCastleAes256EncryptionStrategy());
            var service = new TrezorWalletAccountService(vault, sessionProvider, discovery);

            var account = service.CreateFromKnownAddress(
                index: 3, deviceId: DeviceId, address: Address, label: "Trezor cold wallet", setAsSelected: true);

            var encrypted = vault.Encrypt("securepass123");

            var restoredVault = new WalletVault(new BouncyCastleAes256EncryptionStrategy());
            restoredVault.RegisterFactory(new TrezorWalletAccountFactory(sessionProvider, discovery));
            restoredVault.Decrypt(encrypted, "securepass123");

            var restored = Assert.IsType<TrezorWalletAccount>(Assert.Single(restoredVault.Accounts));
            Assert.Equal("trezor", TrezorWalletAccount.TypeName);
            Assert.Equal(Address, account.Address);
            Assert.Equal(3u, restored.Index);
            Assert.Equal(DeviceId, restored.DeviceId);
            Assert.Equal(DeviceId, restored.GroupId);
            Assert.Equal("Trezor cold wallet", restored.Name);
            Assert.True(restored.IsSelected);
            var device = Assert.Single(restoredVault.GetHardwareDevicesByType("trezor"));
            Assert.Equal(DeviceId, device.Id);
            Assert.Equal("Trezor Wallet 1", device.Label);
        }

        [Fact]
        [NethereumDocExample(DocSection.WalletUI, "trezor-wallet-accounts", "Default labels and device labels", Order = 2)]
        public void ShouldLabelAnUnlabelledAccountWithItsAddressAndRenameTheDevice()
        {
            var vault = new WalletVault(new BouncyCastleAes256EncryptionStrategy());
            var service = new TrezorWalletAccountService(vault, new NoDeviceSessionProvider(), new RecordingDiscoveryService());

            var firstDefaultLabel = service.GetDefaultHardwareDeviceLabel();
            var account = service.BuildAccount(0, DeviceId, Address);
            service.CreateFromKnownAddress(1, DeviceId, "0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47");
            service.UpdateHardwareDeviceLabel(DeviceId, "  Office Trezor  ");

            Assert.Equal(Address, account.Label);
            Assert.Equal("Trezor Wallet 1", firstDefaultLabel);
            Assert.Equal("Trezor Wallet 2", service.GetDefaultHardwareDeviceLabel());
            Assert.Equal("Office Trezor", vault.FindHardwareDevice(DeviceId)!.Label);
            Assert.Single(vault.Accounts);
        }

        [Fact]
        [NethereumDocExample(DocSection.WalletUI, "trezor-wallet-accounts", "EnsureReadyAsync re-discovers the account on the device", Order = 3)]
        public async Task ShouldRediscoverTheAccountOnTheDeviceWhenEnsuringItIsReady()
        {
            var discovery = new RecordingDiscoveryService();
            var account = new TrezorWalletAccount(Address, "label", 5, DeviceId, new NoDeviceSessionProvider(), discovery);

            await account.EnsureReadyAsync();

            Assert.Equal((DeviceId, 5u, 1u), Assert.Single(discovery.Calls));
        }
    }
}
