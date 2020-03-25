using Nethereum.Contracts.Services;
using Nethereum.GSN.DTOs;
using Nethereum.GSN.Exceptions;
using Nethereum.GSN.Interfaces;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN.Validators
{
    public class RecipientBalanceValidator : IRecipientBalanceValidator
    {
        private readonly IEthApiContractService _ethApiContractService;

        public RecipientBalanceValidator(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public async Task Validate(
            string hubAddress,
            string recipient,
            BigInteger gasLimit,
            BigInteger gasPrice,
            BigInteger relayFee)
        {
            var balanceOf = new BalanceOfFunction() { Target = recipient };
            var balanceOfHandler = _ethApiContractService.GetContractQueryHandler<BalanceOfFunction>();

            var balance = await balanceOfHandler
                .QueryAsync<BigInteger>(hubAddress, balanceOf)
                .ConfigureAwait(false);

            if (balance.IsZero)
            {
                throw new GSNLowBalanceException($"Recipient {recipient} has no funds for paying for relayed calls on the relay hub.");
            }

            var maxPossibleCharge = new MaxPossibleChargeFunction()
            {
                RelayedCallStipend = gasLimit,
                GasPriceParam = gasPrice,
                TransactionFee = relayFee
            };
            var maxPossibleChargeHandler = _ethApiContractService.GetContractQueryHandler<MaxPossibleChargeFunction>();

            var maxCharge = await maxPossibleChargeHandler
                .QueryAsync<BigInteger>(hubAddress, maxPossibleCharge)
                .ConfigureAwait(false);

            if (maxCharge.CompareTo(balance) == 1)
            {
                throw new GSNLowBalanceException($"Recipient {recipient} has not enough funds for paying for this relayed call (has {balance}, requires {maxCharge}).");
            }
        }
    }
}
