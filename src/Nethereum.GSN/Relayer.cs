using Nethereum.GSN.Exceptions;
using Nethereum.GSN.Interfaces;
using Nethereum.GSN.Models;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class Relayer : IRelayer
    {
        private readonly IRelayHubManager _relayHubManager;
        private readonly IRecipientBalanceValidator _balanceValidator;
        private readonly IRelayPolicy _relayPolicy;

        public Relayer(IRelayHubManager relayHubManager, IRecipientBalanceValidator balanceValidator, IRelayPolicy relayPolicy)
        {
            _relayHubManager = relayHubManager;
            _balanceValidator = balanceValidator;
            _relayPolicy = relayPolicy;
        }

        public async Task<string> Relay(
            TransactionInput transaction,
            Func<Relay, TransactionInput, string, Task<string>> relayFunc)
        {
            var relayHubAddress = await _relayHubManager
                .GetHubAddressAsync(transaction.To)
                .ConfigureAwait(false);

            await _balanceValidator.Validate(
                relayHubAddress,
                transaction.To,
                transaction.Gas.Value,
                transaction.GasPrice.Value,
                new BigInteger(0))
                .ConfigureAwait(false);

            var relays = await _relayHubManager
                .GetRelaysAsync(relayHubAddress, _relayPolicy)
                .ConfigureAwait(false);

            BigInteger gasPrice = transaction.GasPrice;
            BigInteger minGasPrice = default;

            foreach (var lazyRelay in relays)
            {
                var relay = lazyRelay.Value;

                if (minGasPrice == default)
                {
                    minGasPrice = relay.MinGasPrice;
                }

                if (!relay.Ready ||
                    relay.MinGasPrice.CompareTo(gasPrice) == 1)
                {
                    minGasPrice = BigInteger.Min(minGasPrice, relay.MinGasPrice);
                    continue;
                }

                try
                {
                    var nonce = await _relayHubManager
                        .GetNonceAsync(relayHubAddress, transaction.From)
                        .ConfigureAwait(false);
                    transaction.Nonce = new HexBigInteger(nonce);

                    var hash = await relayFunc(relay, transaction, relayHubAddress)
                        .ConfigureAwait(false);
                    await _relayPolicy.GraceAsync(relay).ConfigureAwait(false);
                    return hash;
                }
                catch
                {
                    await _relayPolicy.PenalizeAsync(relay).ConfigureAwait(false);
                    continue;
                }
            }


            var message = minGasPrice.CompareTo(gasPrice) == 1 ?
                $"Relay not found. Minimum relay gas price is {minGasPrice}, provided {gasPrice}" :
                null;
            throw new GSNRelayNotFoundException(message);
        }
    }
}
