using Nethereum.Util.HashProviders;
using System;
using System.Linq;


namespace Nethereum.Merkle.Patricia
{
    public class PatriciaTrie
    {
        public PatriciaTrie(IHashProvider hashProvider) 
        {
            HashProvider = hashProvider;
            Root = new EmptyNode();
        }

        public PatriciaTrie(byte[] hashRoot, IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
            Root = new HashNode(hashProvider) { Hash = hashRoot };
        }

        public PatriciaTrie(byte[] hashRoot):this(hashRoot, new Sha3KeccackHashProvider())
        {
           
        }

        public PatriciaTrie():this(new Sha3KeccackHashProvider())
        {
            
        }
        public IHashProvider HashProvider { get; }

        public Node Root { get; private set; }

        public byte[] Get(byte[] key, ITrieStorage storage)
        {
            return Get(Root, key.ConvertToNibbles(), storage);
        }

        public byte[] Get(Node node, byte[] keyAsNibbles, ITrieStorage storage)
        {
            if (node is null || node is EmptyNode)
            {
                return null;
            }

            if (node is LeafNode leafNode)
            {
                return GetFromLeafNode(leafNode, keyAsNibbles);
            }

            if (node is BranchNode branchNode)
            {
                return GetFromBranchNode(branchNode, keyAsNibbles, storage);
            }

            if (node is ExtendedNode extendedNode)
            {
                return GetFromExtendedNode(extendedNode, keyAsNibbles, storage);
            }

            if (node is HashNode hashNode)
            {
                return GetFromHashNode(keyAsNibbles, storage, hashNode);
            }

            return null;
        }

        public byte[] GetFromHashNode(byte[] keyAsNibbles, ITrieStorage storage, HashNode hashNode)
        {
            if (hashNode.InnerNode == null)
            {
                hashNode.DecodeInnerNode(storage, false);

            }
            return Get(hashNode.InnerNode, keyAsNibbles, storage);
        }

        public byte[] GetFromExtendedNode(ExtendedNode currentNode, byte[] keyAsNibbles, ITrieStorage storage)
        {
            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            if (currentNode.Nibbles.Length > foundSameNibbles.Length) return null; //No entry in between
            return Get(currentNode.InnerNode, keyAsNibbles.Skip(foundSameNibbles.Length).ToArray(), storage);
        }

        public byte[] GetFromLeafNode(LeafNode currentNode, byte[] keyAsNibbles)
        {
            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            var areLeafNodeNibblesAndKeyNibblesTheSame =
               (foundSameNibbles.Length == currentNode.Nibbles.Length && foundSameNibbles.Length == keyAsNibbles.Length);
            if (areLeafNodeNibblesAndKeyNibblesTheSame)
            {
                return currentNode.Value;
            }

            return null;
        }

        public byte[] GetFromBranchNode(BranchNode currentNode, byte[] keyAsNibbles, ITrieStorage storage)
        {
            if(keyAsNibbles == null || keyAsNibbles.Length == 0)
            {
                return currentNode.Value;
            }
            return Get(currentNode.Children[keyAsNibbles[0]], keyAsNibbles.Skip(1).ToArray(), storage);
        }

        public Node Put(Node node, byte[] keyAsNibbles, byte[] value, ITrieStorage storage = null)
        {
            if (node is null || node is EmptyNode)
            {
                return PutOnAnExistingEmptyNode(keyAsNibbles, value);
            }

            if (node is LeafNode leafNode)
            {
                return PutOnAnExistingLeafNode(leafNode, keyAsNibbles, value);
            }

            if (node is BranchNode branchNode)
            {
                return PutOnAnExistingBranchNode(branchNode, keyAsNibbles, value, storage);
            }

            if (node is ExtendedNode extendedNode)
            {
                return PutOnAnExistingExtendedNode(extendedNode, keyAsNibbles, value, storage);
            }

            if (node is HashNode hashNode)
            {
                return PutOnAnExistingHashNode(hashNode, keyAsNibbles, value, storage);
            }

            return null;

        }

        private Node PutOnAnExistingHashNode(HashNode hashNode, byte[] keyAsNibbles, byte[] value, ITrieStorage storage)
        {
            if(hashNode.InnerNode == null)
            {
                hashNode.DecodeInnerNode(storage, false);
            }

            hashNode.InnerNode = Put(hashNode.InnerNode, keyAsNibbles, value, storage);
            return hashNode;
        }

