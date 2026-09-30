using System;
using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public static class EthECKeyBuilderFromSignedAuthorisation
    {
        public static EthECKey RecoverEthECKey(this Authorisation7702Signed authorisation7702Signed)
        {
            var signature = EthECDSASignatureFactory.FromSignature(authorisation7702Signed);
            var hash = authorisation7702Signed.EncodeAndHash();
            return EthECKey.RecoverFromParityYSignature(signature, hash);
        }

        public static string RecoverSignerAddress(this Authorisation7702Signed authorisation7702Signed)
        {
            return authorisation7702Signed.RecoverEthECKey().GetPublicAddress();
        }

        /// <summary>
        /// The authority of a signed authorization tuple, or null when its signature
        /// yields none — EIP-7702's y-parity and canonical-s gates, then recovery.
        ///
        /// <para>The single definition of "this tuple has an authority", shared by
        /// <c>Eip7702AuthorizationApplication</c> and by every producer of a block
        /// witness.</para>
        ///
        /// <para>A malformed signature is not an authority; a cancellation or an
        /// out-of-memory is not an answer about the signature at all and propagates.</para>
        /// </summary>
        public static string TryRecoverSignerAddress(this Authorisation7702Signed authorisation7702Signed)
        {
            var yParity = authorisation7702Signed.V == null || authorisation7702Signed.V.Length == 0
                ? (byte)0
                : authorisation7702Signed.V[0];
            if ((authorisation7702Signed.V != null && authorisation7702Signed.V.Length > 1) || yParity > 1)
                return null;

            try
            {
                if (!EthECDSASignatureFactory.FromSignature(authorisation7702Signed).IsCanonical)
                    return null;

                return authorisation7702Signed.RecoverSignerAddress();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException || ex is OutOfMemoryException))
            {
                return null;
            }
        }

        public static List<string> RecoverAuthorities(this ISignedTransaction signedTransaction)
        {
            var authorisationList = (signedTransaction as Transaction7702)?.AuthorisationList;
            if (authorisationList == null)
                return null;

            var authorities = new List<string>(authorisationList.Count);
            foreach (var authorisation in authorisationList)
                authorities.Add(authorisation.TryRecoverSignerAddress());
            return authorities;
        }
    }
}
