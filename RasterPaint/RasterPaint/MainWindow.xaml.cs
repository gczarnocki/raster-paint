using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RasterPaint
{
    public partial class MainWindow
    {
        WriteableBitmap _wb;

        private bool _showGrid;
        private bool _removalMode;
        private bool _drawingMode;
        private bool _moveObjectMode;
        private bool _editObjectMode; // tryby aplikacji;

        public int GridCellValue { get; set; }
        public int LineWidthValue { get; set; }

        private Point _lastPoint;
        private Point _firstPoint;
        private Point _movePoint;
        private Point _lastMovePoint;

        private MyObject _temporaryObject;
        private MyObject _objectToMove;

        public List<MyObject> ObjectsList { get; }

        public Brush ButtonBrush { get; set; } = Brushes.LightGray;
        public Brush EnabledBrush { get; set; } = Brushes.LightGreen;
        public Color WhiteColor { get; set; } = Colors.White;

        // właściwości:

        public bool ShowGrid
        {
            get
            {
                return _showGrid;
            }

            set
            {
                _showGrid = value;
                GridSize.IsEnabled = !value;
                GridColor.IsEnabled = !value;
            }
        }

        public bool RemovalMode
        {
            get
            {
                return _removalMode;
            }

            set
            {
                _removalMode = value;
                RemoveButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }

        public bool MoveObjectMode
        {
            get
            {
                return _moveObjectMode;
            }

            set
            {
                RemovalMode = false;
                _moveObjectMode = value;
                MoveButton.Background = value ? EnabledBrush : ButtonBrush;

                if (ShowGrid)
                {
                    DrawGrid();
                    MessageBox.Show("Przy włączonej siatce, pamiętaj o wyłączeniu trybu \"Move\", by przerysować siatkę!");
                }
            }
        }

        public bool EditObjectMode
        {
            get
            {
                return _editObjectMode;
            }

            set
            {
                _editObjectMode = value;
                EditButton.Background = value ? EnabledBrush : ButtonBrush;
            }
        }

        public bool DrawingMode
        {
            get
            {
                return _drawingMode;
            }

            set
            {
                _drawingMode = value;

                if (ObjectColor != null)
                {
                    ObjectColor.IsEnabled = !value;
                    LineWidth.IsEnabled = !value;
                }
            }
        }

        public bool DrawingPolygon { get; set; }

        public bool DrawingLine { get; set; }

        public bool DrawingPoint { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            ObjectsList = new List<MyObject>();
            RemovalMode = false;
            ShowGrid = false;
        }

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

        private void MyImage_ButtonDown(object sender, MouseButtonEventArgs e) // kliknięcie na bitmapę;
        {
            Point p = e.GetPosition(MyImage);
            MyObject myObject = null;
            bool removeNow = false;

            if (!RemovalMode && !MoveObjectMode && !DrawingMode) // zaczynamy rysować;
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

                if (ObjectColor.SelectedColor != null)
                {
                    _temporaryObject.Color = ObjectColor.SelectedColor.Value;
                }

                _firstPoint = _lastPoint = p;
            }
            else if (RemovalMode) // tryb usuwania;
            {
                foreach (MyObject mo in ObjectsList)
                {
                    if (mo.MyBoundary.Contains(p) || (mo is MyPoint && DistanceBetweenPoints(p, ((MyPoint)mo).Point) <= 15.0F))
                    {
                        myObject = mo;

                        mo.HighlightObject(true, _wb);

                        if (MessageBox.Show("Czy chcesz usunąć podświetlony obiekt?", "Usuwanie obiektu", MessageBoxButton.OKCancel)
                            == MessageBoxResult.OK)
                        {
                            removeNow = true;
                            break; // wiemy, że mamy usunąć obiekt;
                        }
                    }

                    mo.HighlightObject(false, _wb); // wyczyść podświetlenie;
                    RemovalMode = true;
                }
            }
            else if (MoveObjectMode)
            {
                _movePoint = e.GetPosition(MyImage);

                foreach (var mo in ObjectsList)
                {
                    if (mo.MyBoundary.Contains(_movePoint))
                    {
                        myObject = mo;
                        mo.HighlightObject(true, _wb); // podświetlenie obiektu;
                        break;
                    }
                }

                _objectToMove = myObject;
            }

            if (removeNow && myObject != null)
            {
                myObject.EraseObject(ObjectsList, _wb);

                if (ShowGrid)
                {
                    DrawGrid();
                }

                RedrawAllObjects(_wb);

                removeNow = false;
            }
        }

        private void MyImage_ButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(MyImage);

            if (DrawingMode && !RemovalMode && !MoveObjectMode && !EditObjectMode)
            {
                if (DrawingPolygon)
                {
                    Point snappedPoint = SnapTemporaryPoint(point, 15); // "snap", 15px;

                    if (snappedPoint.Equals(_firstPoint) && ((MyPolygon)_temporaryObject).LinesList.Count > 1)
                    {
                        ClosePolygon();
                    }
                    else
                    {
                        if (ObjectColor.SelectedColor != null)
                        {
                            ((MyPolygon)_temporaryObject).DrawAndAddLine(_wb, new MyLine(_lastPoint, point), _temporaryObject.Color);
                        }
                    }

                    _lastPoint = point;
                }
                else if (DrawingLine)
                {
                    if (ObjectColor.SelectedColor != null)
                    {
                        ((MyLine)_temporaryObject).DrawAndAddLine(_wb, new MyLine(_lastPoint, point), _temporaryObject.Color);
                    }

                    AddObjectToList(_temporaryObject.Clone());
                    ClearTemporaryObject();

                    DrawingMode = false;
                }
                else if (DrawingPoint)
                {
                    Color color = _temporaryObject.Color;

                    if (ObjectColor.SelectedColor != null)
                    {
                        ((MyPoint)_temporaryObject).DrawAndAdd(_wb, _lastPoint, _temporaryObject.Color, _temporaryObject.Width);
                    }

                    AddObjectToList(_temporaryObject.Clone());
                    ClearTemporaryObject();

                    DrawingMode = false;
                }
            }
            else if (MoveObjectMode)
            {
                Point p = e.GetPosition(MyImage);
                Vector v = new Vector(p.X - _movePoint.X, p.Y - _movePoint.Y);

                if (_objectToMove != null && ObjectsList.Contains(_objectToMove))
                {
                    MyObject newObject = _objectToMove.MoveObject(v);
                    newObject.UpdateBoundaries();

                    _objectToMove.EraseObject(ObjectsList, _wb);
                    AddObjectToList(newObject);
                }

                RedrawAllObjects(_wb);
            }
        }

        private void MyImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (DrawingPolygon && _temporaryObject != null)
            {
                if (((MyPolygon)_temporaryObject).LinesList.Count > 1)
                {
                    Point point = _firstPoint;

                    ClosePolygon();

                    _lastPoint = point;
                }
                else
                {
                    foreach (var item in ((MyPolygon)_temporaryObject).LinesList)
                    {
                        BitmapExtensions.DrawLine(_wb, item.StartPoint, item.EndPoint, Colors.White, item.Width);
                    }

                    if (DrawingMode)
                    {
                        BitmapExtensions.DrawLine(_wb, _lastPoint, _lastMovePoint, Colors.White, LineWidthValue);
                    }

                    DrawingMode = false;
                    ClearTemporaryObject();
                }
            }

            DrawingMode = false;
        }

        private void ImageGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _wb = new WriteableBitmap((int)e.NewSize.Width, (int)e.NewSize.Height, 96, 96, PixelFormats.Bgra32, null);
            MyImage.Source = _wb;

            DrawGrid();
            RedrawAllObjects(_wb);
        }

        private void GridSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            GridCellValue = (int)e.NewValue;
        }

        private void LineWidth_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            LineWidthValue = (int)e.NewValue;
        }

        private void DrawGrid()
        {
            if (GridColor.SelectedColor == null) return;

            var color = ShowGrid ? GridColor.SelectedColor.Value : WhiteColor; // wybór koloru;

            for (int i = 0; i <= Math.Max(ImageGrid.ActualWidth, ImageGrid.ActualHeight); i += GridCellValue)
            {
                BitmapExtensions.DrawGridLine(_wb, new Point(i, 0), new Point(i, ImageGrid.ActualWidth), color);
                BitmapExtensions.DrawGridLine(_wb, new Point(0, i), new Point(ImageGrid.ActualWidth, i), color); // narysowanie siatki;
            }
        }

        private void ClosePolygon()
        {
            DrawingMode = false;

            if (ObjectColor.SelectedColor != null && ObjectColor.SelectedColor != null)
            {
                ((MyPolygon)_temporaryObject).DrawAndAddLine(_wb, new MyLine(_lastPoint, _firstPoint), _temporaryObject.Color);
            }

            AddObjectToList(_temporaryObject.Clone());
            ClearTemporaryObject();
        }

        private void ClearTemporaryObject()
        {
            if (_temporaryObject is MyPolygon)
            {
                ((MyPolygon)_temporaryObject).LinesList.RemoveAll(x => true);
            }
            else if (_temporaryObject is MyLine)
            {
                ((MyLine)_temporaryObject).StartPoint = ((MyLine)_temporaryObject).EndPoint = new Point(0, 0);
            }
            else if (_temporaryObject is MyPoint)
            {
                ((MyPoint)_temporaryObject).Point = new Point(0, 0);
            }

            _temporaryObject.Color = Colors.Transparent;
            _temporaryObject.Width = 0;
        }

        private void AddObjectToList(MyObject mo)
        {
            if (!ObjectsList.Contains(mo))
            {
                ObjectsList.Add(mo);
            }
        }

        private Point SnapTemporaryPoint(Point p, int distance)
        {
            if (distance > 0)
            {
                if (_temporaryObject is MyPolygon)
                {
                    foreach (var item in ((MyPolygon)_temporaryObject).LinesList)
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
                else if (_temporaryObject is MyLine)
                {
                    if (DistanceBetweenPoints(p, ((MyLine)_temporaryObject).StartPoint) <= distance)
                    {
                        return ((MyLine)_temporaryObject).StartPoint;
                    }

                    if (DistanceBetweenPoints(p, ((MyLine)_temporaryObject).EndPoint) <= distance)
                    {
                        return ((MyLine)_temporaryObject).EndPoint;
                    }
                }
                else if (_temporaryObject is MyPoint)
                {
                    if (DistanceBetweenPoints(((MyPoint)_temporaryObject).Point, p) <= distance)
                    {
                        return ((MyPoint)_temporaryObject).Point;
                    }
                }
            }

            return p;
        }

        private double DistanceBetweenPoints(Point a, Point b)
        {
            return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        private void RedrawAllObjects(WriteableBitmap wb)
        {
            foreach (MyObject item in ObjectsList)
            {
                item.DrawObject(wb);
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpWindow hw = new HelpWindow();
            hw.ShowDialog();
        }

        private void EraseLine(Point startPoint, Point endPoint)
        {
            BitmapExtensions.DrawLine(_wb, startPoint, endPoint, WhiteColor, (int)Width);
        }

        private void MyImage_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && DrawingMode && !DrawingPoint && !ShowGrid)
            {
                Point p = e.GetPosition(MyImage);

                EraseLine(_lastPoint, _lastMovePoint);

                // DrawGrid(); // performance!
                RedrawObject(_temporaryObject);
                RedrawAllObjects(_wb);

                if (ObjectColor.SelectedColor != null)
                {
                    BitmapExtensions.DrawLine(_wb, _lastPoint, p, ObjectColor.SelectedColor.Value, LineWidthValue);
                }

                _lastMovePoint = p;
            }
        }

        private void RedrawObject(MyObject myObject)
        {
            if (ObjectColor.SelectedColor != null)
            {
                if (myObject is MyPolygon)
                {
                    foreach (var item in ((MyPolygon)myObject).LinesList)
                    {
                        BitmapExtensions.DrawLine(_wb, item.StartPoint, item.EndPoint, myObject.Color, myObject.Width);
                    }
                }
                else if (myObject is MyLine)
                {
                    BitmapExtensions.DrawLine(_wb, ((MyLine)myObject).StartPoint, ((MyLine)myObject).EndPoint, myObject.Color, myObject.Width);
                }
                else if (myObject is MyPoint)
                {
                    BitmapExtensions.DrawPoint(_wb, ((MyPoint)myObject).Point, myObject.Color, myObject.Width);
                }
            }
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

        private void ListButton_Click(object sender, RoutedEventArgs e)
        {
            ListWindow lw = new ListWindow(ObjectsList);
            lw.Show();
        }
    }
}