using System;
using System.Text;

namespace Nethereum.Mud.EncodingDecoding
{
    public static class ResourceEncoder
    {
        public static string TrimNameAsValidSize(string name)
        {
            if (name.Length > 16)
            {
                return name.Substring(0, 16);
            }
            return name;
        }

        public static string TrimNamespaceNameAsValidSize(string namespaceName)
        {
            if (namespaceName.Length > 14)
            {
                return namespaceName.Substring(0, 14);
            }
            return namespaceName;
        }

        public static Resource Decode(byte[] resourceBytes)
        {
            var resource = new Resource();
            var resourceId = new byte[2];
            var namespaceBytes = new byte[14];
            var nameBytes = new byte[16];
            Array.Copy(resourceBytes, 0, resourceId, 0, 2);
            Array.Copy(resourceBytes, 2, namespaceBytes, 0, 14);
            Array.Copy(resourceBytes, 16, nameBytes, 0, 16);
#if NETSTANDARD1_1
            resource.Namespace = Encoding.UTF8.GetString(namespaceBytes, 0, namespaceBytes.Length).TrimEnd('\0');
            resource.Name = Encoding.UTF8.GetString(nameBytes, 0, nameBytes.Length).TrimEnd('\0');
#else
            resource.Namespace = Encoding.UTF8.GetString(namespaceBytes).TrimEnd('\0');
            resource.Name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
#endif
            resource.ResourceTypeId = resourceId;
            return resource;
        }

        public static byte[] EncodeNamesapce(string @namespace)
        {
            return Encode(Resource.RESOURCE_NAMESPACE, @namespace);
        }

        public static byte[] EncodeTable(string @namespace, string name)
        {
            return Encode(Resource.RESOURCE_TABLE, @namespace, name);
        }

        public static byte[] EncodeRootTable(string name)
        {
            return Encode(Resource.RESOURCE_TABLE, string.Empty, name);
        }

        public static byte[] EncodeOffchainTable(string @namespace, string name)
        {
            return Encode(Resource.RESOURCE_OFFCHAIN_TABLE, @namespace, name);
        }

        public static byte[] EncodeRootOffchainTable(string name)
        {
            return Encode(Resource.RESOURCE_OFFCHAIN_TABLE, string.Empty, name);
        }

        public static byte[] EncodeSystem(string @namespace, string name)
        {
            return Encode(Resource.RESOURCE_SYSTEM, @namespace, name);
        }

        public static byte[] EncodeRootSystem(string name)
        {
            return Encode(Resource.RESOURCE_SYSTEM, string.Empty, name);
        }


        public static byte[] Encode(IResource resource)
        {
            return Encode(resource.ResourceTypeId, resource.Namespace, resource.Name);
        }

        public static byte[] Encode(byte[] typeId, string @namespace, string name = "")
        {


            // Check for typeId length to ensure it's exactly 2 bytes.
            if (typeId.Length != 2) throw new ArgumentException("Type ID must be exactly 2 bytes.", nameof(typeId));

            if (typeId[0] == Resource.RESOURCE_NAMESPACE[0] && typeId[1] == Resource.RESOURCE_NAMESPACE[1] && !string.IsNullOrEmpty(name))
                throw new ArgumentException("Name must not be provided for a namespace.", nameof(name));

            if (@namespace.Length > 14) throw new ArgumentException("Namespace must be 14 bytes or fewer.", nameof(@namespace));
            // Ensure the name is correctly sized (30 bytes max).
            if (name.Length > 16) throw new ArgumentException("Name must be 16 bytes or fewer.", nameof(name));

            byte[] resourceId = new byte[32]; // Initialize with zeros

            // Copy the typeId into the first 2 bytes of resourceId
            Array.Copy(typeId, 0, resourceId, 0, 2);

            // Convert name to bytes and copy into the resourceId array starting at the 3rd byte
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] @namespaceBytes = Encoding.UTF8.GetBytes(@namespace);
            Array.Copy(@namespaceBytes, 0, resourceId, 2, @namespaceBytes.Length);
            Array.Copy(nameBytes, 0, resourceId, 16, nameBytes.Length);

            return resourceId;
        }
    }
}
