using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Util;

namespace Nethereum.Siwe.Core
{
    public class SiweMessage
    {
        /// <summary>
        /// RFC 4501 dns authority that is requesting the signing.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Ethereum address performing the signing conformant to capitalization
        /// encoded checksum specified in EIP-55 where applicable.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Human-readable ASCII assertion that the user will sign, and it must not contain `\n`. 
        /// </summary>
        public string Statement { get; set; }

        /// <summary>
        /// RFC 3986 URI referring to the resource that is the subject of the signing
        /// (as in the __subject__ of a claim).
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Current version of the message. 
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Randomized token used to prevent replay attacks, at least 8 alphanumeric characters. 
        /// </summary>
        public string Nonce { get; set; }

        /// <summary>
        ///  ISO 8601 datetime string of the current time. 
        /// </summary>
        public string IssuedAt { get; set; }

        /// <summary>
        /// ISO 8601 datetime string that, if present, indicates when the signed authentication message is no longer valid. 
        /// </summary>
        public string ExpirationTime { get; set; }

        /// <summary>
        /// ISO 8601 datetime string that, if present, indicates when the signed authentication message will become valid. 
        /// </summary>
        public string NotBefore { get; set; }

        /// <summary>
        /// System-specific identifier that may be used to uniquely refer to the sign-in request
        /// </summary>
      
        public string RequestId { get; set; }

        /// <summary>
        /// EIP-155 Chain ID to which the session is bound, and the network where, Contract Accounts must be resolved
        /// </summary>
        public string ChainId { get; set; }

        /// <summary>
        /// List of information or references to information the user wishes to have resolved as part of authentication by the relying party. They are expressed as RFC 3986 URIs separated by `\n- `
        /// </summary>
        public List<string> Resources { get; set; }

        ///// <summary>
        /////Signature of the message signed by the wallet
        ///// </summary>
        //public string Signature { get; set; }


        public void SetIssuedAtNow()
        {
           SetIssuedAt(DateTime.Now);
        }

        public void SetIssuedAt(DateTime dateTime)
        {
            IssuedAt = GetDateAsIso8602String(dateTime);
        }

        private static string GetDateAsIso8602String(DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString("o");
        }

        public DateTime GetIssuedAtAsDateTime()
        {
            if (string.IsNullOrEmpty(IssuedAt))
            {
                return GetIso8602AsDateTime(IssuedAt);
            }

            throw new Exception("IssuedAt Not Set");
        }

        public DateTime GetNotBeforeAsDateTime()
        {
            if (string.IsNullOrEmpty(NotBefore))
            {
                return GetIso8602AsDateTime(NotBefore);
            }

            throw new Exception("NotBefore Not Set");
        }

        public DateTime GetExpirationTimeAsDateTime()
        {
            if (string.IsNullOrEmpty(ExpirationTime))
            {
                return GetIso8602AsDateTime(ExpirationTime);
            }

            throw new Exception("ExpirationTime Not Set");
        }

        protected DateTime GetIso8602AsDateTime(string iso8601dateTime)
        {
            return DateTime.ParseExact(iso8601dateTime, "o",
                System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
        }

        public void SetExpirationTime(DateTime utcDateTime)
        {
            ExpirationTime = GetDateAsIso8602String(utcDateTime);
        }

        public void SetNotBefore(DateTime utcDateTime)
        {
            NotBefore = GetDateAsIso8602String(utcDateTime);
        }

        public bool HasMessageDateStarted()
        {
            if (string.IsNullOrEmpty(NotBefore)) return true;
            return DateTime.Now.ToUniversalTime() > GetIso8602AsDateTime(NotBefore);
        }

        public bool HasMessageDateExpired()
        {
            if (string.IsNullOrEmpty(ExpirationTime)) return false;
            return DateTime.Now.ToUniversalTime() > GetIso8602AsDateTime(ExpirationTime);
        }

        public bool HasMessageDateStartedAndNotExpired()
        {
            return HasMessageDateStarted() && !HasMessageDateExpired();
        }

        public bool HasRequiredFields()
        {
            return !string.IsNullOrEmpty(Domain) &&
                   Address.IsValidEthereumAddressHexFormat() &&
                   !string.IsNullOrEmpty(Version) &&
                   !string.IsNullOrEmpty(Nonce) &&
                   !string.IsNullOrEmpty(ChainId) &&
                   !string.IsNullOrEmpty(IssuedAt);
        }

        public bool IsTheSame(SiweMessage other)
        {
            return SiweMessageUtil.AreMessagesTheSame(this, other);
        }
	}

    public static class SiweMessageUtil
    {
        public static bool AreMessagesTheSame(SiweMessage first, SiweMessage second)
        {
            var currentMessage = SiweMessageStringBuilder.BuildMessage(first);
            var existingMessage = SiweMessageStringBuilder.BuildMessage(second);
            if (currentMessage == existingMessage)
            {
                return true;
            }

            return false;
        }
    }
}
