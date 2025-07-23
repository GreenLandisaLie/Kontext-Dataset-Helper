using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace KontextDatasetHelper
{
    public static class Utils
    {
        public static BitmapSource LoadBitmap(string imagePath)
        {
            BitmapSource imageSource = null;
            using (FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
            {
                BitmapImage tempBitmap = new BitmapImage();
                tempBitmap.BeginInit();
                tempBitmap.CacheOption = BitmapCacheOption.OnLoad;
                tempBitmap.StreamSource = fs;
                tempBitmap.EndInit();
                tempBitmap.Freeze();

                BitmapSource sourceToConvert = tempBitmap;
                if (tempBitmap.Format != PixelFormats.Bgr32)
                {
                    FormatConvertedBitmap converter = new FormatConvertedBitmap(tempBitmap, PixelFormats.Bgr32, null, 0);
                    converter.Freeze();
                    sourceToConvert = converter;
                }

                // Now, apply the DPI fix using the Pbgra32 source
                if (sourceToConvert.DpiX != 96 || sourceToConvert.DpiY != 96)
                {
                    WriteableBitmap tempWriteableBitmap = new WriteableBitmap(
                        sourceToConvert.PixelWidth,
                        sourceToConvert.PixelHeight,
                        96, // Target DPI X
                        96, // Target DPI Y
                        PixelFormats.Bgra32,
                        null);

                    int stride = sourceToConvert.PixelWidth * 4;
                    byte[] pixels = new byte[stride * sourceToConvert.PixelHeight];
                    sourceToConvert.CopyPixels(new Int32Rect(0, 0, sourceToConvert.PixelWidth, sourceToConvert.PixelHeight), pixels, stride, 0);

                    tempWriteableBitmap.WritePixels(new Int32Rect(0, 0, sourceToConvert.PixelWidth, sourceToConvert.PixelHeight), pixels, stride, 0);
                    tempWriteableBitmap.Freeze();
                    
                    imageSource = tempWriteableBitmap;
                }
                else
                {
                    imageSource = sourceToConvert;
                }
            }
            return imageSource;
        }

        public static Point GetImageRelativePoint(Image imageControl, Point mousePos)
        {
            if (imageControl.Source == null)
                return new Point(0, 0);

            var bmp = imageControl.Source;
            double imageAspect = bmp.Width / bmp.Height;
            double containerAspect = imageControl.ActualWidth / imageControl.ActualHeight;

            double renderWidth = imageControl.ActualWidth;
            double renderHeight = imageControl.ActualHeight;
            double offsetX = 0, offsetY = 0;

            if (containerAspect > imageAspect)
            {
                // Image is letterboxed on the left/right
                renderWidth = bmp.Width * (imageControl.ActualHeight / bmp.Height);
                offsetX = (imageControl.ActualWidth - renderWidth) / 2;
            }
            else
            {
                // Image is letterboxed on the top/bottom
                renderHeight = bmp.Height * (imageControl.ActualWidth / bmp.Width);
                offsetY = (imageControl.ActualHeight - renderHeight) / 2;
            }

            double scaleX = bmp.Width / renderWidth;
            double scaleY = bmp.Height / renderHeight;

            double xInImage = (mousePos.X - offsetX) * scaleX;
            double yInImage = (mousePos.Y - offsetY) * scaleY;

            //return new Point(Clamp(xInImage, 0, bmp.Width - 1), Clamp(yInImage, 0, bmp.Height - 1));
            return new Point(
                Math.Max(0, Math.Min(bmp.Width - 1, xInImage)),
                Math.Max(0, Math.Min(bmp.Height - 1, yInImage))
            );
        }

        public static void UpdateMaskBitmap(WriteableBitmap maskBitmap, int centerX, int centerY, int radius, bool isBase)
        {
            // Lock the bitmap to write pixels directly
            maskBitmap.Lock();
            IntPtr backBuffer = maskBitmap.BackBuffer;
            int stride = maskBitmap.BackBufferStride;

            // Define the color you want (Red for example)
            byte red = 255;
            byte green = 0;
            byte blue = 0;
            byte alpha = 255; // Fully opaque

            // Draw a solid circle (or square) for the mask
            for (int y = Math.Max(0, centerY - radius); y < Math.Min(maskBitmap.PixelHeight, centerY + radius); y++)
            {
                for (int x = Math.Max(0, centerX - radius); x < Math.Min(maskBitmap.PixelWidth, centerX + radius); x++)
                {
                    double dist = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                    if (dist <= radius) // Check if within circle
                    {
                        unsafe
                        {
                            byte* pPixel = (byte*)(backBuffer + y * stride + x * 4); // Assuming Bgra32

                            // Set the B, G, R, and A components
                            pPixel[0] = blue;   // Blue channel
                            pPixel[1] = green;  // Green channel
                            pPixel[2] = red;    // Red channel
                            pPixel[3] = alpha;  // Alpha channel (fully opaque)
                        }
                    }
                }
            }

            maskBitmap.AddDirtyRect(new Int32Rect(Math.Max(0, centerX - radius), Math.Max(0, centerY - radius),
                                                 Math.Min(maskBitmap.PixelWidth, centerX + radius) - Math.Max(0, centerX - radius),
                                                 Math.Min(maskBitmap.PixelHeight, centerY + radius) - Math.Max(0, centerY - radius)));
            maskBitmap.Unlock();
        }


        /// <summary>
        /// Creates a new BitmapSource that highlights pixels present in bms_2 but not in bms_1.
        /// The highlighted pixels will be bright green, and other areas will be transparent.
        /// </summary>
        /// <returns>A new BitmapSource showing the differences.</returns>
        public static BitmapSource GenerateDifferenceImage(BitmapSource bms_1, BitmapSource bms_2, int threshold = 5)
        {
            int width = bms_1.PixelWidth;
            int height = bms_1.PixelHeight;
            int stride = width * 4; // Assuming 32-bit PxFormat (BGRA or ARGB)

            byte[] pixels1 = new byte[height * stride];
            byte[] pixels2 = new byte[height * stride];
            byte[] diffPixels = new byte[height * stride];

            // Copy pixel data from both BitmapSources
            bms_1.CopyPixels(pixels1, stride, 0);
            bms_2.CopyPixels(pixels2, stride, 0);

            // Define the bright green color (ARGB)
            // A=255 (fully opaque), R=0, G=255, B=0
            byte greenA = 255;
            byte greenR = 0;
            byte greenG = 255;
            byte greenB = 0;

            for (int i = 0; i < pixels1.Length; i += 4)
            {
                // Assuming BGRA format for PixelFormats.Bgra32
                // byte 0: Blue, byte 1: Green, byte 2: Red, byte 3: Alpha

                // Calculate absolute differences for each channel
                int diffB = Math.Abs(pixels1[i] - pixels2[i]);
                int diffG = Math.Abs(pixels1[i + 1] - pixels2[i + 1]);
                int diffR = Math.Abs(pixels1[i + 2] - pixels2[i + 2]);
                int diffA = Math.Abs(pixels1[i + 3] - pixels2[i + 3]);

                // Check if any channel's difference exceeds the threshold
                if (diffB > threshold || diffG > threshold || diffR > threshold || diffA > threshold)
                {
                    // Set to bright green if the difference is significant
                    diffPixels[i] = greenB;     // Blue
                    diffPixels[i + 1] = greenG; // Green
                    diffPixels[i + 2] = greenR; // Red
                    diffPixels[i + 3] = greenA; // Alpha (fully opaque)
                }
                else
                {
                    // Pixels are considered "the same" within the threshold, make them fully transparent
                    diffPixels[i] = 0;     // Blue
                    diffPixels[i + 1] = 0; // Green
                    diffPixels[i + 2] = 0; // Red
                    diffPixels[i + 3] = 0; // Alpha (fully transparent)
                }
            }

            // Create the new BitmapSource
            return BitmapSource.Create(
                width,
                height,
                bms_1.DpiX, // Use DPI from bms_1
                bms_1.DpiY, // Use DPI from bms_1
                PixelFormats.Bgra32, // Use a pixel format that supports alpha
                null,
                diffPixels,
                stride);
        }


        /// <summary>
        /// Used for the Auto Mask feature - very flawed, returns a black image with white masks that represents the diff
        /// </summary>
        public static BitmapSource GetDifferenceMap(BitmapSource bs_1, BitmapSource bs_2, int minRegionSize, byte colorTolerance = 0)
        {
            if (bs_1.PixelWidth != bs_2.PixelWidth || bs_1.PixelHeight != bs_2.PixelHeight)
                throw new ArgumentException("BitmapSources must be the same size.");

            int width = bs_1.PixelWidth;
            int height = bs_1.PixelHeight;
            int stride = width * 4;

            byte[] pixels1 = new byte[height * stride];
            byte[] pixels2 = new byte[height * stride];
            byte[] diffMap = new byte[height * stride];

            bs_1.CopyPixels(pixels1, stride, 0);
            bs_2.CopyPixels(pixels2, stride, 0);

            unsafe
            {
                fixed (byte* p1 = pixels1, p2 = pixels2, pOut = diffMap)
                {
                    for (int i = 0; i < height * stride; i += 4)
                    {
                        // Compare BGRA channels with tolerance
                        bool similar =
                            Math.Abs(p1[i + 0] - p2[i + 0]) <= colorTolerance && // B
                            Math.Abs(p1[i + 1] - p2[i + 1]) <= colorTolerance && // G
                            Math.Abs(p1[i + 2] - p2[i + 2]) <= colorTolerance && // R
                            Math.Abs(p1[i + 3] - p2[i + 3]) <= colorTolerance;   // A

                        if (!similar)
                        {
                            pOut[i + 0] = 255;
                            pOut[i + 1] = 255;
                            pOut[i + 2] = 255;
                            pOut[i + 3] = 255;
                        }
                        else
                        {
                            pOut[i + 0] = 0;
                            pOut[i + 1] = 0;
                            pOut[i + 2] = 0;
                            pOut[i + 3] = 255;
                        }
                    }
                }
            }

            // Connected component filtering
            bool[] visited = new bool[width * height];
            Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
            List<(int x, int y)> regionPixels = new List<(int x, int y)>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    int byteIndex = index * 4;

                    if (visited[index] || diffMap[byteIndex] != 255)
                        continue;

                    regionPixels.Clear();
                    queue.Enqueue((x, y));
                    visited[index] = true;

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        int cIndex = cy * width + cx;
                        int cByteIndex = cIndex * 4;
                        regionPixels.Add((cx, cy));

                        foreach (var (nx, ny) in new[] { (cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1) })
                        {
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                int nIndex = ny * width + nx;
                                int nByteIndex = nIndex * 4;

                                if (!visited[nIndex] && diffMap[nByteIndex] == 255)
                                {
                                    visited[nIndex] = true;
                                    queue.Enqueue((nx, ny));
                                }
                            }
                        }
                    }

                    if (regionPixels.Count < minRegionSize)
                    {
                        foreach (var (px, py) in regionPixels)
                        {
                            int pIndex = (py * width + px) * 4;
                            diffMap[pIndex + 0] = 0;
                            diffMap[pIndex + 1] = 0;
                            diffMap[pIndex + 2] = 0;
                            diffMap[pIndex + 3] = 255;
                        }
                    }
                }
            }

            var wb = new WriteableBitmap(width, height, bs_1.DpiX, bs_1.DpiY, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), diffMap, stride, 0);
            return wb;
        }


        /// <summary>
        /// Originally a function that copies white pixels from source to destination as red pixels. <br/>
        /// When mode == 0 -> copies white pixels. (original code) <br/>
        /// When mode == 1 -> copies black pixels. (inverted mask) <br/>
        /// When mode == 2 -> copies non green pixels. (for inverted diff mask)
        /// </summary>
        public static void CopyPixels(BitmapSource source, WriteableBitmap destination, int mode = 0)
        {
            if (source.Format != PixelFormats.Bgra32)
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            
            bool copyBlackPixels = mode == 1;
            bool copyNonGreenPixels = mode == 2;
            bool copyWhitePixels = !copyBlackPixels && !copyNonGreenPixels;  // defaults back to original code if mode is invalid/set to whitePixels.

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4; // 4 bytes per pixel (BGRA)

            byte[] pixelData = new byte[height * stride];
            source.CopyPixels(pixelData, stride, 0);

            // Define the color you want (Red for example)
            byte red = 255;
            byte green = 0;
            byte blue = 0;
            byte alpha = 255; // Fully opaque

            destination.Lock();
            try
            {
                IntPtr backBuffer = destination.BackBuffer;
                unsafe
                {
                    byte* destPtr = (byte*)backBuffer;
                    fixed (byte* srcPtr = pixelData)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* srcRow = srcPtr + y * stride;
                            byte* destRow = destPtr + y * stride;

                            for (int x = 0; x < width; x++)
                            {
                                int i = x * 4;

                                byte b = srcRow[i + 0];
                                byte g = srcRow[i + 1];
                                byte r = srcRow[i + 2];
                                byte a = srcRow[i + 3];

                                bool isWhite = (a == 255 && r == 255 && g == 255 && b == 255);
                                bool isBlack = (a == 255 && r == 0 && g == 0 && b == 0);
                                bool isGreen = (a == 255 && r == 0 && g == 255 && b == 0);

                                if ( (copyWhitePixels && isWhite) || (copyBlackPixels && isBlack) || (copyNonGreenPixels && !isGreen))
                                {
                                    // Set the B, G, R, and A components
                                    destRow[i + 0] = blue;   // Blue channel
                                    destRow[i + 1] = green;  // Green channel
                                    destRow[i + 2] = red;    // Red channel
                                    destRow[i + 3] = alpha;  // Alpha channel (fully opaque)
                                }
                            }
                        }
                    }
                }

                destination.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
            }
            finally
            {
                destination.Unlock();
            }
        }



        // Helper to create a deep copy of a BitmapSource (for undo/redo)
        public static BitmapSource CloneBitmapSource(BitmapSource source)
        {
            if (source == null) return null;

            // Use PngBitmapEncoder for lossless copy. Other encoders like JpegBitmapEncoder are lossy.
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin); // Reset stream position
                return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }


        // Helper to create a deep copy of a WriteableBitmap (for undo/redo)
        public static WriteableBitmap CloneWriteableBitmap(WriteableBitmap source)
        {
            if (source == null) { return null; }

            // Create a new WriteableBitmap with the same size and pixel format
            WriteableBitmap clone = new WriteableBitmap(
                source.PixelWidth,
                source.PixelHeight,
                source.DpiX,
                source.DpiY,
                source.Format,
                source.Palette
            );

            try
            {
                int height = source.PixelHeight;
                int width = source.PixelWidth;
                int stride = source.BackBufferStride;
                int bufferSize = width * height * 4;

                byte[] pixelData = new byte[bufferSize];

                // Copy pixels from source
                source.CopyPixels(new Int32Rect(0, 0, width, height), pixelData, width * 4, 0);

                // Lock the source and clone for reading/writing - can only be done after copying pixels otherwise it will HANG
                source.Lock();
                clone.Lock();

                // Write pixels to clone
                Marshal.Copy(pixelData, 0, clone.BackBuffer, bufferSize);

                // Indicate that the entire bitmap area has changed
                clone.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                source.Unlock();
                clone.Unlock();
            }
            return clone;
        }



        public static void SaveBitmapAsRgb(BitmapSource source, string path)
        {
            // Convert the image to Bgr24 format (RGB, no alpha)
            FormatConvertedBitmap rgbImage = new FormatConvertedBitmap();
            rgbImage.BeginInit();
            rgbImage.Source = source;
            rgbImage.DestinationFormat = PixelFormats.Bgr24; // RGB (no alpha)
            rgbImage.EndInit();

            // Save using a PNG encoder (could also use JpegBitmapEncoder for true RGB-only formats)
            using (FileStream stream = new FileStream(path, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rgbImage));
                encoder.Save(stream);
            }
        }



    }
}
