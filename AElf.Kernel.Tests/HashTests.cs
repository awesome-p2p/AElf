using System.Collections.Generic;
using Xunit;

namespace AElf.Kernel.Tests
{
    public class HashTests
    {
        public static IEnumerable<object[]> RandomHashes
        {
            get
            {
                yield return new object[] {Hash.Generate(), Hash.Generate()};
                yield return new object[] {Hash.Generate(), Hash.Generate()};
                yield return new object[] {Hash.Generate(), Hash.Generate()};
                yield return new object[] {Hash.Generate(), Hash.Generate()};
            }
        }

        [Theory]
        [InlineData(new byte[] {10, 14, 1, 15}, new byte[] {10, 14, 1, 15})]
        public void EqualTest(Hash hash1, Hash hash2)
        {
            Assert.Equal(hash1, hash2);
        }

        [Theory]
        [InlineData(new byte[] {10, 14, 1, 15}, new byte[] {15, 1, 14, 10})]
        public void NotEqualTest(Hash hash1, Hash hash2)
        {
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void CompareTest()
        {
            var hash1 = new Hash(new byte[] {10, 14, 1, 15});
            var hash2 = new Hash(new byte[] {15, 1, 14, 10});
            
            Assert.True(new Hash().Compare(hash1, hash2) == -1);
        }

        [Fact]
        public void DictionaryTest()
        {
            var dict = new Dictionary<Hash, string>();
            var hash = new Hash(new byte[] {10, 14, 1, 15});
            dict[hash] = "test";
            
            var anotherHash = new Hash(new byte[] {10, 14, 1, 15});
            
            Assert.True(dict.TryGetValue(anotherHash, out var test));
            Assert.Equal("test", test);
        }

        [Theory]
        [MemberData(nameof(RandomHashes))]
        public void RandomHashTest(Hash hash1, Hash hash2)
        {
            Assert.False(hash1 == hash2);
        }
    }
}