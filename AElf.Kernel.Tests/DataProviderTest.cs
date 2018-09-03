using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.SmartContract;
using AElf.ChainController;
using AElf.Kernel.Managers;
using AElf.Kernel.Storages;
using Google.Protobuf;
using NLog;
using Xunit;
using Xunit.Frameworks.Autofac;

namespace AElf.Kernel.Tests
{
    [UseAutofacTestFramework]
    public class DataProviderTest
    {
        private readonly IStateDictator _stateDictator;
        private readonly IDataStore _dataStore;
        private readonly BlockTest _blockTest;
        private readonly ILogger _logger;

        public DataProviderTest(IStateDictator stateDictator, DataStore  dataStore, BlockTest blockTest, ILogger logger)
        {
            _stateDictator = stateDictator;
            _dataStore = dataStore;
            _blockTest = blockTest;
            _logger = logger;
        }

        [Fact]
        public void StateCacheTest()
        {
            var accountDataProvider = _stateDictator.GetAccountDataProvider(Hash.Generate());
            var dataProvider = accountDataProvider.GetDataProvider();
            var subDataProvider = dataProvider.GetDataProvider("Test");
            Assert.Same(dataProvider.StateCache, subDataProvider.StateCache);
            Assert.Equal(0, dataProvider.StateCache.Count);
            Assert.Equal(0, subDataProvider.StateCache.Count);
            dataProvider.StateCache.Add(new DataPath(), new StateCache(Hash.Generate().ToByteArray()));
            Assert.Equal(1, dataProvider.StateCache.Count);
            Assert.Equal(1, subDataProvider.StateCache.Count);
            dataProvider.ClearCache();
            Assert.Equal(0, dataProvider.StateCache.Count);
            Assert.Equal(0, subDataProvider.StateCache.Count);
            subDataProvider.StateCache.Add(new DataPath(), new StateCache(Hash.Generate().ToByteArray()));
            Assert.Equal(1, dataProvider.StateCache.Count);
            Assert.Equal(1, subDataProvider.StateCache.Count);
        }

        private IEnumerable<byte[]> CreateSet(int count)
        {
            var list = new List<byte[]>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(Hash.Generate().GetHashBytes());
            }

            return list;
        }

        private IEnumerable<Hash> GenerateKeys(IEnumerable<byte[]> set)
        {
           return set.Select(data => new Hash(data.CalculateHash())).ToList();
        }
    }
}