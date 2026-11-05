using System;
using System.IO;
using Nethereum.Documentation;

namespace Nethereum.Consensus.Clique
{
    [NethereumDocExample(DocSection.Consensus, "clique", "Encode and decode a snapshot for storage")]
    public static class CliqueSnapshotCodec
    {
        public static byte[] Encode(CliqueSnapshot snapshot)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(snapshot.BlockNumber);
            writer.Write(snapshot.Timestamp);
            writer.Write(snapshot.BlockHash.Length);
            writer.Write(snapshot.BlockHash);

            writer.Write(snapshot.Signers.Count);
            foreach (var signer in snapshot.Signers)
                writer.Write(signer);

            writer.Write(snapshot.Recents.Count);
            foreach (var recent in snapshot.Recents)
            {
                writer.Write(recent.Key);
                writer.Write(recent.Value);
            }

            writer.Write(snapshot.Votes.Count);
            foreach (var vote in snapshot.Votes)
            {
                writer.Write(vote.Key);
                writer.Write(vote.Value.Signer);
                writer.Write(vote.Value.Target);
                writer.Write(vote.Value.Authorize);
                writer.Write(vote.Value.BlockNumber);
            }

            writer.Write(snapshot.VoteTally.Count);
            foreach (var tally in snapshot.VoteTally)
            {
                writer.Write(tally.Key);
                writer.Write(tally.Value);
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static CliqueSnapshot? Decode(byte[] encoded)
        {
            try
            {
                using var reader = new BinaryReader(new MemoryStream(encoded));
                var snapshot = new CliqueSnapshot
                {
                    BlockNumber = reader.ReadInt64(),
                    Timestamp = reader.ReadInt64()
                };
                snapshot.BlockHash = reader.ReadBytes(reader.ReadInt32());

                for (var i = reader.ReadInt32(); i > 0; i--)
                    snapshot.Signers.Add(reader.ReadString());

                for (var i = reader.ReadInt32(); i > 0; i--)
                    snapshot.Recents[reader.ReadInt64()] = reader.ReadString();

                for (var i = reader.ReadInt32(); i > 0; i--)
                {
                    var key = reader.ReadString();
                    snapshot.Votes[key] = new CliqueVote
                    {
                        Signer = reader.ReadString(),
                        Target = reader.ReadString(),
                        Authorize = reader.ReadBoolean(),
                        BlockNumber = reader.ReadInt64()
                    };
                }

                for (var i = reader.ReadInt32(); i > 0; i--)
                    snapshot.VoteTally[reader.ReadString()] = reader.ReadInt32();

                return snapshot;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException || ex is FormatException)
            {
                return null;
            }
        }
    }
}
