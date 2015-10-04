using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//using System.Windows.Forms;

using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using GifAnimator;

namespace Screenshot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        int SCREEN_WIDTH;
        int SCREEN_HEIGHT;
        bool rec_continue = false;
        Window stopper = new Stopper();

        public MainWindow()
        {
            InitializeComponent();

            SCREEN_WIDTH = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
            SCREEN_HEIGHT = (int)System.Windows.SystemParameters.PrimaryScreenHeight;


            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            stopper.Left = desktopWorkingArea.Right - stopper.Width;
            stopper.Top = desktopWorkingArea.Bottom - stopper.Height;
            ((Button)(stopper.FindName("stopButton"))).Click += (sender, e) =>
            {
                rec_continue = false;
            };
            stopper.Hide();

            SetEvents();
        }

        private void SetEvents()
        {
            this.MouseDown += (sender, e) =>
            {
                var ev = (MouseButtonEventArgs)e;
                if (ev.LeftButton == MouseButtonState.Pressed)
                {
                    ((Window)sender).DragMove();
                }
            };

            shotButton.Click += (sender, e) =>
            {
                Shot((Button)sender);
            };

            fullShotButton.Click += (sender, e) =>
            {
                Shot((Button)sender);
            };

            colorButton.Click += (sender, e) =>
            {
                ChangeWindowColor();
            };

            Closing += (sender, e) =>
            {
                stopper.Close();
            };
        }

        private void ChangeWindowColor()
        {
            if (Background == System.Windows.Media.Brushes.Black)
            {
                Background = System.Windows.Media.Brushes.AliceBlue;
                Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                Background = System.Windows.Media.Brushes.Black;
                Foreground = System.Windows.Media.Brushes.AliceBlue;
            }
        }

        private async void Shot(Button sender)
        {
            int x0, y0, width, height;
            if (sender.Name == "shotButton")
            {
                x0 = (int)Left;
                y0 = (int)Top;
                width = (int)ActualWidth;
                height = (int)ActualHeight;
            }
            else
            {
                x0 = 0;
                y0 = 0;
                width = SCREEN_WIDTH;
                height = SCREEN_HEIGHT;
            }

            var text = outputWidth.Text;
            var scale = 1.0d;
            if (text != "")
            {
                try
                {
                    var outW = Convert.ToDouble(text);
                    scale = outW / width;
                }
                catch
                {
                    return;
                }
            }

            var fps = 5;

            //double delayTime;
            //try
            //{
            //    delayTime = Convert.ToDouble(timer.Text);
            //}
            //catch
            //{
            //    delayTime = 0.5;
            //}
            double delayTime = 0.5;

            Hide();
            stopper.Show();

            var filename = String.Format("{0}/{1}.gif",
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                System.DateTime.Now.ToString("yyMMddHHmmssf")
            );
            System.Threading.Thread.CurrentThread.Join(TimeSpan.FromSeconds(delayTime));
            Console.WriteLine($"{x0}, {y0}, {width}, {height}, {scale}");
            await Task.Run(() =>
            {
                Save(x0, y0, width, height, scale, fps, filename);
            });
            Show();
            stopper.Hide();
        }

        void Save(int x, int y, int w, int h, double scale, int fps, string filename)
        {
            var delayTime = 1.0 / fps;
            var delayMilliSec = (uint)(delayTime * 1000);
            var counter = 0;
            var frameMax = 100;
            DateTime last = new DateTime(0);
            rec_continue = true;
            var gifAnimator = new GifAnimator.GifAnimator(filename);

            while (true)
            {
                using (Bitmap bmp = new Bitmap(w, h))
                {
                    var duration = DateTime.Now - last;
                    var breakTime = delayTime - duration.TotalSeconds;
                    Console.WriteLine($"break: {breakTime}\n process duration: {duration.Milliseconds}");
                    if (breakTime >= 0.0)
                    {
                        Thread.Sleep((int)(breakTime * 1000));
                    }
                    last = DateTime.Now;

                    using (var g = Graphics.FromImage(bmp))
                    {

                        g.CopyFromScreen(new System.Drawing.Point(x, y),
                                         System.Drawing.Point.Empty,
                                         bmp.Size);

                        // draw mouse cursor on screenshot
                        User32.CURSORINFO cursorInfo;
                        cursorInfo.cbSize = Marshal.SizeOf(typeof(User32.CURSORINFO));
                        if (User32.GetCursorInfo(out cursorInfo))
                        {
                            // if the cursor is showing draw it on the screen shot
                            if (cursorInfo.flags == User32.CURSOR_SHOWING)
                            {
                                // we need to get hotspot so we can draw the cursor in the correct position
                                var iconPointer = User32.CopyIcon(cursorInfo.hCursor);
                                User32.ICONINFO iconInfo;
                                int iconX, iconY;

                                if (User32.GetIconInfo(iconPointer, out iconInfo))
                                {
                                    // calculate the correct position of the cursor
                                    iconX = cursorInfo.ptScreenPos.x - ((int)iconInfo.xHotspot) - x;
                                    iconY = cursorInfo.ptScreenPos.y - ((int)iconInfo.yHotspot) - y;

                                    // draw the cursor icon on top of the captured screen image
                                    User32.DrawIcon(g.GetHdc(), iconX, iconY, cursorInfo.hCursor);

                                    // release the handle created by call to g.GetHdc()
                                    g.ReleaseHdc();
                                }
                            }
                        }
                    }
                    var hBmp = bmp.GetHbitmap();
                    var bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBmp,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    DeleteObject(hBmp);
                    var tBmp = new TransformedBitmap(bmpSource, new ScaleTransform(scale, scale));

                    var sw = new Stopwatch();
                    sw.Start();
                    gifAnimator.Add(BitmapFrame.Create(tBmp), delayMilliSec);
                    Console.WriteLine($"animator: {sw.Elapsed}");

                    counter++;
                    if (!rec_continue || counter > frameMax)
                    {
                        gifAnimator.Finish();
                        break;
                    }
                }
            }
        }


        public static class User32
        {
            public const Int32 CURSOR_SHOWING = 0x00000001;

            [StructLayout(LayoutKind.Sequential)]
            public struct ICONINFO
            {
                public bool fIcon;
                public Int32 xHotspot;
                public Int32 yHotspot;
                public IntPtr hbmMask;
                public IntPtr hbmColor;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public Int32 x;
                public Int32 y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct CURSORINFO
            {
                public Int32 cbSize;
                public Int32 flags;
                public IntPtr hCursor;
                public POINT ptScreenPos;
            }

            [DllImport("user32.dll")]
            public static extern bool GetCursorInfo(out CURSORINFO pci);

            [DllImport("user32.dll")]
            public static extern IntPtr CopyIcon(IntPtr hIcon);

            [DllImport("user32.dll")]
            public static extern bool DrawIcon(IntPtr hdc, int x, int y, IntPtr hIcon);

            [DllImport("user32.dll")]
            public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
        }
    }
}