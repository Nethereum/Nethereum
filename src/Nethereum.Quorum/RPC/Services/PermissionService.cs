using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.Permission;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class PermissionService : RpcClientWrapper, IPermissionService
    {
        public PermissionService(IClient client) : base(client)
        {
            AcctList = new QuorumPermissionAcctList(client);
            AddAccountToOrg = new QuorumPermissionAddAccountToOrg(client);
            AddNewRole = new QuorumPermissionAddNewRole(client);
            AddNode = new QuorumPermissionAddNode(client);
            AddOrg = new QuorumPermissionAddOrg(client);
            AddSubOrg = new QuorumPermissionAddSubOrg(client);
            ApproveAdminRole = new QuorumPermissionApproveAdminRole(client);
            ApproveBlackListedAccountRecovery = new QuorumPermissionApproveBlackListedAccountRecovery(client);
            ApproveBlackListedNodeRecovery = new QuorumPermissionApproveBlackListedNodeRecovery(client);
            ApproveOrg = new QuorumPermissionApproveOrg(client);
            ApproveOrgStatus = new QuorumPermissionApproveOrgStatus(client);
            AssignAdminRole = new QuorumPermissionAssignAdminRole(client);
            ChangeAccountRole = new QuorumPermissionChangeAccountRole(client);
            ConnectionAllowed = new QuorumPermissionConnectionAllowed(client);
            GetOrgDetails = new QuorumPermissionGetOrgDetails(client);
            NodeList = new QuorumPermissionNodeList(client);
            OrgList = new QuorumPermissionOrgList(client);
            RecoverBlackListedAccount = new QuorumPermissionRecoverBlackListedAccount(client);
            RecoverBlackListedNode = new QuorumPermissionRecoverBlackListedNode(client);
            RemoveRole = new QuorumPermissionRemoveRole(client);
            RoleList = new QuorumPermissionRoleList(client);
            TransactionAllowed = new QuorumPermissionTransactionAllowed(client);
            UpdateAccountStatus = new QuorumPermissionUpdateAccountStatus(client);
            UpdateNodeStatus = new QuorumPermissionUpdateNodeStatus(client);
            UpdateOrgStatus = new QuorumPermissionUpdateOrgStatus(client);
        }

        public IQuorumPermissionAcctList AcctList { get; }
        public IQuorumPermissionAddAccountToOrg AddAccountToOrg { get; }
        public IQuorumPermissionAddNewRole AddNewRole { get; }
        public IQuorumPermissionAddNode AddNode { get; }
        public IQuorumPermissionAddOrg AddOrg { get; }
        public IQuorumPermissionAddSubOrg AddSubOrg { get; }
        public IQuorumPermissionApproveAdminRole ApproveAdminRole { get; }
        public IQuorumPermissionApproveBlackListedAccountRecovery   ApproveBlackListedAccountRecovery { get; }
        public IQuorumPermissionApproveBlackListedNodeRecovery  ApproveBlackListedNodeRecovery { get; }
        public IQuorumPermissionApproveOrg ApproveOrg { get; }
        public IQuorumPermissionApproveOrgStatus ApproveOrgStatus { get; }
        public IQuorumPermissionAssignAdminRole AssignAdminRole { get; }
        public IQuorumPermissionChangeAccountRole ChangeAccountRole { get; }
        public IQuorumPermissionConnectionAllowed ConnectionAllowed { get; }
        public IQuorumPermissionGetOrgDetails GetOrgDetails { get; }
        public IQuorumPermissionNodeList   NodeList { get; }
        public IQuorumPermissionOrgList OrgList { get; }
        public IQuorumPermissionRecoverBlackListedAccount RecoverBlackListedAccount { get; }
        public IQuorumPermissionRecoverBlackListedNode RecoverBlackListedNode { get; }
        public IQuorumPermissionRemoveRole RemoveRole { get; }
        public IQuorumPermissionRoleList RoleList { get; }
        public IQuorumPermissionTransactionAllowed TransactionAllowed { get; }
        public IQuorumPermissionUpdateAccountStatus UpdateAccountStatus { get; }
        public IQuorumPermissionUpdateNodeStatus UpdateNodeStatus { get; }
        public IQuorumPermissionUpdateOrgStatus UpdateOrgStatus { get; }

    }
}