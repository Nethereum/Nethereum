namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.MainnetFork
{
    /// <summary>
    /// A minimal accept-all ERC-4337 paymaster used to prove the client packs a paymaster-sponsored
    /// UserOperation correctly against the canonical v0.8 EntryPoint over the reference bundler.
    ///
    /// Why not the bundler's own TestPaymasterAcceptAll? The account-abstraction submodule and the
    /// @account-abstraction/contracts npm package shipped with the bundler are compiled for v0.9,
    /// whose BasePaymaster constructor runs an ERC-165 self-check:
    /// <c>require(IERC165(entryPoint).supportsInterface(type(IEntryPoint).interfaceId))</c>. The
    /// canonical on-chain EntryPoint at 0x4337084D... is v0.8 and returns false for the v0.9
    /// interfaceId (0x283f5489), so deploying that paymaster reverts with
    /// <c>ERC165Error(0x4337084D..., 0x283f5489)</c>. The SimpleAccount/SimpleAccountFactory stack
    /// has no such guard, which is why the account-only interop tests pass with the same v0.9
    /// artifacts. This paymaster omits the version self-check, so it deploys against v0.8 while the
    /// only thing under proof - the client's paymasterAndData packing (address + paymaster gas
    /// limits) - is exercised unchanged.
    ///
    /// The paymaster is accept-all: validation returns SIG_VALIDATION_SUCCESS (0) with an empty
    /// context, so it needs no off-chain signature and no postOp state; it simply sponsors gas from
    /// its EntryPoint deposit. validatePaymasterUserOp/postOp keep the exact canonical selectors
    /// (0x52b7512c / 0x7c627b21) so the EntryPoint's calls dispatch, and addStake(uint32) forwards a
    /// stake to the EntryPoint so the paymaster can be staked like a production sponsor.
    ///
    /// Solidity source (compiled with solc 0.8.28, optimizer on, 200 runs):
    /// <code>
    /// // SPDX-License-Identifier: MIT
    /// pragma solidity ^0.8.28;
    ///
    /// struct PackedUserOperation {
    ///     address sender; uint256 nonce; bytes initCode; bytes callData;
    ///     bytes32 accountGasLimits; uint256 preVerificationGas; bytes32 gasFees;
    ///     bytes paymasterAndData; bytes signature;
    /// }
    ///
    /// interface IStakeEntryPoint { function addStake(uint32 unstakeDelaySec) external payable; }
    ///
    /// contract MinimalAcceptAllPaymaster {
    ///     address public immutable entryPoint;
    ///     constructor(address _entryPoint) { entryPoint = _entryPoint; }
    ///
    ///     function validatePaymasterUserOp(PackedUserOperation calldata, bytes32, uint256)
    ///         external returns (bytes memory context, uint256 validationData)
    ///     {
    ///         require(msg.sender == entryPoint, "not from EntryPoint");
    ///         return ("", 0);
    ///     }
    ///
    ///     function postOp(uint8, bytes calldata, uint256, uint256) external view {
    ///         require(msg.sender == entryPoint, "not from EntryPoint");
    ///     }
    ///
    ///     function addStake(uint32 unstakeDelaySec) external payable {
    ///         IStakeEntryPoint(entryPoint).addStake{value: msg.value}(unstakeDelaySec);
    ///     }
    /// }
    /// </code>
    /// </summary>
    internal static class MinimalAcceptAllPaymaster
    {
        public const string Bytecode =
            "0x60a060405234801561000f575f5ffd5b5060405161049338038061049383398101604081905261002e9161003f565b6001600160a01b031660805261006c565b5f6020828403121561004f575f5ffd5b81516001600160a01b0381168114610065575f5ffd5b9392505050565b6080516103fb6100985f395f818160bd0152818161011101528181610184015261021001526103fb5ff3fe60806040526004361061003e575f3560e01c80630396cb601461004257806352b7512c146100575780637c627b211461008d578063b0d691fe146100ac575b5f5ffd5b61005561005036600461027a565b6100f7565b005b348015610062575f5ffd5b506100766100713660046102a4565b610176565b6040516100849291906102f3565b60405180910390f35b348015610098575f5ffd5b506100556100a736600461032f565b610205565b3480156100b7575f5ffd5b506100df7f000000000000000000000000000000000000000000000000000000000000000081565b6040516001600160a01b039091168152602001610084565b604051621cb65b60e51b815263ffffffff821660048201527f00000000000000000000000000000000000000000000000000000000000000006001600160a01b031690630396cb609034906024015f604051808303818588803b15801561015c575f5ffd5b505af115801561016e573d5f5f3e3d5ffd5b505050505050565b60605f336001600160a01b037f000000000000000000000000000000000000000000000000000000000000000016146101ec5760405162461bcd60e51b81526020600482015260136024820152721b9bdd08199c9bdb48115b9d1c9e541bda5b9d606a1b60448201526064015b60405180910390fd5b505060408051602081019091525f808252935093915050565b336001600160a01b037f000000000000000000000000000000000000000000000000000000000000000016146102735760405162461bcd60e51b81526020600482015260136024820152721b9bdd08199c9bdb48115b9d1c9e541bda5b9d606a1b60448201526064016101e3565b5050505050565b5f6020828403121561028a575f5ffd5b813563ffffffff8116811461029d575f5ffd5b9392505050565b5f5f5f606084860312156102b6575f5ffd5b833567ffffffffffffffff8111156102cc575f5ffd5b840161012081870312156102de575f5ffd5b95602085013595506040909401359392505050565b604081525f83518060408401528060208601606085015e5f606082850101526060601f19601f8301168401019150508260208301529392505050565b5f5f5f5f5f60808688031215610343575f5ffd5b853560ff81168114610353575f5ffd5b9450602086013567ffffffffffffffff81111561036e575f5ffd5b8601601f8101881361037e575f5ffd5b803567ffffffffffffffff811115610394575f5ffd5b8860208284010111156103a5575f5ffd5b95986020919091019750949560408101359560609091013594509250505056fea2646970667358221220057e7d01149a4e82240d527c89fac0296a355cf99b67ab2eb735db01fdf1112e64736f6c634300081c0033";
    }
}
