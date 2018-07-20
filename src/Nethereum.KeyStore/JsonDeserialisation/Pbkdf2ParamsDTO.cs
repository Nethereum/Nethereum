namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class Pbkdf2ParamsDTO : KdfParamsDTO
    {
        public int c { get; set; }
        public string prf { get; set; }
    }
}