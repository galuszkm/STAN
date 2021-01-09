using System;
using System.Collections.Generic;

namespace STAN_Database
{
    // Development note:
    //
    //   In general only isoparametric finite elements are considered here. 
    //   It's very easy and universal approach that significantly reduce coding effort.
    //
    //   To create new Finite Element formulation the following data are required:
    //     - Number of element Gauss (integration) points
    //     - Integration weight
    //     - Partial derivatives of shape function in natural coordinates at each Gauss point
    //   
    //   Class FE_Library exists just to store Dictionary Object "FE" where keys are FE names/types
    //   and values are "ElementType" objects that store data mentioned above.
    //    
    //   Main method in "ElementType" class assign all data - this is where you can define your own FE formulation. 
    //   First of all define some name. Best practise is to use self explaining term - 
    //   e.g HEX8_G1 stands for 8-node hexahedron with Gauss integration order 1.
    //   Add new if operation with (type == ...) condition to catch your new FE.
    //
    //   Next step is to define number of Gauss points (GaussPointNumb) and weight 
    //   at integration point (GaussWeight). Integration rule higher than 2 is not really efficient for linear elements
    //   But keep in mind locking effects for full integration and hourglassing for reduced integration.
    //
    //   Finally shape functions must to be defined to calculate their partial derivatives at Gauss points.
    //   Here you need introduce your own method, see "HEX8_Diff_ShapeFunctions" as an example.
    //   
    //   At last add new ElementType object to FE dictionary in FE_Library class
    //

    public class FE_Library
    {
        public Dictionary<string, ElementType> FE { get; }

        public FE_Library()
        {
            // Initialize FE dicionary
            FE = new Dictionary<string, ElementType>
            {
                // Add all FE types to Dictionary
                { "HEX8_G1", new ElementType("HEX8_G1") },
                { "HEX8_G2", new ElementType("HEX8_G2") },
                { "TET4_G1", new ElementType("TET4_G1") },
                { "TET4_G2", new ElementType("TET4_G2") },
                { "PENTA6_G1", new ElementType("PENTA6_G1") },  // Not implemented yet
                { "PENTA6_G2", new ElementType("PENTA6_G2") }   // Not implemeneted yet
            };
        }
    }

    public class ElementType
    {
        public int GaussPointNumb { get; }
        public double GaussWeight { get; }
        public List<double[]> N { get; set; }
        public List<MatrixST> dN_dLocal { get; }

