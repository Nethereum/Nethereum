using Nethereum.ABI.EIP712;
using Nethereum.Util;
using System.Threading.Tasks;

namespace Nethereum.Signer.EIP712
{

    public class Eip712TypedDataSigner
    {
        
        private readonly MessageSigner _signer = new MessageSigner();
        public static Eip712TypedDataSigner Current { get; } = new Eip712TypedDataSigner();

        public string SignTypedData<T, TDomain>(T data, TDomain domain, string primaryTypeName, EthECKey key) 
        {
            var typedData = Eip712TypedDataEncoder.Current.GenerateTypedData(data, domain, primaryTypeName);

            return SignTypedData(typedData, key);
        }


        public string SignTypedData<TDomain>(TypedData<TDomain> typedData, EthECKey key)
        {
            var encodedData = EncodeTypedData(typedData);
            return _signer.HashAndSign(encodedData, key);
        }

        public async Task<string> SignTypedDataV4<TDomain>(TypedData<TDomain> typedData, IEthExternalSigner ethExternalSigner)
        {
            var encodedData = EncodeTypedData(typedData);
            var signature = await ethExternalSigner.SignAsync(Sha3Keccack.Current.CalculateHash(encodedData));
            return EthECDSASignature.CreateStringSignature(signature);
        }


        public string SignTypedDataV4<TDomain>(TypedData<TDomain> typedData, EthECKey key) 
        {
            var encodedData = EncodeTypedData(typedData);
            var signature = key.SignAndCalculateV(Sha3Keccack.Current.CalculateHash(encodedData));
            return EthECDSASignature.CreateStringSignature(signature);
        }

        public string SignTypedDataV4(string json, EthECKey key)
        {
            var encodedData = EncodeTypedData(json);
            var signature = key.SignAndCalculateV(Sha3Keccack.Current.CalculateHash(encodedData));
            return EthECDSASignature.CreateStringSignature(signature);
        }

        public string SignTypedDataV4<TDomain>(string json, EthECKey key, string messageKeySelector = "message")
        {
            var encodedData = EncodeTypedData<TDomain>(json, messageKeySelector);
            var signature = key.SignAndCalculateV(Sha3Keccack.Current.CalculateHash(encodedData));
            return EthECDSASignature.CreateStringSignature(signature);
        }


        public string SignTypedDataV4<T, TDomain>(T message, TypedData<TDomain> typedData, EthECKey key) 
        {
            var encodedData = EncodeTypedData(message, typedData);
            var signature = key.SignAndCalculateV(Sha3Keccack.Current.CalculateHash(encodedData));
            return EthECDSASignature.CreateStringSignature(signature);
        }

        public string RecoverFromSignatureV4<T,TDomain>(T message, TypedData<TDomain> typedData, string signature) 
        {
            var encodedData = EncodeTypedData(message, typedData);
            return new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedData), signature);
        }

        public string RecoverFromSignatureV4<TDomain>(TypedData<TDomain> typedData, string signature) 
        {
            var encodedData = EncodeTypedData(typedData);
            return  new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedData), signature);
        }

        public string RecoverFromSignatureV4(string json, string signature, string messageKeySelector = "message")
        {
            var encodedData = EncodeTypedData<Domain>(json, messageKeySelector);
            return new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedData), signature);
        }


        public string RecoverFromSignatureV4(byte[] encodedData, string signature)
        {
            return new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedData), signature);
        }

        public string RecoverFromSignatureHashV4(byte[] hash, string signature)
        {
            return new MessageSigner().EcRecover(hash, signature);
        }

        public byte[] EncodeTypedData<TDomain>(TypedData<TDomain> typedData)
        {
            return Eip712TypedDataEncoder.Current.EncodeTypedData(typedData);
        }

        public byte[] EncodeTypedDataRaw(TypedDataRaw typedData)
        {
            return Eip712TypedDataEncoder.Current.EncodeTypedDataRaw(typedData);
        }

        public byte[] EncodeTypedData(string json)
        {
            return Eip712TypedDataEncoder.Current.EncodeTypedData(json);
        }


        public byte[] EncodeTypedData<TDomain>(string json, string messageKeySelector = "message")
        {
            return Eip712TypedDataEncoder.Current.EncodeTypedData<TDomain>(json, messageKeySelector);
        }

        public byte[] EncodeTypedData<T, TDomain>(T message, TypedData<TDomain> typedData)
        {
            return Eip712TypedDataEncoder.Current.EncodeTypedData(message, typedData);
        }


        }
}