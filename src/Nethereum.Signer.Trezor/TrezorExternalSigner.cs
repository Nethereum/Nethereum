using hw.trezor.messages;
using hw.trezor.messages.bitcoin;
using hw.trezor.messages.bootloader;
using hw.trezor.messages.common;
using hw.trezor.messages.crypto;
using hw.trezor.messages.debug;
using hw.trezor.messages.definitions;
using hw.trezor.messages.ethereum;
using hw.trezor.messages.ethereumeip712;
using hw.trezor.messages.management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.EIP712;
using Nethereum.Signer.Trezor.Internal;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Trezor.Net;
using EthereumDataType = hw.trezor.messages.ethereumeip712.EthereumTypedDataStructAck.EthereumDataType;
using EthereumFieldType = hw.trezor.messages.ethereumeip712.EthereumTypedDataStructAck.EthereumFieldType;
using EthereumStructMember = hw.trezor.messages.ethereumeip712.EthereumTypedDataStructAck.EthereumStructMember;
using FailureType = hw.trezor.messages.common.Failure.FailureType;

namespace Nethereum.Signer.Trezor
{
    public class TrezorExternalSigner : EthExternalSignerBase
    {
        private readonly string _customPath;

        private readonly uint _index;

        private string? _cachedAddress;

        private readonly ILogger<TrezorExternalSigner> _logger;

        public TrezorManagerBase<MessageType> TrezorManager { get; }

        public override bool CalculatesV { get; protected set; } = true;


        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Transaction;


        public override bool Supported1559 { get; } = true;


        public TrezorExternalSigner(TrezorManagerBase<MessageType> trezorManager, uint index, string? knownAddress = null, ILogger<TrezorExternalSigner>? logger = null)
        {
            _index = index;
            TrezorManager = trezorManager;
            _cachedAddress = NormaliseAddress(knownAddress);
            _logger = logger ?? NullLogger<TrezorExternalSigner>.Instance;
        }

        public TrezorExternalSigner(TrezorManagerBase<MessageType> trezorManager, string customPath, uint index, string? knownAddress = null, ILogger<TrezorExternalSigner>? logger = null)
        {
            _customPath = customPath;
            _index = index;
            TrezorManager = trezorManager;
            _cachedAddress = NormaliseAddress(knownAddress);
            _logger = logger ?? NullLogger<TrezorExternalSigner>.Instance;
        }

        public override async Task<string> GetAddressAsync()
        {
            if (!string.IsNullOrEmpty(_cachedAddress))
            {
                return _cachedAddress;
            }
            _cachedAddress = await RefreshAddressFromDeviceAsync().ConfigureAwait(false);
            return _cachedAddress;
        }

        public async Task<string> RefreshAddressFromDeviceAsync()
        {
            _cachedAddress = (await TrezorManager.SendMessageAsync<EthereumAddress, EthereumGetAddress>(new EthereumGetAddress
            {
                ShowDisplay = false,
                AddressNs = GetPath()
            }).ConfigureAwait(continueOnCapturedContext: false)).Address.ConvertToEthereumChecksumAddress();
            return _cachedAddress;
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
            var response = await TrezorManager
                .SendMessageAsync<EthereumMessageSignature, EthereumSignMessage>(message)
                .ConfigureAwait(false);
            return ECDSASignatureFactory.ExtractECDSASignature(response.Signature);
        }

        public override async Task<EthECDSASignature> SignEthereumMessageAsync(byte[] rawBytes)
        {
            var message = new EthereumSignMessage
            {
                AddressNs = GetPath(),
                Message = rawBytes
            };

            var response = await TrezorManager
                .SendMessageAsync<EthereumMessageSignature, EthereumSignMessage>(message)
                .ConfigureAwait(false);

            var signature = ECDSASignatureFactory.ExtractECDSASignature(response.Signature);
            return new EthECDSASignature(signature);
        }

