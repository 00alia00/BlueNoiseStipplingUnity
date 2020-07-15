/* 
	================================================================================
	Copyright (c) 2012, Jose Esteve. http://www.joesfer.com
	This software is released under the LGPL-3.0 license: http://www.opensource.org/licenses/lgpl-3.0.html	
	================================================================================

    Changes to support point lists by Alia McCutcheon 2020
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BlueNoise
{
    [Serializable]
    public struct Size
    {
        public int Width;
        public int Height;

        public Size(int v1, int v2) : this()
        {
            this.Width = v1;
            this.Height = v2;
        }
    }

    [Serializable]
    public struct Rectangle
    {
        public int x, y;

        public int width;
        public int height;

        public Rectangle(int v1, int v2, int width, int height) : this()
        {
            x = v1;
            y = v2;

            this.width = width;
            this.height = height;
        }

        public int Right { get { return x + width; } }
        public int Left { get { return x; } }
        public int Top { get { return y; } }
        public int Bottom { get { return y + height; } }
    }

    [Serializable]
    public struct Point
    {
        public int x, y;
    }

    [Serializable]
    public class PointSet
    {
        List<Point> points = new List<Point>();

        int width;
        int height;

        public PointSet(PointSet img)
        {
            this.width = img.width;
            this.height = img.height;

            points = new List<Point>(img.points);
        }

        public PointSet(int width, int height)
        {
            this.width = width;
            this.height = height;

            points = new List<Point>();
        }

        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public List<Point> Values { get { return points; } }

        internal bool HasPoint(int x, int y)
        {
            return points.FindIndex((p) => p.x == x && p.y == y) == -1;
        }

        internal void SetPoint(int x, int y)
        {
            points.Add(new Point() { x = x, y = y });
        }

        internal float[,] ToDensity()
        {
            float[,] res = new float[Width, Height];

            foreach (var p in points)
            {
                res[p.x, p.y] = 1.0f;
            }

            return res;
        }
    }

    class ImageTreeNode
    {
        public ImageTreeNode() { children = null; tile = -1; tileDensity = 1.0f; }
        public ImageTreeNode[,] children;
        public int tile;
        public float tileDensity;
    }

    /// <summary>
    /// Stipple using Wang Tiles
    /// </summary>
    class WTStipple
    {
        public WTStipple(float density, int random_start_tile_index, WangTileSet tileSet, int tonalRange, PointSet dest)
        {
            this.tileSet = tileSet;

            // Subdivide image recusively as long as we need more density on each leave's tile
            // to account for the underlying sampled region on the image
            ImageTreeNode root = new ImageTreeNode();
            root.tile = random_start_tile_index % tileSet.tiles.Count;
            Rectangle rect = new Rectangle(0, 0, dest.Width, dest.Height);
            int minSize = 8;

            Refine_r(root, rect, density, minSize, 0, 5, tonalRange, dest);
        }

        public WTStipple(PointSet source, int random_start_tile_index, WangTileSet tileSet, int tonalRange, PointSet dest)
        {
            // Convert source image to grayscale
            float[,] grayscale = source.ToDensity();

            this.tileSet = tileSet;

            // Subdivide image recusively as long as we need more density on each leave's tile
            // to account for the underlying sampled region on the image
            ImageTreeNode root = new ImageTreeNode();
            root.tile = random_start_tile_index % tileSet.tiles.Count;
            Rectangle rect = new Rectangle(0, 0, source.Width, source.Height);
            int minSize = 8;

            Refine_r(root, rect, grayscale, minSize, 0, 5, tonalRange, dest);
        }

        private static float AreaDensity(float[,] img, Rectangle rect)
        {
            float sum = 0;
            for (int j = rect.y; j < rect.y + rect.height; j++)
            {
                for (int i = rect.x; i < rect.x + rect.width; i++)
                {
                    sum += img[i, j];
                }
            }
            return sum;
        }

        private static float DiskDensity(float[,] img, Point center, int radius)
        {
            float sum = 0;
            for (int j = Math.Max(0, center.y - radius); j < Math.Min(img.GetLength(1), center.y + radius); j++)
            {
                for (int i = Math.Max(0, center.x - radius); i < Math.Min(img.GetLength(0), center.x + radius); i++)
                {
                    int d2 = (i - center.x) * (i - center.x) + (j - center.y) * (j - center.y);
                    if (d2 > radius * radius) continue;
                    sum += img[i, j];
                }
            }
            return sum;
        }

        private static float DiskDensity(PointSet img, Point center, int radius)
        {
            float sum = 0;
            for (int j = Math.Max(0, center.y - radius); j < Math.Min(img.Height, center.y + radius); j++)
            {
                for (int i = Math.Max(0, center.x - radius); i < Math.Min(img.Width, center.x + radius); i++)
                {
                    int d2 = (i - center.x) * (i - center.x) + (j - center.y) * (j - center.y);
                    if (d2 > radius * radius) continue;
                    sum += 1.0f - (img.HasPoint(i, j) ? 0 : 1);
                }
            }
            return sum;
        }

        private void Refine_r(ImageTreeNode node, Rectangle rect, float[,] density, int minSize, int depth, int maxDepth, int toneScale, PointSet dest)
        {
            Debug.Assert(node.tile != -1);
            List<PoissonDist.PoissonSample> distribution = tileSet.tiles[node.tile].distribution;
            float tileMaxDensity = (float)distribution.Count;
            float requiredDensity = AreaDensity(density, rect);

            // Cover the area with the current tile

            float tileAvgDensity = Math.Min(1.0f, tileMaxDensity / (rect.width * rect.height));

            for (int i = 0; i < distribution.Count; i++)
            {
                int stippleX = rect.Left + (int)(rect.width * distribution[i].x);
                int stippleY = rect.Top + (int)(rect.height * distribution[i].y);
                float r = 1;//Math.Max(1, (distribution[i].radius * rect.Width));
                float area = (float)(r * r * Math.PI);
                float diskDensity = DiskDensity(density, new Point() { x = stippleX, y = stippleY }, (int)r);
                float diskAvgDensity = diskDensity / area;

                float factor = (float)(0.1f / Math.Pow(1, -2) * Math.Pow(4, 2.0f * depth) / toneScale);
                if (diskAvgDensity < (float)i * factor) continue;
                dest.SetPoint(stippleX, stippleY);
            }

            // Check whether we need to keep subdividing

            if (rect.width <= minSize || rect.height <= minSize || depth == maxDepth) return;

            if (Math.Pow(0.1, -2) / Math.Pow(4, 2 * depth) * toneScale - tileMaxDensity > 16 * tileMaxDensity)
            {
                // we need to subdivide
                int[,] subd;
                int splitsPerDimension;
                tileSet.GetSubdivisions(node.tile, out subd, out splitsPerDimension);
                node.children = new ImageTreeNode[splitsPerDimension, splitsPerDimension];
                Size childRectSize = new Size((int)Math.Floor((float)rect.width / splitsPerDimension),
                                              (int)Math.Floor((float)rect.height / splitsPerDimension));
                for (int j = 0; j < splitsPerDimension; j++)
                {
                    for (int i = 0; i < splitsPerDimension; i++)
                    {
                        node.children[i, j] = new ImageTreeNode();
                        node.children[i, j].tile = subd[i, j];
                        Rectangle childRect = new Rectangle(rect.x + i * childRectSize.Width,
                                                            rect.y + j * childRectSize.Height,
                                                            childRectSize.Width,
                                                            childRectSize.Height);
                        if (i == splitsPerDimension - 1) // adjust borders
                            childRect.width = rect.Right - childRect.x;
                        if (j == splitsPerDimension - 1) // adjust borders
                            childRect.height = rect.Bottom - childRect.y;
                        Refine_r(node.children[i, j], childRect, density, minSize, depth + 1, maxDepth, toneScale, dest);
                    }
                }
            }
        }
        private void Refine_r(ImageTreeNode node, Rectangle rect, float density, int minSize, int depth, int maxDepth, int toneScale, PointSet dest)
        {
            Debug.Assert(node.tile != -1);
            List<PoissonDist.PoissonSample> distribution = tileSet.tiles[node.tile].distribution;
            float tileMaxDensity = (float)distribution.Count;
            float requiredDensity = density;

            // Cover the area with the current tile

            float tileAvgDensity = Math.Min(1.0f, tileMaxDensity / (rect.width * rect.height));

            for (int i = 0; i < distribution.Count; i++)
            {
                int stippleX = rect.Left + (int)(rect.width * distribution[i].x);
                int stippleY = rect.Top + (int)(rect.height * distribution[i].y);
                float r = 1;//Math.Max(1, (distribution[i].radius * rect.Width));
                float area = (float)(r * r * Math.PI);
                float diskDensity = density;
                float diskAvgDensity = diskDensity / area;

                float factor = (float)(0.1f / Math.Pow(1, -2) * Math.Pow(4, 2.0f * depth) / toneScale);
                if (diskAvgDensity < (float)i * factor) continue;
                dest.SetPoint(stippleX, stippleY);
            }

            // Check whether we need to keep subdividing

            if (rect.width <= minSize || rect.height <= minSize || depth == maxDepth) return;

            if (Math.Pow(0.1, -2) / Math.Pow(4, 2 * depth) * toneScale - tileMaxDensity > 16 * tileMaxDensity)
            {
                // we need to subdivide
                int[,] subd;
                int splitsPerDimension;
                tileSet.GetSubdivisions(node.tile, out subd, out splitsPerDimension);
                node.children = new ImageTreeNode[splitsPerDimension, splitsPerDimension];
                Size childRectSize = new Size((int)Math.Floor((float)rect.width / splitsPerDimension),
                                              (int)Math.Floor((float)rect.height / splitsPerDimension));
                for (int j = 0; j < splitsPerDimension; j++)
                {
                    for (int i = 0; i < splitsPerDimension; i++)
                    {
                        node.children[i, j] = new ImageTreeNode();
                        node.children[i, j].tile = subd[i, j];
                        Rectangle childRect = new Rectangle(rect.x + i * childRectSize.Width,
                                                            rect.y + j * childRectSize.Height,
                                                            childRectSize.Width,
                                                            childRectSize.Height);
                        if (i == splitsPerDimension - 1) // adjust borders
                            childRect.width = rect.Right - childRect.x;
                        if (j == splitsPerDimension - 1) // adjust borders
                            childRect.height = rect.Bottom - childRect.y;
                        Refine_r(node.children[i, j], childRect, density, minSize, depth + 1, maxDepth, toneScale, dest);
                    }
                }
            }
        }

        private WangTileSet tileSet;
    }
}

