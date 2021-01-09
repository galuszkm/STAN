using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Kitware.VTK;
using STAN_Database;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy ExportWindow.xaml
    /// </summary>
    public partial class ExportWindow : Window
    {
        Database DB;
        Functions fun = new Functions();

        public ExportWindow(Database db)
        {
            InitializeComponent();
            Width = 420;
            Height = 467;
            DB = db;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResultType.ItemsSource = TreeViewModel.SetTemplate();
            for (int i = 0; i < DB.AnalysisLib.GetIncNumb() + 1; i++)
            {
                Step.Items.Add(new CheckBox { Content = "Increment " + i.ToString(), IsChecked=true });
            }
            foreach(Part P in DB.PartLib.Values)
            {
                Part.Items.Add(new CheckBox { Content = "PID " + P.ID.ToString() + ": " + P.Name, IsChecked = true });
            }

            // Update
            TreeViewModel Displacement = (TreeViewModel)ResultType.Items[0]; Displacement.VerifyCheckedState();
            TreeViewModel Stress = (TreeViewModel)ResultType.Items[1]; Stress.VerifyCheckedState();
            TreeViewModel Strain = (TreeViewModel)ResultType.Items[2]; Strain.VerifyCheckedState();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "VTU File |*.vtu",
                FilterIndex = 0,
                RestoreDirectory = true,
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string Prefix = fun.BeforeLast(dialog.FileName, ".");

                vtkXMLUnstructuredGridWriter writter = vtkXMLUnstructuredGridWriter.New();

                if (Format.SelectedIndex == 0) writter.SetDataModeToBinary();
                if (Format.SelectedIndex == 1) writter.SetDataModeToAscii();

                // Set Result List
                TreeViewModel Displacement = (TreeViewModel)ResultType.Items[0];
                TreeViewModel Stress = (TreeViewModel)ResultType.Items[1];
                TreeViewModel Strain = (TreeViewModel)ResultType.Items[2];

                List<string> res = Displacement.GetSelectedItems();
                res.AddRange(Strain.GetSelectedItems());
                res.AddRange(Stress.GetSelectedItems());

                // Check Step number
                int StepNumb = 0;
                foreach (CheckBox item in Step.Items)
                {
                    if (item.IsChecked == true) StepNumb++;
                }

                // Check Part number
                int PartNumb = 0;
                foreach (CheckBox item in Part.Items)
                {
                    if (item.IsChecked == true) PartNumb++;
                }

                int index = 0;
                foreach (CheckBox StepItem in Step.Items)
                {
                    if (StepItem.IsChecked == true)
                    {
                        int inc = int.Parse(StepItem.Content.ToString().Replace("Increment ", ""));
                        vtkAppendFilter Append = vtkAppendFilter.New();

                        foreach (CheckBox PartItem in Part.Items)
                        {
                            if (PartItem.IsChecked == true)
                            {
                                int PID = int.Parse(fun.Between(PartItem.Content.ToString(), "PID", ":"));
                                Append.AddInput(DB.PartLib[PID].ExportGrid(DB, res, inc));
                            }
                        }
                        writter.SetInput(Append.GetOutput());
                        writter.SetFileName(Prefix + "_" + inc.ToString("000") + ".vtu");
                        writter.Write();
                        index++;
                    }
                }

                MessageBox.Show("Result file succesfully exported");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        
    }

}