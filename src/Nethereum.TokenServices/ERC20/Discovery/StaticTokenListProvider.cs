using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Discovery
{
    public class StaticTokenListProvider : ITokenListProvider
    {
        private readonly Dictionary<long, List<TokenInfo>> _tokensByChain;

        public StaticTokenListProvider()
        {
            _tokensByChain = new Dictionary<long, List<TokenInfo>>();
        }

        public StaticTokenListProvider(Dictionary<long, List<TokenInfo>> tokensByChain)
        {
            _tokensByChain = tokensByChain ?? new Dictionary<long, List<TokenInfo>>();
        }

        public void AddToken(TokenInfo token)
        {
            if (token == null) return;

            if (!_tokensByChain.TryGetValue(token.ChainId, out var list))
            {
                list = new List<TokenInfo>();
                _tokensByChain[token.ChainId] = list;
            }

            var existing = list.FirstOrDefault(t =>
                string.Equals(t.Address, token.Address, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                list.Remove(existing);
            }

            list.Add(token);
        }

        public void AddTokens(IEnumerable<TokenInfo> tokens)
        {
            foreach (var token in tokens)
            {
                AddToken(token);
            }
        }

        public Task<List<TokenInfo>> GetTokensAsync(long chainId)
        {
            _tokensByChain.TryGetValue(chainId, out var tokens);
            return Task.FromResult(tokens ?? new List<TokenInfo>());
        }

        public Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return Task.FromResult<TokenInfo>(null);

            _tokensByChain.TryGetValue(chainId, out var tokens);
            var token = tokens?.FirstOrDefault(t =>
                string.Equals(t.Address, contractAddress, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(token);
        }

        public Task<bool> SupportsChainAsync(long chainId)
        {
            return Task.FromResult(_tokensByChain.ContainsKey(chainId));
        }
    }
}
