using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;
using STAN_Database;

namespace STAN_Solver
{
    public class SolverFunctions
    {
        public void Welcome_Messsage()
        {

            Console.WindowWidth = 62;
            Console.WindowHeight = 50;
            Console.BufferWidth = 62;
            //Console.BufferHeight = 200;

            Console.WriteLine("");
            Console.WriteLine("           #########       ##########  ##       ##           ");
            Console.WriteLine("           ##              ##      ##  ###      ##           ");
            Console.WriteLine("           ##   ##############     ##  ####     ##           ");
            Console.WriteLine("           ##         ##   ##      ##  ## ##    ##           ");
            Console.WriteLine("           #########  ##   ##########  ##  ##   ##           ");
            Console.WriteLine("                  ##  ##   ##      ##  ##   ##  ##           ");
            Console.WriteLine("                  ##  ##   ##      ##  ##    ## ##           ");
            Console.WriteLine("                  ##  ##   ##      ##  ##     ####           ");
            Console.WriteLine("           #########  ##   ##      ##  ##      ###           ");
            Console.WriteLine("                      ##                                     ");
            Console.WriteLine("                                                             ");
            Console.WriteLine("  ========================================================== ");
            Console.WriteLine("  ********************************************************** ");
            Console.WriteLine("                  STAN - STructural ANalyser                 ");
            Console.WriteLine("  ********************************************************** ");
            Console.WriteLine("      Version: DEV-1.0              Released: 06.11.2020     ");
            Console.WriteLine("      Solver: Linear, Statics                                ");
            Console.WriteLine("      Copyright (c) 2020 Michal Galuszka                     ");
            Console.WriteLine("      Contact: michal.galuszka1@gmail.com                    ");
            Console.WriteLine("  ========================================================== ");
            Console.WriteLine("                                                             ");

        }

