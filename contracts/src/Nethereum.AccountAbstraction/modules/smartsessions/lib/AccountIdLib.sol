// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { strings } from "stringutils/strings.sol";

library AccountIdLib {
    using strings for *;

    // id follows "vendorname.accountname.semver" structure as per ERC-7579
    // use this for parsing the full account name and version as strings
    function parseAccountId(string memory id) internal pure returns (string memory name, string memory version) {
        strings.slice memory id = id.toSlice();
        strings.slice memory delim = ".".toSlice();
        name = string(abi.encodePacked(id.split(delim).toString(), " ", id.split(delim).toString()));
        version = id.toString();
    }
}
