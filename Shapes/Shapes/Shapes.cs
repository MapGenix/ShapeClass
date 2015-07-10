namespace Shapes
{
    using DotSpatial.Controls;
    using DotSpatial.Data;
    using DotSpatial.Symbology;
    using DotSpatial.Topology;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Linq;
    using System.Windows.Forms;

    public class Shapes : DotSpatial.Controls.MapFunction
    {
        private ContextMenu newInsideContext;
        private Coordinate actualCoord = new Coordinate();
        private List<Coordinate> XyCoord;
        private List<List<Coordinate>> lineSet;
        private IFeatureLayer setLayer;
        private IFeatureSet setFeature;
        private IMapLineLayer tempLayer;
        private System.Drawing.Point pointLocate;
        private bool onStandby;

        public Shapes()
        {
            Configure();
        }

        public Shapes(IMap map)
            : base(map)
        {
            Configure();
        }

        public IFeatureLayer Layer
        {
            get
            {
                return setLayer;
            }
            set
            {
                if (setLayer == value) return;
                setLayer = value;
                setFeature = setLayer != null ? setLayer.DataSet : null;
            }
        }

        private void Configure()
        {
            YieldStyle = (YieldStyles.LeftButton | YieldStyles.RightButton);
            newInsideContext = new ContextMenu();
            newInsideContext.MenuItems.Add("Delete", DeleteShape); ;
            newInsideContext.MenuItems.Add("Finish Shape", FinishShape);
            lineSet = new List<List<Coordinate>>();
        }

        public void DeleteShape(object sender, EventArgs e)
        {
            XyCoord = new List<Coordinate>();
            lineSet = new List<List<Coordinate>>();
            Map.Invalidate();
        }

        public void FinishPart(object sender, EventArgs e)
        {
            lineSet.Add(XyCoord);
            XyCoord = new List<Coordinate>();
            Map.Invalidate();
        }

        public void FinishShape(object sender, EventArgs e)
        {
            if (setFeature != null && !setFeature.IsDisposed)
            {
                Feature f = null;
                if (setFeature.FeatureType == FeatureType.MultiPoint)
                {
                    f = new Feature(new MultiPoint(XyCoord));
                }
                if (setFeature.FeatureType == FeatureType.Line || setFeature.FeatureType == FeatureType.Polygon)
                {
                    FinishPart(sender, e);
                    Shape shp = new Shape(setFeature.FeatureType);
                    foreach (List<Coordinate> part in lineSet)
                    {
                        if (part.Count >= 2)
                        {
                            shp.AddPart(part, setFeature.CoordinateType);
                        }
                    }
                    f = new Feature(shp);
                }
                if (f != null)
                {
                    setFeature.Features.Add(f);
                }
                setFeature.ShapeIndices = null; 
                setFeature.UpdateExtent();
                setLayer.AssignFastDrawnStates();
                setFeature.InvalidateVertices();
            }

            XyCoord = new List<Coordinate>();
            lineSet = new List<List<Coordinate>>();
        }

        protected override void OnActivate()
        {
            if (onStandby == false) { XyCoord = new List<Coordinate>(); }
            if (tempLayer != null)
            {
                Map.MapFrame.DrawingLayers.Remove(tempLayer);
                Map.MapFrame.Invalidate();
                Map.Invalidate();
                tempLayer = null;
            }
            onStandby = false;

            base.OnActivate();
        }

        protected override void OnMouseMove(GeoMouseArgs e)
        {
            if (onStandby) { return; }

            actualCoord = e.GeographicLocation;

            if (XyCoord != null && XyCoord.Count > 0)
            {
                List<System.Drawing.Point> points = XyCoord.Select(coord => Map.ProjToPixel(coord)).ToList();
                Rectangle oldRect = SymbologyGlobal.GetRectangle(pointLocate, points[points.Count - 1]);
                Rectangle newRect = SymbologyGlobal.GetRectangle(e.Location, points[points.Count - 1]);
                Rectangle invalid = Rectangle.Union(newRect, oldRect);
                invalid.Inflate(20, 20);
                Map.Invalidate(invalid);
            }

            pointLocate = e.Location;
            base.OnMouseMove(e);
        }

        protected override void OnDraw(MapDrawArgs e)
        {
            if (onStandby) { return; }

            if (setFeature.FeatureType == FeatureType.Point) { return; }

            if (lineSet != null)
            {
                GraphicsPath graphPath = new GraphicsPath();

                List<System.Drawing.Point> partPoints = new List<System.Drawing.Point>();
                foreach (List<Coordinate> part in lineSet)
                {
                    partPoints.AddRange(part.Select(c => Map.ProjToPixel(c)));
                    if (setFeature.FeatureType == FeatureType.Line)
                    {
                        graphPath.AddLines(partPoints.ToArray());
                    }
                    if (setFeature.FeatureType == FeatureType.Polygon)
                    {
                        graphPath.AddPolygon(partPoints.ToArray());
                    }
                    partPoints.Clear();
                }

                e.Graphics.DrawPath(Pens.Blue, graphPath);

                if (setFeature.FeatureType == FeatureType.Polygon)
                {
                    Brush brushColor = new SolidBrush(Color.Orange);
                    e.Graphics.FillPath(brushColor, graphPath);
                    brushColor.Dispose();
                }
            }

            Pen bluePen = new Pen(Color.Blue, 2F);
            Pen redPen = new Pen(Color.DarkGray, 2F);
            Brush redBrush = new SolidBrush(Color.Red);

            List<System.Drawing.Point> points = new List<System.Drawing.Point>();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (XyCoord != null)
            {
                points.AddRange(XyCoord.Select(coord => Map.ProjToPixel(coord)));
                foreach (System.Drawing.Point pt in points)
                {
                    e.Graphics.FillRectangle(redBrush, new Rectangle(pt.X - 2, pt.Y - 2, 4, 4));
                }
                if (points.Count > 1)
                {
                    if (setFeature.FeatureType != FeatureType.MultiPoint)
                    {
                        e.Graphics.DrawLines(bluePen, points.ToArray());
                    }
                }
                if (points.Count > 0 && onStandby == false)
                {
                    if (setFeature.FeatureType != FeatureType.MultiPoint)
                    {
                        e.Graphics.DrawLine(redPen, points[points.Count - 1], pointLocate);
                    }
                }
            }

            bluePen.Dispose();
            redPen.Dispose();
            redBrush.Dispose();
            base.OnDraw(e);
        }

        protected override void OnMouseUp(GeoMouseArgs e)
        {
            if (onStandby) { return; }
            if (setFeature == null || setFeature.IsDisposed) { return; }

            if (setFeature.FeatureType == FeatureType.Point)
            {
                Coordinate snappedCoord = actualCoord;
                DotSpatial.Topology.Point pt = new DotSpatial.Topology.Point(snappedCoord); 
                Feature f = new Feature(pt);

                setFeature.Features.Add(f);
                setFeature.ShapeIndices = null; 
                setFeature.UpdateExtent();
                setLayer.AssignFastDrawnStates();
                setFeature.InvalidateVertices();
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                newInsideContext.Show((Control)Map, e.Location);
            }
            else
            {
                if (XyCoord == null) { XyCoord = new List<Coordinate>(); }
                Coordinate snappedCoord = e.GeographicLocation;

                XyCoord.Add(snappedCoord); 
                if (XyCoord.Count > 1)
                {
                    System.Drawing.Point pointOne = Map.ProjToPixel(XyCoord[XyCoord.Count - 1]);
                    System.Drawing.Point pointTwo = Map.ProjToPixel(XyCoord[XyCoord.Count - 2]);
                    Rectangle invalid = SymbologyGlobal.GetRectangle(pointOne, pointTwo);
                    invalid.Inflate(20, 20);
                    Map.Invalidate(invalid);
                }
            }

            base.OnMouseUp(e);
        }
    }
}