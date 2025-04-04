using Nethereum.ABI;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccount;
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccount.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory;
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ExecAccountTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ExecAccountTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }


        [Fact]
        public async Task ShouldExecuteAccountAsync()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var ownerAddress = EthereumClientIntegrationFixture.AccountAddress;

            var entryPointDeployment = new EntryPointDeployment();
            
            var entryPointService = 
                await EntryPointService.DeployContractAndGetServiceAsync(web3, entryPointDeployment);

            var simpleAccountFactoryDeployment = new TestExecAccountFactoryDeployment();
            simpleAccountFactoryDeployment.EntryPoint = entryPointService.ContractHandler.ContractAddress;

            var simpleAccountFactoryService =
                await TestExecAccountFactoryService.DeployContractAndGetServiceAsync(web3, simpleAccountFactoryDeployment);

            var txnAccountCreationReceipt = await simpleAccountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(ownerAddress, 0);
            //0 salt
            var account = await simpleAccountFactoryService.CreateAccountQueryAsync(ownerAddress, 0);

            //await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(account, 1);
            
            var simpleAccountService = new TestExecAccountService(web3, account);
            
            //deposit to entry point to pay for gas
            var receiptDeposit = await simpleAccountService.AddDepositRequestAndWaitForReceiptAsync(
                new AddDepositFunction() { AmountToSend = Web3.Web3.Convert.ToWei(1) });

            var execSignature = ABITypedRegistry.GetFunctionABI<ExecuteUserOpFunction>().Sha3Signature;
            var entrySignature = ABITypedRegistry.GetFunctionABI<EntryPointFunction>().Sha3Signature;
            var innerCall = new ABIEncode().GetABIEncoded(new ABIValue("address", account), new ABIValue("bytes", entrySignature.HexToByteArray()));
            var callData = ByteUtil.Merge(execSignature.HexToByteArray(), innerCall);

            var signedPackedUserOperation = await entryPointService.SignAndInitialiseUserOperationAsync(new UserOperation()
            {
                Sender = account,
                CallGasLimit = 1000000, //estimation fails with this calldata
                CallData = callData
            }, new Signer.EthECKey(EthereumClientIntegrationFixture.AccountPrivateKey));

            try
            {
                var receipt = await entryPointService.HandleOpsRequestAndWaitForReceiptAsync(new List<EntryPoint.ContractDefinition.PackedUserOperation>() { signedPackedUserOperation }, ownerAddress);
                var executed = receipt.DecodeAllEvents<ExecutedEventDTO>();
                Assert.True(executed.Count == 1);

                var addressInnerCall = new AddressType().Decode<string>(executed[0].Event.InnerCallRet);
                Assert.True(entryPointService.ContractAddress.IsTheSameAddress(addressInnerCall));
            }
            catch (SmartContractCustomErrorRevertException e)
            {
                var error = entryPointService.FindCustomErrorException(e);
                throw error;
            }


        }
    }

}
