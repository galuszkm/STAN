using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class Database
    {
        [ProtoMember(1)] public Dictionary<int, Node> NodeLib { get;}
        [ProtoMember(2)] public Dictionary<int, Element> ElemLib { get; }
        [ProtoMember(3)] public Dictionary<int, Material> MatLib { get; set; }
                         public Dictionary<int, Part> PartLib { get; set; }
        [ProtoMember(4)] public Dictionary<int, BoundaryCondition> BCLib { get; set; }
                         public FE_Library FELib { get; set; }
                         private List<string> Import_Error;
        [ProtoMember(5)] public int nDOF;
        [ProtoMember(6)] public Analysis AnalysisLib {get; set;}
        [ProtoMember(7)] public Information Info { get; set; }


        public Database()
        {
            // ============================ Initialize ==================================

            NodeLib = new Dictionary<int, Node>();           // Node library - key is Node ID, value is Node object
            ElemLib = new Dictionary<int, Element>();        // Element library - key is Element ID, value is Element object
            MatLib = new Dictionary<int, Material>();        // Material library - key is Mat ID, value is Material object
            BCLib = new Dictionary<int,BoundaryCondition>(); // Boundary Conditions library
            FELib = new FE_Library();                        // Finite Element library - key is FE type, value is FE_Library object
            PartLib = new Dictionary<int, Part>();           // Part library - key is Part ID, value is Part object
            AnalysisLib = new Analysis();                    // Analysis library - analysis settings
            Import_Error = new List<string>();               // List with import errors
            Info = new Information();                        // Additional info to serialize
        }

        public void ReadNastranMesh(string path)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

            //  ==================== List of element type supported ========================
            List<string> Elem_types_allowed = new List<string>
            {
                "CHEXA",
                //"CTETRA"
            };

            //  ========================= Read file ===================================
            string[] data = File.ReadAllLines(path);

            for (int i = 0; i < data.Length; i++)       // Find lines in text file
            {
                if (data[i].StartsWith("$") == false)   // Skip commented lines
                {
                    //  ----------------------- Find elements -------------------------
                    if (Elem_types_allowed.Any(s => data[i].Contains(s)))  // Check if element type is supported
                    {
                        string temp = data[i];   // Temporary string to collect text line

                        for (int j = i + 1; j < data.Length; j++)  // Check next lines (usually starts with + or whitespace)
                        {
                            if (data[j].StartsWith("+") || data[j].StartsWith(" "))
                            {
                                temp += data[j];  // Append next line
                                i = j;            // Increase i - to skip this line in outer loop
                            }
                            else break;           // Break inner loop otherwise
                        }

                        try
                        {
                            Element E = new Element(temp);   // Try to create element based on collected lines
                            ElemLib.Add(E.ID, E);            // Add new element to Element Library
                        }
                        catch
                        {
                            Import_Error.Add(temp);          // Catch error
                        }
                    }

                    //  ----------------------- Find nodes -------------------------
                    if (data[i].StartsWith("GRID"))
                    {
                        try
                        {
                            Node N = new Node(data[i]);   // Try to create element based on collected lines
                            NodeLib.Add(N.ID, N);                // Add new node to Node Library
                        }
                        catch
                        {
                            Import_Error.Add(data[i]);
                        }
                    }
                }

            }

            // Create list with Parts - using LINQ select PID of all elements, remove duplicates and sort
            List<int> PartList = ElemLib.Values.Select(x => x.PID).Distinct().ToList();
            PartList.Sort();

            // Create Part objects and add to Part Library
            foreach (int pid in PartList)
            {
                Part NewPart = new Part(pid);
                NewPart.CreatePart(NodeLib, ElemLib);
                PartLib.Add(pid, NewPart);
            }
        }

        public int Get_nDoF()
        {
            return nDOF;
        }

        public void AddMat(int id, Material Mat)
        {
            MatLib.Add(id, Mat);
        }

        public string Database_Summary()
        {
            string summary = "";
            summary += "\n  ==================   DATABASE SUMMARY   ==================";
            summary += "\n   Number of nodes:".PadRight(25) + NodeLib.Count.ToString().PadLeft(31);
            summary += "\n   Number of elements:".PadRight(25) + ElemLib.Count.ToString().PadLeft(31);
            summary += "\n   Number of DoF:".PadRight(25) + nDOF.ToString().PadLeft(31);
            summary += "\n  ========================================================== \n";

            return summary;
        }

        public void Set_nDOF()
        {
            nDOF = NodeLib.Count * 3;
        }

        public void AssignDOF()
        {
            // Initialize Node EList - if empty before Serializing than not pass through Protocol Buffers
            foreach (Node n in NodeLib.Values)
            {
                n.Initialize_EList();
            }

            // Add Elements to Node EList
            foreach (Element E in ElemLib.Values)
            {
                E.AddElem2Nodes(NodeLib);
            }

            // Remove duplicated Element IDs in Node EList
            foreach (Node n in NodeLib.Values)
            {
                n.RemoveElemDuplicates();
            }

            // Dictionary with neighbor nodes
            Dictionary<int, List<int>> Neighbors = new Dictionary<int, List<int>>();
            foreach (Node N in NodeLib.Values)
            {
                List<int> N_neighbors = new List<int>();  // List of Node n neighbors
                foreach (int E in N.GetElements())  // Get Element E that contains Node N
                {
                    foreach (int NID in ElemLib[E].NList)  // Get Nodes NID of Element E
                    {
                        N_neighbors.Add(NID);  // Add Node NID to neighbors of Node N 
                    }
                }
                N_neighbors = N_neighbors.Distinct().ToList(); // Remove duplicates
                N_neighbors.Remove(N.ID);  // Remove Node N form its neighbors 

                Neighbors.Add(N.ID, N_neighbors);  // Add Node N to dict
            }

            // Find some peripheral Node
            int FirstNode = 0;
            bool GoOn = true;
            for (int i = 1; i < 7; i++)
            {
                if (GoOn)
                {
                    foreach (Node n in NodeLib.Values)
                    {
                        if (n.GetElements().Count == i)
                        {
                            FirstNode = n.ID;
                            GoOn = false;
                            break;
                        }
                    }
                }
                else break;
            }

            // Assign DOF index to Node
            int index = 0;

            // Create Node dict with data if Node has assigned DOF
            Dictionary<int, bool> NID_Done = new Dictionary<int, bool>();
            foreach (int NID in NodeLib.Keys)
            {
                NID_Done.Add(NID, false);
            }

            // Start
            NodeLib[FirstNode].SetDOF(index);   // Set DOF of First Node
            index++;                            // Increase DOF index
            NID_Done[FirstNode] = true;         // Set First Node as done

            List<int> NextNode = Neighbors[FirstNode];  // List with next nodes to do

            int index2 = 0;
            while (index < NodeLib.Count)
            {
                int NID = NextNode[index2];
                if (NID_Done[NID] == false)
                {
                    NodeLib[NID].SetDOF(index);
                    NID_Done[NID] = true;
                    index++;
                    foreach (int n in Neighbors[NID])
                    {
                        if (NID_Done[n] == false)
                        {
                            NextNode.Add(n);
                        }
                    }
                }
                index2++;
            }
        }

        public double[] GetBounds()
        {
            List<double> X = new List<double>();
            List<double> Y = new List<double>();
            List<double> Z = new List<double>();

            for (int inc = 0; inc <= AnalysisLib.GetResultStepNo(); inc++)
            {
                foreach (Node N in NodeLib.Values)
                {
                    X.Add(N.X + N.GetDisp(inc, 0));
                    Y.Add(N.Y + N.GetDisp(inc, 1));
                    Z.Add(N.Z + N.GetDisp(inc, 2));
                }
            }

            double[] bounds = new double[6]
            {
                X.Min(),  X.Max(), Y.Min(), Y.Max(), Z.Min(), Z.Max()
            };
            return bounds;
        }
    }
}
