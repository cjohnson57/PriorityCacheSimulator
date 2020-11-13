using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;

namespace PriorityCacheSimulator
{
    public partial class Form1 : Form
    {

        const int MemBlocks = 10000;
        const int CoreCount = 4;
        const int ChangeShareInterval = 100;

        Random rand = new Random();


        /* Algorithms:
         * 1. Static 25% each (Control, no priority)
         * 2. Need-based (Control, like dynamic but no priority)
         * 3. Basic Priority (First method, high priority takes as much as they want)
         * 4. Static Priority (Second method, high priority gets higher percentage)
         * 5. Modified Static Priority (Unused share of high priority can be used by other processes)
         * 6. Dynamic Priority (Final method: considers previous share, priority, and miss rate to calculate new share)
         */
        int Algorithm = 1;

        public Form1()
        {
            InitializeComponent();
        }

        //Run the simulation
        public string RunSimulation()
        {
            rand = new Random();
            string processtext = File.ReadAllText("processes.json");
            //Get all processes from the json file
            List<Process> processes = JsonConvert.DeserializeObject<List<Process>>(processtext);
            int processcount = processes.Count();
            //Initialize list of cores
            List<Core> cores = new List<Core>();
            for(int i = 0; i < CoreCount; i++)
            {
                Core newcore = new Core();
                newcore.CurrentMemoryBlocks = MemBlocks / CoreCount; //Init memblocks to equally divided portion
                SetCore(processes.First(), ref newcore); //Set core with process and initial memblocks
                cores.Add(newcore); //Add to list of cores
                processes.RemoveAt(0); //Remove first process which was just assigned to the core
            }

            int interval = ChangeShareInterval;
            int cyclestaken = 0;
            List<Latency> latencylist = new List<Latency>();
            List<MemoryWait> memorywaitlist = new List<MemoryWait>();

            //Each loop here is considered one cycle
            while(cores.Where(x => x.CurrentProcess.PID == -1).Count() < CoreCount) //PID = -1 -> no process, so wait until all cores have no process
            {
                //Interval has passed, must re-evaluate cache shares
                if(interval == 0)
                {

                    interval = ChangeShareInterval;
                    cores = AdjustShares(cores);
                }
                else
                {
                    interval--;
                }
                //Simulate computation
                for(int i = 0; i < CoreCount; i++)
                {
                    Core c = cores[i]; //Copy core from list

                    cores[i] = c; //Replace modified core
                }
                cyclestaken++;
            }
            //Build output string
            string output = "Algorithm: " + GetAlgorithmName(Algorithm) + Environment.NewLine;
            output += "Processes: " + processcount + Environment.NewLine;
            output += "Cycles taken: " + cyclestaken + Environment.NewLine;
            output += "Throughput: " + ((double)processcount * 1000 / cyclestaken).ToString("N4") + " processes per thousand cycles" + Environment.NewLine;
            output += "Priority\t\t1\t2\t3\t4\t5\tTotal" + Environment.NewLine;
            //Add average latency for each priority
            output += "Avg. Latency";
            for(int i = 1; i <= 5; i++)
            {
                output += "\t" + latencylist.Where(x => x.Priority == i).Average(x => x.Value).ToString("N2");
            }
            output += "\t" + latencylist.Average(x => x.Value).ToString("N2") + Environment.NewLine;
            //Add average cycles spent waiting for memory for each priority
            output += "Avg. Waiting Cycles";
            for (int i = 1; i <= 5; i++)
            {
                output += "\t" + memorywaitlist.Where(x => x.Priority == i).Average(x => x.Value).ToString("N2");
            }
            output += "\t" + memorywaitlist.Average(x => x.Value).ToString("N2") + Environment.NewLine;
            output += "-----------------------------------------------------------------------------------------------------------------------------------------------------------" + Environment.NewLine;
            return output;
        }

        private Core SimulateCore(Core c)
        {
            if (c.MemoryWaitCycles == 0) //No memory wait cycles and no value needed, complete 1 computation cycle
            {
                //Do computation
                c.RemainingComputationCycles--;
                //If computation is finished, then set a new process
                if (c.RemainingComputationCycles == 0)
                {
                    //Add latency to list
                    Latency l = new Latency();
                    l.Priority = c.CurrentProcess.Priority;
                    l.Value = c.CyclesSinceStart;
                    latencylist.Add(l);
                    //Add cycles spent waiting for memory to list
                    MemoryWait m = new MemoryWait();
                    m.Priority = c.CurrentProcess.Priority;
                    m.Value = c.CyclesSinceStart - c.CurrentProcess.ComputationCycles; //Total cycles - computation cycles should be how many cycles were spent waiting for memory
                    memorywaitlist.Add(m);
                    //Set new process, if one is available
                    Process newprocess = new Process();
                    if (processes.Count > 0)
                    {
                        newprocess = processes.First();
                        processes.RemoveAt(0);
                    }
                    SetCore(newprocess, ref c);
                }
                //Set memory wait
                bool incache = CacheOrMemory(c); //True = cache, false = memory
                c.MemoryWaitCycles = incache ? 1 : 10; //Cache takes 1 cycle to retrieve, memory takes 10
                                                       //Increment either hit or miss count
                if (incache)
                {
                    c.HitCount++;
                }
                else
                {
                    c.MissCount++;
                }
            }
            else if (c.MemoryWaitCycles > 0) //Need to continue waiting for memory
            {
                c.MemoryWaitCycles--;
            }
            c.CyclesSinceStart++;

            return c;
        }

