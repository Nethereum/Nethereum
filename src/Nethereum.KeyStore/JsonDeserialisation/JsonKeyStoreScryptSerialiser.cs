using Nethereum.KeyStore.Model;
using Newtonsoft.Json;

namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class JsonKeyStoreScryptSerialiser
    {
        public static string SerialiseScrypt(KeyStore<ScryptParams> scryptKeyStore)
        {
            var dto = MapModelToDTO(scryptKeyStore);
            return JsonConvert.SerializeObject(scryptKeyStore);
        }

        public static KeyStore<ScryptParams> DeserialiseScrypt(string json)
        {
            var dto = JsonConvert.DeserializeObject<KeyStoreScryptDTO>(json);
            return MapDTOToModel(dto);
        }

        public static KeyStoreScryptDTO MapModelToDTO(KeyStore<ScryptParams> scryptKeyStore)
        {
            var dto = new KeyStoreScryptDTO();
            dto.address = scryptKeyStore.Address;
            dto.id = scryptKeyStore.Id;
            dto.version = scryptKeyStore.Version;
            dto.crypto.cipher = scryptKeyStore.Crypto.Cipher;
            dto.crypto.cipherText = scryptKeyStore.Crypto.CipherText;
            dto.crypto.kdf = scryptKeyStore.Crypto.Kdf;
            dto.crypto.mac = scryptKeyStore.Crypto.Mac;
            dto.crypto.kdfparams.r = scryptKeyStore.Crypto.Kdfparams.R;
            dto.crypto.kdfparams.n = scryptKeyStore.Crypto.Kdfparams.N;
            dto.crypto.kdfparams.p = scryptKeyStore.Crypto.Kdfparams.P;
            dto.crypto.kdfparams.dklen = scryptKeyStore.Crypto.Kdfparams.Dklen;
            dto.crypto.kdfparams.salt = scryptKeyStore.Crypto.Kdfparams.Salt;
            dto.crypto.cipherparams.iv = scryptKeyStore.Crypto.CipherParams.Iv;
            return dto;
        }

        public static KeyStore<ScryptParams> MapDTOToModel(KeyStoreScryptDTO dto)
        {
            var scryptKeyStore = new KeyStore<ScryptParams>();
            scryptKeyStore.Address = dto.address;
            scryptKeyStore.Id = dto.id;
            scryptKeyStore.Version = dto.version;
            scryptKeyStore.Crypto = new CryptoInfo<ScryptParams>();
            scryptKeyStore.Crypto.Cipher = dto.crypto.cipher;
            scryptKeyStore.Crypto.CipherText = dto.crypto.cipherText;
            scryptKeyStore.Crypto.Kdf = dto.crypto.kdf;
            scryptKeyStore.Crypto.Mac = dto.crypto.mac;
            scryptKeyStore.Crypto.Kdfparams = new ScryptParams();
            scryptKeyStore.Crypto.Kdfparams.R = dto.crypto.kdfparams.r;
            scryptKeyStore.Crypto.Kdfparams.N = dto.crypto.kdfparams.n;
            scryptKeyStore.Crypto.Kdfparams.P = dto.crypto.kdfparams.p;
            scryptKeyStore.Crypto.Kdfparams.Dklen = dto.crypto.kdfparams.dklen;
            scryptKeyStore.Crypto.Kdfparams.Salt = dto.crypto.kdfparams.salt;
            scryptKeyStore.Crypto.CipherParams = new CipherParams();
            scryptKeyStore.Crypto.CipherParams.Iv = dto.crypto.cipherparams.iv;
            return scryptKeyStore;
        }
    }
}