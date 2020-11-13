# PriorityCacheSimulator

This is a simulator to test 6 different algorithms.

The idea is for a hardware priority value that decides how much cache space is allocated to a thread. 

6 algorithm choices are provided:

1. Static 25% each (Control, no priority)
2. Need-based (Control, like dynamic but no priority)
3. Basic Priority (First method, high priority takes as much as they want)
4. Static Priority (Second method, high priority gets higher percentage)
5. Modified Static Priority (Unused share of high priority can be used by other processes)
6. Dynamic Priority (Final method: considers previous share, priority, and miss rate to
calculate new share)

The important code resides in [Form1.cs](https://github.com/cjohnson57/PriorityCacheSimulator/blob/master/Form1.cs)

## How to use

#### 1) Provide a .json file of Process objects.

One has been provided, but another could be generated:

- Open the program
  
- Click "Generate Inputs", another form will open
  
- Write desired file name and number to generate
  
Whatever the desired .json is, put it in the filepath field in the main program

#### 2) Select algorithm from radio list

#### 3) Click "Simulate"

#### 4) Results will be displayed in the large text box.

Further simulations will append to the text box so multiple results can be viewed at once.
