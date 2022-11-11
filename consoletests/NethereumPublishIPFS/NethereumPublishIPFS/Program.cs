// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

await AssemblyPublisher.PublishAssemblies(@"../../../Staging", @"../../../Libraries/assemblies.json");
//await AssemblyPublisher.PublishAssemblies(@"../../../NetCore", @"../../../Libraries/core-assemblies.json");
//await AssemblyPublisher.PublishAssemblies(@"../../../NetDapps", @"../../../Libraries/net-dapps.json");

public class AssemblyMetadata
{
    [JsonProperty("fullName")]
    public string FullName { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; }
    [JsonProperty("url")]
    public string Url { get; set; }
}


public class AssemblyPublisher
{
    public static async Task PublishAssemblies(string path, string fileOuput)
    {

        var files = Directory.GetFiles(path);
        var ipfsService = new Nethereum.Web3.IpfsHttpService("https://ipfs.infura.io:5001", "", "");
        var metadataList = new List<AssemblyMetadata>();
        foreach (var file in files)
        {
            Thread.SpinWait(2000);
            var absolutePath = Path.GetFullPath(file);
            var assembly = Assembly.LoadFile(absolutePath);
            var fullName = assembly.FullName;
            var name = assembly.GetName().Name;
            var ipfsHash = await ipfsService.AddFileAsync(absolutePath);
            var assemblyMetadata = new AssemblyMetadata();
            assemblyMetadata.FullName = fullName;
            assemblyMetadata.Name = name;
            assemblyMetadata.Url = "ipfs://" + ipfsHash.Hash;
            metadataList.Add(assemblyMetadata);
        }
        using (StreamWriter file = File.CreateText(fileOuput))
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(file, metadataList);
        }
    }
}


