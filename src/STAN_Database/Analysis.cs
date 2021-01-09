using ProtoBuf;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class Analysis
    {
        [ProtoMember(1)] private string Type;
        [ProtoMember(2)] private string LinSolver;
        [ProtoMember(3)] private double LinSolverTolerance;
        [ProtoMember(4)] private int LinSolverIterMax;
        [ProtoMember(5)] private int IncNumb;
        [ProtoMember(6)] private int Result_StepNo;

        public Analysis()
        {
            Type = "Linear_Statics";
            LinSolver = "CG";
            LinSolverTolerance = 1.0e-6;
            LinSolverIterMax = 0;
            IncNumb = 0;

            // Result status
            Result_StepNo = 0;
        }
        
        public void SetAnalysisType(string type)
        {
            Type = type;
        }

        public string GetAnalysisType()
        {
            return Type;
        }

        public void SetLinSolver(string linsol)
        {
            LinSolver = linsol;
        }

        public string GetLinSolver()
        {
            return LinSolver;
        }

        public void SetLinSolverTolerance(double tolerance)
        {
            LinSolverTolerance = tolerance;
        }

        public double GetLinSolverTolerance()
        {
            return LinSolverTolerance;
        }

        public void SetLinSolverMaxIter(int maxiter)
        {
            LinSolverIterMax = maxiter;
        }

        public int GetLinSolverMaxIter()
        {
            return LinSolverIterMax;
        }

        public void SetIncNumb(int inc)
        {
            IncNumb = inc;
        }

        public int GetIncNumb()
        {
            return IncNumb;
        }

        public int GetResultStepNo()
        {
            return Result_StepNo;
        }

        public void SetResultStepNo(int inc)
        {
            Result_StepNo = inc;
        }
    }
}
