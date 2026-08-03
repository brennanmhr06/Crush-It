using System;
using System.Drawing;
using System.Windows.Forms;

namespace CrushIt.Core
{
    public enum TransitionDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    public class TransitionManager
    {
        private Form? currentForm;
        private Form? nextForm;
        private TransitionDirection direction;
        private System.Windows.Forms.Timer transitionTimer;
        private int transitionProgress;
        private const int TransitionDuration = 300;
        private const int TransitionSteps = 30;
        private int stepDuration;

        public event EventHandler? TransitionCompleted;

        public TransitionManager()
        {
            transitionTimer = new System.Windows.Forms.Timer();
            transitionTimer.Tick += TransitionTimer_Tick;
            stepDuration = TransitionDuration / TransitionSteps;
        }

        public void StartTransition(Form fromForm, Form toForm, TransitionDirection dir)
        {
            currentForm = fromForm;
            nextForm = toForm;
            direction = dir;
            transitionProgress = 0;


            Rectangle screenBounds = fromForm.Bounds;
            nextForm.Bounds = screenBounds;
            nextForm.Show();
            nextForm.BringToFront();


            switch (direction)
            {
                case TransitionDirection.Left:
                    nextForm.Left = screenBounds.Right;
                    break;
                case TransitionDirection.Right:
                    nextForm.Left = -screenBounds.Width;
                    break;
                case TransitionDirection.Up:
                    nextForm.Top = screenBounds.Bottom;
                    break;
                case TransitionDirection.Down:
                    nextForm.Top = -screenBounds.Height;
                    break;
            }


            currentForm.Left = screenBounds.X;
            currentForm.Top = screenBounds.Y;

            transitionTimer.Interval = stepDuration;
            transitionTimer.Start();
        }

        private void TransitionTimer_Tick(object? sender, EventArgs e)
        {
            transitionProgress++;
            double progress = (double)transitionProgress / TransitionSteps;
            double easedProgress = EaseInOutCubic(progress);

            if (currentForm == null || nextForm == null) return;

            Rectangle screenBounds = currentForm.Bounds;
            int offsetX = 0;
            int offsetY = 0;

            switch (direction)
            {
                case TransitionDirection.Left:
                    offsetX = (int)(screenBounds.Width * easedProgress);
                    currentForm.Left = screenBounds.X - offsetX;
                    nextForm.Left = screenBounds.Right - offsetX;
                    break;
                case TransitionDirection.Right:
                    offsetX = (int)(screenBounds.Width * easedProgress);
                    currentForm.Left = screenBounds.X + offsetX;
                    nextForm.Left = -screenBounds.Width + offsetX;
                    break;
                case TransitionDirection.Up:
                    offsetY = (int)(screenBounds.Height * easedProgress);
                    currentForm.Top = screenBounds.Y - offsetY;
                    nextForm.Top = screenBounds.Bottom - offsetY;
                    break;
                case TransitionDirection.Down:
                    offsetY = (int)(screenBounds.Height * easedProgress);
                    currentForm.Top = screenBounds.Y + offsetY;
                    nextForm.Top = -screenBounds.Height + offsetY;
                    break;
            }

            if (transitionProgress >= TransitionSteps)
            {
                transitionTimer.Stop();
                currentForm.Hide();
                currentForm.Dispose();
                TransitionCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private double EaseInOutCubic(double t)
        {
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        public void Dispose()
        {
            transitionTimer?.Stop();
            transitionTimer?.Dispose();
        }
    }
}