        //Adjust the share for each core depending on the selected algorithm
        private List<Core> AdjustShares(List<Core> cores)
        {
            switch(Algorithm)
            {
                case 1:
                    cores = Static25(cores);
                    break;
                case 2:
                    cores = NeedBased(cores);
                    break;
                case 3:
                    cores = BasicPriority(cores);
                    break;
                case 4:
                    cores = StaticPriority(cores);
                    break;
                case 5:
                    cores = ModifiedStaticPriority(cores);
                    break;
                case 6:
                    cores = DynamicPriority(cores);
                    break;
            }
            return cores;
        }

        //Each core retains a 25% share
        //Cores are initialized this way, so nothing must be done
        private List<Core> Static25(List<Core> cores)
        {
            return cores;
        }

        private List<Core> NeedBased(List<Core> cores)
        {
            return DynamicAlgorithm(cores, false);
        }

        //Highest priority threads get as many shares as they want
        //If equal priority and they exceed the total, split equally among those
        private List<Core> BasicPriority(List<Core> cores)
        {
            int remainingblocks = MemBlocks;
            //Go from highest to lowest priority (5 to 1) assigning blocks as needed/split equally until all are assigned
            for(int i = 5; i > 0; i--)
            {
                if(remainingblocks == 0)
                {
                    //Will probably happen after highest priority process(s) unless their total needed count is below their share
                    //Just assign 0 to the rest of the processes
                    foreach (Core c in cores.Where(x => x.CurrentProcess.Priority == i))
                    {
                        c.CurrentMemoryBlocks = 0;
                    }
                }
                int numpriorityi = cores.Where(x => x.CurrentProcess.Priority == i).Count(); //number of blocks of this priority
                if(numpriorityi > 0) //Only look if there are processes of this priority
                {
                    int share = remainingblocks / numpriorityi; //Max share per block of this priority
                    foreach (Core c in cores.Where(x => x.CurrentProcess.Priority == i))
                    {
                        //Assign either this process's share or their max blocks needed and subtract from remaining blocks
                        int blocksassigned = c.CurrentProcess.MemoryBlocks < share ? c.CurrentProcess.MemoryBlocks : share;
                        c.CurrentMemoryBlocks = blocksassigned;
                        remainingblocks -= blocksassigned;
                    }
                }
            }
            return cores;
        }

        //Get percentage based on priority
        private List<Core> StaticPriority(List<Core> cores)
        {
            int totalblocks = 0;
            //Get total priority so that percentage can be computed
            int totalpriority = 0;
            foreach(Core c in cores)
            {
                totalpriority += c.CurrentProcess.Priority;
            }
            foreach(Core c in cores)
            {
                if(c == cores.Last()) //Set last core based on remaining number to account for rounding errors
                {
                    c.CurrentMemoryBlocks = MemBlocks - totalblocks; //Should be roughly the same as if calculated in the else block
                }
                else
                {
                    c.CurrentMemoryBlocks = MemBlocks * c.CurrentProcess.Priority / totalpriority; //Rounded to nearest int 
                }
                totalblocks += c.CurrentMemoryBlocks;
            }
            if(totalblocks != MemBlocks)
            {
                return new List<Core>(); //Indicate error
            }
            return cores;
        }

