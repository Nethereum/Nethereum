namespace Nethereum.Explorer.Services.Localization;

public static class ExplorerKeys
{
    public static class Search
    {
        public const string Placeholder = "Search.Placeholder";
        public const string GoToBlock = "Search.GoToBlock";
        public const string GoToAddress = "Search.GoToAddress";
        public const string TxOrBlockHash = "Search.TxOrBlockHash";
        public const string HexValue = "Search.HexValue";
        public const string SearchLabel = "Search.SearchLabel";
        public const string RecentSearches = "Search.RecentSearches";
    }

    public static class Shared
    {
        public const string Page = "Shared.Page";
        public const string PageOf = "Shared.PageOf";
        public const string NoData = "Shared.NoData";
        public const string Loading = "Shared.Loading";
        public const string CopiedToClipboard = "Shared.CopiedToClipboard";
        public const string CopyToClipboard = "Shared.CopyToClipboard";
        public const string FirstPage = "Shared.FirstPage";
        public const string LastPage = "Shared.LastPage";
        public const string ViewAll = "Shared.ViewAll";
        public const string Retry = "Shared.Retry";
        public const string Total = "Shared.Total";
        public const string Updated = "Shared.Updated";
        public const string ExportCsv = "Shared.ExportCsv";
        public const string ErrorLoadingData = "Shared.ErrorLoadingData";
        public const string All = "Shared.All";
        public const string Incoming = "Shared.Incoming";
        public const string Outgoing = "Shared.Outgoing";
        public const string Self = "Shared.Self";
    }

    public static class Table
    {
        public const string TxHash = "Table.TxHash";
        public const string Method = "Table.Method";
        public const string Block = "Table.Block";
        public const string Age = "Table.Age";
        public const string From = "Table.From";
        public const string To = "Table.To";
        public const string Value = "Table.Value";
        public const string TxFee = "Table.TxFee";
        public const string Depth = "Table.Depth";
        public const string Type = "Table.Type";
        public const string GasUsed = "Table.GasUsed";
        public const string ContractCreation = "Table.ContractCreation";
        public const string Revert = "Table.Revert";
        public const string CallData = "Table.CallData";
        public const string Input = "Table.Input";
        public const string Output = "Table.Output";
        public const string Name = "Table.Name";
        public const string Indexed = "Table.Indexed";
        public const string Number = "Table.Number";
        public const string Contract = "Table.Contract";
        public const string Balance = "Table.Balance";
        public const string Amount = "Table.Amount";
        public const string TokenId = "Table.TokenId";
        public const string AmountOrTokenId = "Table.AmountOrTokenId";
        public const string LastUpdated = "Table.LastUpdated";
        public const string Token = "Table.Token";
        public const string Txns = "Table.Txns";
        public const string GasLimit = "Table.GasLimit";
        public const string Validator = "Table.Validator";
        public const string Gas = "Table.Gas";
        public const string Status = "Table.Status";
        public const string Collection = "Table.Collection";
    }

    public static class Home
    {
        public const string Title = "Home.Title";
        public const string Dashboard = "Home.Dashboard";
        public const string LatestBlock = "Home.LatestBlock";
        public const string TotalTransactions = "Home.TotalTransactions";
        public const string ChainId = "Home.ChainId";
        public const string AddToWallet = "Home.AddToWallet";
        public const string LatestBlocks = "Home.LatestBlocks";
        public const string LatestTransactions = "Home.LatestTransactions";
        public const string NoBlocksYet = "Home.NoBlocksYet";
        public const string WaitingForBlocks = "Home.WaitingForBlocks";
        public const string NoTransactionsYet = "Home.NoTransactionsYet";
        public const string WaitingForTransactions = "Home.WaitingForTransactions";
    }

    public static class Blocks
    {
        public const string Title = "Blocks.Title";
        public const string NoBlocksIndexed = "Blocks.NoBlocksIndexed";
        public const string BlocksWillAppear = "Blocks.BlocksWillAppear";
    }

