SET configuration=Release
SET out=C:/Publish

call dnu pack ./src/JsonRpc.Core --configuration %configuration% --out "%out%/JsonRpc.Core"

call dnu pack ./src/JsonRpc.Router --configuration %configuration% --out "%out%/JsonRpc.Router"

call dnu pack ./src/JsonRpc.Client --configuration %configuration% --out "%out%/JsonRpc.Client"
