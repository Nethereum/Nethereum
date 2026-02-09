// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "./PackedUserOperation.sol";

enum PostOpMode {
    OpSucceeded,
    OpReverted,
    PostOpReverted
}

interface IPaymaster {
    function validatePaymasterUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 maxCost
    ) external returns (bytes memory context, uint256 validationData);

    function postOp(
        PostOpMode mode,
        bytes calldata context,
        uint256 actualGasCost,
        uint256 actualUserOpFeePerGas
    ) external;
}

interface IVerifyingPaymaster is IPaymaster {
    function verifyingSigner() external view returns (address);

    function setVerifyingSigner(address signer) external;

    event GasSponsored(address indexed sender, uint256 gasCost);
}

interface ITokenPaymaster is IPaymaster {
    function token() external view returns (address);

    function priceOracle() external view returns (address);

    function priceMarkup() external view returns (uint256);

    function setPriceMarkup(uint256 markup) external;

    event TokenPayment(address indexed sender, address indexed token, uint256 amount);
}

interface IDepositPaymaster is IPaymaster {
    function deposits(address account) external view returns (uint256);

    function deposit() external payable;

    function depositFor(address account) external payable;

    function withdraw(uint256 amount) external;

    function withdrawTo(address payable to, uint256 amount) external;

    event Deposited(address indexed account, uint256 amount);
    event Withdrawn(address indexed account, address indexed to, uint256 amount);
}

interface ISponsoredPaymaster is IPaymaster {
    function maxDailySponsorPerUser() external view returns (uint256);

    function dailySponsored(address account) external view returns (uint256);

    function setMaxDailySponsorPerUser(uint256 amount) external;

    event SponsorLimitSet(uint256 oldLimit, uint256 newLimit);
}

interface IHybridPaymaster is IPaymaster {
    function freeOpsPerMonth() external view returns (uint256);

    function freeOpsUsed(address account) external view returns (uint256);

    function creditBalance(address account) external view returns (uint256);

    function creditPricePerGas() external view returns (uint256);

    function buyCredits(uint256 tokenAmount) external;

    event CreditsPurchased(address indexed account, uint256 amount);
    event FreeOpUsed(address indexed account, uint256 opsRemaining);
    event CreditsUsed(address indexed account, uint256 amount);
}
