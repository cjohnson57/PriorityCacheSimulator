using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriorityCacheSimulator
{
    class Process
    {
        public int PID;
        public int ComputationCycles;
        public int MemoryBlocks;
        public int Priority;

        //Constructor, initialize everything to -1
        public Process()
        {
            PID = -1;
            ComputationCycles = -1;
            MemoryBlocks = -1;
            Priority = -1;
        }
    }
}