    public static class BlockDetail
    {
        public const string Title = "BlockDetail.Title";
        public const string LoadingBlock = "BlockDetail.LoadingBlock";
        public const string FailedToLoad = "BlockDetail.FailedToLoad";
        public const string NotFound = "BlockDetail.NotFound";
        public const string NotIndexedYet = "BlockDetail.NotIndexedYet";
        public const string Overview = "BlockDetail.Overview";
        public const string BlockNumber = "BlockDetail.BlockNumber";
        public const string Timestamp = "BlockDetail.Timestamp";
        public const string Transactions = "BlockDetail.Transactions";
        public const string TransactionsInBlock = "BlockDetail.TransactionsInBlock";
        public const string ValidatedBy = "BlockDetail.ValidatedBy";
        public const string GasAndSize = "BlockDetail.GasAndSize";
        public const string Size = "BlockDetail.Size";
        public const string BaseFeePerGas = "BlockDetail.BaseFeePerGas";
        public const string BurnedFees = "BlockDetail.BurnedFees";
        public const string BlockReward = "BlockDetail.BlockReward";
        public const string PoADevChain = "BlockDetail.PoADevChain";
        public const string StaticReward = "BlockDetail.StaticReward";
        public const string Difficulty = "BlockDetail.Difficulty";
        public const string HashesAndExtra = "BlockDetail.HashesAndExtra";
        public const string Hash = "BlockDetail.Hash";
        public const string ParentHash = "BlockDetail.ParentHash";
        public const string Nonce = "BlockDetail.Nonce";
        public const string ExtraData = "BlockDetail.ExtraData";
        public const string StateAndReceipts = "BlockDetail.StateAndReceipts";
        public const string StateRoot = "BlockDetail.StateRoot";
        public const string ReceiptsRoot = "BlockDetail.ReceiptsRoot";
        public const string WithdrawalsRoot = "BlockDetail.WithdrawalsRoot";
        public const string LogsBloom = "BlockDetail.LogsBloom";
        public const string NoTransactionsInBlock = "BlockDetail.NoTransactionsInBlock";
        public const string RawBlockData = "BlockDetail.RawBlockData";
        public const string Details = "BlockDetail.Details";
        public const string Prev = "BlockDetail.Prev";
        public const string Next = "BlockDetail.Next";
        public const string Bytes = "BlockDetail.Bytes";
        public const string ErrorOccurred = "BlockDetail.ErrorOccurred";
        public const string Create = "BlockDetail.Create";
    }

    public static class Transactions
    {
        public const string Title = "Transactions.Title";
        public const string NoTransactionsIndexed = "Transactions.NoTransactionsIndexed";
        public const string TransactionsWillAppear = "Transactions.TransactionsWillAppear";
    }

    public static class TransactionDetail
    {
        public const string Title = "TransactionDetail.Title";
        public const string LoadingTransaction = "TransactionDetail.LoadingTransaction";
        public const string FailedToLoad = "TransactionDetail.FailedToLoad";
        public const string NotFound = "TransactionDetail.NotFound";
        public const string TransactionDetails = "TransactionDetail.TransactionDetails";
        public const string Success = "TransactionDetail.Success";
        public const string Failed = "TransactionDetail.Failed";
        public const string TransactionReverted = "TransactionDetail.TransactionReverted";
        public const string RevertReason = "TransactionDetail.RevertReason";
        public const string Error = "TransactionDetail.Error";
        public const string RawTransactionData = "TransactionDetail.RawTransactionData";
        public const string Overview = "TransactionDetail.Overview";
        public const string TransactionHash = "TransactionDetail.TransactionHash";
        public const string Timestamp = "TransactionDetail.Timestamp";
        public const string TransactionFee = "TransactionDetail.TransactionFee";
        public const string GasAndFees = "TransactionDetail.GasAndFees";
        public const string CumulativeGas = "TransactionDetail.CumulativeGas";
        public const string GasPrice = "TransactionDetail.GasPrice";
        public const string MaxFeePerGas = "TransactionDetail.MaxFeePerGas";
        public const string MaxPriorityFee = "TransactionDetail.MaxPriorityFee";
        public const string OtherAttributes = "TransactionDetail.OtherAttributes";
        public const string TransactionType = "TransactionDetail.TransactionType";
        public const string PositionInBlock = "TransactionDetail.PositionInBlock";
        public const string BlockHash = "TransactionDetail.BlockHash";
        public const string ReceiptHash = "TransactionDetail.ReceiptHash";
        public const string TokenTransfers = "TransactionDetail.TokenTransfers";
        public const string InternalTransactions = "TransactionDetail.InternalTransactions";
        public const string BlobData = "TransactionDetail.BlobData";
        public const string MaxFeePerBlobGas = "TransactionDetail.MaxFeePerBlobGas";
        public const string BlobCount = "TransactionDetail.BlobCount";
        public const string BlobVersionedHashes = "TransactionDetail.BlobVersionedHashes";
        public const string AuthorizationList = "TransactionDetail.AuthorizationList";
        public const string AuthorizedAddress = "TransactionDetail.AuthorizedAddress";
        public const string Signature = "TransactionDetail.Signature";
        public const string StateChanges = "TransactionDetail.StateChanges";
        public const string Accounts = "TransactionDetail.Accounts";
        public const string ViaPrestateTracer = "TransactionDetail.ViaPrestateTracer";
        public const string CodeChanged = "TransactionDetail.CodeChanged";
        public const string StorageSlots = "TransactionDetail.StorageSlots";
        public const string New = "TransactionDetail.New";
        public const string Deleted = "TransactionDetail.Deleted";
        public const string AndMore = "TransactionDetail.AndMore";
        public const string LoadingStateDiff = "TransactionDetail.LoadingStateDiff";
        public const string StateDiffUnavailable = "TransactionDetail.StateDiffUnavailable";
        public const string LoadStateChanges = "TransactionDetail.LoadStateChanges";
        public const string SimulatesTransaction = "TransactionDetail.SimulatesTransaction";
        public const string InputData = "TransactionDetail.InputData";
        public const string DecodedParameters = "TransactionDetail.DecodedParameters";
        public const string RawInput = "TransactionDetail.RawInput";
        public const string EventLogs = "TransactionDetail.EventLogs";
        public const string ErrorOccurred = "TransactionDetail.ErrorOccurred";
    }

