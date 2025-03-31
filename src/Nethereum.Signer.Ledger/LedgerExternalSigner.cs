using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Device.Net;
using Hardwarewallets.Net.Model;
using Ledger.Net;
using Ledger.Net.Exceptions;
using Ledger.Net.Requests;
using Ledger.Net.Responses;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Web3.Accounts;


namespace Nethereum.Ledger
{

    public class LedgerExternalSigner : EthExternalSignerBase
    {
        private readonly uint _index;
        private readonly string _customPath;
        private readonly bool _legacyPath;
        public LedgerManager LedgerManager { get; }
        public byte[] CurrentPublicKey { get; set; }

        /// <summary>
        /// Ledger does not support a V bigger than a byte, the response only returns 1 byte and r+s so calculations using the chainId will fail that result on something bigger than a byte.
        /// </summary>
        public override bool CalculatesV { get; protected set; } = false;

        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.RLP;

        public LedgerExternalSigner(LedgerManager ledgerManager, uint index, bool legacyPath = false)
        {

            _index = index;
            _legacyPath = legacyPath;
            LedgerManager = ledgerManager;

            LedgerManager.SetCoinNumber(60);
        }

        public LedgerExternalSigner(LedgerManager ledgerManager, uint index, string customPath)
        {
            _index = index;
            _customPath = customPath;
            LedgerManager = ledgerManager;
            LedgerManager.SetCoinNumber(60);
        }

        protected override async Task<byte[]> GetPublicKeyAsync()
        {
            if (CurrentPublicKey == null)
            {
                var path = GetPath();
                var publicKeyResponse = await LedgerManager.RequestHandler.SendRequestAsync<EthereumAppGetPublicKeyResponse, EthereumAppGetPublicKeyRequest>(new EthereumAppGetPublicKeyRequest(true, false, path)).ConfigureAwait(false);
                if (publicKeyResponse.IsSuccess)
                {
                    CurrentPublicKey = publicKeyResponse.PublicKeyData;
                    return publicKeyResponse.PublicKeyData;
                }
                throw new Exception(publicKeyResponse.StatusMessage);
            }
            return CurrentPublicKey;
        }

        protected override async Task<ECDSASignature> SignExternallyAsync(byte[] hash)
        {
            var path = GetPath();

            var firstRequest = new EthereumAppSignatureRequest(true, path.Concat(hash).ToArray());

            var response = await LedgerManager.RequestHandler.SendRequestAsync<EthereumAppSignatureResponse, EthereumAppSignatureRequest>(firstRequest).ConfigureAwait(false);
            if (response.SignatureS == null || response.SignatureR == null) throw new Exception("Signing failure or not accepted");
            var signature = ECDSASignatureFactory.FromComponents(response.SignatureR, response.SignatureS);
            signature.V = new BigInteger(response.SignatureV).ToBytesForRLPEncoding();
            return signature;
        }

        public override async Task SignAsync(LegacyTransactionChainId transaction)
        {
            await SignRLPTransactionAsync(transaction).ConfigureAwait(false);
        }

        public override async Task SignAsync(LegacyTransaction transaction)
        {
            await SignRLPTransactionAsync(transaction).ConfigureAwait(false);
        }

        public override Task SignAsync(Transaction1559 transaction)
        {
            return SignRLPTransactionAsync(transaction);
        }

        public override bool Supported1559 { get; } = true;
        public byte[] GetPath()
        {
            if (!string.IsNullOrEmpty(_customPath))
            {
                var path = KeyPath.Parse(_customPath).Derive(_index);
                return GetByteData(path.Indexes);
            }

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

        public override Task SignAsync(Transaction7702 transaction)
        {
            throw new NotImplementedException();
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

        internal static byte[] ReadAllBytes(this Stream stream, int totalByteCount)
        {
            var data = new byte[totalByteCount];
            var totalReadCount = 0;
            int readCount;
            do
            {
                totalReadCount += (readCount = stream.Read(data, totalReadCount, totalByteCount - totalReadCount));
            } while (readCount > 0 && totalReadCount < totalByteCount);
            return data;
        }
    }
}
    