        private Node PutOnAnExistingExtendedNode(ExtendedNode currentNode, byte[] keyAsNibbles, byte[] value, ITrieStorage storage = null)
        {

            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            var extendedNodeHasNonSameNibbles = foundSameNibbles.Length < currentNode.Nibbles.Length;

            if (extendedNodeHasNonSameNibbles)
            {
                var newBranchCurrentNodeNibble = currentNode.Nibbles[foundSameNibbles.Length];
                var currentNodeNonSameNibbles = currentNode.Nibbles.Skip(foundSameNibbles.Length + 1).ToArray();
                var newBranch = new BranchNode(HashProvider);
                //Extension node if more nibbles
                if (currentNodeNonSameNibbles.Length > 0)
                {
                    var extendedNode = new ExtendedNode();
                    extendedNode.Nibbles = currentNodeNonSameNibbles;
                    extendedNode.InnerNode = currentNode.InnerNode;
                    newBranch.SetChild(newBranchCurrentNodeNibble, extendedNode);
                }
                else
                {
                    newBranch.SetChild(newBranchCurrentNodeNibble, currentNode.InnerNode);

                }

                var keyHasMoreNibblesThanFoundTheSame = foundSameNibbles.Length < keyAsNibbles.Length;

                if (keyHasMoreNibblesThanFoundTheSame)
                {
                    var newLeafBranchNibble = keyAsNibbles[foundSameNibbles.Length];
                    var keyNonSameNibbles = keyAsNibbles.Skip(foundSameNibbles.Length + 1).ToArray();
                    var newLeafValue = new LeafNode(HashProvider);
                    newLeafValue.Value = value;
                    newLeafValue.Nibbles = keyNonSameNibbles;
                    newBranch.SetChild(newLeafBranchNibble, newLeafValue);
                }
                else
                {
                    var keyHasTheSameFoundNibbles = foundSameNibbles.Length == keyAsNibbles.Length;
                    if (keyHasTheSameFoundNibbles)
                    {
                        newBranch.Value = value;
                    }

                }

                if (foundSameNibbles.Length == 0)
                {
                    return newBranch;
                }
                else
                {
                    return new ExtendedNode(HashProvider) { Nibbles = foundSameNibbles, InnerNode = newBranch };
                }
            
            }
            else
            {
                currentNode.InnerNode = Put(currentNode.InnerNode, keyAsNibbles.Skip(foundSameNibbles.Length).ToArray(), value, storage);
                return currentNode;
            }

        }

        private Node PutOnAnExistingBranchNode(BranchNode currentNode, byte[] keyAsNibbles, byte[] value, ITrieStorage storage = null)
        {
            if(keyAsNibbles == null || keyAsNibbles.Length == 0)
            {
                currentNode.Value = value;
                return currentNode;
            }

            var nibbleBranch = keyAsNibbles[0];
            currentNode.Children[nibbleBranch] = Put(currentNode.Children[nibbleBranch], keyAsNibbles.Skip(1).ToArray(), value, storage);
            return currentNode;
        }
        

