namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class KeyStorePbkdf2DTO : KeyStoreDTOBase
    {
        public KeyStorePbkdf2DTO()
        {
            crypto = new CryptoInfoPbkdf2DTO();
        }
        public CryptoInfoPbkdf2DTO crypto { get; set; }
    }
}