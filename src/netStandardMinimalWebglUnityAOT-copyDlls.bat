REM Excel formula if needed ="xcopy ""compiledlibraries\netStandardUnityAOT\"&D1&""" ""compiledlibraries\netStandardMinimalWebglUnityAOT\" & D1 & """ /s /y"

xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.ABI.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.ABI.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Accounts.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Accounts.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Contracts.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Contracts.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Hex.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Hex.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.JsonRpc.Client.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.JsonRpc.Client.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.JsonRpc.RpcClient.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.JsonRpc.RpcClient.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.KeyStore.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.KeyStore.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Model.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Model.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.RLP.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.RLP.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.RPC.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.RPC.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Signer.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Signer.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Signer.EIP712.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Signer.EIP712.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Siwe.Core.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Siwe.Core.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Unity.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Unity.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Unity.Metamask.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Unity.Metamask.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\Nethereum.Util.dll" "compiledlibraries\netStandardMinimalWebglUnityAOT\Nethereum.Util.dll" /s /y
xcopy "compiledlibraries\netStandardUnityAOT\*.jslib "compiledlibraries\netStandardMinimalWebglUnityAOT" /s /y
EXIT /B 0