using ProtoBuf;

namespace STAN_Database
{
    /// <summary>
    /// 2 dimensional matrix class.
    /// Used to replace List(double[]) or double[,]
    /// <br>Google Protocol Buffers used to serialized objects cannot contain neasted lists and arrays</br><br></br>
    /// <br>Solution: <br></br>List(object) => object = double[]</br><br> instead of</br><br> List(double[])</br>
    /// <list>
    /// <item><c>M</c> is List of VectorST classes used instead of List(double[])</item>
    /// </list>
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class MatrixST
    {
        [ProtoMember(1)] private double[] M;
        [ProtoMember(2)] private int Rows;
        [ProtoMember(3)] private int Cols;

        public MatrixST(int rows, int cols)
        {
            M = new double[rows * cols];
            Rows = rows;
            Cols = cols;
        }

        /// <summary>
        /// Get element of this matrix
        /// <br>CHECKING OF INDICES - slower than GetFast() </br>
        /// </summary>
        /// <returns>[row, col] element of this matrix
        /// <br>Returns 0 if element not exists in this matrix</br></returns>
        public double Get(int row, int col)
        {
            if (row >= 0 && row < Rows && col >= 0 && col < Cols)
            {
                return M[row * Cols + col];
            }
            else
            {
                throw new System.ArgumentException("Get error: Matrix doesn't have specified index", "Index");
            }
        }

        /// <summary>
        /// Set [row, col] element of this matrix to "val"
        /// <br>CHECKING OF INDICES - slower than SetFast() </br>
        /// </summary>
        public void Set(int row, int col, double val)
        {
            if (row >= 0 && row < Rows && col >= 0 && col < Cols)
            {
                M[row * Cols + col] = val;
            }
            else
            {
                throw new System.ArgumentException("Set error: Matrix doesn't have specified index", "Index");
            }
        }

        /// <summary>
        /// Add value to [row, col] element of this matrix
        /// <br>CHECKING OF INDICES - slower than AddFast() </br>
        /// </summary>
        public void Add(int row, int col, double val)
        {
            if (row >= 0 && row < Rows && col >= 0 && col < Cols)
            {
                M[row * Cols + col] += val;
            }
            else
            {
                throw new System.ArgumentException("Add error: Matrix doesn't have specified index", "Index");
            }
        }

        /// <summary>
        /// Get element of this matrix
        /// <br>NO CHECKING OF INDICES to improve performance - use carefully </br>
        /// </summary>
        /// <returns>[row, col] element of this matrix</returns>
        public double GetFast(int row, int col)
        {
            return M[row * Cols + col];
        }

        /// <summary>
        /// Set [row, col] element of this matrix to "val"
        /// <br>NO CHECKING OF INDICES to improve performance - use carefully </br>
        /// </summary>
        public void SetFast(int row, int col, double val)
        {
            M[row * Cols + col] = val;
        }

        /// <summary>
        /// Add value to [row, col] element of this matrix
        /// <br>NO CHECKING OF INDICES to improve performance - use carefully </br>
        /// </summary>
        public void AddFast(int row, int col, double val)
        {
            M[row * Cols + col] += val;
        }

        /// <summary>
        /// Get row of this matrix
        /// </summary>
        /// <returns> Row of this matrix as double array</returns>
        public double[] GetRow(int index)
        {
            if (index >= 0 && index < Rows)
            {
                double[] row = new double[Cols];
                for (int i = 0; i < Cols; i++)
                {
                    row[i] = GetFast(index, i);
                }
                return row;
            }
            else
            {
                throw new System.ArgumentException("GetRow error: Matrix doesn't have specified row", "Index");
            }
        }

        /// <summary>
        /// Get row of this matrix
        /// </summary>
        /// <returns> Row of this matrix as MatrixST with size 1 x ColumnNumber </returns>
        public MatrixST GetRow_MatrixST(int index)
        {
            if (index >= 0 && index < Rows)
            {
                MatrixST row = new MatrixST(1, Cols);
                for (int i = 0; i < Cols; i++)
                {
                    row.SetFast(0, i, GetFast(index, i));
                }
                return row;
            }
            else
            {
                throw new System.ArgumentException("GetRow error: Matrix doesn't have specified row", "Index");
            }
        }

        /// <summary>
        /// Add row values to this matrix row
        /// </summary>
        public void AddRow(int rowIndex, double[] value)
        {
            if (rowIndex >= 0 && rowIndex < Rows && value.Length == Cols)
            {
                for (int i = 0; i < Cols; i++)
                {
                    AddFast(rowIndex, i, value[i]);
                }
            }
            else
            {
                throw new System.ArgumentException("AddRow error: Matrix doesn't have specified row or matrix and vector sizes don't match", "Index");
            }
        }

        /// <summary>
        /// Set row values to this matrix row
        /// </summary>
        public void SetRow(int rowIndex, double[] value)
        {
            if (rowIndex >= 0 && rowIndex < Rows && value.Length == Cols)
            {
                for (int i = 0; i < Cols; i++)
                {
                    SetFast(rowIndex, i, value[i]);
                }
            }
            else
            {
                throw new System.ArgumentException("SetRow error: Matrix doesn't have specified row or matrix and vector sizes don't match", "Index");
            }
        }

        /// <summary>
        /// Get column of this matrix
        /// </summary>
        /// <returns> Column of this matrix as double array</returns>
        public double[] GetColumn(int index)
        {
            if (index >= 0 && index < Cols)
            {
                double[] column = new double[Rows];
                for (int i = 0; i < Rows; i++)
                {
                    column[i] = GetFast(i, index);
                }

                return column;
            }
            else
            {
                throw new System.ArgumentException("GetColumn error: Matrix doesn't have specified row", "Index");
            }
        }

        /// <returns>
        /// Number of rows of this matrix
        /// </returns>
        public int GetNRows()
        {
            return Rows;
        }

        /// <returns>
        /// Number of columns of this matrix
        /// </returns>
        public int GetNCols()
        {
            return Cols;
        }

        /// <returns>
        /// Minimal value in this matrix
        /// </returns>
        public double Min()
        {
            double min = M[0];
            for (int i=1; i<M.Length; i++)
            {
                if (M[i] < min) min = M[i];
            }
            return min;
        }

        /// <returns>
        /// Maximal value in this matrix
        /// </returns>
        public double Max()
        {
            double max = M[0];
            for (int i = 1; i < M.Length; i++)
            {
                if (M[i] < max) max = M[i];
            }
            return max;
        }

        /// <returns>
        /// Transposition of this matrix 
        /// </returns>
        public MatrixST Transpose()
        {
            MatrixST C = new MatrixST(Cols, Rows);

            for (int r=0; r<Rows; r++)
            {
                for (int c=0; c<Cols; c++)
                {
                    C.SetFast(c, r, GetFast(r,c));
                }
            }
            return C;
        }

        ///<summary>
        /// Fast calculation of 3x3 matrix determinant.
        /// <br>Designed for Jacobian matrix</br>
        ///</summary>
        /// <returns> Determinant of this matrix if size is 3x3. Otherwise returns 0. </returns>
        public double Det3()
        {
            if (Rows == 3 && Cols == 3)
            {
                double det = GetFast(0, 0) * GetFast(1, 1) * GetFast(2, 2) +
                             GetFast(1, 0) * GetFast(2, 1) * GetFast(0, 2) +
                             GetFast(2, 0) * GetFast(0, 1) * GetFast(1, 2) -
                             GetFast(0, 2) * GetFast(1, 1) * GetFast(2, 0) -
                             GetFast(0, 0) * GetFast(1, 2) * GetFast(2, 1) -
                             GetFast(2, 2) * GetFast(0, 1) * GetFast(1, 0);

                return det;
            }
            else
            {
                throw new System.ArgumentException("Det3 error: Matrix size in not 3x3", "Size");
            }
        }

        /// <summary>
        /// Inverse of 3x3 matrix. Designed for Jacobian matrix.
        /// </summary>
        /// <returns>Inverse of this matrix if size is 3x3 and determinant is not 0.
        /// <br>Otherwise returns empty 3x3 matrix</br></returns>
        public MatrixST Inverse()
        {
            double det = Det3();

            if (det != 0 && Rows == 3 && Cols == 3)
            {
                MatrixST Inv = new MatrixST(3, 3);
                double X = 1.0 / det;

                Inv.SetFast(0, 0, X * (GetFast(1, 1) * GetFast(2, 2) - GetFast(1, 2) * GetFast(2, 1)));
                Inv.SetFast(0, 1, X * (GetFast(0, 2) * GetFast(2, 1) - GetFast(0, 1) * GetFast(2, 2)));
                Inv.SetFast(0, 2, X * (GetFast(0, 1) * GetFast(1, 2) - GetFast(0, 2) * GetFast(1, 1)));
                Inv.SetFast(1, 0, X * (GetFast(1, 2) * GetFast(2, 0) - GetFast(1, 0) * GetFast(2, 2)));
                Inv.SetFast(1, 1, X * (GetFast(0, 0) * GetFast(2, 2) - GetFast(0, 2) * GetFast(2, 0)));
                Inv.SetFast(1, 2, X * (GetFast(0, 2) * GetFast(1, 0) - GetFast(0, 0) * GetFast(1, 2)));
                Inv.SetFast(2, 0, X * (GetFast(1, 0) * GetFast(2, 1) - GetFast(1, 1) * GetFast(2, 0)));
                Inv.SetFast(2, 1, X * (GetFast(0, 1) * GetFast(2, 0) - GetFast(0, 0) * GetFast(2, 1)));
                Inv.SetFast(2, 2, X * (GetFast(0, 0) * GetFast(1, 1) - GetFast(0, 1) * GetFast(1, 0)));

                return Inv;
            }
            else
            {
                throw new System.ArgumentException("Inverse matrix error: Matrix size in not 3x3 or det=0", "Inverse");
            }
        }

        /// <summary>
        /// Multiply all element of this matrix by scalar s
        /// </summary>
        /// <returns>
        /// Matrix-Scalar product
        /// </returns>
        public MatrixST MultiplyScalar(double s)
        {
            MatrixST C = new MatrixST(Rows, Cols);

            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    C.SetFast(i, j, GetFast(i, j) * s);
                }
            }
            return C;
        }

        /// <summary>
        /// Matrix-Vector multiplication
        /// </summary>
        /// <returns>Vector as double array.
        /// <br>Returns zero vector if size of this matrix and V doesn't match.</br>
        /// </returns>
        public double[] MultiplyVector(double[] V)
        {
            double[] C = new double[Rows];

            if (Cols == V.Length)
            {
                for (int i = 0; i < Rows; i++)
                {
                    for (int j = 0; j < Cols; j++)
                    {
                        C[i] += GetFast(i, j) * V[j];
                    }
                }

                return C;
            }
            else
            {
                throw new System.ArgumentException("Matrix-Vector product error: Matrix and Vector size not fit!", "Size");
            }
        }

        public MatrixST Vector2Tensor(double[] v)
        {
            MatrixST T = new MatrixST(3, 3);
            T.SetFast(0, 0, v[0]);
            T.SetFast(0, 1, v[3]);
            T.SetFast(0, 2, v[5]);
            T.SetFast(1, 0, v[3]);
            T.SetFast(1, 1, v[1]);
            T.SetFast(1, 2, v[4]);
            T.SetFast(2, 0, v[5]);
            T.SetFast(2, 1, v[4]);
            T.SetFast(2, 2, v[2]);

            return T;
        }

        public double[] Tensor2Vector(MatrixST T)
        {
            double[] v = new double[6];
            v[0] = T.GetFast(0, 0);
            v[1] = T.GetFast(1, 1);
            v[2] = T.GetFast(2, 2);
            v[3] = T.GetFast(0, 1);
            v[4] = T.GetFast(1, 2);
            v[5] = T.GetFast(0, 2);

            return v;
        }

        // ----- OPERATORS -----------------------------------------------------------------

        /// <summary>
        /// Matrix multiplication operator
        /// <para>Returns zero matrix if sizes of matrices don't match.</para>
        /// </summary>
        public static MatrixST operator *(MatrixST A, MatrixST B)
        {
            MatrixST C = new MatrixST(A.Rows, B.Cols);

            if (A.Cols == B.Rows)
            {
                for (int i = 0; i < A.Rows; i++)
                {
                    for (int j = 0; j < B.Cols; j++)
                    {
                        for (int k = 0; k < A.Cols; k++)
                        {
                            C.AddFast(i, j, A.GetFast(i, k) * B.GetFast(k, j));
                        }
                    }
                }

                return C;
            }
            else
            {
                throw new System.ArgumentException("Multiplication error: A cols not equal to B rows", "Size");
            }
        }

        /// <summary>
        /// Addition operator ( A + B )
        /// </summary>
        /// <returns>Sum of matrices A and B. First matrix provides the size.
        /// <br>If size B is bigger than size A: only common indices added</br>
        /// <br>Size of B cannot be smaller than size of A !!!</br></returns>
        public static MatrixST operator +(MatrixST A, MatrixST B)
        {
            MatrixST C = new MatrixST(A.Rows, A.Cols);

            if (B.Rows >= A.Rows && B.Cols >= A.Cols)
            {
                for (int i = 0; i < A.Rows; i++)
                {
                    for (int j = 0; j < A.Cols; j++)
                    {
                        C.AddFast(i, j, A.GetFast(i, j) + B.GetFast(i, j));
                    }
                }
                return C;
            }
            else
            {
                throw new System.ArgumentException("Addition error: B smaller than A", "Size");
            }
        }

        /// <summary>
        /// Subtraction operator ( A - B )
        /// </summary>
        /// <returns>Difference of matrices A and B. First matrix provides the size.
        /// <br>If size B is bigger than size A: only common indices subtracted</br>
        /// <br>Size of B cannot be smaller than size of A !!!</br></returns>
        public static MatrixST operator -(MatrixST A, MatrixST B)
        {
            MatrixST C = new MatrixST(A.Rows, A.Cols);

            if (B.Rows >= A.Rows && B.Cols >= A.Cols)
            {
                for (int i = 0; i < A.Rows; i++)
                {
                    for (int j = 0; j < A.Cols; j++)
                    {
                        C.AddFast(i, j, A.GetFast(i, j) - B.GetFast(i, j));
                    }
                }
                return C;
            }
            else
            {
                throw new System.ArgumentException("Subtraction error: B smaller than A", "Size");
            }
        }

    }
}
