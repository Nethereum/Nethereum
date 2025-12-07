using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.HostWallet;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.UI.Components.Services
{
    public interface IPromptQueueService
    {
        event EventHandler<PromptQueueChangedEventArgs>? QueueChanged;
        
        IReadOnlyList<PromptRequest> PendingPrompts { get; }
        int PendingCount { get; }
        bool HasPendingPrompts { get; }
        
        Task<string> EnqueueTransactionPromptAsync(TransactionPromptInfo promptInfo);
        Task<string> EnqueueSignaturePromptAsync(SignaturePromptInfo promptInfo);
        Task<string> EnqueueTypedDataPromptAsync(SignaturePromptInfo promptInfo);
        Task<string> EnqueuePermissionPromptAsync(DappPermissionPromptInfo promptInfo);
        Task<string> EnqueueChainAdditionPromptAsync(ChainAdditionPromptInfo promptInfo);
        Task<string> EnqueueNetworkSwitchPromptAsync(ChainSwitchPromptInfo promptInfo);
        PromptRequest? GetNextPrompt();
        PromptRequest? GetPromptById(string id);
        Task CompletePromptAsync(string promptId, object? result);
        Task RejectPromptAsync(string promptId, string? reason = null, Exception? exception = null);
        Task RejectAllAsync();
        void ClearQueue();
    }
    
    public class PromptRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public PromptType Type { get; set; }
        public object Data { get; set; } = new object();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Origin { get; set; }
        public string? DAppName { get; set; }
        public string? DAppIcon { get; set; }
        public TaskCompletionSource<object?> CompletionSource { get; set; } = new();
        public PromptStatus Status { get; set; } = PromptStatus.Pending;
    }
    
    public enum PromptType
    {
        Transaction,
        Signature,
        Permission,
        AccountSelection,
        NetworkSwitch,
        ChainAddition
    }
    
    public enum PromptStatus
    {
        Pending,
        InProgress,
        Completed,
        Rejected,
        TimedOut
    }
    
    public class PromptQueueChangedEventArgs : EventArgs
    {
        public PromptQueueChangeType ChangeType { get; set; }
        public PromptRequest? Prompt { get; set; }
        public int NewCount { get; set; }
    }
    
    public enum PromptQueueChangeType
    {
        Added,
        Removed,
        Completed,
        Rejected
    }
    
    public class PromptInfo
    {
       
        public string? Origin { get; set; }
        public string? DAppName { get; set; }
        public string? DAppIcon { get; set; }

        public string? WarningMessage { get; set; }
    }

    public class TransactionPromptInfo:PromptInfo
    {
        public TransactionInput TransactionInput { get; set; } = new TransactionInput();
    }
    
    public class SignaturePromptInfo : PromptInfo
    {
        public string Method { get; set; } = string.Empty;
        public string RawMessage { get; set; } = string.Empty;
        public string? DecodedMessage { get; set; }
        public bool IsMessageHex { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? DomainName { get; set; }
        public string? DomainVersion { get; set; }
        public string? VerifyingContract { get; set; }
        public string? PrimaryType { get; set; }
        public string? ChainId { get; set; }
    }

    public class DappPermissionPromptInfo : PromptInfo
    {
        public string AccountAddress { get; set; } = string.Empty;
    }

    public class ChainAdditionPromptInfo : PromptInfo
    {
        public AddEthereumChainParameter Parameter { get; set; } = new();
        public ChainFeature ChainFeature { get; set; } = new();
        public bool SwitchAfterAdd { get; set; } = true;
    }

    public class ChainSwitchPromptInfo : PromptInfo
    {
        public BigInteger ChainId { get; set; }
        public ChainFeature? TargetChain { get; set; }
        public bool IsKnown { get; set; }
        public bool AllowAdd { get; set; } = true;
        public long? CurrentChainId { get; set; }
        public ChainFeature? CurrentChain { get; set; }
    }

    public class TypedDataSigningInfo: PromptInfo
    {
        public string TypedData { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public string DomainVersion { get; set; } = string.Empty;
        public string VerifyingContract { get; set; } = string.Empty;
        public string ParsedMessage { get; set; } = string.Empty;
        public string NetworkName { get; set; } = string.Empty;
        public bool IsHighRiskSigning { get; set; }

    }
    public class PersonalSigningInfo: PromptInfo
    {
        public string Message { get; set; } = string.Empty;
        public bool IsHighRiskSigning { get; set; }
    }

}
