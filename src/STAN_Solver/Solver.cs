using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using STAN_Database;

namespace STAN_Solver
{
    class Solver
    {
        public static Database DB;
        static SolverFunctions Fun = new SolverFunctions();
        static string Path { get; set; }

        static void Main(string[] path)
        {
            Fun.Welcome_Messsage();  // Print software data in Console

            //Reading Input file
            Console.Write("   Reading input file: ");
            Path = path[0];

            byte[] Input = File.ReadAllBytes(Path);
            DB = Fun.ProtoDeserialize<Database>(Input);

            // Create FELib - not passed through Protocol Buffers
            DB.FELib = new FE_Library();

            // Initialize Materials
            foreach(Material Mat in DB.MatLib.Values)
            {
                if(Mat.Type.Contains("Elastic"))
                {
                    Mat.SetElastic(Mat.E, Mat.Poisson);
                }
            }

            Console.WriteLine("     Done");


            // Assign DOF indexing to create band K matrix
            Console.Write("   DoF ordering: ");
            DB.AssignDOF();
            Console.WriteLine("           Done");

            // Database summary in Console
            Console.Write(DB.Database_Summary());

            // Run solver
            if (DB.AnalysisLib.GetAnalysisType() == "Linear_Statics")
            {
                SolverLinearStatics();
                DB.AnalysisLib.SetResultStepNo(1);
            }
            if (DB.AnalysisLib.GetAnalysisType() == "Nonlinear_Statics")
            {
                SolverNonlinearStatics();
            }

            // Export output file
            ExportOutput();

            // Wait few seconds
            Console.WriteLine("\n  Solver exit in 10s ...");
            System.Threading.Thread.Sleep(10000);
        }

