start /wait cmd /k CALL  net461AOT.bat
start /wait cmd /k CALL  netStandardUnityAOT.bat
start /wait cmd /k CALL netStandardMinimalWebglUnityAOT-copyDlls.bat
start /wait cmd /k CALL netStandardCommonUnityAOT-copyDlls.bat
start /wait cmd /k CALL net472AOT.bat
start /wait cmd /k CALL net472CommonUnityAOT-copyDlls.bat
start /wait cmd /k CALL nuget.bat