using System;
using System.Collections.Generic;

namespace Nethereum.Mud
{
    public class ResourceRegistry
    {
        public static Dictionary<Type, IResource> ResourceIds = new Dictionary<Type, IResource>();

        public static void RegisterResource<TResource>() where TResource : IResource, new()
        {
            if (!ResourceIds.ContainsKey(typeof(TResource)))
            {
                ResourceIds[typeof(TResource)] = new TResource();
            }
        }

        public static byte[] GetResourceEncoded(Type resourceType)
        {
            if (!ResourceIds.ContainsKey(resourceType))
            {
                ResourceIds[resourceType] = ((IResource)Activator.CreateInstance(resourceType));
            }

            return ResourceIds[resourceType].ResourceIdEncoded;
        }

        public static byte[] GetResourceEncoded<TResource>() where TResource : IResource, new()
        {
            if(!ResourceIds.ContainsKey(typeof(TResource)))
            {
                ResourceIds[typeof(TResource)] = new TResource();
            }

            return ResourceIds[typeof(TResource)].ResourceIdEncoded;
        }

        public static IResource GetResource(Type resourceType)
        {
            if (!ResourceIds.ContainsKey(resourceType))
            {
                ResourceIds[resourceType] = ((IResource)Activator.CreateInstance(resourceType));
            }

            return ResourceIds[resourceType];
        }

        public static TResource GetResource<TResource>() where TResource : IResource, new()
        {
            if (!ResourceIds.ContainsKey(typeof(TResource)))
            {
                ResourceIds[typeof(TResource)] = new TResource();
            }

            return (TResource)ResourceIds[typeof(TResource)];
        }   
        
    }
}