        private static void SolverLinearStatics()
        {
            SolverFunctions Fun = new SolverFunctions();
            Stopwatch sw = new Stopwatch(); sw.Start();

            // Others
            int inc = 1;
            string separator = "  ========================================================== ";

            // Initialize time 0 and time 1
            foreach (Node N in DB.NodeLib.Values)
            {
                N.Initialize_StepZero();
                N.Initialize_NewDisp(inc);
            }
            foreach (Element E in DB.ElemLib.Values)
            {
                E.Initialize_StepZero(DB.FELib);
                E.Initialize_Increment(inc);
            }

            // Console paragraph
            Console.WriteLine("\n" + separator);
            Console.WriteLine("        LINEAR STATIC ANALYSIS ");
            Console.WriteLine(separator);

            // *****************************************************************************
            // ========================= MAIN FINITE ELEMENT CODE ==========================
            // *****************************************************************************

            //  ============================ Essential boundary conditions ======================================

            // DoF list with kinematic BC
            List<int> Fix_DOF = new List<int>();

            foreach (BoundaryCondition BC in DB.BCLib.Values.Where(x => x.Type == "SPC"))
            {
                foreach (int NID in BC.NodalValues.Keys)
                {
                    if (BC.NodalValues[NID].Get(0, 0) == 1) Fix_DOF.Add(DB.NodeLib[NID].DOF[0]);
                    if (BC.NodalValues[NID].Get(1, 0) == 1) Fix_DOF.Add(DB.NodeLib[NID].DOF[1]);
                    if (BC.NodalValues[NID].Get(2, 0) == 1) Fix_DOF.Add(DB.NodeLib[NID].DOF[2]);
                }
            }

            // Sort and remove duplicates of kinematic BC
            Fix_DOF = Fix_DOF.Distinct().ToList();
            Fix_DOF.Sort();

            // Define Row index reduction - rows and columns related to Dirichlet BC are removed from K matrix and F vector
            int[] nDOF_reduction = new int[DB.nDOF];
            for (int i = 0; i < Fix_DOF.Count; i++) nDOF_reduction[Fix_DOF[i]] = -1;

            int reduc = 0;
            for (int i = 0; i < DB.nDOF; i++)
            {
                if (nDOF_reduction[i] == -1)
                {
                    reduc++;
                }
                else nDOF_reduction[i] = reduc;
            }

            //  ============================ F (Right hand side) Vector =========================================

            double[] F = new double[DB.nDOF - Fix_DOF.Count];

            foreach (BoundaryCondition BC in DB.BCLib.Values.Where(x => x.Type == "PointLoad"))
            {
                foreach (int NID in BC.NodalValues.Keys)
                {
                    for (int dir = 0; dir < 3; dir++)
                    {
                        if (nDOF_reduction[DB.NodeLib[NID].DOF[dir]] != -1)
                        {
                            if (inc == 1)
                                F[DB.NodeLib[NID].DOF[dir] - nDOF_reduction[DB.NodeLib[NID].DOF[dir]]] +=
                                  BC.NodalValues[NID].Get(dir, 0);
                        }
                    }
                }
            }

            // ============================== GLOBAL STIFFNESS MATRIX ===================================

            alglib.sparsematrix K = Fun.ParallelAssembly_K(DB, nDOF_reduction, inc, "Initial");

            // ============================== SOLVING LINEAR SYSTEM ===================================

            double[] U = new double[DB.nDOF - Fix_DOF.Count];

            if (DB.AnalysisLib.GetLinSolver() == "CG")        U = Fun.LinearSolver_CG(K, F, DB.AnalysisLib);
            if (DB.AnalysisLib.GetLinSolver() == "Cholesky")  U = Fun.LinearSolver_Cholesky(K, F);
            if (DB.AnalysisLib.GetLinSolver() == "LU")        U = Fun.LinearSolver_LU(K, F);

            // ================== U (Left hand side) vector reconstruction =====================================

            double[] U_Full = Fun.Include_BC_DOF(U, nDOF_reduction);

            // Assign displacements to nodes (replace dU vector and update dU Buffer)
            foreach (Node n in DB.NodeLib.Values)
            {
                n.dU_buffer[0] = U_Full[n.DOF[0]];
                n.dU_buffer[1] = U_Full[n.DOF[1]];
                n.dU_buffer[2] = U_Full[n.DOF[2]];

                //Console.Write("Node " + n.ID + " disp: "); Fun.Print_Vector(n.dU_buffer, 3);
            }

            // Internal Forces vector
            double[] R = new double[DB.nDOF];

            Console.Write("   Stress recovery: ");
            Parallel.ForEach(DB.ElemLib.Values, Elem =>
            {
                Elem.Recovery_Stress(DB);
                Elem.Compute_NodalForces(DB);

                // Assembly Nodal Forces vector
                for (int i = 0; i < Elem.NList.Count; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        R[DB.NodeLib[Elem.NList[i]].DOF[j]] += Elem.NodalForces.GetFast(3 * i + j, 0);
                    }
                }
            });

            R = Fun.Exclude_BC_DOF(R, nDOF_reduction);
            Console.WriteLine("            Done");

            // Update nodal displacement, element stress and strain
            foreach (Node n in DB.NodeLib.Values)
            {
                n.Update_Displacement(inc);
            }
            foreach (Element E in DB.ElemLib.Values)
            {
                E.Update_StrainStress(inc);
            }

            // Print CPU time summary
            sw.Stop();
            Console.WriteLine("\n" + separator);
            Console.WriteLine("  Total CPU time: " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s");
            Console.WriteLine(separator);
        }

