using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using STAN_Database;

namespace STAN_PrePost
{
    /// <summary>
    /// Logika interakcji dla klasy BC_Box.xaml
    /// </summary>
    public partial class BOX_Analysis : UserControl
    {
        Analysis Analys { get; }

        public BOX_Analysis(Database db)
        {
            InitializeComponent();
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            Analys = db.AnalysisLib;

            // Load type
            if (Analys.GetAnalysisType() == "Linear_Statics") Type_Box.SelectedIndex = 0;

            //Load Solver type
            if (Analys.GetLinSolver() == "CG")
            {
                LinSolverType_Box.SelectedIndex = 0;
                Tolerance.IsEnabled = true;
                Tolerance.Text = Analys.GetLinSolverTolerance().ToString(CultureInfo.InvariantCulture);
                LinSolverMaxIter.IsEnabled = true;
                LinSolverMaxIter.Text = Analys.GetLinSolverMaxIter().ToString();
            }
            if (Analys.GetLinSolver() == "Cholesky")
            {
                LinSolverType_Box.SelectedIndex = 1;
                Tolerance.IsEnabled = false;
                LinSolverMaxIter.IsEnabled = false;
            }

        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Set type
            if (Type_Box.SelectedIndex == 0)
            {
                Analys.SetAnalysisType("Linear_Statics");
                Analys.SetIncNumb(1);
            }

            //Set Linear Solver
            if (LinSolverType_Box.SelectedIndex == 0)
            {
                Analys.SetLinSolver("CG");
                if (double.TryParse(Tolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    Analys.SetLinSolverTolerance(double.Parse(Tolerance.Text, CultureInfo.InvariantCulture));
                }
                else Analys.SetLinSolverTolerance(0.00);

                if (int.TryParse(LinSolverMaxIter.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    Analys.SetLinSolverMaxIter(int.Parse(Tolerance.Text, CultureInfo.InvariantCulture));
                }
                else Analys.SetLinSolverMaxIter(0);
            }
            //Set Linear Solver
            else if (LinSolverType_Box.SelectedIndex == 1)
            {
                Analys.SetLinSolver("Cholesky");
                Analys.SetLinSolverTolerance(0.00);
                Analys.SetLinSolverMaxIter(0);
            }
        }

        private void LinSolverType_Box_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LinSolverType_Box.SelectedIndex == 0)
            {
                Tolerance.IsEnabled = true;
                LinSolverMaxIter.IsEnabled = true;
            }
            else if (LinSolverType_Box.SelectedIndex == 1)
            {
                Tolerance.IsEnabled = false;
                LinSolverMaxIter.IsEnabled = false;
            }
        }
    }
}
