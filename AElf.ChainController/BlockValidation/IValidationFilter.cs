﻿using System.Threading.Tasks;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    public interface IValidationFilter
    {
        Task<ValidationResult> ValidateBlockAsync(IBlock block, IChainContext context, ECKeyPair keyPair);
    }
}