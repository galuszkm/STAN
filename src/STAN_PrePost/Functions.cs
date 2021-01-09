using System.IO;
using STAN_Database;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System;

namespace STAN_PrePost
{
    public class Functions
    {
        public string Between(string s, string start, string end)
        {
            int pFrom = s.IndexOf(start) + start.Length;
            int pTo = s.IndexOf(end);

            string result = s.Substring(pFrom, pTo - pFrom);
            return result;
        }

        public string Before(string s, string end)
        {
            int pTo = s.IndexOf(end);
            string result = s.Substring(0, pTo);
            return result;
        }

        public string BeforeLast(string s, string end)
        {
            int pTo = s.LastIndexOf(end);
            string result = s.Substring(0, pTo);
            return result;
        }

        public Tuple<Database, bool> OpenDatabase (string path)
        {
            //Reading binary file
            byte[] Input = File.ReadAllBytes(path);
            Database DB = ProtoDeserialize<Database>(Input);

            // Bool variable if Database is usefull
            bool GoOn = false;

            string error = "Database loading error:"; // Error message

            try
            {
                if (DB.NodeLib != null && DB.NodeLib.Count > 0)
                {
                    if (DB.ElemLib != null && DB.ElemLib.Count > 0)
                    {
                        // Initialize Part Library and others if not exist
                        DB.PartLib = new Dictionary<int, Part>();
                        if (DB.MatLib == null) DB.MatLib = new Dictionary<int, Material>();        
                        if (DB.BCLib == null) DB.BCLib = new Dictionary<int, BoundaryCondition>();
                        if (DB.FELib == null) DB.FELib = new FE_Library();                        
                        if (DB.AnalysisLib == null) DB.AnalysisLib = new Analysis();                    
                        if (DB.Info == null) DB.Info = new Information();

                        // Create list with Parts - using LINQ select PID of all elements, remove duplicates and sort
                        List<int> PartList = DB.ElemLib.Values.Select(x => x.PID).Distinct().ToList();
                        PartList.Sort();

                        // Create Part objects and add to Part Library
                        foreach (int pid in PartList)
                        {
                            Part NewPart = new Part(pid);
                            NewPart.CreatePart(DB.NodeLib, DB.ElemLib);
                            DB.PartLib.Add(pid, NewPart);
                        }

                        DB.Set_nDOF();  // Calculate number of Degrees of Freedom

                        // Create Boundary Conditions
                        if (DB.BCLib != null)
                        {
                            foreach (BoundaryCondition bc in DB.BCLib.Values)
                            {
                                bc.Initialize();
                            }
                        }

                        GoOn = true;
                    }
                }
            }
            catch
            {
                if(DB.NodeLib != null && DB.ElemLib != null)
                {
                    error += "\n   - Unknown error";
                }
            }

            if (DB.NodeLib == null)
            {
                error += "\n   - Nodes not detected";
            }

            if (DB.ElemLib == null)
            {
                error += "\n   - Elements not detected";
            }

            if (GoOn == false)
            {
                System.Windows.Forms.MessageBox.Show(error);
            }

            Tuple<Database, bool> exit = new Tuple<Database, bool>(DB, GoOn);

            return exit;
        }

        public void AddPart2GUI(Part part, RenderInterface iRen, ListBox PartBox, TreeView Tree)
        {
            // Define Part TreeView item
            TreeViewItem TreePart = (TreeViewItem)Tree.Items[0];

            // Add Part to Selection Box and Viewport
            PartBox.Items.Add("PID " + part.ID.ToString() + ": " + part.Name);
            iRen.AddActor(part.Get_Actor());
            iRen.AddActor(part.GetEdges());
            iRen.AppendFaces.AddInput(part.GetFaces());

            // Add Part to TreeView
            TreeViewItem item = new TreeViewItem()
            {
                Header = "Part ID " + part.ID.ToString() + ": " + part.Name
            };
            TreePart.Items.Add(item);
            TreePart.IsExpanded = true; // Expand Parts in Tree
        }

