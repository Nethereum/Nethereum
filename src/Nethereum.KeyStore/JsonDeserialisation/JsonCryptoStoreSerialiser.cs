using Nethereum.KeyStore.Model;
using Newtonsoft.Json;

namespace Nethereum.KeyStore.JsonDeserialisation
{
    public static class JsonCryptoStoreSerialiser
    {
        public static string Serialise<TKdfParams>(CryptoStore<TKdfParams> cryptoStore) where TKdfParams : KdfParams
        {
            return JsonConvert.SerializeObject(cryptoStore);
        }

        public static CryptoStore<TKdfParams> Deserialise<TKdfParams>(string json) where TKdfParams : KdfParams
        {
            return JsonConvert.DeserializeObject<CryptoStore<TKdfParams>>(json);
        }
    }
}
