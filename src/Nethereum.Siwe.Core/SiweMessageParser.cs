using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Nethereum.Siwe.Core
{
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
