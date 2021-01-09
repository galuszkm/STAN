using Kitware.VTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace STAN_PrePost
{
    public class RenderInterface
    {
        public bool ModelLoaded;
        
        // VTK rendering objects
        private RenderWindowControl RenWinControl;
        private vtkRenderWindow renderWindow;
        private vtkRenderer Viewport;
        private vtkScalarBarActor ScalarBar;
        private vtkLookupTable colorLookupTable;

        // Interactors
        private vtkRenderWindowInteractor Inter;
        private vtkInteractorStyleRubberBandPick InterStylePick;
        private vtkInteractorStyleTrackballCamera InterStyleTrack;

        // Coordinate system object
        private vtkOrientationMarkerWidget Widget;
        private vtkAxesActor Axes;
        private vtkTextProperty TextProp;

        // Clip obejcts
        private vtkPlane ClipPlane;
        private double ClipPlaneSize;
        private vtkActor ClipPlaneActor;
        private vtkTextWidget Reverse;
        private vtkTextWidget ClipX;
        private vtkTextWidget ClipY;
        private vtkTextWidget ClipZ;
        private vtkTextWidget Grid;
        private double[] BoundaryRange;

        private vtkSliderWidget SliderWidget;
        private vtkSliderRepresentation2D SliderRep;

        // Selecting method items
        public vtkAppendPolyData AppendFaces;
        public vtkPolyData Faces;
        private bool SelectionMode;
        private vtkTextWidget ModeWidget;
        private double SelectionSize;
        private vtkPoints SelectionPoints;
        private vtkPolyData SelectionPolyData;
        private vtkSphereSource SelectionSphere;
        private vtkGlyph3D SelectionGlyph;
        private vtkPolyDataMapper SelectionMapper;
        private vtkActor SelectionActor;

        public RenderInterface()
        {
            vtkObject.GlobalWarningDisplayOff(); // Turn off Warning Output Window - annoying sometimes
        }

        public void CreateViewport(Grid Window)
        {
            WindowsFormsHost VTK_Window = new WindowsFormsHost(); // Create Windows Forms Host for VTK Window
            RenWinControl = new RenderWindowControl();  // Initialize VTK Renderer Window Control

            // Clear input Window and add new host 
            Window.Children.Clear();
            Window.Children.Add(VTK_Window);
            VTK_Window.Child = RenWinControl;

            // Create Render Window
            renderWindow = RenWinControl.RenderWindow;

            // Initialize Interactor
            Inter = vtkRenderWindowInteractor.New();
            Inter.LeftButtonPressEvt += new vtkObject.vtkObjectEventHandler(SelectPointClick);
            Inter.RightButtonPressEvt += new vtkObject.vtkObjectEventHandler(UnselectPointClick);
            renderWindow.SetInteractor(Inter);
            Inter.Initialize();

            InterStyleTrack = vtkInteractorStyleTrackballCamera.New();
            //Inter.SetInteractorStyle(InterStyleTrack);
            InterStylePick = vtkInteractorStyleRubberBandPick.New();
            Inter.SetInteractorStyle(InterStylePick);

            // Initialize View
            Viewport = renderWindow.GetRenderers().GetFirstRenderer();
            Viewport.RemoveAllViewProps();
            CreateViewportBorder(Viewport, new double[3] { 128.0, 128.0, 128.0 });

            // Set default background color
            Viewport.GradientBackgroundOn();
            Viewport.SetBackground(163.0 / 255.0, 163.0 / 255.0, 163.0 / 255.0);
            Viewport.SetBackground2(45.0 / 255.0, 85.0 / 255.0, 125.0 / 255.0);

            // Other properties
            Viewport.GetActiveCamera().ParallelProjectionOn();

            // Initialize Selection objects
            AppendFaces = vtkAppendPolyData.New();
            Faces = vtkPolyData.New();
            SelectionMode = false;
            SelectionSize = 0.1;
            SelectionPoints = vtkPoints.New();
            SelectionActor = vtkActor.New();
            SelectionPolyData = vtkPolyData.New();
            SelectionPolyData.SetPoints(SelectionPoints);

            SelectionSphere = vtkSphereSource.New();
            SelectionSphere.SetPhiResolution(12);
            SelectionSphere.SetThetaResolution(12);
            SelectionSphere.SetRadius(SelectionSize);
            SelectionGlyph = vtkGlyph3D.New();
            SelectionGlyph.SetInput(SelectionPolyData);
            SelectionGlyph.SetSourceConnection(SelectionSphere.GetOutputPort());
            SelectionMapper = vtkPolyDataMapper.New();
            SelectionMapper.SetInputConnection(SelectionGlyph.GetOutputPort());

            SelectionActor.SetMapper(SelectionMapper);
            SelectionActor.GetProperty().SetColor(1, 1, 1);
            SelectionActor.VisibilityOn();
            Viewport.AddActor(SelectionActor);

            // Create new Properties and Objects
            CreateColorMap();
            CreateScalarBar();
            CreateAxes();
            CreateSlider();
            CreateClipPlane();

        }

        private void CreateViewportBorder(vtkRenderer renderer, double[] color)
        {
            ModelLoaded = false;

            // points start at upper right and proceed anti-clockwise
            vtkPoints points = vtkPoints.New();
            points.SetNumberOfPoints(4);
            points.InsertPoint(0, 1, 1, 0);
            points.InsertPoint(1, 1e-3, 1, 0);
            points.InsertPoint(2, 1e-3, 1e-3, 0);
            points.InsertPoint(3, 1, 1e-3, 0);

            // create cells, and lines
            vtkCellArray cells = vtkCellArray.New();
            cells.Initialize();

            vtkPolyLine lines = vtkPolyLine.New();
            lines.GetPointIds().SetNumberOfIds(5);
            for (int i = 0; i < 4; ++i) lines.GetPointIds().SetId(i, i);
            lines.GetPointIds().SetId(4, 0);
            cells.InsertNextCell(lines);

            // now make tge polydata and display it
            vtkPolyData poly = vtkPolyData.New();
            poly.Initialize();
            poly.SetPoints(points);
            poly.SetLines(cells);

            // use normalized viewport coordinates since
            // they are independent of window size
            vtkCoordinate coordinate = vtkCoordinate.New();
            coordinate.SetCoordinateSystemToNormalizedViewport();

            vtkPolyDataMapper2D mapper = vtkPolyDataMapper2D.New();
            mapper.SetInput(poly);
            mapper.SetTransformCoordinate(coordinate);

            vtkActor2D actor = vtkActor2D.New();
            actor.SetMapper(mapper);
            actor.GetProperty().SetColor(color[0], color[1], color[2]);
            // line width should be at least 2 to be visible at extremes

            actor.GetProperty().SetLineWidth((float)2.0); // Line Width

            renderer.AddViewProp(actor);
        }

        private void CreateColorMap()
        {
            // Create the color map
            colorLookupTable = vtkLookupTable.New();
            LegendStyle1();
            colorLookupTable.Build();
        }

        private void CreateScalarBar()
        {
            // Initialize ScalarBar actor
            ScalarBar = vtkScalarBarActor.New();
            ScalarBar.SetLookupTable(colorLookupTable);

            // Assign default number of colors and label format
            ScalarBar.SetNumberOfLabels(10);
            ScalarBar.SetLabelFormat("%.2e");

            TextProp = vtkTextProperty.New();
            TextProp.SetFontSize(12);
            TextProp.SetBold(0);
            TextProp.SetFontFamilyToArial();
            TextProp.ItalicOff();
            TextProp.SetJustificationToLeft();
            TextProp.SetVerticalJustificationToBottom();
            TextProp.ShadowOff();
            TextProp.SetColor(1, 1, 1);

            ScalarBar.SetTitleTextProperty(TextProp);
            ScalarBar.SetLabelTextProperty(TextProp);

            // Assign default size of Scalar Bar
            ScalarBar.SetMaximumWidthInPixels(120);
            ScalarBar.SetPosition(0.015, 0.10);
            ScalarBar.SetPosition2(0.16, 0.90);

            //Hide ScalarBar
            ScalarBar.VisibilityOff();

            //Add to Viewport
            Viewport.AddActor2D(ScalarBar);
        }

        private void CreateAxes()
        {
            //  -------------------- Axes of Coordinate system --------------------------------------------
            Axes = new vtkAxesActor();
            Axes.GetXAxisShaftProperty().SetLineWidth((float)3.0);
            Axes.GetYAxisShaftProperty().SetLineWidth((float)3.0);
            Axes.GetZAxisShaftProperty().SetLineWidth((float)3.0);
            Axes.GetXAxisShaftProperty().SetRepresentationToSurface();
            Axes.GetYAxisShaftProperty().SetRepresentationToSurface();
            Axes.GetZAxisShaftProperty().SetRepresentationToSurface();

            Axes.GetXAxisCaptionActor2D().GetCaptionTextProperty().SetFontSize(25);
            Axes.GetXAxisCaptionActor2D().GetCaptionTextProperty().SetBold(0);
            Axes.GetXAxisCaptionActor2D().GetCaptionTextProperty().ItalicOff();
            Axes.GetXAxisCaptionActor2D().GetCaptionTextProperty().ShadowOff();
            Axes.GetXAxisCaptionActor2D().GetTextActor().SetTextScaleModeToNone();

            Axes.GetYAxisCaptionActor2D().GetCaptionTextProperty().SetFontSize(25);
            Axes.GetYAxisCaptionActor2D().GetCaptionTextProperty().SetBold(0);
            Axes.GetYAxisCaptionActor2D().GetCaptionTextProperty().ItalicOff();
            Axes.GetYAxisCaptionActor2D().GetCaptionTextProperty().ShadowOff();
            Axes.GetYAxisCaptionActor2D().GetTextActor().SetTextScaleModeToNone();

            Axes.GetZAxisCaptionActor2D().GetCaptionTextProperty().SetFontSize(25);
            Axes.GetZAxisCaptionActor2D().GetCaptionTextProperty().SetBold(0);
            Axes.GetZAxisCaptionActor2D().GetCaptionTextProperty().ItalicOff();
            Axes.GetZAxisCaptionActor2D().GetCaptionTextProperty().ShadowOff();
            Axes.GetZAxisCaptionActor2D().GetTextActor().SetTextScaleModeToNone();

            Widget = new vtkOrientationMarkerWidget();
            Widget.SetOrientationMarker(Axes);
            Widget.SetInteractor(renderWindow.GetInteractor());
            Widget.SetViewport(0.7, 0.7, 1.2, 1.2);
            Widget.SetEnabled(1);
            Widget.InteractiveOff();
        }

        private void CreateSlider()
        {
            //  ------------ Cliping slider ------------------------------------------------------------
            SliderRep = vtkSliderRepresentation2D.New();

            // Default value range and slider in the middle
            SliderRep.SetMinimumValue(0);
            SliderRep.SetMaximumValue(10);
            SliderRep.SetValue(5);

            // Title and Label properties
            SliderRep.SetTitleText("Clip Plane position");
            SliderRep.GetTitleProperty().SetFontSize(10);
            SliderRep.GetTitleProperty().SetFontFamilyToArial();
            SliderRep.GetTitleProperty().SetBold(0);
            SliderRep.GetTitleProperty().ShadowOff();
            SliderRep.SetTitleHeight(0.03);
            SliderRep.GetLabelProperty().SetFontSize(10);
            SliderRep.GetLabelProperty().SetFontFamilyToArial();
            SliderRep.GetLabelProperty().SetBold(0);
            SliderRep.GetLabelProperty().ShadowOff();

            // Slider positon - normalized to viewport
            SliderRep.GetPoint1Coordinate().SetCoordinateSystemToNormalizedViewport();
            SliderRep.GetPoint1Coordinate().SetValue(0.20, 0.09);
            SliderRep.GetPoint2Coordinate().SetCoordinateSystemToNormalizedViewport();
            SliderRep.GetPoint2Coordinate().SetValue(0.95, 0.09);

            // Slider dimensions
            SliderRep.SetSliderLength(0.08);
            SliderRep.SetSliderWidth(0.025);
            SliderRep.SetHandleSize(0.01);
            SliderRep.SetTubeWidth(0.005);
            SliderRep.SetEndCapLength(0.00);

            // Slider color properties:
            SliderRep.GetSliderProperty().SetColor(180.0 / 255.0, 180.0 / 255.0, 180.0 / 255.0);  // Change the color of the knob that slides
            SliderRep.GetTitleProperty().SetColor(1.0, 1.0, 1.0);                                 // Change the color of the text indicating what the slider controls
            SliderRep.GetLabelProperty().SetColor(1.0, 1.0, 1.0);                                 // Change the color of the text displaying the value
            SliderRep.GetSelectedProperty().SetColor(131.0 / 255, 245.0 / 255.0, 3.0 / 255.0);    // Change the color of the knob when the mouse is held on it
            SliderRep.GetTubeProperty().SetColor(180.0 / 255.0, 180.0 / 255.0, 180.0 / 255.0);    // Change the color of the bar
            SliderRep.GetCapProperty().SetColor(180.0 / 255.0, 180.0 / 255.0, 180.0 / 255.0);     // Change the color of the ends of the bar

            // Slider Widget
            SliderWidget = vtkSliderWidget.New();
            SliderWidget.SetInteractor(renderWindow.GetInteractor());
            SliderWidget.SetRepresentation(SliderRep);
            SliderWidget.SetAnimationModeToAnimate();
            SliderWidget.SetEnabled(0);
            SliderWidget.InteractionEvt += new vtkObject.vtkObjectEventHandler(MoveClipPlane);
        }

        private void CreateClipPlane()
        {
            // Clip Plane
            ClipPlane = vtkPlane.New();

            vtkPoints ClipPoints = vtkPoints.New();
            ClipPlaneSize = 1;

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    double y = j * ClipPlaneSize / 9 - ClipPlaneSize / 2;
                    double z = i * ClipPlaneSize / 9 - ClipPlaneSize / 2;
                    ClipPoints.InsertNextPoint(0, y, z);
                }
            }
            vtkUnstructuredGrid ClipGrid = vtkUnstructuredGrid.New();
            ClipGrid.SetPoints(ClipPoints);
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    vtkQuad quad = vtkQuad.New();
                    quad.GetPointIds().SetId(0, i * 10 + j);
                    quad.GetPointIds().SetId(1, i * 10 + j + 1);
                    quad.GetPointIds().SetId(2, (i + 1) * 10 + j + 1);
                    quad.GetPointIds().SetId(3, (i + 1) * 10 + j);
                    ClipGrid.InsertNextCell(quad.GetCellType(), quad.GetPointIds());
                }
            }

            vtkDataSetMapper ClipMapper = vtkDataSetMapper.New();
            ClipMapper.SetInput(ClipGrid);

            ClipPlaneActor = vtkActor.New();
            ClipPlaneActor.SetMapper(ClipMapper);
            ClipPlaneActor.GetProperty().EdgeVisibilityOn();
            ClipPlaneActor.GetProperty().SetColor(195.0 / 255.0, 195.0 / 255.0, 195.0 / 255.0);
            ClipPlaneActor.GetProperty().SetOpacity(0.1);
            ClipPlaneActor.VisibilityOff();
            Viewport.AddActor(ClipPlaneActor);

            //  ------------- Clip Plane Buttons --------------------------------------------
            Grid = CreateClipButton("Grid OFF", new double[2] { 0.875, 0.39 }, 8);
            Grid.StartInteractionEvt += new vtkObject.vtkObjectEventHandler(ShowGrid);

            Reverse = CreateClipButton("Reverse", new double[2] { 0.875, 0.34 }, 8);
            Reverse.StartInteractionEvt += new vtkObject.vtkObjectEventHandler(ReverseClipPlaneNormal);

            ClipX = CreateClipButton("Clip X", new double[2] { 0.88, 0.29 }, 8);
            ClipX.StartInteractionEvt += new vtkObject.vtkObjectEventHandler(SetClipPlaneNormal);

            ClipY = CreateClipButton("Clip Y", new double[2] { 0.88, 0.24 }, 8);
            ClipY.StartInteractionEvt += new vtkObject.vtkObjectEventHandler(SetClipPlaneNormal);

            ClipZ = CreateClipButton("Clip Z", new double[2] { 0.88, 0.19 }, 8);
            ClipZ.StartInteractionEvt += new vtkObject.vtkObjectEventHandler(SetClipPlaneNormal);

        }

        private vtkTextWidget CreateClipButton(string Text, double[] Position, int FontSize)
        {
            // Create Text Actor and Representation
            vtkTextActor TextActor = vtkTextActor.New();
            TextActor.SetInput(Text);
            TextActor.GetTextProperty().SetBold(0);
            TextActor.GetTextProperty().SetFontFamilyToArial();
            vtkTextRepresentation Rep = vtkTextRepresentation.New();
            Rep.SetPosition(Position[0], Position[1]);
            Rep.SetTextActor(TextActor);
            Rep.SetShowBorderToOff();

            // Set widget
            vtkTextWidget Widget = vtkTextWidget.New();
            Widget.SetRepresentation(Rep);
            Widget.GetTextActor().GetTextProperty().SetFontSize(FontSize);
            Widget.GetTextActor().SetTextScaleModeToViewport();
            Widget.SetInteractor(renderWindow.GetInteractor());
            Widget.SelectableOn();
            Widget.SetEnabled(0);
            Widget.ResizableOff();

            return Widget;
        }

        private void ShowGrid(vtkObject sender, vtkObjectEventArgs e)
        {
            if (ClipPlaneActor.GetVisibility() == 1)
            {
                ClipPlaneActor.VisibilityOff();
                Grid.GetTextActor().SetInput("Grid ON");
            }
            else
            {
                ClipPlaneActor.VisibilityOn();
                Grid.GetTextActor().SetInput("Grid OFF");
            }

            Refresh();
        }

        private void SetClipPlaneNormal(vtkObject sender, vtkObjectEventArgs e)
        {
            vtkTextWidget widget = sender as vtkTextWidget;
            string text = widget.GetTextActor().GetInput();

            if (text == "Clip X") SetClipPlane("X");
            if (text == "Clip Y") SetClipPlane("Y");
            if (text == "Clip Z") SetClipPlane("Z");

            Refresh();
        }

        private void ReverseClipPlaneNormal(vtkObject sender, vtkObjectEventArgs e)
        {
            double[] N = ClipPlane.GetNormal();
            ClipPlane.SetNormal(-N[0], -N[1], -N[2]);
            Refresh();
        }

        private void MoveClipPlane(vtkObject sender, vtkObjectEventArgs e)
        {
            if (ClipPlane.GetNormal()[0] != 0) ClipPlane.SetOrigin(SliderRep.GetValue(), ClipPlane.GetOrigin()[1], ClipPlane.GetOrigin()[2]);
            if (ClipPlane.GetNormal()[1] != 0) ClipPlane.SetOrigin(ClipPlane.GetOrigin()[0], SliderRep.GetValue(), ClipPlane.GetOrigin()[2]);
            if (ClipPlane.GetNormal()[2] != 0) ClipPlane.SetOrigin(ClipPlane.GetOrigin()[0], ClipPlane.GetOrigin()[1], SliderRep.GetValue());

            ClipPlaneActor.SetPosition(
                                ClipPlane.GetOrigin()[0],
                                ClipPlane.GetOrigin()[1],
                                ClipPlane.GetOrigin()[2]);
        }

        private void SelectAreaClick(vtkObject sender, vtkObjectEventArgs e)
        {
            int[] clickPos = Inter.GetEventPosition();
            vtkAreaPicker picker = vtkAreaPicker.New();
            picker.AreaPick(clickPos[0], clickPos[1], clickPos[0] + 100, clickPos[1] + 100, Viewport);

            if (picker.GetActor() != null)
            {
                vtkPlanes Boundary = picker.GetFrustum();
                vtkExtractGeometry Box = vtkExtractGeometry.New();
                Box.SetImplicitFunction(Boundary);
                Box.SetInput(picker.GetActor().GetMapper().GetInput());

                vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
                glyphFilter.SetInputConnection(Box.GetOutputPort());
                glyphFilter.Update();

                vtkPolyData selected = glyphFilter.GetOutput();
                vtkPoints points = vtkPoints.New();
                vtkUnstructuredGrid grid = vtkUnstructuredGrid.New();
                for (int i=0; i<selected.GetNumberOfPoints(); i++)
                    points.InsertNextPoint(selected.GetPoint(i)[0], selected.GetPoint(i)[1], selected.GetPoint(i)[2]);
                grid.SetPoints(points);
                vtkSphereSource sphere = vtkSphereSource.New();
                sphere.SetPhiResolution(6);
                sphere.SetThetaResolution(6);
                sphere.SetRadius(0.1);
                vtkGlyph3D glyph3D = vtkGlyph3D.New();
                glyph3D.SetInput(grid);
                glyph3D.SetSourceConnection(sphere.GetOutputPort());

                vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
                mapper.SetInputConnection(glyph3D.GetOutputPort());

                //double[] P = new double[3];
                //bool selected = false;
                //vtkPoints points = Faces.GetPoints();
                //double[] ClickedPoint = PointPicker.GetActor().GetMapper().GetInput().GetPoint(PointPicker.GetPointId());
                //for (int i = 0; i < points.GetNumberOfPoints(); i++)
                //{
                //    if (Math.Abs(points.GetPoint(i)[0] - ClickedPoint[0]) < 1e-6 &&
                //        Math.Abs(points.GetPoint(i)[1] - ClickedPoint[1]) < 1e-6 &&
                //        Math.Abs(points.GetPoint(i)[2] - ClickedPoint[2]) < 1e-6)
                //    {
                //        selected = true;
                //        P = points.GetPoint(i);
                //        break;
                //    }
                //}
                //
                //if (selected == true)
                //{
                //    SelectionPoints.InsertNextPoint(P[0], P[1], P[2]);
                //
                //    SelectionGlyph = vtkGlyph3D.New();
                //    SelectionGlyph.SetInput(SelectionPolyData);
                //    SelectionGlyph.SetSourceConnection(SelectionSphere.GetOutputPort());
                //    SelectionMapper.SetInputConnection(SelectionGlyph.GetOutputPort());
                //
                //    // Refresh Viewport
                //    Refresh();
                //}
            }
        }

        private void SelectPointClick(vtkObject sender, vtkObjectEventArgs e)
        {
            if (ModelLoaded == true && SelectionMode == true)
            {
                int[] clickPos = Inter.GetEventPosition();
                vtkPointPicker PointPicker = vtkPointPicker.New();
                PointPicker.SetTolerance(0.05);
                PointPicker.Pick(clickPos[0], clickPos[1], 0, Viewport);

                vtkPoints points = Faces.GetPoints();

                double[] PickPosition = PointPicker.GetPickPosition();

                for (int j = 0; j < points.GetNumberOfPoints(); j++)
                {
                    if (Math.Abs(points.GetPoint(j)[0] - PickPosition[0]) < 1e-6 &&
                        Math.Abs(points.GetPoint(j)[1] - PickPosition[1]) < 1e-6 &&
                        Math.Abs(points.GetPoint(j)[2] - PickPosition[2]) < 1e-6)
                    {
                        SelectionPoints.InsertNextPoint(PickPosition[0], PickPosition[1], PickPosition[2]);
                        break;
                    }
                }

                SelectionGlyph = vtkGlyph3D.New();
                SelectionGlyph.SetInput(SelectionPolyData);
                SelectionGlyph.SetSourceConnection(SelectionSphere.GetOutputPort());
                SelectionMapper.SetInputConnection(SelectionGlyph.GetOutputPort());
                SelectionActor.SetMapper(SelectionMapper);

                // Refresh Viewport
                Refresh();
            }
        }

        private void UnselectPointClick(vtkObject sender, vtkObjectEventArgs e)
        {
            if (ModelLoaded == true && SelectionMode == true)
            {
                int[] clickPos = Inter.GetEventPosition();
                vtkPointPicker PointPicker = vtkPointPicker.New();
                PointPicker.SetTolerance(0.05);
                PointPicker.Pick(clickPos[0], clickPos[1], 0, Viewport);


                double[] PickPosition = PointPicker.GetPickPosition();
                vtkPoints temp = vtkPoints.New();

                for (int j = 0; j < SelectionPoints.GetNumberOfPoints(); j++)
                {
                    if (Math.Abs(SelectionPoints.GetPoint(j)[0] - PickPosition[0]) < 1e-6 &&
                        Math.Abs(SelectionPoints.GetPoint(j)[1] - PickPosition[1]) < 1e-6 &&
                        Math.Abs(SelectionPoints.GetPoint(j)[2] - PickPosition[2]) < 1e-6)
                    { }
                    else
                    {
                        temp.InsertNextPoint(SelectionPoints.GetPoint(j)[0],
                                             SelectionPoints.GetPoint(j)[1],
                                             SelectionPoints.GetPoint(j)[2]);
                    }
                }

                SelectionPoints.ShallowCopy(temp);
                SelectionPolyData.SetPoints(SelectionPoints);
                SelectionGlyph = vtkGlyph3D.New();
                SelectionGlyph.SetInput(SelectionPolyData);
                SelectionGlyph.SetSourceConnection(SelectionSphere.GetOutputPort());
                SelectionMapper.SetInputConnection(SelectionGlyph.GetOutputPort());
                SelectionActor.SetMapper(SelectionMapper);

                // Refresh Viewport
                Refresh();
            }
        }


        // ===================== PUBLIC methods to modify Graphical View properties ===================

        public void ChangeBackgroundColor(bool gradient, double[] color1, double[] color2)
        {
            if (gradient == true)
            {
                Viewport.GradientBackgroundOn();
                Viewport.SetBackground(color1[0] / 255.0, color1[1] / 255.0, color1[2] / 255.0);
                Viewport.SetBackground2(color2[0] / 255.0, color2[1] / 255.0, color2[2] / 255.0);
            }
            else
            {
                Viewport.GradientBackgroundOff();
                Viewport.SetBackground(color1[0] / 255.0, color1[1] / 255.0, color1[2] / 255.0);
            }
        }

        public void ChangeLabelColor(double[] color)
        {
            TextProp.SetColor(color[0],color[1], color[2]);
            SliderRep.GetTitleProperty().SetColor(color[0], color[1], color[2]);
            SliderRep.GetLabelProperty().SetColor(color[0], color[1], color[2]);

            ScalarBar.SetLabelTextProperty(TextProp);
            ScalarBar.SetTitleTextProperty(TextProp);

            Axes.GetXAxisCaptionActor2D().GetCaptionTextProperty().SetColor(color[0], color[1], color[2]);
            Axes.GetYAxisCaptionActor2D().GetCaptionTextProperty().SetColor(color[0], color[1], color[2]);
            Axes.GetZAxisCaptionActor2D().GetCaptionTextProperty().SetColor(color[0], color[1], color[2]);

            Reverse.GetTextActor().GetTextProperty().SetColor(color[0], color[1], color[2]);
            ClipX.GetTextActor().GetTextProperty().SetColor(color[0], color[1], color[2]);
            ClipY.GetTextActor().GetTextProperty().SetColor(color[0], color[1], color[2]);
            ClipZ.GetTextActor().GetTextProperty().SetColor(color[0], color[1], color[2]);
            Grid.GetTextActor().GetTextProperty().SetColor(color[0], color[1], color[2]);
        }

        public void ChangeColorRange(double min, double max)
        {
            colorLookupTable.SetTableRange(min, max);
        }

        public void ChangeColorNumber(int numb)
        {
            colorLookupTable.SetNumberOfColors(numb);
            ScalarBar.SetNumberOfLabels(numb);
        }

        public void ChangeLegendStyle(int numb)
        {
            if (numb==1)
            {
                LegendStyle1();
                ScalarBar.SetNumberOfLabels(10);
            }
            if (numb == 2)
            {
                LegendStyle2();
                ScalarBar.SetNumberOfLabels(11);
            }
        }

        public void ChangeScalarName(string title)
        {
            ScalarBar.SetTitle(title);
        }

        public void ChangeScalarLabelFormat(int precision, char format)
        {
            ScalarBar.SetLabelFormat("%." + precision.ToString() + format.ToString());
        }

        public vtkLookupTable Get_ColorTable()
        {
            return colorLookupTable;
        }

        public void HideScalarBar()
        {
            ScalarBar.VisibilityOff();
        }

        public void ShowScalarBar()
        {
            ScalarBar.VisibilityOn();
        }

        public void AddActor(vtkActor actor)
        {
            Viewport.AddActor(actor);
        }

        public void Refresh()
        {
            Viewport.Render();
            Viewport.ResetCameraClippingRange();
            RenWinControl.Refresh();
        }

        public void FitView()
        {
            Viewport.ResetCamera();
        }

        public void SetViewOri(string view)
        {
            if(view == "XY")
            {
                Viewport.GetActiveCamera().SetPosition(0, 0, 1);
                Viewport.GetActiveCamera().SetFocalPoint(0, 0, -1);
                Viewport.GetActiveCamera().SetViewUp(0, 1, 0);
                Viewport.ResetCamera();
            }
            if (view == "YX")
            {
                Viewport.GetActiveCamera().SetPosition(0, 0, -1);
                Viewport.GetActiveCamera().SetFocalPoint(0, 0, 1);
                Viewport.GetActiveCamera().SetViewUp(0, -1, 0);
                Viewport.ResetCamera();
            }
            if (view == "YZ")
            {
                Viewport.GetActiveCamera().SetPosition(1, 0, 0);
                Viewport.GetActiveCamera().SetFocalPoint(-1, 0, 0);
                Viewport.GetActiveCamera().SetViewUp(0, 0, 1);
                Viewport.ResetCamera();
            }
            if (view == "ZY")
            {
                Viewport.GetActiveCamera().SetPosition(-1, 0, 0);
                Viewport.GetActiveCamera().SetFocalPoint(1, 0, 0);
                Viewport.GetActiveCamera().SetViewUp(0, 0, 1);
                Viewport.ResetCamera();
            }
            if (view == "ZX")
            {
                Viewport.GetActiveCamera().SetPosition(0, 1, 0);
                Viewport.GetActiveCamera().SetFocalPoint(0, -1, 0);
                Viewport.GetActiveCamera().SetViewUp(0, 0, 1);
                Viewport.ResetCamera();
            }
            if (view == "XZ")
            {
                Viewport.GetActiveCamera().SetPosition(0, -1, 0);
                Viewport.GetActiveCamera().SetFocalPoint(0, 1, 0);
                Viewport.GetActiveCamera().SetViewUp(0, 0, 1);
                Viewport.ResetCamera();
            }
            if (view == "ISO")
            {
                Viewport.GetActiveCamera().SetPosition(0, 0, 1);
                Viewport.GetActiveCamera().SetFocalPoint(1, 1, 0);
                Viewport.GetActiveCamera().SetViewUp(0, 0, 1);
                Viewport.ResetCamera();
            }
            if (view == "FIT")
            {
                Viewport.ResetCamera();
            }
            Refresh();
        }

        public void InitializeFaces()
        {
            vtkCleanPolyData Clean = vtkCleanPolyData.New();
            Clean.SetInputConnection(AppendFaces.GetOutputPort());
            Clean.Update();
            Faces = Clean.GetOutput();
        }

        // ----- Clip plane --------------------------------------------------

        public vtkPlane Get_ClipPlane()
        {
            return ClipPlane;
        }

        public vtkActor Get_ClipPlaneActor()
        {
            return ClipPlaneActor;
        }

        public void ShowClip(bool show)
        {
            if (show == true)
            {
                SliderWidget.SetEnabled(1);
                ClipPlaneActor.VisibilityOn();
                Reverse.SetEnabled(1);
                ClipX.SetEnabled(1);
                ClipY.SetEnabled(1);
                ClipZ.SetEnabled(1);
                Grid.SetEnabled(1);

                // Restore Clip Buttons position
                Grid.GetTextActor().SetPosition(0.88, 0.39);
                Reverse.GetTextActor().SetPosition(0.875, 0.34);
                ClipX.GetTextActor().SetPosition(0.88, 0.29);
                ClipY.GetTextActor().SetPosition(0.88, 0.24);
                ClipZ.GetTextActor().SetPosition(0.88, 0.19);
            }
            else
            {
                SliderWidget.SetEnabled(0);
                ClipPlaneActor.VisibilityOff();
                Reverse.SetEnabled(0);
                ClipX.SetEnabled(0);
                ClipY.SetEnabled(0);
                ClipZ.SetEnabled(0);
                Grid.SetEnabled(0);
            }
        }

        /// <summary>
        /// Set Clip Plane scale based on maximum dimension.
        /// <br>This method is used at the begining to set Plane size.</br>
        /// </summary>
        public void SetClipPlaneScale(double[] boundaryRange)
        {
            List<double> temp = new List<double>();
            temp.Add(Math.Abs(boundaryRange[1] - boundaryRange[0]) * 1.25);
            temp.Add(Math.Abs(boundaryRange[3] - boundaryRange[2]) * 1.25);
            temp.Add(Math.Abs(boundaryRange[5] - boundaryRange[4]) * 1.25);

            ClipPlaneSize = temp.Max();
            ClipPlaneActor.SetScale(ClipPlaneSize);
            BoundaryRange = boundaryRange;
        }

        /// <summary>
        /// Set Clip Plane direction (normal). If direction has changed plane is set at the minimal position
        /// </summary>
        public void SetClipPlane(string N)
        {
            bool OriChanged = false;             // Check if orientation has changed
            double[] n = ClipPlane.GetNormal();  // Previous direction of Normal
            double min = 0; double max = 1;
            if (N == "X")
            {
                if (n[0] == 0) OriChanged = true;
                min = BoundaryRange[0];
                max = BoundaryRange[1];
                ClipPlane.SetNormal(1, 0, 0);
                ClipPlaneActor.SetOrientation(0, 0, 0);
            }
            if (N == "Y")
            {
                if (n[1] == 0) OriChanged = true;
                min = BoundaryRange[2];
                max = BoundaryRange[3];
                ClipPlane.SetNormal(0, 1, 0);
                ClipPlaneActor.SetOrientation(0, 0, 90);
            }
            if (N == "Z")
            {
                if (n[2] == 0) OriChanged = true;
                min = BoundaryRange[4];
                max = BoundaryRange[5];
                ClipPlane.SetNormal(0, 0, 1);
                ClipPlaneActor.SetOrientation(0, 90, 0);
            }
            if (N == "-X")
            {
                if (n[0] == 0) OriChanged = true;
                min = BoundaryRange[0];
                max = BoundaryRange[1];
                ClipPlane.SetNormal(-1, 0, 0);
                ClipPlaneActor.SetOrientation(0, 0, 0);
            }
            if (N == "-Y")
            {
                if (n[1] == 0) OriChanged = true;
                min = BoundaryRange[2];
                max = BoundaryRange[3];
                ClipPlane.SetNormal(0, -1, 0);
                ClipPlaneActor.SetOrientation(0, 0, 90);
            }
            if (N == "-Z")
            {
                if (n[2] == 0) OriChanged = true;
                min = BoundaryRange[4];
                max = BoundaryRange[5];
                ClipPlane.SetNormal(0, 0, -1);
                ClipPlaneActor.SetOrientation(0, 90, 0);
            }
            // Update slider values
            SliderRep.SetMinimumValue(min);
            SliderRep.SetMaximumValue(max);

            // Use Boundary range of all Actors to set Clip Plane in the middle
            if (OriChanged == true)
            {
                if (N == "X")
                {
                    ClipPlane.SetOrigin(
                            BoundaryRange[0],
                            (BoundaryRange[3] + BoundaryRange[2]) / 2.0,
                            (BoundaryRange[5] + BoundaryRange[4]) / 2.0);
                }
                if (N == "Y")
                {
                    ClipPlane.SetOrigin(
                            (BoundaryRange[1] + BoundaryRange[0]) / 2.0,
                            BoundaryRange[2],
                            (BoundaryRange[5] + BoundaryRange[4]) / 2.0);
                }
                if (N == "Z")
                {
                    ClipPlane.SetOrigin(
                            (BoundaryRange[1] + BoundaryRange[0]) / 2.0,
                            (BoundaryRange[3] + BoundaryRange[2]) / 2.0,
                            BoundaryRange[4]);
                }

                ClipPlaneActor.SetPosition(
                    ClipPlane.GetOrigin()[0],
                    ClipPlane.GetOrigin()[1],
                    ClipPlane.GetOrigin()[2]);

                SliderRep.SetValue(min);
            }
        }

        // -------- Color Templates ---------------------------------------

        public void LegendStyle1()
        {
            // Assign default number of colors
            colorLookupTable.SetNumberOfTableValues(9);

            colorLookupTable.SetTableValue(8, 2.55E+02 / 255.0, 0.00E+00 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(7, 2.55E+02 / 255.0, 1.28E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(6, 2.55E+02 / 255.0, 2.55E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(5, 1.28E+02 / 255.0, 2.55E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(4, 0.00E+00 / 255.0, 2.55E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(3, 0.00E+00 / 255.0, 2.55E+02 / 255.0, 1.28E+02 / 255.0, 1.0);
            colorLookupTable.SetTableValue(2, 0.00E+00 / 255.0, 2.55E+02 / 255.0, 2.55E+02 / 255.0, 1.0);
            colorLookupTable.SetTableValue(1, 0.00E+00 / 255.0, 1.28E+02 / 255.0, 2.55E+02 / 255.0, 1.0);
            colorLookupTable.SetTableValue(0, 0.00E+00 / 255.0, 0.00E+00 / 255.0, 2.55E+02 / 255.0, 1.0);

        }

        public void LegendStyle2()
        {
            // Assign number of colors
            colorLookupTable.SetNumberOfTableValues(10);

            colorLookupTable.SetTableValue(9, 2.55E+02 / 255.0, 2.55E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(8, 2.55E+02 / 255.0, 2.02E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(7, 2.55E+02 / 255.0, 1.49E+02 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(6, 2.55E+02 / 255.0, 7.90E+01 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(5, 2.55E+02 / 255.0, 2.60E+01 / 255.0, 0.00E+00 / 255.0, 1.0);
            colorLookupTable.SetTableValue(4, 2.29E+02 / 255.0, 0.00E+00 / 255.0, 2.60E+01 / 255.0, 1.0);
            colorLookupTable.SetTableValue(3, 1.76E+02 / 255.0, 0.00E+00 / 255.0, 7.90E+01 / 255.0, 1.0);
            colorLookupTable.SetTableValue(2, 1.06E+02 / 255.0, 0.00E+00 / 255.0, 1.49E+02 / 255.0, 1.0);
            colorLookupTable.SetTableValue(1, 5.30E+01 / 255.0, 0.00E+00 / 255.0, 2.02E+02 / 255.0, 1.0);
            colorLookupTable.SetTableValue(0, 0.00E+00 / 255.0, 0.00E+00 / 255.0, 2.55E+02 / 255.0, 1.0);


        }

    }
}
