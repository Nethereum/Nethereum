// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "@openzeppelin/contracts/token/ERC20/utils/SafeERC20.sol";
import "./BasePaymaster.sol";
import "../interfaces/IPaymaster.sol";

interface IPriceOracle {
    function getPrice(address token) external view returns (uint256);
}

contract TokenPaymaster is BasePaymaster, ITokenPaymaster {
    using SafeERC20 for IERC20;

    address private _token;
    address private _priceOracle;
    uint256 private _priceMarkup;

    uint256 public constant MARKUP_DENOMINATOR = 100;
    uint256 public constant PRICE_DECIMALS = 18;

    event OracleChanged(address indexed oldOracle, address indexed newOracle);
    event TokenChanged(address indexed oldToken, address indexed newToken);

    error InvalidMarkup();
    error InsufficientTokenAllowance();
    error InsufficientTokenBalance();
    error TokenTransferFailed();

    constructor(
        address _entryPoint,
        address _owner,
        address tokenAddress,
        address oracleAddress,
        uint256 markup
    ) BasePaymaster(_entryPoint, _owner) {
        _token = tokenAddress;
        _priceOracle = oracleAddress;
        _priceMarkup = markup;
    }

    function token() external view override returns (address) {
        return _token;
    }

    function priceOracle() external view override returns (address) {
        return _priceOracle;
    }

    function priceMarkup() external view override returns (uint256) {
        return _priceMarkup;
    }

    function setPriceMarkup(uint256 markup) external override onlyOwner {
        if (markup < 100) revert InvalidMarkup();
        _priceMarkup = markup;
    }

    function setToken(address tokenAddress) external onlyOwner {
        address oldToken = _token;
        _token = tokenAddress;
        emit TokenChanged(oldToken, tokenAddress);
    }

    function setPriceOracle(address oracleAddress) external onlyOwner {
        address oldOracle = _priceOracle;
        _priceOracle = oracleAddress;
        emit OracleChanged(oldOracle, oracleAddress);
    }

    function validatePaymasterUserOp(
        PackedUserOperation calldata userOp,
        bytes32,
        uint256 maxCost
    )
        external
        override(BasePaymaster, IPaymaster)
        onlyEntryPoint
        returns (bytes memory context, uint256 validationData)
    {
        address sender = userOp.sender;

        uint256 tokenCost = _calculateTokenCost(maxCost);

        IERC20 tokenContract = IERC20(_token);

        if (tokenContract.allowance(sender, address(this)) < tokenCost) {
            return ("", SIG_VALIDATION_FAILED);
        }

        if (tokenContract.balanceOf(sender) < tokenCost) {
            return ("", SIG_VALIDATION_FAILED);
        }

        tokenContract.safeTransferFrom(sender, address(this), tokenCost);

        context = abi.encode(sender, tokenCost, maxCost);
        validationData = 0;
    }

    function postOp(
        PostOpMode mode,
        bytes calldata context,
        uint256 actualGasCost,
        uint256
    ) external override(BasePaymaster, IPaymaster) onlyEntryPoint {
        (address sender, uint256 preCharged, uint256 maxCost) = abi.decode(
            context,
            (address, uint256, uint256)
        );

        if (mode == PostOpMode.OpReverted) {
            IERC20(_token).safeTransfer(sender, preCharged);
            return;
        }

        uint256 actualTokenCost = _calculateTokenCost(actualGasCost);

        if (preCharged > actualTokenCost) {
            uint256 refund = preCharged - actualTokenCost;
            IERC20(_token).safeTransfer(sender, refund);
        }

        emit TokenPayment(sender, _token, actualTokenCost);
    }

    function _calculateTokenCost(uint256 ethCost) internal view returns (uint256) {
        uint256 tokenPrice = IPriceOracle(_priceOracle).getPrice(_token);

        return (ethCost * _priceMarkup * (10 ** PRICE_DECIMALS)) / (tokenPrice * MARKUP_DENOMINATOR);
    }

    function estimateTokenCost(uint256 ethCost) external view returns (uint256) {
        return _calculateTokenCost(ethCost);
    }

    function getCurrentTokenPrice() external view returns (uint256) {
        return IPriceOracle(_priceOracle).getPrice(_token);
    }

    function withdrawTokens(address to, uint256 amount) external onlyOwner {
        IERC20(_token).safeTransfer(to, amount);
    }
}
