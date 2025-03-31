using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.RPC.Eth.Mappers
{
    public static class AuthorisationListRPCMapper
    {
        public static List<Authorisation7702Signed> ToAuthorisation7720SignedList(this List<Authorisation> authorizations)
        {
            if (authorizations == null) return null;
            var authorisationLists = new List<Authorisation7702Signed>();
            foreach (var sourceAuthorisation in authorizations)
            {
                authorisationLists.Add(ToAuthorisation7702Signed(sourceAuthorisation));
            }

            return authorisationLists;
        }

        public static Authorisation7702Signed ToAuthorisation7702Signed(this Authorisation sourceAuthorisation)
        {
            var authorisationListItem = new Authorisation7702Signed();
            authorisationListItem.Address = sourceAuthorisation.Address;
            authorisationListItem.ChainId = sourceAuthorisation.ChainId;
            authorisationListItem.Nonce = sourceAuthorisation.Nonce;
            authorisationListItem.R = sourceAuthorisation.R.HexToByteArray();
            authorisationListItem.S = sourceAuthorisation.S.HexToByteArray();
            authorisationListItem.V = sourceAuthorisation.YParity.HexToByteArray();
            return authorisationListItem;
        }

        public static List<Authorisation> ToRPCAuthorisation(this List<Authorisation7702Signed> authorisations)
        {
            if (authorisations == null) return null;
            var authorisationLists = new List<Authorisation>();
            foreach (var sourceAuthorisation in authorisations)
            {
                authorisationLists.Add(ToRPCAuthorisation(sourceAuthorisation));
            }
            return authorisationLists;
        }

        public static Authorisation ToRPCAuthorisation(this Authorisation7702Signed sourceAuthorisation)
        {
            var authorisationListItem = new Authorisation();
            authorisationListItem.Address = sourceAuthorisation.Address;
            authorisationListItem.ChainId = new HexBigInteger(sourceAuthorisation.ChainId);
            authorisationListItem.Nonce = new HexBigInteger(sourceAuthorisation.Nonce);
            authorisationListItem.R = sourceAuthorisation.R.ToHex();
            authorisationListItem.S = sourceAuthorisation.S.ToHex();
            authorisationListItem.YParity = sourceAuthorisation.V.ToHex();
            return authorisationListItem;
        }
    }
}
