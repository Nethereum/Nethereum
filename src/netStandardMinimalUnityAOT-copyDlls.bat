REM Excel formula if needed ="xcopy ""compiledlibraries\netStandardAOT\"&D1&""" ""compiledlibraries\netStandardMinimalUnityAOT\" & D1 & """ /s /y"

xcopy "compiledlibraries\netStandardAOT\Nethereum.ABI.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.ABI.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Accounts.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Accounts.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Contracts.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Contracts.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Hex.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Hex.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.JsonRpc.Client.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.JsonRpc.Client.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.JsonRpc.RpcClient.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.JsonRpc.RpcClient.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.KeyStore.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.KeyStore.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Model.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Model.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.RLP.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.RLP.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.RPC.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.RPC.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Signer.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Signer.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Signer.EIP712.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Signer.EIP712.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Siwe.Core.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Siwe.Core.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Unity.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Unity.dll" /s /y
xcopy "compiledlibraries\netStandardAOT\Nethereum.Util.dll" "compiledlibraries\netStandardMinimalUnityAOT\Nethereum.Util.dll" /s /y

EXIT /B 0