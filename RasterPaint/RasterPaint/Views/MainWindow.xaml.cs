using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;
using Microsoft.Win32;
using RasterPaint.Annotations;
using RasterPaint.Objects;
using RasterPaint.Utilities;

namespace RasterPaint.Views
{
    public sealed partial class MainWindow : INotifyPropertyChanged
    {
        #region Fields
        private WriteableBitmap _wb;
        public ClipWindow ClipWnd { get; set; }
        public ListWindow ListWnd { get; set; }

        #region Application Modes
        private bool _removalMode;
        private bool _drawingMode;
        private bool _moveObjectMode;
        private bool _editObjectMode;
        private bool _fillPolygonMode;
        private bool _reduceImageMode;
        private bool _clipPolygonMode; // application modes;
        private bool _editCoeffsMode;
        #endregion

        #region Points and Objects
        private bool _clipWndHelpWndShown = false;

        private byte _rValue;
        private byte _gValue;
        private byte _bValue;

        private Point _lastPoint;
        private Point _firstPoint;
        private Point _movePoint;
        private Point _lastMovePoint;
        private Point? _clipStartPoint;
        private Point? _clipEndPoint;

        private MyLine _firstLine;
        private MyLine _secondLine;
        private MyObject _objectToMove;
        private MyObject _objectToEdit;
        private MyPolygon _polygonToClip;
        private MyPolygon _clippingPolygon;
        private MyObject _temporaryObject;
        private PhongLight _phongLightToMove;

        private PhongIlluminationModel _illuminationModel;
        #endregion

        #region Phong Shading
        public double RAmbient { get; set; } = 0.15;
        public double RDiffuse { get; set; } = 0.85;
        public double RSpecular { get; set; } = 0.45;
        public double GAmbient { get; set; } = 0.15;
        public double GDiffuse { get; set; } = 0.85;
        public double GSpecular { get; set; } = 0.45;
        public double BAmbient { get; set; } = 0.15;
        public double BDiffuse { get; set; } = 0.85;
        public double BSpecular { get; set; } = 0.45;
        public int Shininess { get; set; } = 20;

        public int ViewerZ { get; set; } = 60;
        public int LightsZ { get; set; } = 70;

        public List<PhongMaterial> AllMaterials
        {
            get
            {
                return
                    ObjectsList.OfType<MyPolygon>()
                        .Where(x => x.PhongMaterial != null)
                        .Select(x => x.PhongMaterial)
                        .ToList();
            }
        }

