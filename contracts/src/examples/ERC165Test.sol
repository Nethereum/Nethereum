// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/utils/introspection/ERC165.sol";
import "@openzeppelin/contracts/token/ERC721/IERC721.sol";
import "@openzeppelin/contracts/token/ERC1155/IERC1155.sol";

contract ERC165Test is ERC165 {
    // Supports ERC165 (0x01ffc9a7) by default via ERC165 base
    // We add a custom interface for testing
    bytes4 public constant CUSTOM_INTERFACE_ID = 0xdeadbeef;

    function supportsInterface(bytes4 interfaceId) public view override returns (bool) {
        return interfaceId == CUSTOM_INTERFACE_ID || super.supportsInterface(interfaceId);
    }
}
