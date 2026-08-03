using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrushIt.Data;

namespace CrushIt.Core
{
    public class InputHandler
    {
        private readonly Form parentForm;
        private readonly int rows;
        private readonly int cols;
        private readonly int tileSize;
        private readonly int gridOffsetX;
        private readonly int gridOffsetY;

        private Point? selectedTile = null;
        private Point dragStartPos;
        private bool isDragging = false;
        private bool isTouchDevice = false;


        public bool IsAnimating { get; private set; } = false;
        public Point SwapTileA { get; private set; }
        public Point SwapTileB { get; private set; }
        public PointF AnimOffsetA { get; private set; }
        public PointF AnimOffsetB { get; private set; }

        public event Func<Point, Point, Task>? OnSwapRequested;

        public InputHandler(Form form, int rows, int cols, int tileSize, int gridOffsetX, int gridOffsetY)
        {
            this.parentForm = form;
            this.rows = rows;
            this.cols = cols;
            this.tileSize = tileSize;
            this.gridOffsetX = gridOffsetX;
            this.gridOffsetY = gridOffsetY;

            isTouchDevice = MobileHelper.IsMobile;

            this.parentForm.MouseDown += OnMouseDown;
            this.parentForm.MouseMove += OnMouseMove;
            this.parentForm.MouseUp += OnMouseUp;


            if (isTouchDevice)
            {
                this.parentForm.MouseDown += OnTouchDown;
                this.parentForm.MouseMove += OnTouchMove;
                this.parentForm.MouseUp += OnTouchUp;
            }
        }

        public Point? SelectedTile => selectedTile;

        public Point? GetTileFromMouse(Point mousePos)
        {
            int c = (mousePos.X - gridOffsetX) / tileSize;
            int r = (mousePos.Y - gridOffsetY) / tileSize;

            if (r >= 0 && r < rows && c >= 0 && c < cols)
            {
                Rectangle tileBounds = new Rectangle(gridOffsetX + c * tileSize, gridOffsetY + r * tileSize, tileSize, tileSize);
                if (tileBounds.Contains(mousePos)) return new Point(c, r);
            }
            return null;
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (IsAnimating || e.Button != MouseButtons.Left) return;

            Point? tile = GetTileFromMouse(e.Location);
            if (tile.HasValue)
            {
                selectedTile = tile;
                dragStartPos = e.Location;
                isDragging = true;
                parentForm.Invalidate();
            }
        }

        private async void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!isDragging || !selectedTile.HasValue || IsAnimating) return;

            int dx = e.X - dragStartPos.X;
            int dy = e.Y - dragStartPos.Y;
            int dragThreshold = isTouchDevice ? 30 : 18;

            if (Math.Abs(dx) > dragThreshold || Math.Abs(dy) > dragThreshold)
            {
                isDragging = false;
                Point startTile = selectedTile.Value;
                Point targetTile = startTile;

                if (Math.Abs(dx) > Math.Abs(dy))
                {
                    targetTile.X += dx > 0 ? 1 : -1;
                }
                else
                {
                    targetTile.Y += dy > 0 ? 1 : -1;
                }

                selectedTile = null;

                if (targetTile.X >= 0 && targetTile.X < cols && targetTile.Y >= 0 && targetTile.Y < rows)
                {
                    if (OnSwapRequested != null)
                    {
                        await OnSwapRequested.Invoke(startTile, targetTile);
                    }
                }
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            isDragging = false;
        }


        private void OnTouchDown(object? sender, MouseEventArgs e)
        {
            OnMouseDown(sender, e);
        }

        private void OnTouchMove(object? sender, MouseEventArgs e)
        {
            OnMouseMove(sender, e);
        }

        private void OnTouchUp(object? sender, MouseEventArgs e)
        {
            OnMouseUp(sender, e);
        }


        public async Task AnimateSwapAsync(Point p1, Point p2, bool isRevert = false)
        {
            IsAnimating = true;
            SwapTileA = p1;
            SwapTileB = p2;

            float targetDx = (p2.X - p1.X) * tileSize;
            float targetDy = (p2.Y - p1.Y) * tileSize;

            int steps = 8;
            int delayPerStep = 12;

            for (int i = 1; i <= steps; i++)
            {
                float progress = (float)i / steps;
                AnimOffsetA = new PointF(targetDx * progress, targetDy * progress);
                AnimOffsetB = new PointF(-targetDx * progress, -targetDy * progress);

                parentForm.Invalidate();
                await Task.Delay(delayPerStep);
            }

            AnimOffsetA = PointF.Empty;
            AnimOffsetB = PointF.Empty;
            IsAnimating = false;
            parentForm.Invalidate();
        }
    }
}

