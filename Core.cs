using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriorityCacheSimulator
{
    class Core
    {
        public Process CurrentProcess;
        public int RemainingComputationCycles;
        public int MemoryWaitCycles;
        public int CurrentMemoryBlocks;
        public int MissCount;
        public int HitCount;
        public int CyclesSinceStart;
    }
}
