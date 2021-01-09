using System.Windows;
using System.Windows.Controls;
using STAN_Database;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy BC_Box.xaml
    /// </summary>
    public partial class BOX_Part : UserControl
    {
        Part P { get; }
        Database DB { get; }
        TreeViewItem TreeItem { get; }

        RenderInterface iRen;

        readonly Functions Fun = new Functions();

        public BOX_Part(Part p, Database db, TreeView Tree, RenderInterface iren)
        {
            InitializeComponent();
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            P = p;
            DB = db;
            iRen = iren;

            // Define BC TreeView item
            TreeViewItem TreePart = (TreeViewItem)Tree.Items[0];
            if (TreePart.Items != null)
            {
                foreach (TreeViewItem i in TreePart.Items)
                {
                    if(i.Header.ToString().Contains("Part ID " + P.ID + ":"))
                    {
                        TreeItem = i;
                        break;
                    }
                }
            }

            // Load data
            Name.Text = P.Name;

            // Load Materials
            if (DB.MatLib != null && DB.MatLib.Count > 0)
            {
                foreach (Material mat in DB.MatLib.Values)
                {
                    Material_Box.Items.Add("Mat ID " + mat.ID.ToString() + ": " + mat.Name);
                }
            }

            //Load Color
            Color_Box.SelectedIndex = P.ColorID;

            // Load FE types
            if (DB.FELib.FE.Count > 0)
            {
                foreach (string type in DB.FELib.FE.Keys)
                {
                    if (type.Contains("HEX"))
                    {
                        HEX_Type.Items.Add(type);
                    }
                    if (type.Contains("PENTA"))
                    {
                        PENTA_Type.Items.Add(type);
                    }
                    if (type.Contains("TET"))
                    {
                        TETRA_Type.Items.Add(type);
                    }
                }
            }

            // Select right material from list if it should be
            // if MatID is 0 just leave it, selected field will be empty - right way for new model
            if (P.Get_MatID() != 0)
            {
                for (int i = 0; i < Material_Box.Items.Count; i++)
                {
                    if (P.Get_MatID() == int.Parse(Fun.Between(Material_Box.Items[i].ToString(), "ID", ":")))
                    {
                        Material_Box.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Select FE type 
            for (int i = 0; i < HEX_Type.Items.Count; i++)
            {
                if (P.Get_FEtype()[0] == HEX_Type.Items[i].ToString())
                {
                    HEX_Type.SelectedIndex = i;
                    break;
                }
            }
            for (int i = 0; i < PENTA_Type.Items.Count; i++)
            {
                if (P.Get_FEtype()[1] == PENTA_Type.Items[i].ToString())
                {
                    PENTA_Type.SelectedIndex = i;
                    break;
                }
            }
            for (int i = 0; i < TETRA_Type.Items.Count; i++)
            {
                if (P.Get_FEtype()[2] == TETRA_Type.Items[i].ToString())
                {
                    TETRA_Type.SelectedIndex = i;
                    break;
                }
            }

        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Set BC name and clear Nodal Values
            P.Name = Name.Text;

            //Change names in TreeView
            TreeItem.Header = "Part ID " + P.ID + ": " + P.Name;

            // Assign Material
            if (Material_Box.SelectedItem != null)
            {
                int id = int.Parse(Fun.Between(Material_Box.SelectedItem.ToString(), "ID", ":"));
                P.Set_MatID(id, DB);
            }

            // Assign Color
            P.ColorID = Color_Box.SelectedIndex;
            P.SetColor(P.ColorID);

            // Assign FE types
            P.Assign_FEtype(DB, HEX_Type.SelectedItem.ToString(), 
                                PENTA_Type.SelectedItem.ToString(), 
                                TETRA_Type.SelectedItem.ToString());

            // Refresh Viewport
            iRen.Refresh();
        }

    }
}
