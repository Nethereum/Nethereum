using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using System.Collections.Generic;

namespace Nethereum.Signer
{
    public class Authorisation7702Signer
    {
        public Authorisation7702Signer() { }
        public Authorisation7702Signed SignAuthorisation(string privateKey, Authorisation7702 authorisation)
        {
            return SignAuthorisation(privateKey.HexToByteArray(), authorisation);
        }
        public Authorisation7702Signed SignAuthorisation(byte[] privateKey, Authorisation7702 authorisation)
        {
            return SignAuthorisation(new EthECKey(privateKey, true), authorisation);
        }
        public Authorisation7702Signed SignAuthorisation(EthECKey ecKey, Authorisation7702 authorisation)
        {
            var signature = ecKey.SignAndCalculateYParityV(authorisation.EncodeAndHash());
            return new Authorisation7702Signed(authorisation, signature.R, signature.S, signature.V);
        }

        public List<Authorisation7702Signed> SignAuthorisations(string privateKey, List<Authorisation7702> authorisations)
        {
            return SignAuthorisations(privateKey.HexToByteArray(), authorisations);
        }

        public List<Authorisation7702Signed> SignAuthorisations(byte[] privateKey, List<Authorisation7702> authorisations)
        {
            return SignAuthorisations(new EthECKey(privateKey, true), authorisations);
        }

        public List<Authorisation7702Signed> SignAuthorisations(EthECKey ecKey, List<Authorisation7702> authorisations)
        {
            var signatures = new List<Authorisation7702Signed>();
            foreach (var authorisation in authorisations)
            {
                var signature = ecKey.SignAndCalculateYParityV(authorisation.EncodeAndHash());
                signatures.Add(new Authorisation7702Signed(authorisation, signature.R, signature.S, signature.V));
            }
            return signatures;
        }

    }
}