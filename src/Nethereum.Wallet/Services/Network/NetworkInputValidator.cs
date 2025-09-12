using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.RPC.Chain; 

namespace Nethereum.Wallet.Services.Network
{
    public static class NetworkInputValidator
    {
        public enum NetworkValidationError
        {
            None = 0,
            ChainIdRequired,
            InvalidChainId,
            NetworkNameRequired,
            NetworkNameTooShort,
            CurrencySymbolRequired,
            InvalidCurrencySymbol,
            CurrencyNameRequired,
            CurrencyNameTooShort,
            InvalidCurrencyDecimals,
            RpcUrlRequired,
            InvalidRpcUrlFormat,
            DuplicateRpcUrl,
            ExplorerUrlRequired,
            InvalidExplorerUrlFormat,
            DuplicateExplorerUrl,
            AtLeastOneRpcRequired
        }

        private static readonly string[] RpcValidSchemes = { "http", "https", "ws", "wss" };
        private static readonly string[] ExplorerValidSchemes = { "http", "https" };

        public static bool IsValid(NetworkValidationError err) => err == NetworkValidationError.None;

        #region Primitive validators

        public static NetworkValidationError ValidateChainId(string chainId)
        {
            if (string.IsNullOrWhiteSpace(chainId)) return NetworkValidationError.ChainIdRequired;
            if (!BigInteger.TryParse(chainId, out var id) || id <= 0) return NetworkValidationError.InvalidChainId;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateChainId(BigInteger chainId)
        {
            if (chainId <= 0) return NetworkValidationError.InvalidChainId;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateNetworkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return NetworkValidationError.NetworkNameRequired;
            if (name.Trim().Length < 2) return NetworkValidationError.NetworkNameTooShort;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateCurrencySymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return NetworkValidationError.CurrencySymbolRequired;
            if (symbol.Trim().Length > 10) return NetworkValidationError.InvalidCurrencySymbol;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateCurrencyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return NetworkValidationError.CurrencyNameRequired;
            if (name.Trim().Length < 2) return NetworkValidationError.CurrencyNameTooShort;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateCurrencyDecimals(int decimals)
        {
            if (decimals < 0 || decimals > 18) return NetworkValidationError.InvalidCurrencyDecimals;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateRpcUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return NetworkValidationError.RpcUrlRequired;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return NetworkValidationError.InvalidRpcUrlFormat;
            if (!RpcValidSchemes.Contains(uri.Scheme.ToLowerInvariant())) return NetworkValidationError.InvalidRpcUrlFormat;
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateExplorerUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return NetworkValidationError.ExplorerUrlRequired;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return NetworkValidationError.InvalidExplorerUrlFormat;
            if (!ExplorerValidSchemes.Contains(uri.Scheme.ToLowerInvariant())) return NetworkValidationError.InvalidExplorerUrlFormat;
            return NetworkValidationError.None;
        }

        #endregion

        #region Duplicate helpers

        public static bool IsDuplicateChainId(BigInteger chainId, IEnumerable<BigInteger> existingIds) =>
            existingIds.Contains(chainId);

        public static NetworkValidationError CheckDuplicateRpc(string url, IEnumerable<string> existing) =>
            existing.Any(r => string.Equals(r, url, StringComparison.OrdinalIgnoreCase))
                ? NetworkValidationError.DuplicateRpcUrl
                : NetworkValidationError.None;

        public static NetworkValidationError CheckDuplicateExplorer(string url, IEnumerable<string> existing) =>
            existing.Any(r => string.Equals(r, url, StringComparison.OrdinalIgnoreCase))
                ? NetworkValidationError.DuplicateExplorerUrl
                : NetworkValidationError.None;

        public static bool HasDuplicateRpcUrls(IEnumerable<string> urls)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in urls)
            {
                if (!set.Add(u)) return true;
            }
            return false;
        }

        #endregion

        #region Aggregated collections

        public static NetworkValidationError ValidateRpcCollection(IEnumerable<string> urls)
        {
            if (urls == null || !urls.Any()) return NetworkValidationError.AtLeastOneRpcRequired;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in urls)
            {
                var e = ValidateRpcUrl(u);
                if (!IsValid(e)) return e;
                if (!set.Add(u)) return NetworkValidationError.DuplicateRpcUrl;
            }
            return NetworkValidationError.None;
        }

        public static NetworkValidationError ValidateRpcEndpoint(string url, IEnumerable<string> existing)
        {
            var fmt = ValidateRpcUrl(url);
            if (!IsValid(fmt)) return fmt;
            var dup = CheckDuplicateRpc(url, existing);
            return dup;
        }

        public static NetworkValidationError ValidateExplorer(string url, IEnumerable<string> existing)
        {
            var fmt = ValidateExplorerUrl(url);
            if (!IsValid(fmt)) return fmt;
            var dup = CheckDuplicateExplorer(url, existing);
            return dup;
        }

        #endregion

        #region ChainFeature aggregate

        /// <summary>
        /// Full validation of a ChainFeature object. Collects all errors (no short-circuit).
        /// </summary>
        public static bool ValidateChainFeature(ChainFeature chain, out List<NetworkValidationError> errors)
        {
            errors = new List<NetworkValidationError>();
            if (chain == null)
            {
                errors.Add(NetworkValidationError.InvalidChainId);
                return false;
            }

            // ChainId
            var cidErr = ValidateChainId(chain.ChainId);
            if (!IsValid(cidErr)) errors.Add(cidErr);

            // Name
            var nameErr = ValidateNetworkName(chain.ChainName ?? string.Empty);
            if (!IsValid(nameErr)) errors.Add(nameErr);

            // Currency (native optional object, but if present validate)
            if (chain.NativeCurrency != null)
            {
                var symErr = ValidateCurrencySymbol(chain.NativeCurrency.Symbol ?? string.Empty);
                if (!IsValid(symErr)) errors.Add(symErr);
                var curNameErr = ValidateCurrencyName(chain.NativeCurrency.Name ?? string.Empty);
                if (!IsValid(curNameErr)) errors.Add(curNameErr);
                var decErr = ValidateCurrencyDecimals((int)chain.NativeCurrency.Decimals);
                if (!IsValid(decErr)) errors.Add(decErr);
            }

            var http = chain.HttpRpcs ?? new List<string>();
            var ws = chain.WsRpcs ?? new List<string>();
            var combinedRpcs = http.Concat(ws).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (combinedRpcs.Count == 0)
            {
                errors.Add(NetworkValidationError.AtLeastOneRpcRequired);
            }
            else
            {
                var rpcSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rpc in combinedRpcs)
                {
                    var rerr = ValidateRpcUrl(rpc);
                    if (!IsValid(rerr)) errors.Add(rerr);
                    if (!rpcSet.Add(rpc)) errors.Add(NetworkValidationError.DuplicateRpcUrl);
                }
            }

            // Explorers
            if (chain.Explorers != null)
            {
                var expSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var ex in chain.Explorers.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    var exErr = ValidateExplorerUrl(ex);
                    if (!IsValid(exErr)) errors.Add(exErr);
                    if (!expSet.Add(ex)) errors.Add(NetworkValidationError.DuplicateExplorerUrl);
                }
            }

            // De-duplicate collected errors
            errors = errors.Distinct().ToList();
            return errors.Count == 0;
        }

        #endregion
    }
}