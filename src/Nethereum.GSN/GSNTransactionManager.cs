using Nethereum.ABI;
using Nethereum.ABI.Encoders;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Contracts.Services;
using Nethereum.GSN.DTOs;
using Nethereum.GSN.Models;
using Nethereum.GSN.Policies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Signer;
using Nethereum.Util;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class GSNTransactionManager : IGSNTransactionManager
    {
        private static string RelayPrefix = "rlx:";

        private readonly GSNOptions _options;
        private readonly IRelayHubManager _relayHubManager;
        private readonly IEthApiContractService _ethApiContractService;
        private readonly IClient _client;
        private readonly IRelayClient _relayClient;
        private readonly IRelayPolicy _relayPolicy;
        private readonly string _privateKey;
        private readonly string _contractAddress;

        // TODO: Remove contractAddress parameter (replace by `To` paramenter of `TransactionInput`)
        public GSNTransactionManager(
            GSNOptions options,
            IRelayHubManager relayHubManager,
            IEthApiContractService ethApiContractService,
            IClient client,
            IRelayClient relayClient,
            IRelayPolicy policy,
            string privateKey,
            string contractAddress)
        {
            _options = options;
            _relayHubManager = relayHubManager;
            _ethApiContractService = ethApiContractService;
            _client = client;
            _relayClient = relayClient;
            _relayPolicy = policy;
            _privateKey = privateKey;
            _contractAddress = contractAddress;
        }

        public async Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));

            if (string.IsNullOrEmpty(transactionInput.From))
                throw new Exception("From address is null of empty");

            if (string.IsNullOrEmpty(transactionInput.To))
                throw new Exception("Cannot deploy a new contract via the GSN");

            var value = transactionInput.Value.HexValue;
            if (value != "0" && value != "0x0")
                throw new Exception("Cannot send funds via the GSN");

            var relayHubAddress = await _relayHubManager
                .GetHubAddressAsync(_contractAddress)
                .ConfigureAwait(false);

            await ValidateRecipientBalance(
                relayHubAddress,
                transactionInput.To,
                transactionInput.Gas.Value,
                transactionInput.GasPrice.Value,
                new BigInteger(0))
                .ConfigureAwait(false);

            var relays = await _relayHubManager
                .GetRelaysAsync(relayHubAddress, _relayPolicy)
                .ConfigureAwait(false);

            foreach (var lazyRelay in relays)
            {
                var relay = lazyRelay.Value;

                if (!relay.Ready ||
                    relay.MinGasPrice.CompareTo(transactionInput.GasPrice.Value) == 1)
                {
                    continue;
                }

                try
                {
                    var hash = await RelayTransaction(relay, transactionInput, relayHubAddress)
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

            return null;
        }

        private async Task ValidateRecipientBalance(
            string hubAddress,
            string recipient,
            BigInteger gasLimit,
            BigInteger gasPrice,
            BigInteger relayFee)
        {
            var balanceOf = new BalanceOfFunction() { Target = recipient };
            var balanceOfHandler = _ethApiContractService.GetContractQueryHandler<BalanceOfFunction>();

            var balance = await balanceOfHandler.QueryAsync<BigInteger>(hubAddress, balanceOf)
                .ConfigureAwait(false);

            if (balance.IsZero)
            {
                throw new Exception($"Recipient ${recipient} has no funds for paying for relayed calls on the relay hub.");
            }

            var maxPossibleCharge = new MaxPossibleChargeFunction()
            {
                RelayedCallStipend = gasLimit,
                GasPriceParam = gasPrice,
                TransactionFee = relayFee
            };
            var maxPossibleChargeHandler = _ethApiContractService.GetContractQueryHandler<MaxPossibleChargeFunction>();

            var maxCharge = await maxPossibleChargeHandler.QueryAsync<BigInteger>(hubAddress, maxPossibleCharge)
                .ConfigureAwait(false);

            if (maxCharge.CompareTo(balance) == 1)
            {
                throw new Exception($"Recipient {recipient} has not enough funds for paying for this relayed call (has {balance}, requires {maxCharge}).");
            }
        }

        private async Task<string> RelayTransaction(
            Relay relay,
            TransactionInput transaction,
            string hubAddress)
        {
            var nonce = await _relayHubManager
                .GetNonceAsync(hubAddress, transaction.From)
                .ConfigureAwait(false);

            var hash = GetTransactionHash(
                transaction.From,
                transaction.To,
                transaction.Data,
                relay.Fee,
                transaction.GasPrice.Value,
                transaction.Gas.Value,
                nonce,
                hubAddress,
                relay.Address);

            var signer = new EthereumMessageSigner();
            var signature = signer.Sign(hash.HexToByteArray(), _privateKey);

            var approvalDataSig = signer.Sign(
                GetApprovalData(
                    transaction,
                    relay.Fee,
                    transaction.GasPrice.Value,
                    transaction.Gas.Value,
                    nonce,
                    hubAddress,
                    relay.Address),
                _privateKey);

            var transactionCount = await _ethApiContractService.Transactions.GetTransactionCount
                .SendRequestAsync(relay.Address)
                .ConfigureAwait(false);
            var relayMaxNonce = transactionCount.Value + new BigInteger(_options.AllowedRelayNonceGap);

            var txHash = await SendViaRelay(
                relay.Url,
                transaction,
                relay.Fee,
                transaction.GasPrice.Value,
                transaction.Gas.Value,
                nonce,
                hubAddress,
                relay.Address,
                signature,
                approvalDataSig,
                relayMaxNonce)
                .ConfigureAwait(false);

            return txHash;
        }

        private string GetTransactionHash(
            string from,
            string to,
            string data,
            BigInteger txFee,
            BigInteger gasPrice,
            BigInteger gasLimit,
            BigInteger nonce,
            string relayHubAddress,
            string relayAddress)
        {
            var keccack256 = new Sha3Keccack();

            var encoder = new IntTypeEncoder();
            return keccack256.CalculateHashFromHex(
                RelayPrefix.ToHexUTF8(),
                from,
                to,
                data,
                encoder.EncodeInt(txFee).ToHex(),
                encoder.EncodeInt(gasPrice).ToHex(),
                encoder.EncodeInt(gasLimit).ToHex(),
                encoder.EncodeInt(nonce).ToHex(),
                relayHubAddress,
                relayAddress);
        }

        private byte[] GetApprovalData(
            TransactionInput transaction,
            BigInteger txFee,
            BigInteger gasPrice,
            BigInteger gasLimit,
            BigInteger nonce,
            string relayHubAddress,
            string relayAddress)
        {
            var abiEncode = new ABIEncode();
            return abiEncode.GetSha3ABIEncodedPacked(
                new ABIValue("address", relayAddress),
                new ABIValue("address", transaction.From),
                new ABIValue("bytes", transaction.Data.HexToByteArray()),
                new ABIValue("uint256", txFee),
                new ABIValue("uint256", gasPrice),
                new ABIValue("uint256", gasLimit),
                new ABIValue("uint256", nonce),
                new ABIValue("address", relayHubAddress),
                new ABIValue("address", transaction.To));
        }

        private async Task<string> SendViaRelay(
            string relayUrl,
            TransactionInput transaction,
            BigInteger txFee,
            BigInteger gasPrice,
            BigInteger gasLimit,
            BigInteger nonce,
            string relayHubAddress,
            string relayAddress,
            string signature,
            string approvalData,
            BigInteger relayMaxNonce)
        {
            var requestData = new RelayRequest
            {
                EncodedFunction = transaction.Data,
                Signature = signature.HexToByteArray(),
                ApprovalData = approvalData.HexToByteArray(),
                From = transaction.From,
                To = transaction.To,
                GasPrice = gasPrice,
                GasLimit = gasLimit,
                RelayFee = txFee,
                RecipientNonce = nonce,
                RelayMaxNonce = relayMaxNonce,
                RelayHubAddress = relayHubAddress,
                UserAgent = _options.UserAgent
            };

            RelayResponse relayResponse = await _relayClient.RelayAsync(new Uri(relayUrl), requestData)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(relayResponse.Error))
                throw new Exception(relayResponse.Error);

            if (relayResponse.Nonce.Value.IsZero)
                throw new Exception("Empty body received from server, or neither 'error' nor 'nonce' fields present.");

            ValidateTx(
                relayResponse,
                transaction,
                txFee,
                gasPrice,
                gasLimit,
                nonce,
                relayHubAddress,
                relayAddress);

            var tx = ConvertToTx(relayResponse);
            string hash = string.Empty;
            var ethSendTransaction = new EthSendRawTransaction(_client);
            try
            {
                hash = await ethSendTransaction.SendRequestAsync(tx.GetRLPEncoded().ToHex().EnsureHexPrefix())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("the tx doesn't have the correct nonce") &&
                    !ex.Message.Contains("known transaction") &&
                    !ex.Message.Contains("nonce too low"))
                {
                    throw ex;
                }
            }

            var txHash = relayResponse.Hash;
            if (!string.IsNullOrEmpty(hash) &&
                relayResponse.Hash.EnsureHexPrefix().ToLower() != hash.EnsureHexPrefix().ToLower())
            {
                txHash = hash;
            }

            return txHash;
        }

        private void ValidateTx(
            RelayResponse returnedTx,
            TransactionInput transaction,
            BigInteger txFee,
            BigInteger gasPrice,
            BigInteger gasLimit,
            BigInteger nonce,
            string relayHubAddress,
            string relayAddress)
        {
            var tx = ConvertToTx(returnedTx);
            var signer = tx.Key.GetPublicAddress();

            var functionEncodingService = new FunctionMessageEncodingService<RelayCallFunction>();
            var relayCall = functionEncodingService.DecodeInput(new RelayCallFunction(), returnedTx.Input);

            var returnedTxHash = GetTransactionHash(
                relayCall.From,
                relayCall.To,
                relayCall.EncodedFunction.ToHex(),
                relayCall.TransactionFee,
                relayCall.GasPriceParam,
                relayCall.GasLimit,
                relayCall.NonceParam,
                returnedTx.To,
                signer);

            var hash = GetTransactionHash(
                transaction.From,
                transaction.To,
                transaction.Data,
                txFee,
                transaction.GasPrice.Value,
                transaction.Gas.Value,
                nonce,
                relayHubAddress,
                relayAddress);

            if (returnedTxHash.EnsureHexPrefix().ToLower() != hash.EnsureHexPrefix().ToLower() ||
                signer.EnsureHexPrefix().ToLower() != relayAddress.EnsureHexPrefix().ToLower())
                throw new Exception("Relay response is invalid");
        }

        private Nethereum.Signer.Transaction ConvertToTx(RelayResponse response)
        {
            var tx = new Nethereum.Signer.Transaction(
               response.To,
               response.Value.Value,
               response.Nonce.Value,
               response.GasPrice.Value,
               response.Gas.Value,
               response.Input);

            tx.SetSignature(new EthECDSASignature(
                new Org.BouncyCastle.Math.BigInteger(response.R.RemoveHexPrefix(), 16),
                new Org.BouncyCastle.Math.BigInteger(response.S.RemoveHexPrefix(), 16),
                response.V.HexToByteArray()));

            return tx;
        }
    }
}
