using ProtoBuf;
using System.Collections.Generic;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class Material
    {
        [ProtoMember(1)] public int ID { get; }
        [ProtoMember(2)] public string Type { get; set; }
        [ProtoMember(3)] public string Name { get; set; }
        [ProtoMember(4)] public double E { get; set; }
        [ProtoMember(5)] public double Poisson { get; set; }
        [ProtoMember(6)] public int ColorID { get; set; }

        // Solver properties
        private MatrixST ElasticMatrix;

        public Material(int id)
        {
            // Assign data
            ID = id;
            Type = "Elastic";
            ColorID = ID % 9;

            // Default values of elastic properties
            E = -999;
            Poisson = -999;
        }

        public void SetElastic(double Young, double PoissonRatio)
        {
            // Set elastic properties
            E = Young;
            Poisson = PoissonRatio;

            // Calculate elastic D matrix
            ElasticMatrix = new MatrixST(6, 6);
            double lambda = (E * Poisson) / ((1 - 2 * Poisson) * (1 + Poisson));
            double G = (0.5 * E) / (1 + Poisson);

            ElasticMatrix.SetFast(0, 0, lambda + (2 * G));
            ElasticMatrix.SetFast(0, 1, lambda);
            ElasticMatrix.SetFast(0, 2, lambda);
            ElasticMatrix.SetFast(1, 0, lambda);
            ElasticMatrix.SetFast(1, 1, lambda + (2 * G));
            ElasticMatrix.SetFast(1, 2, lambda);
            ElasticMatrix.SetFast(2, 0, lambda);
            ElasticMatrix.SetFast(2, 1, lambda);
            ElasticMatrix.SetFast(2, 2, lambda + (2 * G));
            ElasticMatrix.SetFast(3, 3, G);
            ElasticMatrix.SetFast(4, 4, G);
            ElasticMatrix.SetFast(5, 5, G);


        }

        public MatrixST GetElastic()
        {
            return ElasticMatrix;
        }

        private double[] GetColor()
        {
            List<double[]> Colors = new List<double[]>
            {
                new double[3]{  12, 197,  19 },
                new double[3]{ 141, 245, 145 },
                new double[3]{ 109, 232, 226 },
                new double[3]{  42,  85, 199 },
                new double[3]{ 223, 232,  28 },
                new double[3]{ 236, 134,  64 },
                new double[3]{ 216,  52,  52 },
                new double[3]{ 184,  61, 241 },
                new double[3]{ 110,  10, 120 }
            };
            return Colors[ColorID];
        }
    }
}
