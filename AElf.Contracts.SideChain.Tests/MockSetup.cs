﻿    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using AElf.ChainController;
    using AElf.Execution;
    using AElf.Kernel;
    using AElf.Kernel.Managers;
    using AElf.Kernel.Storages;
    using AElf.SmartContract;
    using Google.Protobuf;
    using ServiceStack;
    
    namespace AElf.Contracts.SideChain.Tests
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
    
            public IStateDictator StateDictator { get; }
            private IChainCreationService _chainCreationService;
    
            private ISmartContractRunnerFactory _smartContractRunnerFactory;
    
            public MockSetup(IStateDictator stateDictator, IChainCreationService chainCreationService, DataStore dataStore, IChainContextService chainContextService, IFunctionMetadataService functionMetadataService, ISmartContractRunnerFactory smartContractRunnerFactory)
            {
                StateDictator = stateDictator;
                _chainCreationService = chainCreationService;
                _functionMetadataService = functionMetadataService;
                _smartContractRunnerFactory = smartContractRunnerFactory;
                SmartContractManager = new SmartContractManager(dataStore);
                Task.Factory.StartNew(async () =>
                {
                    await Init();
                }).Unwrap().Wait();
                SmartContractService = new SmartContractService(SmartContractManager, _smartContractRunnerFactory, StateDictator, _functionMetadataService);
    
                new ServicePack()
                {
                    ChainContextService = chainContextService,
                    SmartContractService = SmartContractService,
                    ResourceDetectionService = null,
                    StateDictator = StateDictator
                };
            }
    
            public byte[] SideChainCode
            {
                get
                {
                    byte[] code = null;
                    using (FileStream file = File.OpenRead(Path.GetFullPath("../../../../AElf.Contracts.SideChain/bin/Debug/netstandard2.0/AElf.Contracts.SideChain.dll")))
                    {
                        code = file.ReadFully();
                    }
                    return code;
                }
            }
            
            public byte[] SCZeroContractCode
            {
                get
                {
                    byte[] code = null;
                    using (FileStream file = File.OpenRead(Path.GetFullPath("../../../../AElf.Contracts.Genesis/bin/Debug/netstandard2.0/AElf.Contracts.Genesis.dll")))
                    {
                        code = file.ReadFully();
                    }
                    return code;
                }
            }
            
            private async Task Init()
            {
                var reg1 = new SmartContractRegistration
                {
                    Category = 0,
                    ContractBytes = ByteString.CopyFrom(SideChainCode),
                    ContractHash = SideChainCode.CalculateHash(),
                    Type = (int)SmartContractType.SideChainContract
                };
                var reg0 = new SmartContractRegistration
                {
                    Category = 0,
                    ContractBytes = ByteString.CopyFrom(SCZeroContractCode),
                    ContractHash = SCZeroContractCode.CalculateHash(),
                    Type = (int)SmartContractType.BasicContractZero
                };
    
                var chain1 =
                    await _chainCreationService.CreateNewChainAsync(ChainId1,
                        new List<SmartContractRegistration> {reg0, reg1});
                StateDictator.ChainId = ChainId1;
                StateDictator.GetAccountDataProvider(
                    ChainId1.OfType(HashType.AccountZero));
            }
            
            public async Task<IExecutive> GetExecutiveAsync(Hash address)
            {
                var executive = await SmartContractService.GetExecutiveAsync(address, ChainId1);
                return executive;
            }
        }
    }