        public void AddBC2GUI(BoundaryCondition BC, RenderInterface iRen, TreeView Tree, bool Selected)
        {
            // Define BC TreeView item
            TreeViewItem TreeBC = (TreeViewItem)Tree.Items[2];

            // Add BC actor to Viewport
            iRen.AddActor(BC.GetActor()[0]);
            iRen.AddActor(BC.GetActor()[1]);
            iRen.AddActor(BC.GetActor()[2]);
            BC.HideActor();

            // Add BC to TreeView
            TreeViewItem item = new TreeViewItem()
            {
                Header = "BC ID " + BC.ID.ToString() + ": " + BC.Name,
                IsSelected = Selected
            };
            TreeBC.Items.Add(item);
            TreeBC.IsExpanded = true;
        }

        public void AddMat2GUI (Material Mat, TreeView Tree)
        {
            // Define Mat TreeView item
            TreeViewItem TreeMat = (TreeViewItem)Tree.Items[1];

            // Add BC to TreeView
            TreeViewItem item = new TreeViewItem()
            {
                Header = "Mat ID " + Mat.ID.ToString() + ": " + Mat.Name,
                IsSelected = true
            };
            TreeMat.Items.Add(item);
            TreeMat.IsExpanded = true;
        }

        public ResultControl UpdateMesh(Database DB, RenderInterface iRen, ResultControl ResControl )
        {
            // Catch increment
            int inc = ResControl.Step;

            // --- Scalar Bar ---------------------------------------------
            if (ResControl.Result != "None")
            {
                string title = ResControl.Result;
                if (ResControl.Result.Contains("Displacement"))  // Use shorter title
                {
                    title = ResControl.Result.Replace("Displacement", "Displ.");
                }
                if (ResControl.Result == "von Mises Stress")  // Use shorter title
                {
                    title = "Stress\nvon Mises";
                }
                if (ResControl.Result == "Effective Strain")  // Use shorter title
                {
                    title = "Effective\nStrain";
                }
                iRen.ChangeScalarName(title);
                iRen.ShowScalarBar();
            }
            else
            {
                iRen.HideScalarBar();
            }

            // --- Colormaps --------------------------------------------

            // Set range of results (manual or automatic, depends on variable "Manual_Range")
            if (ResControl.ManualRange == false)
            {
                //Set automatic result range
                List<double> MinVal = new List<double>();
                List<double> MaxVal = new List<double>();

                foreach (Part p in DB.PartLib.Values)
                {
                    double[] PartRange = p.Get_ScalarRange(inc, ResControl.Result, ResControl.ResultStyle);
                    MinVal.Add(PartRange[0]);
                    MaxVal.Add(PartRange[1]);
                }
                // Calculate total result range
                ResControl.ResultRange = new double[2] { MinVal.Min(), MaxVal.Max() };
            }

            // Change Color LookupTable range
            iRen.ChangeColorRange(ResControl.ResultRange[0], ResControl.ResultRange[1]);

            // Update Parts
            foreach (Part p in DB.PartLib.Values)
            {
                p.UpdateNode(DB, inc);
                p.UpdateScalar(inc, ResControl.Result, ResControl.ResultStyle);
            }

            double[] N = iRen.Get_ClipPlane().GetNormal();
            if (N[0] < 0) iRen.SetClipPlane("-X");
            if (N[1] < 0) iRen.SetClipPlane("-Y");
            if (N[2] < 0) iRen.SetClipPlane("-Z");
            if (N[0] > 0) iRen.SetClipPlane("X");
            if (N[1] > 0) iRen.SetClipPlane("Y");
            if (N[2] > 0) iRen.SetClipPlane("Z");

            // Refresh Viewport
            iRen.Refresh();

            return ResControl;
        }

        public byte[] ProtoSerialize<T>(T record) where T : class
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, record);
                return stream.ToArray();
            }
        }

        public T ProtoDeserialize<T>(byte[] data) where T : class
        {
            using (var stream = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }

    }
}
