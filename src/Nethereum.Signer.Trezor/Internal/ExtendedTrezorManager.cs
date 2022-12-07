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
using Trezor.Net.Contracts.Bitcoin;
using Trezor.Net.Contracts.Bootloader;
using Trezor.Net.Contracts.Cardano;
using Trezor.Net.Contracts.Common;
using Trezor.Net.Contracts.Crypto;
using Trezor.Net.Contracts.Debug;
using Trezor.Net.Contracts.Ethereum;
using Trezor.Net.Contracts.Lisk;
using Trezor.Net.Contracts.Management;
using Trezor.Net.Contracts.Monero;
using Trezor.Net.Contracts.NEM;
using Trezor.Net.Contracts.Ontology;
using Trezor.Net.Contracts.Ripple;
using Trezor.Net.Contracts.Stellar;
using Trezor.Net.Contracts.Tezos;
using Trezor.Net;
using BackwardsCompatible = Trezor.Net.Contracts.BackwardsCompatible;
using static Nethereum.Signer.Trezor.TrezorExternalSigner;
using Trezor.Net.Manager;

namespace Nethereum.Signer.Trezor.Internal
{

    public class ExtendedTrezorManager : TrezorManagerBase<ExtendedMessageType.MessageType>
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


                        case AddressType.Cardano:
                            CheckForSupported(nameof(AddressType.Cardano));
                            return (await SendMessageAsync<CardanoAddress, CardanoGetAddress>(new CardanoGetAddress { ShowDisplay = display, AddressNs = path }).ConfigureAwait(false)).Address;

                        case AddressType.Stellar:
                            return (await SendMessageAsync<StellarAddress, StellarGetAddress>(new StellarGetAddress { ShowDisplay = display, AddressNs = path }).ConfigureAwait(false)).Address;

                        case AddressType.Tezoz:
                            CheckForSupported(nameof(AddressType.Tezoz));
                            return (await SendMessageAsync<TezosAddress, TezosGetAddress>(new TezosGetAddress { ShowDisplay = display, AddressNs = path }).ConfigureAwait(false)).Address;

                        case AddressType.NEM:
                            return (await SendMessageAsync<NEMAddress, NEMGetAddress>(new NEMGetAddress { ShowDisplay = display, AddressNs = path }).ConfigureAwait(false)).Address;

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

