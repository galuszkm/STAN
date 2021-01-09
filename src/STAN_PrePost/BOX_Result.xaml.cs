using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using STAN_Database;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy BOX_Result.xaml
    /// </summary>
    public partial class BOX_Result : UserControl
    {
        Functions Fun = new Functions();
        ResultControl ResControl;
        Database DB;
        RenderInterface iRen;
        bool Initial_Load;

        public BOX_Result(ResultControl res, Database db, RenderInterface iren)
        {
            InitializeComponent();
            ResControl = res;
            DB = db;
            iRen = iren;

            // Set changes as initial load - not update mesh
            Initial_Load = true;

            // Select current Result general type (Stress, strain, etc.)
            foreach (ComboBoxItem i in Result_ComboBox.Items)
            {
                if (ResControl.Result.Contains(i.Content.ToString()))
                {
                    Result_ComboBox.SelectedItem = i;
                    break;
                }
            }

            // Select current Result direction (XX, YY, XY, etc.)
            foreach (string i in ResultBox.Items)
            {
                if (i.ToString() == ResControl.Result)
                {
                    ResultBox.SelectedItem = i;
                    break;
                }
            }

            // Select current Result style ("Element Max", etc.)
            foreach (ComboBoxItem i in ResultStyle_ComboBox.Items)
            {
                if(i.Content.ToString() == ResControl.ResultStyle)
                {
                    ResultStyle_ComboBox.SelectedItem = i;
                    break;
                }
            }

            // Set Manual or Automatic range
            if (ResControl.ManualRange == true)
            {
                Manual_CheckBox.IsChecked = true;
                ResultManualWindow.IsEnabled = true;
            }
            else
            {
                Manual_CheckBox.IsChecked = false;
                ResultManualWindow.IsEnabled = false;
            }

            // Set Current analysis step
            Step.Content = ResControl.Step.ToString();

            // Update Manual Range textboxes
            Update_RangeText();


            // Set window as loaded - from now on the mesh will be updated
            Initial_Load = false;

            // Disable this window if there are no results in Database
            if(DB.AnalysisLib.GetIncNumb() > 0)
            {
                ResultWindow.IsEnabled = true;
            }
            else
            {
                ResultWindow.IsEnabled = false;
            }
        }

        private void Result_Changed(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as ComboBox).Name == "Result_ComboBox")
            {
                // Clear ResultBox if exists
                if (ResultBox != null)
                {
                    ResultBox.Items.Clear();
                }

                // Catch selected Result type
                ComboBoxItem item = (ComboBoxItem) Result_ComboBox.SelectedItem;
                string type = item.Content.ToString();

                if (type == "None")
                {
                    ResControl.Result = "None";
                }
                if (type == "Displacement")
                {
                    ResultBox.Items.Add("Displacement X");
                    ResultBox.Items.Add("Displacement Y");
                    ResultBox.Items.Add("Displacement Z");
                    ResultBox.Items.Add("Total Displacement");
                }
                if (type == "Stress")
                {
                    ResultBox.Items.Add("von Mises Stress");
                    ResultBox.Items.Add("Stress P1");
                    ResultBox.Items.Add("Stress P2");
                    ResultBox.Items.Add("Stress P3");
                    ResultBox.Items.Add("Stress XX");
                    ResultBox.Items.Add("Stress YY");
                    ResultBox.Items.Add("Stress ZZ");
                    ResultBox.Items.Add("Stress XY");
                    ResultBox.Items.Add("Stress YZ");
                    ResultBox.Items.Add("Stress XZ");
                }
                if (type == "Strain")
                {
                    ResultBox.Items.Add("Effective Strain");
                    ResultBox.Items.Add("Strain P1");
                    ResultBox.Items.Add("Strain P2");
                    ResultBox.Items.Add("Strain P3");
                    ResultBox.Items.Add("Strain XX");
                    ResultBox.Items.Add("Strain YY");
                    ResultBox.Items.Add("Strain ZZ");
                    ResultBox.Items.Add("Strain XY");
                    ResultBox.Items.Add("Strain YZ");
                    ResultBox.Items.Add("Strain XZ");
                }
            }

            if ((sender as ComboBox).Name == "ResultStyle_ComboBox")
            {
                ComboBoxItem item = (ComboBoxItem)ResultStyle_ComboBox.SelectedItem;
                ResControl.ResultStyle = item.Content.ToString();
            }

            // Update Mesh
            ResControl = Fun.UpdateMesh(DB, iRen, ResControl);
            Update_RangeText();
        }

        private void Result_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Initial_Load == false && ResultBox.Items.Count > 0)
            {
                // Catch selected result in ResultBox
                ResControl.Result = ResultBox.SelectedItem.ToString();
                
                // Update View
                ResControl = Fun.UpdateMesh(DB, iRen, ResControl);
                Update_RangeText();
                
            }
        }

        private void Manual_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool status = (bool)(sender as CheckBox).IsChecked;

            if (status == true)
            {
                ResultManualWindow.IsEnabled = true;
                ResControl.ManualRange = true;
            }
            else
            {
                ResultManualWindow.IsEnabled = false;
                ResControl.ManualRange = false;

                if (Initial_Load == false)
                {
                    ResControl = Fun.UpdateMesh(DB, iRen, ResControl);
                    Update_RangeText();
                }
            }
        }

        private void Manual_Range_Apply(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(Manual_Min.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                ResControl.ResultRange[0] = double.Parse(Manual_Min.Text, CultureInfo.InvariantCulture);

            if (double.TryParse(Manual_Max.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                ResControl.ResultRange[1] = double.Parse(Manual_Max.Text, CultureInfo.InvariantCulture);

            ResControl = Fun.UpdateMesh(DB, iRen, ResControl);
        }

        private void StepClick(object sender, RoutedEventArgs e)
        {
            // Hide BC actors
            foreach (BoundaryCondition bc in DB.BCLib.Values) bc.HideActor();

            string s = (sender as Button).Name.ToString();
            if (s.Contains("NextStep"))
            {
                int step = int.Parse(Step.Content.ToString());
                if (step < DB.AnalysisLib.GetIncNumb())
                {
                    ResControl.Step = step + 1;
                    Step.Content = (step + 1).ToString();
                    ResControl = Fun.UpdateMesh(DB, iRen, ResControl);
                    Update_RangeText();
                }
            }
            if (s.Contains("PrevStep"))
            {
                int step = int.Parse(Step.Content.ToString());
                if (step > 0)
                {
                    ResControl.Step = step - 1;
                    Step.Content = (step - 1).ToString();
                    ResControl = Fun.UpdateMesh(DB, iRen, ResControl);
                    Update_RangeText();
                }
            }
        }

        private void Update_RangeText()
        {
            if (ResControl.ManualRange == false)
            {
                // Set data format
                string format = "e3";

                // Set text in Manual Result range boxes
                if (ResControl.ResultRange[1] >= 1e-3)
                {
                    if (ResControl.ResultRange[1] >= 1e-1)
                    {
                        format = "f2";
                    }
                    else
                    {
                        format = "f4";
                    }
                }

                Manual_Min.Text = ResControl.ResultRange[0].ToString(format, CultureInfo.InvariantCulture);
                Manual_Max.Text = ResControl.ResultRange[1].ToString(format, CultureInfo.InvariantCulture);

            }
        }
    }
}
