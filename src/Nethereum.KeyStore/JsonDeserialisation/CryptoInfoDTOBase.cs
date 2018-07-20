namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class CryptoInfoDTOBase
    {
        public CryptoInfoDTOBase()
        {
            cipherparams = new CipherParamsDTO();
        }

        public string cipher { get; set; }
        public string cipherText { get; set; }
        public CipherParamsDTO cipherparams { get; set; }
        public string kdf { get; set; }
        public string mac { get; set; }
    }
}