        public bool EditCoeffsMode
        {
            get { return _editCoeffsMode; }

            set
            {
                _editCoeffsMode = value;
                EditCoeffsButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }
        #endregion
        #endregion

        #region Public Properties
        public List<MyObject> ObjectsList { get; }

        public int GridCellValue { get; set; }
        public int LineWidthValue { get; set; }

        public Color GridColor { get; set; } = Colors.Gray;
        public Color BackgroundColor { get; set; } = Color.FromRgb(25, 25, 25);
        public Color ObjectColor { get; set; } = Colors.DarkViolet;
        public Color HighlightColor { get; set; } = Colors.Black;
        public Color FillColor { get; set; } = Colors.CornflowerBlue;

        public Brush ButtonBrush { get; set; } = Brushes.LightGray;
        public Brush EnabledBrush { get; set; } = Brushes.LightGreen;
        #endregion

        #region App. Logic Properties
        #region Modes
        public bool ShowGrid { get; set; }

        public bool RemovalMode
        {
            get { return _removalMode; }

            set
            {
                _removalMode = value;
                RemoveButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }

        public bool MoveObjectMode
        {
            get { return _moveObjectMode; }

            set
            {
                _moveObjectMode = value;
                MoveButton.Background = value ? EnabledBrush : ButtonBrush;

                DrawGrid();
                RedrawAllObjects(_wb);
                UserInformation.Text = "";
            }
        }

        public bool EditObjectMode
        {
            get { return _editObjectMode; }

            set
            {
                _editObjectMode = value;

                if (_objectToEdit != null)
                {
                    _objectToEdit.DrawObject(_wb);
                    _objectToEdit = null;
                    _firstLine = _secondLine = null;
                }

                EditButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }

        public bool FillPolygonMode
        {
            get { return _fillPolygonMode; }

            set
            {
                _fillPolygonMode = value;
                _objectToEdit = value ? _objectToEdit : null;
                FillButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }

        public bool ReduceImageMode
        {
            get { return _reduceImageMode; }

            set
            {
                _reduceImageMode = value;
                _objectToEdit = value ? _objectToEdit : null;
                ReduceButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }

        public bool ClipPolygonMode
        {
            get { return _clipPolygonMode; }

            set
            {
                _clipPolygonMode = value;
                _objectToEdit = value ? _objectToEdit : null;
                ClipButton.Background = value ? EnabledBrush : ButtonBrush;

                if (value)
                {
                    UserInformation.Text = "Right click on a polygon you want to clip and then drag the clipping polygon onto the polygon to clip.";
                }
            }
        }

        public bool IlluminationEnabled => EnableIlluminationCheckBox.IsChecked != null && EnableIlluminationCheckBox.IsChecked.Value;
        public bool BumpMappingEnabled => BumpMappingCheckBox.IsChecked != null && BumpMappingCheckBox.IsChecked.Value;
        #endregion
        
        #region Drawing
        private void DrawGrid(bool ifToErase = false)
        {
            Color color = ShowGrid ? GridColor : BackgroundColor;

            if (ifToErase)
            {
                _wb.Clear(BackgroundColor);
                color = GridColor;
            }

            for (var i = 0; i <= Math.Max(ImageGrid.ActualWidth, ImageGrid.ActualHeight); i += GridCellValue)
            {
                _wb.DrawLine(new Point(i, 0), new Point(i, ImageGrid.ActualWidth), color, 0); // 0: (default), 1 px;
                _wb.DrawLine(new Point(0, i), new Point(ImageGrid.ActualWidth, i), color, 0); // narysowanie siatki;
            }
        }

        public bool DrawingMode
        {
            get { return _drawingMode; }

            set
            {
                _drawingMode = value;

                if (ObjectColorPicker != null)
                {
                    ObjectColorPicker.IsEnabled = !value;
                    LineWidth.IsEnabled = !value;
                }
            }
        }

        public bool DrawingPolygon { get; set; }

        public bool DrawingLine { get; set; }

        public bool DrawingPoint { get; set; }
        #endregion

        #region Color Reduction
        public byte RValue
        {
            get { return _rValue; }

            set
            {
                if (value == _rValue) return;
                _rValue = value;
                OnPropertyChanged();
            }
        }

        public byte GValue
        {
            get { return _gValue; }

            set
            {
                if (value == _gValue) return;
                _gValue = value;
                OnPropertyChanged();
            }
        }

        public byte BValue
        {
            get { return _bValue; }

            set
            {
                if (value == _bValue) return;
                _bValue = value;
                OnPropertyChanged();
            }
        }
        #endregion
        #endregion

        public MainWindow()
        {
            // ShowSplashScreen();

            InitializeComponent();
            DataContext = this;

            ObjectsList = new List<MyObject>();
            RemovalMode = false;
            ShowGrid = false;

            RValue = GValue = BValue = 2;
        }

        #region Event Handlers
        #region Buttons
        private void DrawGridButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGrid = !ShowGrid;

            DrawGrid();
            RedrawAllObjects(_wb);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemovalMode = !RemovalMode;
        }

        private void MoveButton_Click(object sender, RoutedEventArgs e)
        {
            MoveObjectMode = !MoveObjectMode;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditObjectMode = !EditObjectMode;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpWindow hw = new HelpWindow();
            hw.ShowDialog();
        }

        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            FillPolygonMode = !FillPolygonMode;
        }

        public void ReduceButton_Click(object sender, RoutedEventArgs e)
        {
            ReduceImageMode = !ReduceImageMode;
        }

        private void ClipButton_Click(object sender, RoutedEventArgs e)
        {
            ClipPolygonMode = !ClipPolygonMode;

            if (!_clipWndHelpWndShown)
            {
                if (MessageBox.Show(
                    "Do you want to display help window?",
                    "Display Clip Help Window?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var clipHelpWindow = new ClipHelpWindow();
                    clipHelpWindow.Show();
                }
            }

            _clipWndHelpWndShown = true;
        }

        private void ListButton_Click(object sender, RoutedEventArgs e)
        {
            ListWnd = new ListWindow(ObjectsList, _wb, BackgroundColor);
            ListWnd.Show();
        }

        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = ".xml Files (*.xml)|*.xml",
                Multiselect = false,
                FileName = "Serialized.xml"
            };

            if (ofd.ShowDialog() != true) return;

            XmlSerializer xs = new XmlSerializer(typeof (SerializableObject));
            StreamReader sr = new StreamReader(ofd.OpenFile());

            SerializableObject so = xs.Deserialize(sr) as SerializableObject;

            ObjectsList.RemoveAll(x => true);

            if (so == null) return;

            foreach (var item in so.AllLines)
            {
                AddObjectToList(item);
            }

            foreach (var item in so.AllPolygons)
            {
                AddObjectToList(item);
            }

            foreach (var item in so.AllPoints)
            {
                AddObjectToList(item);
            }

            GridCellValue = so.GridSize;
            GridSize.Value = GridCellValue;

            GridColor = so.GridColor;
            BackgroundColor = so.BackgroundColor;
            
            GridColorPicker.SelectedColor = GridColor;
            BackgroundColorPicker.SelectedColor = BackgroundColor;

            ShowGrid = so.ShowGrid;
            DrawGrid();
            RedrawAllObjects(_wb);
        }

        private void ClipWndButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClipWnd != null)
            {
                ClipWnd.BackgroundColor = BackgroundColor;
                ClipWnd.ListOfAllObjects = ObjectsList;
                ClipWnd.OnPropertyChanged();

                ClipWnd.Show();
            }
            else
            {
                ClipWnd = new ClipWindow(ObjectsList, this);
                ClipWnd.Show();
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            XmlSerializer xs = new XmlSerializer(typeof (SerializableObject));

            using (FileStream fs = new FileStream(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Serialized.xml", FileMode.Create))
            {
                xs.Serialize(fs, new SerializableObject(ObjectsList, BackgroundColor, GridColor, GridCellValue, ShowGrid));

                UserInformation.Text = "Serialization succedded. File was saved on Desktop.";
            }
        }

        private void SavePngButton_OnClick(object sender, RoutedEventArgs e)
        {
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\SavedImage.png";

            Static.CreateThumbnail(filePath, _wb.Clone());

            MessageBox.Show("Scene successfully saved on Desktop.");
        }

        private void DrawingType_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;

            if (button != null)
            {
                switch (button.Name)
                {
                    case "PolygonRadioButton":
                        DrawingPolygon = true;
                        DrawingLine = false;
                        DrawingPoint = false;
                        break;
                    case "LineRadioButton":
                        DrawingLine = true;
                        DrawingPolygon = false;
                        DrawingPoint = false;
                        break;
                    case "PointRadioButton":
                        DrawingPoint = true;
                        DrawingLine = false;
                        DrawingPolygon = false;
                        break;
                }
            }
        }

        #region Color Quantization
        private int ColorsCount => ColorsCountUpDown.Value ?? 0;

        private void UniformQuantization_Click(object sender, RoutedEventArgs e)
        {
            if (_objectToEdit is MyPolygon)
            {
                MyPolygon mp = _objectToEdit as MyPolygon;
                mp.FillBitmap = mp.InitialBitmap.Clone();

                var newBitmap = ColorReduction.UniformQuantization(mp.FillBitmap, RValue, GValue, BValue);
                mp.FillBitmap = newBitmap;
            }

            ClearAndRedraw();
        }

        private void PopularityQuantization_Click(object sender, RoutedEventArgs e)
        {
            if (_objectToEdit is MyPolygon)
            {
                MyPolygon mp = _objectToEdit as MyPolygon;
                mp.FillBitmap = mp.InitialBitmap.Clone();

                var newBitmap = ColorReduction.PopularityAlgorithm(mp.FillBitmap, ColorsCount);
                mp.FillBitmap = newBitmap;
            }

            ClearAndRedraw();
        }

        private void OctreeQuantization_Click(object sender, RoutedEventArgs e)
        {
            if (_objectToEdit is MyPolygon)
            {
                MyPolygon mp = _objectToEdit as MyPolygon;
                mp.FillBitmap = mp.InitialBitmap.Clone();

                Octree o = new Octree(mp.FillBitmap);

                var newBitmap = o.GenerateBitmapFromOctree(ColorsCount);
                mp.FillBitmap = newBitmap;
            }

            ClearAndRedraw();
        }

        private void RevertReduction_Click(object sender, RoutedEventArgs e)
        {
            var polygons = ObjectsList.OfType<MyPolygon>().Where(x => x.IfToFillWithImage).Select(x => x);

            foreach (MyPolygon mp in polygons)
            {
                mp.FillBitmap = mp.InitialBitmap;
            }

            ClearAndRedraw();
        }
        #endregion
        #endregion

        #region WriteableBitmap
        private void MyImage_ButtonDown(object sender, MouseButtonEventArgs e) // kliknięcie na bitmapę;
        {
            Point p = e.GetPosition(MyImage);
            MyObject myObject = null;
            bool removeNow = false;

            if (TabController.SelectedIndex == 1 && !ReduceImageMode && !EditObjectMode)
            {
                MessageBox.Show("Remember to turn \"Reduce\" mode on (first tab).");
            }

            if (TabController.SelectedIndex == 2)
            {
                if (_illuminationModel != null)
                {
                    foreach (var item in _illuminationModel.PhongLights)
                    {
                        if (Static.DistanceBetweenPoints(new Point(item.Position.X, item.Position.Y), p) <= 15)
                        {
                            _phongLightToMove = item;
                            break;
                        }
                    }
                }
                
                foreach (MyPolygon mp in ObjectsList.OfType<MyPolygon>())
                {
                    if (EditCoeffsMode && mp.MyBoundary.Contains(p))
                    {
                        var newMaterial = GetNewMaterial();

                        if (mp.PhongMaterial != newMaterial)
                        {
                            mp.PhongMaterial = newMaterial;
                        }

                        MessageBox.Show("Polygon's material changed successfully.");
                        break;
                    }
                }
            }

            if (!RemovalMode && !MoveObjectMode && !DrawingMode && !EditObjectMode && !FillPolygonMode && !ClipPolygonMode && !ReduceImageMode && TabController.SelectedIndex != 2)
                // zaczynamy rysować;
            {
                if (DrawingPolygon)
                {
                    _temporaryObject = new MyPolygon();
                }
                else if (DrawingLine)
                {
                    _temporaryObject = new MyLine();
                }
                else if (DrawingPoint)
                {
                    _temporaryObject = new MyPoint();
                }

                DrawingMode = true;
                _temporaryObject.Width = LineWidthValue;
                _temporaryObject.Color = ObjectColor;
                _firstPoint = _lastPoint = p;
            }
            else if (RemovalMode) // tryb usuwania;
            {
                foreach (MyObject mo in ObjectsList)
                {
                    if (mo.MyBoundary.Contains(p) || (mo is MyPoint && Static.DistanceBetweenPoints(p, ((MyPoint) mo).Point) <= 15.0F))
                    {
                        myObject = mo;
                        mo.HighlightObject(true, _wb, HighlightColor);

                        if (MessageBox.Show("Do you want to delete highlighted object?",
                            "Delete the object?",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question)
                            == MessageBoxResult.OK)
                        {
                            removeNow = true;
                            break;
                        }
                    }

                    mo.HighlightObject(false, _wb, HighlightColor);
                    RemovalMode = true;
                }
            }
            else if (MoveObjectMode)
            {
                _movePoint = e.GetPosition(MyImage);

                foreach (var mo in ObjectsList)
                {
                    if (mo.MyBoundary.Contains(_movePoint) || (mo is MyPoint && DistanceBetweenPoints(p, ((MyPoint) mo).Point) <= Static.Distance) || mo.IfPointCloseToBoundary(_movePoint))
                    {
                        myObject = mo;
                        mo.HighlightObject(true, _wb, HighlightColor); // podświetlenie obiektu;
                        break;
                    }
                }

                _objectToMove = myObject;
            }
            else if (EditObjectMode)
            {
                foreach (var mo in ObjectsList)
                {
                    if (mo.MyBoundary.Contains(p) || (mo is MyPoint && DistanceBetweenPoints(p, ((MyPoint)mo).Point) <= Static.Distance) || mo.IfPointCloseToBoundary(p))
                    {
                        myObject = mo;
                        _objectToEdit = myObject;
                        mo.HighlightObject(true, _wb, HighlightColor);
                        break;
                    }
                }

                if (_objectToEdit is MyPolygon)
                {
                    MyPolygon myPolygon = _objectToEdit as MyPolygon;
                    Point point = SnapPoint(myPolygon, p, (int) Static.Distance);

                    foreach (var item in myPolygon.LinesList)
                    {
                        if (point.Equals(item.EndPoint))
                        {
                            _firstLine = item;
                        }

                        if (point.Equals(item.StartPoint))
                        {
                            _secondLine = item;
                        }
                    }
                }
            }
            else if (FillPolygonMode)
            {
                foreach (var item in ObjectsList.Where(item => item is MyPolygon && item.MyBoundary.Contains(p)))
                {
                    _objectToEdit = item as MyPolygon;
                    break;
                }

                if (_objectToEdit is MyPolygon)
                {
                    MyPolygon polygonToEdit = _objectToEdit as MyPolygon;

                    using (FillOptionWindow fow = new FillOptionWindow(polygonToEdit))
                    {
                        if (fow.ShowDialog() == true)
                        {
                            if (fow.ChosenOption == ChosenOption.ImageBrush)
                            {
                                polygonToEdit.FillBitmap = fow.LoadedFillBitmap;
                                polygonToEdit.InitialBitmap = polygonToEdit.FillBitmap.Clone();
                                polygonToEdit.DrawObject(_wb);
                            }
                            else if (fow.ChosenOption == ChosenOption.NormalBitmap)
                            {
                                polygonToEdit.NormalBitmap = fow.LoadedNormalBitmap;
                            }
                            else
                            {
                                var color = fow.LoadedColor;

                                polygonToEdit.FillBitmap = null;
                                polygonToEdit.FillColor = color;
                                polygonToEdit.DrawObject(_wb);
                            }
                        }
                    }

                    FillPolygonMode = false;
                }
            }
            else if(ReduceImageMode)
            {
                TabController.SelectedIndex = 1;
                UserInformation.Text = "Select a desired polygon (border color will change to Black) and choose the algorithm.";

                foreach (var mo in ObjectsList)
                {
                    if (mo.MyBoundary.Contains(p) || (mo is MyPoint && DistanceBetweenPoints(p, ((MyPoint)mo).Point) <= Static.Distance) || mo.IfPointCloseToBoundary(p))
                    {
                        myObject = mo;
                        _objectToEdit = myObject;
                        mo.HighlightObject(true, _wb, HighlightColor);
                        break;
                    }
                }
            }
            else if (ClipPolygonMode)
            {
                _clipStartPoint = p;
            }

            if (ClipPolygonMode && MoveObjectMode)
            {
                if (_objectToMove is MyPolygon && ((MyPolygon)_objectToMove).PolygonIsConvex())
                {
                    _clippingPolygon = _objectToMove as MyPolygon;
                }
                else
                {
                    UserInformation.Text = "The clipping polygon is not convex! Clipping aborted.";
                    ClipPolygonMode = false;
                    _polygonToClip = null;
                }
            }

            if (removeNow)
            {
                myObject.EraseObject(ObjectsList, _wb, BackgroundColor);

                if (ShowGrid)
                {
                    DrawGrid();
                }

                RedrawAllObjects(_wb);
            }
        }

        private void MyImage_ButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(MyImage);
            _phongLightToMove = null;

            if (DrawingMode && !RemovalMode && !MoveObjectMode && !EditObjectMode && !ClipPolygonMode && TabController.SelectedIndex != 2)
            {
                if (DrawingPolygon)
                {
                    Point snappedPoint = SnapPoint(_temporaryObject, point, 15); // "snap", 15px;

                    if (snappedPoint.Equals(_firstPoint) && ((MyPolygon) _temporaryObject).LinesList.Count > 1)
                    {
                        ClosePolygon();

                        EraseLine(_lastPoint, _lastMovePoint);
                        DrawGrid();
                        RedrawAllObjects(_wb);
                    }
                    else
                    {
                        if (ObjectColorPicker.SelectedColor != null)
                        {
                            ((MyPolygon) _temporaryObject).DrawAndAddLine(_wb, new MyLine(_lastPoint, point), _temporaryObject.Color);
                        }
                    }

                    _lastPoint = point;
                }
                else if (DrawingLine)
                {
                    if (ObjectColorPicker.SelectedColor != null && point != _firstPoint)
                    {
                        ((MyLine) _temporaryObject).DrawAndAddLine(_wb, new MyLine(_lastPoint, point), _temporaryObject.Color);

                        AddObjectToList(_temporaryObject.Clone());
                        ClearTemporaryObject();
                    }

                    DrawingMode = false;
                }
                else if (DrawingPoint)
                {
                    if (ObjectColorPicker.SelectedColor != null)
                    {
                        ((MyPoint) _temporaryObject).DrawAndAdd(_wb, _lastPoint, _temporaryObject.Color, _temporaryObject.Width);
                    }

                    AddObjectToList(_temporaryObject.Clone());
                    ClearTemporaryObject();

                    DrawingMode = false;
                }
            }
            else if (MoveObjectMode && !RemovalMode)
            {
                Point p = e.GetPosition(MyImage);
                Vector v = new Vector(p.X - _movePoint.X, p.Y - _movePoint.Y);

                if (_objectToMove != null && ObjectsList.Contains(_objectToMove))
                {
                    MyObject newObject = _objectToMove.MoveObject(v);
                    newObject.UpdateBoundaries();

                    if (_objectToMove is MyPolygon && _objectToMove.Equals(_clippingPolygon))
                    {
                        _clippingPolygon = newObject as MyPolygon;
                    }

                    _objectToMove.EraseObject(ObjectsList, _wb, BackgroundColor);
                    AddObjectToList(newObject);
                }

                MoveObjectMode = true;
                _wb.Clear(BackgroundColor);
                if (ShowGrid) DrawGrid(true);
                RedrawAllObjects(_wb);

                _objectToMove = null;
            }
            else if (EditObjectMode)
            {
                if (_firstLine != null && _secondLine != null && _objectToEdit != null)
                {
                    if (DrawingPolygon)
                    {
                        _firstLine.EndPoint = point;
                        _secondLine.StartPoint = point;
                    }

                    _objectToEdit.UpdateBoundaries();
                    ClearAndRedraw();
                }
            }
            else if (ClipPolygonMode)
            {
                _clipEndPoint = point;
                ClipPolygonWithRectangle();
            }

            if (MoveObjectMode && ClipPolygonMode)
            {
                if (_polygonToClip != null && _clippingPolygon != null)
                {
                    if (_clippingPolygon.PolygonIsConvex())
                    {
                        var newPolygonArray = PolygonClipping.GetIntersectedPolygon(_polygonToClip.GetPointsArray, _clippingPolygon.GetPointsArray);

                        if (newPolygonArray.Count() > 0)
                        {
                            MyPolygon mp = new MyPolygon
                            {
                                Color = _polygonToClip.Color,
                                FillColor = _polygonToClip.FillColor,
                                Width = _polygonToClip.Width
                            };

                            if (_polygonToClip.IfToFillWithImage)
                            {
                                mp.InitialBitmap = _polygonToClip.InitialBitmap.Clone();
                                mp.FillBitmap = _polygonToClip.FillBitmap.Clone();
                            }

                            for (int i = 0; i < newPolygonArray.Count(); i++)
                            {
                                mp.LinesList.Add(new MyLine(newPolygonArray[i], newPolygonArray[(i + 1) % newPolygonArray.Count()]));
                            }

                            mp.UpdateBoundaries();
                            ObjectsList.Remove(_polygonToClip);
                            AddObjectToList(mp);
                            ClearAndRedraw();
                        }
                        else
                        {
                            UserInformation.Text = "Program failed to clip the polygon!";
                        }

                        _polygonToClip = null;
                        _clippingPolygon = null;
                        ClipPolygonMode = false;
                    }
                }  
            }
        }

        private void MyImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ClipPolygonMode)
            {
                Point p = e.GetPosition(MyImage);

                foreach (var obj in ObjectsList)
                {
                    if (obj.MyBoundary.Contains(p))
                    {
                        _polygonToClip = obj as MyPolygon;
                        _polygonToClip?.HighlightObject(true, _wb, Colors.Black);
                        break;
                    }
                }
            }
        }

