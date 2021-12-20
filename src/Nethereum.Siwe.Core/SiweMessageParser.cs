using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.Util;

namespace Nethereum.Siwe.Core
{

    public class SiweMessageStringBuilder
    {
        public const string DOMAIN = "{0} wants you to sign in with your Ethereum account:";
        public const string ADDRESS = "\n{0}\n\n";
        public const string STATEMENT = "{0}\n";
        public const string URI_LINE = "\nURI: {0}";
        public const string VERSION = "\nVersion: {0}";
        public const string CHAIN_ID = "\nChain ID: {0}";
        public const string NONCE = "\nNonce: {0}";
        public const string ISSUED_AT = "\nIssued At: {0}";
        public const string EXPIRATION_TIME = "\nExpiration Time: {0}";
        public const string NOT_BEFORE = "\nNot Before: {0}";
        public const string REQUEST_ID = "\nRequest ID: {0}";
        public const string RESOURCES = "\nResources:";
        public const string RESOURCE = "\n- {0}";

        private static string GetDomain(SiweMessage message)
        {
            if (string.IsNullOrEmpty(message.Domain)) throw new ArgumentException("Domain cannot be null or empty");
            return string.Format(DOMAIN, message.Domain);
        }

        private static string GetAddress(SiweMessage message)
        {
            //if (message.Address.IsEthereumChecksumAddress())
            if(message.Address.IsValidEthereumAddressHexFormat())
            {
                return string.Format(ADDRESS, message.Address);
            }

            throw new FormatException("Invalid address format");
        }

        private static string GetStatement(SiweMessage message)
        {
            if (!string.IsNullOrEmpty(message.Statement))
            {
                return string.Format(STATEMENT, message.Statement);
            }
            
            return string.Empty;
        }

        private static string GetUriLine(SiweMessage message)
        {
            return string.Format(URI_LINE, message.Uri);
        }

        private static string GetVersion(SiweMessage message)
        {
            if (string.IsNullOrEmpty(message.Version)) throw new ArgumentException("Version cannot be null or empty");
            return string.Format(VERSION, message.Version);
        }

        private static string GetChainId(SiweMessage message)
        {
            if (string.IsNullOrEmpty(message.ChainId)) throw new ArgumentException("ChainId cannot be null or empty");
            return string.Format(CHAIN_ID, message.ChainId);
        }

        private static string GetNonce(SiweMessage message)
        {
            if (string.IsNullOrEmpty(message.Nonce)) throw new ArgumentException("Nonce cannot be null or empty");
            if (message.Nonce.Length < 8) throw new ArgumentException("Nonce has to be bigger or equal to 8 characters");
            return string.Format(NONCE, message.Nonce);
        }

        private static string GetIssuedAt(SiweMessage message)
        {
            if (string.IsNullOrEmpty(message.IssuedAt)) throw new ArgumentException("IssuedAt cannot be null or empty");
            return string.Format(ISSUED_AT, message.IssuedAt);
        }

        private static string GetExpirationTime(SiweMessage message)
        {
            if (!string.IsNullOrEmpty(message.ExpirationTime))
            {
                return string.Format(EXPIRATION_TIME, message.ExpirationTime);
            }

            return string.Empty;
        }

        private static string GetNotBefore(SiweMessage message)
        {
            if (!string.IsNullOrEmpty(message.NotBefore))
            {
                return string.Format(NOT_BEFORE, message.NotBefore);
            }

            return string.Empty;
        }


        private static string GetRequestId(SiweMessage message)
        {
            if (!string.IsNullOrEmpty(message.RequestId))
            {
                return string.Format(REQUEST_ID, message.RequestId);
            }

            return string.Empty;
        }

        private static string GetResources(SiweMessage message)
        {
            if (message.Resources != null && message.Resources.Count > 0)
            {
                var returnString = RESOURCES;
                foreach (var resource in message.Resources)
                {
                    returnString += string.Format(RESOURCE, resource);
                }

                return returnString;
            }

            return string.Empty;
        }

        //{URI_LINE}{VERSION}{CHAIN_ID}{NONCE}{ISSUED_AT}{EXPIRATION_TIME}{NOT_BEFORE}{REQUEST_ID}{RESOURCES}
        public static string BuildMessage(SiweMessage message)
        {
            return GetDomain(message) +
                   GetAddress(message) +
                   GetStatement(message) +
                   GetUriLine(message) +
                   GetVersion(message) +
                   GetChainId(message) +
                   GetNonce(message) +
                   GetIssuedAt(message) +
                   GetExpirationTime(message) +
                   GetNotBefore(message) +
                   GetRequestId(message) +
                   GetResources(message);

        }
    }

