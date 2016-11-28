using Nethereum.KeyStore.Model;

namespace Nethereum.KeyStore
{
    public interface IKeyStoreService<T> where T : KdfParams
    {
        byte[] DecryptKeyStore(string password, KeyStore<T> keyStore);
        KeyStore<T> DeserializeKeyStoreFromJson(string json);
        KeyStore<T> EncryptAndGenerateKeyStore(string password, byte[] privateKey, string address);
        string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string addresss);
        string GetCipherType();
        string GetKdfType();
    }
}