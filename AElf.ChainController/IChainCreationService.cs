﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel;

namespace AElf.ChainController
{
    /// <summary>
    /// Create a new chain never existing
    /// </summary>
    public interface IChainCreationService
    {
        Task<IChain> CreateNewChainAsync(Hash chainId, List<SmartContractRegistration> smartContractZeros);
        Hash GenesisContractHash(Hash chainId, SmartContractType contractType);
    }
}