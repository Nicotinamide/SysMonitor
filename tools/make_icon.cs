using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace IconGen
{
    class Program
    {
        static void Main()
        {
            using (Bitmap bmp = new Bitmap(128, 128))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // Outer dark watch bezel
                    using (GraphicsPath path = CreateRoundedRectangle(8, 8, 112, 112, 28))
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(13, 17, 23)))
                    using (Pen pen = new Pen(Color.FromArgb(0, 210, 255), 3))
                    {
                        g.FillPath(brush, path);
                        g.DrawPath(pen, path);
                    }

                    // Tile 1 - Power
                    using (GraphicsPath p1 = CreateRoundedRectangle(18, 18, 92, 26, 7))
                    using (SolidBrush b1 = new SolidBrush(Color.FromArgb(22, 27, 34)))
                    {
                        g.FillPath(b1, p1);
                    }
                    using (SolidBrush bBat = new SolidBrush(Color.FromArgb(245, 158, 11)))
                    {
                        g.FillRectangle(bBat, 24, 34, 45, 3);
                    }

                    // Tile 2 - Net
                    using (GraphicsPath p2 = CreateRoundedRectangle(18, 50, 92, 28, 7))
                    using (SolidBrush b2 = new SolidBrush(Color.FromArgb(22, 27, 34)))
                    {
                        g.FillPath(b2, p2);
                    }
                    using (SolidBrush bNet = new SolidBrush(Color.FromArgb(88, 166, 255)))
                    {
                        g.FillRectangle(bNet, 24, 66, 35, 3);
                    }

                    // Tile 3 - ZT
                    using (GraphicsPath p3 = CreateRoundedRectangle(18, 84, 92, 24, 7))
                    using (SolidBrush b3 = new SolidBrush(Color.FromArgb(22, 27, 34)))
                    {
                        g.FillPath(b3, p3);
                    }
                    using (SolidBrush bZt = new SolidBrush(Color.FromArgb(16, 185, 129)))
                    {
                        g.FillEllipse(bZt, 24, 92, 8, 8);
                    }
                }

                // Save to .ico using standard Icon format
                IntPtr hIcon = bmp.GetHicon();
                using (Icon icon = Icon.FromHandle(hIcon))
                using (FileStream fs = new FileStream("app.ico", FileMode.Create))
                {
                    icon.Save(fs);
                }
                Console.WriteLine("app.ico generated successfully!");
            }
        }

        private static GraphicsPath CreateRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
