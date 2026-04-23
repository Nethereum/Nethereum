using System.Collections.Generic;
using Nethereum.Beaconchain.Responses;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.Beaconchain
{
    public static class BlobSidecarHelper
    {
        public static List<byte[]> ExtractBlobs(BlobSidecarResponse response)
        {
            var blobs = new List<byte[]>();
            if (response?.Data == null) return blobs;

            foreach (var item in response.Data)
            {
                if (!string.IsNullOrEmpty(item.Blob))
                    blobs.Add(item.Blob.HexToByteArray());
            }
            return blobs;
        }

        public static BlobSidecar ToBlobSidecar(BlobSidecarResponse response)
        {
            var blobs = new List<byte[]>();
            var commitments = new List<byte[]>();
            var proofs = new List<byte[]>();

            if (response?.Data == null)
                return new BlobSidecar(blobs, commitments, proofs);

            foreach (var item in response.Data)
            {
                if (!string.IsNullOrEmpty(item.Blob))
                    blobs.Add(item.Blob.HexToByteArray());
                if (!string.IsNullOrEmpty(item.KzgCommitment))
                    commitments.Add(item.KzgCommitment.HexToByteArray());
                if (!string.IsNullOrEmpty(item.KzgProof))
                    proofs.Add(item.KzgProof.HexToByteArray());
            }

            return new BlobSidecar(blobs, commitments, proofs);
        }

        public static byte[] DecodeData(BlobSidecarResponse response)
        {
            var blobs = ExtractBlobs(response);
            if (blobs.Count == 0) return new byte[0];
            return BlobEncoder.DecodeBlobs(blobs);
        }
    }
}
