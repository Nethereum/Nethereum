using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Trezor.Net;
using Trezor.Net.Contracts.Ethereum;

namespace Nethereum.Signer.Trezor
{

    public class TrezorExternalSigner: EthExternalSignerBase
    {
        private readonly string _customPath;
        private readonly uint _index;
        private readonly bool _legacyPath;
        public TrezorManager TrezorManager { get; }
        public override bool CalculatesV { get; protected set; } = true;

        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Transaction;

        public TrezorExternalSigner(TrezorManager trezorManager, uint index)
        { 
            _index = index;
            TrezorManager = trezorManager;
        }

        public TrezorExternalSigner(TrezorManager trezorManager, string customPath, uint index)
        {
            _customPath = customPath;
            _index = index;
            TrezorManager = trezorManager;
        }

        public override async Task<string> GetAddressAsync()
        {
           var addressResponse = await TrezorManager.SendMessageAsync<EthereumAddress, EthereumGetAddress>(new EthereumGetAddress { ShowDisplay = false, AddressNs = GetPath() });
           return addressResponse.Address.ToHex(true).ConvertToEthereumChecksumAddress();
        }

        protected override Task<byte[]> GetPublicKeyAsync()
        {
            throw new Exception("Not implemented interface to retrieve the public key from Trezor");
        }

        protected override async Task<ECDSASignature> SignExternallyAsync(byte[] bytes)
        {
            var message = new EthereumSignMessage
            {
                AddressNs = GetPath(),
                Message = bytes
            };

            var messageSignature = await TrezorManager.SendMessageAsync<EthereumMessageSignature, EthereumSignMessage>(message);
            return ECDSASignatureFactory.ExtractECDSASignature(messageSignature.Signature);
        }

        public override async Task SignAsync(TransactionChainId transaction)
        {
            var txMessage = new EthereumSignTx
            {
                Nonce = transaction.Nonce,
                GasPrice = transaction.GasPrice,
                GasLimit = transaction.GasLimit,
                To = transaction.ReceiveAddress,
                Value = transaction.Value,
                AddressNs = GetPath(),
                ChainId =  (uint)new BigInteger(transaction.ChainId)
            };

            if (transaction.Data.Length > 0)
            {
                txMessage.DataInitialChunk = transaction.Data;
                txMessage.DataLength = (uint)transaction.Data.Length;
            }

            var signature = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTx>(txMessage);
            if (signature.SignatureS == null || signature.SignatureR == null) throw new Exception("Signing failure or not accepted");
            transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
        }

        public override async Task SignAsync(Transaction transaction)
        {
            var txMessage = new EthereumSignTx
            {
                Nonce = transaction.Nonce,
                GasPrice = transaction.GasPrice,
                GasLimit = transaction.GasLimit,
                To = transaction.ReceiveAddress,
                Value = transaction.Value,
                AddressNs = GetPath(),
            };

            if (transaction.Data.Length > 0)
            {
                txMessage.DataInitialChunk = transaction.Data;
                txMessage.DataLength = (uint)transaction.Data.Length;
            }

            var signature = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTx>(txMessage);
            transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
        }

        public uint[] GetPath()
        {
            if (!string.IsNullOrEmpty(_customPath))
            {
                var path = KeyPath.Parse(_customPath).Derive(_index);
                return path.Indexes;
            }
            else
            {
                var path = KeyPath.Parse("m/44'/60'/0'/0").Derive(_index);
                return path.Indexes;
            }
        }
    }
}