        //Get percentage based on priority, but if any are unneeded they're assigned to next highest priority thread
        private List<Core> ModifiedStaticPriority(List<Core> cores)
        {
            cores = StaticPriority(cores); //Start out using static priority
            //Calculate the remaining blocks from processes with a lower block count than they were assigned
            int remainingblocks = 0;
            foreach(Core c in cores)
            {
                if(c.CurrentProcess.MemoryBlocks < c.CurrentMemoryBlocks)
                {
                    remainingblocks += c.CurrentMemoryBlocks - c.CurrentProcess.MemoryBlocks; //Update how many free blocks remain
                    c.CurrentMemoryBlocks = c.CurrentProcess.MemoryBlocks; //Set current blocks to process's needed blocks
                }
            }
            //Go from highest to lowest priority (5 to 1) assigning remaining blocks as needed/split equally until all are assigned
            //You can see this loop is very similar to that in BasicPriority, except:
            //When remaining blocks are 0, it leaves block counts alone instead of setting them to 0
            //Instead of setting block counts, it only adds to them
            //Note that it is not unlikely that remainingblocks will equal 0 in the first place, which means the loop will immediately exit,
            //  and this will be equivalent to having just run StaticPriority.
            for (int i = 5; i > 0; i--)
            {
                if (remainingblocks == 0)
                {
                    break; //No need to keep going after this
                }
                int numpriorityi = cores.Where(x => x.CurrentProcess.Priority == i).Count(); //number of blocks of this priority
                if(numpriorityi > 0) //Only look if there are processes of this priority
                {
                    int share = remainingblocks / numpriorityi; //Max share per block of this priority
                    foreach (Core c in cores.Where(x => x.CurrentProcess.Priority == i))
                    {
                        int neededblocks = c.CurrentProcess.MemoryBlocks - c.CurrentMemoryBlocks;
                        if (neededblocks != 0)
                        {
                            //Assign either this process's share or their max blocks needed and subtract from remaining blocks
                            int blocksassigned = neededblocks < share ? neededblocks : share;
                            c.CurrentMemoryBlocks += blocksassigned;
                            remainingblocks -= blocksassigned;
                        }
                    }
                }
            }
            return cores;
        }

        //Modify each share based on miss rate and priority
        private List<Core> DynamicPriority(List<Core> cores)
        {
            return DynamicAlgorithm(cores, true);
        }

        //Modify each share dynamicall based on miss rate and, if specified, priority
        private List<Core> DynamicAlgorithm(List<Core> cores, bool usepriority)
        {
            List<double> missrates = new List<double>();
            //Find miss rate for each core
            foreach (Core c in cores)
            {
                missrates.Add((double)c.MissCount / (c.MissCount + c.HitCount));
            }
            double avgmissrate = missrates.Average();
            //Find difference between miss rate and average 
            //If higher than average, value is positive: Give more shares
            //If lower than average, value is negative: Take shares away
            List<double> oldpercentages = new List<double>();
            List<double> newpercentages = new List<double>();
            for (int i = 0; i < CoreCount; i++)
            {
                double oldpercent = (double)cores[i].CurrentMemoryBlocks / MemBlocks;
                oldpercentages.Add(oldpercent);
                //If using priority, then account for it here by raising or lowering average depending on priority, if not simply ignore
                //Priority mod is as follows:
                // 1    2    3    4    5
                //-6   -3    0    3    6
                double prioritymod = usepriority ? (cores[i].CurrentProcess.Priority - 3) * 3 : 0;
                double newpercent = oldpercent + missrates[i] - avgmissrate + prioritymod * .01;  //Priority mod must be converted to percentage
                newpercentages.Add(newpercent);
            }
            //Each % difference accounts for one % difference in shares
            //Calculate value of alpha that will ensure total value = 100% (1)
            //alpha*oldCore1 + (1-alpha)*newCore1 + alpha*oldCore2 + (1-alpha)*newCore2 + alpha*oldCore3 + (1-alpha)*newCore3 + alpha*oldCore4 + (1-alpha)*newCore4 = 1
            //At this point, all values known except alpha, which can be solved for using:
            //alpha = (1 - newCore1 - newCore2 - newCore3 - newCore3) / (oldCore1 - newCore1 + oldCore2 - newCore2 + oldCore3 - newCore3 + oldCore4 - newCore4)
            double alpha = (1.0 - newpercentages.Sum()) / (oldpercentages.Sum() - newpercentages.Sum());
            int remainingblocks = 10000;
            for (int i = 0; i < CoreCount; i++)
            {
                int share;
                if (i == CoreCount - 1) //Set last core based on remaining number to account for rounding errors
                {
                    share = remainingblocks; //Should be roughly the same as if calculated in the else block
                }
                else
                {
                    double newpercent = alpha * oldpercentages[i] + (1 - alpha) * newpercentages[i];
                    share = MemBlocks * (int)newpercent;
                }
                cores[i].CurrentMemoryBlocks = cores[i].CurrentProcess.MemoryBlocks < share ? cores[i].CurrentProcess.MemoryBlocks : share;
                remainingblocks -= cores[i].CurrentMemoryBlocks;
            }
            //Now, the method in which leftover space is assigned will depend on whether or not priority is considered
            if(remainingblocks > 0 && usepriority) //Use same priority method as other algorithms use
            {
                for (int i = 5; i > 0; i--)
                {
                    if (remainingblocks == 0)
                    {
                        break; //No need to keep going after this
                    }
                    int numpriorityi = cores.Where(x => x.CurrentProcess.Priority == i).Count(); //number of blocks of this priority
                    if (numpriorityi > 0) //Only look if there are processes of this priority
                    {
                        int share = remainingblocks / numpriorityi; //Max share per block of this priority
                        foreach (Core c in cores.Where(x => x.CurrentProcess.Priority == i))
                        {
                            int neededblocks = c.CurrentProcess.MemoryBlocks - c.CurrentMemoryBlocks;
                            if (neededblocks != 0)
                            {
                                //Assign either this process's share or their max blocks needed and subtract from remaining blocks
                                int blocksassigned = neededblocks < share ? neededblocks : share;
                                c.CurrentMemoryBlocks += blocksassigned;
                                remainingblocks -= blocksassigned;
                            }
                        }
                    }
                }
            }
            else if(remainingblocks > 0) //Simply divide evenly among processes with more needed blocks than assigned
            {
                int morethancachecount = cores.Where(x => x.CurrentProcess.MemoryBlocks > x.CurrentMemoryBlocks).Count();
                if(morethancachecount > 0) //Check for rare situation where all processes have as many blocks in cache as they need
                {
                    int extraeach = remainingblocks / morethancachecount;
                    foreach (Core c in cores.Where(x => x.CurrentProcess.MemoryBlocks > x.CurrentMemoryBlocks))
                    {
                        if (c == cores.Where(x => x.CurrentProcess.MemoryBlocks > x.CurrentMemoryBlocks).Last()) //If last element, assign remaining blocks to account for rounding errors
                        {
                            c.CurrentMemoryBlocks += remainingblocks; //Should roughly equal extra each
                        }
                        else
                        {
                            c.CurrentMemoryBlocks += extraeach;
                        }
                    }
                }
            }
            return cores;
        }

