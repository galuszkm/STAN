using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using STAN_Database;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy BC_Box.xaml
    /// </summary>
    public partial class BOX_Mat : UserControl
    {
        Material Mat { get; }
        Database DB { get; }
        TreeViewItem TreeMat { get; }
        TreeViewItem TreeItem { get; }


        readonly Functions Fun = new Functions();

        public BOX_Mat(Material mat, Database db, TreeView Tree)
        {
            InitializeComponent();
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            DB = db;
            Mat = mat;

            // Define Mat TreeView item
            TreeMat = (TreeViewItem)Tree.Items[1];
            if (TreeMat.Items != null)
            {
                foreach (TreeViewItem i in TreeMat.Items)
                {
                    if(i.Header.ToString().Contains("Mat ID " + Mat.ID + ":"))
                    {
                        TreeItem = i;
                        break;
                    }
                }
            }

            // Load name
            Name.Text = Mat.Name;

            // Load type
            for(int i=0; i<Type_Box.Items.Count; i++)
            {
                ComboBoxItem item = (ComboBoxItem)Type_Box.Items[i];
                if(item.Content.ToString() == Mat.Type)
                {
                    Type_Box.SelectedIndex = i;
                    break;
                }
            }

            //Load Color
            Color_Box.SelectedIndex = Mat.ColorID;

            //Load values
            if (Mat.E > 0) Young.Text = Mat.E.ToString(CultureInfo.InvariantCulture);
            if (Mat.Poisson > 0) Poisson.Text = Mat.Poisson.ToString(CultureInfo.InvariantCulture);

        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Set BC name and clear Nodal Values
            Mat.Name = Name.Text;

            //Change names in TreeView
            TreeItem.Header = "Mat ID " + Mat.ID + ": " + Mat.Name;

            // Assign Color
            Mat.ColorID = Color_Box.SelectedIndex;

            //Assign values
            if (double.TryParse(Young.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                Mat.E = double.Parse(Young.Text, CultureInfo.InvariantCulture);
            }
            if (double.TryParse(Poisson.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                Mat.Poisson = double.Parse(Poisson.Text, CultureInfo.InvariantCulture);
            }

        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            // Remove Material from Database
            DB.MatLib.Remove(Mat.ID);

            // Remove Material from Parts
            foreach (Part p in DB.PartLib.Values)
            {
                if(p.Get_MatID() == Mat.ID)
                {
                    p.Set_MatID(0, DB);
                }
            }

            // Remove BC from TreeView
            if (TreeMat.Items != null)
            {
                foreach (TreeViewItem i in TreeMat.Items)
                {
                    if (i.Header.ToString().Contains("Mat ID " + Mat.ID + ":"))
                    {
                        TreeMat.Items.Remove(i);
                        break;
                    }
                }
            }
        }
    }
}
