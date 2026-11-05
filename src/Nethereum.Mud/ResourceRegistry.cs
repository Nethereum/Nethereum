using System;
using System.Collections.Generic;

namespace Nethereum.Mud
{
    public class ResourceRegistry
    {
        public static Dictionary<Type, IResource> ResourceIds = new Dictionary<Type, IResource>();
        private static readonly object ResourceGate = new object();

        public static void RegisterResource<TResource>() where TResource : IResource, new()
        {
            GetResource<TResource>();
        }

        public static byte[] GetResourceEncoded(Type resourceType)
        {
            return GetResource(resourceType).ResourceIdEncoded;
        }

        public static byte[] GetResourceEncoded<TResource>() where TResource : IResource, new()
        {
            return GetResource<TResource>().ResourceIdEncoded;
        }

        public static IResource GetResource(Type resourceType)
        {
            lock (ResourceGate)
            {
                if (!ResourceIds.TryGetValue(resourceType, out var resource))
                {
                    resource = (IResource)Activator.CreateInstance(resourceType);
                    ResourceIds[resourceType] = resource;
                }
                return resource;
            }
        }

        public static TResource GetResource<TResource>() where TResource : IResource, new()
        {
            lock (ResourceGate)
            {
                if (!ResourceIds.TryGetValue(typeof(TResource), out var resource))
                {
                    resource = new TResource();
                    ResourceIds[typeof(TResource)] = resource;
                }
                return (TResource)resource;
            }
        }   
        
    }
}
