using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CrushIt.Core
{
    public static class MobileHelper
    {
        private static bool? _isMobile;
        private static float? _scaleFactor;
        private static Size? _screenSize;


        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_MAXIMUMTOUCHES = 95;

        public static bool IsMobile
        {
            get
            {
                if (_isMobile.HasValue)
                    return _isMobile.Value;


                var screenSize = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                bool isSmallScreen = screenSize.Width <= 800 || screenSize.Height <= 600;
                bool hasTouch = GetSystemMetrics(SM_MAXIMUMTOUCHES) > 0;

                _isMobile = isSmallScreen || hasTouch;
                return _isMobile.Value;
            }
            set
            {
                _isMobile = value;
            }
        }

        public static float ScaleFactor
        {
            get
            {
                if (_scaleFactor.HasValue)
                    return _scaleFactor.Value;


                var screenSize = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                float widthScale = screenSize.Width / 550f;
                float heightScale = screenSize.Height / 700f;
                _scaleFactor = Math.Min(widthScale, heightScale);


                if (_scaleFactor.Value > 1.2f)
                    _scaleFactor = 1.2f;

                return _scaleFactor.Value;
            }
        }

        public static Size ScreenSize
        {
            get
            {
                if (_screenSize.HasValue)
                    return _screenSize.Value;

                _screenSize = Screen.PrimaryScreen?.Bounds.Size ?? new Size(1920, 1080);
                return _screenSize.Value;
            }
        }

        public static int ScaleInt(int value)
        {
            return (int)(value * ScaleFactor);
        }

        public static float ScaleFloat(float value)
        {
            return value * ScaleFactor;
        }

        public static Size ScaleSize(Size size)
        {
            return new Size(ScaleInt(size.Width), ScaleInt(size.Height));
        }

        public static Rectangle ScaleRectangle(Rectangle rect)
        {
            return new Rectangle(
                ScaleInt(rect.X),
                ScaleInt(rect.Y),
                ScaleInt(rect.Width),
                ScaleInt(rect.Height)
            );
        }

        public static Point ScalePoint(Point point)
        {
            return new Point(ScaleInt(point.X), ScaleInt(point.Y));
        }

        public static void ApplyMobileScaling(Form form)
        {
            if (!IsMobile)
                return;

            var originalSize = form.Size;
            var scaledSize = ScaleSize(originalSize);


            var screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            form.Location = new Point(
                (screenBounds.Width - scaledSize.Width) / 2,
                (screenBounds.Height - scaledSize.Height) / 2
            );

            form.Size = scaledSize;
        }

        public static void ResetDetection()
        {
            _isMobile = null;
            _scaleFactor = null;
            _screenSize = null;
        }
    }
}

