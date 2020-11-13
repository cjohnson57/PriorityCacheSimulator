using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.IO;

namespace PriorityCacheSimulator
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        void GenerateInputs(string filepath, int togenerate)
        {
            List<Process> processes = new List<Process>();
            Random rand = new Random();
            for (int i = 1; i <= togenerate; i++)
            {
                Process p = new Process();
                p.PID = i;
                p.Priority = rand.Next(1, 6); //Priority in range [1, 5]
                p.MemoryBlocks = rand.Next(250, 4751); //Memory blocks in ragne [250, 4750]
                p.ComputationCycles = rand.Next(500, 1501); //Cycles in range [500, 1500]
                processes.Add(p);
            }
            string processesstring = JsonConvert.SerializeObject(processes); //Convert list to JSON string
            File.WriteAllText(filepath, processesstring); //Write JSON to file
        }

        private void button1_Click(object sender, EventArgs e)
        {
            GenerateInputs(textBox1.Text, (int)numericUpDown1.Value);
            MessageBox.Show("Inputs generated to " + textBox1.Text);
        }
    }
}
