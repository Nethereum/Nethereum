using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Wallet.Services.Network
{
    public enum RpcSelectionMode
    {
        Single,
        RandomMultiple,
        LoadBalanced
    }
    public class RpcSelectionConfiguration
    {
        public BigInteger ChainId { get; set; }
        public RpcSelectionMode Mode { get; set; } = RpcSelectionMode.Single;
        public List<string> SelectedRpcUrls { get; set; } = new List<string>();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public bool IsValid()
        {
            if (SelectedRpcUrls == null || SelectedRpcUrls.Count == 0)
                return false;
                
            if (Mode == RpcSelectionMode.Single && SelectedRpcUrls.Count != 1)
                return false;
                
            if ((Mode == RpcSelectionMode.RandomMultiple || Mode == RpcSelectionMode.LoadBalanced) 
                && SelectedRpcUrls.Count < 1)
                return false;
                
            return true;
        }
    }
}