    public static class Account
    {
        public const string Title = "Account.Title";
        public const string ContractTitle = "Account.ContractTitle";
        public const string Overview = "Account.Overview";
        public const string Address = "Account.Address";
        public const string Nonce = "Account.Nonce";
        public const string Actions = "Account.Actions";
        public const string InteractWithContract = "Account.InteractWithContract";
        public const string ViewAsContract = "Account.ViewAsContract";
        public const string TokenBalances = "Account.TokenBalances";
        public const string TokenHoldings = "Account.TokenHoldings";
        public const string LoadingTokens = "Account.LoadingTokens";
        public const string NftInventory = "Account.NftInventory";
        public const string NftHoldings = "Account.NftHoldings";
        public const string LoadingNfts = "Account.LoadingNfts";
        public const string NoTransactionsFound = "Account.NoTransactionsFound";
        public const string NoFilteredTx = "Account.NoFilteredTx";
        public const string NoAddressTx = "Account.NoAddressTx";
        public const string NoInternalTransactions = "Account.NoInternalTransactions";
        public const string NoFilteredItx = "Account.NoFilteredItx";
        public const string NoAddressItx = "Account.NoAddressItx";
        public const string LoadingTransactions = "Account.LoadingTransactions";
        public const string LoadingInternalTx = "Account.LoadingInternalTx";
        public const string Unknown = "Account.Unknown";
    }

    public static class AccountTokens
    {
        public const string Title = "AccountTokens.Title";
        public const string Balances = "AccountTokens.Balances";
        public const string Nfts = "AccountTokens.Nfts";
        public const string Transfers = "AccountTokens.Transfers";
        public const string TokenIndexingNotAvailable = "AccountTokens.TokenIndexingNotAvailable";
        public const string TokenIndexingSubtitle = "AccountTokens.TokenIndexingSubtitle";
        public const string LoadingBalances = "AccountTokens.LoadingBalances";
        public const string NoTokenBalances = "AccountTokens.NoTokenBalances";
        public const string NoTokenBalancesSubtitle = "AccountTokens.NoTokenBalancesSubtitle";
        public const string LoadingNfts = "AccountTokens.LoadingNfts";
        public const string NoNfts = "AccountTokens.NoNfts";
        public const string NoNftsSubtitle = "AccountTokens.NoNftsSubtitle";
        public const string LoadingTransfers = "AccountTokens.LoadingTransfers";
        public const string NoTransfers = "AccountTokens.NoTransfers";
        public const string NoTransfersSubtitle = "AccountTokens.NoTransfersSubtitle";
        public const string BlockPrefix = "AccountTokens.BlockPrefix";
    }

