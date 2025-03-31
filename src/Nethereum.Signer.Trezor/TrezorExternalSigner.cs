using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Trezor.Internal;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Trezor.Net;
using Trezor.Net.Contracts.Ethereum;

namespace Nethereum.Signer.Trezor
{

    public class TrezorExternalSigner : EthExternalSignerBase
    {
        private readonly string _customPath;
        private readonly uint _index;
        private readonly bool _legacyPath;
        public TrezorManagerBase<ExtendedMessageType.MessageType> TrezorManager { get; }
        public override bool CalculatesV { get; protected set; } = true;

        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Transaction;

        public TrezorExternalSigner(TrezorManagerBase<ExtendedMessageType.MessageType> trezorManager, uint index)
        {
            _index = index;
            TrezorManager = trezorManager;
        }

        public TrezorExternalSigner(TrezorManagerBase<ExtendedMessageType.MessageType> trezorManager, string customPath, uint index)
        {
            _customPath = customPath;
            _index = index;
            TrezorManager = trezorManager;
        }

        public override async Task<string> GetAddressAsync()
        {
            var addressResponse = await TrezorManager.SendMessageAsync<EthereumAddress, EthereumGetAddress>(new EthereumGetAddress { ShowDisplay = false, AddressNs = GetPath() }).ConfigureAwait(false);
            return addressResponse.Address.ConvertToEthereumChecksumAddress();
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

            var messageSignature = await TrezorManager.SendMessageAsync<EthereumMessageSignature, EthereumSignMessage>(message).ConfigureAwait(false);
            return ECDSASignatureFactory.ExtractECDSASignature(messageSignature.Signature);
        }

        public override async Task SignAsync(LegacyTransactionChainId transaction)
        {
            var txMessage = new EthereumSignTx
            {
                Nonce = transaction.Nonce,
                GasPrice = transaction.GasPrice,
                GasLimit = transaction.GasLimit,
                To = (transaction.ReceiveAddress != null && transaction.ReceiveAddress.Length > 0) ? transaction.ReceiveAddress.ConvertToEthereumChecksumAddress() : "",
                Value = transaction.Value,
                AddressNs = GetPath(),
                ChainId = (uint)new BigInteger(transaction.ChainId)
            };

            if (transaction.Data.Length > 0)
            {
                if (transaction.Data.Length <= 1024)
                {
                    txMessage.DataInitialChunk = transaction.Data;
                    txMessage.DataLength = (uint)transaction.Data.Length;
                    var signature = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTx>(txMessage).ConfigureAwait(false);
                    if (signature.SignatureS == null || signature.SignatureR == null) throw new Exception("Signing failure or not accepted");
                    transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
                }
                else
                {
                    txMessage.DataLength = (uint)transaction.Data.Length;
                    txMessage.DataInitialChunk = transaction.Data.Slice(0, 1024);
                    var response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTx>(txMessage).ConfigureAwait(false);
                    var currentPosition = txMessage.DataInitialChunk.Length;
                    while (response.DataLength > 0)
                    {
                        var request = new EthereumTxAck();
                        request.DataChunk = transaction.Data.Slice(currentPosition, currentPosition + (int)response.DataLength);
                        currentPosition = currentPosition + (int)response.DataLength;
                        response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumTxAck>(request).ConfigureAwait(false);
                    }
                    var signature = response;
                    if (signature.SignatureS == null || signature.SignatureR == null) throw new Exception("Signing failure or not accepted");
                    transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
                }
            }
        }
        public override async Task SignAsync(Transaction1559 transaction)
        {
            var encoder = new Transaction1559Encoder();
            var txMessage = new EthereumSignTxEIP1559
            {
                Nonce = encoder.GetBigIntegerForEncoding(transaction.Nonce),
                MaxGasFee = encoder.GetBigIntegerForEncoding(transaction.MaxFeePerGas),
                MaxPriorityFee = encoder.GetBigIntegerForEncoding(transaction.MaxPriorityFeePerGas),
                GasLimit = encoder.GetBigIntegerForEncoding(transaction.GasLimit),
                To = (transaction.ReceiverAddress != null && transaction.ReceiverAddress.Length > 0) ? transaction.ReceiverAddress.ConvertToEthereumChecksumAddress() : "",
                Value = encoder.GetBigIntegerForEncoding(transaction.Amount),
                AddressNs = GetPath(),
                ChainId = (ulong)transaction.ChainId
            };

            byte[] data = transaction.Data.HexToByteArray();
            if (transaction.Data.Length > 0)
            {
                if (transaction.Data.Length <= 1024)
                {
                    txMessage.DataInitialChunk = data;
                    txMessage.DataLength = (uint)data.Length;
                    var signature = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTxEIP1559>(txMessage).ConfigureAwait(false);
                    if (signature.SignatureS == null || signature.SignatureR == null) throw new Exception("Signing failure or not accepted");
                    transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
                }
                else
                {

                    txMessage.DataLength = (uint)data.Length;
                    txMessage.DataInitialChunk = data.Slice(0, 1024);
                    var response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTxEIP1559>(txMessage).ConfigureAwait(false);
                    var currentPosition = txMessage.DataInitialChunk.Length;
                    while (response.DataLength > 0)
                    {
                        var request = new EthereumTxAck();
                        request.DataChunk = data.Slice(currentPosition, currentPosition + (int)response.DataLength);
                        currentPosition = currentPosition + (int)response.DataLength;
                        response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumTxAck>(request).ConfigureAwait(false);
                    }
                    var signature = response;
                    if (signature.SignatureS == null || signature.SignatureR == null) throw new Exception("Signing failure or not accepted");
                    transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
                }
            }
        }

        public override bool Supported1559 { get; } = true;

        public override async Task SignAsync(LegacyTransaction transaction)
        {
            throw new System.NotSupportedException("Please provide a chain Id");
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

        public override Task SignAsync(Transaction7702 transaction)
        {
            throw new NotImplementedException();
        }
    }
 }
