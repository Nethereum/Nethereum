using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine.Networking;
using Nethereum.Contracts.Standards.ERC721;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Contracts.Standards.ERC721
{
    public class NftMetadataUnityRequest<TNFTMetadata> : UnityRequest<List<TNFTMetadata>> where TNFTMetadata : NftMetadata
    {
        public IEnumerator GetAllMetadata(List<string> metadataUrls)
        {
            var returnData = new List<TNFTMetadata>();

            foreach (var metadataUrl in metadataUrls)
            {
                var metadataLocation = IpfsUrlService.ResolveIpfsUrlGateway(metadataUrl);


                using (UnityWebRequest webRequest = UnityWebRequest.Get(metadataLocation))
                {
                    yield return webRequest.SendWebRequest();


                    switch (webRequest.result)
                    {
                        case UnityWebRequest.Result.ConnectionError:
                        case UnityWebRequest.Result.DataProcessingError:
                            Exception = new Exception(webRequest.error);
                            yield break;

                        case UnityWebRequest.Result.ProtocolError:
                            Exception = new Exception("Http Error: " + webRequest.error);
                            yield break;

                        case UnityWebRequest.Result.Success:
                            try
                            {
                                returnData.Add(JsonConvert.DeserializeObject<TNFTMetadata>(webRequest.downloadHandler.text));
                            }
                            catch (Exception e)
                            {
                                Exception = e;
                                yield break;
                            }
                            break;
                    }
                }
                Result = returnData;
            }

        }
    }
}
