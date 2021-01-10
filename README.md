# STAN - Structural Analyser
STructural ANalyser - C#/.NET Finite Element Software\
[Download](https://github.com/galuszkm/STAN/raw/main/bin/STAN_binary.zip) binary files and [Example](https://github.com/galuszkm/STAN/raw/main/examples/Example1.zip) to test it!

This app is still under development, a lot of features are not avaliable at the moment.
Documentation and code description soon!

## Introduction
STAN is a stand-alone C#/.NET Finite Element environment with Pre/Post Processor and Solver for 3D structural analysis.
Main purpose is to provide user-friendly and object oriented code to develop custom algorithms, FE formulation, structures, etc.
Obviously, it is not as fast as comercial Fortran or C++ codes (and most likely will never be).
Although this app provides really efficient enviroment for pre/post processing and solving boundary value problems.
Therefore if you are intrested only in finite element code or only in data visualisation, you may find it very usefull.\
<br>Pre/Post Processor:</br>
  * Nastran format (.bdf) mesh import
  * Currently only 8-node hexahedral finite elements supported
  * Boundary condition: Point Load and Single Point Constraint
  * Result export to .vtu format (readable in ParaView)
  
Solver:
 * Linear static analysis
 * Linear elasic material
 * Iterative (Conjugate Gradient) and Direct (Cholesky) linear system solver

## Getting started
### Create new project
To create new project unzip binary package and run <em>STAN_PrePost.exe</em>. User interface layout consists of: Menus (1), Toolbar(2), TreeView (3), Property Box (4), Graphics Viewport (5), Part Box (6). Graphics settings such as background and label color, mesh wireframe, transparency, colorbar setup could be modified using Menus. Toolbar contains several buttons used to open STAN database file, import a mesh, export results, add material and boundary condition, etc. All model properties are collected in TreeView. By selecting item in TreeView you are able to modify it in Property Box. Part Box allows you to control Part visibility in Viewport.

<img src="https://github.com/galuszkm/STAN/blob/main/images/MainView.png">

### Mesh
First step is to import a mesh. Click <em>Import</em> button in Toolbar and select <em>.bdf</em> Nastran file.
There are many excellent both comercial as well as open source mesh generators avaliable on the market. Therefore I decided to skip this point and allow user to import mesh created by any tool. Currently only short Nastran format is supported. An example of mesh file is presented below:
```
$$  GRID Data
GRID           1             0.0    15.0     0.0
GRID           2        -7.11-15     5.0     0.0
GRID           3             0.0    -5.0     0.0
...
$$  CHEXA Elements: First Order
CHEXA          1       1     573     570     571     572    1236    1237+       
+           1238    1239
CHEXA          2       1     575     569     570     573    1240    1241+       
+           1237    1236
CHEXA          3       1     576     573     572     574    1242    1236+       
+           1239    1243
...
```
At the moment, only 8-none hexahedral elements are avaliable (4-node tetrahedron and 6-node pentahedron are under development). In STAN third number in element line defines PART ID. You can use <em>Mesh.bdf</em> file from Example 1 as a test run. Note that only one mesh can be imported in single project. 

### Boundary Conditions, Materials and Analysis
When mesh is loaded you can create boundary conditions, materials and analysis setup.
 * To add <b>Material</b> click <em>Add Mat</em> button in Toolbar. New item appears in Material TreeView, select and modify it in Property Box. Only linear elastic material type is avaliable at the moment.
 * To add <b>Boundary Condition</b> click <em>Add BC</em> button in Toolbar. There are two types of BC avaliable: Single Point Constraint (SPC) and Point Load. Currently only "Paste mode" is implemented to assign DOF. Therefore you can prepare BC setup with any text editor, copy it and use <em>Clipboard</em> button to paste. 4 column format with <b>tab</b> separators is required: Node ID, X value, Y value, Z value. For SPC value 1 means that DOF is fixed, 0 means free. Point Load values specify forces acting in each direction.
 * Select <b>Part</b> in TreeView and to assign material from the list. You also change its name, color of finite element formulation (G2 - full integration, G1 - reduced integration).
 * Select <b>Analysis</b> setup in TreeView to modify setup. Only linear static analysis is currently avaliable. Two linear system solver have been implemented: Iterative (Conjugate Gradient) and Direct (Cholesky - LLT factorization).
```
SPC (XYZ fixed):
4	1	1	1
5	1	1	1
16	1	1	1
...
Point Load:
2	0	0	50
3	0	0	50
102	0	0	50
...
```
<img src="https://github.com/galuszkm/STAN/blob/main/images/Properties.png">
 
### Solve
To solve the job click <em>Solve</em> button in Toolbar. First of all you need to save the model in STdb (STAN database) format. This file will be read by solver and overwritten with results. In general, STAN PrePost and STAN Solver use the same binary database format, serialized with Google Protocol Buffers (most efficient way for reading and writting I could find). Solver is a separated console app so you can still use PrePost while the job is running. In console you can see current status of analysis.

<p align="center">
  <img src="https://github.com/galuszkm/STAN/blob/main/images/Solver.PNG">
</p>

### Results
When solving is done, you can load the results using <em>Load results</em> button or you can create new project and <em>Open</em> overwritten STdb file. <em>Result</em> item is now avaliable in TreeView. You can choose several result type like Displacement, Stress and Strain presented as smooth contour map or element min/max/average values. For Stress and Strain postprocessing, STAN uses extrapolated values from Gauss points to Nodes.\
If you need advanced postprocessing features, you can export any result in .vtu format and read it e.g. in ParaView. Finally, if you don't need results any more and want to reduce STdb file size, click <em>Remove results</em> button and save the model.

<img src="https://github.com/galuszkm/STAN/blob/main/images/Results.png">