        private Node PutOnAnExistingLeafNode(LeafNode currentNode, byte[] keyAsNibbles, byte[] value)
        {
            var foundSameNibbles = currentNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
            var areLeafNodeNibblesAndKeyNibblesTheSame = 
                (foundSameNibbles.Length == currentNode.Nibbles.Length && foundSameNibbles.Length == keyAsNibbles.Length);
            
            if (areLeafNodeNibblesAndKeyNibblesTheSame)
            {
                currentNode.Value = value;
                return currentNode;
            }

            var branchNode = new BranchNode(HashProvider);
     
            var allTheLeafNodeNibblesFoundTheSameAreIncludedButKeyNibblesHasMore = (currentNode.Nibbles.Length == foundSameNibbles.Length) && !areLeafNodeNibblesAndKeyNibblesTheSame;
            var allTheKeyNibblesFoundTheSameAreIncludedButLeafNodeHasMore = (keyAsNibbles.Length == foundSameNibbles.Length) && !areLeafNodeNibblesAndKeyNibblesTheSame;
            var keyNibblesHasMoreNibblesThanFoundTheSame = keyAsNibbles.Length > foundSameNibbles.Length;
            var leafNodeHasMoreNibblesThanFoundTheSame = currentNode.Nibbles.Length > foundSameNibbles.Length;
            
            if (allTheLeafNodeNibblesFoundTheSameAreIncludedButKeyNibblesHasMore)
            {
                // set the branch node value with the current node value and we will create a new leaf with new value
                branchNode.Value = currentNode.Value;
            }

            if (keyNibblesHasMoreNibblesThanFoundTheSame)
            {
                var newLeafNode = new LeafNode(HashProvider);
                newLeafNode.Value = value;
                //Set the nibbles as the reminder that are not found the same
                newLeafNode.Nibbles = keyAsNibbles.Skip(foundSameNibbles.Length + 1).ToArray();
                //set the child as the first nibble not found
                branchNode.SetChild(keyAsNibbles[foundSameNibbles.Length], newLeafNode);
            }

            if (allTheKeyNibblesFoundTheSameAreIncludedButLeafNodeHasMore)
            {
                // set the branch node value with the new value as we will be creating a new leaf using the original value
                branchNode.Value = value;

            }

            if (leafNodeHasMoreNibblesThanFoundTheSame) 
            {
                var newLeafNode = new LeafNode(HashProvider);
                newLeafNode.Value = currentNode.Value;
                //Set the nibbles as the reminder that are not found the same
                newLeafNode.Nibbles = currentNode.Nibbles.Skip(foundSameNibbles.Length + 1).ToArray();
                //set the child as the first nibble not found
                branchNode.SetChild(currentNode.Nibbles[foundSameNibbles.Length], newLeafNode);
            }

            //create an extended node with the branchNode if we have found some nibbles the same
            if(foundSameNibbles.Length > 0)
            {
                var extendedNode = new ExtendedNode(HashProvider);
                extendedNode.Nibbles = foundSameNibbles;
                extendedNode.InnerNode = branchNode;
                return extendedNode;
            }

            return branchNode;
        }

        private LeafNode PutOnAnExistingEmptyNode(byte[] keyAsNibbles, byte[] value)
        {
            var newLeafNode = new LeafNode(HashProvider);
            newLeafNode.Nibbles = keyAsNibbles;
            newLeafNode.Value = value;
            return newLeafNode;
        }

        public void Put(byte[] key, byte[] value, ITrieStorage storage = null)
        {
            Root = Put(Root, key.ConvertToNibbles(), value, storage);
        }

        public InMemoryTrieStorage GenerateProof(byte[] key)
        {
           return GenerateProof(Root, key.ConvertToNibbles(), new InMemoryTrieStorage());
        }

        public InMemoryTrieStorage GenerateProof(Node currentNode, byte[] keyAsNibbles, InMemoryTrieStorage storage)
        {
            storage.Put(currentNode.GetHash(), currentNode.GetRLPEncodedData());

            if (currentNode is EmptyNode || currentNode is null)
            {
                //not matching any keys
                return null;
            }

            if (currentNode is LeafNode leafNode)
            {
                var foundSameNibbles = leafNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
                if (foundSameNibbles.Length != leafNode.Nibbles.Length || foundSameNibbles.Length != keyAsNibbles.Length)
                {
                    //not matching all
                    return null;
                }
                return storage;
            }

            if (currentNode is BranchNode branchNode)
            {
               if(keyAsNibbles.Length == 0)
               {
                    if(branchNode.Value == null)
                    {
                        return null;
                    }
                    return storage;
                }
                return GenerateProof(branchNode.Children[keyAsNibbles[0]], keyAsNibbles.Skip(1).ToArray(), storage);
            }

            if (currentNode is ExtendedNode extendedNode)
            {
                var foundSameNibbles = extendedNode.Nibbles.FindAllTheSameBytesFromTheStart(keyAsNibbles);
                if(foundSameNibbles.Length < extendedNode.Nibbles.Length)
                {
                    return null;
                }

                return GenerateProof(extendedNode.InnerNode, keyAsNibbles.Skip(foundSameNibbles.Length).ToArray(), storage);
            }

            return storage;
        }
    }
}
