// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

interface IRecoveryModule {
    enum RecoveryStatus {
        None,
        Pending,
        Ready,
        Executed,
        Cancelled
    }

    struct RecoveryRequest {
        address newOwner;
        uint64 executeAfter;
        uint32 approvalCount;
        RecoveryStatus status;
    }

    event RecoveryInitiated(
        address indexed account,
        address indexed newOwner,
        uint64 executeAfter,
        bytes32 indexed recoveryId
    );

    event RecoveryApproved(
        bytes32 indexed recoveryId,
        address indexed approver,
        uint32 approvalCount
    );

    event RecoveryExecuted(
        bytes32 indexed recoveryId,
        address indexed oldOwner,
        address indexed newOwner
    );

    event RecoveryCancelled(bytes32 indexed recoveryId);

    function initiateRecovery(
        address account,
        address newOwner
    ) external returns (bytes32 recoveryId);

    function approveRecovery(bytes32 recoveryId) external;

    function executeRecovery(bytes32 recoveryId) external;

    function cancelRecovery(bytes32 recoveryId) external;

    function getRecoveryRequest(
        bytes32 recoveryId
    ) external view returns (RecoveryRequest memory);

    function getRecoveryDelay() external view returns (uint256);

    function getRequiredApprovals(address account) external view returns (uint256);

    function isApprover(address account, address approver) external view returns (bool);
}
