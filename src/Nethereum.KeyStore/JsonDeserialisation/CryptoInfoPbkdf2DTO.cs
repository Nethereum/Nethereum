using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Model;
using Newtonsoft.Json;

namespace Nethereum.KeyStore.JsonDeserialisation
{
    public class CryptoInfoPbkdf2DTO : CryptoInfoDTOBase
    {
        public CryptoInfoPbkdf2DTO()
        {
            kdfparams = new Pbkdf2ParamsDTO();
        }

        public Pbkdf2ParamsDTO kdfparams { get; set; }
    }
}