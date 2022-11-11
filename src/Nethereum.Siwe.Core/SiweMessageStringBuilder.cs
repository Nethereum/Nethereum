using System;
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
            //should this be a checksum address?
            //if (message.Address.IsEthereumChecksumAddress())
            if(message.Address.IsEthereumChecksumAddress())
            {
                return string.Format(ADDRESS, message.Address);
            }

            throw new FormatException("Invalid address format, please ensure is a valid address using the EIP-55 checksum");
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
}