        //Provides whether the needed value is placed in cache or memory
        private bool CacheOrMemory(Core c)
        {
            double percentcache = (double)c.CurrentMemoryBlocks / c.CurrentProcess.MemoryBlocks; //Proportion of process's needed blocks that are within cache
            if (percentcache > 1)
            {
                percentcache = 1;
            }
            //Convert to percentage and then percentage * 100
            //ex, current blocks = 200 and total = 500, 200/500 = .4
            // .4 * 100 = 40, 40 * 100 = 4000
            //Max value = 10000, find random value from 0 to 10000 and compare to this value
            //This makes it so that roughly whatever percent of memory in cache is the change the needed value is in cache and vice versa
            //So using previous example, 40% of blocks are in cache, so there is a 40% chance of needing 1 cycle and 60% chance of needing 2 cycles
            percentcache *= 100 * 100;
            int randomvalue = rand.Next(0, MemBlocks + 1);
            return randomvalue <= (int)percentcache;
        }

        //Sets a core with a new process
        private void SetCore(Process p, ref Core c)
        {
            //Set process
            c.CurrentProcess = p;
            c.MemoryWaitCycles = 0;
            c.CyclesSinceStart = 0;
            c.RemainingComputationCycles = p.ComputationCycles;
            //Reset hit and miss rates
            c.MissCount = 0;
            c.HitCount = 0;
            //Leave current block alone
        }

        //Since radio buttons are not part of a list must just check each one
        private string GetAlgorithmName(int algo)
        {
            switch(algo)
            {
                case 1:
                    return "Static 25% each";
                case 2:
                    return "Need-based";
                case 3:
                    return "Basic Priority";
                case 4:
                    return "Static Priority";
                case 5:
                    return "Modified Static Priority";
                default: //case 6
                    return "Dynamic Priority";
            }


        }

        //Since radio buttons are not part of a list must just check each one
        private int GetAlgorithm()
        {
            if (radioButton1.Checked)
            {
                return 1;
            }
            else if (radioButton2.Checked)
            {
                return 2;
            }
            else if (radioButton3.Checked)
            {
                return 3;
            }
            else if (radioButton4.Checked)
            {
                return 4;
            }
            else if (radioButton5.Checked)
            {
                return 5;
            }
            else //radioButton6.Checked
            {
                return 6;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

            Algorithm = GetAlgorithm(); //Decide algorithm based on radio buttons
            string simulationoutput = RunSimulation(); //Run the simulation and get the output
            richTextBox1.Text += simulationoutput; //Append to text box
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Form2 frm2 = new Form2();
            frm2.Show();
        }

        //Scroll textbox when text written to it
        private void richTextBox_TextChanged(object sender, EventArgs e)
        {
            // set the current caret position to the end
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            // scroll it automatically
            richTextBox1.ScrollToCaret();
        }
    }
}