        public async Task<EthECDSASignature> SignTypedDataHashAsync(byte[] domainSeparatorHash, byte[] messageHash = null, byte[] encodedNetwork = null, byte[] typedDataHash = null)
        {
            if (domainSeparatorHash == null) throw new ArgumentNullException(nameof(domainSeparatorHash));
            if (domainSeparatorHash.Length != 32) throw new ArgumentException("Domain separator hash must be 32 bytes.", nameof(domainSeparatorHash));
            if (messageHash != null && messageHash.Length != 32) throw new ArgumentException("Message hash must be 32 bytes.", nameof(messageHash));
            typedDataHash ??= ComputeTypedDataHash(domainSeparatorHash, messageHash);

            var addressPath = GetPath();
            var request = new EthereumSignTypedHash
            {
                AddressNs = addressPath,
                DomainSeparatorHash = domainSeparatorHash
            };

            _logger.LogInformation("SignTypedDataHashAsync start path={Path} hasMessageHash={HasMessageHash} hasEncodedNetwork={HasEncodedNetwork}",
                FormatPath(addressPath), messageHash != null, encodedNetwork != null);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("SignTypedDataHashAsync domainHash={DomainHash} messageHash={MessageHash} typedHash={TypedHash}",
                    domainSeparatorHash.ToHex(true),
                    messageHash?.ToHex(true) ?? "null",
                    typedDataHash?.ToHex(true) ?? "null");
            }

            if (messageHash != null)
            {
                request.MessageHash = messageHash;
            }

            if (encodedNetwork != null)
            {
                request.EncodedNetwork = encodedNetwork;
            }

            try
            {
                var response = await TrezorManager
                    .SendMessageAsync<EthereumTypedDataSignature, EthereumSignTypedHash>(request)
                    .ConfigureAwait(false);

                _logger.LogInformation("SignTypedDataHashAsync completed hardwareAddress={Address}", response.Address);
                return new EthECDSASignature(ECDSASignatureFactory.ExtractECDSASignature(response.Signature));
            }
            catch (FailureException<Failure> failure) when (failure.Failure?.Code == FailureType.FailureUnexpectedMessage)
            {
                _logger.LogWarning("SignTypedDataHashAsync unsupported on this device, escalating to interactive flow: {Message}", failure.Message);
                throw;
            }
        }

        public override Task<EthECDSASignature> SignTypedDataAsync<TDomain>(TypedData<TDomain> typedData)
        {
            return SignTypedDataAsync(typedData, null);
        }

        public async Task<EthECDSASignature> SignTypedDataAsync<TDomain>(TypedData<TDomain> typedData, byte[] encodedNetwork = null)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            typedData.EnsureDomainRawValuesAreInitialised();
            if (typedData.Message == null)
            {
                throw new ArgumentException("Typed data message is not initialised. Call SetMessage before signing.", nameof(typedData));
            }

