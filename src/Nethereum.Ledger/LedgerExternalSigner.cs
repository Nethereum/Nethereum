using System;
using System.Collections.Generic;
using System.IO;
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


    public class LedgerFactory
    {
        public static VendorProductIds[] WellKnownLedgerWallets = new VendorProductIds[]
        {
            new VendorProductIds(0x2c97),
            new VendorProductIds(0x2581, 0x3b7c)
        };


        private static readonly UsageSpecification[] _UsageSpecification = new[] { new UsageSpecification(0xffa0, 0x01) };

        public static async Task<IHidDevice> GetWindowsConnectedLedgerHidDevice()
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

            var deviceFound = devices
                .FirstOrDefault(d =>
                    _UsageSpecification == null ||
                    _UsageSpecification.Length == 0 ||
                    _UsageSpecification.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage));

           var ledgerHidDevice = new WindowsHidDevice(deviceFound);
           await ledgerHidDevice.InitializeAsync();
           return ledgerHidDevice;
        }

        public static async Task<LedgerManager> GetWindowsConnectedLedgerManager()
        {
            var ledgerHidDevice = await GetWindowsConnectedLedgerHidDevice();
            return new LedgerManager(ledgerHidDevice);
        }
    }


    public class LedgerExternalSigner:IEthExternalSigner
    {
        private readonly uint _index;
        private readonly bool _legacyPath;
        public LedgerManager LedgerManager { get; }
      
        public bool CalculatesV { get; } = true;
        public ExternalSignerFormat ExternalSignerFormat { get; } = ExternalSignerFormat.RLP;

        public LedgerExternalSigner(LedgerManager ledgerManager, uint index, bool legacyPath = false)
        {
            _index = index;
            _legacyPath = legacyPath;
            LedgerManager = ledgerManager;
            LedgerManager.SetCoinNumber(60);
        }

        public async Task<byte[]> GetPublicKeyAsync()
        {
            var path = GetPath();
            var publicKeyResponse = await LedgerManager.SendRequestAsync<EthereumAppGetPublicKeyResponse, EthereumAppGetPublicKeyRequest>(new EthereumAppGetPublicKeyRequest(true, false, path));
            if (publicKeyResponse.IsSuccess)
            {
                return publicKeyResponse.PublicKeyData;
            }

            throw new Exception(publicKeyResponse.StatusMessage);
        }

        public async Task<ECDSASignature> SignAsync(byte[] hash)
        {
            var path = GetPath();

            var firstRequest = new EthereumAppSignTransactionRequest(true, path.Concat(hash).ToArray());

            var response = await LedgerManager.SendRequestAsync<EthereumAppSignTransactionResponse, EthereumAppSignTransactionRequest>(firstRequest);

            var signature = ECDSASignatureFactory.FromComponents(response.SignatureR, response.SignatureS);
            signature.V = new BigInteger(response.SignatureV).ToBytesForRLPEncoding();
            return signature;
        }

        public byte[] GetPath()
        {
            if (_legacyPath)
            {
                var path = KeyPath.Parse("m/44'/60'/0'").Derive(_index);
                return GetByteData(path.Indexes);
            }
            else
            {
                var path = KeyPath.Parse("m/44'/60'/0'/0").Derive(_index);
                return GetByteData(path.Indexes);
            }
        }


        private static byte[] GetByteData(uint[] indices)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                memoryStream.WriteByte((byte)indices.Length);
                for (int index = 0; index < indices.Length; ++index)
                {
                    byte[] bytes = indices[index].ToBytes();
                    memoryStream.Write(bytes, 0, bytes.Length);
                }
                return memoryStream.ToArray();
            }
        }
    }

    internal static class ExtensionMethods
    {
        internal static byte[] ToBytes(this uint value)
        {
            return new byte[4]
            {
                (byte) (value >> 24),
                (byte) (value >> 16),
                (byte) (value >> 8),
                (byte) value
            };
        }
    }



}