    public class SiweMessageParser
    {
        public const string DOMAIN = "(?<domain>([^?#]*)) wants you to sign in with your Ethereum account:";
        public const string ADDRESS = "\\n(?<address>0x[a-zA-Z0-9]{40})\\n\\n";
        public const string STATEMENT = "((?<statement>[^\\n]+)\\n)?";
        public const string URI = "(([^:?#]+):)?(([^?#]*))?([^?#]*)(\\?([^#]*))?(#(.*))";
        public const string URI_LINE = $"\\nURI: (?<uri>{URI}?)";
        public const string VERSION = "\\nVersion: (?<version>1)";
        public const string CHAIN_ID = "\\nChain ID: (?<chainId>[0-9]+)";
        public const string NONCE = "\\nNonce: (?<nonce>[a-zA-Z0-9]{8,})";
        public const string DATETIME = @"([0-9]+)-(0[1-9]|1[012])-(0[1-9]|[12][0-9]|3[01])[Tt]([01][0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]|60)(\.[0-9]+)?(([Zz])|([\+|\-]([01][0-9]|2[0-3]):[0-5][0-9]))";
        public const string ISSUED_AT = $"\\nIssued At: (?<issuedAt>{DATETIME})";
        public const string EXPIRATION_TIME = $"(\\nExpiration Time: (?<expirationTime>{DATETIME}))?";
        public const string NOT_BEFORE = $"(\\nNot Before: (?<notBefore>{DATETIME}))?";
        public const string REQUEST_ID = "(\\nRequest ID: (?<requestId>[-._~!$&'()*+,;=:@%a-zA-Z0-9]*))?";
        public const string RESOURCES = $"(\\nResources:(?<resources>(\\n- {URI}?)+))?";
        public const string MESSAGE = $@"^{DOMAIN}{ADDRESS}{STATEMENT}{URI_LINE}{VERSION}{CHAIN_ID}{NONCE}{ISSUED_AT}{EXPIRATION_TIME}{NOT_BEFORE}{REQUEST_ID}{RESOURCES}";
        
       

        private static Regex _regex = new System.Text.RegularExpressions.Regex(MESSAGE);

        public static SiweMessage Parse(string siweMessage)
        {
            if (string.IsNullOrEmpty(siweMessage))
                throw new ArgumentException("Siwe Message cannot be null or empty", nameof(siweMessage));
            var matches = _regex.Matches(siweMessage);

            if (matches.Count > 0)
            {
                var siweMessageDecoded = new SiweMessage();
                var fullMatch = matches[0];

                var domain = fullMatch.Groups["domain"].Captures[0].Value;
                var address = fullMatch.Groups["address"].Captures[0].Value;

                if (fullMatch.Groups["statement"].Captures.Count > 0)
                {
                    siweMessageDecoded.Statement = fullMatch.Groups["statement"].Captures[0].Value;
                }
                
                var uri = fullMatch.Groups["uri"].Captures[0].Value;
                var version = fullMatch.Groups["version"].Captures[0].Value;
                var chainId = fullMatch.Groups["chainId"].Captures[0].Value;
                var nonce = fullMatch.Groups["nonce"].Captures[0].Value;
                var issuedAt = fullMatch.Groups["issuedAt"].Captures[0].Value;

                siweMessageDecoded.Domain = domain;
                siweMessageDecoded.Address = address;
                siweMessageDecoded.Uri = uri;
                siweMessageDecoded.Version = version;
                siweMessageDecoded.ChainId = chainId;
                siweMessageDecoded.Nonce = nonce;
                siweMessageDecoded.IssuedAt = issuedAt;

                if(fullMatch.Groups["expirationTime"].Captures.Count > 0)
                {
                    siweMessageDecoded.ExpirationTime = fullMatch.Groups["expirationTime"].Captures[0].Value;
                }

                if (fullMatch.Groups["notBefore"].Captures.Count > 0)
                {
                    siweMessageDecoded.NotBefore = fullMatch.Groups["notBefore"].Captures[0].Value;
                }

                if (fullMatch.Groups["notBefore"].Captures.Count > 0)
                {
                    siweMessageDecoded.NotBefore = fullMatch.Groups["notBefore"].Captures[0].Value;
                }

                if (fullMatch.Groups["requestId"].Captures.Count > 0)
                {
                    siweMessageDecoded.RequestId = fullMatch.Groups["requestId"].Captures[0].Value;
                }

                
                if (fullMatch.Groups["resources"].Captures.Count > 0)
                {
                    var resources = new List<string>();
                    var matchedResources = fullMatch.Groups["resources"].Captures[0].Value;
                    resources.AddRange(matchedResources.Split(new string[]{"\n- "}, StringSplitOptions.RemoveEmptyEntries));
                    siweMessageDecoded.Resources = resources;
                }

                return siweMessageDecoded;
            }
            throw new ArgumentException("Invalid Siwe Message", nameof(siweMessage));
        }

        public static SiweMessage ParseUsingAbnf(string siweMessage)
        {
            var siweMessageRule =
                Parser.Parse(
                    "sign-in-with-ethereum",
                    siweMessage);
            var visitor = new MessageExtractor();
            siweMessageRule.Accept(visitor);
            return visitor.SiweMessage;
        }

    }
}
