using System.Threading.Tasks;
using AElf.ChainController;
using AElf.Kernel;

namespace AElf.Miner.Miner
{
    public interface IBlockExecutor
    {
        Task<ExecutionResult> ExecuteBlock(IBlock block);
        void Start();
    }
}