using System;
using System.Collections.Generic;
using System.Text;
using Device.Net;
using Hardwarewallets.Net.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Trezor.Net.Contracts;
using hw.trezor.messages;
using hw.trezor.messages.common;
using hw.trezor.messages.ethereum;
using hw.trezor.messages.ethereumeip712;
using hw.trezor.messages.management;
using hw.trezor.messages.crypto;
using hw.trezor.messages.debug;
using hw.trezor.messages.bootloader;
using hw.trezor.messages.bitcoin;
using hw.trezor.messages.definitions;
using Trezor.Net;
using BackwardsCompatible = Trezor.Net.Contracts.BackwardsCompatible;
using Trezor.Net.Manager;

namespace Nethereum.Signer.Trezor.Internal
{

    public class ExtendedTrezorManager : TrezorManagerBase<MessageType>
        {

            #region Private Fields

            private const string LogSection = "Extended Trezor Manager";
            private bool disposed;

            #endregion Private Fields

            #region Public Constructors

            public ExtendedTrezorManager(
                EnterPinArgs enterPinCallback,
                EnterPinArgs enterPassphraseCallback,
                IDevice trezorDevice,
                ILogger<ExtendedTrezorManager> logger = null,
                ICoinUtility coinUtility = null) : base(
                    enterPinCallback,
                    enterPassphraseCallback,
                    trezorDevice,
                    (ILogger<ExtendedTrezorManager>)logger,
                    coinUtility)
            {
            }

            #endregion Public Constructors

            #region Public Properties

            public static IReadOnlyList<FilterDeviceDefinition> DeviceDefinitions { get; } = new ReadOnlyCollection<FilterDeviceDefinition>(new List<FilterDeviceDefinition>
            {
                new FilterDeviceDefinition(vendorId: 0x534C, productId: 0x0001, label: "Trezor One Firmware 1.6.x", usagePage: 65280),
                new FilterDeviceDefinition(vendorId: 0x534C, productId: 0x0001, label: "Trezor One Firmware 1.6.x (Android Only)"),
                new FilterDeviceDefinition(vendorId: 0x1209, productId: 0x53C1, label: "Trezor One Firmware 1.7.x"),
                new FilterDeviceDefinition(vendorId: 0x1209, productId: 0x53C0, label: "Model T")
            });

            public Features Features { get; private set; }

            public override bool IsInitialized => Features != null;

            #endregion Public Properties

            #region Protected Properties

            protected override string ContractNamespace => "Trezor.Net.Contracts";

            protected override bool? IsOldFirmware => Features?.MajorVersion < 2 && Features?.MinorVersion < 8;

            protected override Type MessageTypeType => typeof(MessageType);

            #endregion Protected Properties

            #region Public Methods

            public override void Dispose()
            {
                if (disposed) return;
                disposed = true;

                base.Dispose();
            }

            public override Task<string> GetAddressAsync(IAddressPath addressPath, bool isPublicKey, bool display)
            {
                if (CoinUtility == null)
                {
                    throw new ManagerException($"A {nameof(CoinUtility)} must be specified if {nameof(AddressType)} is not specified.");
                }

                if (addressPath == null) throw new ArgumentNullException(nameof(addressPath));

                var cointType = addressPath.AddressPathElements.Count > 1 ? addressPath.AddressPathElements[1].Value : throw new ManagerException("The first element of the address path is considered to be the coin type. This was not specified so no coin information is available. Please use an overload that specifies CoinInfo.");

                var coinInfo = CoinUtility.GetCoinInfo(cointType);

                return GetAddressAsync(addressPath, isPublicKey, display, coinInfo);
            }

            public Task<string> GetAddressAsync(IAddressPath addressPath, bool isPublicKey, bool display, CoinInfo coinInfo)
            {
                if (coinInfo == null) throw new ArgumentNullException(nameof(coinInfo));

                var inputScriptType = coinInfo.IsSegwit ? InputScriptType.Spendp2shwitness : InputScriptType.Spendaddress;

                return GetAddressAsync(addressPath, isPublicKey, display, coinInfo.AddressType, inputScriptType, coinInfo.CoinName);
            }

            public Task<string> GetAddressAsync(IAddressPath addressPath, bool isPublicKey, bool display, AddressType addressType, InputScriptType inputScriptType) => GetAddressAsync(addressPath, isPublicKey, display, addressType, inputScriptType, null);

