using Kitware.VTK;
using ProtoBuf;
using System.Collections.Generic;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class BoundaryCondition
    {
        [ProtoMember(1)] public string Type { get; set; }
        [ProtoMember(2)] public string Name { get; set; }
        [ProtoMember(3)] public int ID { get; set; }
        [ProtoMember(4)] public Dictionary<int, MatrixST> NodalValues { get; set; }
        [ProtoMember(5)] public int ColorID { get; set; }

        private vtkGlyph3D GlyphX;
        private vtkGlyph3D GlyphY;
        private vtkGlyph3D GlyphZ;
        private vtkDataSetMapper MapperX;
        private vtkDataSetMapper MapperY;
        private vtkDataSetMapper MapperZ;
        private vtkActor ActorX;
        private vtkActor ActorY;
        private vtkActor ActorZ;
        private vtkTableBasedClipDataSet ClipperX;
        private vtkTableBasedClipDataSet ClipperY;
        private vtkTableBasedClipDataSet ClipperZ;

        public BoundaryCondition(string name, string type, int id)
        {
            Name = name;
            Type = type;
            ID = id;
            ColorID = ID % 9;
            NodalValues = new Dictionary<int, MatrixST>();

        }

        public void Initialize()
        {
            // Initialize Actors
            GlyphX = vtkGlyph3D.New();
            MapperX = vtkDataSetMapper.New();
            MapperX.SetInputConnection(GlyphX.GetOutputPort());
            ActorX = vtkActor.New();
            ActorX.SetMapper(MapperX);
            ClipperX = vtkTableBasedClipDataSet.New();

            GlyphY = vtkGlyph3D.New();
            MapperY = vtkDataSetMapper.New();
            MapperY.SetInputConnection(GlyphY.GetOutputPort());
            ActorY = vtkActor.New();
            ActorY.SetMapper(MapperY);
            ClipperY = vtkTableBasedClipDataSet.New();

            GlyphZ = vtkGlyph3D.New();
            MapperZ = vtkDataSetMapper.New();
            MapperZ.SetInputConnection(GlyphZ.GetOutputPort());
            ActorZ = vtkActor.New();
            ActorZ.SetMapper(MapperZ);
            ClipperZ = vtkTableBasedClipDataSet.New();

            // Enable wireframe display mode
            ActorX.GetProperty().EdgeVisibilityOn();
            ActorY.GetProperty().EdgeVisibilityOn();
            ActorZ.GetProperty().EdgeVisibilityOn();

            // Hide Actors
            ActorX.VisibilityOff();
            ActorY.VisibilityOff();
            ActorZ.VisibilityOff();
        }

        /// <summary>
        /// Add nodal value to List
        /// <list>
        /// <item><c>nid</c></item>
        /// <description> - Node ID</description>
        /// <item><c>direction</c></item>
        /// <description> - X=0; Y=1; Z=2</description>
        /// <item><c>value</c></item>
        /// <description> - Value of BC (double for Force BC, 0 or 1 for Fix BC)</description>
        /// <item><c>NodeLib</c></item>
        /// <description> - Database Node library</description>
        /// </list>
        /// </summary>
        public void Add(int nid, double[] value, Dictionary<int, Node> NodeLib)
        {
            if (NodeLib.ContainsKey(nid))
            {
                MatrixST nVal = new MatrixST(3, 1);
                nVal.SetFast(0, 0, value[0]);
                nVal.SetFast(1, 0, value[1]);
                nVal.SetFast(2, 0, value[2]);

                NodalValues.Add(nid, nVal);
            }
        }

        /// <summary>
        /// Clear list of Nodal Values
        /// </summary>
        public void Clear()
        {
            NodalValues = new Dictionary<int, MatrixST>();
        }

        /// <summary>
        /// Update Boundary Condition actor (arrows) in Viewport
        /// </summary>
        public void Update_Arrows(Dictionary<int, Node> NodeLib, double scale, int Step, bool ClipMode)
        {
            vtkPoints PointsX = vtkPoints.New();
            vtkPoints PointsY = vtkPoints.New();
            vtkPoints PointsZ = vtkPoints.New();

            // Create Cone Sources for X, Y and Z direction
            vtkConeSource ConeSourceX = vtkConeSource.New();
            vtkConeSource ConeSourceY = vtkConeSource.New();
            vtkConeSource ConeSourceZ = vtkConeSource.New();

            ConeSourceX.SetAngle(15);
            ConeSourceX.SetHeight(scale);
            ConeSourceX.SetRadius(scale / 4);
            ConeSourceX.SetResolution(12);
            ConeSourceX.SetDirection(1, 0, 0);

            ConeSourceY.SetAngle(15);
            ConeSourceY.SetHeight(scale);
            ConeSourceY.SetRadius(scale / 4);
            ConeSourceY.SetResolution(12);
            ConeSourceY.SetDirection(0, 1, 0);

            ConeSourceZ.SetAngle(15);
            ConeSourceZ.SetHeight(scale);
            ConeSourceZ.SetRadius(scale / 4);
            ConeSourceZ.SetResolution(12);
            ConeSourceZ.SetDirection(0, 0, 1);

            // Create Points
            foreach (int i in NodalValues.Keys)
            {
                double X = NodeLib[i].X + NodeLib[i].GetDisp(Step, 0);
                double Y = NodeLib[i].Y + NodeLib[i].GetDisp(Step, 1);
                double Z = NodeLib[i].Z + NodeLib[i].GetDisp(Step, 2);

                if (NodalValues[i].Get(0, 0) != 0) PointsX.InsertNextPoint(X - scale / 2, Y, Z);
                if (NodalValues[i].Get(1, 0) != 0) PointsY.InsertNextPoint(X, Y - scale / 2, Z);
                if (NodalValues[i].Get(2, 0) != 0) PointsZ.InsertNextPoint(X, Y, Z - scale / 2);
            }

            // Set Points to PolyData
            vtkPolyData PolyX = vtkPolyData.New(); PolyX.SetPoints(PointsX);
            vtkPolyData PolyY = vtkPolyData.New(); PolyY.SetPoints(PointsY);
            vtkPolyData PolyZ = vtkPolyData.New(); PolyZ.SetPoints(PointsZ);

            // Create Glyphs 3D
            GlyphX = vtkGlyph3D.New();
            GlyphY = vtkGlyph3D.New();
            GlyphZ = vtkGlyph3D.New();

            GlyphX.SetSourceConnection(ConeSourceX.GetOutputPort());
            GlyphX.SetInput(PolyX); 
            GlyphX.Update();

            GlyphY.SetSourceConnection(ConeSourceY.GetOutputPort());
            GlyphY.SetInput(PolyY);
            GlyphY.Update();

            GlyphZ.SetSourceConnection(ConeSourceZ.GetOutputPort());
            GlyphZ.SetInput(PolyZ);
            GlyphZ.Update();

            // Set Mapper based on Clip Mode
            if (ClipMode == true)
            {
                // Add Clippers to Mapper
                ClipperX.SetInputConnection(GlyphX.GetOutputPort());
                ClipperX.Update();
                MapperX.SetInputConnection(ClipperX.GetOutputPort());
                MapperX.Update();

                ClipperY.SetInputConnection(GlyphY.GetOutputPort());
                ClipperY.Update();
                MapperY.SetInputConnection(ClipperY.GetOutputPort());
                MapperY.Update();

                ClipperZ.SetInputConnection(GlyphZ.GetOutputPort());
                ClipperZ.Update();
                MapperZ.SetInputConnection(ClipperZ.GetOutputPort());
                MapperZ.Update();
            }
            else
            {
                // Add Glyphs to Mapper
                MapperX.SetInputConnection(GlyphX.GetOutputPort());
                MapperY.SetInputConnection(GlyphY.GetOutputPort());
                MapperZ.SetInputConnection(GlyphZ.GetOutputPort());
                MapperX.Update();
                MapperY.Update();
                MapperZ.Update();
            }

            // Update Actor color
            ActorX.GetProperty().SetColor(
                GetColor()[0] / 255.0,
                GetColor()[1] / 255.0,
                GetColor()[2] / 255.0);

            ActorY.GetProperty().SetColor(
               GetColor()[0] / 255.0,
               GetColor()[1] / 255.0,
               GetColor()[2] / 255.0);

            ActorZ.GetProperty().SetColor(
               GetColor()[0] / 255.0,
               GetColor()[1] / 255.0,
               GetColor()[2] / 255.0);
        }

        public void ShowActor()
        {
            ActorX.VisibilityOn();
            ActorY.VisibilityOn();
            ActorZ.VisibilityOn();
        }

        public void HideActor()
        {
            ActorX.VisibilityOff();
            ActorY.VisibilityOff();
            ActorZ.VisibilityOff();
        }

        public vtkActor[] GetActor()
        {
            return new vtkActor[3] { ActorX, ActorY, ActorZ };
        }

        private double[] GetColor()
        {
            List<double[]> Colors = new List<double[]>
            {
                new double[3]{  12, 197,  19 },
                new double[3]{ 141, 245, 145 },
                new double[3]{ 109, 232, 226 },
                new double[3]{  42,  85, 199 },
                new double[3]{ 223, 232,  28 },
                new double[3]{ 236, 134,  64 },
                new double[3]{ 216,  52,  52 },
                new double[3]{ 184,  61, 241 },
                new double[3]{ 110,  10, 120 }
            };
            return Colors[ColorID];
        }

        public void ClipBC (vtkPlane ClipPlane, bool clip)
        {
            if (clip == true)
            {
                ClipperX = vtkTableBasedClipDataSet.New();
                ClipperX.SetClipFunction(ClipPlane);
                ClipperX.SetInputConnection(GlyphX.GetOutputPort());
                ClipperX.Update();
                MapperX.SetInputConnection(ClipperX.GetOutputPort());
                MapperX.Update();

                ClipperY = vtkTableBasedClipDataSet.New();
                ClipperY.SetClipFunction(ClipPlane);
                ClipperY.SetInputConnection(GlyphY.GetOutputPort());
                ClipperY.Update();
                MapperY.SetInputConnection(ClipperY.GetOutputPort());
                MapperY.Update();

                ClipperZ = vtkTableBasedClipDataSet.New();
                ClipperZ.SetClipFunction(ClipPlane);
                ClipperZ.SetInputConnection(GlyphZ.GetOutputPort());
                ClipperZ.Update();
                MapperZ.SetInputConnection(ClipperZ.GetOutputPort());
                MapperZ.Update();
            }
            else
            {
                // Add Glyphs to Mapper
                MapperX.SetInputConnection(GlyphX.GetOutputPort());
                MapperY.SetInputConnection(GlyphY.GetOutputPort());
                MapperZ.SetInputConnection(GlyphZ.GetOutputPort());
                MapperX.Update();
                MapperY.Update();
                MapperZ.Update();
            }
        }

        public void SetWireframe(int arg)
        {
            ActorX.GetProperty().SetEdgeVisibility(arg);
            ActorY.GetProperty().SetEdgeVisibility(arg);
            ActorZ.GetProperty().SetEdgeVisibility(arg);
        }
    }
}
