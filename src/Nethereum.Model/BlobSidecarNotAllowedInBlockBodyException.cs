using System;

namespace Nethereum.Model
{
    public class BlobSidecarNotAllowedInBlockBodyException : Exception
    {
        public BlobSidecarNotAllowedInBlockBodyException()
            : base("A type-3 transaction in network (blob sidecar) form is not a valid block-body transaction.")
        {
        }
    }
}
