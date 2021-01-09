using Kitware.VTK;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace STAN_Database
{
    public class Part
    {
        public int ID { get; }
        public string Name { get; set; }
        public int ColorID { get; set; }

        // Visualization properties
        private vtkPoints Points;
        private vtkUnstructuredGrid Grid;
        private vtkDataSetMapper Mapper;
        private vtkActor Actor;

        private vtkDataSetSurfaceFilter Filter;
        private vtkActor Edges;
        private vtkPolyData Faces;

        // Results
        private vtkFloatArray[,,] CellScalar;
        private vtkFloatArray[,] PointScalar;

        // Clipping
        private vtkTableBasedClipDataSet Clipper;

        // Other properties
        private int MatID;                      // Material ID - assigned to Part
        private string HEX_Type;                // HEXA element formulation 
        private string PENTA_Type;              // PENTA element formulation 
        private string TET_Type;                // TETRA element formulation 

        // Nodes and Elements used to store connection betwen ID and index
        // Data needed to assign scalar values during contour map rendering
        Dictionary<int, int> Nodes;         // Nodes included in Part => key is Node ID, value is Node index
        List<int> Elements;                 // Elements IDs in indexing sequence - for color of cells


        public Part(int id)
        {
            ID = id;

            // Default name
            Name = "New Part";

            // Default material ID 0
            MatID = 0;

            // Default element formulation
            HEX_Type = "HEX8_G2";
            PENTA_Type = "PENTA6_G2";
            TET_Type = "TET4_G2";
        }

        /// <summary>
        /// Create new VTK structure of Part:
        /// <br> Points -> Grid -> Mapper -> Actor </br>
        /// </summary>
        public void CreatePart(Dictionary<int, Node> NodeLib, Dictionary<int, Element> ElemLib)
        {
            // Catch nodes - only used by Part
            Nodes = DetectPartNodes(ElemLib);

            // Initialize Elements - to connect vtkCells with Element IDs (and therefore scalar/result value) 
            Elements = new List<int>();

            // Initialize VTK data
            Points = vtkPoints.New();
            Grid = vtkUnstructuredGrid.New();
            Mapper = vtkDataSetMapper.New();
            Actor = vtkActor.New();
            Edges = vtkActor.New();
            Faces = vtkPolyData.New();
            Filter = vtkDataSetSurfaceFilter.New();

            // Insert Points
            foreach (int n in Nodes.Keys)
            {
                Points.InsertNextPoint(NodeLib[n].X, NodeLib[n].Y, NodeLib[n].Z);
            }

            Grid.SetPoints(Points);      // Add points to Grid
            CreateMesh(ElemLib);         // Create Mesh grid

            // Add Grid to Mapper
            Mapper.SetInput(Grid);                
            Mapper.SetScalarModeToUseCellData();
            Mapper.ScalarVisibilityOff();
            Mapper.InterpolateScalarsBeforeMappingOn();
            Mapper.Update();

            Actor.SetMapper(Mapper);                        // Add Mapper to Actor 
            Actor.GetProperty().SetEdgeVisibility(1);       // Enable wireframe display mode
            Actor.GetProperty().SetLineWidth((float)0.5);   // Set wireframe line thickness
            // Set Actor default color
            ColorID = ID % 13;
            SetColor(ColorID);

            // Outer features
            ExtractFeatures();
        }

        /// <summary>
        /// Clip this part - Set Mapper input to Clipper
        /// </summary>
        public void ClipPart(vtkPlane ClipPlane, bool clip)
        {
            if (clip == true)
            {
                Clipper = vtkTableBasedClipDataSet.New();
                Clipper.SetClipFunction(ClipPlane);
                Clipper.SetInputConnection(Grid.GetProducerPort());
                Clipper.Update();
                Mapper.SetInputConnection(Clipper.GetOutputPort());
                Mapper.Update();

                // Update Feature Filter -> will update Edges and Faces
                Filter.SetInput(Clipper.GetOutput());
                Filter.Update();



                // ------- Alternative without Clip Surface Extraction --------------

                //vtkPlaneCollection planes = vtkPlaneCollection.New();
                //planes.AddItem(ClipPlane);
                //Edges.GetMapper().SetClippingPlanes(planes);
                //Edges.GetMapper().Update();

                //vtkPlaneCollection Planes = vtkPlaneCollection.New();
                //Planes.AddItem(ClipPlane);
                //Mapper.SetClippingPlanes(Planes);
                //Mapper.Update();
            }
            else
            {
                Mapper.SetInput(Grid);
                Mapper.Update();

                // Update Feature Filter -> will update Edges and Faces
                Filter.SetInput(Grid);
                Filter.Update();


                // ------- Alternative without Clip Surface Extraction --------------
                // Edges
                //vtkPlaneCollection planes = vtkPlaneCollection.New();
                //Edges.GetMapper().SetClippingPlanes(planes);
                //Edges.GetMapper().Update();

                //vtkPlaneCollection Planes = vtkPlaneCollection.New();
                //Mapper.SetClippingPlanes(Planes);
                //Mapper.Update();
            }
        }

        /// <summary>
        /// Insert elements (cells) to Part Grid
        /// </summary>
        /// <param name="Elem"></param>
        private void CreateMesh(Dictionary<int, Element> Elem)
        {
            foreach (Element E in Elem.Values)
            {
                // ============= HEXA =================
                if (E.Type.Contains("HEX") && E.PID == ID)
                {
                    // Create Hexahedron
                    vtkHexahedron Hex = vtkHexahedron.New();
                    Hex.GetPointIds().SetNumberOfIds(8);

                    // Set Hexa nodes
                    for (int i = 0; i < 8; i++)
                    {
                        Hex.GetPointIds().SetId(i, Nodes[E.NList[i]]);
                    }

                    // Save Element index
                    Elements.Add(E.ID);
                    Grid.InsertNextCell(Hex.GetCellType(), Hex.GetPointIds());
                }

                // ============= PENTA =================
                if (E.Type.Contains("PENTA") && E.PID == ID)
                {
                    // Create Wedge
                    vtkWedge Penta = vtkWedge.New();
                    Penta.GetPointIds().SetNumberOfIds(6);

                    // Set Penta nodes
                    for (int i = 0; i < 6; i++)
                    {
                        Penta.GetPointIds().SetId(i, Nodes[E.NList[i]]);
                    }

                    // Save Element index
                    Elements.Add(E.ID);
                    Grid.InsertNextCell(Penta.GetCellType(), Penta.GetPointIds());
                }

                // ============= TETRA =================
                if (E.Type.Contains("TET") && E.PID == ID)
                {
                    // Create Tetra
                    vtkTetra Tetra = vtkTetra.New();
                    Tetra.GetPointIds().SetNumberOfIds(4);

                    // Set Tetra nodes
                    for (int i = 0; i < 4; i++)
                    {
                        Tetra.GetPointIds().SetId(i, Nodes[E.NList[i]]);
                    }

                    // Save Element index
                    Elements.Add(E.ID);
                    Grid.InsertNextCell(Tetra.GetCellType(), Tetra.GetPointIds());
                }
            }
        }

        /// <summary>
        /// Load scalar results to Grid Point and Cell Data
        /// </summary>
        public void Load_Scalar(Database DB)
        {
            int NumbScalar = 24;                                 // Total number of scalar results avaliable
            int NumbInc = DB.AnalysisLib.GetResultStepNo() + 1;  // Total number of Analysis increments (including 0)

            // Initialize Scalar data
            CellScalar = new vtkFloatArray[NumbInc, NumbScalar, 3];
            PointScalar = new vtkFloatArray[NumbInc, NumbScalar];


            // Loop through all Analysis increments
            for (int inc = 0; inc < NumbInc; inc++)
            {
                // Create Scalar Arrays for new increment
                for (int s = 0; s < NumbScalar; s++)
                {
                    vtkFloatArray CellMax = vtkFloatArray.New();
                    vtkFloatArray CellAverage = vtkFloatArray.New();
                    vtkFloatArray CellMin = vtkFloatArray.New();
                    vtkFloatArray PointArray = vtkFloatArray.New();

                    CellMax.SetNumberOfComponents(1);
                    CellAverage.SetNumberOfComponents(1);
                    CellMin.SetNumberOfComponents(1);
                    PointArray.SetNumberOfComponents(1);

                    CellScalar[inc, s, 0] = CellMax;
                    CellScalar[inc, s, 1] = CellAverage;
                    CellScalar[inc, s, 2] = CellMin;
                    PointScalar[inc, s] = PointArray;
                }

                // ---------- CELL (ELEMENT) SCALARS ------------------------------------------------
                // Set Array Name
                for (int i = 0; i < 3; i++)
                {
                    string prefix = "";
                    if (i == 0) prefix = "Max";
                    if (i == 1) prefix = "Average";
                    if (i == 2) prefix = "Min";

                    CellScalar[inc, 0, i].SetName(prefix + " Displacement X INC " + inc.ToString());
                    CellScalar[inc, 1, i].SetName(prefix + " Displacement Y INC " + inc.ToString());
                    CellScalar[inc, 2, i].SetName(prefix + " Displacement Z INC " + inc.ToString());
                    CellScalar[inc, 3, i].SetName(prefix + " Total Displacement INC " + inc.ToString());

                    CellScalar[inc, 4 , i].SetName(prefix + " Stress XX INC " + inc.ToString());
                    CellScalar[inc, 5 , i].SetName(prefix + " Stress YY INC " + inc.ToString());
                    CellScalar[inc, 6 , i].SetName(prefix + " Stress ZZ INC " + inc.ToString());
                    CellScalar[inc, 7 , i].SetName(prefix + " Stress XY INC " + inc.ToString());
                    CellScalar[inc, 8 , i].SetName(prefix + " Stress YZ INC " + inc.ToString());
                    CellScalar[inc, 9 , i].SetName(prefix + " Stress XZ INC " + inc.ToString());
                    CellScalar[inc, 10, i].SetName(prefix + " Stress P1 INC " + inc.ToString());
                    CellScalar[inc, 11, i].SetName(prefix + " Stress P2 INC " + inc.ToString());
                    CellScalar[inc, 12, i].SetName(prefix + " Stress P3 INC " + inc.ToString());
                    CellScalar[inc, 13, i].SetName(prefix + " von Mises Stress INC " + inc.ToString());
                                   
                    CellScalar[inc, 14, i].SetName(prefix + " Strain XX INC " + inc.ToString());
                    CellScalar[inc, 15, i].SetName(prefix + " Strain YY INC " + inc.ToString());
                    CellScalar[inc, 16, i].SetName(prefix + " Strain ZZ INC " + inc.ToString());
                    CellScalar[inc, 17, i].SetName(prefix + " Strain XY INC " + inc.ToString());
                    CellScalar[inc, 18, i].SetName(prefix + " Strain YZ INC " + inc.ToString());
                    CellScalar[inc, 19, i].SetName(prefix + " Strain XZ INC " + inc.ToString());
                    CellScalar[inc, 20, i].SetName(prefix + " Strain P1 INC " + inc.ToString());
                    CellScalar[inc, 21, i].SetName(prefix + " Strain P2 INC " + inc.ToString());
                    CellScalar[inc, 22, i].SetName(prefix + " Strain P3 INC " + inc.ToString());
                    CellScalar[inc, 23, i].SetName(prefix + " Effective Strain INC " + inc.ToString());
                }

                // For each Element collect Nodal values, take Min/Average/Max and insert to array
                Parallel.ForEach(Elements, ID =>
                {
                    // Take element from database
                    int index = Elements.IndexOf(ID);
                    Element E = DB.ElemLib[Elements[index]];

                    // List of results in Nodes
                    List<double[]> Nval = new List<double[]>();
                    for (int i = 0; i < NumbScalar; i++)
                    {
                        Nval.Add(new double[E.NList.Count]);
                    }

                    // Assign Nodal values
                    for (int i = 0; i < E.NList.Count; i++)
                    {
                        // Displacement
                        Nval[0][i] = DB.NodeLib[E.NList[i]].GetDisp(inc, 0);
                        Nval[1][i] = DB.NodeLib[E.NList[i]].GetDisp(inc, 1);
                        Nval[2][i] = DB.NodeLib[E.NList[i]].GetDisp(inc, 2);
                        Nval[3][i] = Math.Sqrt(Math.Pow(Nval[0][i], 2) + Math.Pow(Nval[1][i], 2) + Math.Pow(Nval[2][i], 2));

                        // Stress
                        Matrix<double> S = Matrix<double>.Build.Dense(3, 3);
                        S[0, 0] = E.GetStress(inc, i, 0);
                        S[1, 1] = E.GetStress(inc, i, 1);
                        S[2, 2] = E.GetStress(inc, i, 2);
                        S[0, 1] = E.GetStress(inc, i, 3);
                        S[1, 0] = E.GetStress(inc, i, 3);
                        S[1, 2] = E.GetStress(inc, i, 4);
                        S[2, 1] = E.GetStress(inc, i, 4);
                        S[0, 2] = E.GetStress(inc, i, 5);
                        S[2, 0] = E.GetStress(inc, i, 5);
                        Evd<double> eigen = S.Evd();
                        double P1 = eigen.EigenValues[2].Real;
                        double P2 = eigen.EigenValues[1].Real;
                        double P3 = eigen.EigenValues[0].Real;

                        Nval[4][i] = S[0, 0];
                        Nval[5][i] = S[1, 1];
                        Nval[6][i] = S[2, 2];
                        Nval[7][i] = S[0, 1];
                        Nval[8][i] = S[1, 2];
                        Nval[9][i] = S[0, 2];

                        Nval[10][i] = P1;
                        Nval[11][i] = P2;
                        Nval[12][i] = P3;

                        Nval[13][i] = Math.Sqrt((Math.Pow(P1 - P2, 2) + Math.Pow(P2 - P3, 2) + Math.Pow(P3 - P1, 2)) / 2);

                        // Strain
                        S = Matrix<double>.Build.Dense(3, 3);
                        S[0, 0] = E.GetStrain(inc, i, 0);
                        S[1, 1] = E.GetStrain(inc, i, 1);
                        S[2, 2] = E.GetStrain(inc, i, 2);
                        S[0, 1] = E.GetStrain(inc, i, 3);
                        S[1, 0] = E.GetStrain(inc, i, 3);
                        S[1, 2] = E.GetStrain(inc, i, 4);
                        S[2, 1] = E.GetStrain(inc, i, 4);
                        S[0, 2] = E.GetStrain(inc, i, 5);
                        S[2, 0] = E.GetStrain(inc, i, 5);
                        eigen = S.Evd();
                        P1 = eigen.EigenValues[2].Real;
                        P2 = eigen.EigenValues[1].Real;
                        P3 = eigen.EigenValues[0].Real;

                        Nval[14][i] = S[0, 0];
                        Nval[15][i] = S[1, 1];
                        Nval[16][i] = S[2, 2];
                        Nval[17][i] = S[0, 1];
                        Nval[18][i] = S[1, 2];
                        Nval[19][i] = S[0, 2];

                        Nval[20][i] = P1;
                        Nval[21][i] = P2;
                        Nval[22][i] = P3;

                        Nval[23][i] = (2.0 / 3.0) * Math.Sqrt((Math.Pow(P1 - P2, 2) + Math.Pow(P2 - P3, 2) + Math.Pow(P3 - P1, 2)) / 2);
                    }

                    lock (CellScalar)
                    {
                        for (int s = 0; s < NumbScalar; s++)
                        {
                            CellScalar[inc, s, 0].InsertTuple1(index, Nval[s].Max());
                            CellScalar[inc, s, 1].InsertTuple1(index, Nval[s].Average());
                            CellScalar[inc, s, 2].InsertTuple1(index, Nval[s].Min());
                        }
                    }
                });
                // Add arrays to Grid CellData

                foreach (vtkFloatArray array in CellScalar)
                {
                    Grid.GetCellData().AddArray(array);
                }
                

                // ---------- POINT (NODE) SCALARS ------------------------------------------------
                // Set Array Name
                
                    PointScalar[inc, 0].SetName("Displacement X INC " + inc.ToString());
                    PointScalar[inc, 1].SetName("Displacement Y INC " + inc.ToString());
                    PointScalar[inc, 2].SetName("Displacement Z INC " + inc.ToString());
                    PointScalar[inc, 3].SetName("Total Displacement INC " + inc.ToString());

                    PointScalar[inc, 4].SetName("Stress XX INC " + inc.ToString());
                    PointScalar[inc, 5].SetName("Stress YY INC " + inc.ToString());
                    PointScalar[inc, 6].SetName("Stress ZZ INC " + inc.ToString());
                    PointScalar[inc, 7].SetName("Stress XY INC " + inc.ToString());
                    PointScalar[inc, 8].SetName("Stress YZ INC " + inc.ToString());
                    PointScalar[inc, 9].SetName("Stress XZ INC " + inc.ToString());
                    PointScalar[inc, 10].SetName("Stress P1 INC " + inc.ToString());
                    PointScalar[inc, 11].SetName("Stress P2 INC " + inc.ToString());
                    PointScalar[inc, 12].SetName("Stress P3 INC " + inc.ToString());
                    PointScalar[inc, 13].SetName("von Mises Stress INC " + inc.ToString());
                                                 
                    PointScalar[inc, 14].SetName("Strain XX INC " + inc.ToString());
                    PointScalar[inc, 15].SetName("Strain YY INC " + inc.ToString());
                    PointScalar[inc, 16].SetName("Strain ZZ INC " + inc.ToString());
                    PointScalar[inc, 17].SetName("Strain XY INC " + inc.ToString());
                    PointScalar[inc, 18].SetName("Strain YZ INC " + inc.ToString());
                    PointScalar[inc, 19].SetName("Strain XZ INC " + inc.ToString());
                    PointScalar[inc, 20].SetName("Strain P1 INC " + inc.ToString());
                    PointScalar[inc, 21].SetName("Strain P2 INC " + inc.ToString());
                    PointScalar[inc, 22].SetName("Strain P3 INC " + inc.ToString());
                    PointScalar[inc, 23].SetName("Effective Strain INC " + inc.ToString());

                // For each Node collect Nodal values, take Min/Average/Max and insert to array
                Parallel.ForEach(Nodes, n =>
                {
                    // Take element from database
                    Node N = DB.NodeLib[n.Key];

                    // List of results in Nodes
                    List<double[]> Nval = new List<double[]>();
                    for (int i = 0; i < NumbScalar; i++)
                    {
                        Nval.Add(new double[N.EList.Count()]);
                    }

                    // Assign Nodal values
                    for (int i = 0; i < N.EList.Count; i++)
                    {
                        // Index of this node in  i-th Element Node List
                        int NodeIndex = DB.ElemLib[N.EList[i]].NList.IndexOf(N.ID);

                        // Displacement
                        Nval[0][i] = N.GetDisp(inc, 0);
                        Nval[1][i] = N.GetDisp(inc, 1);
                        Nval[2][i] = N.GetDisp(inc, 2);
                        Nval[3][i] = Math.Sqrt(Math.Pow(Nval[0][i], 2) + Math.Pow(Nval[1][i], 2) + Math.Pow(Nval[2][i], 2));

                        // Stress
                        Matrix<double> S = Matrix<double>.Build.Dense(3, 3);
                        S[0, 0] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 0);
                        S[1, 1] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 1);
                        S[2, 2] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 2);
                        S[0, 1] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 3);
                        S[1, 0] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 3);
                        S[1, 2] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 4);
                        S[2, 1] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 4);
                        S[0, 2] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 5);
                        S[2, 0] = DB.ElemLib[N.EList[i]].GetStress(inc, NodeIndex, 5);
                        Evd<double> eigen = S.Evd();
                        double P1 = eigen.EigenValues[2].Real;
                        double P2 = eigen.EigenValues[1].Real;
                        double P3 = eigen.EigenValues[0].Real;

                        Nval[4][i] = S[0, 0];
                        Nval[5][i] = S[1, 1];
                        Nval[6][i] = S[2, 2];
                        Nval[7][i] = S[0, 1];
                        Nval[8][i] = S[1, 2];
                        Nval[9][i] = S[0, 2];

                        Nval[10][i] = P1;
                        Nval[11][i] = P2;
                        Nval[12][i] = P3;

                        Nval[13][i] = Math.Sqrt((Math.Pow(P1 - P2, 2) + Math.Pow(P2 - P3, 2) + Math.Pow(P3 - P1, 2)) / 2);

                        // Strain
                        S = Matrix<double>.Build.Dense(3, 3);
                        S[0, 0] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 0);
                        S[1, 1] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 1);
                        S[2, 2] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 2);
                        S[0, 1] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 3);
                        S[1, 0] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 3);
                        S[1, 2] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 4);
                        S[2, 1] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 4);
                        S[0, 2] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 5);
                        S[2, 0] = DB.ElemLib[N.EList[i]].GetStrain(inc, NodeIndex, 5);
                        eigen = S.Evd();
                        P1 = eigen.EigenValues[2].Real;
                        P2 = eigen.EigenValues[1].Real;
                        P3 = eigen.EigenValues[0].Real;

                        Nval[14][i] = S[0, 0];
                        Nval[15][i] = S[1, 1];
                        Nval[16][i] = S[2, 2];
                        Nval[17][i] = S[0, 1];
                        Nval[18][i] = S[1, 2];
                        Nval[19][i] = S[0, 2];

                        Nval[20][i] = P1;
                        Nval[21][i] = P2;
                        Nval[22][i] = P3;

                        Nval[23][i] = (2.0 / 3.0) * Math.Sqrt((Math.Pow(P1 - P2, 2) + Math.Pow(P2 - P3, 2) + Math.Pow(P3 - P1, 2)) / 2);
                    }

                    lock (PointScalar)
                    {
                        for (int s = 0; s < NumbScalar; s++)
                        {
                            PointScalar[inc, s].InsertTuple1(n.Value, Nval[s].Average());
                        }
                    }
                });
                // Add arrays to Grid PointData
                foreach (vtkFloatArray array in PointScalar)
                {
                    Grid.GetPointData().AddArray(array);
                }
            }
        }

        // ================================ UPDATE PROPERTIES ============================================

        /// <summary>
        /// Update Part Cell or Point scalars
        /// <list>
        /// <item><c>DB</c></item>
        /// <description> - Database with results (e.g. Nodal displacements, Element stress, etc.)</description>
        /// <item><c>inc</c></item>
        /// <description> - increment number for which you want to plot results</description>
        /// <item><c>Result</c></item>
        /// <description> - type of result (Stress XX, Displacement Z, etc.)</description>
        /// <item><c>Style</c></item>
        /// <description> - style of result (Element Max, Element Average, Contour Map)</description>
        /// </list>
        /// </summary>
        public void UpdateScalar(int inc, string Result, string Style)
        {
            // Update Scalars
            if (Result == "None")
            {
                Grid.GetCellData().SetActiveScalars("");
                Grid.GetPointData().SetActiveScalars("");
                Mapper.ScalarVisibilityOff();
            }
            else
            {
                if (Style.Contains("Element"))
                {
                    Grid.GetCellData().SetActiveScalars(
                        Style.Replace("Element ","") + " " + Result + " INC " + inc.ToString());
                    Mapper.SetScalarModeToUseCellData();
                }
                else
                {
                    Grid.GetPointData().SetActiveScalars(Result + " INC " + inc.ToString());
                    Mapper.SetScalarModeToUsePointData();
                }
                Mapper.ScalarVisibilityOn();
            }
            Mapper.Update();
        }

        /// <summary>
        /// Update Part Nodes position
        /// <list>
        /// <item><c>DB</c></item>
        /// <description> - Database with results (e.g. Nodal displacements, Element stress, etc.)</description>
        /// <item><c>inc</c></item>
        /// <description> - increment number for which you want to update</description>
        /// </list>
        /// </summary>
        public void UpdateNode(Database DB, int inc)
        {
            // Update Node position
            foreach (var node in Nodes)   // Update Points coordinates and assign default color
            {
                int id = node.Key;
                int index = node.Value;

                Points.SetPoint(index,
                    DB.NodeLib[id].GetDisp(inc, 0) + DB.NodeLib[id].X,
                    DB.NodeLib[id].GetDisp(inc, 1) + DB.NodeLib[id].Y,
                    DB.NodeLib[id].GetDisp(inc, 2) + DB.NodeLib[id].Z);
            }
        }

        // ================================ PUBLIC METHODS ===============================================

        // ---------- Scalar related methods ------------------------------

        /// <summary>
        /// Set LookupTable to Mapper of this Part
        /// </summary>
        public void Set_ColorTable(vtkLookupTable ColorLT)
        {
            Mapper.SetLookupTable(ColorLT);
            Mapper.InterpolateScalarsBeforeMappingOn();
            Mapper.UseLookupTableScalarRangeOn();
        }

        /// <returns>
        /// Range of current scalars
        /// </returns>
        public double[] Get_ScalarRange(int inc, string Result, string Style)
        {
            float[] range = new float[2] { 0, 1 };

            if (Style.Contains("Element"))
            {
                foreach (vtkFloatArray array in CellScalar)
                {
                    if (array.GetName() == Style.Replace("Element ", "") + " " + Result + " INC " + inc.ToString())
                    {
                        range = array.GetValueRange(0);
                        break;
                    }
                }
            }
            else
            {
                foreach (vtkFloatArray array in PointScalar)
                {
                    if (array.GetName() == Result + " INC " + inc.ToString())
                    {
                        range = array.GetValueRange(0);
                        break;
                    }
                }
            }

            return new double[2] { range[0], range[1] };
        }

        // --------- Other ------------------------------------------------

        /// <summary>
        /// Assign FE type to Part elements
        /// <list>
        /// <item><c>DB</c></item>
        /// <description> - Database with Element library</description>
        /// <item><c>inc</c></item>
        /// <description> - increment number for which you want to plot results</description>
        /// <item><c>ColorLT</c></item>
        /// <description> - LookupTable containing colors, results values are mapped to these colors</description>
        /// <item><c>Interp</c></item>
        /// <description> - type of result value averaging (e.g. Element_Max, Element_Average)</description>
        /// </list>
        /// </summary>
        public void Assign_FEtype(Database DB, string HEX, string PENTA, string TET)
        {
            HEX_Type = HEX;
            PENTA_Type = PENTA;
            TET_Type = TET;

            foreach (Element E in DB.ElemLib.Values)
            {
                if (E.PID == ID)
                {
                    if (E.Type.Contains("HEX")) E.Type = HEX_Type;
                    if (E.Type.Contains("PENTA")) E.Type = PENTA_Type;
                    if (E.Type.Contains("TET")) E.Type = TET_Type;
                }
            }
        }

        /// <returns>
        /// Returns Finite Element types of this Part
        /// </returns>
        public string[] Get_FEtype()
        {
            return new string[3] { HEX_Type, PENTA_Type, TET_Type };
        }

        /// <returns>
        /// vtkActor of this Part
        /// </returns>
        public vtkActor Get_Actor()
        {
            return Actor;
        }

        /// <summary>
        /// Set Actor color based on Color ID. Color palette contains 13 items.
        /// </summary>
        public void SetColor(int id)
        {
            // Define standard color palette
            List<double[]> color = new List<double[]>
            {
                new double[3] { 255.0 / 255.0, 51.00 / 255.0, 51.00 / 255.0 },
                new double[3] { 128.0 / 255.0, 255.0 / 255.0, 0.000 / 255.0 },
                new double[3] { 255.0 / 255.0, 255.0 / 255.0, 51.00 / 255.0 },
                new double[3] { 55.00 / 255.0, 255.0 / 255.0, 255.0 / 255.0 },
                new double[3] { 255.0 / 255.0, 0.000 / 255.0, 127.0 / 255.0 },
                new double[3] { 0.000 / 255.0, 153.0 / 255.0, 0.000 / 255.0 },
                new double[3] { 255.0 / 255.0, 204.0 / 255.0, 229.0 / 255.0 },
                new double[3] { 0.000 / 255.0, 204.0 / 255.0, 204.0 / 255.0 },
                new double[3] { 178.0 / 255.0, 102.0 / 255.0, 255.0 / 255.0 },
                new double[3] { 0.000 / 255.0, 255.0 / 255.0, 128.0 / 255.0 },
                new double[3] { 255.0 / 255.0, 255.0 / 255.0, 153.0 / 255.0 },
                new double[3] { 204.0 / 255.0, 0.000 / 255.0, 204.0 / 255.0 },
                new double[3] { 0.000 / 255.0, 0.000 / 255.0, 204.0 / 255.0 }
            };

            Actor.GetProperty().SetColor(color[id][0], color[id][1], color[id][2]);
        }

        /// <summary>
        /// Create List with Nodes used by Part
        /// <br>Necessary to avoid free vtkPoints - issue during contour map rendering</br>
        /// </summary>
        private Dictionary<int,int> DetectPartNodes(Dictionary<int, Element> ElemLib)
        {
            List<int> temp = new List<int>();
            foreach (Element E in ElemLib.Values)
            {
                if (E.PID == ID)
                {
                    foreach (int nid in E.NList)
                    {
                        temp.Add(nid);
                    }
                }
            }
            // Remove duplicates and sort
            temp = temp.Distinct().ToList();
            temp.Sort();

            // Create (NID, index) dictionary
            Dictionary<int, int> nodes = new Dictionary<int, int>();
            int index = 0;
            foreach (int i in temp)
            {
                nodes.Add(i, index);
                index++;
            }

            return nodes;
        }

        /// <summary>
        /// Enable/Disable wireframe mode (0 - off, 1 - on)
        /// </summary>
        public void SetWireframe(int arg)
        {
            Actor.GetProperty().SetEdgeVisibility(arg);

            if (Actor.GetVisibility() == 1)
            {
                if (arg == 0) Edges.VisibilityOn();
                else Edges.VisibilityOff();
            }
        }

        /// <summary>
        /// Assign MatID to all Elements in this Part
        /// </summary>
        public void Set_MatID(int id, Database db)
        {
            MatID = id;
            foreach(Element E in db.ElemLib.Values)
            {
                if(E.PID == ID) E.MatID = id;
            }
        }

        /// <returns>
        /// MatID of this Part
        /// </returns>
        public int Get_MatID()
        {
            return MatID;
        }

        /// <returns>
        /// List of Elements of this Part
        /// </returns>
        public List<int> Get_PartElements()
        {
            return Elements;
        }

        /// <summary>
        /// Recover Part properties from Database Information
        /// </summary>
        public void SetProperty(Database DB)
        {
            // Assign Name
            Name = DB.Info.GetPart(ID).Name;

            // Assign ColorID
            ColorID = DB.Info.GetPart(ID).ColorID;
            SetColor(ColorID);

            // Assign Mat ID
            Set_MatID(DB.Info.GetPart(ID).MatID, DB);
                        
            // Finally assign FE types
            Assign_FEtype(DB, DB.Info.GetPart(ID).HEX_Type,
                              DB.Info.GetPart(ID).PENTA_Type,
                              DB.Info.GetPart(ID).TET_Type);
        }

        public void ExtractFeatures()
        {
            Filter = vtkDataSetSurfaceFilter.New();
            Filter.SetInput(Grid);
            Filter.Update();
            Faces = Filter.GetOutput();

            vtkFeatureEdges FeatureEdges = vtkFeatureEdges.New();
            FeatureEdges.SetInput(Filter.GetOutput());
            FeatureEdges.Update();

            FeatureEdges.BoundaryEdgesOn();
            FeatureEdges.FeatureEdgesOn();
            FeatureEdges.ManifoldEdgesOn();
            FeatureEdges.NonManifoldEdgesOn();

            // Change Edge color
            FeatureEdges.SetColoring(0);

            // Update
            FeatureEdges.Update();

            vtkPolyDataMapper EdgeMapper = vtkPolyDataMapper.New();
            EdgeMapper.SetInput(FeatureEdges.GetOutput());
            EdgeMapper.ScalarVisibilityOff();

            Edges.SetMapper(EdgeMapper);
            Edges.GetProperty().SetEdgeColor(0, 0, 0);
            Edges.GetProperty().SetColor(0.0, 0.0, 0.0);
            Edges.GetProperty().SetLineWidth((float)1.0);   // Set default edge thickness
            Edges.SetVisibility(0);
        }

        public vtkActor GetEdges()
        {
            return Edges;
        }

        public vtkPolyData GetFaces()
        {
            return Faces;
        }

        // ============================= DATA COMPRESSION ===================================

        public vtkUnstructuredGrid ExportGrid(Database DB, List<string> Result, int step)
        {
            vtkUnstructuredGrid Output = vtkUnstructuredGrid.New();

            // Update Node Coordinates
            UpdateNode(DB, step);

            // Create Deep Copy of Gird
            Output.SetPoints(Points);

            // Recreate Mesh for Output Grid
            foreach (Element E in DB.ElemLib.Values)
            {
                // ============= HEXA =================
                if (E.Type.Contains("HEX") && E.PID == ID)
                {
                    // Create Hexahedron
                    vtkHexahedron Hex = vtkHexahedron.New();
                    Hex.GetPointIds().SetNumberOfIds(8);

                    // Set Hexa nodes
                    for (int i = 0; i < 8; i++)
                    {
                        Hex.GetPointIds().SetId(i, Nodes[E.NList[i]]);
                    }

                    // Save Element index
                    Output.InsertNextCell(Hex.GetCellType(), Hex.GetPointIds());
                }

                // ============= PENTA =================
                if (E.Type.Contains("PENTA") && E.PID == ID)
                {
                    // Create Wedge
                    vtkWedge Penta = vtkWedge.New();
                    Penta.GetPointIds().SetNumberOfIds(6);

                    // Set Penta nodes
                    for (int i = 0; i < 6; i++)
                    {
                        Penta.GetPointIds().SetId(i, Nodes[E.NList[i]]);
                    }

                    // Save Element index
                    Output.InsertNextCell(Penta.GetCellType(), Penta.GetPointIds());
                }

                // ============= TETRA =================
                if (E.Type.Contains("TET") && E.PID == ID)
                {
                    // Create Tetra
                    vtkTetra Tetra = vtkTetra.New();
                    Tetra.GetPointIds().SetNumberOfIds(4);

                    // Set Tetra nodes
                    for (int i = 0; i < 4; i++)
                    {
                        Tetra.GetPointIds().SetId(i, Nodes[E.NList[i]]);
                    }

                    // Save Element index
                    Output.InsertNextCell(Tetra.GetCellType(), Tetra.GetPointIds());
                }
            }

            foreach (string res in Result)
            {
                foreach (vtkFloatArray array in PointScalar)
                {
                    if (array.GetName() == res + " INC " + step.ToString())
                    {
                        vtkFloatArray ArrayCopy = vtkFloatArray.New();
                        ArrayCopy.DeepCopy(array);
                        ArrayCopy.SetName(res);
                        Output.GetPointData().AddArray(ArrayCopy);
                    }
                }
            }
            Output.Update();

            return Output;
        }

    }
}
