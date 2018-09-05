using System;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Types;

// ReSharper disable once CheckNamespace
namespace AElf.Kernel
{
    public interface IBlockHeader : IHashProvider
    {
        int Version { get; set; }
        Hash MerkleTreeRootOfTransactions { get; set; }
        ECSignature GetSignature();
    }
}