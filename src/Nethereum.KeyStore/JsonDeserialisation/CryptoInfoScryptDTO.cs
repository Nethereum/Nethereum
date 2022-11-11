namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class CryptoInfoScryptDTO : CryptoInfoDTOBase
    { 
        public CryptoInfoScryptDTO()
        {
            kdfparams = new ScryptParamsDTO();
        }
        public ScryptParamsDTO kdfparams { get; set; }
    }
}