        public byte[] ProtoSerialize<T>(T record) where T : class
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, record);
                return stream.ToArray();
            }
        }

        public T ProtoDeserialize<T>(byte[] data) where T : class
        {
            using (var stream = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }

        public void Print_Matrix(alglib.sparsematrix A, int rows, int col)
        {
            for (int i = 0; i < rows; i++)
            {
                string output = "";
                for (int j = 0; j < col; j++)
                {
                    output += alglib.sparseget(A, i, j).ToString("E2") + "  ";
                }
                Console.WriteLine(output);
            }
        }

        public void Print_Table(double[,] A, int rows, int col)
        {
            for (int i = 0; i < rows; i++)
            {
                string output = "";
                for (int j = 0; j < col; j++)
                {
                    output += A[i, j].ToString("E2") + "  ";
                }
                Console.WriteLine(output);
            }
        }

        public void Print_Vector(double[] A, int col)
        {
            string output = "";
            for (int j = 0; j < col; j++)
            {
                output += A[j].ToString("E2") + "  ";
            }
            Console.WriteLine(output);
        }

        public void Print_MatrixST(MatrixST A, int rows, int col)
        {
            for (int i = 0; i < rows; i++)
            {
                string output = "";
                for (int j = 0; j < col; j++)
                {
                    output += A.GetFast(i, j).ToString("e2") + "  ";
                }
                Console.WriteLine(output);
            }
        }


        // -------------------- Finite Element matrix methods -----------------------------

        public alglib.sparsematrix ParallelAssembly_K(Database DB, int[] nDOF_reduction, int inc, string type)
        {
            Stopwatch sw = new Stopwatch(); sw.Start();

            // Global Stiffness Matrix Initialization
            int nFixDOF = nDOF_reduction.Where(x => x == -1).Count();
            alglib.sparsecreate(DB.nDOF - nFixDOF, DB.nDOF - nFixDOF, out alglib.sparsematrix K);

            // Assembly loop

            Console.Write("   K Matrix assembly: "); // Create output in console

            Parallel.ForEach(DB.ElemLib.Values, Elem =>
            {
                MatrixST k = new MatrixST(3 * Elem.NList.Count, 3 * Elem.NList.Count);

                if(type == "Initial")
                {
                    k = Elem.K_Initial(inc, DB);
                }
                if(type == "Tangent")
                {
                    k = Elem.K_Tangent(inc, DB);
                }

                // Assembly to Global Stiffness Matrix
                for (int i = 0; i < Elem.NList.Count; i++)
                {
                    for (int m = 0; m < 3; m++)
                    {
                        for (int j = 0; j < Elem.NList.Count; j++)
                        {
                            for (int n = 0; n < 3; n++)
                            {
                                int row = DB.NodeLib[Elem.NList[i]].DOF[m];
                                int col = DB.NodeLib[Elem.NList[j]].DOF[n];

                                // Only Upper Left triangle is needed
                                if (col >= row)
                                {
                                    // K matrix with Esential BC included
                                    if (nDOF_reduction[row] != -1)
                                    {
                                        if (nDOF_reduction[col] != -1)
                                        {
                                            lock (K)
                                            {
                                                alglib.sparseadd(K, row - nDOF_reduction[row], col - nDOF_reduction[col],
                                                                 k.GetFast(i * 3 + m, j * 3 + n));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            });

            sw.Stop();
            Console.WriteLine("          Done in " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s");

            return K;
        }

        public void ExportBinary(Database DB, int[] nDOF_reduction, double[] F, int inc, string path, string type)
        {
            Stopwatch sw = new Stopwatch(); sw.Start();
            Console.Write("   Export to C++ Linear Solver: "); // Create output in console

            // Global Stiffness Matrix Initialization
            int nFixDOF = nDOF_reduction.Where(x => x == -1).Count();
            Dictionary<Tuple<int,int>, double> K = new Dictionary<Tuple<int, int>, double>();

            // Assembly loop

            Parallel.ForEach(DB.ElemLib.Values, Elem =>
            {
                MatrixST k = new MatrixST(3 * Elem.NList.Count, 3 * Elem.NList.Count);
                if (type == "Initial")
                {
                    k = Elem.K_Initial(inc, DB);
                }
                if (type == "Tangent")
                {
                    k = Elem.K_Tangent(inc, DB);
                }

                // Assembly to Global Stiffness Matrix
                for (int i = 0; i < Elem.NList.Count; i++)
                {
                    for (int m = 0; m < 3; m++)
                    {
                        for (int j = 0; j < Elem.NList.Count; j++)
                        {
                            for (int n = 0; n < 3; n++)
                            {
                                int row = DB.NodeLib[Elem.NList[i]].DOF[m];
                                int col = DB.NodeLib[Elem.NList[j]].DOF[n];

                                // Only Upper Left triangle is needed
                                if (col >= row)
                                {
                                    // K matrix with Esential BC included
                                    if (nDOF_reduction[row] != -1)
                                    {
                                        if (nDOF_reduction[col] != -1)
                                        {
                                            lock (K)
                                            {
                                                Tuple<int, int> key = new Tuple<int, int>(row - nDOF_reduction[row], col - nDOF_reduction[col]);
                                                if (K.ContainsKey(key))
                                                    K[key] += k.GetFast(i * 3 + m, j * 3 + n);
                                                else K.Add(key, k.GetFast(i * 3 + m, j * 3 + n));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            });
            
            // Write binary K matrix
            using (BinaryWriter b = new BinaryWriter(File.Open(path + @"\K.bin", FileMode.Create)))
            {
                foreach (KeyValuePair<Tuple<int, int>, double> i in K)
                {
                    b.Write(i.Key.Item1); b.Write(i.Key.Item2); b.Write(i.Value);
                }
            }
            // Write binary F vector
            using (BinaryWriter b = new BinaryWriter(File.Open(path + @"\F.bin", FileMode.Create)))
            {
                foreach (double i in F)
                {
                    b.Write(i);
                }
            }
            //using (StreamWriter b = new StreamWriter(File.Open(path + @"\STAN_KMatrix.txt", FileMode.Create)))
            //{
            //    foreach (KeyValuePair<Tuple<int, int>, double> i in K)
            //    {
            //        b.Write(i.Key.Item1); b.Write(" "); b.Write(i.Key.Item2); b.Write(" "); b.Write(i.Value); b.Write("\n");
            //    }
            //}
            sw.Stop();
            Console.WriteLine("          Done in " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s");
        }

        // Linear system of equation solvers -----------------------------

        public double[] LinearSolver_CG(alglib.sparsematrix K, double[] F, Analysis AnalysisLib)
        {
            Stopwatch sw = new Stopwatch(); sw.Start();
            Console.Write("   Solving linear system...   ");

            alglib.sparseconverttocrs(K);
            alglib.lincgcreate(alglib.sparsegetnrows(K), out alglib.lincgstate s);

            //          From ALGLIB manual - lincgsetcond function
            //  
            //  *************************************************************************
            //  This function sets stopping criteria.
            //  
            //  INPUT PARAMETERS:
            //      EpsF    -   algorithm will be stopped if norm of residual is less than
            //                  EpsF*||b||.
            //      MaxIts  -   algorithm will be stopped if number of iterations is  more
            //                  than MaxIts.
            //  
            //  OUTPUT PARAMETERS:
            //      State   -   structure which stores algorithm state
            //  
            //  NOTES:
            //  If  both  EpsF  and  MaxIts  are  zero then small EpsF will be set to small
            //  value.
            //  
            //    -- ALGLIB --
            //       Copyright 14.11.2011 by Bochkanov Sergey
            //  *************************************************************************

            double tolerance = AnalysisLib.GetLinSolverTolerance();
            int IterMax = AnalysisLib.GetLinSolverMaxIter();
            alglib.lincgsetcond(s, tolerance, IterMax);

            alglib.lincgsolvesparse(s, K, true, F);
            alglib.lincgresults(s, out double[] U, out alglib.lincgreport report);

            
            // OUTPUT PARAMETERS:
            // X       -   array[N], solution
            // Rep     -   optimization report:
            //             * Rep.TerminationType completetion code:
            //                 * -5    input matrix is either not positive definite,
            //                         too large or too small
            //                 * -4    overflow/underflow during solution
            //                         (ill conditioned problem)
            //                 *  1    ||residual||<=EpsF*||b||
            //                 *  5    MaxIts steps was taken
            //                 *  7    rounding errors prevent further progress,
            //                         best point found is returned
            //             * Rep.IterationsCount contains iterations count
            //             * NMV countains number of matrix-vector calculations

            if (report.terminationtype == 1 || report.terminationtype == 7) Console.Write("  NORMAL ");
            else Console.Write("  ERROR ");
            Console.Write(" (type " + report.terminationtype + ")");
            sw.Stop();
            Console.WriteLine(" in " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s");

            return U;
        }

        public double[] LinearSolver_Cholesky(alglib.sparsematrix K, double[] F)
        {
            Stopwatch sw = new Stopwatch(); sw.Start();

            //          From ALGLIB manual - sparsecholeskyskyline function
            //
            //  /*************************************************************************
            //  Sparse Cholesky decomposition for skyline matrixm using in-place algorithm
            //  without allocating additional storage.
            //  
            //  The algorithm computes Cholesky decomposition  of  a  symmetric  positive-
            //  definite sparse matrix. The result of an algorithm is a representation  of
            //  A as A=U^T*U or A=L*L^T
            //  
            //  This  function  is  a  more  efficient alternative to general, but  slower
            //  SparseCholeskyX(), because it does not  create  temporary  copies  of  the
            //  target. It performs factorization in-place, which gives  best  performance
            //  on low-profile matrices. Its drawback, however, is that it can not perform
            //  profile-reducing permutation of input matrix.
            //  
            //  INPUT PARAMETERS:
            //      A       -   sparse matrix in skyline storage (SKS) format.
            //      N       -   size of matrix A (can be smaller than actual size of A)
            //      IsUpper -   if IsUpper=True, then factorization is performed on  upper
            //                  triangle. Another triangle is ignored (it may contant some
            //                  data, but it is not changed).
            //  
            //  
            //  OUTPUT PARAMETERS:
            //      A       -   the result of factorization, stored in SKS. If IsUpper=True,
            //                  then the upper  triangle  contains  matrix  U,  such  that
            //                  A = U^T*U. Lower triangle is not changed.
            //                  Similarly, if IsUpper = False. In this case L is returned,
            //                  and we have A = L*(L^T).
            //                  Note that THIS function does not  perform  permutation  of
            //                  rows to reduce bandwidth.
            //  
            //  RESULT:
            //      If  the  matrix  is  positive-definite,  the  function  returns  True.
            //      Otherwise, the function returns False. Contents of A is not determined
            //      in such case.
            //  
            //  NOTE: for  performance  reasons  this  function  does NOT check that input
            //        matrix  includes  only  finite  values. It is your responsibility to
            //        make sure that there are no infinite or NAN values in the matrix.
            //  
            //    -- ALGLIB routine --
            //       16.01.2014
            //       Bochkanov Sergey
            //  *************************************************************************/

            Console.WriteLine("   Linear system K*U=F:");
            alglib.sparseconverttosks(K);

            Console.Write("    - Cholesky decomposition:");
            bool Result = alglib.sparsecholeskyskyline(K, alglib.sparsegetnrows(K), true);
            if (Result == true)
            {
                Console.WriteLine("   Done");
            }
            else
            {
                Console.WriteLine("   ERROR");
            }


            //          From ALGLIB manual - sparsecholeskysolvesks function
            //
            //   /*************************************************************************
            //   Sparse linear solver for A*x=b with N*N real  symmetric  positive definite
            //   matrix A given by its Cholesky decomposition, and N*1 vectors x and b.
            //   
            //   IMPORTANT: this solver requires input matrix to be in  the  SKS  (Skyline)
            //              sparse storage format. An exception will be  generated  if  you
            //              pass matrix in some other format (HASH or CRS).
            //   
            //   INPUT PARAMETERS
            //       A       -   sparse NxN matrix stored in SKS format, must be NxN exactly
            //       N       -   size of A, N>0
            //       IsUpper -   which half of A is provided (another half is ignored)
            //       B       -   array[N], right part
            //   
            //   OUTPUT PARAMETERS
            //       Rep     -   solver report, following fields are set:
            //                   * rep.terminationtype - solver status; >0 for success,
            //                     set to -3 on failure (degenerate or non-SPD system).
            //       X       -   array[N], it contains:
            //                   * rep.terminationtype>0    =>  solution
            //                   * rep.terminationtype=-3   =>  filled by zeros
            //   
            //     -- ALGLIB --
            //        Copyright 26.12.2017 by Bochkanov Sergey
            //   *************************************************************************/

            Console.Write("    - Solving:");
            alglib.sparsecholeskysolvesks(K, alglib.sparsegetnrows(K), true, F, out alglib.sparsesolverreport report, out double[] U);

            // Termination type (>0 - solution; -3 - error (filled by zeros))
            if (report.terminationtype > 0)
            {
                Console.Write("                  NORMAL termination");
            }
            else
            {
                Console.Write("                  ERROR termination");
            }
            Console.WriteLine(" (type " + report.terminationtype + ")");
            
            sw.Stop();
            Console.WriteLine("    Total time to solve K*U=F:  " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s");

            return U;
        }

        public double[] LinearSolver_LU(alglib.sparsematrix K, double[] F)
        {
            Stopwatch sw = new Stopwatch(); sw.Start();
            Console.Write("   Solving linear system...   ");
        
            //  Sparse LU decomposition with column pivoting for sparsity and row pivoting
            //  for stability. Input must be square sparse matrix stored in CRS format.
            //  
            //  The algorithm  computes  LU  decomposition  of  a  general  square  matrix
            //  (rectangular ones are not supported). The result  of  an  algorithm  is  a
            //  representation of A as A = P*L*U*Q, where:
            //  * L is lower unitriangular matrix
            //  * U is upper triangular matrix
            //  * P = P0*P1*...*PK, K=N-1, Pi - permutation matrix for I and P[I]
            //  * Q = QK*...*Q1*Q0, K=N-1, Qi - permutation matrix for I and Q[I]
            //  
            //  This function pivots columns for higher sparsity, and then pivots rows for
            //  stability (larger element at the diagonal).
            //  
            //  INPUT PARAMETERS:
            //      A       -   sparse NxN matrix in CRS format. An exception is generated
            //                  if matrix is non-CRS or non-square.
            //      PivotType-  pivoting strategy:
            //                  * 0 for best pivoting available (2 in current version)
            //                  * 1 for row-only pivoting (NOT RECOMMENDED)
            //                  * 2 for complete pivoting which produces most sparse outputs
            //  
            //  OUTPUT PARAMETERS:
            //      A       -   the result of factorization, matrices L and U stored in
            //                  compact form using CRS sparse storage format:
            //                  * lower unitriangular L is stored strictly under main diagonal
            //                  * upper triangilar U is stored ON and ABOVE main diagonal
            //      P       -   row permutation matrix in compact form, array[N]
            //      Q       -   col permutation matrix in compact form, array[N]
            //  
            //  This function always succeeds, i.e. it ALWAYS returns valid factorization,
            //  but for your convenience it also returns  boolean  value  which  helps  to
            //  detect symbolically degenerate matrices:
            //  * function returns TRUE, if the matrix was factorized AND symbolically
            //    non-degenerate
            //  * function returns FALSE, if the matrix was factorized but U has strictly
            //    zero elements at the diagonal (the factorization is returned anyway).
        
            alglib.sparseconverttocrs(K);
            alglib.sparselu(K, 0, out int[] P, out int[] Q);
        
            // Sparse linear solver for A*x=b with general (nonsymmetric) N*N sparse real
            // matrix A given by its LU factorization, N * 1 vectors x and b.
            //
            //   IMPORTANT: this solver requires input matrix  to  be  in  the  CRS  sparse
            //   storage format. An exception will  be  generated  if  you  pass
            //   matrix in some other format (HASH or SKS).
            //
            // INPUT PARAMETERS
            //    A       -   LU factorization of the sparse matrix, must be NxN exactly
            //                in CRS storage format
            //    P, Q    -   pivot indexes from LU factorization
            //    N       -   size of A, N>0
            //    B       -   array[0..N-1], right part
        
            alglib.sparselusolve(K, P, Q, alglib.sparsegetnrows(K), F, out double[] U, out alglib.sparsesolverreport report);
        
            // Termination type (>0 - solution; -3 - error (filled by zeros))
            if (report.terminationtype > 0) Console.Write("NORMAL TERMINATION");
            else Console.Write("ERROR TERMINATION");
            Console.Write(" (type " + report.terminationtype + ")");
            sw.Stop();
            Console.WriteLine(" in " + sw.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s");
        
            return U;
        }

        // Cutting rows/columns of matrix -------------------------------------------

        public double[] Include_BC_DOF (double[] A, int[] nDOF_reduction)
        {
            double[] A_Full = new double[nDOF_reduction.Length];

            for (int i = 0; i < A_Full.Length; i++)
            {
                if (nDOF_reduction[i] == -1)
                {
                    // Include 0.00 displacement - esential BC
                    A_Full[i] = 0;
                }
                else
                {
                    A_Full[i] = A[i - nDOF_reduction[i]];
                }
            }

            return A_Full;
        }

        public double[] Exclude_BC_DOF (double[] A, int[] nDOF_reduction)
        {
            int nDOF = nDOF_reduction.Length;
            int nFixDof = nDOF_reduction.Where(x => x == -1).Count();
            double[] A_Red = new double[nDOF - nFixDof];

            for (int i = 0; i < nDOF; i++)
            {
                if (nDOF_reduction[i] != -1)
                {
                    A_Red[i - nDOF_reduction[i]] = A[i];
                }
            }

            return A_Red;
        }

        // Others --------------------------------------

        public double Vector_Norm(double[] v)
        {
            double norm = 0;
            foreach (double i in v)
            {
                norm += Math.Pow(i, 2);
            }
            norm = Math.Sqrt(norm);

            return norm;
        }


    }
}
