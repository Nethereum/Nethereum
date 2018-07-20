namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class KeyStoreDTOBase
    {
        public string id { get; set; }
        public string address { get; set; }
        public int version { get; set; }
    }
}