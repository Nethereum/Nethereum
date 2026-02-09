// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "../DataTypes.sol";
import { LibZip } from "solady/utils/LibZip.sol";
import { PackedUserOperation } from "modulekit/external/ERC4337.sol";
import { SmartSessionModeLib } from "./SmartSessionModeLib.sol";

library EncodeLib {
    using LibZip for bytes;
    using EncodeLib for *;
    using SmartSessionModeLib for SmartSessionMode;

    error ChainIdAndHashesLengthMismatch(uint256 chainIdsLength, uint256 hashesLength);

    function getSender(PackedUserOperation calldata userOp) internal pure returns (address) {
        address data;
        //read sender from userOp, which is first userOp member (saves 800 gas...)
        assembly {
            data := calldataload(userOp)
        }
        return address(uint160(data));
    }

    function unpackMode(bytes calldata packed)
        internal
        pure
        returns (SmartSessionMode mode, PermissionId permissionId, bytes calldata data)
    {
        mode = SmartSessionMode(uint8(bytes1(packed[:1])));
        if (mode.isEnableMode()) {
            data = packed[1:];
        } else {
            permissionId = PermissionId.wrap(bytes32(packed[1:33]));
            data = packed[33:];
        }
    }

    function encodeUse(PermissionId permissionId, bytes memory sig) internal pure returns (bytes memory userOpSig) {
        userOpSig = abi.encodePacked(SmartSessionMode.USE, permissionId, sig);
    }

    function encodeUnsafeEnable(
        bytes memory sig,
        EnableSession memory enableData
    )
        internal
        pure
        returns (bytes memory packedSig)
    {
        packedSig = abi.encodePacked(SmartSessionMode.UNSAFE_ENABLE, abi.encode(enableData, sig).flzCompress());
    }

    function decodeEnable(bytes calldata packedSig)
        internal
        pure
        returns (EnableSession memory enableData, bytes memory signature)
    {
        (enableData, signature) = abi.decode(packedSig.flzDecompress(), (EnableSession, bytes));
    }
}
