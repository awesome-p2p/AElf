﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Storages;
using AElf.Kernel.Managers;
using AElf.ChainController;
using AElf.SmartContract;
using AElf.Execution;
using Google.Protobuf;
using AElf.Kernel.Tests;

namespace AElf.Sdk.CSharp.Tests
{
    public class MockSetup
    {
        // IncrementId is used to differentiate txn
        // which is identified by From/To/IncrementId
        private static int _incrementId = 0;
        public ulong NewIncrementId()
        {
            var n = Interlocked.Increment(ref _incrementId);
            return (ulong)n;
        }

        public Hash ChainId1 { get; } = Hash.Generate();
        public ISmartContractManager SmartContractManager;
        public ISmartContractService SmartContractService;
        private IFunctionMetadataService _functionMetadataService;

        public IChainContextService ChainContextService;

        public IAccountDataProvider DataProvider1;

        public ServicePack ServicePack;

        public IStateDictator StateDictator { get; set; }
        private IChainCreationService _chainCreationService;

        private ISmartContractRunnerFactory _smartContractRunnerFactory;

        public MockSetup(IStateDictator stateDictator, IChainCreationService chainCreationService, IDataStore dataStore, IChainContextService chainContextService, IFunctionMetadataService functionMetadataService, ISmartContractRunnerFactory smartContractRunnerFactory)
        {
            StateDictator = stateDictator;
            _chainCreationService = chainCreationService;
            ChainContextService = chainContextService;
            _functionMetadataService = functionMetadataService;
            _smartContractRunnerFactory = smartContractRunnerFactory;
            SmartContractManager = new SmartContractManager(dataStore);
            Task.Factory.StartNew(async () =>
            {
                await Init();
            }).Unwrap().Wait();
            SmartContractService = new SmartContractService(SmartContractManager, _smartContractRunnerFactory, StateDictator, _functionMetadataService);

            ServicePack = new ServicePack()
            {
                ChainContextService = chainContextService,
                SmartContractService = SmartContractService,
                ResourceDetectionService = null,
                StateDictator = StateDictator
            };
        }

        public byte[] SmartContractZeroCode
        {
            get
            {
                return ContractCodes.TestContractZeroCode;
            }
        }
        
        private async Task Init()
        {
            var reg = new SmartContractRegistration
            {
                Category = 0,
                ContractBytes = ByteString.CopyFrom(SmartContractZeroCode),
                ContractHash = Hash.Zero
            };
            var chain1 = await _chainCreationService.CreateNewChainAsync(ChainId1, new List<SmartContractRegistration>{reg});

            StateDictator.ChainId = ChainId1;
            DataProvider1 = StateDictator.GetAccountDataProvider(ChainId1.OfType(HashType.AccountZero));
        }

        public async Task DeployContractAsync(byte[] code, Hash address)
        {
            var reg = new SmartContractRegistration
            {
                Category = 1,
                ContractBytes = ByteString.CopyFrom(code),
                ContractHash = new Hash(code)
            };

            await SmartContractService.DeployContractAsync(ChainId1, address, reg, false);
        }

        public async Task<IExecutive> GetExecutiveAsync(Hash address)
        {
            var executive = await SmartContractService.GetExecutiveAsync(address, ChainId1);
            return executive;
        }

    }
}
