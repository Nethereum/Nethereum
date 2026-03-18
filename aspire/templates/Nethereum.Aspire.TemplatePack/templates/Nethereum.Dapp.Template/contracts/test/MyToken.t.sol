// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

import "forge-std/Test.sol";
import "../src/MyToken.sol";

contract MyTokenTest is Test {
    MyToken public token;
    address public deployer;
    address public alice;
    address public bob;

    function setUp() public {
        deployer = address(this);
        alice = makeAddr("alice");
        bob = makeAddr("bob");

        token = new MyToken("MyToken", "MTK", 1_000_000 ether);
    }

    function test_InitialSupply() public view {
        assertEq(token.totalSupply(), 1_000_000 ether);
        assertEq(token.balanceOf(deployer), 1_000_000 ether);
    }

    function test_Name() public view {
        assertEq(token.name(), "MyToken");
    }

    function test_Symbol() public view {
        assertEq(token.symbol(), "MTK");
    }

    function test_Mint() public {
        token.mint(alice, 500 ether);
        assertEq(token.balanceOf(alice), 500 ether);
    }

    function test_Transfer() public {
        token.transfer(alice, 100 ether);
        assertEq(token.balanceOf(alice), 100 ether);
        assertEq(token.balanceOf(deployer), 1_000_000 ether - 100 ether);
    }

    function test_MintAndTransfer() public {
        token.mint(alice, 200 ether);

        vm.prank(alice);
        token.transfer(bob, 50 ether);

        assertEq(token.balanceOf(alice), 150 ether);
        assertEq(token.balanceOf(bob), 50 ether);
    }
}
