using Nethereum.Mud.EncodingDecoding;
using System;

namespace Nethereum.Mud
{
    public interface IResource
    {
        string Name { get; }
        string Namespace { get; }
        byte[] ResourceIdEncoded { get; }
        byte[] ResourceTypeId { get; }
    }


    public static class IResourceExtensions
    {
        public static bool IsNamespace(this IResource resource)
        {
            return resource.ResourceTypeId[0] == Resource.RESOURCE_NAMESPACE[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_NAMESPACE[1];
        }

        public static bool IsTable(this IResource resource)
        {
            return resource.ResourceTypeId[0] == Resource.RESOURCE_TABLE[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_TABLE[1];
        }

        public static bool IsOffchainTable(this IResource resource)
        {
            return resource.ResourceTypeId[0] == Resource.RESOURCE_OFFCHAIN_TABLE[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_OFFCHAIN_TABLE[1];
        }

        public static bool IsSystem(this IResource resource)
        {
            return resource.ResourceTypeId[0] == Resource.RESOURCE_SYSTEM[0] && resource.ResourceTypeId[1] == Resource.RESOURCE_SYSTEM[1];
        }

        public static bool IsRoot(this IResource resource)
        {
            return string.IsNullOrEmpty(resource.Namespace);
        }
    }

    public class Resource : IResource
    {
        //bytes2 constant RESOURCE_TABLE = "tb";
        public static readonly byte[] RESOURCE_TABLE = { 0x74, 0x62 };
        //bytes2 constant RESOURCE_OFFCHAIN_TABLE = "ot";
        public static readonly byte[] RESOURCE_OFFCHAIN_TABLE = { 0x6f, 0x74 };
        // Resource that identifies a namespace, a container belonging to a
        // specific address (not necessarily the original deployer of the World).
        // A namespace can include tables and systems.
        //bytes2 constant RESOURCE_NAMESPACE = "ns";
        public static readonly byte[] RESOURCE_NAMESPACE = { 0x6e, 0x73 };
        // Resource that identifies a system, a contract used to manipulate
        // the state.
        //bytes2 constant RESOURCE_SYSTEM = "sy";
        public static readonly byte[] RESOURCE_SYSTEM = { 0x73, 0x79 };

        public byte[] ResourceTypeId { get; set; }
        public string Namespace { get; set; } = String.Empty;
        public string Name { get; set; }
        public byte[] ResourceIdEncoded { get; set; }
       
    }
}
