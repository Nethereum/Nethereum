using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.PrivacyPools.CommitmentVerifier;
using Nethereum.PrivacyPools.CommitmentVerifier.ContractDefinition;
using Nethereum.PrivacyPools.Entrypoint;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Nethereum.PrivacyPools.ERC1967Proxy;
using Nethereum.PrivacyPools.ERC1967Proxy.ContractDefinition;
using Nethereum.PrivacyPools.PoseidonT3;
using Nethereum.PrivacyPools.PoseidonT3.ContractDefinition;
using Nethereum.PrivacyPools.PoseidonT4;
using Nethereum.PrivacyPools.PoseidonT4.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Nethereum.PrivacyPools.PrivacyPoolComplex;
using Nethereum.PrivacyPools.PrivacyPoolComplex.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolSimple;
using Nethereum.PrivacyPools.PrivacyPoolSimple.ContractDefinition;
using Nethereum.PrivacyPools.WithdrawalVerifier;
using Nethereum.PrivacyPools.WithdrawalVerifier.ContractDefinition;
using Nethereum.Web3;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPoolDeploymentConfig
    {
        public string OwnerAddress { get; set; } = "";
        public string? PostmanAddress { get; set; }
        public string NativeAsset { get; set; } = "0xEeeeeEeeeEeEeeEeEeEeeEEEeeeeEeeeeeeeEEeE";
        public BigInteger MinimumDepositAmount { get; set; } = BigInteger.Zero;
        public BigInteger VettingFeeBps { get; set; } = BigInteger.Zero;
        public BigInteger MaxRelayFeeBps { get; set; } = BigInteger.Zero;
        public BigInteger DeployGasLimit { get; set; } = 15_000_000;
    }

    public class PrivacyPoolDeploymentResult
    {
        public EntrypointService Entrypoint { get; set; } = null!;
        public PrivacyPoolSimpleService Pool { get; set; } = null!;
        public WithdrawalVerifierService WithdrawalVerifier { get; set; } = null!;
        public CommitmentVerifierService CommitmentVerifier { get; set; } = null!;
        public PoseidonT3Service PoseidonT3 { get; set; } = null!;
        public PoseidonT4Service PoseidonT4 { get; set; } = null!;
        public string ProxyAddress { get; set; } = "";
    }

    public class PrivacyPoolERC20DeploymentConfig
    {
        public string OwnerAddress { get; set; } = "";
        public string? PostmanAddress { get; set; }
        public string TokenAddress { get; set; } = "";
        public BigInteger MinimumDepositAmount { get; set; } = BigInteger.Zero;
        public BigInteger VettingFeeBps { get; set; } = BigInteger.Zero;
        public BigInteger MaxRelayFeeBps { get; set; } = BigInteger.Zero;
        public BigInteger DeployGasLimit { get; set; } = 15_000_000;
    }

    public class PrivacyPoolERC20DeploymentResult
    {
        public EntrypointService Entrypoint { get; set; } = null!;
        public PrivacyPoolComplexService Pool { get; set; } = null!;
        public WithdrawalVerifierService WithdrawalVerifier { get; set; } = null!;
        public CommitmentVerifierService CommitmentVerifier { get; set; } = null!;
        public PoseidonT3Service PoseidonT3 { get; set; } = null!;
        public PoseidonT4Service PoseidonT4 { get; set; } = null!;
        public string ProxyAddress { get; set; } = "";
        public string TokenAddress { get; set; } = "";
    }

    public static class PrivacyPoolDeployer
    {
        public static async Task<PrivacyPoolDeploymentResult> DeployFullStackAsync(
            IWeb3 web3, PrivacyPoolDeploymentConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.OwnerAddress)) throw new ArgumentException("OwnerAddress is required", nameof(config));

            var owner = config.OwnerAddress;
            var postman = config.PostmanAddress ?? owner;

            // 1. Deploy Entrypoint implementation via raw bytecode (skips gas estimation that reverts on _disableInitializers)
            var implReceipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                EntrypointDeploymentBase.BYTECODE,
                owner,
                new Nethereum.Hex.HexTypes.HexBigInteger(config.DeployGasLimit));

            // 2. Deploy UUPS proxy
            var initFunction = new InitializeFunction { Owner = owner, Postman = postman };
            var initCalldata = initFunction.GetCallData();

            var proxyDeployment = new ERC1967ProxyDeployment
            {
                Implementation = implReceipt.ContractAddress,
                Data = initCalldata,
                Gas = (BigInteger)5_000_000
            };
            var proxyReceipt = await ERC1967ProxyService.DeployContractAndWaitForReceiptAsync(web3, proxyDeployment);
            var entrypointService = new EntrypointService(web3, proxyReceipt.ContractAddress);

            // 3. Deploy verifiers
            var withdrawalVerifierService = await WithdrawalVerifierService.DeployContractAndGetServiceAsync(
                web3, new WithdrawalVerifierDeployment());
            var commitmentVerifierService = await CommitmentVerifierService.DeployContractAndGetServiceAsync(
                web3, new CommitmentVerifierDeployment());

            // 4. Deploy Poseidon libraries
            var poseidonT3Service = await PoseidonT3Service.DeployContractAndGetServiceAsync(
                web3, new PoseidonT3Deployment { Gas = (BigInteger)config.DeployGasLimit });
            var poseidonT4Service = await PoseidonT4Service.DeployContractAndGetServiceAsync(
                web3, new PoseidonT4Deployment { Gas = (BigInteger)10_000_000 });

            // 5. Deploy pool with linked libraries
            var poolDeployment = new PrivacyPoolSimpleDeployment
            {
                Entrypoint = entrypointService.ContractAddress,
                WithdrawalVerifier = withdrawalVerifierService.ContractAddress,
                RagequitVerifier = commitmentVerifierService.ContractAddress,
                Gas = (BigInteger)10_000_000
            };
            poolDeployment.LinkLibraries(poseidonT3Service.ContractAddress, poseidonT4Service.ContractAddress);

            var poolService = await PrivacyPoolSimpleService.DeployContractAndGetServiceAsync(web3, poolDeployment);

            // 6. Register pool with entrypoint
            var registerReceipt = await entrypointService.RegisterPoolRequestAndWaitForReceiptAsync(
                config.NativeAsset, poolService.ContractAddress,
                config.MinimumDepositAmount, config.VettingFeeBps, config.MaxRelayFeeBps);

            if (registerReceipt.HasErrors() == true)
                throw new Exception("Pool registration failed");

            return new PrivacyPoolDeploymentResult
            {
                Entrypoint = entrypointService,
                Pool = poolService,
                WithdrawalVerifier = withdrawalVerifierService,
                CommitmentVerifier = commitmentVerifierService,
                PoseidonT3 = poseidonT3Service,
                PoseidonT4 = poseidonT4Service,
                ProxyAddress = proxyReceipt.ContractAddress
            };
        }

        public static async Task<PrivacyPoolERC20DeploymentResult> DeployERC20FullStackAsync(
            IWeb3 web3, PrivacyPoolERC20DeploymentConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.OwnerAddress)) throw new ArgumentException("OwnerAddress is required", nameof(config));
            if (string.IsNullOrEmpty(config.TokenAddress)) throw new ArgumentException("TokenAddress is required", nameof(config));

            var owner = config.OwnerAddress;
            var postman = config.PostmanAddress ?? owner;

            var implReceipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                EntrypointDeploymentBase.BYTECODE,
                owner,
                new Nethereum.Hex.HexTypes.HexBigInteger(config.DeployGasLimit));

            var initFunction = new InitializeFunction { Owner = owner, Postman = postman };
            var initCalldata = initFunction.GetCallData();

            var proxyDeployment = new ERC1967ProxyDeployment
            {
                Implementation = implReceipt.ContractAddress,
                Data = initCalldata,
                Gas = (BigInteger)5_000_000
            };
            var proxyReceipt = await ERC1967ProxyService.DeployContractAndWaitForReceiptAsync(web3, proxyDeployment);
            var entrypointService = new EntrypointService(web3, proxyReceipt.ContractAddress);

            var withdrawalVerifierService = await WithdrawalVerifierService.DeployContractAndGetServiceAsync(
                web3, new WithdrawalVerifierDeployment());
            var commitmentVerifierService = await CommitmentVerifierService.DeployContractAndGetServiceAsync(
                web3, new CommitmentVerifierDeployment());

            var poseidonT3Service = await PoseidonT3Service.DeployContractAndGetServiceAsync(
                web3, new PoseidonT3Deployment { Gas = (BigInteger)config.DeployGasLimit });
            var poseidonT4Service = await PoseidonT4Service.DeployContractAndGetServiceAsync(
                web3, new PoseidonT4Deployment { Gas = (BigInteger)10_000_000 });

            var poolDeployment = new PrivacyPoolComplexDeployment
            {
                Entrypoint = entrypointService.ContractAddress,
                WithdrawalVerifier = withdrawalVerifierService.ContractAddress,
                RagequitVerifier = commitmentVerifierService.ContractAddress,
                Asset = config.TokenAddress,
                Gas = (BigInteger)10_000_000
            };
            poolDeployment.LinkLibraries(poseidonT3Service.ContractAddress, poseidonT4Service.ContractAddress);

            var poolService = await PrivacyPoolComplexService.DeployContractAndGetServiceAsync(web3, poolDeployment);

            var registerReceipt = await entrypointService.RegisterPoolRequestAndWaitForReceiptAsync(
                config.TokenAddress, poolService.ContractAddress,
                config.MinimumDepositAmount, config.VettingFeeBps, config.MaxRelayFeeBps);

            if (registerReceipt.HasErrors() == true)
                throw new Exception("ERC20 pool registration failed");

            return new PrivacyPoolERC20DeploymentResult
            {
                Entrypoint = entrypointService,
                Pool = poolService,
                WithdrawalVerifier = withdrawalVerifierService,
                CommitmentVerifier = commitmentVerifierService,
                PoseidonT3 = poseidonT3Service,
                PoseidonT4 = poseidonT4Service,
                ProxyAddress = proxyReceipt.ContractAddress,
                TokenAddress = config.TokenAddress
            };
        }
    }
}
