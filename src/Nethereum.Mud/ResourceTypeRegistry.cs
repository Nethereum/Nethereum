using System;
using System.Collections.Generic;

namespace Nethereum.Mud
{
    public class ResourceTypeRegistry
    {
        public static Dictionary<string, Type> ResourceTypes = new Dictionary<string, Type>();

        public static void RegisterType(string resourceIdHex, Type resourceType)
        {
            ResourceTypes[resourceIdHex] = resourceType;
        }

        public static Type GetResourceType(string resourceIdHex)
        {
            return ResourceTypes[resourceIdHex];
        }
    }
}
