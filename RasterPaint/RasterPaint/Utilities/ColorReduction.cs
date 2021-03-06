﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using RasterPaint.Objects;
using Color = System.Drawing.Color;

namespace RasterPaint.Utilities
{
    public static class ColorReduction
    {
        #region Algorithms
        public static WriteableBitmap UniformQuantization(WriteableBitmap wbm, byte nR, byte nG, byte nB)
        {
            var result = wbm.Clone();

            unsafe
            {
                using (var context = result.GetBitmapContext())
                {
                    for (int i = 0; i < wbm.PixelWidth; i++)
                    {
                        for (int j = 0; j < wbm.PixelHeight; j++)
                        {
                            var c = context.Pixels[j * context.Width + i];
                            byte a = (byte)(c >> 24);

                            // Prevent division by zero
                            int ai = a;
                            if (ai == 0)
                            {
                                ai = 1;
                            }

                            ai = ((255 << 8) / ai);

                            var color = Color.FromArgb(
                                a,
                                (byte)((((c >> 16) & 0xFF) * ai) >> 8),
                                (byte)((((c >> 8) & 0xFF) * ai) >> 8),
                                (byte)((((c & 0xFF) * ai) >> 8)));

                            var r = ReducePixelUq(nR, color.R);
                            var g = ReducePixelUq(nG, color.G);
                            var b = ReducePixelUq(nB, color.B);

                            context.Pixels[j * context.Width + i] = (a << 24) | (r << 16) | (g << 8) | b;
                        }
                    }
                }

                return result;
            }
        }

        public static WriteableBitmap PopularityAlgorithm(WriteableBitmap wbm, int k)
        {
            var clone = wbm.Clone();

            unsafe
            {
                using (var context = clone.GetBitmapContext())
                {
                    var colorsArray = GetMostPopularColors(context, k).ToArray();

                    if (colorsArray != null)
                    {
                        for (int i = 0; i < context.Width; i++)
                        {
                            for (int j = 0; j < context.Height; j++)
                            {
                                var c = context.Pixels[j * context.Width + i];
                                var a = (byte)(c >> 24);

                                int ai = a;
                                if (ai == 0)
                                {
                                    ai = 1;
                                }

                                ai = ((255 << 8) / ai);
                                var color = Color.FromArgb(a,
                                    (byte)((((c >> 16) & 0xFF) * ai) >> 8),
                                    (byte)((((c >> 8) & 0xFF) * ai) >> 8),
                                    (byte)((((c & 0xFF) * ai) >> 8)));

                                var closestColor = GetTheClosestPixel(color, colorsArray);

                                context.Pixels[j * context.Width + i] = (255 << 24) | (closestColor.R << 16) | (closestColor.G << 8) | closestColor.B;
                            }
                        }
                    }
                }

                return clone;
            }
        }

        public static WriteableBitmap OctreeAlgorithm(WriteableBitmap wbm, int k)
        {
            var clone = wbm.Clone();

            Octree o = new Octree(clone);

            return o.GenerateBitmapFromOctree(k);
        }
        #endregion

        #region Methods
        private static void AddColorToDictionary(ref Dictionary<System.Windows.Media.Color, int> colorsDictionary, System.Windows.Media.Color color)
        {
            if (colorsDictionary.ContainsKey(color))
            {
                colorsDictionary[color]++;
            }
            else
            {
                colorsDictionary[color] = 1;
            }
        }

        public static IEnumerable<Color> GetMostPopularColors(BitmapContext context, int k)
        {
            try
            {
                unsafe
                {
                    Dictionary<Color, int> colorsDictionary = new Dictionary<Color, int>();

                    for (int i = 0; i < context.Width; i++)
                    {
                        for (int j = 0; j < context.Height; j++)
                        {
                            var c = context.Pixels[j*context.Width + i];
                            byte a = (byte) (c >> 24);

                            // Prevent division by zero
                            int ai = a;
                            if (ai == 0)
                            {
                                ai = 1;
                            }

                            ai = ((255 << 8)/ai);

                            var color = Color.FromArgb(
                                a,
                                (byte) ((((c >> 16) & 0xFF)*ai) >> 8),
                                (byte) ((((c >> 8) & 0xFF)*ai) >> 8),
                                (byte) ((((c & 0xFF)*ai) >> 8)));

                            if (colorsDictionary.ContainsKey(color))
                            {
                                colorsDictionary[color]++;
                            }
                            else
                            {
                                colorsDictionary[color] = 1;
                            }
                        }
                    }

                    return colorsDictionary.OrderByDescending(b => b.Value).Select(b => b.Key).Take(k);
                }
            }
            catch (AccessViolationException)
            {
                MessageBox.Show("Tried to access protected memory. Try reloading picture.");
            }

            return null;
        }

        public static Color GetTheClosestPixel(Color c, Color[] colorsArray)
        {
            int distance = int.MaxValue;
            Color? closestColor = null;

            for (int i = 0; i < colorsArray.Count(); i++)
            {
                var dist = DistanceBetweenPixels(c, colorsArray[i]);

                if (dist == 0)
                {
                    closestColor = colorsArray[i];
                    return (Color) closestColor;
                }

                if (distance > dist)
                {
                    distance = dist;
                    closestColor = colorsArray[i];
                }
            }
        
            return closestColor ?? c;
        }

        public static int DistanceBetweenPixels(Color a, Color b)
        {
            return (a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B);
        }

        public static int DistanceBetweenPixels(System.Windows.Media.Color a, System.Windows.Media.Color b)
        {
            return (a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B);
        }

        private static byte ReducePixelUq(byte n, byte pixelValue)
        {
            if (n == 0) return pixelValue;
            if (n == 1) pixelValue = 128;
            int result;

            int fraction = (int) Math.Floor((double) 256 / n);
            int part = (int) Math.Ceiling((double) pixelValue / fraction); // ilość części == n;

            if (pixelValue == 0) part++; // because 0 is in the first part;
            if (pixelValue == 255) part = n; // because 0 is in the last part;

            if (part > n)
            {
                part = n;
            }

            if (part == n)
            {
                var offset = (part - 1) * fraction;
                result = (byte) Math.Floor((double)(offset + ((255 - offset) / 2)));
            }
            else
            {
                result = (byte)(Math.Floor((double)((part - 1) * fraction + fraction / 2)));
            }

            return (byte)result;
        }
        #endregion

        #region ForTests
        public static byte ReducePixelUqForTests(byte n, byte pixelValue)
        {
            return ReducePixelUq(n, pixelValue);
        }
        #endregion
    }
}