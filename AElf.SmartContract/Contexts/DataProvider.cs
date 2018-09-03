using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

// ReSharper disable once CheckNamespace
namespace AElf.SmartContract
{
    public class DataProvider : IDataProvider
    {
        private readonly DataPath _dataPath;
        private readonly IStateDictator _stateDictator;

        public IEnumerable<StateValueChange> GetValueChanges()
        {
            var changes = new List<StateValueChange>();
            foreach (var keyState in StateCache)
            {
                changes.Add(new StateValueChange
                {
                    Path = keyState.Key,
                    CurrentValue = ByteString.CopyFrom(keyState.Value.CurrentValue ?? new byte[0])
                });
            }

            return changes;
        }

        private int Layer { get; }

        /// <inheritdoc />
        /// <summary>
        /// DataPath - StateCache
        /// </summary>
        public Dictionary<DataPath, StateCache> StateCache { get; set; } = new Dictionary<DataPath, StateCache>();

        public void ClearCache()
        {
            StateCache.Clear();
        }

        /// <summary>
        /// To dictinct DataProviders of same account and same level.
        /// Using a string value is just a choise, actually we can use any type of value, even integer.
        /// </summary>
        private readonly string _dataProviderKey;

        public DataProvider(DataPath dataPath, IStateDictator stateDictator, int layer = 0,
            string dataProviderKey = "")
        {
            _stateDictator = stateDictator;
            _dataProviderKey = dataProviderKey;
            Layer = layer;
            _dataPath = dataPath.Clone();

            _dataPath.SetDataProvider(GetHash());
        }

        private Hash GetHash()
        {
            return new StringValue {Value = _dataProviderKey}.CalculateHashWith(Layer);
        }

        /// <inheritdoc />
        /// <summary>
        /// Get a child DataProvider.
        /// </summary>
        /// <param name="dataProviderKey">child DataProvider's name</param>
        /// <returns></returns>
        public IDataProvider GetDataProvider(string dataProviderKey)
        {
            return new DataProvider(_dataPath, _stateDictator, 1, dataProviderKey)
            {
                StateCache = StateCache
            };
        }

        public async Task<byte[]> GetAsync<T>(Hash keyHash) where T : IMessage, new()
        {
            return GetStateAsync(keyHash)?.CurrentValue ?? (await GetDataAsync<T>(keyHash))?.ToByteArray();
        }

        public async Task SetAsync<T>(Hash keyHash, byte[] obj) where T : IMessage, new()
        {
            var dataPath = _dataPath.Clone();
            dataPath.SetDataKey(keyHash);
            if (!dataPath.AreYouOk())
            {
                throw new InvalidOperationException("DataPath: I'm not OK.");
            }
            
            dataPath.Type = typeof(T).Name;
            
            await _stateDictator.SetHashAsync(dataPath.ResourcePathHash, dataPath.ResourcePointerHash);

            StateCache[dataPath] = new StateCache(obj);
        }
        
        private StateCache GetStateAsync(Hash keyHash)
        {
            var dataPath = _dataPath.Clone();
            dataPath.SetDataKey(keyHash);
            if (!dataPath.AreYouOk())
            {
                throw new InvalidOperationException("DataPath: I'm not OK.");
            }

            if (StateCache.TryGetValue(dataPath, out var state))
            {
                return state;
            }
            
            return null;;
        }

        /// <inheritdoc />
        /// <summary>
        /// Directly set data to database instead of push it to cache.
        /// </summary>
        /// <param name="keyHash"></param>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="T:System.InvalidOperationException">If the DataPath is not ready.</exception>
        public async Task SetDataAsync<T>(Hash keyHash, T obj) where T : IMessage, new()
        {
            var dataPath = _dataPath.Clone();
            dataPath.SetDataKey(keyHash);
            if (!dataPath.AreYouOk())
            {
                throw new InvalidOperationException("DataPath: I'm not OK.");
            }
            
            //Directly set to database.
            await _stateDictator.SetDataAsync(dataPath.ResourcePointerHash, obj);
            //Set path hash - pointer hash.
            await _stateDictator.SetHashAsync(dataPath.ResourcePathHash, dataPath.ResourcePointerHash);
        }
        
        /// <summary>
        /// Get data from database.
        /// </summary>
        /// <param name="keyHash"></param>
        /// <returns></returns>
        public async Task<T> GetDataAsync<T>(Hash keyHash) where T : IMessage, new()
        {
            var dataPath = _dataPath.Clone();
            dataPath.SetDataKey(keyHash);
            if (!dataPath.AreYouOk())
            {
                throw new InvalidOperationException("DataPath: I'm not OK.");
            }
            
            //Get resource pointer.
            var pointerHash = await _stateDictator.GetHashAsync(dataPath.ResourcePathHash);
            if (pointerHash == null)
            {
                return default(T);
            }
            
            return await _stateDictator.GetDataAsync<T>(pointerHash);
        }
    }
}