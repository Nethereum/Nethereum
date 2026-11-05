using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Util;

namespace Nethereum.Siwe.Core
{
    public class SiweMessage
    {
        public string Domain { get; set; }

        public string Address { get; set; }

        public string Statement { get; set; }

        public string Uri { get; set; }

        public string Version { get; set; }

        public string Nonce { get; set; }

        public string IssuedAt { get; set; }

        public string ExpirationTime { get; set; }

        public string NotBefore { get; set; }

      
        public string RequestId { get; set; }

        public string ChainId { get; set; }

        public List<string> Resources { get; set; }



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
            if (!string.IsNullOrEmpty(IssuedAt))
            {
                return GetIso8602AsDateTime(IssuedAt);
            }

            throw new Exception("IssuedAt Not Set");
        }

        public DateTime GetNotBeforeAsDateTime()
        {
            if (!string.IsNullOrEmpty(NotBefore))
            {
                return GetIso8602AsDateTime(NotBefore);
            }

            throw new Exception("NotBefore Not Set");
        }

        public DateTime GetExpirationTimeAsDateTime()
        {
            if (!string.IsNullOrEmpty(ExpirationTime))
            {
                return GetIso8602AsDateTime(ExpirationTime);
            }

            throw new Exception("ExpirationTime Not Set");
        }

        private static readonly System.Text.RegularExpressions.Regex ExcessFractionalDigits =
            new System.Text.RegularExpressions.Regex(@"(\.\d{7})\d+");

        private static readonly string[] Iso8601Formats =
        {
            "o",
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
        };

        protected DateTime GetIso8602AsDateTime(string iso8601dateTime)
        {
            var truncated = ExcessFractionalDigits.Replace(iso8601dateTime, "$1");
            return DateTime.ParseExact(truncated, Iso8601Formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None).ToUniversalTime();
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