                Features = await SendMessageAsync<Features, Initialize>(new Initialize()).ConfigureAwait(false);

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
            protected override Type GetContractType(ExtendedMessageType.MessageType messageType, string typeName)
            {
                switch (messageType)
                {
                    case ExtendedMessageType.MessageType.MessageTypeAddress:
                        return typeof(Address);
                    case ExtendedMessageType.MessageType.MessageTypeGetAddress:
                        return typeof(GetAddress);
                    case ExtendedMessageType.MessageType.MessageTypeButtonAck:
                        return typeof(ButtonAck);
                    case ExtendedMessageType.MessageType.MessageTypeButtonRequest:
                        return typeof(ButtonRequest);
                    case ExtendedMessageType.MessageType.MessageTypePublicKey:
                        return typeof(PublicKey);
                    case ExtendedMessageType.MessageType.MessageTypeFeatures:
                        return typeof(Features);
                    case ExtendedMessageType.MessageType.MessageTypePinMatrixAck:
                        return typeof(PinMatrixAck);
                    case ExtendedMessageType.MessageType.MessageTypePinMatrixRequest:
                        return typeof(PinMatrixRequest);
                    case ExtendedMessageType.MessageType.MessageTypeApplyFlags:
                        return typeof(ApplyFlags);
                    case ExtendedMessageType.MessageType.MessageTypeApplySettings:
                        return typeof(ApplySettings);
                    case ExtendedMessageType.MessageType.MessageTypeBackupDevice:
                        return typeof(BackupDevice);
                    case ExtendedMessageType.MessageType.MessageTypeCancel:
                        return typeof(Cancel);
                  
             
                    case ExtendedMessageType.MessageType.MessageTypeSuccess:
                        return typeof(Success);
                
                    case ExtendedMessageType.MessageType.MessageTypeTxAck:
                        return typeof(TxAck);
                    case ExtendedMessageType.MessageType.MessageTypeTxRequest:
                        return typeof(TxRequest);
                    case ExtendedMessageType.MessageType.MessageTypeVerifyMessage:
                        return typeof(VerifyMessage);
                    case ExtendedMessageType.MessageType.MessageTypeWipeDevice:
                        return typeof(WipeDevice);
                    case ExtendedMessageType.MessageType.MessageTypeWordAck:
                        return typeof(WordAck);
                    case ExtendedMessageType.MessageType.MessageTypeWordRequest:
                        return typeof(WordRequest);
                    case ExtendedMessageType.MessageType.MessageTypeInitialize:
                        return typeof(Initialize);
                    case ExtendedMessageType.MessageType.MessageTypePing:
                        return typeof(Ping);
                    case ExtendedMessageType.MessageType.MessageTypeFailure:
                        return typeof(Failure);
                    case ExtendedMessageType.MessageType.MessageTypeChangePin:
                        return typeof(ChangePin);
                    case ExtendedMessageType.MessageType.MessageTypeGetEntropy:
                        return typeof(GetEntropy);
                    case ExtendedMessageType.MessageType.MessageTypeEntropy:
                        return typeof(Entropy);
                    case ExtendedMessageType.MessageType.MessageTypeLoadDevice:
                        return typeof(LoadDevice);
                    case ExtendedMessageType.MessageType.MessageTypeResetDevice:
                        return typeof(ResetDevice);
                   
                    case ExtendedMessageType.MessageType.MessageTypeEntropyRequest:
                        return typeof(EntropyRequest);
                    case ExtendedMessageType.MessageType.MessageTypeEntropyAck:
                        return typeof(EntropyAck);
                    case ExtendedMessageType.MessageType.MessageTypePassphraseRequest:
                        return typeof(PassphraseRequest);
                    case ExtendedMessageType.MessageType.MessageTypePassphraseAck:
                        return typeof(PassphraseAck);
                   
                    case ExtendedMessageType.MessageType.MessageTypeRecoveryDevice:
                        return typeof(RecoveryDevice);
                    case ExtendedMessageType.MessageType.MessageTypeGetFeatures:
                        return typeof(GetFeatures);
                    case ExtendedMessageType.MessageType.MessageTypeSetU2FCounter:
                        return typeof(SetU2FCounter);
                    case ExtendedMessageType.MessageType.MessageTypeFirmwareErase:
                        return typeof(FirmwareErase);
                    case ExtendedMessageType.MessageType.MessageTypeFirmwareUpload:
                        return typeof(FirmwareUpload);
                    case ExtendedMessageType.MessageType.MessageTypeFirmwareRequest:
                        return typeof(FirmwareRequest);
                    case ExtendedMessageType.MessageType.MessageTypeSelfTest:
                        return typeof(SelfTest);
                    case ExtendedMessageType.MessageType.MessageTypeGetPublicKey:
                        return typeof(GetPublicKey);
                    case ExtendedMessageType.MessageType.MessageTypeSignTx:
                        return typeof(SignTx);
                    case ExtendedMessageType.MessageType.MessageTypeSignMessage:
                        return typeof(SignMessage);
                    case ExtendedMessageType.MessageType.MessageTypeMessageSignature:
                        return typeof(MessageSignature);
                    case ExtendedMessageType.MessageType.MessageTypeCipherKeyValue:
                        return typeof(CipherKeyValue);
                    case ExtendedMessageType.MessageType.MessageTypeCipheredKeyValue:
                        return typeof(CipheredKeyValue);
                    case ExtendedMessageType.MessageType.MessageTypeSignIdentity:
                        return typeof(SignIdentity);
                    case ExtendedMessageType.MessageType.MessageTypeSignedIdentity:
                        return typeof(SignedIdentity);
                    case ExtendedMessageType.MessageType.MessageTypeGetECDHSessionKey:
                        return typeof(GetECDHSessionKey);
                    case ExtendedMessageType.MessageType.MessageTypeECDHSessionKey:
                        return typeof(ECDHSessionKey);
                    case ExtendedMessageType.MessageType.MessageTypeCosiCommit:
                        return typeof(CosiCommit);
                    case ExtendedMessageType.MessageType.MessageTypeCosiCommitment:
                        return typeof(CosiCommitment);
                    case ExtendedMessageType.MessageType.MessageTypeCosiSign:
                        return typeof(CosiSign);
                    case ExtendedMessageType.MessageType.MessageTypeCosiSignature:
                        return typeof(CosiSignature);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkDecision:
                        return typeof(DebugLinkDecision);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkGetState:
                        return typeof(DebugLinkGetState);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkState:
                        return typeof(DebugLinkState);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkStop:
                        return typeof(DebugLinkStop);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkLog:
                        return typeof(DebugLinkLog);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkMemoryRead:
                        return typeof(DebugLinkMemoryRead);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkMemory:
                        return typeof(DebugLinkMemory);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkMemoryWrite:
                        return typeof(DebugLinkMemoryWrite);
                    case ExtendedMessageType.MessageType.MessageTypeDebugLinkFlashErase:
                        return typeof(DebugLinkFlashErase);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumGetAddress:
                        return typeof(EthereumGetAddress);
                     case ExtendedMessageType.MessageType.MessageTypeEthereumSignTx:
                        return typeof(EthereumSignTx);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumTxRequest:
                        return typeof(EthereumTxRequest);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumTxAck:
                        return typeof(EthereumTxAck);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumSignMessage:
                        return typeof(EthereumSignMessage);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumVerifyMessage:
                        return typeof(EthereumVerifyMessage);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumMessageSignature:
                        return typeof(EthereumMessageSignature);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumSignTxEIP1559:
                        return typeof(EthereumSignTxEIP1559);
                    case ExtendedMessageType.MessageType.MessageTypeEthereumAddress:
                    return IsOldFirmware.HasValue && IsOldFirmware.Value ? typeof(BackwardsCompatible.EthereumAddress) : typeof(EthereumAddress);

                default:
                    throw new NotImplementedException();
                }
            }
#pragma warning restore CA1502

            protected override object GetEnumValue(string messageTypeString)
            {
                var isValid = Enum.TryParse(messageTypeString, out ExtendedMessageType.MessageType messageType);
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

