using ProtoBuf;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class Node
    {
        [ProtoMember(1)] public int ID { get; }
        [ProtoMember(2)] public double X { get; }
        [ProtoMember(3)] public double Y { get; }
        [ProtoMember(4)] public double Z { get; }

        [ProtoMember(5)] public List<int> EList;           // List of Elements that contain this Node
        [ProtoMember(6)] public int[] DOF { get; set; }     // Degrees of Freedom array

        [ProtoMember(7)] private List<double> DispX;
        [ProtoMember(8)] private List<double> DispY;
        [ProtoMember(9)] private List<double> DispZ;
                         public double[] dU;
                         public double[] dU_buffer;

        public Node(string input)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");  // Set format - comma issue in floats

            List<string> data = new List<string>();  // Initialize list to store 8-char columns

            for (int i = 0; i < input.Length / 8; i++)  // Catch 8-char substrings form line
            {
                string text = input.Substring(i * 8, 8).Replace(" ", "");  // Remove whitespace chars

                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Nastran .bdf format can use scientific notation without "e" (only "-" or "+")
                    // Issue for double.Parse()
                    // Substring may start with "-" for negative number

                    if (!text.Contains("e") && !text.Contains("E"))  // if not contains "e" or "E"
                    {
                        if (text.Substring(1).Contains("-"))  // if "e-" is required
                        {
                            if (text[0] == '-')  // if number is negative ("-" at start)
                            {
                                text = "-" + text.Substring(1).Replace("-", "e-");
                            }
                            else text = text.Replace("-", "e-"); // if number is positive
                        }

                        if (text.Substring(1).Contains("+")) // if "e+" is required
                        {
                            text.Replace("+", "e+");
                        }
                    }

                    // Also it's possible that number starts with "." (without 0) in .bdf format - must be added to avoid exception
                    if (text[0] == '.') text = "0" + text;

                    data.Add(text); // Add modified string to list
                }
            }

            // Assign properties
            ID = int.Parse(data[1]);    // Node ID - identification for Elements
            X = double.Parse(data[2], CultureInfo.InvariantCulture);  // X position
            Y = double.Parse(data[3], CultureInfo.InvariantCulture);  // Y position
            Z = double.Parse(data[4], CultureInfo.InvariantCulture);  // Z position

            // Initialize Element List and DoF array
            EList = new List<int>();
            Initialize_EList();
            DOF = new int[3];

            // Initialize Displacement list and add Disp at time 0
            DispX = new List<double> { 0 };
            DispY = new List<double> { 0 };
            DispZ = new List<double> { 0 };
        }

        /// <summary>
        /// Initialize Element list that contain this Node.
        /// <br>If it was empty before Serializing then not pass through Protocol Buffers.</br>
        /// </summary>
        public void Initialize_EList()
        {
            EList = new List<int>();
        }

        /// <summary>
        /// Node displacement initialization at time 0
        /// <br>Reset Node displacement list</br>
        /// </summary>
        public void Initialize_StepZero()
        {
            // Initialize Displacement list and add Disp at time 0
            DispX = new List<double> { 0 };
            DispY = new List<double> { 0 };
            DispZ = new List<double> { 0 };
            dU = new double[3];
            dU_buffer = new double[3];
        }

        /// <summary>
        /// Initialize Displacement table for new increment.
        /// Displacements at the end of previous increment are used.
        /// </summary>
        public void Initialize_NewDisp(int inc)
        {
            DispX.Add(DispX[inc - 1]);
            DispY.Add(DispY[inc - 1]);
            DispZ.Add(DispZ[inc - 1]);
            dU = new double[3];
            dU_buffer = new double[3];
        }

        /// <summary>
        /// Get Node displacement at the end of increment
        /// <list>
        /// <item><c>inc</c></item>
        /// <description> - increment number </description>
        /// <item><c>dir</c></item>
        /// <description> - direction: X=0; Y=1; Z=2</description>
        /// </list>
        /// </summary>
        /// <returns>Value of displacement as double</returns>
        public double GetDisp(int inc, int dir)
        {
            double output = 0;
            if (dir == 0) output = DispX[inc];
            if (dir == 1) output = DispY[inc];
            if (dir == 2) output = DispZ[inc];

            return output;
        }

        public double[] GetDispRange(int dir)
        {
            double min = 0;
            double max = 0;
            if (dir == 0)
            {
                foreach (double disp in DispX)
                {
                    if (min > disp) min = disp;
                    if (max < disp) max = disp;
                }
            }
            if (dir == 1)
            {
                foreach (double disp in DispY)
                {
                    if (min > disp) min = disp;
                    if (max < disp) max = disp;
                }
            }
            if (dir == 2)
            {
                foreach (double disp in DispZ)
                {
                    if (min > disp) min = disp;
                    if (max < disp) max = disp;
                }
            }
            return new double[2] { min, max };
        }

        public void SetDisp(int inc, double[] disp)
        {
            DispX[inc] = disp[0];
            DispY[inc] = disp[1];
            DispZ[inc] = disp[2];
        }

        public void Update_Displacement(int inc)
        {
            DispX[inc] += dU_buffer[0];
            DispY[inc] += dU_buffer[1];
            DispZ[inc] += dU_buffer[2];
        }

        /// <returns>
        /// Number of analysis increments (results)
        /// </returns>
        public int GetIncrementNumb()
        {
            return DispX.Count - 1;
        }

        /// <summary>
        /// Add Element ID to list
        /// </summary>
        public void AddElement(int EID)
        {
            EList.Add(EID);
        }

        /// <summary>
        /// Removes duplicated Element ID from Element list
        /// </summary>
        public void RemoveElemDuplicates()
        {
            EList = EList.Distinct().ToList();
        }

        /// <returns>
        /// Number of Elements that contains this Node
        /// </returns>
        public List<int> GetElements()
        {
            return EList;
        }

        /// <summary>
        /// Assign DOF to Node
        /// </summary>
        public void SetDOF(int index)
        {
            DOF[0] = 3 * index;
            DOF[1] = 3 * index + 1;
            DOF[2] = 3 * index + 2;
        }

        public void ClearResults()
        {
            DispX = null;
            DispY = null;
            DispZ = null;
        }

    }
}
