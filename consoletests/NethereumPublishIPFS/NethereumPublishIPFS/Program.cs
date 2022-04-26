// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

var files = Directory.GetFiles("../../../Staging");
var ipfsService = new Nethereum.Web3.IpfsHttpService("https://ipfs.infura.io:5001", "id", "id");
var metadataList = new List<AssemblyMetadata>();
foreach (var file in files)
{
    Thread.SpinWait(2000);
    var absolutePath = Path.GetFullPath(file);
    var assembly =  Assembly.LoadFile(absolutePath);
    var fullName = assembly.FullName;
    var name = assembly.GetName().Name;
    var ipfsHash = await ipfsService.AddFileAsync(absolutePath);
    var assemblyMetadata = new AssemblyMetadata();
    assemblyMetadata.FullName = fullName;
    assemblyMetadata.Name = name;
    assemblyMetadata.Url = "ipfs://" + ipfsHash.Hash;
    metadataList.Add(assemblyMetadata);
}
using (StreamWriter file = File.CreateText(@"../../../Libraries/assemblies.json"))
{
    JsonSerializer serializer = new JsonSerializer();
    serializer.Serialize(file, metadataList);
}

public class AssemblyMetadata
{
    public string Name { get; set; }
    public string FullName { get; set; }
    public string Url { get; set; }
}



