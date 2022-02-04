using Nethereum.Quorum.RPC.Permission;

namespace Nethereum.Quorum.RPC.Services
{
    public interface IPermissionService
    {
        IQuorumPermissionAcctList AcctList { get; }
        IQuorumPermissionAddAccountToOrg AddAccountToOrg { get; }
        IQuorumPermissionAddNewRole AddNewRole { get; }
        IQuorumPermissionAddNode AddNode { get; }
        IQuorumPermissionAddOrg AddOrg { get; }
        IQuorumPermissionAddSubOrg AddSubOrg { get; }
        IQuorumPermissionApproveAdminRole ApproveAdminRole { get; }
        IQuorumPermissionApproveBlackListedAccountRecovery ApproveBlackListedAccountRecovery { get; }
        IQuorumPermissionApproveBlackListedNodeRecovery ApproveBlackListedNodeRecovery { get; }
        IQuorumPermissionApproveOrg ApproveOrg { get; }
        IQuorumPermissionApproveOrgStatus ApproveOrgStatus { get; }
        IQuorumPermissionAssignAdminRole AssignAdminRole { get; }
        IQuorumPermissionChangeAccountRole ChangeAccountRole { get; }
        IQuorumPermissionConnectionAllowed ConnectionAllowed { get; }
        IQuorumPermissionGetOrgDetails GetOrgDetails { get; }
        IQuorumPermissionNodeList NodeList { get; }
        IQuorumPermissionOrgList OrgList { get; }
        IQuorumPermissionRecoverBlackListedAccount RecoverBlackListedAccount { get; }
        IQuorumPermissionRecoverBlackListedNode RecoverBlackListedNode { get; }
        IQuorumPermissionRemoveRole RemoveRole { get; }
        IQuorumPermissionRoleList RoleList { get; }
        IQuorumPermissionTransactionAllowed TransactionAllowed { get; }
        IQuorumPermissionUpdateAccountStatus UpdateAccountStatus { get; }
        IQuorumPermissionUpdateNodeStatus UpdateNodeStatus { get; }
        IQuorumPermissionUpdateOrgStatus UpdateOrgStatus { get; }
    }
}