    public static class Contract
    {
        public const string Title = "Contract.Title";
        public const string AbiStatus = "Contract.AbiStatus";
        public const string AbiAvailable = "Contract.AbiAvailable";
        public const string FunctionsEvents = "Contract.FunctionsEvents";
        public const string NoAbi = "Contract.NoAbi";
        public const string ProxyContract = "Contract.ProxyContract";
        public const string Implementation = "Contract.Implementation";
        public const string NotConnected = "Contract.NotConnected";
        public const string ConnectWallet = "Contract.ConnectWallet";
        public const string AbiJson = "Contract.AbiJson";
        public const string Bytecode = "Contract.Bytecode";
        public const string BytesLabel = "Contract.BytesLabel";
        public const string Collapse = "Contract.Collapse";
        public const string Expand = "Contract.Expand";
        public const string PasteAbiJson = "Contract.PasteAbiJson";
        public const string StoreAbi = "Contract.StoreAbi";
        public const string InvalidAbiJson = "Contract.InvalidAbiJson";
        public const string FailedToParseAbi = "Contract.FailedToParseAbi";
        public const string Query = "Contract.Query";
        public const string Transact = "Contract.Transact";
        public const string Events = "Contract.Events";
        public const string SourceCode = "Contract.SourceCode";
        public const string EventSignatures = "Contract.EventSignatures";
        public const string RecentEventLogs = "Contract.RecentEventLogs";
        public const string LoadEvents = "Contract.LoadEvents";
        public const string ClickLoadEvents = "Contract.ClickLoadEvents";
        public const string NoEventsFound = "Contract.NoEventsFound";
        public const string NoEventsSubtitle = "Contract.NoEventsSubtitle";
        public const string ContractTransactions = "Contract.ContractTransactions";
        public const string NoTransactionsFound = "Contract.NoTransactionsFound";
        public const string NoContractTxSubtitle = "Contract.NoContractTxSubtitle";
        public const string ConnectAWallet = "Contract.ConnectAWallet";
        public const string ConnectWalletMessage = "Contract.ConnectWalletMessage";
        public const string ChainAddedSwitched = "Contract.ChainAddedSwitched";
        public const string FailedAddChain = "Contract.FailedAddChain";
        public const string Log = "Contract.Log";
        public const string Index = "Contract.Index";
        public const string Topic = "Contract.Topic";
        public const string ContractName = "Contract.ContractName";
        public const string Proxy = "Contract.Proxy";
        public const string AddToWallet = "Contract.AddToWallet";
        public const string Wallet = "Contract.Wallet";
        public const string LoadingContract = "Contract.LoadingContract";
        public const string LoadingTransactions = "Contract.LoadingTransactions";
    }

    public static class MudWorlds
    {
        public const string Title = "MudWorlds.Title";
        public const string LoadingWorlds = "MudWorlds.LoadingWorlds";
        public const string NoWorldsFound = "MudWorlds.NoWorldsFound";
        public const string NoWorldsSubtitle = "MudWorlds.NoWorldsSubtitle";
        public const string WorldContract = "MudWorlds.WorldContract";
        public const string Tables = "MudWorlds.Tables";
        public const string Records = "MudWorlds.Records";
        public const string BrowseTables = "MudWorlds.BrowseTables";
    }

    public static class MudTables
    {
        public const string Title = "MudTables.Title";
        public const string World = "MudTables.World";
        public const string SelectWorldAddress = "MudTables.SelectWorldAddress";
        public const string SelectAWorld = "MudTables.SelectAWorld";
        public const string LoadingTables = "MudTables.LoadingTables";
        public const string NoTablesFound = "MudTables.NoTablesFound";
        public const string NoTablesSubtitle = "MudTables.NoTablesSubtitle";
        public const string KeyFields = "MudTables.KeyFields";
        public const string ValueFields = "MudTables.ValueFields";
        public const string BrowseRecords = "MudTables.BrowseRecords";
        public const string Unnamed = "MudTables.Unnamed";
    }

