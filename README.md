# STAN - Structural Analyser
STructural ANalyser - C#/.NET Finite Element Software\
[Download](https://github.com/galuszkm/STAN/raw/main/bin/STAN_binary.zip) binary files and [Example](https://github.com/galuszkm/STAN/raw/main/examples/Example1.zip) to test it!

This app is still under development, a lot of features are not avaliable at the moment.

## Introduction
STAN is a stand-alone C#/.NET Finite Element environment with Pre/Post Processor and Solver for 3D structural analysis.\
Main purpose is to provide user-friendly and object oriented code to develop custom algorithms, FE formulation, structures, etc.\
Obviously, it is not as fast as comercial Fortran or C++ codes (and most likely will never be).\
Although this app provides really efficient full enviroment for pre/post processing and solving boundary value problems.\
Therefore if you are intrested only in finite element code or only in data visualisation, you can find it very usefull.\
<br>Pre/Post Processor:</br>
  * Nastran format (.bdf) mesh import
  * Currently only 8-node hexahedral finite elements supported
  * Boundary condition: Point Load and Node fix in X, Y, Z direction
  * Result export to .vtu format (readable in ParaView)
  
Solver:
 * Linear static analysis
 * Linear elasic material
 * Iterative (Conjugate Gradient) and Direct (Cholesky) linear system solver
