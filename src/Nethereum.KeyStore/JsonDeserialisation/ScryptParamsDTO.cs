namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class ScryptParamsDTO : KdfParamsDTO
    {
        public int n { get; set; }
        public int r { get; set; }
        public int p { get; set; }
    }
}