    public static class MudRecords
    {
        public const string Title = "MudRecords.Title";
        public const string WorldAddress = "MudRecords.WorldAddress";
        public const string TableLabel = "MudRecords.TableLabel";
        public const string AllTables = "MudRecords.AllTables";
        public const string RecordsCount = "MudRecords.RecordsCount";
        public const string LoadingRecords = "MudRecords.LoadingRecords";
        public const string QueryFilters = "MudRecords.QueryFilters";
        public const string Clear = "MudRecords.Clear";
        public const string AddFilter = "MudRecords.AddFilter";
        public const string FieldPlaceholder = "MudRecords.FieldPlaceholder";
        public const string ValuePlaceholder = "MudRecords.ValuePlaceholder";
        public const string OrderBy = "MudRecords.OrderBy";
        public const string QueryButton = "MudRecords.QueryButton";
        public const string NoResults = "MudRecords.NoResults";
        public const string NoResultsSubtitle = "MudRecords.NoResultsSubtitle";
        public const string StaticData = "MudRecords.StaticData";
        public const string DynamicData = "MudRecords.DynamicData";
        public const string Key = "MudRecords.Key";
        public const string Active = "MudRecords.Active";
        public const string NoRecords = "MudRecords.NoRecords";
        public const string NoRecordsSubtitle = "MudRecords.NoRecordsSubtitle";
        public const string SelectAWorld = "MudRecords.SelectAWorld";
        public const string SelectAWorldSubtitle = "MudRecords.SelectAWorldSubtitle";
        public const string FailedToLoadRecords = "MudRecords.FailedToLoadRecords";
        public const string QueryFailed = "MudRecords.QueryFailed";
        public const string KeyField = "MudRecords.KeyField";
    }

    public static class Errors
    {
        public const string PageNotFound = "Errors.PageNotFound";
        public const string PageNotFoundMessage = "Errors.PageNotFoundMessage";
        public const string BackToDashboard = "Errors.BackToDashboard";
        public const string SomethingWentWrong = "Errors.SomethingWentWrong";
        public const string UnexpectedError = "Errors.UnexpectedError";
        public const string ErrorDetails = "Errors.ErrorDetails";
        public const string UnhandledError = "Errors.UnhandledError";
        public const string Reload = "Errors.Reload";
    }

    public static class ContractList
    {
        public const string Title = "ContractList.Title";
        public const string NoContractsIndexed = "ContractList.NoContractsIndexed";
        public const string ContractsWillAppear = "ContractList.ContractsWillAppear";
        public const string Creator = "ContractList.Creator";
        public const string CreationTx = "ContractList.CreationTx";
        public const string IndexedAt = "ContractList.IndexedAt";
    }

    public static class AccountList
    {
        public const string Title = "AccountList.Title";
        public const string NoAccountsIndexed = "AccountList.NoAccountsIndexed";
        public const string AccountsWillAppear = "AccountList.AccountsWillAppear";
        public const string EOA = "AccountList.EOA";
    }

    public static class Nav
    {
        public const string Dashboard = "Nav.Dashboard";
        public const string Blocks = "Nav.Blocks";
        public const string Txns = "Nav.Txns";
        public const string Contracts = "Nav.Contracts";
        public const string AccountsNav = "Nav.Accounts";
        public const string Worlds = "Nav.Worlds";
        public const string Tables = "Nav.Tables";
        public const string Records = "Nav.Records";
        public const string English = "Nav.English";
        public const string Spanish = "Nav.Spanish";
        public const string ExplorerTitle = "Nav.ExplorerTitle";
        public const string PoweredBy = "Nav.PoweredBy";
    }

    public static class EventLog
    {
        public const string LogNumber = "EventLog.LogNumber";
        public const string DecodedParameters = "EventLog.DecodedParameters";
    }

    public static class DecodedParams
    {
        public const string Unknown = "DecodedParams.Unknown";
    }

    public static class Debugger
    {
        public const string Title = "Debugger.Title";
        public const string Debug = "Debugger.Debug";
        public const string ReplayingTransaction = "Debugger.ReplayingTransaction";
        public const string ReplayingSubtitle = "Debugger.ReplayingSubtitle";
        public const string ReplayFailed = "Debugger.ReplayFailed";
        public const string BackToTransaction = "Debugger.BackToTransaction";
        public const string Reverted = "Debugger.Reverted";
        public const string RenderingError = "Debugger.RenderingError";
    }

    public static class PendingTx
    {
        public const string Title = "PendingTx.Title";
        public const string PendingTab = "PendingTx.PendingTab";
        public const string QueuedTab = "PendingTx.QueuedTab";
        public const string NoPending = "PendingTx.NoPending";
        public const string NoPendingSubtitle = "PendingTx.NoPendingSubtitle";
        public const string NoQueued = "PendingTx.NoQueued";
        public const string NotSupported = "PendingTx.NotSupported";
        public const string NotSupportedSubtitle = "PendingTx.NotSupportedSubtitle";
        public const string LoadingPending = "PendingTx.LoadingPending";
        public const string Refresh = "PendingTx.Refresh";
    }
}
