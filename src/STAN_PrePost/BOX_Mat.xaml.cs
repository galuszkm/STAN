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
        TreeViewItem TreeItem { get; }


        readonly Functions Fun = new Functions();

        public BOX_Mat(Material mat, TreeView Tree)
        {
            InitializeComponent();
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            Mat = mat;

            // Define Mat TreeView item
            TreeViewItem TreeMat = (TreeViewItem)Tree.Items[1];
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

    }
}
