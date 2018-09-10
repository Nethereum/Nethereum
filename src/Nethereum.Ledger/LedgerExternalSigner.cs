using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Hid.Net;
using Ledger.Net;
using Ledger.Net.Requests;
using Ledger.Net.Responses;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Helpers = Ledger.Net.Helpers;

namespace Nethereum.Ledger
{
    public class LedgerExternalSigner:IEthExternalSigner
    {
        public bool CalculatesV { get; } = true;

        public async Task<byte[]> GetPublicKeyAsync()
        {
            var ledgerManager = await GetLedger();

            ledgerManager.SetCoinNumber(60);
            var address=  await ledgerManager.GetAddressAsync(0, 0);

            if (address == null)
            {
                throw new Exception("Address not returned");
            }

            return address.HexToByteArray();
        }

        public async Task<ECDSASignature> SignAsync(byte[] hash)
        {
            var ledgerManager = await GetLedger();
            ledgerManager.SetCoinNumber(60);
            var derivationData = Helpers.GetDerivationPathData(ledgerManager.CurrentCoin.App, ledgerManager.CurrentCoin.CoinNumber, 0, 0, false, ledgerManager.CurrentCoin.IsSegwit);

            // Create base class like GetPublicKeyResponseBase and make the method more like GetAddressAsync
            var firstRequest = new EthereumAppSignTransactionRequest(derivationData.Concat(hash).ToArray());

            var response = await ledgerManager.SendRequestAsync<EthereumAppSignTransactionResponse, EthereumAppSignTransactionRequest>(firstRequest);

            var signature = new ECDSASignature(new Org.BouncyCastle.Math.BigInteger(response.SignatureR), new Org.BouncyCastle.Math.BigInteger(response.SignatureS));
            signature.V = new BigInteger(response.SignatureV).ToBytesForRLPEncoding();
            return signature;
        }

        public static VendorProductIds[] WellKnownLedgerWallets = new VendorProductIds[]
        {
            new VendorProductIds(0x2c97),
            new VendorProductIds(0x2581, 0x3b7c)
        };


        public class VendorProductIds
        {
            public VendorProductIds(int vendorId)
            {
                VendorId = vendorId;
            }
            public VendorProductIds(int vendorId, int? productId)
            {
                VendorId = vendorId;
                ProductId = productId;
            }
            public int VendorId
            {
                get;
            }
            public int? ProductId
            {
                get;
            }
        }

        public class UsageSpecification
        {
            public UsageSpecification(ushort usagePage, ushort usage)
            {
                UsagePage = usagePage;
                Usage = usage;
            }

            public ushort Usage
            {
                get;
            }
            public ushort UsagePage
            {
                get;
            }
        }


        private static readonly UsageSpecification[] _UsageSpecification = new[] { new UsageSpecification(0xffa0, 0x01) };


        private static async Task<LedgerManager> GetLedger()
        {
            var devices = new List<DeviceInformation>();
          

            var collection = WindowsHidDevice.GetConnectedDeviceInformations();

            foreach (var ids in WellKnownLedgerWallets)
            {
                if (ids.ProductId == null)
                {
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId));
                }
                else
                {
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId && c.ProductId == ids.ProductId));
                }
            }

            var retVal = devices
                .FirstOrDefault(d =>
                    _UsageSpecification == null ||
                    _UsageSpecification.Length == 0 ||
                    _UsageSpecification.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage));

            var ledgerHidDevice = new WindowsHidDevice(retVal);
            await ledgerHidDevice.InitializeAsync();
            var ledgerManager = new LedgerManager(ledgerHidDevice);
            return ledgerManager;
        }
    }
}
