namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class KeyStoreScryptDTO:KeyStoreDTOBase
    {
        public KeyStoreScryptDTO()
        {
            crypto = new CryptoInfoScryptDTO();
        }

        public CryptoInfoScryptDTO crypto { get; set; }
    }
}