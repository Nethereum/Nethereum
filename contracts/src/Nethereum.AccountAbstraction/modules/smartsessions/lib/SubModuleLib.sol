// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "../DataTypes.sol";

library SubModuleLib {
    error DataTooShort(uint256 length);

    function parseInstallData(bytes calldata data) internal pure returns (ConfigId, bytes calldata) {
        if (data.length < 32) revert DataTooShort(data.length);
        return (ConfigId.wrap(bytes32(data[0:32])), data[52:]);
    }
}
