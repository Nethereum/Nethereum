using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Signer.Trezor.Internal
{

    [global::ProtoBuf.ProtoContract()]
    internal partial class EthereumSignTxEIP1559 : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"address_n")]
        public uint[] AddressNs { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"nonce", IsRequired = true)]
        public byte[] Nonce { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"max_gas_fee", IsRequired = true)]
        public byte[] MaxGasFee { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"max_priority_fee", IsRequired = true)]
        public byte[] MaxPriorityFee { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"gas_limit", IsRequired = true)]
        public byte[] GasLimit { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"to")]
        [global::System.ComponentModel.DefaultValue("")]
        public string To
        {
            get => __pbn__To ?? "";
            set => __pbn__To = value;
        }
        public bool ShouldSerializeTo() => __pbn__To != null;
        public void ResetTo() => __pbn__To = null;
        private string __pbn__To;

        [global::ProtoBuf.ProtoMember(7, Name = @"value", IsRequired = true)]
        public byte[] Value { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"data_initial_chunk")]
        public byte[] DataInitialChunk
        {
            get => __pbn__DataInitialChunk;
            set => __pbn__DataInitialChunk = value;
        }
        public bool ShouldSerializeDataInitialChunk() => __pbn__DataInitialChunk != null;
        public void ResetDataInitialChunk() => __pbn__DataInitialChunk = null;
        private byte[] __pbn__DataInitialChunk;

        [global::ProtoBuf.ProtoMember(9, Name = @"data_length", IsRequired = true)]
        public uint DataLength { get; set; }

        [global::ProtoBuf.ProtoMember(10, Name = @"chain_id", IsRequired = true)]
        public ulong ChainId { get; set; }

        [global::ProtoBuf.ProtoMember(11, Name = @"access_list")]
        public global::System.Collections.Generic.List<EthereumAccessList> AccessLists { get; } = new global::System.Collections.Generic.List<EthereumAccessList>();

        [global::ProtoBuf.ProtoContract()]
        public partial class EthereumAccessList : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"address", IsRequired = true)]
            public string Address { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"storage_keys")]
            public global::System.Collections.Generic.List<byte[]> StorageKeys { get; } = new global::System.Collections.Generic.List<byte[]>();

        }

    }

    public class ExtendedMessageType
    {
        [global::ProtoBuf.ProtoContract()]
        public enum MessageType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Initialize")]
            MessageTypeInitialize = 0,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Ping")]
            MessageTypePing = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Success")]
            MessageTypeSuccess = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Failure")]
            MessageTypeFailure = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ChangePin")]
            MessageTypeChangePin = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WipeDevice")]
            MessageTypeWipeDevice = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetEntropy")]
            MessageTypeGetEntropy = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Entropy")]
            MessageTypeEntropy = 10,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_LoadDevice")]
            MessageTypeLoadDevice = 13,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ResetDevice")]
            MessageTypeResetDevice = 14,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SetBusy")]
            MessageTypeSetBusy = 16,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Features")]
            MessageTypeFeatures = 17,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_PinMatrixRequest")]
            MessageTypePinMatrixRequest = 18,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_PinMatrixAck")]
            MessageTypePinMatrixAck = 19,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Cancel")]
            MessageTypeCancel = 20,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_LockDevice")]
            MessageTypeLockDevice = 24,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ApplySettings")]
            MessageTypeApplySettings = 25,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ButtonRequest")]
            MessageTypeButtonRequest = 26,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ButtonAck")]
            MessageTypeButtonAck = 27,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ApplyFlags")]
            MessageTypeApplyFlags = 28,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetNonce")]
            MessageTypeGetNonce = 31,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Nonce")]
            MessageTypeNonce = 33,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BackupDevice")]
            MessageTypeBackupDevice = 34,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EntropyRequest")]
            MessageTypeEntropyRequest = 35,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EntropyAck")]
            MessageTypeEntropyAck = 36,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_PassphraseRequest")]
            MessageTypePassphraseRequest = 41,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_PassphraseAck")]
            MessageTypePassphraseAck = 42,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_RecoveryDevice")]
            MessageTypeRecoveryDevice = 45,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WordRequest")]
            MessageTypeWordRequest = 46,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WordAck")]
            MessageTypeWordAck = 47,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetFeatures")]
            MessageTypeGetFeatures = 55,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SdProtect")]
            MessageTypeSdProtect = 79,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ChangeWipeCode")]
            MessageTypeChangeWipeCode = 82,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EndSession")]
            MessageTypeEndSession = 83,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DoPreauthorized")]
            MessageTypeDoPreauthorized = 84,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_PreauthorizedRequest")]
            MessageTypePreauthorizedRequest = 85,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CancelAuthorization")]
            MessageTypeCancelAuthorization = 86,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_RebootToBootloader")]
            MessageTypeRebootToBootloader = 87,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetFirmwareHash")]
            MessageTypeGetFirmwareHash = 88,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_FirmwareHash")]
            MessageTypeFirmwareHash = 89,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_UnlockPath")]
            MessageTypeUnlockPath = 93,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_UnlockedPathRequest")]
            MessageTypeUnlockedPathRequest = 94,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SetU2FCounter")]
            MessageTypeSetU2FCounter = 63,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetNextU2FCounter")]
            MessageTypeGetNextU2FCounter = 80,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NextU2FCounter")]
            MessageTypeNextU2FCounter = 81,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Deprecated_PassphraseStateRequest")]
            [global::System.Obsolete]
            MessageTypeDeprecatedPassphraseStateRequest = 77,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Deprecated_PassphraseStateAck")]
            [global::System.Obsolete]
            MessageTypeDeprecatedPassphraseStateAck = 78,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_FirmwareErase")]
            MessageTypeFirmwareErase = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_FirmwareUpload")]
            MessageTypeFirmwareUpload = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_FirmwareRequest")]
            MessageTypeFirmwareRequest = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SelfTest")]
            MessageTypeSelfTest = 32,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetPublicKey")]
            MessageTypeGetPublicKey = 11,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_PublicKey")]
            MessageTypePublicKey = 12,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SignTx")]
            MessageTypeSignTx = 15,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TxRequest")]
            MessageTypeTxRequest = 21,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TxAck")]
            MessageTypeTxAck = 22,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetAddress")]
            MessageTypeGetAddress = 29,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_Address")]
            MessageTypeAddress = 30,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TxAckPaymentRequest")]
            MessageTypeTxAckPaymentRequest = 37,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SignMessage")]
            MessageTypeSignMessage = 38,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_VerifyMessage")]
            MessageTypeVerifyMessage = 39,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MessageSignature")]
            MessageTypeMessageSignature = 40,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetOwnershipId")]
            MessageTypeGetOwnershipId = 43,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_OwnershipId")]
            MessageTypeOwnershipId = 44,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetOwnershipProof")]
            MessageTypeGetOwnershipProof = 49,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_OwnershipProof")]
            MessageTypeOwnershipProof = 50,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_AuthorizeCoinJoin")]
            MessageTypeAuthorizeCoinJoin = 51,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CipherKeyValue")]
            MessageTypeCipherKeyValue = 23,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CipheredKeyValue")]
            MessageTypeCipheredKeyValue = 48,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SignIdentity")]
            MessageTypeSignIdentity = 53,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_SignedIdentity")]
            MessageTypeSignedIdentity = 54,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_GetECDHSessionKey")]
            MessageTypeGetECDHSessionKey = 61,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_ECDHSessionKey")]
            MessageTypeECDHSessionKey = 62,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CosiCommit")]
            MessageTypeCosiCommit = 71,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CosiCommitment")]
            MessageTypeCosiCommitment = 72,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CosiSign")]
            MessageTypeCosiSign = 73,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CosiSignature")]
            MessageTypeCosiSignature = 74,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkDecision")]
            MessageTypeDebugLinkDecision = 100,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkGetState")]
            MessageTypeDebugLinkGetState = 101,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkState")]
            MessageTypeDebugLinkState = 102,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkStop")]
            MessageTypeDebugLinkStop = 103,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkLog")]
            MessageTypeDebugLinkLog = 104,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkMemoryRead")]
            MessageTypeDebugLinkMemoryRead = 110,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkMemory")]
            MessageTypeDebugLinkMemory = 111,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkMemoryWrite")]
            MessageTypeDebugLinkMemoryWrite = 112,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkFlashErase")]
            MessageTypeDebugLinkFlashErase = 113,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkLayout")]
            MessageTypeDebugLinkLayout = 9001,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkReseedRandom")]
            MessageTypeDebugLinkReseedRandom = 9002,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkRecordScreen")]
            MessageTypeDebugLinkRecordScreen = 9003,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkEraseSdCard")]
            MessageTypeDebugLinkEraseSdCard = 9005,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugLinkWatchLayout")]
            MessageTypeDebugLinkWatchLayout = 9006,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumGetPublicKey")]
            MessageTypeEthereumGetPublicKey = 450,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumPublicKey")]
            MessageTypeEthereumPublicKey = 451,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumGetAddress")]
            MessageTypeEthereumGetAddress = 56,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumAddress")]
            MessageTypeEthereumAddress = 57,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumSignTx")]
            MessageTypeEthereumSignTx = 58,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumSignTxEIP1559")]
            MessageTypeEthereumSignTxEIP1559 = 452,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTxRequest")]
            MessageTypeEthereumTxRequest = 59,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTxAck")]
            MessageTypeEthereumTxAck = 60,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumSignMessage")]
            MessageTypeEthereumSignMessage = 64,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumVerifyMessage")]
            MessageTypeEthereumVerifyMessage = 65,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumMessageSignature")]
            MessageTypeEthereumMessageSignature = 66,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumSignTypedData")]
            MessageTypeEthereumSignTypedData = 464,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTypedDataStructRequest")]
            MessageTypeEthereumTypedDataStructRequest = 465,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTypedDataStructAck")]
            MessageTypeEthereumTypedDataStructAck = 466,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTypedDataValueRequest")]
            MessageTypeEthereumTypedDataValueRequest = 467,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTypedDataValueAck")]
            MessageTypeEthereumTypedDataValueAck = 468,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumTypedDataSignature")]
            MessageTypeEthereumTypedDataSignature = 469,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EthereumSignTypedHash")]
            MessageTypeEthereumSignTypedHash = 470,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NEMGetAddress")]
            MessageTypeNEMGetAddress = 67,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NEMAddress")]
            MessageTypeNEMAddress = 68,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NEMSignTx")]
            MessageTypeNEMSignTx = 69,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NEMSignedTx")]
            MessageTypeNEMSignedTx = 70,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NEMDecryptMessage")]
            MessageTypeNEMDecryptMessage = 75,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_NEMDecryptedMessage")]
            MessageTypeNEMDecryptedMessage = 76,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TezosGetAddress")]
            MessageTypeTezosGetAddress = 150,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TezosAddress")]
            MessageTypeTezosAddress = 151,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TezosSignTx")]
            MessageTypeTezosSignTx = 152,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TezosSignedTx")]
            MessageTypeTezosSignedTx = 153,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TezosGetPublicKey")]
            MessageTypeTezosGetPublicKey = 154,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_TezosPublicKey")]
            MessageTypeTezosPublicKey = 155,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarSignTx")]
            MessageTypeStellarSignTx = 202,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarTxOpRequest")]
            MessageTypeStellarTxOpRequest = 203,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarGetAddress")]
            MessageTypeStellarGetAddress = 207,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarAddress")]
            MessageTypeStellarAddress = 208,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarCreateAccountOp")]
            MessageTypeStellarCreateAccountOp = 210,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarPaymentOp")]
            MessageTypeStellarPaymentOp = 211,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarPathPaymentStrictReceiveOp")]
            MessageTypeStellarPathPaymentStrictReceiveOp = 212,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarManageSellOfferOp")]
            MessageTypeStellarManageSellOfferOp = 213,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarCreatePassiveSellOfferOp")]
            MessageTypeStellarCreatePassiveSellOfferOp = 214,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarSetOptionsOp")]
            MessageTypeStellarSetOptionsOp = 215,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarChangeTrustOp")]
            MessageTypeStellarChangeTrustOp = 216,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarAllowTrustOp")]
            MessageTypeStellarAllowTrustOp = 217,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarAccountMergeOp")]
            MessageTypeStellarAccountMergeOp = 218,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarManageDataOp")]
            MessageTypeStellarManageDataOp = 220,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarBumpSequenceOp")]
            MessageTypeStellarBumpSequenceOp = 221,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarManageBuyOfferOp")]
            MessageTypeStellarManageBuyOfferOp = 222,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarPathPaymentStrictSendOp")]
            MessageTypeStellarPathPaymentStrictSendOp = 223,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_StellarSignedTx")]
            MessageTypeStellarSignedTx = 230,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoGetPublicKey")]
            MessageTypeCardanoGetPublicKey = 305,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoPublicKey")]
            MessageTypeCardanoPublicKey = 306,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoGetAddress")]
            MessageTypeCardanoGetAddress = 307,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoAddress")]
            MessageTypeCardanoAddress = 308,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxItemAck")]
            MessageTypeCardanoTxItemAck = 313,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxAuxiliaryDataSupplement")]
            MessageTypeCardanoTxAuxiliaryDataSupplement = 314,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxWitnessRequest")]
            MessageTypeCardanoTxWitnessRequest = 315,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxWitnessResponse")]
            MessageTypeCardanoTxWitnessResponse = 316,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxHostAck")]
            MessageTypeCardanoTxHostAck = 317,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxBodyHash")]
            MessageTypeCardanoTxBodyHash = 318,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoSignTxFinished")]
            MessageTypeCardanoSignTxFinished = 319,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoSignTxInit")]
            MessageTypeCardanoSignTxInit = 320,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxInput")]
            MessageTypeCardanoTxInput = 321,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxOutput")]
            MessageTypeCardanoTxOutput = 322,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoAssetGroup")]
            MessageTypeCardanoAssetGroup = 323,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoToken")]
            MessageTypeCardanoToken = 324,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxCertificate")]
            MessageTypeCardanoTxCertificate = 325,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxWithdrawal")]
            MessageTypeCardanoTxWithdrawal = 326,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxAuxiliaryData")]
            MessageTypeCardanoTxAuxiliaryData = 327,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoPoolOwner")]
            MessageTypeCardanoPoolOwner = 328,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoPoolRelayParameters")]
            MessageTypeCardanoPoolRelayParameters = 329,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoGetNativeScriptHash")]
            MessageTypeCardanoGetNativeScriptHash = 330,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoNativeScriptHash")]
            MessageTypeCardanoNativeScriptHash = 331,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxMint")]
            MessageTypeCardanoTxMint = 332,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxCollateralInput")]
            MessageTypeCardanoTxCollateralInput = 333,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxRequiredSigner")]
            MessageTypeCardanoTxRequiredSigner = 334,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxInlineDatumChunk")]
            MessageTypeCardanoTxInlineDatumChunk = 335,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxReferenceScriptChunk")]
            MessageTypeCardanoTxReferenceScriptChunk = 336,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_CardanoTxReferenceInput")]
            MessageTypeCardanoTxReferenceInput = 337,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_RippleGetAddress")]
            MessageTypeRippleGetAddress = 400,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_RippleAddress")]
            MessageTypeRippleAddress = 401,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_RippleSignTx")]
            MessageTypeRippleSignTx = 402,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_RippleSignedTx")]
            MessageTypeRippleSignedTx = 403,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionInitRequest")]
            MessageTypeMoneroTransactionInitRequest = 501,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionInitAck")]
            MessageTypeMoneroTransactionInitAck = 502,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionSetInputRequest")]
            MessageTypeMoneroTransactionSetInputRequest = 503,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionSetInputAck")]
            MessageTypeMoneroTransactionSetInputAck = 504,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionInputViniRequest")]
            MessageTypeMoneroTransactionInputViniRequest = 507,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionInputViniAck")]
            MessageTypeMoneroTransactionInputViniAck = 508,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionAllInputsSetRequest")]
            MessageTypeMoneroTransactionAllInputsSetRequest = 509,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionAllInputsSetAck")]
            MessageTypeMoneroTransactionAllInputsSetAck = 510,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionSetOutputRequest")]
            MessageTypeMoneroTransactionSetOutputRequest = 511,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionSetOutputAck")]
            MessageTypeMoneroTransactionSetOutputAck = 512,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionAllOutSetRequest")]
            MessageTypeMoneroTransactionAllOutSetRequest = 513,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionAllOutSetAck")]
            MessageTypeMoneroTransactionAllOutSetAck = 514,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionSignInputRequest")]
            MessageTypeMoneroTransactionSignInputRequest = 515,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionSignInputAck")]
            MessageTypeMoneroTransactionSignInputAck = 516,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionFinalRequest")]
            MessageTypeMoneroTransactionFinalRequest = 517,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroTransactionFinalAck")]
            MessageTypeMoneroTransactionFinalAck = 518,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroKeyImageExportInitRequest")]
            MessageTypeMoneroKeyImageExportInitRequest = 530,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroKeyImageExportInitAck")]
            MessageTypeMoneroKeyImageExportInitAck = 531,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroKeyImageSyncStepRequest")]
            MessageTypeMoneroKeyImageSyncStepRequest = 532,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroKeyImageSyncStepAck")]
            MessageTypeMoneroKeyImageSyncStepAck = 533,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroKeyImageSyncFinalRequest")]
            MessageTypeMoneroKeyImageSyncFinalRequest = 534,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroKeyImageSyncFinalAck")]
            MessageTypeMoneroKeyImageSyncFinalAck = 535,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroGetAddress")]
            MessageTypeMoneroGetAddress = 540,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroAddress")]
            MessageTypeMoneroAddress = 541,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroGetWatchKey")]
            MessageTypeMoneroGetWatchKey = 542,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroWatchKey")]
            MessageTypeMoneroWatchKey = 543,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugMoneroDiagRequest")]
            MessageTypeDebugMoneroDiagRequest = 546,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_DebugMoneroDiagAck")]
            MessageTypeDebugMoneroDiagAck = 547,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroGetTxKeyRequest")]
            MessageTypeMoneroGetTxKeyRequest = 550,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroGetTxKeyAck")]
            MessageTypeMoneroGetTxKeyAck = 551,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroLiveRefreshStartRequest")]
            MessageTypeMoneroLiveRefreshStartRequest = 552,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroLiveRefreshStartAck")]
            MessageTypeMoneroLiveRefreshStartAck = 553,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroLiveRefreshStepRequest")]
            MessageTypeMoneroLiveRefreshStepRequest = 554,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroLiveRefreshStepAck")]
            MessageTypeMoneroLiveRefreshStepAck = 555,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroLiveRefreshFinalRequest")]
            MessageTypeMoneroLiveRefreshFinalRequest = 556,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_MoneroLiveRefreshFinalAck")]
            MessageTypeMoneroLiveRefreshFinalAck = 557,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EosGetPublicKey")]
            MessageTypeEosGetPublicKey = 600,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EosPublicKey")]
            MessageTypeEosPublicKey = 601,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EosSignTx")]
            MessageTypeEosSignTx = 602,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EosTxActionRequest")]
            MessageTypeEosTxActionRequest = 603,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EosTxActionAck")]
            MessageTypeEosTxActionAck = 604,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_EosSignedTx")]
            MessageTypeEosSignedTx = 605,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceGetAddress")]
            MessageTypeBinanceGetAddress = 700,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceAddress")]
            MessageTypeBinanceAddress = 701,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceGetPublicKey")]
            MessageTypeBinanceGetPublicKey = 702,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinancePublicKey")]
            MessageTypeBinancePublicKey = 703,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceSignTx")]
            MessageTypeBinanceSignTx = 704,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceTxRequest")]
            MessageTypeBinanceTxRequest = 705,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceTransferMsg")]
            MessageTypeBinanceTransferMsg = 706,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceOrderMsg")]
            MessageTypeBinanceOrderMsg = 707,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceCancelMsg")]
            MessageTypeBinanceCancelMsg = 708,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_BinanceSignedTx")]
            MessageTypeBinanceSignedTx = 709,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WebAuthnListResidentCredentials")]
            MessageTypeWebAuthnListResidentCredentials = 800,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WebAuthnCredentials")]
            MessageTypeWebAuthnCredentials = 801,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WebAuthnAddResidentCredential")]
            MessageTypeWebAuthnAddResidentCredential = 802,
            [global::ProtoBuf.ProtoEnum(Name = @"MessageType_WebAuthnRemoveResidentCredential")]
            MessageTypeWebAuthnRemoveResidentCredential = 803,
        }
    }
}
