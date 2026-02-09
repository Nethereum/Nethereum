// SPDX-License-Identifier: LGPL-3.0-only
pragma solidity ^0.8.23;

import { IModule } from "erc7579/interfaces/IERC7579Module.sol";

/**
 * ISessionValidator is a contract that validates signatures for a given session.
 * this interface expects to validate the signature in a stateless way.
 * all parameters required to validate the signature are passed in the function call.
 * Only one ISessionValidator is responsible to validate a userOp.
 * if you want to use multiple validators, you can create a ISessionValidator that aggregates multiple signatures that
 * are packed into userOp.signature
 * It is used to validate the signature of a session.
 *  hash The userOp hash
 *  sig The signature of userOp
 *  data the config data that is used to validate the signature
 */
interface ISessionValidator is IModule {
    function validateSignatureWithData(
        bytes32 hash,
        bytes calldata sig,
        bytes calldata data
    )
        external
        view
        returns (bool validSig);
}