        private static void SolverNonlinearStatics()
        {
            SolverFunctions Fun = new SolverFunctions();
            Stopwatch sw = new Stopwatch(); sw.Start();

            // Analysis settings
            double tolerance = 0.001;

            // Others
            string separator = "  ========================================================== ";


            // Initialize time 0 and time 1
            foreach (Node N in DB.NodeLib.Values)
            {
                N.Initialize_StepZero();
            }
            foreach (Element E in DB.ElemLib.Values)
            {
                E.Initialize_StepZero(DB.FELib);
            }

            // Console paragraph
            Console.WriteLine("\n" + separator);
            if (DB.AnalysisLib.GetAnalysisType().StartsWith("Linear"))
            {
                Console.WriteLine("        LINEAR STATIC ANALYSIS ");
            }
            if (DB.AnalysisLib.GetAnalysisType().StartsWith("Nonlinear"))
            {
                Console.WriteLine("        NONLINEAR STATIC ANALYSIS ");
            }
            Console.WriteLine(separator);

            // *****************************************************************************
            // ========================= MAIN FINITE ELEMENT CODE ==========================
            // *****************************************************************************


            //  ============================ Essential boundary conditions ======================================

            // DoF list with kinematic BC
            List<int> Fix_DOF = new List<int>();

            foreach (BoundaryCondition BC in DB.BCLib.Values.Where(x => x.Type == "SPC"))
            {
                foreach (int NID in BC.NodalValues.Keys)
                {
                    if (BC.NodalValues[NID].Get(0, 0) == 1) Fix_DOF.Add(DB.NodeLib[NID].DOF[0]);
                    if (BC.NodalValues[NID].Get(1, 0) == 1) Fix_DOF.Add(DB.NodeLib[NID].DOF[1]);
                    if (BC.NodalValues[NID].Get(2, 0) == 1) Fix_DOF.Add(DB.NodeLib[NID].DOF[2]);
                }
            }

            // Sort and remove duplicates of kinematic BC
            Fix_DOF = Fix_DOF.Distinct().ToList();
            Fix_DOF.Sort();

            // Define Row index reduction - rows and columns related to Dirichlet BC are removed from K matrix and F vector
            int[] nDOF_reduction = new int[DB.nDOF];
            for (int i = 0; i < Fix_DOF.Count; i++) nDOF_reduction[Fix_DOF[i]] = -1;

            int reduc = 0;
            for (int i = 0; i < DB.nDOF; i++)
            {
                if (nDOF_reduction[i] == -1)
                {
                    reduc++;
                }
                else nDOF_reduction[i] = reduc;
            }


            //  >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>  INCREMENTAL ANALYSIS   <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

            for (int inc = 1; inc <= DB.AnalysisLib.GetIncNumb(); inc++)
            {
                Console.WriteLine("\n" + separator);
                Console.WriteLine("      INCREMENT " + inc);
                Console.WriteLine(separator);

                // Initialize Nodal displacements for new increment
                foreach (Node n in DB.NodeLib.Values)
                {
                    n.Initialize_NewDisp(inc);
                }

                // Initialize Element Strain and Stress for new increment
                foreach (Element Elem in DB.ElemLib.Values)
                {
                    Elem.Initialize_Increment(inc);
                }

                //  ============================ F (Right hand side) Vector =========================================

                double[] F = new double[DB.nDOF - Fix_DOF.Count];

                foreach (BoundaryCondition BC in DB.BCLib.Values.Where(x => x.Type == "PointLoad"))
                {
                    foreach (int NID in BC.NodalValues.Keys)
                    {
                        for (int dir = 0; dir < 3; dir++)
                        {
                            if (nDOF_reduction[DB.NodeLib[NID].DOF[dir]] != -1)
                            {
                                if (inc == 1)
                                    F[DB.NodeLib[NID].DOF[dir] - nDOF_reduction[DB.NodeLib[NID].DOF[dir]]] +=
                                      BC.NodalValues[NID].Get(dir, 0) * inc / DB.AnalysisLib.GetIncNumb();
                            }
                        }
                    }
                }

                // ================================    NEWTON-RAPHSON ITERATION   =====================================
                int iter = 0;
                double NormF = Fun.Vector_Norm(F);
                double NormRes = 1;

                // Iteration zero:
                Console.WriteLine("  ITERATION 0: ");
                alglib.sparsematrix K = Fun.ParallelAssembly_K(DB, nDOF_reduction, inc, "Initial");


                // Newton loop
                while (NormRes > tolerance)
                //while (iter<1)
                {
                    if (iter > 0)
                    {
                        Console.WriteLine("  ITERATION " + iter + ": ");
                        K = Fun.ParallelAssembly_K(DB, nDOF_reduction, inc, "Tangent");
                        Fun.Print_Matrix(K, 6, 8);
                    }

                    // ============================== SOLVING LINEAR SYSTEM ===================================

                    double[] U = new double[DB.nDOF - Fix_DOF.Count];
                    alglib.sparsematrix K1 = (alglib.sparsematrix) K.make_copy();

                    if (DB.AnalysisLib.GetLinSolver() == "CG")
                    {
                        U = Fun.LinearSolver_CG(K1, F, DB.AnalysisLib);
                    }

                    if (DB.AnalysisLib.GetLinSolver() == "Cholesky")
                    {
                        U = Fun.LinearSolver_Cholesky(K1, F);
                    }

                    if (DB.AnalysisLib.GetLinSolver() == "LU")
                    {
                        U = Fun.LinearSolver_LU(K1, F);
                    }

                    // ================== U (Left hand side) vector reconstruction =====================================

                    double[] U_Full = Fun.Include_BC_DOF(U, nDOF_reduction);

                    // Assign displacements to nodes (replace dU vector and update dU Buffer)
                    foreach (Node n in DB.NodeLib.Values)
                    {
                        n.dU[0] = U_Full[n.DOF[0]];   n.dU_buffer[0] += U_Full[n.DOF[0]];
                        n.dU[1] = U_Full[n.DOF[1]];   n.dU_buffer[1] += U_Full[n.DOF[1]];
                        n.dU[2] = U_Full[n.DOF[2]];   n.dU_buffer[2] += U_Full[n.DOF[2]];

                        //Console.Write("Node " + n.ID + " disp: ");  Fun.Print_Vector(n.dU_buffer, 3);
                    }

                    // Internal Forces vector
                    double[] R = new double[DB.nDOF];

                    Console.Write("   Stress recovery: ");
                    Parallel.ForEach(DB.ElemLib.Values, Elem =>
                    {
                        Elem.Recovery_Stress(DB);
                        Elem.Compute_NodalForces(DB);
                        
                        // Assembly Nodal Forces vector
                        for (int i = 0; i < Elem.NList.Count; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                R[DB.NodeLib[Elem.NList[i]].DOF[j]] += Elem.NodalForces.GetFast(3 * i + j, 0);
                            }
                        }
                    });
                    R = Fun.Exclude_BC_DOF(R, nDOF_reduction);
                    Console.WriteLine("            Done");

                    Fun.Print_MatrixST(DB.ElemLib[1].dS[0].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[1].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[2].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[3].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[4].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[5].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[6].Transpose(), 1, 6);
                    Fun.Print_MatrixST(DB.ElemLib[1].dS[7].Transpose(), 1, 6);
                    Console.WriteLine("\n Nodal forces:");
                    Fun.Print_Vector(R, R.Length);
                    Console.WriteLine("\n");

                    // Calculate Residual Forces
                    double[] Residual = new double[DB.nDOF - Fix_DOF.Count];

                    for (int i = 0; i < F.Length; i++)
                    {
                        Residual[i] =  F[i] - R[i];
                    }

                    // Calculate Residual Forces norm and extreme value
                    NormRes = Fun.Vector_Norm(Residual) / NormF;
                    double ExtremeResidual = Math.Max(Residual.Max(), Math.Abs(Residual.Min()));

                    Console.WriteLine("   Residual norm: " + NormRes.ToString("F4"));
                    Console.WriteLine("   Max residual force: " + ExtremeResidual.ToString("#0.0e+0") + "\n");
                    //Console.WriteLine("\nOut of balance forces:\n" + string.Join("\n", F.Where(x => x != 0)) + "\n");

                    // Next iteration
                    F = Residual;
                    iter++;
                }
                Console.WriteLine("  INCREMENT CONVERGED in " + iter + " iterations\n");

                // Update nodal displacement, element stress and strain
                foreach (Node N in DB.NodeLib.Values) N.Update_Displacement(inc);
                foreach (Element E in DB.ElemLib.Values) E.Update_StrainStress(inc);
            }

