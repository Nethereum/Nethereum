namespace Nethereum.Merkle
{
    /// <summary>
    /// Implmentation of OpenZeppelin's StandardMerkleTree, same as https://github.com/OpenZeppelin/merkle-tree/blob/master/src/standard.ts
    /// This can be used to create same proof and tree as OpenZeppelin's JavaScript library and can be used when verifying against their 
    /// standard implementation https://github.com/binodnp/openzeppelin-solidity/blob/master/contracts/cryptography/MerkleProof.sol 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OpenZeppelinStandardMerkleTree<T> : AbiStructSha3KeccackMerkleTree<T>
    {
    }
}