            var hashResult = Eip712TypedDataEncoder.Current.CalculateTypedDataHashes(typedData);
            _logger.LogInformation("SignTypedDataAsync start primaryType={PrimaryType} hasMessage={HasMessage}",
                typedData.PrimaryType, typedData.Message != null);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("SignTypedDataAsync domainHash={DomainHash} messageHash={MessageHash} typedHash={TypedHash}",
                    hashResult.DomainHash.ToHex(true),
                    hashResult.MessageHash?.ToHex(true) ?? "null",
                    hashResult.TypedDataHash?.ToHex(true) ?? "null");
            }
            try
            {
                return await SignTypedDataInteractiveAsync(typedData, encodedNetwork).ConfigureAwait(false);
                
            }
            catch (FailureException<Failure> failure) when (failure.Failure?.Code == FailureType.FailureUnexpectedMessage)
            {
                _logger.LogInformation("Interactive typed data signing unavailable: {Message}. Falling back to hash flow.", failure.Message);
               
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Interactive typed data signing failed, falling back to hashflow.");
            }
            return await SignTypedDataHashAsync(hashResult.DomainHash, hashResult.MessageHash, encodedNetwork, hashResult.TypedDataHash).ConfigureAwait(false);
        }

        public async Task<EthECDSASignature> SignTypedDataAsync<TDomain, TMessage>(TypedData<TDomain> typedData, TMessage message, byte[] encodedNetwork = null)
        {
            if (typedData == null) throw new ArgumentNullException(nameof(typedData));
            typedData.SetMessage(message);
            return await SignTypedDataAsync(typedData, encodedNetwork).ConfigureAwait(false);
        }

        public override async Task SignAsync(LegacyTransactionChainId transaction)
        {
            EthereumSignTx txMessage = new EthereumSignTx
            {
                Nonce = transaction.Nonce,
                GasPrice = transaction.GasPrice,
                GasLimit = transaction.GasLimit,
                To = ((transaction.ReceiveAddress != null && transaction.ReceiveAddress.Length != 0) ? transaction.ReceiveAddress.ConvertToEthereumChecksumAddress() : ""),
                Value = transaction.Value,
                AddressNs = GetPath(),
                ChainId = (uint)new BigInteger(transaction.ChainId)
            };
            if (transaction.Data.Length == 0)
            {
                return;
            }
            EthereumTxRequest signature;
            if (transaction.Data.Length <= 1024)
            {
                txMessage.DataInitialChunk = transaction.Data;
                txMessage.DataLength = (uint)transaction.Data.Length;
                signature = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTx>(txMessage).ConfigureAwait(continueOnCapturedContext: false);
                if (signature.SignatureS == null || signature.SignatureR == null)
                {
                    throw new Exception("Signing failure or not accepted");
                }
                transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
                return;
            }
            txMessage.DataLength = (uint)transaction.Data.Length;
            txMessage.DataInitialChunk = transaction.Data.Slice(0, 1024);
            EthereumTxRequest response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTx>(txMessage).ConfigureAwait(continueOnCapturedContext: false);
            int currentPosition = txMessage.DataInitialChunk.Length;
            while (response.DataLength != 0)
            {
                EthereumTxAck request = new EthereumTxAck
                {
                    DataChunk = transaction.Data.Slice(currentPosition, currentPosition + (int)response.DataLength)
                };
                currentPosition += (int)response.DataLength;
                response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumTxAck>(request).ConfigureAwait(continueOnCapturedContext: false);
            }
            signature = response;
            if (signature.SignatureS == null || signature.SignatureR == null)
            {
                throw new Exception("Signing failure or not accepted");
            }
            transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
        }

        public override async Task SignAsync(Transaction1559 transaction)
        {
            Transaction1559Encoder encoder = new Transaction1559Encoder();
            EthereumSignTxEIP1559 txMessage = new EthereumSignTxEIP1559
            {
                Nonce = encoder.GetBigIntegerForEncoding(transaction.Nonce),
                MaxGasFee = encoder.GetBigIntegerForEncoding(transaction.MaxFeePerGas),
                MaxPriorityFee = encoder.GetBigIntegerForEncoding(transaction.MaxPriorityFeePerGas),
                GasLimit = encoder.GetBigIntegerForEncoding(transaction.GasLimit),
                To = ((transaction.ReceiverAddress != null && transaction.ReceiverAddress.Length > 0) ? transaction.ReceiverAddress.ConvertToEthereumChecksumAddress() : ""),
                Value = encoder.GetBigIntegerForEncoding(transaction.Amount),
                AddressNs = GetPath(),
                ChainId = (ulong)transaction.ChainId
            };
            var dataHex = transaction.Data ?? string.Empty;
            var data = dataHex.HexToByteArray();
            EthereumTxRequest signature;
            if (data.Length == 0)
            {
                txMessage.DataInitialChunk = Array.Empty<byte>();
                txMessage.DataLength = 0;
                signature = await TrezorManager
                    .SendMessageAsync<EthereumTxRequest, EthereumSignTxEIP1559>(txMessage)
                    .ConfigureAwait(false);
            }
            else if (data.Length <= 1024)
            {
                txMessage.DataInitialChunk = data;
                txMessage.DataLength = (uint)data.Length;
                signature = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTxEIP1559>(txMessage).ConfigureAwait(continueOnCapturedContext: false);
                if (signature.SignatureS == null || signature.SignatureR == null)
                {
                    throw new Exception("Signing failure or not accepted");
                }
                transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
                return;
            }
            txMessage.DataLength = (uint)data.Length;
            txMessage.DataInitialChunk = data.AsSpan(0, Math.Min(1024, data.Length)).ToArray();
            EthereumTxRequest response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumSignTxEIP1559>(txMessage).ConfigureAwait(continueOnCapturedContext: false);
            int currentPosition = txMessage.DataInitialChunk.Length;
            while (response.DataLength != 0)
            {
                EthereumTxAck request = new EthereumTxAck
                {
                    DataChunk = data.AsSpan(currentPosition, (int)response.DataLength).ToArray()
                };
                currentPosition += (int)response.DataLength;
                response = await TrezorManager.SendMessageAsync<EthereumTxRequest, EthereumTxAck>(request).ConfigureAwait(continueOnCapturedContext: false);
            }
            signature = response;
            if (signature.SignatureS == null || signature.SignatureR == null)
            {
                throw new Exception("Signing failure or not accepted");
            }
            transaction.SetSignature(EthECDSASignatureFactory.FromComponents(signature.SignatureR, signature.SignatureS, (byte)signature.SignatureV));
        }

        public override async Task SignAsync(LegacyTransaction transaction)
        {
            throw new System.NotSupportedException("Please provide a chain Id");
        }

        public uint[] GetPath()
        {
            KeyPath path;
            if (!string.IsNullOrEmpty(_customPath))
            {
                path = KeyPath.Parse(_customPath).Derive(_index);
                return path.Indexes;
            }
            path = KeyPath.Parse("m/44'/60'/0'/0").Derive(_index);
            return path.Indexes;
        }

        public override async Task SignAsync(Transaction7702 transaction)
        {
            throw new System.NotSupportedException("Not supported by Trezor");
        }

        private static string? NormaliseAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }
            return address.ConvertToEthereumChecksumAddress();
        }

        private async Task<EthECDSASignature> SignTypedDataInteractiveAsync<TDomain>(TypedData<TDomain> typedData, byte[] encodedNetwork)
        {
            var typeDefinitions = typedData.Types;
            var domainData = ConvertStruct("EIP712Domain", typedData.DomainRawValues, typeDefinitions);
            var messageData = ConvertStruct(typedData.PrimaryType, typedData.Message, typeDefinitions);

            var path = GetPath();
            var request = new EthereumSignTypedData
            {
                AddressNs = path,
                PrimaryType = typedData.PrimaryType,
                MetamaskV4Compat = true
            };

            _logger.LogInformation("SignTypedDataInteractive start primaryType={PrimaryType} path={Path}", typedData.PrimaryType, FormatPath(path));

            if (encodedNetwork != null)
            {
                request.Definitions = new EthereumDefinitions { EncodedNetwork = encodedNetwork };
            }

            object response = await TrezorManager
                .SendMessageAsync<object, EthereumSignTypedData>(request)
                .ConfigureAwait(false);

            response = await HandleInteractiveControlResponsesAsync(response).ConfigureAwait(false);

            while (response is EthereumTypedDataStructRequest structRequest)
            {
                _logger.LogDebug("StructRequest name={Name}", structRequest.Name);
                var ack = new EthereumTypedDataStructAck();
                if (typeDefinitions.TryGetValue(structRequest.Name, out var structMembers))
                {
                    foreach (var field in structMembers)
                    {
                        ack.Members.Add(new EthereumStructMember
                        {
                            Name = field.Name,
                            Type = BuildFieldType(field.Type, typeDefinitions)
                        });
                    }
                }

                response = await TrezorManager
                    .SendMessageAsync<object, EthereumTypedDataStructAck>(ack)
                    .ConfigureAwait(false);

                response = await HandleInteractiveControlResponsesAsync(response).ConfigureAwait(false);
            }

            while (response is EthereumTypedDataValueRequest valueRequest)
            {
                var memberPath = valueRequest.MemberPaths ?? Array.Empty<uint>();
                var (memberValue, memberType) = ResolveMemberData(memberPath, domainData, messageData, typedData.PrimaryType, typeDefinitions);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("ValueRequest path={Path} resolvedType={ResolvedType} valueType={ValueType}",
                        FormatPath(memberPath), memberType, memberValue?.GetType().Name ?? "null");
                }

                byte[] encoded;
                if (memberValue is IList list)
                {
                    var length = (ushort)list.Count;
                    encoded = new byte[2];
                    encoded[0] = (byte)(length >> 8);
                    encoded[1] = (byte)(length & 0xFF);
                }
                else
                {
                    encoded = EncodeMemberValue(memberValue, memberType);
                }

                response = await TrezorManager
                    .SendMessageAsync<object, EthereumTypedDataValueAck>(new EthereumTypedDataValueAck { Value = encoded })
                    .ConfigureAwait(false);

                response = await HandleInteractiveControlResponsesAsync(response).ConfigureAwait(false);
            }

            if (response is EthereumTypedDataSignature typedDataSignature)
            {
                _logger.LogInformation("SignTypedDataInteractive completed hardwareAddress={Address}", typedDataSignature.Address);
                return new EthECDSASignature(ECDSASignatureFactory.ExtractECDSASignature(typedDataSignature.Signature));
            }

            _logger.LogError("SignTypedDataInteractive unexpected response type {ResponseType}", response?.GetType().Name ?? "null");
            throw new InvalidOperationException($"Unexpected response type {response?.GetType().Name} while signing typed data");
        }

        private static (object Value, string TypeName) ResolveMemberData(uint[] memberPath, IDictionary<string, object> domainData, IDictionary<string, object> messageData, string primaryType, IDictionary<string, MemberDescription[]> types)
        {
            object currentData;
            string currentTypeName;

            if (memberPath[0] == 0)
            {
                currentData = domainData;
                currentTypeName = "EIP712Domain";
            }
            else
            {
                currentData = messageData;
                currentTypeName = primaryType;
            }

            for (var i = 1; i < memberPath.Length; i++)
            {
                var index = (int)memberPath[i];
                if (currentData is IDictionary<string, object> dict)
                {
                    var member = types[currentTypeName][index];
                    currentTypeName = member.Type;
                    currentData = dict[member.Name];
                }
                else if (currentData is IList list)
                {
                    currentTypeName = StripArraySuffix(currentTypeName);
                    currentData = list[index];
                }
            }

            return (currentData, currentTypeName);
        }

        private static IDictionary<string, object> ConvertStruct(string typeName, IEnumerable<MemberValue> members, IDictionary<string, MemberDescription[]> types)
        {
            var result = new Dictionary<string, object>();
            var memberValues = members?.ToArray() ?? Array.Empty<MemberValue>();
            var descriptions = types.TryGetValue(typeName, out var desc) ? desc : Array.Empty<MemberDescription>();

            for (var i = 0; i < descriptions.Length && i < memberValues.Length; i++)
            {
                result[descriptions[i].Name] = ConvertMemberValue(memberValues[i], descriptions[i].Type, types);
            }

            return result;
        }

        private static object ConvertMemberValue(MemberValue memberValue, string typeName, IDictionary<string, MemberDescription[]> types)
        {
            if (types.ContainsKey(typeName) && memberValue.Value is IEnumerable<MemberValue> nested)
            {
                return ConvertStruct(typeName, nested, types);
            }

            if (typeName.EndsWith("[]") && memberValue.Value is IList list)
            {
                var elementType = StripArraySuffix(typeName);
                return list.Cast<object>()
                    .Select(v => ConvertMemberValue(new MemberValue { TypeName = elementType, Value = v }, elementType, types))
                    .ToList();
            }

            return memberValue.Value;
        }

        private static string StripArraySuffix(string typeName)
        {
            var index = typeName.IndexOf('[');
            return index > 0 ? typeName.Substring(0, index) : typeName;
        }

        private static EthereumFieldType BuildFieldType(string typeName, IDictionary<string, MemberDescription[]> types)
        {
            if (typeName.EndsWith("[]"))
            {
                return new EthereumFieldType
                {
                    DataType = EthereumDataType.Array,
                    EntryType = BuildFieldType(StripArraySuffix(typeName), types)
                };
            }

            if (typeName.StartsWith("uint") || typeName.StartsWith("int"))
            {
                var size = (uint)ParseTypeSize(typeName);
                return new EthereumFieldType
                {
                    DataType = typeName.StartsWith("uint") ? EthereumDataType.Uint : EthereumDataType.Int,
                    Size = size
                };
            }

            if (typeName.StartsWith("bytes"))
            {
                var field = new EthereumFieldType
                {
                    DataType = EthereumDataType.Bytes
                };
                if (typeName != "bytes")
                {
                    field.Size = (uint)ParseTypeSize(typeName);
                }
                return field;
            }

            if (typeName == "string") return new EthereumFieldType { DataType = EthereumDataType.String };
            if (typeName == "bool") return new EthereumFieldType { DataType = EthereumDataType.Bool };
            if (typeName == "address") return new EthereumFieldType { DataType = EthereumDataType.Address };

            if (types.ContainsKey(typeName))
            {
                return new EthereumFieldType
                {
                    DataType = EthereumDataType.Struct,
                    StructName = typeName,
                    Size = (uint)types[typeName].Length
                };
            }

            throw new System.NotSupportedException($"Unsupported EIP-712 field type {typeName}");
        }

        private static int ParseTypeSize(string typeName)
        {
            var digits = new string(typeName.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? 0 : int.Parse(digits) / 8;
        }

        private static byte[] EncodeMemberValue(object value, string typeName)
        {
            typeName = StripArraySuffix(typeName);

            if (typeName.StartsWith("bytes"))
            {
                var bytes = value switch
                {
                    byte[] b => b,
                    string s => s.HexToByteArray(),
                    _ => throw new System.NotSupportedException($"Cannot encode bytes value of type {value?.GetType().Name}")
                };
                if (typeName != "bytes")
                {
                    var size = ParseTypeSize(typeName);
                    return bytes.Length == size ? bytes : bytes.Length < size ? PadLeft(bytes, size) : bytes.Take(size).ToArray();
                }
                return bytes;
            }

            if (typeName == "string")
            {
                return Encoding.UTF8.GetBytes(value?.ToString() ?? string.Empty);
            }

            if (typeName == "bool")
            {
                var flag = value is bool b ? b : Convert.ToBoolean(value);
                return new[] { flag ? (byte)1 : (byte)0 };
            }

            if (typeName == "address")
            {
                return (value?.ToString() ?? string.Empty).HexToByteArray();
            }

            if (typeName.StartsWith("uint") || typeName.StartsWith("int"))
            {
                var sizeBytes = ParseTypeSize(typeName);
                var bigInt = value switch
                {
                    BigInteger bigInteger => bigInteger,
                    string s => BigInteger.Parse(s),
                    _ => new BigInteger(Convert.ToDecimal(value))
                };
                return EncodeInteger(bigInt, sizeBytes, typeName.StartsWith("int"));
            }

            throw new System.NotSupportedException($"Unsupported EIP-712 member type {typeName}");
        }

        private static byte[] EncodeInteger(BigInteger value, int sizeBytes, bool signed)
        {
            var bytes = value.ConvertToByteArray(false);
            if (bytes.Length > sizeBytes)
            {
                if (bytes.Length == sizeBytes + 1 && bytes[0] == 0)
                {
                    bytes = bytes.Skip(1).ToArray();
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Integer value does not fit in specified size");
                }
            }

            var buffer = new byte[sizeBytes];
            Buffer.BlockCopy(bytes, 0, buffer, sizeBytes - bytes.Length, bytes.Length);
            return buffer;
        }

        private static byte[] PadLeft(byte[] value, int size)
        {
            if (value.Length >= size) return value;
            var buffer = new byte[size];
            Buffer.BlockCopy(value, 0, buffer, size - value.Length, value.Length);
            return buffer;
        }

        private static byte[] ComputeTypedDataHash(byte[] domainHash, byte[] messageHash)
        {
            var buffer = new byte[2 + domainHash.Length + (messageHash != null ? messageHash.Length : 0)];
            buffer[0] = 0x19;
            buffer[1] = 0x01;
            Buffer.BlockCopy(domainHash, 0, buffer, 2, domainHash.Length);
            if (messageHash != null)
            {
                Buffer.BlockCopy(messageHash, 0, buffer, 2 + domainHash.Length, messageHash.Length);
            }
            return Sha3Keccack.Current.CalculateHash(buffer);
        }

        private static string FormatPath(uint[]? path)
        {
            if (path == null || path.Length == 0)
            {
                return "(empty)";
            }
            return string.Join("/", path);
        }

        private async Task<object> HandleInteractiveControlResponsesAsync(object response)
        {
            while (true)
            {
                switch (response)
                {
                    case ButtonRequest _:
                        _logger.LogDebug("ButtonRequest received during typed data flow, sending ButtonAck");
                        response = await TrezorManager.SendMessageAsync<object, ButtonAck>(new ButtonAck()).ConfigureAwait(false);
                        continue;
                    default:
                        return response;
                }
            }
        }

        public override async Task<string> SignTypedDataJsonAsync(string typedDataJson, string messageKeySelector = "message")
        {
            if (string.IsNullOrWhiteSpace(typedDataJson))
            {
                throw new ArgumentException("Typed data json cannot be null or empty.", nameof(typedDataJson));
            }

            var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData<Domain>(typedDataJson, messageKeySelector);
            var typedData = new TypedData<Domain>
            {
                PrimaryType = typedDataRaw.PrimaryType,
                Types = typedDataRaw.Types,
                DomainRawValues = typedDataRaw.DomainRawValues,
                Message = typedDataRaw.Message
            };

            var signature = await SignTypedDataAsync(typedData);
            return EthECDSASignature.CreateStringSignature(signature);
        }
    }
}