            sw.Stop();

            Console.WriteLine("\n" + separator);
            Console.WriteLine("  Total CPU time: " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s");
            Console.WriteLine(separator);
        }

        private static void ExportOutput()
        {
            string OutPath = Path;
            byte[] Export = Fun.ProtoSerialize(DB);
            using (var fs = new FileStream(OutPath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(Export, 0, Export.Length);
            }
        }

        private static void AssignDOF(Dictionary<int, Node> NodeLib, Dictionary<int, Element> ElemLib)
        {
            // Initialize Node EList - if empty before Serializing than not pass through Protovol Buffers
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

        private static void DebugSparseMatrix(Database DB)
        {
            // Initialize time 0 and 1
            int inc = 1;
            foreach (Node N in DB.NodeLib.Values) N.Initialize_StepZero();
            foreach (Element E in DB.ElemLib.Values) E.Initialize_StepZero(DB.FELib);

            foreach (Node n in DB.NodeLib.Values) n.Initialize_NewDisp(inc);
            foreach (Element Elem in DB.ElemLib.Values) Elem.Initialize_Increment(inc);

            AssignDOF(DB.NodeLib, DB.ElemLib);

            string output = "";
            Dictionary<int[], bool> mat = new Dictionary<int[], bool>();
            foreach (Element Elem in DB.ElemLib.Values)
            {
                // Assembly to Global Stiffness Matrix
                for (int i = 0; i < Elem.NList.Count; i++)
                {
                    for (int m = 0; m < 3; m++)
                    {
                        for (int j = 0; j < Elem.NList.Count; j++)
                        {
                            for (int n = 0; n < 3; n++)
                            {
                                int row = DB.NodeLib[Elem.NList[i]].DOF[m];
                                int col = DB.NodeLib[Elem.NList[j]].DOF[n];
                                //if (!mat.ContainsKey(new int[2] { row, col }))
                                //{
                                output += (row + 1).ToString() + "\t" + (col + 1).ToString() + "\t" + "1\n";
                                mat.Add(new int[2] { row, col }, true);
                                //}
                            }
                        }
                    }
                }
            }
            using (StreamWriter writer = new StreamWriter(@"C:\Users\Michal\Desktop\Solver\Band_Order.txt"))
            {
                writer.Write(output);
            }

            int fff = 0;
            foreach (Node n in DB.NodeLib.Values)
            {
                n.DOF = new int[3] { fff * 3, fff * 3 + 1, fff * 3 + 2 };
                fff++;
            }

            output = "";
            mat = new Dictionary<int[], bool>();
            foreach (Element Elem in DB.ElemLib.Values)
            {
                // Assembly to Global Stiffness Matrix
                for (int i = 0; i < Elem.NList.Count; i++)
                {
                    for (int m = 0; m < 3; m++)
                    {
                        for (int j = 0; j < Elem.NList.Count; j++)
                        {
                            for (int n = 0; n < 3; n++)
                            {
                                int row = DB.NodeLib[Elem.NList[i]].DOF[m];
                                int col = DB.NodeLib[Elem.NList[j]].DOF[n];
                                //if (!mat.ContainsKey(new int[2] { row, col }))
                                //{
                                output += (row + 1).ToString() + "\t" + (col + 1).ToString() + "\t" + "1\n";
                                mat.Add(new int[2] { row, col }, true);
                                //}
                            }
                        }
                    }
                }
            }

            using (StreamWriter writer = new StreamWriter(@"C:\Users\Michal\Desktop\Solver\NID_Order.txt"))
            {
                writer.Write(output);
            }
        }

    }
}