        public ElementType(string type)
        {
            if (type == "HEX8_G1")
            {
                // ================================================================================
                // HEX8_G1 type:
                //      8-node hexahedral finite element
                //      Linear shape functions
                //      Reduced integration (1 Gauss point)

                GaussPointNumb = 1;
                GaussWeight = 2.0 * 2.0 * 2.0;

                // Gauss Point location 
                double GaussLocation = 0;

                N = new List<double[]>
                { 
                    // Shape function partial derivatives at Gauss point
                    new double[8] { 1, 1, 1, 1, 1, 1, 1, 1 }
                };

                dN_dLocal = new List<MatrixST>
                { 
                    // Shape function partial derivatives at Gauss point
                    HEX8_Diff_ShapeFunctions(new double[3] { GaussLocation, GaussLocation, GaussLocation })
                };

            }

            if (type == "HEX8_G2")
            {
                // ================================================================================
                // HEX8_G2 type:
                //      8-node hexahedral finite element
                //      Linear shape functions
                //      Full integration (8 Gauss point)

                GaussPointNumb = 2 * 2 * 2;
                GaussWeight = 1.0;

                // Gauss Point location 
                double GaussLocation = Math.Sqrt(1.0 / 3.0);

                N = new List<double[]>
                {
                    // Shape function extrapolated from Gauss point
                    HEX8_ShapeFunctions(new double[3] { -1, -1, -1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { +1, -1, -1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { +1, +1, -1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { -1, +1, -1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { -1, -1, +1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { +1, -1, +1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { +1, +1, +1 }, GaussLocation),
                    HEX8_ShapeFunctions(new double[3] { -1, +1, +1 }, GaussLocation)
                };

                dN_dLocal = new List<MatrixST>
                {
                    // Shape function partial derivatives at Gauss point
                    HEX8_Diff_ShapeFunctions(new double[3] { -GaussLocation, -GaussLocation, -GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { +GaussLocation, -GaussLocation, -GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { +GaussLocation, +GaussLocation, -GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { -GaussLocation, +GaussLocation, -GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { -GaussLocation, -GaussLocation, +GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { +GaussLocation, -GaussLocation, +GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { +GaussLocation, +GaussLocation, +GaussLocation }),
                    HEX8_Diff_ShapeFunctions(new double[3] { -GaussLocation, +GaussLocation, +GaussLocation })
                };

            }

            if (type == "TET4_G1")
            {
                // ================================================================================
                // TET4_G1 type:
                //      4-node tetrahedral finite element
                //      Linear shape functions
                //      Reduced integration (1 Gauss point)

                GaussPointNumb = 1;
                GaussWeight = 1.0;

                // Gauss Point location 
                double GaussLocation = 0.25;

                N = new List<double[]>
                { 
                    // Shape function partial derivatives at Gauss point
                    new double[4] { 1, 1, 1, 1 }
                };

                dN_dLocal = new List<MatrixST>
                { 
                    // Shape function partial derivatives at Gauss point
                    TET4_Diff_ShapeFunctions()
                };

            }

            if (type == "TET4_G2")
            {
                // ================================================================================
                // TET4_G2 type:
                //      4-node tetrahedral finite element
                //      Linear shape functions
                //      Full integration (4 Gauss points)

                GaussPointNumb = 4;
                GaussWeight = 0.25;

                // Gauss Point location 
                double[] GaussLocation1 = new double[3] { 0.138196601125010, 0.138196601125010, 0.138196601125010 };
                double[] GaussLocation2 = new double[3] { 0.138196601125010, 0.138196601125010, 0.585410196624968 };
                double[] GaussLocation3 = new double[3] { 0.585410196624968, 0.138196601125010, 0.138196601125010 };
                double[] GaussLocation4 = new double[3] { 0.138196601125010, 0.585410196624968, 0.138196601125010 };

                N = new List<double[]>
                {
                    // Shape function extrapolated from Gauss point
                    TET4_ShapeFunctions(new double[3] { 0, 0, 0 }, GaussLocation1),
                    TET4_ShapeFunctions(new double[3] { 0, 0, 1 }, GaussLocation2),
                    TET4_ShapeFunctions(new double[3] { 1, 0, 0 }, GaussLocation3),
                    TET4_ShapeFunctions(new double[3] { 0, 1, 0 }, GaussLocation4)
                };

                dN_dLocal = new List<MatrixST>
                { 
                    // Shape function partial derivatives at Gauss point
                    TET4_Diff_ShapeFunctions(),
                    TET4_Diff_ShapeFunctions(),
                    TET4_Diff_ShapeFunctions(),
                    TET4_Diff_ShapeFunctions()
                };

            }
        }

        /// <summary>
        /// Finite Element type:
        /// <br>Hexahedral, 8 node, Isoparametric</br>
        /// <br><c>Gauss_Point</c> - Integration point natural coordinates {xi, eta, zeta}</br>
        /// </summary>
        /// <returns>Matrix with partial derivatives (natural) of shape functions in Gauss point
        /// <br></br></returns>
        private MatrixST HEX8_Diff_ShapeFunctions(double[] Gauss_Point)
        {

            //   Input:  Gauss_Point = { xi, eta, zeta}

            //                        | dN1/d_xi    dN2/d_xi        dN8/d_xi   |
            //   Output:  dN_dLocal = | dN1/d_eta   dN2/d_eta  ...  dN8/d_eta  |
            //                        | dN1/d_zeta  dN2/d_zeta      dN8/d_zeta |

            // Shape function of HEX8 element:
            //      N1 = 1 / 8 * (1 - xi) * (1 - eta) * (1 - zeta)
            //      N2 = 1 / 8 * (1 + xi) * (1 - eta) * (1 - zeta)
            //      N3 = 1 / 8 * (1 + xi) * (1 + eta) * (1 - zeta)
            //      N4 = 1 / 8 * (1 - xi) * (1 + eta) * (1 - zeta)
            //      N5 = 1 / 8 * (1 - xi) * (1 - eta) * (1 + zeta)
            //      N6 = 1 / 8 * (1 + xi) * (1 - eta) * (1 + zeta)
            //      N7 = 1 / 8 * (1 + xi) * (1 + eta) * (1 + zeta)
            //      N8 = 1 / 8 * (1 - xi) * (1 + eta) * (1 + zeta)

            //  Table with signs:
            //      
            //           1   xi   eta  zeta  xi*eta  eta*zeta  xi*zeta  xi*eta*zeta
            //      N1   +    -    -    -      +        +         +         -
            //      N2   +    +    -    -      -        +         -         +
            //      N3   +    +    +    -      +        -         -         -
            //      N4   +    -    +    -      -        -         +         +
            //      N5   +    -    -    +      +        -         -         +
            //      N6   +    +    -    +      -        -         +         -
            //      N7   +    +    +    +      +        +         +         +
            //      N8   +    -    +    +      -        +         -         -

            // Gauss point coordinates
            double xi = Gauss_Point[0];
            double eta = Gauss_Point[1];
            double zeta = Gauss_Point[2];

            // Initialize Matrix with shape function derivatives in local coordinate system
            MatrixST dN_dLocal = new MatrixST(3, 8);

            // dN/d_xi
            dN_dLocal.SetFast(0, 0, 1.0 / 8.0 * (-1 + eta + zeta - eta * zeta));
            dN_dLocal.SetFast(0, 1, 1.0 / 8.0 * ( 1 - eta - zeta + eta * zeta));
            dN_dLocal.SetFast(0, 2, 1.0 / 8.0 * ( 1 + eta - zeta - eta * zeta));
            dN_dLocal.SetFast(0, 3, 1.0 / 8.0 * (-1 - eta + zeta + eta * zeta));
            dN_dLocal.SetFast(0, 4, 1.0 / 8.0 * (-1 + eta - zeta + eta * zeta));
            dN_dLocal.SetFast(0, 5, 1.0 / 8.0 * ( 1 - eta + zeta - eta * zeta));
            dN_dLocal.SetFast(0, 6, 1.0 / 8.0 * ( 1 + eta + zeta + eta * zeta));
            dN_dLocal.SetFast(0, 7, 1.0 / 8.0 * (-1 - eta - zeta - eta * zeta));

            // dN/d_eta    
            dN_dLocal.SetFast(1, 0, 1.0 / 8.0 * (-1 + xi + zeta - xi * zeta));
            dN_dLocal.SetFast(1, 1, 1.0 / 8.0 * (-1 - xi + zeta + xi * zeta));
            dN_dLocal.SetFast(1, 2, 1.0 / 8.0 * ( 1 + xi - zeta - xi * zeta));
            dN_dLocal.SetFast(1, 3, 1.0 / 8.0 * ( 1 - xi - zeta + xi * zeta));
            dN_dLocal.SetFast(1, 4, 1.0 / 8.0 * (-1 + xi - zeta + xi * zeta));
            dN_dLocal.SetFast(1, 5, 1.0 / 8.0 * (-1 - xi - zeta - xi * zeta));
            dN_dLocal.SetFast(1, 6, 1.0 / 8.0 * ( 1 + xi + zeta + xi * zeta));
            dN_dLocal.SetFast(1, 7, 1.0 / 8.0 * ( 1 - xi + zeta - xi * zeta));

            // dN/d_zeta   
            dN_dLocal.SetFast(2, 0, 1.0 / 8.0 * (-1 + xi + eta - xi * eta));
            dN_dLocal.SetFast(2, 1, 1.0 / 8.0 * (-1 - xi + eta + xi * eta));
            dN_dLocal.SetFast(2, 2, 1.0 / 8.0 * (-1 - xi - eta - xi * eta));
            dN_dLocal.SetFast(2, 3, 1.0 / 8.0 * (-1 + xi - eta + xi * eta));
            dN_dLocal.SetFast(2, 4, 1.0 / 8.0 * ( 1 - xi - eta + xi * eta));
            dN_dLocal.SetFast(2, 5, 1.0 / 8.0 * ( 1 + xi - eta - xi * eta));
            dN_dLocal.SetFast(2, 6, 1.0 / 8.0 * ( 1 + xi + eta + xi * eta));
            dN_dLocal.SetFast(2, 7, 1.0 / 8.0 * ( 1 - xi + eta - xi * eta));

            return dN_dLocal;
        }

        /// <summary>
        /// Finite Element type:
        /// <br>Hexahedral, 8 node, Isoparametric</br>
        /// <br><c>Gauss_Point</c> - Integration point natural coordinates {xi, eta, zeta}</br>
        /// </summary>
        /// <returns>Matrix with shape functions extrapolated from Gauss points (used in stress recovery)
        /// <br></br></returns>
        private double[] HEX8_ShapeFunctions(double[] Node_Coord, double GaussPointLoc)
        {
            //   Input: Node_Coord - Natural cordinates of Node (1, 1, 1), (-1, 1, -1), etc.
            //          GaussPointLoc - Gauss point location -> 1/sqrt(3) - full integration;   0 - reduced integration

            //   Output:  N = | N1, N2, N3, ...  |

            // Shape function of HEX8 element:
            //      N1 = 1 / 8 * (1 - xi) * (1 - eta) * (1 - zeta)
            //      N2 = 1 / 8 * (1 + xi) * (1 - eta) * (1 - zeta)
            //      N3 = 1 / 8 * (1 + xi) * (1 + eta) * (1 - zeta)
            //      N4 = 1 / 8 * (1 - xi) * (1 + eta) * (1 - zeta)
            //      N5 = 1 / 8 * (1 - xi) * (1 - eta) * (1 + zeta)
            //      N6 = 1 / 8 * (1 + xi) * (1 - eta) * (1 + zeta)
            //      N7 = 1 / 8 * (1 + xi) * (1 + eta) * (1 + zeta)
            //      N8 = 1 / 8 * (1 - xi) * (1 + eta) * (1 + zeta)

            // Gauss point coordinates
            double xi = Node_Coord[0] / GaussPointLoc;
            double eta = Node_Coord[1] / GaussPointLoc;
            double zeta = Node_Coord[2] / GaussPointLoc;

            // Initialize Matrix with shape function derivatives in local coordinate system
            double[] n = new double[8];

            // Shape fuction extrapolated values
            n[0] = 1.0 / 8.0 * (1 - xi) * (1 - eta) * (1 - zeta);
            n[1] = 1.0 / 8.0 * (1 + xi) * (1 - eta) * (1 - zeta);
            n[2] = 1.0 / 8.0 * (1 + xi) * (1 + eta) * (1 - zeta);
            n[3] = 1.0 / 8.0 * (1 - xi) * (1 + eta) * (1 - zeta);
            n[4] = 1.0 / 8.0 * (1 - xi) * (1 - eta) * (1 + zeta);
            n[5] = 1.0 / 8.0 * (1 + xi) * (1 - eta) * (1 + zeta);
            n[6] = 1.0 / 8.0 * (1 + xi) * (1 + eta) * (1 + zeta);
            n[7] = 1.0 / 8.0 * (1 - xi) * (1 + eta) * (1 + zeta);

            return n;
        }

        /// <summary>
        /// Finite Element type:
        /// <br>Tetrahedral, 4 node, Isoparametric</br>
        /// <br><c>Gauss_Point</c> - Integration point natural coordinates {xi, eta, zeta}</br>
        /// </summary>
        /// <returns>Matrix with partial derivatives (natural) of shape functions in Gauss point
        /// <br></br></returns>
        private MatrixST TET4_Diff_ShapeFunctions()
        {

            //   Input:  Gauss_Point = { xi, eta, zeta}

            //                        | dN1/d_xi    dN2/d_xi        dN8/d_xi   |
            //   Output:  dN_dLocal = | dN1/d_eta   dN2/d_eta  ...  dN8/d_eta  |
            //                        | dN1/d_zeta  dN2/d_zeta      dN8/d_zeta |

            // Shape function of TET4 element:
            //      N1 = 1 - xi - eta - zeta
            //      N2 = xi
            //      N3 = eta
            //      N4 = zeta

            // Initialize Matrix with shape function derivatives in local coordinate system
            MatrixST dN_dLocal = new MatrixST(3, 4);

            // dN/d_xi
            dN_dLocal.SetFast(0, 0, -1);
            dN_dLocal.SetFast(0, 1,  1);
            dN_dLocal.SetFast(0, 2,  0);
            dN_dLocal.SetFast(0, 3,  0);

            // dN/d_eta    
            dN_dLocal.SetFast(1, 0, -1);
            dN_dLocal.SetFast(1, 1,  0);
            dN_dLocal.SetFast(1, 2,  1);
            dN_dLocal.SetFast(1, 3,  0);

            // dN/d_zeta   
            dN_dLocal.SetFast(2, 0, -1);
            dN_dLocal.SetFast(2, 1,  0);
            dN_dLocal.SetFast(2, 2,  0);
            dN_dLocal.SetFast(2, 3,  1);

            return dN_dLocal;
        }

        /// <summary>
        /// Finite Element type:
        /// <br>Tetrahedral, 4 node, Isoparametric</br>
        /// <br><c>Gauss_Point</c> - Integration point natural coordinates {xi, eta, zeta}</br>
        /// </summary>
        /// <returns>Matrix with shape functions extrapolated from Gauss points (used in stress recovery)
        /// <br></br></returns>
        private double[] TET4_ShapeFunctions(double[] Node_Coord, double[] GaussPointLoc)
        {
            //   Input: Node_Coord - Natural cordinates of Node (0, 0, 0), (1, 0, 0), etc.
            //          GaussPointLoc - Gauss point location

            //   Output:  N = | N1, N2, N3, ...  |

            // Shape function of TET4 element:
            //      N1 = 1 - xi - eta - zeta
            //      N2 = xi
            //      N3 = eta
            //      N4 = zeta

            // Gauss point coordinates
            double xi = Node_Coord[0] / GaussPointLoc[0];
            double eta = Node_Coord[1] / GaussPointLoc[1];
            double zeta = Node_Coord[2] / GaussPointLoc[2];

            // Initialize Matrix with shape function derivatives in local coordinate system
            double[] n = new double[4];

            // Shape fuction extrapolated values
            n[0] = 1 - xi - eta - zeta;
            n[1] = xi;
            n[2] = eta;
            n[3] = zeta;

            return n;
        }

    }
}
