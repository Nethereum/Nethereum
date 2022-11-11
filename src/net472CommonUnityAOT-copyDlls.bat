REM Excel formula if needed ="xcopy ""compiledlibraries\net472dllsAOT\"&D1&""" ""compiledlibraries\net472UnityCommonAOT\" & D1 & """ /s /y"

xcopy "compiledlibraries\net472dllsAOT\Nethereum.ABI.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.ABI.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Accounts.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Accounts.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Contracts.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Contracts.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.BlockchainProcessing.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.BlockchainProcessing.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.GnosisSafe.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.GnosisSafe.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.HdWallet.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.HdWallet.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Hex.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Hex.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.JsonRpc.Client.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.JsonRpc.Client.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.JsonRpc.RpcClient.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.JsonRpc.RpcClient.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.KeyStore.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.KeyStore.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Model.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Model.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.RLP.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.RLP.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.RPC.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.RPC.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Signer.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Signer.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Signer.EIP712.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Signer.EIP712.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Siwe.Core.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Siwe.Core.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Siwe.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Siwe.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Unity.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Unity.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Unity.Metamask.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Unity.Metamask.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Util.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Util.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\Nethereum.Web3.dll" "compiledlibraries\net472UnityCommonAOT\Nethereum.Web3.dll" /s /y
xcopy "compiledlibraries\net472dllsAOT\*.jslib "compiledlibraries\net472UnityCommonAOT" /s /y

EXIT /B 0