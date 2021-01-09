using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STAN_Database;

namespace STAN_PrePost
{
    public class ResultControl
    {
        public string Result { get; set; }
        public string ResultStyle { get; set; }
        public bool ManualRange { get; set; }
        public double[] ResultRange { get; set; }
        public int Step { get; set; }
        public int LegendStyle { get; set; }

        public ResultControl()
        {
            Result = "None";                         // Current result
            ResultStyle = "Contour Map";             // Current result style
            ResultRange = new double[2] { 0, 1 };    // Current result range
            ManualRange = false;                     // Switch to Manual range
            Step = 0;
            LegendStyle = 1;
        }
    }
}
