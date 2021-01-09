using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using STAN_Database;
using System.Globalization;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy BC_Box.xaml
    /// </summary>
    public partial class BOX_BC : UserControl
    {
        BoundaryCondition BC { get; }
        Database DB { get; }
        TreeViewItem TreeItem { get; }
        RenderInterface iRen { get; }
        double ArrowScale { get; }
        double GridColWidth = 70;  // Width of Table column

        int Step;   // Current selected analysis Step

        public BOX_BC(BoundaryCondition bc, Database db, TreeView Tree, RenderInterface iren, double arrowScale, int step)
        {
            InitializeComponent();
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            BC = bc;
            DB = db;
            iRen = iren;
            ArrowScale = arrowScale;
            Step = step;

            // Define BC TreeView item
            TreeViewItem TreeBC = (TreeViewItem)Tree.Items[2];
            if (TreeBC.Items != null)
            {
                foreach (TreeViewItem i in TreeBC.Items)
                {
                    if(i.Header.ToString().Contains("BC ID " + BC.ID + ":"))
                    {
                        TreeItem = i;
                        break;
                    }
                }
            }

            // Load data
            Name.Text = BC.Name;
            Color_Box.SelectedIndex = BC.ColorID;

            if (BC.Type == "SPC") Type_Box.SelectedIndex = 0;
            if (BC.Type == "PointLoad") Type_Box.SelectedIndex = 1;

            Table.Items.Clear();

            foreach (KeyValuePair<int, MatrixST> i in BC.NodalValues)
            {
                Table.Items.Add(new SPC_Row { NID = i.Key, X = i.Value.GetFast(0, 0), Y = i.Value.GetFast(1, 0), Z = i.Value.GetFast(2, 0) });
            }
        }

        private void Type_Box_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Table.Items.Clear();
            while(Table.Columns.Count>0)
            {
                Table.Columns.RemoveAt(0);
            }

            ComboBoxItem item = (ComboBoxItem)Type_Box.SelectedItem;
            string type = item.Content.ToString();

            if (type == "Boundary SPC")
            {
                DataGridTextColumn NID_Column = new DataGridTextColumn
                {
                    Header = "Node ID",
                    Binding = new Binding("NID"),
                    Width = GridColWidth
                };

                DataGridTextColumn X_Column = new DataGridTextColumn
                {
                    Header = "Fix X",
                    Binding = new Binding("X"),
                    Width = GridColWidth
                };

                DataGridTextColumn Y_Column = new DataGridTextColumn
                {
                    Header = "Fix Y",
                    Binding = new Binding("Y"),
                    Width = GridColWidth
                };

                DataGridTextColumn Z_Column = new DataGridTextColumn
                {
                    Header = "Fix Z",
                    Binding = new Binding("Z"),
                    Width = GridColWidth
                };

                Table.Columns.Add(NID_Column);
                Table.Columns.Add(X_Column);
                Table.Columns.Add(Y_Column);
                Table.Columns.Add(Z_Column);

                Table.Items.Add(new SPC_Row { NID = 0, X=0, Y=0, Z=0 });

                // Disable Value TextBox
                XValue.IsEnabled = false;
                YValue.IsEnabled = false;
                ZValue.IsEnabled = false;
            }

            if (type == "Point load")
            {
                DataGridTextColumn NID_Column = new DataGridTextColumn
                {
                    Header = "Node ID",
                    Binding = new Binding("NID"),
                    Width = GridColWidth
                };

                DataGridTextColumn X_Column = new DataGridTextColumn
                {
                    Header = "Force X",
                    Binding = new Binding("X"),
                    Width = GridColWidth
                };

                DataGridTextColumn Y_Column = new DataGridTextColumn
                {
                    Header = "Force Y",
                    Binding = new Binding("Y"),
                    Width = GridColWidth
                };

                DataGridTextColumn Z_Column = new DataGridTextColumn
                {
                    Header = "Force Z",
                    Binding = new Binding("Z"),
                    Width = GridColWidth
                };

                Table.Columns.Add(NID_Column);
                Table.Columns.Add(X_Column);
                Table.Columns.Add(Y_Column);
                Table.Columns.Add(Z_Column);

                Table.Items.Add(new SPC_Row { NID = 0, X = 0, Y = 0, Z = 0 });
            }

            // Enable Value TextBox
            XValue.IsEnabled = true;
            YValue.IsEnabled = true;
            ZValue.IsEnabled = true;

        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Set BC name and clear Nodal Values
            BC.Name = Name.Text;
            BC.Clear();

            // Check BC selected type
            string type = Type_Box.SelectedItem.ToString().Replace("System.Windows.Controls.ComboBoxItem: ", "");

            if (type == "Boundary SPC" ) BC.Type = "SPC";
            if (type == "Point load") BC.Type = "PointLoad";

            foreach (SPC_Row row in Table.Items)
            {
                BC.Add(row.NID, new double[3] { row.X, row.Y, row.Z }, DB.NodeLib);
            }

            // Hide all actors and show this one
            foreach (BoundaryCondition bc in DB.BCLib.Values) bc.HideActor();
            BC.ColorID = Color_Box.SelectedIndex;
            BC.ShowActor();

            // Check if Clip Mode is ON based on Clip Plane Actor visibility
            bool ClipMode = false;
            if (iRen.Get_ClipPlaneActor().GetVisibility() == 1) ClipMode = true;

            // Update Arrow Actors
            BC.Update_Arrows(DB.NodeLib, ArrowScale, Step, ClipMode);

            // Refresh Viewport
            iRen.Refresh();

            //Change names in TreeView
            TreeItem.Header = "BC ID " + BC.ID + ": " + BC.Name;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Table.Items.Clear();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            // Clear Table
            Table.Items.Clear();

            if (Clipboard.ContainsText())
            {
                string[] text = Regex.Split(Clipboard.GetText(TextDataFormat.Text), "\n");

                if (text.Length > 1)
                {
                    foreach(string s in text)
                    {
                        if(text.Length>0)
                        {
                            string[] line = s.Split(',');
                            if(line.Length!=4)
                            {
                                line = s.Split(' ');
                                if (line.Length!=4)
                                {
                                    line = s.Split('\t');
                                }
                            }
                            if (line.Length == 4)
                            {
                                try
                                {
                                    Table.Items.Add(new SPC_Row
                                    {
                                        NID = int.Parse(line[0]),
                                        X = double.Parse(line[1], CultureInfo.InvariantCulture),
                                        Y = double.Parse(line[2], CultureInfo.InvariantCulture),
                                        Z = double.Parse(line[3], CultureInfo.InvariantCulture),
                                    });
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }

        private void Table_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.V)
                {
                    Table.Items.Clear();
                    string type = Type_Box.SelectedItem.ToString().Replace("System.Windows.Controls.ComboBoxItem: ", "");
                    string PasteText = Clipboard.GetText(TextDataFormat.Text);
                    List<string> temp = Regex.Split(PasteText, @"\n").ToList();
                    foreach (string s in temp)
                    {
                        try
                        {
                            List<string> data = Regex.Split(s, @"\t").ToList();

                            if (type == "Boundary SPC" || type == "Point load")
                            {
                                Table.Items.Add(new SPC_Row
                                {
                                    NID = int.Parse(data[0]),
                                    X = double.Parse(data[1]),
                                    Y = double.Parse(data[2]),
                                    Z = double.Parse(data[3])
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        public struct SPC_Row
        {
            public int NID { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
        }

        private void AddSelected(object sender, RoutedEventArgs e)
        {

        }
    }
}
