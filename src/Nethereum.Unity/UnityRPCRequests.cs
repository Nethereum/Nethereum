using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.Compilation;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Mining;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Eth.Uncles;
using Nethereum.RPC.Net;
using Nethereum.RPC.Personal;
using Nethereum.RPC.Shh;
using Nethereum.RPC.Shh.KeyPair;
using Nethereum.RPC.Web3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Nethereum.JsonRpc.UnityClient
{
    public class Web3ClientVersionUnityRequest : UnityRpcClient<string>
    {
        private readonly Web3ClientVersion _web3ClientVersion;

        public Web3ClientVersionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _web3ClientVersion = new Web3ClientVersion(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _web3ClientVersion.BuildRequest();
            return SendRequest(request);
        }
    }

    public class Web3Sha3UnityRequest : UnityRpcClient<string>
    {
        private readonly Web3Sha3 _web3Sha3;

        public Web3Sha3UnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _web3Sha3 = new Web3Sha3(null);
        }

        public IEnumerator SendRequest(HexUTF8String valueToConvert)
        {
            var request = _web3Sha3.BuildRequest(valueToConvert);
            return SendRequest(request);
        }
    }

    public class ShhNewKeyPairUnityRequest : UnityRpcClient<string>
    {
        private readonly ShhNewKeyPair _shhNewKeyPair;

        public ShhNewKeyPairUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _shhNewKeyPair = new ShhNewKeyPair(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _shhNewKeyPair.BuildRequest();
            return SendRequest(request);
        }
    }

    public class ShhAddPrivateKeyUnityRequest : UnityRpcClient<string>
    {
        private readonly ShhAddPrivateKey _addPrivateKey;

        public ShhAddPrivateKeyUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _addPrivateKey = new ShhAddPrivateKey(null);
        }

        public IEnumerator SendRequest(string privateKey)
        {
            var request = _addPrivateKey.BuildRequest(privateKey);
            return SendRequest(request);
        }
    }

    public class ShhVersionUnityRequest : UnityRpcClient<string>
    {
        private readonly ShhVersion _shhVersion;

        public ShhVersionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _shhVersion = new ShhVersion(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _shhVersion.BuildRequest();
            return SendRequest(request);
        }
    }

    public class PersonalListAccountsUnityRequest : UnityRpcClient<string[]>
    {
        private readonly PersonalListAccounts _personalListAccounts;

        public PersonalListAccountsUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _personalListAccounts = new PersonalListAccounts(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _personalListAccounts.BuildRequest();
            return SendRequest(request);
        }
    }

    public class PersonalLockAccountUnityRequest : UnityRpcClient<bool>
    {
        private readonly PersonalLockAccount _personalLockAccount;

        public PersonalLockAccountUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _personalLockAccount = new PersonalLockAccount(null);
        }

        public IEnumerator SendRequest(string account)
        {
            var request = _personalLockAccount.BuildRequest(account);
            return SendRequest(request);
        }
    }

    public class PersonalNewAccountUnityRequest : UnityRpcClient<string>
    {
        private readonly PersonalNewAccount _personalNewAccount;

        public PersonalNewAccountUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _personalNewAccount = new PersonalNewAccount(null);
        }

        public IEnumerator SendRequest(string passPhrase)
        {
            var request = _personalNewAccount.BuildRequest(passPhrase);
            return SendRequest(request);
        }
    }

    public class PersonalSignAndSendTransactionUnityRequest : UnityRpcClient<string>
    {
        private readonly PersonalSignAndSendTransaction _personalSignAndSendTransaction;

        public PersonalSignAndSendTransactionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _personalSignAndSendTransaction = new PersonalSignAndSendTransaction(null);
        }

        public IEnumerator SendRequest(TransactionInput txn, string password)
        {
            var request = _personalSignAndSendTransaction.BuildRequest(txn, password);
            return SendRequest(request);
        }
    }

    public class PersonalUnlockAccountUnityRequest : UnityRpcClient<bool>
    {
        private readonly PersonalUnlockAccount _personalUnlockAccount;

        public PersonalUnlockAccountUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _personalUnlockAccount = new PersonalUnlockAccount(null);
        }

        public IEnumerator SendRequest(string address, string passPhrase, int durationInSeconds)
        {
            var request = _personalUnlockAccount.BuildRequest(address, passPhrase, durationInSeconds);
            return SendRequest(request);
        }
    }

    public class NetListeningUnityRequest : UnityRpcClient<bool>
    {
        private readonly NetListening _netListening;

        public NetListeningUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _netListening = new NetListening(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _netListening.BuildRequest();
            return SendRequest(request);
        }
    }

    public class NetPeerCountUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly NetPeerCount _netPeerCount;

        public NetPeerCountUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _netPeerCount = new NetPeerCount(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _netPeerCount.BuildRequest();
            return SendRequest(request);
        }
    }

    public class NetVersionUnityRequest : UnityRpcClient<string>
    {
        private readonly NetVersion _netVersion;

        public NetVersionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _netVersion = new NetVersion(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _netVersion.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthAccountsUnityRequest : UnityRpcClient<string[]>
    {
        private readonly EthAccounts _ethAccounts;

        public EthAccountsUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethAccounts = new EthAccounts(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethAccounts.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthCoinBaseUnityRequest : UnityRpcClient<string>
    {
        private readonly EthCoinBase _ethCoinBase;

        public EthCoinBaseUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethCoinBase = new EthCoinBase(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethCoinBase.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthGasPriceUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGasPrice _ethGasPrice;

        public EthGasPriceUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGasPrice = new EthGasPrice(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethGasPrice.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthGetBalanceUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGetBalance _ethGetBalance;

        public EthGetBalanceUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBalance = new EthGetBalance(null);
        }

        public IEnumerator SendRequest(string address, BlockParameter block)
        {
            var request = _ethGetBalance.BuildRequest(address, block);
            return SendRequest(request);
        }
    }

    public class EthGetCodeUnityRequest : UnityRpcClient<string>
    {
        private readonly EthGetCode _ethGetCode;

        public EthGetCodeUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetCode = new EthGetCode(null);
        }

        public IEnumerator SendRequest(string address, BlockParameter block)
        {
            var request = _ethGetCode.BuildRequest(address, block);
            return SendRequest(request);
        }
    }

    public class EthGetStorageAtUnityRequest : UnityRpcClient<string>
    {
        private readonly EthGetStorageAt _ethGetStorageAt;

        public EthGetStorageAtUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetStorageAt = new EthGetStorageAt(null);
        }

        public IEnumerator SendRequest(string address, HexBigInteger position, BlockParameter block)
        {
            var request = _ethGetStorageAt.BuildRequest(address, position, block);
            return SendRequest(request);
        }
    }

    public class EthProtocolVersionUnityRequest : UnityRpcClient<string>
    {
        private readonly EthProtocolVersion _ethProtocolVersion;

        public EthProtocolVersionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethProtocolVersion = new EthProtocolVersion(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethProtocolVersion.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthSignUnityRequest : UnityRpcClient<string>
    {
        private readonly EthSign _ethSign;

        public EthSignUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethSign = new EthSign(null);
        }

        public IEnumerator SendRequest(string address, string data)
        {
            var request = _ethSign.BuildRequest(address, data);
            return SendRequest(request);
        }
    }

    public class EthSyncingUnityRequest : UnityRpcClient<object>
    {
        private readonly EthSyncing _ethSyncing;

        public EthSyncingUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethSyncing = new EthSyncing(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethSyncing.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthGetUncleByBlockHashAndIndexUnityRequest : UnityRpcClient<BlockWithTransactionHashes>
    {
        private readonly EthGetUncleByBlockHashAndIndex _ethGetUncleByBlockHashAndIndex;

        public EthGetUncleByBlockHashAndIndexUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetUncleByBlockHashAndIndex = new EthGetUncleByBlockHashAndIndex(null);
        }

        public IEnumerator SendRequest(string blockHash, HexBigInteger uncleIndex)
        {
            var request = _ethGetUncleByBlockHashAndIndex.BuildRequest(blockHash, uncleIndex);
            return SendRequest(request);
        }
    }

    public class EthGetUncleByBlockNumberAndIndexUnityRequest : UnityRpcClient<BlockWithTransactionHashes>
    {
        private readonly EthGetUncleByBlockNumberAndIndex _ethGetUncleByBlockNumberAndIndex;

        public EthGetUncleByBlockNumberAndIndexUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetUncleByBlockNumberAndIndex = new EthGetUncleByBlockNumberAndIndex(null);
        }

        public IEnumerator SendRequest(BlockParameter blockParameter, HexBigInteger uncleIndex)
        {
            var request = _ethGetUncleByBlockNumberAndIndex.BuildRequest(blockParameter, uncleIndex);
            return SendRequest(request);
        }
    }

    public class EthGetUncleCountByBlockHashUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGetUncleCountByBlockHash _ethGetUncleCountByBlockHash;

        public EthGetUncleCountByBlockHashUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(null);
        }

        public IEnumerator SendRequest(string hash)
        {
            var request = _ethGetUncleCountByBlockHash.BuildRequest(hash);
            return SendRequest(request);
        }
    }

    public class EthGetUncleCountByBlockNumberUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGetUncleCountByBlockNumber _ethGetUncleCountByBlockNumber;

        public EthGetUncleCountByBlockNumberUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber(null);
        }

        public IEnumerator SendRequest(HexBigInteger blockNumber)
        {
            var request = _ethGetUncleCountByBlockNumber.BuildRequest(blockNumber);
            return SendRequest(request);
        }
    }

    public class EthCallUnityRequest : UnityRpcClient<string>
    {
        private readonly EthCall _ethCall;

        public EthCallUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethCall = new EthCall(null);
        }

        public IEnumerator SendRequest(CallInput callInput, BlockParameter block)
        {
            var request = _ethCall.BuildRequest(callInput, block);
            return SendRequest(request);
        }
    }

    public class EthEstimateGasUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthEstimateGas _ethEstimateGas;

        public EthEstimateGasUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethEstimateGas = new EthEstimateGas(null);
        }

        public IEnumerator SendRequest(CallInput callInput)
        {
            var request = _ethEstimateGas.BuildRequest(callInput);
            return SendRequest(request);
        }
    }

    public class EthGetTransactionByBlockHashAndIndexUnityRequest : UnityRpcClient<Transaction>
    {
        private readonly EthGetTransactionByBlockHashAndIndex _ethGetTransactionByBlockHashAndIndex;

        public EthGetTransactionByBlockHashAndIndexUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetTransactionByBlockHashAndIndex = new EthGetTransactionByBlockHashAndIndex(null);
        }

        public IEnumerator SendRequest(string blockHash, HexBigInteger transactionIndex)
        {
            var request = _ethGetTransactionByBlockHashAndIndex.BuildRequest(blockHash, transactionIndex);
            return SendRequest(request);
        }
    }

    public class EthGetTransactionByBlockNumberAndIndexUnityRequest : UnityRpcClient<Transaction>
    {
        private readonly EthGetTransactionByBlockNumberAndIndex _ethGetTransactionByBlockNumberAndIndex;

        public EthGetTransactionByBlockNumberAndIndexUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetTransactionByBlockNumberAndIndex = new EthGetTransactionByBlockNumberAndIndex(null);
        }

        public IEnumerator SendRequest(HexBigInteger blockNumber, HexBigInteger transactionIndex)
        {
            var request = _ethGetTransactionByBlockNumberAndIndex.BuildRequest(blockNumber, transactionIndex);
            return SendRequest(request);
        }
    }

    public class EthGetTransactionByHashUnityRequest : UnityRpcClient<Transaction>
    {
        private readonly EthGetTransactionByHash _ethGetTransactionByHash;

        public EthGetTransactionByHashUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetTransactionByHash = new EthGetTransactionByHash(null);
        }

        public IEnumerator SendRequest(string hashTransaction)
        {
            var request = _ethGetTransactionByHash.BuildRequest(hashTransaction);
            return SendRequest(request);
        }
    }

    public class EthGetTransactionCountUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGetTransactionCount _ethGetTransactionCount;

        public EthGetTransactionCountUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetTransactionCount = new EthGetTransactionCount(null);
        }

        public IEnumerator SendRequest(string address, BlockParameter block)
        {
            var request = _ethGetTransactionCount.BuildRequest(address, block);
            return SendRequest(request);
        }
    }

    public class EthGetTransactionReceiptUnityRequest : UnityRpcClient<TransactionReceipt>
    {
        private readonly EthGetTransactionReceipt _ethGetTransactionReceipt;

        public EthGetTransactionReceiptUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetTransactionReceipt = new EthGetTransactionReceipt(null);
        }

        public IEnumerator SendRequest(string transactionHash)
        {
            var request = _ethGetTransactionReceipt.BuildRequest(transactionHash);
            return SendRequest(request);
        }
    }

    public class EthSendRawTransactionUnityRequest : UnityRpcClient<string>
    {
        private readonly EthSendRawTransaction _ethSendRawTransaction;

        public EthSendRawTransactionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethSendRawTransaction = new EthSendRawTransaction(null);
        }

        public IEnumerator SendRequest(string signedTransactionData)
        {
            var request = _ethSendRawTransaction.BuildRequest(signedTransactionData);
            return SendRequest(request);
        }
    }

    public class EthSendTransactionUnityRequest : UnityRpcClient<string>
    {
        private readonly EthSendTransaction _ethSendTransaction;

        public EthSendTransactionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethSendTransaction = new EthSendTransaction(null);
        }

        public IEnumerator SendRequest(TransactionInput input)
        {
            var request = _ethSendTransaction.BuildRequest(input);
            return SendRequest(request);
        }
    }

    public class EthGetWorkUnityRequest : UnityRpcClient<string[]>
    {
        private readonly EthGetWork _ethGetWork;

        public EthGetWorkUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetWork = new EthGetWork(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethGetWork.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthHashrateUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthHashrate _ethHashrate;

        public EthHashrateUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethHashrate = new EthHashrate(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethHashrate.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthMiningUnityRequest : UnityRpcClient<bool>
    {
        private readonly EthMining _ethMining;

        public EthMiningUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethMining = new EthMining(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethMining.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthSubmitHashrateUnityRequest : UnityRpcClient<bool>
    {
        private readonly EthSubmitHashrate _ethSubmitHashrate;

        public EthSubmitHashrateUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethSubmitHashrate = new EthSubmitHashrate(null);
        }

        public IEnumerator SendRequest(string hashRate, string clientId)
        {
            var request = _ethSubmitHashrate.BuildRequest(hashRate, clientId);
            return SendRequest(request);
        }
    }

    public class EthSubmitWorkUnityRequest : UnityRpcClient<bool>
    {
        private readonly EthSubmitWork _ethSubmitWork;

        public EthSubmitWorkUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethSubmitWork = new EthSubmitWork(null);
        }

        public IEnumerator SendRequest(string nonce, string header, string mix)
        {
            var request = _ethSubmitWork.BuildRequest(nonce, header, mix);
            return SendRequest(request);
        }
    }

    public class EthGetFilterChangesForEthNewFilterUnityRequest : UnityRpcClient<FilterLog[]>
    {
        private readonly EthGetFilterChangesForEthNewFilter _ethGetFilterChangesForEthNewFilter;

        public EthGetFilterChangesForEthNewFilterUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetFilterChangesForEthNewFilter = new EthGetFilterChangesForEthNewFilter(null);
        }

        public IEnumerator SendRequest(HexBigInteger filterId)
        {
            var request = _ethGetFilterChangesForEthNewFilter.BuildRequest(filterId);
            return SendRequest(request);
        }
    }

    public class EthGetFilterChangesForBlockOrTransactionUnityRequest : UnityRpcClient<string[]>
    {
        private readonly EthGetFilterChangesForBlockOrTransaction _ethGetFilterChangesForBlockOrTransaction;

        public EthGetFilterChangesForBlockOrTransactionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetFilterChangesForBlockOrTransaction = new EthGetFilterChangesForBlockOrTransaction(null);
        }

        public IEnumerator SendRequest(HexBigInteger filterId)
        {
            var request = _ethGetFilterChangesForBlockOrTransaction.BuildRequest(filterId);
            return SendRequest(request);
        }
    }

    public class EthGetFilterLogsForBlockOrTransactionUnityRequest : UnityRpcClient<string[]>
    {
        private readonly EthGetFilterLogsForBlockOrTransaction _ethGetFilterLogsForBlockOrTransaction;

        public EthGetFilterLogsForBlockOrTransactionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetFilterLogsForBlockOrTransaction = new EthGetFilterLogsForBlockOrTransaction(null);
        }

        public IEnumerator SendRequest(HexBigInteger filterId)
        {
            var request = _ethGetFilterLogsForBlockOrTransaction.BuildRequest(filterId);
            return SendRequest(request);
        }
    }

    public class EthGetFilterLogsForEthNewFilterUnityRequest : UnityRpcClient<FilterLog[]>
    {
        private readonly EthGetFilterLogsForEthNewFilter _ethGetFilterLogsForEthNewFilter;

        public EthGetFilterLogsForEthNewFilterUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetFilterLogsForEthNewFilter = new EthGetFilterLogsForEthNewFilter(null);
        }

        public IEnumerator SendRequest(HexBigInteger filterId)
        {
            var request = _ethGetFilterLogsForEthNewFilter.BuildRequest(filterId);
            return SendRequest(request);
        }
    }

    public class EthGetLogsUnityRequest : UnityRpcClient<FilterLog[]>
    {
        private readonly EthGetLogs _ethGetLogs;

        public EthGetLogsUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetLogs = new EthGetLogs(null);
        }

        public IEnumerator SendRequest(NewFilterInput newFilter)
        {
            var request = _ethGetLogs.BuildRequest(newFilter);
            return SendRequest(request);
        }
    }

    public class EthNewBlockFilterUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthNewBlockFilter _ethNewBlockFilter;

        public EthNewBlockFilterUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethNewBlockFilter = new EthNewBlockFilter(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethNewBlockFilter.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthNewFilterUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthNewFilter _ethNewFilter;

        public EthNewFilterUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethNewFilter = new EthNewFilter(null);
        }

        public IEnumerator SendRequest(NewFilterInput newFilterInput)
        {
            var request = _ethNewFilter.BuildRequest(newFilterInput);
            return SendRequest(request);
        }
    }

    public class EthNewPendingTransactionFilterUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthNewPendingTransactionFilter _ethNewPendingTransactionFilter;

        public EthNewPendingTransactionFilterUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethNewPendingTransactionFilter = new EthNewPendingTransactionFilter(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethNewPendingTransactionFilter.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthUninstallFilterUnityRequest : UnityRpcClient<bool>
    {
        private readonly EthUninstallFilter _ethUninstallFilter;

        public EthUninstallFilterUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethUninstallFilter = new EthUninstallFilter(null);
        }

        public IEnumerator SendRequest(HexBigInteger filterId)
        {
            var request = _ethUninstallFilter.BuildRequest(filterId);
            return SendRequest(request);
        }
    }

    public class EthCompileLLLUnityRequest : UnityRpcClient<JObject>
    {
        private readonly EthCompileLLL _ethCompileLLL;

        public EthCompileLLLUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethCompileLLL = new EthCompileLLL(null);
        }

        public IEnumerator SendRequest(string lllcode)
        {
            var request = _ethCompileLLL.BuildRequest(lllcode);
            return SendRequest(request);
        }
    }

    public class EthCompileSerpentUnityRequest : UnityRpcClient<JObject>
    {
        private readonly EthCompileSerpent _ethCompileSerpent;

        public EthCompileSerpentUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethCompileSerpent = new EthCompileSerpent(null);
        }

        public IEnumerator SendRequest(string serpentCode)
        {
            var request = _ethCompileSerpent.BuildRequest(serpentCode);
            return SendRequest(request);
        }
    }

    public class EthCompileSolidityUnityRequest : UnityRpcClient<JToken>
    {
        private readonly EthCompileSolidity _ethCompileSolidity;

        public EthCompileSolidityUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethCompileSolidity = new EthCompileSolidity(null);
        }

        public IEnumerator SendRequest(string contractCode)
        {
            var request = _ethCompileSolidity.BuildRequest(contractCode);
            return SendRequest(request);
        }
    }

    public class EthGetCompilersUnityRequest : UnityRpcClient<string[]>
    {
        private readonly EthGetCompilers _ethGetCompilers;

        public EthGetCompilersUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetCompilers = new EthGetCompilers(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethGetCompilers.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthBlockNumberUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthBlockNumber _ethBlockNumber;

        public EthBlockNumberUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethBlockNumber = new EthBlockNumber(null);
        }

        public IEnumerator SendRequest()
        {
            var request = _ethBlockNumber.BuildRequest();
            return SendRequest(request);
        }
    }

    public class EthGetBlockWithTransactionsByHashUnityRequest : UnityRpcClient<BlockWithTransactions>
    {
        private readonly EthGetBlockWithTransactionsByHash _ethGetBlockWithTransactionsByHash;

        public EthGetBlockWithTransactionsByHashUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBlockWithTransactionsByHash = new EthGetBlockWithTransactionsByHash(null);
        }

        public IEnumerator SendRequest(string blockHash)
        {
            var request = _ethGetBlockWithTransactionsByHash.BuildRequest(blockHash);
            return SendRequest(request);
        }
    }

    public class EthGetBlockWithTransactionsHashesByHashUnityRequest : UnityRpcClient<BlockWithTransactionHashes>
    {
        private readonly EthGetBlockWithTransactionsHashesByHash _ethGetBlockWithTransactionsHashesByHash;

        public EthGetBlockWithTransactionsHashesByHashUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBlockWithTransactionsHashesByHash = new EthGetBlockWithTransactionsHashesByHash(null);
        }

        public IEnumerator SendRequest(string blockHash)
        {
            var request = _ethGetBlockWithTransactionsHashesByHash.BuildRequest(blockHash);
            return SendRequest(request);
        }
    }

    public class EthGetBlockWithTransactionsByNumberUnityRequest : UnityRpcClient<BlockWithTransactions>
    {
        private readonly EthGetBlockWithTransactionsByNumber _ethGetBlockWithTransactionsByNumber;

        public EthGetBlockWithTransactionsByNumberUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBlockWithTransactionsByNumber = new EthGetBlockWithTransactionsByNumber(null);
        }

        public IEnumerator SendRequest(HexBigInteger number)
        {
            var request = _ethGetBlockWithTransactionsByNumber.BuildRequest(number);
            return SendRequest(request);
        }
    }

    public class EthGetBlockTransactionCountByHashUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGetBlockTransactionCountByHash _ethGetBlockTransactionCountByHash;

        public EthGetBlockTransactionCountByHashUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash(null);
        }

        public IEnumerator SendRequest(string hash)
        {
            var request = _ethGetBlockTransactionCountByHash.BuildRequest(hash);
            return SendRequest(request);
        }
    }

    public class EthGetBlockTransactionCountByNumberUnityRequest : UnityRpcClient<HexBigInteger>
    {
        private readonly EthGetBlockTransactionCountByNumber _ethGetBlockTransactionCountByNumber;

        public EthGetBlockTransactionCountByNumberUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(null);
        }

        public IEnumerator SendRequest(BlockParameter block)
        {
            var request = _ethGetBlockTransactionCountByNumber.BuildRequest(block);
            return SendRequest(request);
        }
    }

    public class EthGetBlockWithTransactionsHashesByNumberUnityRequest : UnityRpcClient<BlockWithTransactionHashes>
    {
        private readonly EthGetBlockWithTransactionsHashesByNumber _ethGetBlockWithTransactionsHashesByNumber;

        public EthGetBlockWithTransactionsHashesByNumberUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings) : base(url, jsonSerializerSettings)
        {
            _ethGetBlockWithTransactionsHashesByNumber = new EthGetBlockWithTransactionsHashesByNumber(null);
        }

        public IEnumerator SendRequest(HexBigInteger number)
        {
            var request = _ethGetBlockWithTransactionsHashesByNumber.BuildRequest(number);
            return SendRequest(request);
        }
    }
}