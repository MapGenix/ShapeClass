namespace ShapeEditor
{
    using DotSpatial.Controls;
    using DotSpatial.Data;
    using DotSpatial.Symbology;
    using DotSpatial.Topology;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Windows.Forms;

    public class ShapeEditor : DotSpatial.Controls.MapFunction
    {
        private System.Drawing.Point actualCoord;
        private Rectangle imgRect;
        private Coordinate actVertex;
        private Coordinate dragCoord;
        private Coordinate closedCoord;
        private Coordinate prevPoint;
        private Coordinate nextPoint;
        private IFeatureLayer setLayer;
        private IFeatureSet setFeature;
        private IFeature selectFeat;
        private IFeature actFeat;
        private IFeatureCategory actCategory;
        private IFeatureCategory selectCategory;
        private IFeatureCategory oldCategory;
        private bool drag;

        public ShapeEditor()
        {
            Configure();
        }

        public ShapeEditor(IMap map)
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
                setLayer = value;
                setFeature = setLayer.DataSet;
            }
        }

        private void Configure()
        {
            YieldStyle = (YieldStyles.LeftButton | YieldStyles.RightButton);
        }

        protected override void OnMouseMove(GeoMouseArgs e)
        {
            actualCoord = e.Location;
            if (drag)
            {
                actualCoord = Map.ProjToPixel(e.GeographicLocation);
                UpdateDragCoordiante(e.GeographicLocation);
            }
            else
            {
                if (selectFeat != null)
                {
                    VertexHighlight();
                }
                else
                {
                    bool notRequired = false;
                    if (actFeat != null)
                    {
                        if (ShapeRemoveHighlight(e)) { notRequired = true; }
                    }
                    if (actFeat == null)
                    {
                        if (ShapeHighlight(e)) { notRequired = true; }
                    }

                    if (notRequired)
                    {
                        Map.MapFrame.Initialize();
                        Map.Invalidate();
                    }
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnDraw(MapDrawArgs e)
        {
            Rectangle mouseHoverRectangle = new Rectangle(actualCoord.X - 3, actualCoord.Y - 3, 5, 5);

            if (selectFeat != null)
            {
                foreach (Coordinate c in selectFeat.Coordinates)
                {
                    System.Drawing.Point pointOne = Map.ProjToPixel(c);
                    Coordinate xyLinePoint = Map.PixelToProj(pointOne);

                    if (e.GeoGraphics.ImageRectangle.Contains(pointOne))
                    {
                        System.Drawing.Point pointTwo = e.GeoGraphics.ProjToPixel(xyLinePoint);

                        e.Graphics.FillRectangle(Brushes.Blue, pointOne.X - 2, pointOne.Y - 2, 6, 6);
                    }
                    if (mouseHoverRectangle.Contains(pointOne))
                    {
                        e.Graphics.FillRectangle(Brushes.Red, mouseHoverRectangle);
                    }
                }
            }

            if (drag)
            {
                if (setFeature.FeatureType == FeatureType.Point || setFeature.FeatureType == FeatureType.MultiPoint)
                {
                    Rectangle rectangleSym = new Rectangle(actualCoord.X - (imgRect.Width / 2), actualCoord.Y - (imgRect.Height / 2), imgRect.Width, imgRect.Height);
                    selectCategory.Symbolizer.Draw(e.Graphics, rectangleSym);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.DarkRed, actualCoord.X - 3, actualCoord.Y - 3, 6, 6);
                    System.Drawing.Point b = actualCoord;

                    //change

                    Pen p = new Pen(Color.DarkRed) { DashStyle = DashStyle.DashDotDot };

                    if (prevPoint != null)
                    {
                        System.Drawing.Point a = Map.ProjToPixel(prevPoint);
                        e.Graphics.DrawLine(p, a, b);
                    }
                    if (nextPoint != null)
                    {
                        System.Drawing.Point c = Map.ProjToPixel(nextPoint);
                        e.Graphics.DrawLine(p, b, c);
                    }
                    p.Dispose();
                }
            }
        }

        protected override void OnMouseUp(GeoMouseArgs e)
        {
            if (e.Button == MouseButtons.Left && drag)
            {
                drag = false;
                Map.IsBusy = false;

                setFeature.InvalidateVertices();

                if (setFeature.FeatureType == FeatureType.Point || setFeature.FeatureType == FeatureType.MultiPoint)
                {
                    if (actFeat == null)
                    {
                        return;
                    }

                    if (setLayer.GetCategory(actFeat) != selectCategory)
                    {
                        setLayer.SetCategory(actFeat, selectCategory);
                        setLayer.SetVisible(actFeat, true);
                    }
                }
                else
                {
                    if (selectFeat == null) { return; }
                    if (setLayer.GetCategory(selectFeat) != selectCategory)
                    {
                        setLayer.SetCategory(selectFeat, selectCategory);
                    }
                }
            }
            Map.MapFrame.Initialize();
        }

        protected override void OnMouseDown(GeoMouseArgs e)
        {
            actualCoord = e.Location;
            if (drag)
            {
                if (e.Button == MouseButtons.Right)
                {
                    drag = false;
                    Map.Invalidate();
                    Map.IsBusy = false;
                }
            }
            else
            {
                if (selectFeat != null)
                {
                    Rectangle mouseHoverRectangle = new Rectangle(actualCoord.X - 3, actualCoord.Y - 3, 6, 6);

                    IEnvelope env = Map.PixelToProj(mouseHoverRectangle).ToEnvelope();

                    if (CheckForVertexDrag(e)) { return; }

                    if (!selectFeat.Intersects(env.ToPolygon()))
                    {
                        DeselectFeature();
                        return;
                    }
                }

                if (actFeat != null)
                {
                    if (setFeature.FeatureType == FeatureType.Polygon)
                    {
                        selectFeat = actFeat;
                        actFeat = null;

                        if (selectCategory == null)
                        {
                            selectCategory = new PolygonCategory(Color.FromArgb(55, 0, 255, 255), Color.Blue, 1)
                            {
                                LegendItemVisible = false
                            };
                        }

                        setLayer.SetCategory(selectFeat, selectCategory);
                    }
                    else if (setFeature.FeatureType == FeatureType.Line)
                    {
                        selectFeat = actFeat;
                        actFeat = null;

                        if (selectCategory == null)
                        {
                            selectCategory = new LineCategory(Color.Cyan, 1) { LegendItemVisible = false };
                        }

                        setLayer.SetCategory(selectFeat, selectCategory);
                    }
                    else
                    {
                        drag = true;
                        Map.IsBusy = true;
                        dragCoord = actFeat.Coordinates[0];

                        MapPointLayer mPntLayer = setLayer as MapPointLayer;

                        if (mPntLayer != null)
                        {
                            mPntLayer.SetVisible(actFeat, false);
                        }

                        if (selectCategory == null)
                        {
                            IPointSymbolizer pointSym = setLayer.GetCategory(actFeat).Symbolizer as IPointSymbolizer;
                            pointSym.SetFillColor(Color.Cyan);
                            selectCategory = new PointCategory(pointSym);
                        }
                    }
                }
                Map.MapFrame.Initialize();
                Map.Invalidate();
            }
        }

        private bool ShapeHighlight(GeoMouseArgs e)
        {
            if (e == null)
                throw new ArgumentNullException("e", "e is null.");

            Rectangle mouseHoverRectangle = new Rectangle(actualCoord.X - 3, actualCoord.Y - 3, 6, 6);
            Extent extPolygon = Map.PixelToProj(mouseHoverRectangle);
            IPolygon envPolygon = extPolygon.ToEnvelope().ToPolygon();

            bool notRequired = false;

            foreach (IFeature feature in setFeature.Features)
            {
                if (setFeature.FeatureType == FeatureType.Point || setFeature.FeatureType == FeatureType.MultiPoint)
                {
                    MapPointLayer mPntLayer = setLayer as MapPointLayer;

                    if (mPntLayer != null)
                    {
                        int w = 3;
                        int h = 3;

                        PointCategory pntCategory = mPntLayer.GetCategory(feature) as PointCategory;

                        if (pntCategory != null)
                        {
                            if (pntCategory.Symbolizer.ScaleMode != ScaleMode.Geographic)
                            {
                                Size2D size = pntCategory.Symbolizer.GetSize();
                                w = (int)size.Width;
                                h = (int)size.Height;
                            }
                        }

                        imgRect = new Rectangle(e.Location.X - (w / 2), e.Location.Y - (h / 2), w, h);

                        if (imgRect.Contains(Map.ProjToPixel(feature.Coordinates[0])))
                        {
                            actFeat = feature;
                            oldCategory = mPntLayer.GetCategory(feature);
                            if (selectCategory == null)
                            {
                                selectCategory = oldCategory;
                                selectCategory.SetColor(Color.Red);
                                selectCategory.LegendItemVisible = false;
                            }
                            mPntLayer.SetCategory(actFeat, selectCategory);
                        }
                    }
                    notRequired = true;
                }
                else
                {
                    if (feature.Intersects(envPolygon))
                    {
                        actFeat = feature;
                        oldCategory = setLayer.GetCategory(actFeat);

                        if (setFeature.FeatureType == FeatureType.Polygon)
                        {
                            IPolygonCategory polygonCat = actCategory as IPolygonCategory;
                            if (polygonCat == null)
                            {
                                actCategory = new PolygonCategory(Color.FromArgb(55, 145, 14, 178), Color.Purple, 1) { LegendItemVisible = false };
                            }
                        }
                        if (setFeature.FeatureType == FeatureType.Line)
                        {
                            ILineCategory lineCat = actCategory as ILineCategory;
                            if (lineCat == null)
                            {
                                actCategory = new LineCategory(Color.Red, 3) { LegendItemVisible = false };
                            }
                        }
                        setLayer.SetCategory(actFeat, actCategory);
                        notRequired = true;
                    }
                }
            }
            return notRequired;
        }

        private bool ShapeRemoveHighlight(GeoMouseArgs e)
        {
            if (oldCategory == null)
            {
                return false;
            }

            Rectangle mouseHoverRectangle = new Rectangle(actualCoord.X - 3, actualCoord.Y - 3, 6, 6);
            Extent extPolygon = Map.PixelToProj(mouseHoverRectangle);

            bool notRequired = false;

            if (!actFeat.Intersects(extPolygon.ToEnvelope().ToPolygon()))
            {
                setLayer.SetCategory(actFeat, oldCategory);
                actFeat = null;
                notRequired = true;
            }
            return notRequired;
        }

        private void UpdateDragCoordiante(Coordinate loc)
        {
            dragCoord.X = loc.X;
            dragCoord.Y = loc.Y;

            if (closedCoord != null)
            {
                closedCoord.X = loc.X;
                closedCoord.Y = loc.Y;
            }
            Map.Invalidate();
        }

        private void VertexHighlight()
        {
            Rectangle mouseHoverRectangle = new Rectangle(actualCoord.X - 3, actualCoord.Y - 3, 7, 7);
            Extent extPolygon = Map.PixelToProj(mouseHoverRectangle);

            if (!(actVertex == null))
            {
                if (!extPolygon.Contains(actVertex))
                {
                    actVertex = null;
                    Map.Invalidate();
                }
            }
            foreach (Coordinate coord in selectFeat.Coordinates)
            {
                if (extPolygon.Contains(coord))
                {
                    actVertex = coord;
                    Map.Invalidate();
                }
            }
        }

        private bool CheckForVertexDrag(GeoMouseArgs e)
        {
            Rectangle mouseHoverRectangle = new Rectangle(actualCoord.X - 3, actualCoord.Y - 3, 6, 6);
            IEnvelope env = Map.PixelToProj(mouseHoverRectangle).ToEnvelope();

            if (e.Button == MouseButtons.Left)
            {
                if (setLayer.DataSet.FeatureType == FeatureType.Polygon)
                {
                    for (int prt = 0; prt < selectFeat.NumGeometries; prt++)
                    {
                        IBasicGeometry g = selectFeat.GetBasicGeometryN(prt);
                        IList<Coordinate> coords = g.Coordinates;

                        for (int ic = 0; ic < coords.Count; ic++)
                        {
                            Coordinate c = coords[ic];
                            if (env.Contains(c))
                            {
                                drag = true;
                                dragCoord = c;

                                if (ic == 0)
                                {
                                    closedCoord = coords[coords.Count - 1];
                                    prevPoint = coords[coords.Count - 2];
                                    nextPoint = coords[1];
                                }
                                else if (ic == coords.Count - 1)
                                {
                                    closedCoord = coords[0];
                                    prevPoint = coords[coords.Count - 2];
                                    nextPoint = coords[1];
                                }
                                else
                                {
                                    prevPoint = coords[ic - 1];
                                    nextPoint = coords[ic + 1];
                                    closedCoord = null;
                                }
                                Map.Invalidate();
                                return true;
                            }
                        }
                    }
                }
                else if (setLayer.DataSet.FeatureType == FeatureType.Line)
                {
                    for (int prt = 0; prt < selectFeat.NumGeometries; prt++)
                    {
                        IBasicGeometry g = selectFeat.GetBasicGeometryN(prt);
                        IList<Coordinate> coords = g.Coordinates;
                        for (int ic = 0; ic < coords.Count; ic++)
                        {
                            Coordinate c = coords[ic];
                            if (env.Contains(c))
                            {
                                drag = true;
                                dragCoord = c;

                                if (ic == 0)
                                {
                                    prevPoint = null;
                                    nextPoint = coords[1];
                                }
                                else if (ic == coords.Count - 1)
                                {
                                    prevPoint = coords[coords.Count - 2];
                                    nextPoint = null;
                                }
                                else
                                {
                                    prevPoint = coords[ic - 1];
                                    nextPoint = coords[ic + 1];
                                }
                                Map.Invalidate();
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        protected override void OnDeactivate()
        {
            DeselectFeature();
            RemoveHighlightFromFeature();
            oldCategory = null;
            base.OnDeactivate();
        }

        public void RemoveHighlightFromFeature()
        {
            if (actFeat != null)
            {
                setLayer.SetCategory(actFeat, oldCategory);
            }
            actFeat = null;
        }

        public void DeselectFeature()
        {
            if (selectFeat != null)
            {
                setLayer.SetCategory(selectFeat, oldCategory);
            }

            selectFeat = null;
            Map.MapFrame.Initialize();
            Map.Invalidate();
        }
    }
}