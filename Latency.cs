using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriorityCacheSimulator
{
    class Latency
    {
        public int Priority;
        public int Value;
    }

    //Same thing as latency but different name just for clarity's sake
    class MemoryWait
    {
        public int Priority;
        public int Value;
    }
}
