using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class Element
    {
        // Basic parameters
        [ProtoMember(1)] public int ID { get; }
        [ProtoMember(2)] public string Type { get; set; }
        [ProtoMember(3)] public int PID { get; }
        [ProtoMember(4)] public int MatID { get; set; }
        [ProtoMember(5)] public List<int> NList { get; }
                         public List<int[,]> DOF { get; }

        // Physical data
        [ProtoMember(6)] private List<MatrixST> Strain;
        [ProtoMember(7)] private List<MatrixST> Stress;

        // Solver buffors
        public MatrixST NodalForces;  // Nodal Forces
        public int GaussPointNumb;    // Number of Gauss point in element
        public MatrixST[] J;          // Jacobian matrix
        public MatrixST[] BL;         // Strain-displacement matrix 

        public MatrixST[] dE;    // Strain increment
        public MatrixST[] dS;     // Stress increment


        public Element(string input)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");  // Set format - comma issue in floats

            // Split input text with multiple whitespace characters
            // In general that's the easiest way to separate columns
            // Issue comes when column contains 8-digit number... TO DO !!!
            List<string> data = Regex.Split(input, @"\s+").ToList();

            ID = int.Parse(data[1]);   // Assign Element ID
            PID = int.Parse(data[2]);  // Assign Part ID
            NList = new List<int>();   // Initialize Node list

            for (int i = 3; i < data.Count; i++)
            {
                data[i] = data[i].Replace("+", ""); // Some preprocessors separates lines with "+" - issue in int.Parse()

                if (int.TryParse(data[i], out _))  // Try to convert string to int
                {
                    NList.Add(int.Parse(data[i]));  // Add Node ID to list
                }
            }

            // Assign default Element type
            if (data[0] == "CHEXA") Type = "HEX8_G2";
            if (data[0] == "CPENTA") Type = "PENTA6_G2";
            if (data[0] == "CTETRA") Type = "TET4_G2";

            // Assign default Material
            MatID = 0;

            // Initialize Strain and Stress memory
            // Outer list specify Increment - for incremental analysis
            // Inner list specify Node 
            // Each Node contains Stress and Strain tensor (extrapoladed from Gauss Points)
            // defined by 6x1 vector (e.g. {S11, S22, S33, S12, S23, S13})
            Strain = new List<MatrixST>();
            Stress = new List<MatrixST>();
        }

        /// <summary>
        /// Strain and Stress initialization at time 0
        /// <br>Reset Element Stress and Strain list </br>
        /// </summary>
        public void Initialize_StepZero(FE_Library FELib)
        {
            // NUmber of Gauss Points
            GaussPointNumb = FELib.FE[Type].GaussPointNumb;

            // Reset Stress and Strain list and Add Zero Stress and Strain tensor at time 0
            Strain = new List<MatrixST> { new MatrixST(NList.Count, 6) };
            Stress = new List<MatrixST> { new MatrixST(NList.Count, 6) };
        }

        /// <summary>
        /// Strain and Stress initialization required at the begining of new increment
        /// </summary>
        public void Initialize_Increment(int inc)
        {
            // Assign Stress and Strain from last increment
            //Strain.Add(Strain[inc-1]);
            //Stress.Add(Stress[inc-1]);
            Strain.Add(new MatrixST(NList.Count, 6));
            Stress.Add(new MatrixST(NList.Count, 6));

            // Reset Stress and Strain increments
            dE = new MatrixST[NList.Count];
            dS = new MatrixST[NList.Count];

            for (int i=0; i<NList.Count; i++)
            {
                dE[i] = new MatrixST(6, 1);  // Vector 6x1 in each Gauss Point
                dS[i] = new MatrixST(6, 1);  // Vector 6x1 in each Gauss Point
            }

            // Reset solver buffors
            J = new MatrixST[GaussPointNumb];
            BL = new MatrixST[GaussPointNumb];
        }

        /// <summary>
        /// Calculate Initial (Linear) Finite Element Stiffness Matrix
        /// </summary>
        public MatrixST K_Initial(int inc, Database DB)
        {
            // Element Matrix Initialization
            MatrixST K = new MatrixST(NList.Count * 3, NList.Count * 3);

            // Loop thorugh all Gauss Points
            for (int g = 0; g < GaussPointNumb; g++)
            {
                // Jacobian Matrix at Gauss Point
                J[g] = Jacobian(DB.NodeLib, DB.FELib.FE[Type].dN_dLocal[g]);

                // Shape function derivatives in Global Coordinates System
                MatrixST dN = J[g].Inverse() * DB.FELib.FE[Type].dN_dLocal[g];


                //  ------ Linear strain-displacement matrix ---------------------------------
                // Initial displacement effect - BL1 Matrix
                MatrixST U = new MatrixST(NList.Count, 3);
                for (int i = 0; i < NList.Count; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        U.SetFast(i, j, DB.NodeLib[NList[i]].GetDisp(inc, j));
                    }
                }
                BL[g] = BL0_Matrix(dN) + BL1_Matrix(dN, U);  // BL = BL0 + BL1


                //  ------ Constitutive Matrix ---------------------------------
                MatrixST D = DB.MatLib[MatID].GetElastic();

                //  --- LINEAR PART OF STIFFNESS MATRIX -------------------------------------------
                // Add K components to Element K matrix (sum from all Gauss points)
                K += (BL[g].Transpose() * D * BL[g]).MultiplyScalar(J[g].Det3() * DB.FELib.FE[Type].GaussWeight);

            }
            return K;
        }

        /// <summary>
        /// Calculate Tangent stiffness matrix
        /// </summary>
        public MatrixST K_Tangent(int inc, Database DB)
        {
            // Element Matrix Initialization
            MatrixST K = new MatrixST(NList.Count * 3, NList.Count * 3);

            // Loop thorugh all Gauss Points
            for (int g = 0; g < GaussPointNumb; g++)
            {
                // Jacobian Matrix at Gauss Point
                J[g] = Jacobian(DB.NodeLib, DB.FELib.FE[Type].dN_dLocal[g]);

                // Shape function derivatives in Global Coordinates System
                MatrixST dN = J[g].Inverse() * DB.FELib.FE[Type].dN_dLocal[g];


                //  ------ Linear strain-displacement matrix ---------------------------------
                // Initial displacement effect - BL1 Matrix
                MatrixST U = new MatrixST(NList.Count, 3);
                for (int i = 0; i < NList.Count; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        U.SetFast(i, j, DB.NodeLib[NList[i]].GetDisp(inc, j));
                    }
                }
                BL[g] = BL0_Matrix(dN) + BL1_Matrix(dN, U);  // BL = BL0 + BL1


                //  ------ Constitutive Matrix ---------------------------------
                MatrixST D = DB.MatLib[MatID].GetElastic();

                //  --- LINEAR PART OF STIFFNESS MATRIX -------------------------------------------
                // Add K components to Element K matrix (sum from all Gauss points)
                K += (BL[g].Transpose() * D * BL[g]).MultiplyScalar(J[g].Det3() * DB.FELib.FE[Type].GaussWeight);


                // --- NONLINEAR PART OF STIFFNESS MATRIX ------------------------------------------------------------
                // Nonlinear Strain-displacement matrix
                MatrixST BNL = BNL_Matrix(dN);
                
                // Initial Stress matrix
                MatrixST S = Stress_Matrix(inc, g);
                
                // Nonlinear part of Element Stiffness Matrix
                K += (BNL.Transpose() * S * BNL).MultiplyScalar(J[g].Det3() * DB.FELib.FE[Type].GaussWeight);

            }

            return K;
        }

        public void Recovery_Stress(Database DB)
        {
            // Nodal dU vector
            double[] dU = new double[NList.Count * 3];
            for (int i = 0; i < NList.Count; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    dU[i * 3 + j] = DB.NodeLib[NList[i]].dU_buffer[j];
                }
            }

            // Constitutive Matrix
            MatrixST D = DB.MatLib[MatID].GetElastic();

            // Loop thorugh all Gauss Points
            MatrixST dE_Gauss = new MatrixST(GaussPointNumb, 6);  // Matrix to store strain in Gauss Points
            MatrixST dS_Gauss = new MatrixST(GaussPointNumb, 6);  // Matrix to store stress in Gauss Points

            for (int g = 0; g < GaussPointNumb; g++)
            {
                // Add Stress and Strain vectors
                dE_Gauss.SetRow(g, BL[g].MultiplyVector(dU));
                dS_Gauss.SetRow(g, D.MultiplyVector(dE_Gauss.GetRow(g)));
            }

            // Extrapolate Stress and Strain from Gauss Points to Nodes
            for (int i = 0; i < NList.Count; i++)
            {
                for (int g = 0; g < GaussPointNumb; g++)
                {
                    dE[i] += dE_Gauss.GetRow_MatrixST(g).Transpose().MultiplyScalar(DB.FELib.FE[Type].N[i][g]);
                    dS[i] += dS_Gauss.GetRow_MatrixST(g).Transpose().MultiplyScalar(DB.FELib.FE[Type].N[i][g]);
                }
            }
        }

        public void Compute_NodalForces(Database DB)
        { 
            NodalForces = new MatrixST(3 * NList.Count, 1);
            for (int g = 0; g < GaussPointNumb; g++)
            {
                NodalForces += (BL[g].Transpose() * dS[g]).MultiplyScalar(J[g].Det3() * DB.FELib.FE[Type].GaussWeight);
            }
        }

        public void Update_StrainStress(int inc)
        {
            for (int i = 0; i < NList.Count; i++)
            {
                for (int n = 0; n < 6; n++)
                {
                    Strain[inc].SetFast(i, n, dE[i].GetFast(n, 0));
                    Stress[inc].SetFast(i, n, dS[i].GetFast(n, 0));
                }
            }
        }

        //  ==================== Finite Element Matrix methods ===================================

        /// <summary>
        /// <c>Jacobian</c> returns Jacobian matrix in Gauss Point.
        /// </summary>
        public MatrixST Jacobian(Dictionary<int, Node> NodeLib, MatrixST dN_dLocal)
        {
            //     |   dN1/d_xi    dN2/d_xi ... |   |x1  y1  z1|
            // J = |  dN1/d_eta   dN2/d_eta ... | * |x2  y2  z2|
            //     | dN1/d_zeta  dN2/d_zeta ... |   |x3  y3  z3|
            //                                      |    ...   |

            MatrixST Nodal_Coord = new MatrixST(NList.Count, 3);

            for (int i = 0; i < NList.Count; i++)
            {
                Nodal_Coord.SetFast(i, 0, NodeLib[NList[i]].X);
                Nodal_Coord.SetFast(i, 1, NodeLib[NList[i]].Y);
                Nodal_Coord.SetFast(i, 2, NodeLib[NList[i]].Z);
            }
            MatrixST J = dN_dLocal * Nodal_Coord;

            return J;
        }

        /// <summary>
        /// <c>BL0_Matrix</c> returns linear strain-displacement transformation matrix without initial displacement effect
        /// </summary>
        private MatrixST BL0_Matrix(MatrixST dN)
        {
            //    Strain vector:  e = { e_xx   e_yy   e_zz   e_xy   e_yz   e_xz }
            //
            //   BL0 is linear strain-displacement matrix:
            //                 __                                __ 
            //                | dNi/dx     0         0             | 
            //                |    0    dNi/dy       0             |  
            //                |    0       0      dNi/dz           | 
            //    BL0 = L*N = | dNi/dy  dNi/dx       0      ...    | 
            //                |    0    dNi/dz    dNi/dy           | 
            //                | dNi/dz     0      dNi/dx           | 
            //                 ---                              ---  

            // Strain-displacement Matrix BL0
            MatrixST BL0 = new MatrixST(6, 3 * NList.Count);

            for (int i = 0; i < NList.Count; i++)
            {
                BL0.SetFast(0, 3 * i + 0, dN.GetFast(0, i));
                BL0.SetFast(1, 3 * i + 1, dN.GetFast(1, i));
                BL0.SetFast(2, 3 * i + 2, dN.GetFast(2, i));
                BL0.SetFast(3, 3 * i + 0, dN.GetFast(1, i));
                BL0.SetFast(3, 3 * i + 1, dN.GetFast(0, i));
                BL0.SetFast(4, 3 * i + 1, dN.GetFast(2, i));
                BL0.SetFast(4, 3 * i + 2, dN.GetFast(1, i));
                BL0.SetFast(5, 3 * i + 0, dN.GetFast(2, i));
                BL0.SetFast(5, 3 * i + 2, dN.GetFast(0, i));
            }

            return BL0;
        }

        /// <summary>
        /// <c>BL1_Matrix</c> returns intial displacement part of BL matrix in Total Lagrangian framework
        /// </summary>
        private MatrixST BL1_Matrix(MatrixST dN, MatrixST dU)
        {
            //    Strain vector:  e = { e_xx   e_yy   e_zz   e_xy   e_yz   e_xz }
            //           __                                                                                  __ 
            //          |        F11 * dNi/dx              F21 * dNi/dx              F31 * dNi/dx              |  
            //          |        F12 * dNi/dy              F22 * dNi/dy              F32 * dNi/dy              |
            //          |        F13 * dNi/dy              F23 * dNi/dy              F33 * dNi/dy              |
            //    BL1 = | F11*dNi/dy + F12*dNi/dx   F21*dNi/dy + F22*dNi/dx   F11*dNi/dy + F12*dNi/dx    ...   |      
            //          | F12*dNi/dz + F13*dNi/dy   F22*dNi/dz + F23*dNi/dy   F12*dNi/dz + F13*dNi/dy          |
            //          | F11*dNi/dz + F13*dNi/dx   F21*dNi/dz + F23*dNi/dx   F11*dNi/dz + F13*dNi/dx          |
            //           ---                                                                                ---  
            // 
            //                                                      | x1  y1  z1 |         | dN1/dx  dN2/dx       dNk/dx |
            //   F(i,j) = summ ( dN[j, k] * U[k, i] )   where   U = |     ...    |    dN = | dN1/dy  dN2/dy  ...  dNk/dy |
            //                                                      | xk  yk  zk |         | dN1/dz  dN2/dz       dNk/dz |
            //                                          

            MatrixST F = dN * dU;

            MatrixST BL1 = new MatrixST(6, 3 * NList.Count);
            for (int i = 0; i < NList.Count; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    BL1.SetFast(0, 3 * i + j, F.GetFast(j, 0) * dN.GetFast(0, i));
                    BL1.SetFast(1, 3 * i + j, F.GetFast(j, 1) * dN.GetFast(1, i));
                    BL1.SetFast(2, 3 * i + j, F.GetFast(j, 2) * dN.GetFast(2, i));
                    BL1.SetFast(3, 3 * i + j, F.GetFast(j, 0) * dN.GetFast(1, i) + F.GetFast(j, 1) * dN.GetFast(0, i));
                    BL1.SetFast(4, 3 * i + j, F.GetFast(j, 1) * dN.GetFast(2, i) + F.GetFast(j, 2) * dN.GetFast(1, i));
                    BL1.SetFast(5, 3 * i + j, F.GetFast(j, 0) * dN.GetFast(2, i) + F.GetFast(j, 2) * dN.GetFast(0, i));
                }
            }
            return BL1;
        }

        /// <summary>
        /// <c>BNL_Matrix</c> returns non-linear strain-displacement transformation matrix
        /// </summary>
        private MatrixST BNL_Matrix(MatrixST dN)
        {
            //         __       __                  ___                                ___ 
            //        | B   0   0 |                | dN1/dx   0   0   dN2/dx       dNi/dx |
            //  BNL = | 0   B   0 |   where  B =   | dN1/dy   0   0   dN2/dy  ...  dNi/dy |  
            //        | 0   0   B |                | dN1/dz   0   0   dN2/dz       dNi/dz |
            //         ---     ---                  ---                                ---
            //
            //                   | dN1/dx  dN2/dx       dNk/dx |
            //    Input:    dN = | dN1/dy  dN2/dy  ...  dNk/dy |
            //                   | dN1/dz  dN2/dz       dNk/dz |
            //

            // Strain-displacement Matrix
            MatrixST BNL = new MatrixST(9, 3 * NList.Count);

            for (int i = 0; i < NList.Count; i++)
            {
                BNL.SetFast(0, 3 * i + 0, dN.GetFast(0, i));
                BNL.SetFast(1, 3 * i + 0, dN.GetFast(1, i));
                BNL.SetFast(2, 3 * i + 0, dN.GetFast(2, i));
                BNL.SetFast(3, 3 * i + 1, dN.GetFast(0, i));
                BNL.SetFast(4, 3 * i + 1, dN.GetFast(1, i));
                BNL.SetFast(5, 3 * i + 1, dN.GetFast(2, i));
                BNL.SetFast(6, 3 * i + 2, dN.GetFast(0, i));
                BNL.SetFast(7, 3 * i + 2, dN.GetFast(1, i));
                BNL.SetFast(8, 3 * i + 2, dN.GetFast(2, i));
            }

            return BNL;
        }

        /// <summary>
        /// <c>S_Matrix</c> returns stress matrix required to compute nonlinear part of K matrix 
        /// </summary>
        private MatrixST Stress_Matrix(int inc, int GaussPoint)
        {
            //         __       __                  __           __        __        __ 
            //        | s   0   0 |                | s11  s12  s13 |      | s0  s3  s5 |
            //    S = | 0   s   0 |   where  s =   | s21  s22  s23 |  =>  | s3  s1  s4 |
            //        | 0   0   s |                | s31  s32  s33 |      | s5  s4  s2 |
            //         ---     ---                  ---         ---        ---      --- 

            MatrixST S = new MatrixST(9, 9);

            for (int i = 0; i < 3; i++)
            {
                S.SetFast(3 * i + 0, 3 * i + 0, Stress[inc].GetFast(GaussPoint, 0) + dS[GaussPoint].GetFast(0, 0));
                S.SetFast(3 * i + 1, 3 * i + 0, Stress[inc].GetFast(GaussPoint, 3) + dS[GaussPoint].GetFast(3, 0));
                S.SetFast(3 * i + 2, 3 * i + 0, Stress[inc].GetFast(GaussPoint, 5) + dS[GaussPoint].GetFast(5, 0));
                S.SetFast(3 * i + 0, 3 * i + 1, Stress[inc].GetFast(GaussPoint, 3) + dS[GaussPoint].GetFast(3, 0));
                S.SetFast(3 * i + 1, 3 * i + 1, Stress[inc].GetFast(GaussPoint, 1) + dS[GaussPoint].GetFast(1, 0));
                S.SetFast(3 * i + 2, 3 * i + 1, Stress[inc].GetFast(GaussPoint, 4) + dS[GaussPoint].GetFast(4, 0));
                S.SetFast(3 * i + 0, 3 * i + 2, Stress[inc].GetFast(GaussPoint, 5) + dS[GaussPoint].GetFast(5, 0));
                S.SetFast(3 * i + 1, 3 * i + 2, Stress[inc].GetFast(GaussPoint, 4) + dS[GaussPoint].GetFast(4, 0));
                S.SetFast(3 * i + 2, 3 * i + 2, Stress[inc].GetFast(GaussPoint, 2) + dS[GaussPoint].GetFast(2, 0));
            }

            return S;
        }


        // =====================  PUBLIC methods to GET/SET element data  ===================

        /// <summary>
        /// Get Stress value in Gauss point at specified increment
        /// <list>
        /// <item><c>inc</c></item>
        /// <description> - increment number </description>
        /// <item><c>Node</c></item>
        /// <description> - Node index</description>
        /// <item><c>dir</c></item>
        /// <description> - stress type/direction:<br> XX=0; YY=1; ZZ=2; XY=3; YZ=4; XZ=5;</br>
        /// <br> vonMises=6; P1=7; P2=8; P3=9</br></description>
        /// </list>
        /// </summary>
        /// <returns>Value of stress as double</returns>
        public double GetStress(int inc, int Node, int dir)
        {
            return Stress[inc].GetFast(Node, dir);
        }

        /// <summary>
        /// Get Strain value in Node at specified increment
        /// <list>
        /// <item><c>inc</c></item>
        /// <description> - increment number </description>
        /// <item><c>Node</c></item>
        /// <description> - Node index</description>
        /// <item><c>dir</c></item>
        /// <description> - strain type/direction:<br> XX=0; YY=1; ZZ=2; XY=3; YZ=4; XZ=5;</br>
        /// <br> Total=6; P1=7; P2=8; P3=9</br></description>
        /// </list>
        /// </summary>
        /// <returns>Value of strain as double</returns>
        public double GetStrain(int inc, int Node, int dir)
        {
            return Strain[inc].GetFast(Node, dir);
        }

        /// <summary>
        /// Add Element to Node EList
        /// </summary>
        public void AddElem2Nodes(Dictionary<int, Node> NodeLib)
        {
            foreach (int nid in NList)
            {
                NodeLib[nid].AddElement(ID);
            }
        }

        public void ClearResults()
        {
            Stress = null;
            Strain = null;
        }
    }
}