            public async Task<string> GetAddressAsync(
                IAddressPath addressPath,
                bool isPublicKey,
                bool display,
                AddressType addressType,
                InputScriptType inputScriptType,
                string coinName)
            {
                try
                {
                    if (addressPath == null) throw new ArgumentNullException(nameof(addressPath));

                    var path = addressPath.ToArray();

                    if (isPublicKey)
                    {
                        var publicKey = await SendMessageAsync<PublicKey, GetPublicKey>(new GetPublicKey { CoinName = coinName, AddressNs = path, ShowDisplay = display, ScriptType = inputScriptType }).ConfigureAwait(false);
                        return publicKey.Xpub;
                    }

                    switch (addressType)
                    {
                        case AddressType.Bitcoin:

                            //Ultra hack to deal with a coin name change in Firmware Version 1.6.2
                            if (Features.MajorVersion <= 1 && Features.MinorVersion < 6 && string.Equals(coinName, "Bgold", StringComparison.Ordinal))
                            {
                                coinName = "Bitcoin Gold";
                            }

                            return (await SendMessageAsync<Address, GetAddress>(new GetAddress { ShowDisplay = display, AddressNs = path, CoinName = coinName, ScriptType = inputScriptType }).ConfigureAwait(false)).address;

                        case AddressType.Ethereum:
                            return await GetEthereumAddress(display, path).ConfigureAwait(false);

                        default:
                            throw new NotImplementedException();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error Getting Trezor Address {LogSection}", LogSection);
                    throw;
                }
            }

            /// <summary>
            /// Initialize the Trezor. Should only be called once.
            /// </summary>
            public override async Task InitializeAsync()
            {
                if (disposed) throw new ManagerException("Initialization cannot occur after disposal");
                Logger.LogInformation("Initializing Trezor {LogSection}", LogSection);
                Features = await SendMessageAsync<Features, Initialize>(new Initialize()).ConfigureAwait(false);

                Logger.LogInformation("Trezor Initialized. Model: {Model}, Firmware Version: {MajorVersion}.{MinorVersion}.{PatchVersion} {LogSection}", Features.Model, Features.MajorVersion, Features.MinorVersion, Features.PatchVersion, LogSection);
            
                if (Features == null)
                {
                    throw new ManagerException("Error initializing Trezor. Features were not retrieved");
                }
            }

            #endregion Public Methods

            #region Protected Methods

            protected override async Task<object> ButtonAckAsync()
            {
                var retVal = await SendMessageAsync(new ButtonAck()).ConfigureAwait(false);

                return retVal is Failure failure ? throw new FailureException<Failure>("USer didn't push the button.", failure) : retVal;
            }

            protected override void CheckForFailure(object returnMessage)
            {
                if (returnMessage is Failure failure)
                {
                    throw new FailureException<Failure>($"Error sending message to Trezor.\r\nCode: {failure.Code} Message: {failure.Message}", failure);
                }
            }

            /// <summary>
            /// TODO: Nasty. This at least needs some caching or something...
            /// </summary>
#pragma warning disable CA1502
            protected override Type GetContractType(MessageType messageType, string typeName)
            {
                switch (messageType)
                {
                    case MessageType.MessageTypeAddress:
                        return typeof(Address);
                    case MessageType.MessageTypeGetAddress:
                        return typeof(GetAddress);
                    case MessageType.MessageTypeButtonAck:
                        return typeof(ButtonAck);
                    case MessageType.MessageTypeButtonRequest:
                        return typeof(ButtonRequest);
                    case MessageType.MessageTypePublicKey:
                        return typeof(PublicKey);
                    case MessageType.MessageTypeFeatures:
                        return typeof(Features);
                    case MessageType.MessageTypePinMatrixAck:
                        return typeof(PinMatrixAck);
                    case MessageType.MessageTypePinMatrixRequest:
                        return typeof(PinMatrixRequest);
                    case MessageType.MessageTypeApplyFlags:
                        return typeof(ApplyFlags);
                    case MessageType.MessageTypeApplySettings:
                        return typeof(ApplySettings);
                    case MessageType.MessageTypeBackupDevice:
                        return typeof(BackupDevice);
                    case MessageType.MessageTypeCancel:
                        return typeof(Cancel);
                  
             
                    case MessageType.MessageTypeSuccess:
                        return typeof(Success);
                
                    case MessageType.MessageTypeTxAck:
                        return typeof(TxAck);
                    case MessageType.MessageTypeTxRequest:
                        return typeof(TxRequest);
                    case MessageType.MessageTypeVerifyMessage:
                        return typeof(VerifyMessage);
                    case MessageType.MessageTypeWipeDevice:
                        return typeof(WipeDevice);
                    case MessageType.MessageTypeWordAck:
                        return typeof(WordAck);
                    case MessageType.MessageTypeWordRequest:
                        return typeof(WordRequest);
                    case MessageType.MessageTypeInitialize:
                        return typeof(Initialize);
                    case MessageType.MessageTypePing:
                        return typeof(Ping);
                    case MessageType.MessageTypeFailure:
                        return typeof(Failure);
                    case MessageType.MessageTypeChangePin:
                        return typeof(ChangePin);
                    case MessageType.MessageTypeGetEntropy:
                        return typeof(GetEntropy);
                    case MessageType.MessageTypeEntropy:
                        return typeof(Entropy);
                    case MessageType.MessageTypeLoadDevice:
                        return typeof(LoadDevice);
                    case MessageType.MessageTypeResetDevice:
                        return typeof(ResetDevice);
                   
                    case MessageType.MessageTypeEntropyRequest:
                        return typeof(EntropyRequest);
                    case MessageType.MessageTypeEntropyAck:
                        return typeof(EntropyAck);
                    case MessageType.MessageTypePassphraseRequest:
                        return typeof(PassphraseRequest);
                    case MessageType.MessageTypePassphraseAck:
                        return typeof(PassphraseAck);
                   
                    case MessageType.MessageTypeRecoveryDevice:
                        return typeof(RecoveryDevice);
                    case MessageType.MessageTypeGetFeatures:
                        return typeof(GetFeatures);
                    case MessageType.MessageTypeSetU2FCounter:
                        return typeof(SetU2FCounter);
                    case MessageType.MessageTypeFirmwareErase:
                        return typeof(FirmwareErase);
                    case MessageType.MessageTypeFirmwareUpload:
                        return typeof(FirmwareUpload);
                    case MessageType.MessageTypeFirmwareRequest:
                        return typeof(FirmwareRequest);
                
                    case MessageType.MessageTypeGetPublicKey:
                        return typeof(GetPublicKey);
                    case MessageType.MessageTypeSignTx:
                        return typeof(SignTx);
                    case MessageType.MessageTypeSignMessage:
                        return typeof(SignMessage);
                    case MessageType.MessageTypeMessageSignature:
                        return typeof(MessageSignature);
                    case MessageType.MessageTypeCipherKeyValue:
                        return typeof(CipherKeyValue);
                    case MessageType.MessageTypeCipheredKeyValue:
                        return typeof(CipheredKeyValue);
                    case MessageType.MessageTypeSignIdentity:
                        return typeof(SignIdentity);
                    case MessageType.MessageTypeSignedIdentity:
                        return typeof(SignedIdentity);
                    case MessageType.MessageTypeGetECDHSessionKey:
                        return typeof(GetECDHSessionKey);
                    case MessageType.MessageTypeECDHSessionKey:
                        return typeof(ECDHSessionKey);
               
                    case MessageType.MessageTypeDebugLinkDecision:
                        return typeof(DebugLinkDecision);
                    case MessageType.MessageTypeDebugLinkGetState:
                        return typeof(DebugLinkGetState);
                    case MessageType.MessageTypeDebugLinkState:
                        return typeof(DebugLinkState);
                    case MessageType.MessageTypeDebugLinkStop:
                        return typeof(DebugLinkStop);
                    case MessageType.MessageTypeDebugLinkLog:
                        return typeof(DebugLinkLog);
                    case MessageType.MessageTypeDebugLinkMemoryRead:
                        return typeof(DebugLinkMemoryRead);
                    case MessageType.MessageTypeDebugLinkMemory:
                        return typeof(DebugLinkMemory);
                    case MessageType.MessageTypeDebugLinkMemoryWrite:
                        return typeof(DebugLinkMemoryWrite);
                    case MessageType.MessageTypeDebugLinkFlashErase:
                        return typeof(DebugLinkFlashErase);
                    case MessageType.MessageTypeEthereumGetAddress:
                        return typeof(EthereumGetAddress);
                     case MessageType.MessageTypeEthereumSignTx:
                        return typeof(EthereumSignTx);
                    case MessageType.MessageTypeEthereumTxRequest:
                        return typeof(EthereumTxRequest);
                    case MessageType.MessageTypeEthereumTxAck:
                        return typeof(EthereumTxAck);
                    case MessageType.MessageTypeEthereumSignMessage:
                        return typeof(EthereumSignMessage);
                    case MessageType.MessageTypeEthereumVerifyMessage:
                        return typeof(EthereumVerifyMessage);
                    case MessageType.MessageTypeEthereumMessageSignature:
                        return typeof(EthereumMessageSignature);
                    case MessageType.MessageTypeEthereumTypedDataStructRequest:
                        return typeof(EthereumTypedDataStructRequest);
                    case MessageType.MessageTypeEthereumTypedDataStructAck:
                        return typeof(EthereumTypedDataStructAck);
                    case MessageType.MessageTypeEthereumTypedDataValueRequest:
                        return typeof(EthereumTypedDataValueRequest);
                    case MessageType.MessageTypeEthereumTypedDataValueAck:
                        return typeof(EthereumTypedDataValueAck);
                    case MessageType.MessageTypeEthereumTypedDataSignature:
                        return typeof(EthereumTypedDataSignature);
                    case MessageType.MessageTypeEthereumSignTypedData:
                        return typeof(EthereumSignTypedData);
                    case MessageType.MessageTypeEthereumSignTypedHash:
                        return typeof(EthereumSignTypedHash);
                    case MessageType.MessageTypeEthereumSignTxEIP1559:
                        return typeof(EthereumSignTxEIP1559);
                    case MessageType.MessageTypeEthereumAddress:
                    return IsOldFirmware.HasValue && IsOldFirmware.Value ? typeof(BackwardsCompatible.EthereumAddress) : typeof(EthereumAddress);

                default:
                    throw new NotImplementedException();
                }
            }
#pragma warning restore CA1502

            protected override object GetEnumValue(string messageTypeString)
            {
                var isValid = Enum.TryParse(messageTypeString, out MessageType messageType);
                return !isValid ? throw new ManagerException($"{messageTypeString} is not a valid MessageType") : messageType;
            }

            protected override bool IsButtonRequest(object response) => response is ButtonRequest;

            protected override bool IsInitialize(object response) => response is Initialize;

            protected override bool IsPassphraseRequest(object response) => response is PassphraseRequest;

            protected override bool IsPinMatrixRequest(object response) => response is PinMatrixRequest;

            protected override async Task<object> PassphraseAckAsync(string passPhrase)
            {
                var retVal = await SendMessageAsync(new PassphraseAck { Passphrase = passPhrase }).ConfigureAwait(false);

                return retVal is Failure failure ? throw new FailureException<Failure>("Passphrase Attempt Failed.", failure) : retVal;
            }

            protected override async Task<object> PinMatrixAckAsync(string pin)
            {
                var retVal = await SendMessageAsync(new PinMatrixAck { Pin = pin }).ConfigureAwait(false);

                return retVal is Failure failure ? throw new FailureException<Failure>("PIN Attempt Failed.", failure) : retVal;
            }

            #endregion Protected Methods

            #region Private Methods

            private void CheckForSupported(string feature)
            {
                if (string.Compare(Features.Model, "T", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw new System.NotSupportedException($"{feature} is only supported on the Model T");
                }
            }
#pragma warning disable CA2213, CA1502
#pragma warning disable CA1304 // Specify CultureInfo
            private async Task<string> GetEthereumAddress(bool display, uint[] path)
            {
                var ethereumAddresssds = await SendMessageAsync<object, EthereumGetAddress>(new EthereumGetAddress { ShowDisplay = display, AddressNs = path }).ConfigureAwait(false);

                switch (ethereumAddresssds)
                {
                    case EthereumAddress ethereumAddress:
                        return ethereumAddress.Address.ToLower();
                    case BackwardsCompatible.EthereumAddress ethereumAddress:
                        return ethereumAddress.Address.ToHex();
                }

                throw new NotImplementedException();
            }
#pragma warning restore CA2213
#pragma warning restore CA1304 // Specify CultureInfo

            #endregion Private Methods
        }
    }

