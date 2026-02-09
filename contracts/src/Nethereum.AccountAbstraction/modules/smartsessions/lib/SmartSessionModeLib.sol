// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "../DataTypes.sol";

library SmartSessionModeLib {
    function isUseMode(SmartSessionMode mode) internal pure returns (bool) {
        return mode == SmartSessionMode.USE;
    }

    function isEnableMode(SmartSessionMode mode) internal pure returns (bool) {
        return (mode == SmartSessionMode.ENABLE || mode == SmartSessionMode.UNSAFE_ENABLE);
    }

    function useRegistry(SmartSessionMode mode) internal pure returns (bool) {
        return (mode == SmartSessionMode.ENABLE);
    }
}
