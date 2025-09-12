using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public partial class AccountGroupViewModel : ObservableObject
    {
        public AccountGroupViewModel(string? groupId, string groupName, string groupType, IEnumerable<IWalletAccount> accounts)
        {
            GroupId = groupId;
            GroupName = groupName;
            GroupType = groupType;
            Accounts = new ObservableCollection<IWalletAccount>(accounts.OrderBy(a => a.Name));
        }
        public string? GroupId { get; }
        public string GroupName { get; }
        public string GroupType { get; }
        public ObservableCollection<IWalletAccount> Accounts { get; }
        public int AccountCount => Accounts.Count;
        [ObservableProperty]
        private bool _isExpanded = true;
        public bool IsStandalone => string.IsNullOrEmpty(GroupId);
        public bool CanNavigateToDetails => !IsStandalone && !IsTypeBasedGroup;
        public bool IsTypeBasedGroup => GroupId?.StartsWith("type:") == true;
    }
}