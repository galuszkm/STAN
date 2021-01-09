using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using STAN_Database;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Database and general functions
        Database DB;
        Functions Fun;
        RenderInterface iRen;
        ResultControl ResControl;

        // Other objects
        bool ModelLoaded;                               // To speed up loading

        double BC_Arrow_scale = 1;
        bool ClipMode = false;


        public MainWindow()
        {
            InitializeComponent();

            // Initialize
            Fun = new Functions();                 // General methods
            iRen = new RenderInterface();          // 3D Graphics interface
            ResControl = new ResultControl();      // Result Display object

            // Deactive buttons
            OpenButton.IsEnabled = false;
            ImportButton.IsEnabled = false;
            TopButtonBar.IsEnabled = false;

            // Deactive Menu items
            Open.IsEnabled = false;
            Import.IsEnabled = false;
            Save.IsEnabled = false;

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CreateNewModel();
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            DialogResult dialogResult = System.Windows.Forms.MessageBox.Show(
                "This project will be deleted", "New Project", MessageBoxButtons.OKCancel);
            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                // Create new project
                CreateNewModel();
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            bool DataOk = false;

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "STAN Database (*.STdb)|*.STdb",
                FilterIndex = 0,
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Load Database from binary file (or try)
                Tuple<Database, bool> Data = Fun.OpenDatabase(dialog.FileName);
                DataOk = Data.Item2;
                if (Data.Item2 == true)
                {
                    DB = Data.Item1;
                }
            }

            if (DataOk == true)
            {
                // Add Parts to GUI
                foreach (Part p in DB.PartLib.Values)
                {
                    p.SetProperty(DB);
                    Fun.AddPart2GUI(p, iRen, PartBox, Tree);
                }

                // Add Boundary Conditions to GUI
                foreach (BoundaryCondition BC in DB.BCLib.Values)
                {
                    BC.Initialize();
                    Fun.AddBC2GUI(BC, iRen, Tree, false);
                    BC.Update_Arrows(DB.NodeLib, BC_Arrow_scale, ResControl.Step, ClipMode);   // Create BC arrows actors
                }

                // Add Materials to GUI
                foreach (Material Mat in DB.MatLib.Values)
                {
                    Fun.AddMat2GUI(Mat, Tree);
                }

                // Load result to Parts if exists
                if (DB.AnalysisLib.GetResultStepNo() > 0)
                {
                    foreach (Part p in DB.PartLib.Values)
                    {
                        p.Set_ColorTable(iRen.Get_ColorTable());
                        p.Load_Scalar(DB);
                    }
                    // Activate Result Tree item
                    TreeResult.IsEnabled = true;
                    TreeResult.Header = "Results";

                }

                // Refresh Viewport and Set Model as loaded
                iRen.InitializeFaces();
                iRen.FitView();
                iRen.Refresh();
                ModelLoaded = true;
                iRen.ModelLoaded = true;

                // Set size of Clip Plane
                iRen.SetClipPlaneScale(DB.GetBounds());
                iRen.SetClipPlane("X");  // Set initial section normal to X

                // Active/Deactive buttons
                OpenButton.IsEnabled = false;
                ImportButton.IsEnabled = false;
                TopButtonBar.IsEnabled = true;

                // Active/Deactive Menuitems
                Open.IsEnabled = false;
                Import.IsEnabled = false;
                Save.IsEnabled = true;

                // Active TreeView
                Tree.IsEnabled = true;

            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = "STAN Database (*.STdb)|*.STdb",
                FilterIndex = 0,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Create Part Information
                DB.Info.ClearPartInfo();
                foreach (Part p in DB.PartLib.Values)
                {
                    DB.Info.AddPart(p.ID);
                    DB.Info.GetPart(p.ID).SetData(p.ColorID, p.Get_MatID(), p.Name, p.Get_FEtype());
                }

                byte[] Export = Fun.ProtoSerialize(DB);
                string path = dialog.FileName;
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(Export, 0, Export.Length);
                }
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Nastran bulk file (*.bdf)|*.bdf",
                FilterIndex = 0,
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    DB = new Database();    // Create new Database
                    if (dialog.FileName.Contains(".bdf"))
                    {
                        // Nastran bulk data file reading
                        DB.ReadNastranMesh(dialog.FileName);
                        DB.Set_nDOF();  // Calculate number of Degrees of Freedom
                    }

                    // Add Parts to Viewport and TreeView
                    foreach (Part p in DB.PartLib.Values)
                    {
                        Fun.AddPart2GUI(p, iRen, PartBox, Tree);
                    }

                    // Refresh Viewport and Set Model as loaded
                    iRen.InitializeFaces();
                    iRen.FitView();
                    iRen.Refresh();
                    ModelLoaded = true;
                    iRen.ModelLoaded = true;

                    // Set size of Clip Plane
                    iRen.SetClipPlaneScale(DB.GetBounds());
                    iRen.SetClipPlane("X");  // Set initial section normal to X


                    // Active/Deactive buttons
                    OpenButton.IsEnabled = false;
                    ImportButton.IsEnabled = false;
                    TopButtonBar.IsEnabled = true;

                    // Active/Deactive Menuitems
                    Open.IsEnabled = false;
                    Import.IsEnabled = false;
                    Save.IsEnabled = true;

                    // Active TreeView
                    Tree.IsEnabled = true;
                }
                catch
                {
                    System.Windows.Forms.MessageBox.Show("Mesh import error");
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ExportWindow Export = new ExportWindow(DB);
            Export.Owner = this;
            Export.ShowDialog();
        }


        // ======================= VISUALIZATION AND VIEW METHODS ===========================

        private void CreateNewModel()
        {
            // Clear Database
            DB = null;
            ModelLoaded = false;

            // Create new View
            iRen.CreateViewport(RenderingWindow);

            // Remove initial rectangle with background color
            RenderingWindow.Children.Remove(InitialBackground);

            // Clear Treeview, PartBox and PropertyBox
            TreePart.Items.Clear();
            TreeBC.Items.Clear();
            TreeMat.Items.Clear();
            PartBox.Items.Clear();
            PropertyBox.Children.Clear();

            // Clear Result Control
            ResControl = new ResultControl();

            // Active/Deactive buttons
            OpenButton.IsEnabled = true;
            ImportButton.IsEnabled = true;
            TopButtonBar.IsEnabled = false;
            TreeResult.IsEnabled = false;
            TreeResult.Header = "Results (not avaliable)";

            // Active Menu items
            Open.IsEnabled = true;
            Import.IsEnabled = true;

            // Deactive TreeView
            Tree.IsEnabled = false;

        }

        private void PartBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelLoaded)
            {
                foreach (Part p in DB.PartLib.Values)
                {
                    p.Get_Actor().VisibilityOff();
                    p.GetEdges().VisibilityOff();
                }
                foreach (string p in PartBox.SelectedItems)
                {
                    int id = int.Parse(Fun.Between(p, "PID", ":"));
                    DB.PartLib[id].Get_Actor().VisibilityOn();
                    DB.PartLib[id].GetEdges().VisibilityOn();
                }

                // Refresh Viewport
                iRen.Refresh();
            }
        }

        private void ChangeColor(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button b = sender as System.Windows.Controls.Button;

            if (b.Name == "Background_T1")
            {
                iRen.ChangeBackgroundColor(false, new double[3] { 255, 255, 255 }, new double[3] { 255, 255, 255 });
            }
            if (b.Name == "Background_T2")
            {
                iRen.ChangeBackgroundColor(false, new double[3] { 82, 87, 110 }, new double[3] { 82, 87, 110 });
            }
            if (b.Name == "Background_T3")
            {
                iRen.ChangeBackgroundColor(true, new double[3] { 163, 163, 163 }, new double[3] { 45, 85, 125 });
            }
            if (b.Name == "Background_T4")
            {
                iRen.ChangeBackgroundColor(true, new double[3] { 255, 255, 255 }, new double[3] { 150, 150, 150 });
            }
            if (b.Name == "Label_T1")
            {
                iRen.ChangeLabelColor(new double[3] { 1.0, 1.0, 1.0 });
            }
            if (b.Name == "Label_T2")
            {
                iRen.ChangeLabelColor(new double[3] { 0.0, 0.0, 0.0 });
            }
            if( b.Name == "LegendStyle1" && DB.AnalysisLib.GetResultStepNo() > 0)
            {
                iRen.ChangeLegendStyle(1);
                ResControl.LegendStyle = 1;
                Fun.UpdateMesh(DB, iRen, ResControl);
            }
            if (b.Name == "LegendStyle2" && DB.AnalysisLib.GetResultStepNo() > 0)
            {
                iRen.ChangeLegendStyle(2);
                ResControl.LegendStyle = 2;
                Fun.UpdateMesh(DB, iRen, ResControl);
            }
            iRen.Refresh();
        }

        private void WireMode(object sender, RoutedEventArgs e)
        {
            string s = (sender as System.Windows.Controls.MenuItem).Header.ToString();
            if (DB != null && DB.PartLib.Count > 0)
            {
                foreach (Part p in DB.PartLib.Values)
                {
                    if (s == "ON") p.SetWireframe(1);
                    if (s == "OFF") p.SetWireframe(0);
                }
                foreach (BoundaryCondition BC in DB.BCLib.Values)
                {
                    if (s == "ON") BC.SetWireframe(1);
                    if (s == "OFF") BC.SetWireframe(0);
                }
                iRen.Refresh();
            }
        }

        private void Transparent(object sender, RoutedEventArgs e)
        {
            string s = (sender as System.Windows.Controls.MenuItem).Header.ToString();
            if (DB != null && DB.PartLib.Count > 0)
            {
                foreach (Part p in DB.PartLib.Values)
                {
                    if (s == "ON") p.Get_Actor().GetProperty().SetOpacity(0.2);
                    if (s == "OFF") p.Get_Actor().GetProperty().SetOpacity(1.0);
                }
                iRen.Refresh();
            }
        } 

        // =============================== BUTTON METHODS ==================================

        private void Mat_Button_Click(object sender, RoutedEventArgs e)
        {
            // Clear current Box
            PropertyBox.Children.Clear();

            //Create new Material
            Material NewMat = new Material(DB.MatLib.Count + 1) { Name = "New Material" };
            DB.MatLib.Add(NewMat.ID, NewMat);

            // Add Material to TreeView
            TreeViewItem item = new TreeViewItem()
            {
                Header = "Mat ID " + NewMat.ID.ToString() + ": " + NewMat.Name,
                IsSelected = true
            };
            TreeMat.Items.Add(item);
            TreeMat.IsExpanded = true;
        }

        private void BC_Button_Click(object sender, RoutedEventArgs e)
        {
            // Clear current Box
            PropertyBox.Children.Clear();

            // Create new BC, add to Database and Viewport
            BoundaryCondition NewBC = new BoundaryCondition("New Boundary Condition", "SPC", DB.BCLib.Count+1);
            NewBC.Initialize();
            DB.BCLib.Add(NewBC.ID, NewBC);

            // Add new BC to GUI
            Fun.AddBC2GUI(NewBC, iRen, Tree, true);

            // Add Property Card
            BOX_BC NewBox = new BOX_BC(NewBC, DB, Tree, iRen, BC_Arrow_scale, ResControl.Step);
            PropertyBox.Children.Add(NewBox);
        }

        private void RUN_Click(object sender, RoutedEventArgs e)
        {
            // >>>>>>>>>>>>>>>>>>>>>>>>>>> TEMPORARY to test - remove in final release <<<<<<<<<<<<<<<<<<<<<<<<<
            DB.AnalysisLib.SetAnalysisType("Linear_Statics");
            DB.AnalysisLib.SetIncNumb(1);
            // >>>>>>>>>>>>>>>>>>>>>>>>>>> TEMPORARY to test - remove in final release <<<<<<<<<<<<<<<<<<<<<<<<<

            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = "STAN Database (*.STdb)|*.STdb",
                FilterIndex = 0,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Create Part Information
                DB.Info.ClearPartInfo();
                foreach (Part p in DB.PartLib.Values)
                {
                    DB.Info.AddPart(p.ID);
                    DB.Info.GetPart(p.ID).SetData(p.ColorID, p.Get_MatID(), p.Name, p.Get_FEtype());
                }

                byte[] Export = Fun.ProtoSerialize(DB);
                string path = dialog.FileName;
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(Export, 0, Export.Length);
                }

                // ---------- RUN C# Solver ------------------

                // Check Parts
                string PartID_NoMat = "";
                foreach(Part p in DB.PartLib.Values)
                {
                    if (p.Get_MatID() == 0)
                    {
                        PartID_NoMat += "\n  " + p.ID.ToString();
                    }
                }

                if (PartID_NoMat.Length > 0)
                {
                    System.Windows.MessageBox.Show("No material assigned to Parts: " + PartID_NoMat + "\n\n" +
                        "Solver prevented from running.", "Error");
                }
                else
                {
                    Process process = new Process();
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = "STAN_Solver.exe";
                    info.Arguments = path;
                    process.StartInfo = info;
                    process.Start();
                }

            }
        }

        // ============================= VIEWPORT BUTTON METHODS ============================

        private void SectionClick(object sender, RoutedEventArgs e)
        {
            if (ModelLoaded == true)
            {
                // ChangeColor ClipMode
                if (ClipMode == true) ClipMode = false;
                else ClipMode = true;

                // Clip/Unclip Part and Show/Hide ClipPlane
                foreach (Part p in DB.PartLib.Values) p.ClipPart(iRen.Get_ClipPlane(), ClipMode);
                foreach (BoundaryCondition BC in DB.BCLib.Values) BC.ClipBC(iRen.Get_ClipPlane(), ClipMode);
                iRen.ShowClip(ClipMode);

                // Refresh Viewport
                iRen.Refresh();
            }
        }

        private void ChangeViewOri(object sender, RoutedEventArgs e)
        {
            string view = (sender as System.Windows.Controls.Button).Name;
            iRen.SetViewOri(view.Replace("ViewButton_", ""));
        }

        private void ChangeViewMode(object sender, RoutedEventArgs e)
        {
            if (ModelLoaded == true)
            {
                string view = (sender as System.Windows.Controls.Button).Name;
                if (view.Replace("ViewButton_", "") == "Transparent")
                {
                    if (DB.PartLib.First().Value.Get_Actor().GetProperty().GetOpacity() == 1.0)
                    {
                        if (DB.PartLib != null)
                        {
                            foreach (Part p in DB.PartLib.Values)
                            {
                                p.Get_Actor().GetProperty().SetOpacity(0.2);
                            }
                        }
                    }
                    else
                    {
                        foreach (Part p in DB.PartLib.Values)
                        {
                            p.Get_Actor().GetProperty().SetOpacity(1.0);
                        }
                    }
                }
                if (view.Replace("ViewButton_", "") == "Wireframe")
                {
                    if (DB.PartLib.First().Value.Get_Actor().GetProperty().GetEdgeVisibility() == 0)
                    {
                        if (DB.PartLib != null)
                        {
                            foreach (Part p in DB.PartLib.Values) p.SetWireframe(1);
                        }
                        if (DB.BCLib != null)
                        {
                            foreach (BoundaryCondition BC in DB.BCLib.Values) BC.SetWireframe(1);
                        }
                    }
                    else
                    {
                        if (DB.PartLib != null)
                        {
                            foreach (Part p in DB.PartLib.Values) p.SetWireframe(0);
                        }
                        if (DB.BCLib != null)
                        {
                            foreach (BoundaryCondition BC in DB.BCLib.Values) BC.SetWireframe(0);
                        }
                    }
                }

                // Refresh Viewport
                iRen.Refresh();
            }
        }

        // ============================= TREEVIEW METHODS ===================================

        private void TreeItemSelect(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ModelLoaded)
            {
                // Clear current Box
                PropertyBox.Children.Clear();

                // Hide all BC Actors
                if (DB.BCLib != null)
                {
                    foreach (BoundaryCondition bc in DB.BCLib.Values) bc.HideActor();
                }

                // Catch selected item Header
                TreeViewItem item = (TreeViewItem)Tree.SelectedItem;
                string label = item.Header.ToString();

                // Display BOUNDARY CONDITION Property Box
                if (label.Contains("BC ID"))
                {
                    // Catch selected BC ID
                    int ID = int.Parse(Fun.Between(label, "ID ", ":"));

                    // Update Arrows to current Analysis step and display BC Actor
                    DB.BCLib[ID].Update_Arrows(DB.NodeLib, BC_Arrow_scale, ResControl.Step, ClipMode);
                    DB.BCLib[ID].ShowActor();

                    // Add new BC Box to PropertyBox
                    BOX_BC NewBox = new BOX_BC(DB.BCLib[ID], DB, Tree, iRen, BC_Arrow_scale, ResControl.Step);
                    PropertyBox.Children.Add(NewBox);
                }

                // Display PART Property Box
                if (label.Contains("Part ID"))
                {
                    // Catch selected Part ID
                    int ID = int.Parse(Fun.Between(label, "ID ", ":"));

                    // Add new Part Box to PropertyBox
                    BOX_Part NewBox = new BOX_Part(DB.PartLib[ID], DB, Tree, iRen);
                    PropertyBox.Children.Add(NewBox);
                }

                // Display MATERIAL Property Box
                if (label.Contains("Mat ID"))
                {
                    // Catch selected Part ID
                    int ID = int.Parse(Fun.Between(label, "ID ", ":"));

                    // Add new Mat Box to PropertyBox
                    BOX_Mat NewBox = new BOX_Mat(DB.MatLib[ID], Tree);
                    PropertyBox.Children.Add(NewBox);
                }

                // Display ANALYSIS Property Box
                if (label == "Analysis")
                {
                    //Add new Analysis Bix to PropertyBox
                    BOX_Analysis NewBox = new BOX_Analysis(DB);
                    PropertyBox.Children.Add(NewBox);
                }

                // Display RESULT Property Box
                if (label == "Results")
                {
                    // Add new Result Box to PropertyBox
                    BOX_Result NewBox = new BOX_Result(ResControl, DB, iRen);
                    PropertyBox.Children.Add(NewBox);
                }

                iRen.Refresh();    // Refresh View
            }
        }

        // ============================= RESULTS METHODS ====================================

        private void LoadResults_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "STAN Database (*.STdb)|*.STdb",
                FilterIndex = 0,
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //Reading solver output file
                string Path = dialog.FileName;
                byte[] Input = File.ReadAllBytes(Path);
                Database Result = Fun.ProtoDeserialize<Database>(Input);

                // Result verification
                bool GoOn = true;

                // Check if there are any results in file
                if (Result.AnalysisLib.GetResultStepNo() == 0)
                {
                    GoOn = false;
                    System.Windows.Forms.MessageBox.Show("No results detected in file!");
                }
                if (Result.NodeLib.Count != DB.NodeLib.Count || Result.ElemLib.Count != DB.ElemLib.Count)
                {
                    GoOn = false;
                    System.Windows.Forms.MessageBox.Show("Result file not compatible with current model!");
                }

                // If results are ok
                if (GoOn == true)
                {
                    // Update NodeLib and ElemLib from Solver output
                    DB.NodeLib.Clear();
                    DB.ElemLib.Clear();
                    foreach (KeyValuePair<int, Node> n in Result.NodeLib) DB.NodeLib.Add(n.Key, n.Value);
                    foreach (KeyValuePair<int, Element> E in Result.ElemLib) DB.ElemLib.Add(E.Key, E.Value);

                    // Update Analysis 
                    DB.AnalysisLib = Result.AnalysisLib;

                    // Set current step to 0
                    ResControl.Step = 0;

                    // Load result to Parts if exists
                    if (DB.AnalysisLib.GetResultStepNo() > 0)
                    {
                        foreach (Part p in DB.PartLib.Values)
                        {
                            p.Set_ColorTable(iRen.Get_ColorTable());
                            p.Load_Scalar(DB);
                        }
                        // Activate Result Tree item
                        TreeResult.IsEnabled = true;
                        TreeResult.Header = "Results";
                    }
                    Fun.UpdateMesh(DB, iRen, ResControl);

                    // Select Results in Treeview (Reload if already selected)
                    TreePart.IsSelected = true;
                    TreeResult.IsSelected = true;

                    // Set size of Clip Plane
                    iRen.SetClipPlaneScale(DB.GetBounds());
                }
            }
        }

        private void RemoveResults_Click(object sender, RoutedEventArgs e)
        {
            DialogResult d = System.Windows.Forms.MessageBox.Show(
                "Are you sure you want to remove all results from database?",
                "Warning", MessageBoxButtons.YesNo);

            if (d == System.Windows.Forms.DialogResult.Yes)
            {
                ResControl = new ResultControl();
                foreach (Part P in DB.PartLib.Values)
                {
                    P.UpdateNode(DB, 0);
                    P.UpdateScalar(0, "None", "Element Max");
                    P.Load_Scalar(DB);
                }
                iRen.HideScalarBar();
                DB.AnalysisLib.SetResultStepNo(0);
                PropertyBox.Children.Clear();

                foreach (Element E in DB.ElemLib.Values)
                {
                    E.ClearResults();
                }
                foreach(Node N in DB.NodeLib.Values)
                {
                    N.Initialize_StepZero();
                }
                
                TreeResult.Header = "Results (not avaliable)";
                TreeResult.IsEnabled = false;
                iRen.Refresh();
            }
        }

    }
}