        private void MyImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (DrawingMode)
            {
                if (DrawingPolygon && _temporaryObject is MyPolygon && !RemovalMode)
                {
                    if (((MyPolygon) _temporaryObject).LinesList.Count > 1)
                    {
                        Point point = _firstPoint;

                        ClosePolygon();

                        _lastPoint = point;
                    }
                    else
                    {
                        foreach (var item in ((MyPolygon) _temporaryObject).LinesList)
                        {
                            _wb.DrawLine(item.StartPoint, item.EndPoint, BackgroundColor, LineWidthValue);
                        }

                        if (DrawingMode)
                        {
                            _wb.DrawLine(_lastPoint, _lastMovePoint, BackgroundColor, LineWidthValue);
                        }

                        ClearTemporaryObject();
                        if (ShowGrid) DrawGrid();
                        RedrawAllObjects(_wb);
                    }
                }
                else if (DrawingLine && _temporaryObject is MyLine)
                {
                    Point point = e.GetPosition(MyImage);

                    MyLine ml = new MyLine(_firstPoint, point)
                    {
                        Color = _temporaryObject.Color,
                        MyBoundary = _temporaryObject.MyBoundary,
                        Width = _temporaryObject.Width
                    };

                    ml.DrawAndAddLine(_wb, ml, ObjectColor);
                    AddObjectToList(ml);
                    _lastPoint = point;
                }

                DrawingMode = false;
            }
        }

        private void MyImage_OnMouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(MyImage);

            if (TabController.SelectedIndex == 2 && _phongLightToMove != null)
            {
                _phongLightToMove.Position = new Vector3D(p.X, p.Y, _phongLightToMove.Position.Z);
                ClearAndRedraw();
            }
            else if (e.LeftButton == MouseButtonState.Pressed && DrawingMode && !DrawingPoint && !RemovalMode && !ClipPolygonMode && !MoveObjectMode && TabController.SelectedIndex == 0)
            {
                _wb.Clear(BackgroundColor);

                DrawGrid();
                RedrawObject(_temporaryObject);
                RedrawAllObjects(_wb);

                if (ObjectColorPicker.SelectedColor != null)
                {
                    _wb.DrawLine(_lastPoint, p, ObjectColorPicker.SelectedColor.Value, LineWidthValue);
                }
            }
            else if (MoveObjectMode && _objectToMove != null)
            {
                Vector v = new Vector(p.X - _movePoint.X, p.Y - _movePoint.Y);
                var newObject = _objectToMove.MoveObject(v);
                newObject.UpdateBoundaries();

                ClearAndRedraw();
                newObject.DrawObject(_wb);

                _polygonToClip?.HighlightObject(true, _wb, HighlightColor);
            }
            else if (ClipPolygonMode)
            {
                ClearAndRedraw();

                if (MoveObjectMode)
                {
                    _polygonToClip?.HighlightObject(true, _wb, HighlightColor);
                }

                _polygonToClip?.HighlightObject(true, _wb, HighlightColor);

                if (_clipStartPoint != null)
                {
                    if (_clipStartPoint.Value.X > p.X && _clipStartPoint.Value.Y > p.Y) // wynika to z ograniczenia f-cji DrawRectangle();
                    {
                        _wb.DrawRectangle((int) p.X, (int) p.Y, (int)_clipStartPoint.Value.X, (int)_clipStartPoint.Value.Y, Colors.Green);
                    }
                    else
                    {
                        _wb.DrawRectangle((int)_clipStartPoint.Value.X, (int)_clipStartPoint.Value.Y, (int)p.X, (int)p.Y, Colors.Green);
                    }
                }
            }

            _lastMovePoint = p;
        }

        private void ImageGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _wb = new WriteableBitmap((int) e.NewSize.Width, (int) e.NewSize.Height, 96, 96, PixelFormats.Bgra32, null);
            MyImage.Source = _wb;

            _wb.Clear(BackgroundColor);
            DrawGrid();
            RedrawAllObjects(_wb);
        }
        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            ClipWnd?.Close();
            ListWnd?.Close();
        }
        #endregion

        private void TabController_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UserInformation.Text = "";
        }

        #region Properties
        private void GridSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            GridCellValue = (int) e.NewValue;

            if (_wb != null)
            {
                DrawGrid(true);
                // DrawGrid();
                RedrawAllObjects(_wb);
            }
        }

        private void LineWidth_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            LineWidthValue = (int) e.NewValue;

            if (EditObjectMode && _objectToEdit != null)
            {
                _objectToEdit.Width = LineWidthValue;
            }

            if (_wb != null)
            {
                ClearAndRedraw();
            }
        }

        private void ClearAndRedraw()
        {
            _wb.Clear(BackgroundColor);
            DrawGrid();
            RedrawAllObjects(_wb);
        }

        private void BackgroundColor_OnSelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue != null && _wb != null)
            {
                BackgroundColor = e.NewValue.Value;
                ClearAndRedraw();
            }
        }

        private void ObjectColor_OnSelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue != null && _wb != null)
            {
                ObjectColor = e.NewValue.Value;
            }

            if (_objectToEdit != null)
            {
                _objectToEdit.Color = ObjectColor;
                _objectToEdit.DrawObject(_wb);
            }
        }

        private void GridColor_OnSelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue != null && _wb != null)
            {
                GridColor = e.NewValue.Value;
                ClearAndRedraw();
            }
        }

        private void FillColor_OnSelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue != null && _wb != null)
            {
                FillColor = e.NewValue.Value;
            }

            if (_objectToEdit is MyPolygon)
            {
                ((MyPolygon)_objectToEdit).FillColor = FillColor;
                _objectToEdit.DrawObject(_wb);
            }
        }
        #endregion
        #endregion

        #region TemporaryObject
        private void ClearTemporaryObject()
        {
            if (_temporaryObject is MyPolygon)
            {
                ((MyPolygon) _temporaryObject).LinesList.RemoveAll(x => true);
            }
            else if (_temporaryObject is MyLine)
            {
                ((MyLine) _temporaryObject).StartPoint = ((MyLine) _temporaryObject).EndPoint = new Point(0, 0);
            }
            else if (_temporaryObject is MyPoint)
            {
                ((MyPoint) _temporaryObject).Point = new Point(0, 0);
            }

            _temporaryObject.Color = Colors.Transparent;
            _temporaryObject.Width = 0;
            _temporaryObject.MyBoundary = new MyBoundary();
        }

        private static Point SnapPoint(MyObject mo, Point p, int distance)
        {
            if (distance > 0)
            {
                if (mo is MyPolygon)
                {
                    foreach (var item in ((MyPolygon) mo).LinesList)
                    {
                        if (DistanceBetweenPoints(p, item.StartPoint) <= distance)
                        {
                            return item.StartPoint;
                        }

                        if (DistanceBetweenPoints(p, item.EndPoint) <= distance)
                        {
                            return item.EndPoint;
                        }
                    }
                }
                else if (mo is MyLine)
                {
                    if (DistanceBetweenPoints(p, ((MyLine) mo).StartPoint) <= distance)
                    {
                        return ((MyLine) mo).StartPoint;
                    }

                    if (DistanceBetweenPoints(p, ((MyLine) mo).EndPoint) <= distance)
                    {
                        return ((MyLine) mo).EndPoint;
                    }
                }
                else if (mo is MyPoint)
                {
                    if (DistanceBetweenPoints(((MyPoint) mo).Point, p) <= distance)
                    {
                        return ((MyPoint) mo).Point;
                    }
                }
            }

            return p;
        }
        #endregion

        #region Line
        private void EraseLine(Point startPoint, Point endPoint)
        {
            _wb.DrawLine(startPoint, endPoint, BackgroundColor, (int) Width);
        }
        #endregion

        #region Object
        private void ClosePolygon()
        {
            DrawingMode = false;

            ((MyPolygon) _temporaryObject).DrawAndAddLine(_wb, new MyLine(_lastPoint, _firstPoint),
                _temporaryObject.Color);

            Vector3D Ambient = new Vector3D(RAmbient, GAmbient, BAmbient);
            Vector3D Diffuse = new Vector3D(RDiffuse, GDiffuse, BDiffuse);
            Vector3D Specular = new Vector3D(RSpecular, GSpecular, BSpecular);

            ((MyPolygon)_temporaryObject).PhongMaterial = new PhongMaterial(Ambient, Diffuse, Specular, Shininess);

            AddObjectToList(_temporaryObject.Clone());
            ClearTemporaryObject();
        }

        private void RedrawObject(MyObject myObject)
        {
            if (ObjectColorPicker.SelectedColor != null)
            {
                if (myObject is MyPolygon)
                {
                    foreach (var item in ((MyPolygon) myObject).LinesList)
                    {
                        _wb.DrawLine(item.StartPoint, item.EndPoint, myObject.Color, myObject.Width);
                    }
                }
                else if (myObject is MyLine)
                {
                    _wb.DrawLine(((MyLine) myObject).StartPoint, ((MyLine) myObject).EndPoint, myObject.Color,
                        myObject.Width);
                }
                else if (myObject is MyPoint)
                {
                    _wb.DrawPoint(((MyPoint) myObject).Point, myObject.Color, myObject.Width);
                }
            }
        }

        private void AddObjectToList(MyObject mo)
        {
            if (!ObjectsList.Contains(mo))
            {
                ObjectsList.Add(mo);
            }

            OnPropertyChanged(nameof(ObjectsList));
            ClipWnd?.OnPropertyChanged();
        }

        public void RedrawAllObjects(WriteableBitmap wb)
        {
            foreach (MyObject item in ObjectsList)
            {
                if (item is MyPolygon)
                {
                    var mp = item as MyPolygon;

                    if (IlluminationEnabled)
                    {
                        mp.DrawObjectPhong(wb, _illuminationModel, BumpMappingEnabled);
                    }
                    else
                    {
                        mp.DrawObject(wb);
                    }
                }
                else
                {
                    item.DrawObject(wb);
                }
            }

            if (_illuminationModel != null && IlluminationEnabled)
            {
                foreach (var item in _illuminationModel.PhongLights)
                {
                    _wb.DrawPoint(new Point(item.Position.X, (int)item.Position.Y), Colors.Yellow, 5);
                }
            }
        }

        private void ClipPolygonWithRectangle()
        {
            if (_polygonToClip != null && _clipStartPoint != null && _clipEndPoint != null)
            {
                var rect = new MyRectangle(_clipStartPoint.Value, _clipEndPoint.Value);
                var polygon = _polygonToClip.LinesList.Select(x => x.StartPoint).ToArray();

                MyPolygon mp = new MyPolygon
                {
                    Color = _polygonToClip.Color,
                    FillColor = _polygonToClip.FillColor,
                    Width = _polygonToClip.Width,
                    FillBitmap = _polygonToClip.FillBitmap,
                    InitialBitmap = _polygonToClip.InitialBitmap
                };

                var intersected = PolygonClipping.GetIntersectedPolygon(polygon, rect.FourPointsList().ToArray());

                if (intersected.Count() > 0)
                {
                    for (var i = 0; i < intersected.Count(); i++)
                    {
                        mp.LinesList.Add(new MyLine(intersected[i], intersected[(i + 1) % intersected.Count()]));
                    }

                    ObjectsList.Remove(_polygonToClip);
                    _polygonToClip = null;
                    mp.UpdateBoundaries();
                    AddObjectToList(mp);
                }

            }

            _clipStartPoint = null;
            _clipEndPoint = null;
            ClipPolygonMode = false;

            ClearAndRedraw();
        }
        #endregion

        #region Static
        private static double DistanceBetweenPoints(Point a, Point b)
        {
            return Math.Sqrt((a.X - b.X)*(a.X - b.X) + (a.Y - b.Y)*(a.Y - b.Y));
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            if (propertyName == "ObjectsList" && ListWnd != null)
            {
                ListWnd.Objects.ItemsSource = ObjectsList;
            }
        }
        #endregion

        #region Phong Illumination Model
        private void EnableIlluminationCheckBox_Click(object sender, RoutedEventArgs e)
        {
            RefreshScene();
            ClearAndRedraw();
        }

        private void AddLightSource_Click(object sender, RoutedEventArgs e)
        {
            if (_illuminationModel != null)
            {
                _illuminationModel.PhongLights.Add(new PhongLight(new Vector3D(50, 50, LightsZ), Colors.White));
                ClearAndRedraw();
            }
        }

        private void RemoveSources_Click(object sender, RoutedEventArgs e)
        {
            if (_illuminationModel != null)
            {
                _illuminationModel.PhongLights.RemoveAll(x => true);
                ClearAndRedraw();
            }
        }

        private void RefreshScene_Click(object sender, RoutedEventArgs e)
        {
            RefreshScene();
        }

        private void EditCoeffs(object sender, RoutedEventArgs e)
        {
            
        }

        private void RefreshScene()
        {
            Vector3D Ambient = new Vector3D(RAmbient, GAmbient, BAmbient);
            Vector3D Diffuse = new Vector3D(RDiffuse, GDiffuse, BDiffuse);
            Vector3D Specular = new Vector3D(RSpecular, GSpecular, BSpecular);

            var newModel = new PhongIlluminationModel(ViewerZ);

            if (_illuminationModel != null)
            {
                newModel.PhongLights = _illuminationModel.PhongLights;
            }

            foreach (var light in newModel.PhongLights)
            {
                light.Position = new Vector3D(light.Position.X, light.Position.Y, LightsZ);
            }

            _illuminationModel = newModel;

            ClearAndRedraw();
        }

        public PhongMaterial GetNewMaterial()
        {
            Vector3D Ambient = new Vector3D(RAmbient, GAmbient, BAmbient);
            Vector3D Diffuse = new Vector3D(RDiffuse, GDiffuse, BDiffuse);
            Vector3D Specular = new Vector3D(RSpecular, GSpecular, BSpecular);

            return new PhongMaterial(Ambient, Diffuse, Specular, Shininess);
        }

        private void EditCoeffs_Click(object sender, RoutedEventArgs e)
        {
            EditCoeffsMode = !EditCoeffsMode;
        }
        #endregion
    }
}