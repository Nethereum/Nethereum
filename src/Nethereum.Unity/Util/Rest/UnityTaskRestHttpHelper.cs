﻿#if !DOTNET35
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Nethereum.Util.Rest;
using System.Collections.Generic;
namespace Nethereum.Unity.Util.Rest
{

    public class UnityTaskRestHttpHelper : IRestHttpHelper
    {
        // GET request method
        public async Task<T> GetAsync<T>(string path, Dictionary<string, string> headers = null)
        {
            using (var unityRequest = new UnityWebRequest(path, UnityWebRequest.kHttpVerbGET))
            {
                unityRequest.downloadHandler = new DownloadHandlerBuffer();

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        unityRequest.SetRequestHeader(header.Key, header.Value);
                    }
                }

                await unityRequest.SendWebRequest();

                if (unityRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Error occurred when trying to send GET request: {unityRequest.error}");
                }

                byte[] results = unityRequest.downloadHandler.data;
                var result = Encoding.UTF8.GetString(results);

                if (typeof(T) == typeof(string))
                    return (T)(object)result;

                return JsonConvert.DeserializeObject<T>(result);
            }
        }

        public async Task<TResponse> PostAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null)
        {
            using (var unityRequest = new UnityWebRequest(path, UnityWebRequest.kHttpVerbPOST))
            {
                string requestBody = JsonConvert.SerializeObject(request);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);

                unityRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                unityRequest.SetRequestHeader("Content-Type", "application/json");
                unityRequest.downloadHandler = new DownloadHandlerBuffer();

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        unityRequest.SetRequestHeader(header.Key, header.Value);
                    }
                }

                await unityRequest.SendWebRequest();

                if (unityRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Error occurred when trying to send POST request: {unityRequest.error}");
                }

                byte[] results = unityRequest.downloadHandler.data;
                var result = Encoding.UTF8.GetString(results);

                if (typeof(TResponse) == typeof(string))
                    return (TResponse)(object)result;

                return JsonConvert.DeserializeObject<TResponse>(result);
            }
        }

      
        public async Task<TResponse> PutAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null)
        {
            using (var unityRequest = new UnityWebRequest(path, UnityWebRequest.kHttpVerbPUT))
            {
                string requestBody = JsonConvert.SerializeObject(request);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);

                unityRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                unityRequest.SetRequestHeader("Content-Type", "application/json");
                unityRequest.downloadHandler = new DownloadHandlerBuffer();

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        unityRequest.SetRequestHeader(header.Key, header.Value);
                    }
                }

                await unityRequest.SendWebRequest();

                if (unityRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Error occurred when trying to send PUT request: {unityRequest.error}");
                }

                byte[] results = unityRequest.downloadHandler.data;
                var result = Encoding.UTF8.GetString(results);

                if (typeof(TResponse) == typeof(string))
                    return (TResponse)(object)result;

                return JsonConvert.DeserializeObject<TResponse>(result);
            }
        }

        public async Task DeleteAsync(string path, Dictionary<string, string> headers = null)
        {
            using (var unityRequest = new UnityWebRequest(path, UnityWebRequest.kHttpVerbDELETE))
            {
                unityRequest.downloadHandler = new DownloadHandlerBuffer();
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        unityRequest.SetRequestHeader(header.Key, header.Value);
                    }
                }
                await unityRequest.SendWebRequest();

                if (unityRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Error occurred when trying to send DELETE request: {unityRequest.error}");
                }
            }
        }

        public async Task<TResponse> PostMultipartAsync<TResponse>(string path, MultipartFormDataRequest request, Dictionary<string, string> headers = null)
        {
            var sections = new List<IMultipartFormSection>();

           
            foreach (var f in request.Fields)
                sections.Add(new MultipartFormDataSection(f.Name, f.Value));

           
            foreach (var f in request.Files)
                sections.Add(new MultipartFormFileSection(
                    f.FieldName,
                    Encoding.UTF8.GetBytes(f.Content),
                    f.FileName,
                f.ContentType));

            using (var req = UnityWebRequest.Post(path, sections))   
            {
                if (headers != null)
                    foreach (var h in headers)
                        req.SetRequestHeader(h.Key, h.Value);

                await req.SendWebRequest();                         

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Error {req.responseCode}: {req.error}");

                var json = req.downloadHandler.text;

                if (typeof(TResponse) == typeof(string))
                    return (TResponse)(object)json;

                return JsonConvert.DeserializeObject<TResponse>(json);
            
       }   }
    }

}
#endif
