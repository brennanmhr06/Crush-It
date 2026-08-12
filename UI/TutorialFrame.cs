using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;
using CrushIt.UI;

namespace CrushIt.UI
{
    public class TutorialFrame : Form
    {
        private const int Rows = 8;
        private const int Cols = 8;
        private const int TileSize = 54;
        private const int GridOffsetX = 59;
        private const int GridOffsetY = 135;

        private CandyType[,] board = new CandyType[Rows, Cols];
        private Random rand = new Random();

        private InputHandler inputController = null!;
        private bool isProcessingBoard = false;

        private const int TargetPointGoal = 1000;

        private List<CandyParticle> burstParticles = new List<CandyParticle>();
        private System.Windows.Forms.Timer gameLoopTimer = null!;

        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private bool levelCompleted = false;
        private int sessionGold = 0;
        private int completionAnimationPhase = 0;
        private int pulsePhase = 0;
        private StyleParticle[] backgroundParticles = Array.Empty<StyleParticle>();
        private readonly Random bgRand = new Random();
        private float buttonScale = 1f;
        private float buttonPressDepth = 0f;
        private bool isButtonHovered = false;
        private Rectangle skipButtonRect;
        private List<ConfettiParticle> confettiParticles = new List<ConfettiParticle>();
        private bool showSuccessAnimation = false;
        private int successAnimationPhase = 0;

        private readonly IMongoCollection<UserAccount> usersCollection;

        public TutorialFrame(UserAccount user)
        {
            this.currentUser = user;

            ConfigurationHelper.Initialize();

            var client = new MongoClient(ConfigurationHelper.GetMongoConnectionString());
            database = client.GetDatabase(ConfigurationHelper.GetDatabaseName());
            usersCollection = database.GetCollection<UserAccount>("users");

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            GameData.ResetScore();


            MobileHelper.ApplyMobileScaling(this);

            inputController = new InputHandler(this, Rows, Cols, TileSize, GridOffsetX, GridOffsetY);
            inputController.OnSwapRequested += HandleSwapRequestedAsync;

            GenerateBoardWithoutInitialMatches();

            gameLoopTimer = new System.Windows.Forms.Timer();
            gameLoopTimer.Interval = 16;
            gameLoopTimer.Tick += GameLoopTimer_Tick;
            gameLoopTimer.Start();

            backgroundParticles = CrushItStyleHelper.CreateParticles(bgRand, 20, 890, 80, 530); // Reduced from 30 to 20
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Tutorial Level";
            this.Size = new Size(900, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.FormClosed += (s, e) => {
                SoundManager.StopBackgroundMusic();
                if (Application.OpenForms.Count == 0)
                {
                    SoundManager.Cleanup();
                    Application.Exit();
                }
            };
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };
            
            this.MouseMove += TutorialFrame_MouseMove;
            this.MouseClick += TutorialFrame_MouseClick;
            this.MouseLeave += (s, e) => { isButtonHovered = false; this.Cursor = Cursors.Default; };
            
            // Initialize skip button rectangle
            skipButtonRect = new Rectangle(750, 15, 120, 40);
        }
        
        private void TutorialFrame_MouseMove(object? sender, MouseEventArgs e)
        {
            bool wasButtonHovered = isButtonHovered;
            isButtonHovered = skipButtonRect.Contains(e.Location);
            this.Cursor = isButtonHovered ? Cursors.Hand : Cursors.Default;
            
            if (wasButtonHovered != isButtonHovered)
                this.Invalidate();
        }
        
        private void TutorialFrame_MouseClick(object? sender, MouseEventArgs e)
        {
            if (skipButtonRect.Contains(e.Location))
            {
                SoundManager.PlaySound(SoundType.ButtonClick);
                SkipTutorial();
            }
        }
        
        private void SkipTutorial()
        {
            try
            {
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Email, currentUser.Email);
                var update = Builders<UserAccount>.Update
                    .Set(u => u.HasCompletedTutorial, true)
                    .Inc(u => u.Gold, sessionGold);
                usersCollection.UpdateOneAsync(filter, update);

                currentUser.Gold += sessionGold;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update tutorial status: {ex.Message}");
            }

            // Check if MainFrame already exists and refresh it instead of creating new one
            foreach (Form form in Application.OpenForms)
            {
                if (form is MainFrame mainFrame)
                {
                    mainFrame.RefreshLevelsData();
                    mainFrame.Show();
                    this.Hide();
                    this.Dispose();
                    return;
                }
            }

            // If no MainFrame exists, create a new one
            MainFrame main = new MainFrame(currentUser, database);
            main.Show();

            this.Hide();
            this.Dispose();
        }

        private void GenerateBoardWithoutInitialMatches()
        {
            Array values = Enum.GetValues(typeof(CandyType));
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    CandyType type;
                    do
                    {
                        type = (CandyType)values.GetValue(rand.Next(values.Length))!;
                    }
                    while ((c >= 2 && board[r, c - 1] == type && board[r, c - 2] == type) ||
                           (r >= 2 && board[r - 1, c] == type && board[r - 2, c] == type));

                    board[r, c] = type;
                }
            }
        }

        private async Task HandleSwapRequestedAsync(Point p1, Point p2)
        {
            if (isProcessingBoard) return;
            isProcessingBoard = true;

            SoundManager.PlaySound(SoundType.Swipe);

            await inputController.AnimateSwapAsync(p1, p2);

            CandyType temp = board[p1.Y, p1.X];
            board[p1.Y, p1.X] = board[p2.Y, p2.X];
            board[p2.Y, p2.X] = temp;

            List<Point> matches = FindMatches();
            if (matches.Count > 0)
            {
                await ProcessMatchesCascade();
            }
            else
            {
                await inputController.AnimateSwapAsync(p2, p1, isRevert: true);
                board[p2.Y, p2.X] = board[p1.Y, p1.X];
                board[p1.Y, p1.X] = temp;
            }

            isProcessingBoard = false;
            this.Invalidate();
        }

        private List<Point> FindMatches()
        {
            HashSet<Point> matchedPoints = new HashSet<Point>();

            for (int r = 0; r < Rows; r++)
            {
                int matchLength = 1;
                for (int c = 0; c < Cols; c++)
                {
                    bool isEnd = (c == Cols - 1);
                    if (!isEnd && board[r, c] == board[r, c + 1])
                    {
                        matchLength++;
                    }
                    else
                    {
                        if (matchLength >= 3)
                        {
                            for (int i = 0; i < matchLength; i++)
                                matchedPoints.Add(new Point(c - i, r));
                        }
                        matchLength = 1;
                    }
                }
            }

            for (int c = 0; c < Cols; c++)
            {
                int matchLength = 1;
                for (int r = 0; r < Rows; r++)
                {
                    bool isEnd = (r == Rows - 1);
                    if (!isEnd && board[r, c] == board[r + 1, c])
                    {
                        matchLength++;
                    }
                    else
                    {
                        if (matchLength >= 3)
                        {
                            for (int i = 0; i < matchLength; i++)
                                matchedPoints.Add(new Point(c, r - i));
                        }
                        matchLength = 1;
                    }
                }
            }

            for (int r = 0; r < Rows - 1; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    CandyType type = board[r, c];
                    if (type != (CandyType)(-1) &&
                        type == board[r, c + 1] &&
                        type == board[r + 1, c] &&
                        type == board[r + 1, c + 1])
                    {
                        matchedPoints.Add(new Point(c, r));
                        matchedPoints.Add(new Point(c + 1, r));
                        matchedPoints.Add(new Point(c, r + 1));
                        matchedPoints.Add(new Point(c + 1, r + 1));
                    }
                }
            }

            return new List<Point>(matchedPoints);
        }

        private List<Point> FindSquareMatches()
        {
            List<Point> squareMatches = new List<Point>();

            for (int r = 0; r < Rows - 1; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    CandyType type = board[r, c];
                    if (type != (CandyType)(-1) &&
                        type == board[r, c + 1] &&
                        type == board[r + 1, c] &&
                        type == board[r + 1, c + 1])
                    {
                        squareMatches.Add(new Point(c, r));
                        squareMatches.Add(new Point(c + 1, r));
                        squareMatches.Add(new Point(c, r + 1));
                        squareMatches.Add(new Point(c + 1, r + 1));
                        return squareMatches;
                    }
                }
            }

            return squareMatches;
        }

        private async Task ProcessMatchesCascade()
        {
            while (true)
            {
                List<Point> matches = FindMatches();
                if (matches.Count == 0) break;

                List<Point> squareMatches = FindSquareMatches();
                HashSet<Point> explosionPoints = new HashSet<Point>();

                foreach (Point pt in matches)
                {
                    CandyType type = board[pt.Y, pt.X];
                    GameData.AddPoints(type);

                    int goldEarned = CandyGoldValues.GetGoldValue(type, 1);
                    sessionGold += goldEarned;

                    int x = GridOffsetX + pt.X * TileSize + TileSize / 2;
                    int y = GridOffsetY + pt.Y * TileSize + TileSize / 2;
                    SpawnParticles(x, y, GetCandyColor(type));
                }

                if (squareMatches.Count > 0)
                {
                    int centerX = squareMatches[0].X;
                    int centerY = squareMatches[0].Y;
                    CandyType squareType = board[centerY, centerX];

                    int explosionX = GridOffsetX + centerX * TileSize + TileSize / 2;
                    int explosionY = GridOffsetY + centerY * TileSize + TileSize / 2;
                    SpawnExplosionParticles(explosionX, explosionY, GetCandyColor(squareType));

                    int[] cornerX = { centerX - 1, centerX + 2, centerX - 1, centerX + 2 };
                    int[] cornerY = { centerY - 1, centerY - 1, centerY + 2, centerY + 2 };

                    for (int i = 0; i < 4; i++)
                    {
                        int targetX = cornerX[i];
                        int targetY = cornerY[i];

                        if (targetX >= 0 && targetX < Cols && targetY >= 0 && targetY < Rows)
                        {
                            explosionPoints.Add(new Point(targetX, targetY));
                        }
                    }

                    foreach (Point pt in explosionPoints)
                    {
                        if (board[pt.Y, pt.X] != (CandyType)(-1))
                        {
                            GameData.AddPoints(board[pt.Y, pt.X]);
                        }
                    }
                }

                foreach (Point pt in matches)
                {
                    board[pt.Y, pt.X] = (CandyType)(-1);
                }

                foreach (Point pt in explosionPoints)
                {
                    board[pt.Y, pt.X] = (CandyType)(-1);
                }

                this.Invalidate();
                await Task.Delay(200);

                if (GameData.TotalScore >= TargetPointGoal)
                {
                    levelCompleted = true;
                    completionAnimationPhase = 0;
                    this.Invalidate();

                    try
                    {
                        var filter = Builders<UserAccount>.Filter.Eq(u => u.Email, currentUser.Email);
                        var update = Builders<UserAccount>.Update
                            .Set(u => u.HasCompletedTutorial, true)
                            .Inc(u => u.Gold, sessionGold);
                        await usersCollection.UpdateOneAsync(filter, update);

                        currentUser.Gold += sessionGold;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to update tutorial status: {ex.Message}");
                    }

                    await Task.Delay(3000);

                    // Check if MainFrame already exists and refresh it instead of creating new one
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form is MainFrame mainFrame)
                        {
                            mainFrame.RefreshLevelsData();
                            mainFrame.Show();
                            this.Hide();
                            this.Dispose();
                            return;
                        }
                    }

                    // If no MainFrame exists, create a new one
                    MainFrame main = new MainFrame(currentUser, database);
                    main.Show();

                    this.Hide();
                    this.Dispose();
                    return;
                }

                Array values = Enum.GetValues(typeof(CandyType));
                for (int c = 0; c < Cols; c++)
                {
                    for (int r = Rows - 1; r >= 0; r--)
                    {
                        if ((int)board[r, c] == -1)
                        {
                            for (int rAbove = r - 1; rAbove >= 0; rAbove--)
                            {
                                if ((int)board[rAbove, c] != -1)
                                {
                                    board[r, c] = board[rAbove, c];
                                    board[rAbove, c] = (CandyType)(-1);
                                    break;
                                }
                            }

                            if ((int)board[r, c] == -1)
                            {
                                board[r, c] = (CandyType)values.GetValue(rand.Next(values.Length))!;
                            }
                        }
                    }
                }

                this.Invalidate();
                await Task.Delay(250);
            }
        }

        private void SpawnParticles(int x, int y, Color baseColor)
        {
            for (int i = 0; i < 12; i++)
            {
                float speedX = (float)((rand.NextDouble() * 8.0) - 4.0);
                float speedY = (float)((rand.NextDouble() * 8.0) - 4.0);

                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = speedX,
                    SpeedY = speedY,
                    Size = rand.Next(4, 8),
                    Alpha = 255,
                    ParticleColor = baseColor
                });
            }
        }

        private void SpawnExplosionParticles(int x, int y, Color baseColor)
        {
            for (int i = 0; i < 30; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = (float)(rand.NextDouble() * 12.0 + 4.0);
                float speedX = (float)(Math.Cos(angle) * speed);
                float speedY = (float)(Math.Sin(angle) * speed);

                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = speedX,
                    SpeedY = speedY,
                    Size = rand.Next(8, 16),
                    Alpha = 255,
                    ParticleColor = baseColor
                });
            }

            for (int i = 0; i < 15; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = (float)(rand.NextDouble() * 8.0 + 2.0);
                float speedX = (float)(Math.Cos(angle) * speed);
                float speedY = (float)(Math.Sin(angle) * speed);

                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = speedX,
                    SpeedY = speedY,
                    Size = rand.Next(6, 12),
                    Alpha = 255,
                    ParticleColor = Color.White
                });
            }
        }

        private Color GetCandyColor(CandyType candy)
        {
            return candy switch
            {
                CandyType.RedStrawberry => Color.FromArgb(235, 45, 75),
                CandyType.BlueGummy => Color.FromArgb(35, 165, 245),
                CandyType.GreenApple => Color.FromArgb(45, 205, 85),
                CandyType.YellowLemon => Color.FromArgb(255, 215, 35),
                CandyType.PurplePlum => Color.FromArgb(175, 75, 215),
                _ => Color.White
            };
        }

        private void DrawRoundedRectangle(Graphics g, Rectangle rect, int radius, Brush brush)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        private void DrawStar(Graphics g, int x, int y, int size, Color color, int animationPhase)
        {
            float scale = 1.0f + 0.2f * (float)Math.Sin(animationPhase * Math.PI / 30);
            int scaledSize = (int)(size * scale);

            using (SolidBrush starBrush = new SolidBrush(color))
            {
                Point[] starPoints = new Point[10];
                for (int i = 0; i < 10; i++)
                {
                    double angle = i * Math.PI / 5 - Math.PI / 2;
                    double radius = (i % 2 == 0) ? scaledSize : scaledSize / 2;
                    starPoints[i] = new Point(
                        (int)(x + Math.Cos(angle) * radius),
                        (int)(y + Math.Sin(angle) * radius)
                    );
                }
                g.FillPolygon(starBrush, starPoints);
            }
        }

        private void GameLoopTimer_Tick(object? sender, EventArgs e)
        {
            for (int i = burstParticles.Count - 1; i >= 0; i--)
            {
                var p = burstParticles[i];
                p.X += p.SpeedX;
                p.Y += p.SpeedY;
                p.Alpha -= 18;
                p.Alpha = Math.Max(0, p.Alpha);

                if (p.Alpha <= 0)
                    burstParticles.RemoveAt(i);
                else
                    burstParticles[i] = p;
            }

            if (levelCompleted)
            {
                completionAnimationPhase = (completionAnimationPhase + 1) % 120;
                
                // Trigger confetti on completion
                if (completionAnimationPhase == 1 && !showSuccessAnimation)
                {
                    showSuccessAnimation = true;
                    successAnimationPhase = 0;
                    confettiParticles.Clear();
                    
                    for (int i = 0; i < 50; i++)
                    {
                        confettiParticles.Add(new ConfettiParticle
                        {
                            X = this.ClientSize.Width / 2,
                            Y = this.ClientSize.Height / 2,
                            SpeedX = (float)(bgRand.NextDouble() * 10 - 5),
                            SpeedY = (float)(bgRand.NextDouble() * -15 - 5),
                            Rotation = 0,
                            RotationSpeed = (float)(bgRand.NextDouble() * 0.3 - 0.15),
                            Color = CrushItStyleHelper.ParticleColors[bgRand.Next(CrushItStyleHelper.ParticleColors.Length)],
                            Size = bgRand.Next(6, 12),
                            Alpha = 1f
                        });
                    }
                }
            }
            
            // Update confetti
            if (showSuccessAnimation)
            {
                successAnimationPhase++;
                foreach (var confetti in confettiParticles)
                {
                    confetti.X += confetti.SpeedX;
                    confetti.Y += confetti.SpeedY;
                    confetti.SpeedY += 0.15f; // Gravity
                    confetti.Rotation += confetti.RotationSpeed;
                    confetti.Alpha -= 0.008f;
                    confetti.Alpha = Math.Max(0, confetti.Alpha);
                }
                
                confettiParticles.RemoveAll(c => c.Alpha <= 0 || c.Y > this.ClientSize.Height);
                
                if (successAnimationPhase > 180)
                {
                    showSuccessAnimation = false;
                    confettiParticles.Clear();
                }
            }

            pulsePhase++;
            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 120);
            
            // Animate button
            float targetScale = isButtonHovered ? 1.05f : 1f;
            buttonScale += (targetScale - buttonScale) * 0.1f;
            
            float targetDepth = isButtonHovered ? 3f : 0f;
            buttonPressDepth += (targetDepth - buttonPressDepth) * 0.2f;

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            // Use cartoon background from CrushItStyleHelper
            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);
            
            // Draw enhanced title banner
            DrawEnhancedTitleBanner(g);
            
            // Draw enhanced score display
            DrawEnhancedScoreDisplay(g);
            
            // Draw skip button
            DrawSkipButton(g);
            
            // Draw game board with glassmorphism effect
            DrawGameBoard(g);

            if (levelCompleted)
            {
                // Enhanced overlay with glassmorphism
                using (SolidBrush overlay = new SolidBrush(Color.FromArgb(180, 20, 12, 35)))
                {
                    g.FillRectangle(overlay, this.ClientRectangle);
                }

                int bannerWidth = 500;
                int bannerHeight = 100;
                int bannerX = (this.ClientSize.Width - bannerWidth) / 2;
                Rectangle compBanner = new Rectangle(bannerX, 240, bannerWidth, bannerHeight);
                int cornerRadius = 25;

                // Enhanced glow effect
                int glowPulse = (int)(20 * Math.Sin(completionAnimationPhase * Math.PI / 40));
                int glowAlpha = Math.Max(0, Math.Min(255, 80 + glowPulse));
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, 100, 255, 150)))
                {
                    Rectangle glowRect = new Rectangle(compBanner.X - 8, compBanner.Y - 8, compBanner.Width + 16, compBanner.Height + 16);
                    g.FillRoundedRectangle(glow, glowRect, cornerRadius + 8);
                }

                // Shadow
                using (SolidBrush cShadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    Rectangle shadowRect = new Rectangle(compBanner.X + 10, compBanner.Y + 10, compBanner.Width, compBanner.Height);
                    g.FillRoundedRectangle(cShadow, shadowRect, cornerRadius);
                }

                // Main banner with gradient
                Rectangle cInner = new Rectangle(compBanner.X + 6, compBanner.Y + 6, compBanner.Width - 12, compBanner.Height - 12);
                using (LinearGradientBrush cFill = new LinearGradientBrush(cInner, Color.FromArgb(255, 120, 80, 200), Color.FromArgb(255, 80, 50, 160), LinearGradientMode.Vertical))
                {
                    g.FillRoundedRectangle(cFill, cInner, cornerRadius - 4);
                }
                
                // Glassmorphism effect
                using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                    cInner, 
                    Color.FromArgb(50, 255, 255, 255), 
                    Color.FromArgb(25, 255, 255, 255), 
                    LinearGradientMode.Vertical))
                {
                    g.FillRoundedRectangle(glassBrush, cInner, cornerRadius - 4);
                }

                // Highlight
                Rectangle highlightRect = new Rectangle(cInner.X, cInner.Y, cInner.Width, cInner.Height / 2);
                using (LinearGradientBrush cHi = new LinearGradientBrush(highlightRect, Color.FromArgb(150, 255, 200, 230), Color.FromArgb(100, 200, 150, 200), LinearGradientMode.Vertical))
                {
                    g.FillRoundedRectangle(cHi, new Rectangle(cInner.X, cInner.Y, cInner.Width, cInner.Height / 2), cornerRadius - 4);
                }
                
                // Border
                using (Pen borderPen = new Pen(Color.FromArgb(255, 255, 220, 240), 4))
                {
                    g.DrawRoundedRectangle(borderPen, cInner, cornerRadius - 4);
                }

                // Animated stars
                DrawStar(g, compBanner.X + 35, compBanner.Y + 50, 15, Color.FromArgb(255, 255, 215, 0), completionAnimationPhase);
                DrawStar(g, compBanner.Right - 35, compBanner.Y + 50, 15, Color.FromArgb(255, 255, 215, 0), completionAnimationPhase + 30);

                int jumpOffset = (int)(8 * Math.Sin(completionAnimationPhase * Math.PI / 30));

                using (Font compFont = new Font("Comic Sans MS", 28, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        string levelText = "LEVEL";
                        string completedText = "COMPLETED!";

                        float levelY = compBanner.Y + 25 + jumpOffset;
                        float completedY = compBanner.Y + 65 + jumpOffset;

                        CrushItStyleHelper.DrawOutlinedText(g, levelText, compFont, new Rectangle(compBanner.X, (int)levelY, compBanner.Width, 40), Color.White, Color.FromArgb(200, 100, 50, 100), 2, sf);
                        CrushItStyleHelper.DrawOutlinedText(g, completedText, compFont, new Rectangle(compBanner.X, (int)completedY, compBanner.Width, 40), Color.FromArgb(255, 255, 215, 0), Color.FromArgb(200, 100, 50, 100), 2, sf);
                    }
                }
            }

            using (SolidBrush framePen = new SolidBrush(Color.FromArgb(220, 50, 100)))
            {
                g.FillRectangle(framePen, 0, 0, this.ClientSize.Width, 6);
                g.FillRectangle(framePen, 0, 0, 6, this.ClientSize.Height);
                g.FillRectangle(framePen, 0, this.ClientSize.Height - 6, this.ClientSize.Width, 6);
                g.FillRectangle(framePen, this.ClientSize.Width - 6, 0, 6, this.ClientSize.Height);
            }
            
            // Draw confetti on top
            if (showSuccessAnimation)
            {
                DrawConfetti(g);
            }
        }
        
        private void DrawEnhancedTitleBanner(Graphics g)
        {
            Rectangle banner = new Rectangle(50, 15, 500, 60);
            
            // Glassmorphism effect
            using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                banner, 
                Color.FromArgb(60, 255, 255, 255), 
                Color.FromArgb(30, 255, 255, 255), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(glassBrush, banner, 25);
            }

            // Glow effect
            int glowPulse = (int)(10 * Math.Sin(pulsePhase * Math.PI / 40));
            int glowAlpha = Math.Max(0, Math.Min(255, 30 + glowPulse));
            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 255, 200, 100)))
            {
                Rectangle glowRect = new Rectangle(banner.X - 2, banner.Y - 2, banner.Width + 4, banner.Height + 4);
                g.FillRoundedRectangle(glowBrush, glowRect, 27);
            }
            
            // Border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 255, 220, 180), 3))
            {
                g.DrawRoundedRectangle(borderPen, banner, 25);
            }
            
            using (Font titleFont = new Font("Comic Sans MS", 24, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, "TUTORIAL LEVEL", titleFont, banner, Color.White, Color.FromArgb(200, 100, 30), 2, sf);
            }
        }
        
        private void DrawEnhancedScoreDisplay(Graphics g)
        {
            Rectangle scorePanel = new Rectangle(50, 85, 500, 50);
            
            // Glassmorphism panel
            using (LinearGradientBrush panelGradient = new LinearGradientBrush(
                scorePanel, 
                Color.FromArgb(255, 160, 120, 220), 
                Color.FromArgb(255, 120, 80, 190), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(panelGradient, scorePanel, 15);
            }
            
            using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                scorePanel, 
                Color.FromArgb(40, 255, 255, 255), 
                Color.FromArgb(20, 255, 255, 255), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(glassBrush, scorePanel, 15);
            }
            
            using (Pen borderPen = new Pen(Color.FromArgb(255, 100, 60, 160), 3))
            {
                g.DrawRoundedRectangle(borderPen, scorePanel, 15);
            }
            
            using (Font scoreFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string goalText = $"SCORE: {Math.Min(GameData.TotalScore, TargetPointGoal)} / {TargetPointGoal}";
                g.DrawString(goalText, scoreFont, new SolidBrush(Color.White), scorePanel, sf);
            }
            
            Rectangle goldPanel = new Rectangle(570, 85, 200, 50);
            
            using (LinearGradientBrush goldGradient = new LinearGradientBrush(
                goldPanel, 
                Color.FromArgb(255, 255, 215, 50), 
                Color.FromArgb(255, 255, 180, 30), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(goldGradient, goldPanel, 15);
            }
            
            using (Pen goldBorder = new Pen(Color.FromArgb(255, 255, 220, 180), 3))
            {
                g.DrawRoundedRectangle(goldBorder, goldPanel, 15);
            }
            
            using (Font goldFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string goldText = $"GOLD: {sessionGold}";
                g.DrawString(goldText, goldFont, new SolidBrush(Color.FromArgb(100, 80, 40)), goldPanel, sf);
            }
        }
        
        private void DrawSkipButton(Graphics g)
        {
            // Calculate scaled rectangle with 3D press effect
            int scaledWidth = (int)(skipButtonRect.Width * buttonScale);
            int scaledHeight = (int)(skipButtonRect.Height * buttonScale);
            int scaledX = skipButtonRect.X + (skipButtonRect.Width - scaledWidth) / 2;
            int scaledY = skipButtonRect.Y + (skipButtonRect.Height - scaledHeight) / 2 + (int)buttonPressDepth;
            Rectangle scaledRect = new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);
            
            // 3D shadow for press effect
            if (buttonPressDepth > 0.5f)
            {
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                {
                    Rectangle shadowRect = new Rectangle(scaledRect.X, scaledRect.Y + (int)buttonPressDepth, scaledRect.Width, scaledRect.Height);
                    g.FillRoundedRectangle(shadowBrush, shadowRect, 12);
                }
            }
            
            // Button gradient
            Color buttonColor = isButtonHovered ? Color.FromArgb(255, 255, 120, 180) : Color.FromArgb(255, 255, 80, 150);
            Color buttonColor2 = isButtonHovered ? Color.FromArgb(255, 255, 80, 140) : Color.FromArgb(255, 255, 50, 110);

            using (LinearGradientBrush buttonBrush = new LinearGradientBrush(
                scaledRect, buttonColor, buttonColor2, LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(buttonBrush, scaledRect, 12);
            }

            // Glow effect on hover
            if (isButtonHovered)
            {
                int glowPulse = (int)(15 * Math.Sin(pulsePhase * Math.PI / 30));
                int glowAlpha = Math.Max(0, Math.Min(255, 40 + glowPulse));
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 255, 180, 220)))
                {
                    Rectangle glowRect = new Rectangle(scaledRect.X - 3, scaledRect.Y - 3, scaledRect.Width + 6, scaledRect.Height + 6);
                    g.FillRoundedRectangle(glowBrush, glowRect, 15);
                }
            }

            using (Pen buttonBorder = new Pen(Color.FromArgb(255, 255, 220, 240), 3))
            {
                g.DrawRoundedRectangle(buttonBorder, scaledRect, 12);
            }
            
            // Inner highlight
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            {
                Rectangle highlightRect = new Rectangle(scaledRect.X + 4, scaledRect.Y + 4, scaledRect.Width - 8, 6);
                g.FillRoundedRectangle(highlight, highlightRect, 8);
            }

            // Button text
            Rectangle textRect = scaledRect;
            if (buttonPressDepth > 0.5f)
            {
                textRect = new Rectangle(scaledRect.X, scaledRect.Y + (int)(buttonPressDepth * 0.5f), scaledRect.Width, scaledRect.Height);
            }
            
            using (Font buttonFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, "SKIP", buttonFont, textRect, Color.White, Color.FromArgb(200, 100, 50, 100), 2, sf);
            }
        }
        
        private void DrawGameBoard(Graphics g)
        {
            int gridTotalWidth = Cols * TileSize;
            int gridTotalHeight = Rows * TileSize;
            Rectangle boardRect = new Rectangle(GridOffsetX - 10, GridOffsetY - 10, gridTotalWidth + 20, gridTotalHeight + 20);

            // Glassmorphism board background
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                Rectangle shadowRect = new Rectangle(boardRect.X + 8, boardRect.Y + 8, boardRect.Width, boardRect.Height);
                g.FillRoundedRectangle(shadow, shadowRect, 20);
            }

            using (LinearGradientBrush boardGradient = new LinearGradientBrush(
                boardRect, 
                Color.FromArgb(255, 70, 50, 100), 
                Color.FromArgb(255, 50, 35, 80), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(boardGradient, boardRect, 20);
            }
            
            // Glassmorphism effect
            using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                boardRect, 
                Color.FromArgb(40, 255, 255, 255), 
                Color.FromArgb(20, 255, 255, 255), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(glassBrush, boardRect, 20);
            }
            
            // Inner highlight
            Rectangle innerRect = new Rectangle(boardRect.X + 4, boardRect.Y + 4, boardRect.Width - 8, boardRect.Height - 8);
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            {
                Rectangle highlightRect = new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 8);
                g.FillRoundedRectangle(highlight, highlightRect, 16);
            }
            
            // Border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 100, 60, 160), 4))
            {
                g.DrawRoundedRectangle(borderPen, boardRect, 20);
            }

            // Draw grid cells
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    Rectangle cellRect = new Rectangle(
                        GridOffsetX + c * TileSize - 2, 
                        GridOffsetY + r * TileSize - 2, 
                        TileSize - 4, 
                        TileSize - 4);
                    
                    // Cell background
                    using (SolidBrush cellBrush = new SolidBrush(Color.FromArgb(30, 20, 40)))
                    {
                        g.FillRoundedRectangle(cellBrush, cellRect, 8);
                    }
                }
            }

            // Draw candies
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    if ((int)board[r, c] == -1) continue;

                    float drawX = GridOffsetX + (c * TileSize);
                    float drawY = GridOffsetY + (r * TileSize);

                    if (inputController.IsAnimating)
                    {
                        if (inputController.SwapTileA.X == c && inputController.SwapTileA.Y == r)
                        {
                            drawX += inputController.AnimOffsetA.X;
                            drawY += inputController.AnimOffsetA.Y;
                        }
                        else if (inputController.SwapTileB.X == c && inputController.SwapTileB.Y == r)
                        {
                            drawX += inputController.AnimOffsetB.X;
                            drawY += inputController.AnimOffsetB.Y;
                        }
                    }

                    bool isSelected = inputController.SelectedTile.HasValue &&
                                      inputController.SelectedTile.Value.X == c &&
                                      inputController.SelectedTile.Value.Y == r;

                    DrawPixelTileAndCandy(g, board[r, c], (int)drawX, (int)drawY, TileSize - 4, isSelected);
                }
            }

            // Draw particles
            foreach (var p in burstParticles)
            {
                int clampedAlpha = Math.Max(0, Math.Min(255, (int)p.Alpha));
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(clampedAlpha, p.ParticleColor)))
                {
                    g.FillEllipse(pb, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
                }
            }
        }
        
        private void DrawConfetti(Graphics g)
        {
            foreach (var confetti in confettiParticles)
            {
                GraphicsState gstate = g.Save();
                g.TranslateTransform(confetti.X, confetti.Y);
                g.RotateTransform(confetti.Rotation * 180 / (float)Math.PI);
                
                int clampedAlpha = Math.Max(0, Math.Min(255, (int)(255 * confetti.Alpha)));
                using (SolidBrush confettiBrush = new SolidBrush(Color.FromArgb(clampedAlpha, confetti.Color)))
                {
                    g.FillRectangle(confettiBrush, -confetti.Size / 2, -confetti.Size / 2, confetti.Size, confetti.Size);
                }
                
                g.Restore(gstate);
            }
        }

        private void DrawPixelTileAndCandy(Graphics g, CandyType candy, int x, int y, int size, bool isSelected)
        {
            Color mainColor = Color.Red;
            Color darkColor = Color.DarkRed;
            Color lightColor = Color.Pink;
            Color glowColor = Color.White;

            switch (candy)
            {
                case CandyType.RedStrawberry:
                    mainColor = Color.FromArgb(235, 45, 75);
                    darkColor = Color.FromArgb(135, 10, 35);
                    lightColor = Color.FromArgb(255, 140, 160);
                    glowColor = Color.FromArgb(255, 100, 50, 80);
                    break;
                case CandyType.BlueGummy:
                    mainColor = Color.FromArgb(35, 165, 245);
                    darkColor = Color.FromArgb(10, 75, 155);
                    lightColor = Color.FromArgb(150, 225, 255);
                    glowColor = Color.FromArgb(255, 50, 100, 180);
                    break;
                case CandyType.GreenApple:
                    mainColor = Color.FromArgb(45, 205, 85);
                    darkColor = Color.FromArgb(15, 105, 35);
                    lightColor = Color.FromArgb(150, 255, 175);
                    glowColor = Color.FromArgb(255, 50, 150, 80);
                    break;
                case CandyType.YellowLemon:
                    mainColor = Color.FromArgb(255, 215, 35);
                    darkColor = Color.FromArgb(170, 125, 0);
                    lightColor = Color.FromArgb(255, 245, 160);
                    glowColor = Color.FromArgb(255, 200, 150, 50);
                    break;
                case CandyType.PurplePlum:
                    mainColor = Color.FromArgb(175, 75, 215);
                    darkColor = Color.FromArgb(95, 20, 125);
                    lightColor = Color.FromArgb(225, 155, 255);
                    glowColor = Color.FromArgb(255, 100, 50, 180);
                    break;
            }

            // Subtle floating animation
            float floatOffset = (float)(2 * Math.Sin(pulsePhase * 0.05 + (x + y) * 0.1));
            int animatedY = y + (int)floatOffset;

            // Enhanced selection glow
            if (isSelected)
            {
                int glowPulse = (int)(15 * Math.Sin(pulsePhase * Math.PI / 20));
                int glowAlpha = Math.Max(0, Math.Min(255, 60 + glowPulse));
                using (SolidBrush selectionGlow = new SolidBrush(Color.FromArgb(glowAlpha, 255, 255, 100)))
                {
                    Rectangle glowRect = new Rectangle(x - 4, animatedY - 4, size + 8, size + 8);
                    g.FillRoundedRectangle(selectionGlow, glowRect, 12);
                }
                
                using (Pen selectionBorder = new Pen(Color.FromArgb(255, 255, 255, 200), 3))
                {
                    Rectangle borderRect = new Rectangle(x + 1, animatedY + 1, size - 2, size - 2);
                    g.DrawRoundedRectangle(selectionBorder, borderRect, 8);
                }
            }

            // Background
            using (SolidBrush b = new SolidBrush(isSelected ? Color.FromArgb(50, 50, 50) : Color.Black))
                g.FillRectangle(b, x, animatedY, size, size);

            // Candy glow effect
            int candyGlowPulse = (int)(8 * Math.Sin(pulsePhase * Math.PI / 30 + (x + y) * 0.2));
            using (SolidBrush candyGlow = new SolidBrush(Color.FromArgb(20 + candyGlowPulse, glowColor)))
            {
                Rectangle glowRect = new Rectangle(x - 2, animatedY - 2, size + 4, size + 4);
                g.FillRoundedRectangle(candyGlow, glowRect, 10);
            }

            Rectangle inner = new Rectangle(x + 3, animatedY + 3, size - 6, size - 6);
            
            // Gradient fill for candy body
            using (LinearGradientBrush candyGradient = new LinearGradientBrush(
                inner, mainColor, Color.FromArgb(200, darkColor.R, darkColor.G, darkColor.B), LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(candyGradient, inner, 8);
            }

            // Enhanced highlights
            using (SolidBrush b = new SolidBrush(Color.FromArgb(230, lightColor)))
            {
                g.FillRoundedRectangle(b, new Rectangle(inner.X, inner.Y, inner.Width, 5), 4);
                g.FillRoundedRectangle(b, new Rectangle(inner.X, inner.Y, 5, inner.Height), 4);
            }

            // Enhanced shadows
            using (SolidBrush b = new SolidBrush(Color.FromArgb(180, darkColor)))
            {
                g.FillRoundedRectangle(b, new Rectangle(inner.X, inner.Y + inner.Height - 5, inner.Width, 5), 4);
                g.FillRoundedRectangle(b, new Rectangle(inner.X + inner.Width - 5, inner.Y, 5, inner.Height), 4);
            }

            int cx = x + (size / 2);
            int cy = animatedY + (size / 2);

            switch (candy)
            {
                case CandyType.RedStrawberry:
                    DrawPixelStrawberry(g, cx, cy);
                    break;
                case CandyType.BlueGummy:
                    DrawPixelBlueGummy(g, cx, cy);
                    break;
                case CandyType.GreenApple:
                    DrawPixelGreenApple(g, cx, cy);
                    break;
                case CandyType.YellowLemon:
                    DrawPixelYellowLemon(g, cx, cy);
                    break;
                case CandyType.PurplePlum:
                    DrawPixelPurplePlum(g, cx, cy);
                    break;
            }
            
            // Top shine overlay
            DrawCandyShine(g, inner, pulsePhase);
        }
        
        private void DrawCandyShine(Graphics g, Rectangle rect, int phase)
        {
            // Moving shine effect
            int shineX = rect.X + (int)(rect.Width * 0.2 + 5 * Math.Sin(phase * 0.05));
            int shineY = rect.Y + (int)(rect.Height * 0.3);
            
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
            {
                g.FillEllipse(shine, shineX, shineY, 8, 6);
            }
        }

        private void DrawPixelStrawberry(Graphics g, int cx, int cy)
        {
            // Enhanced leaf with gradient effect
            using (SolidBrush leaf = new SolidBrush(Color.FromArgb(60, 210, 80)))
            {
                g.FillRectangle(leaf, cx - 6, cy - 10, 12, 3);
                g.FillRectangle(leaf, cx - 8, cy - 9, 4, 3);
                g.FillRectangle(leaf, cx + 4, cy - 9, 4, 3);
            }
            
            // Leaf highlight
            using (SolidBrush leafHighlight = new SolidBrush(Color.FromArgb(100, 240, 120)))
            {
                g.FillRectangle(leafHighlight, cx - 6, cy - 10, 4, 2);
            }

            // Enhanced body with more depth
            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 90, 120)))
            {
                g.FillRectangle(body, cx - 8, cy - 7, 16, 8);
                g.FillRectangle(body, cx - 6, cy + 1, 12, 6);
                g.FillRectangle(body, cx - 3, cy + 7, 6, 4);
            }
            
            // Body highlight
            using (SolidBrush bodyHighlight = new SolidBrush(Color.FromArgb(255, 140, 170)))
            {
                g.FillRectangle(bodyHighlight, cx - 7, cy - 6, 4, 4);
                g.FillRectangle(bodyHighlight, cx - 5, cy - 2, 3, 3);
            }

            // Enhanced shadow with more detail
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(180, 30, 60)))
            {
                g.FillRectangle(shadow, cx + 5, cy - 6, 3, 6);
                g.FillRectangle(shadow, cx + 3, cy + 1, 3, 5);
                g.FillRectangle(shadow, cx, cy + 7, 3, 3);
            }

            // Enhanced seeds with glow
            using (SolidBrush seed = new SolidBrush(Color.FromArgb(255, 250, 140)))
            {
                g.FillRectangle(seed, cx - 4, cy - 4, 2, 2);
                g.FillRectangle(seed, cx + 2, cy - 4, 2, 2);
                g.FillRectangle(seed, cx - 1, cy, 2, 2);
                g.FillRectangle(seed, cx - 3, cy + 4, 2, 2);
            }
            
            // Seed shine
            using (SolidBrush seedShine = new SolidBrush(Color.FromArgb(255, 255, 200)))
            {
                g.FillRectangle(seedShine, cx - 4, cy - 4, 1, 1);
                g.FillRectangle(seedShine, cx + 2, cy - 4, 1, 1);
            }

            // Enhanced gloss with animated shine
            int glossOffset = (int)(2 * Math.Sin(pulsePhase * 0.1));
            using (SolidBrush gloss = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.FillRectangle(gloss, cx - 6 + glossOffset, cy - 6, 3, 4);
            }
            
            // Extra highlight dot
            using (SolidBrush extraGloss = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            {
                g.FillEllipse(extraGloss, cx + 2, cy - 8, 2, 2);
            }
        }

        private void DrawPixelBlueGummy(Graphics g, int cx, int cy)
        {
            // Enhanced body with gradient-like effect
            using (SolidBrush body = new SolidBrush(Color.FromArgb(120, 220, 255)))
            {
                g.FillRectangle(body, cx - 4, cy - 10, 8, 3);
                g.FillRectangle(body, cx - 8, cy - 7, 16, 5);
                g.FillRectangle(body, cx - 10, cy - 2, 20, 6);
                g.FillRectangle(body, cx - 8, cy + 4, 16, 5);
                g.FillRectangle(body, cx - 4, cy + 9, 8, 3);
            }
            
            // Body highlight
            using (SolidBrush bodyHighlight = new SolidBrush(Color.FromArgb(180, 240, 255)))
            {
                g.FillRectangle(bodyHighlight, cx - 8, cy - 7, 6, 3);
                g.FillRectangle(bodyHighlight, cx - 10, cy - 2, 8, 4);
            }

            // Enhanced dark areas
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(20, 100, 200)))
            {
                g.FillRectangle(dark, cx + 4, cy - 7, 4, 4);
                g.FillRectangle(dark, cx + 6, cy - 2, 4, 6);
                g.FillRectangle(dark, cx + 4, cy + 4, 4, 4);
                g.FillRectangle(dark, cx - 2, cy + 9, 6, 3);
            }

            // Enhanced shine with animation
            int shineOffset = (int)(2 * Math.Sin(pulsePhase * 0.08 + cx * 0.1));
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(240, 255, 255)))
            {
                g.FillRectangle(shine, cx - 6 + shineOffset, cy - 6, 4, 4);
                g.FillRectangle(shine, cx - 8 + shineOffset, cy - 1, 4, 4);
                g.FillRectangle(shine, cx - 6 + shineOffset, cy + 4, 3, 3);
            }
            
            // Gummy bear ear detail
            using (SolidBrush ear = new SolidBrush(Color.FromArgb(100, 200, 240)))
            {
                g.FillRectangle(ear, cx - 11, cy - 3, 3, 4);
                g.FillRectangle(ear, cx + 8, cy - 3, 3, 4);
            }
        }

        private void DrawPixelGreenApple(Graphics g, int cx, int cy)
        {
            // Enhanced stem with gradient
            using (SolidBrush stem = new SolidBrush(Color.FromArgb(140, 90, 40)))
            {
                g.FillRectangle(stem, cx - 1, cy - 11, 3, 4);
            }
            
            // Stem highlight
            using (SolidBrush stemHighlight = new SolidBrush(Color.FromArgb(180, 120, 60)))
            {
                g.FillRectangle(stemHighlight, cx - 1, cy - 11, 1, 2);
            }

            // Enhanced leaf with more detail
            using (SolidBrush leaf = new SolidBrush(Color.FromArgb(130, 245, 80)))
            {
                g.FillRectangle(leaf, cx + 2, cy - 11, 4, 3);
                g.FillRectangle(leaf, cx + 3, cy - 10, 2, 4);
            }
            
            // Leaf vein
            using (SolidBrush leafVein = new SolidBrush(Color.FromArgb(80, 200, 50)))
            {
                g.FillRectangle(leafVein, cx + 4, cy - 10, 1, 3);
            }

            // Enhanced body with gradient effect
            using (SolidBrush body = new SolidBrush(Color.FromArgb(130, 240, 80)))
            {
                g.FillRectangle(body, cx - 9, cy - 7, 18, 12);
                g.FillRectangle(body, cx - 7, cy + 5, 14, 5);
            }
            
            // Body highlight
            using (SolidBrush bodyHighlight = new SolidBrush(Color.FromArgb(180, 255, 120)))
            {
                g.FillRectangle(bodyHighlight, cx - 8, cy - 6, 4, 6);
                g.FillRectangle(bodyHighlight, cx - 6, cy - 3, 3, 4);
            }

            // Enhanced shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(30, 130, 50)))
            {
                g.FillRectangle(shadow, cx + 5, cy - 6, 4, 10);
                g.FillRectangle(shadow, cx + 3, cy + 5, 4, 4);
                g.FillRectangle(shadow, cx - 2, cy + 8, 4, 2);
            }

            // Animated gloss
            int glossOffset = (int)(2 * Math.Sin(pulsePhase * 0.09 + cy * 0.1));
            using (SolidBrush gloss = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            {
                g.FillRectangle(gloss, cx - 6 + glossOffset, cy - 5, 3, 5);
            }
            
            // Apple dimple detail
            using (SolidBrush dimple = new SolidBrush(Color.FromArgb(80, 200, 50)))
            {
                g.FillRectangle(dimple, cx, cy + 2, 2, 2);
            }
            
            // Extra shine
            using (SolidBrush extraShine = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            {
                g.FillEllipse(extraShine, cx - 4, cy - 8, 2, 2);
            }
        }

        private void DrawPixelYellowLemon(Graphics g, int cx, int cy)
        {
            // Enhanced body with gradient effect
            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 250, 100)))
            {
                g.FillRectangle(body, cx - 3, cy - 10, 6, 3);
                g.FillRectangle(body, cx - 7, cy - 7, 14, 4);
                g.FillRectangle(body, cx - 9, cy - 3, 18, 7);
                g.FillRectangle(body, cx - 7, cy + 4, 14, 4);
                g.FillRectangle(body, cx - 3, cy + 8, 6, 3);
            }
            
            // Body highlight
            using (SolidBrush bodyHighlight = new SolidBrush(Color.FromArgb(255, 255, 180)))
            {
                g.FillRectangle(bodyHighlight, cx - 7, cy - 6, 6, 3);
                g.FillRectangle(bodyHighlight, cx - 8, cy - 2, 8, 4);
            }

            // Enhanced shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(210, 150, 20)))
            {
                g.FillRectangle(shadow, cx + 4, cy - 3, 5, 6);
                g.FillRectangle(shadow, cx + 2, cy + 4, 5, 3);
                g.FillRectangle(shadow, cx - 1, cy + 8, 3, 2);
            }

            // Enhanced line detail
            using (SolidBrush line = new SolidBrush(Color.FromArgb(255, 200, 40)))
            {
                g.FillRectangle(line, cx - 5, cy, 10, 2);
            }
            
            // Line highlight
            using (SolidBrush lineHighlight = new SolidBrush(Color.FromArgb(255, 230, 100)))
            {
                g.FillRectangle(lineHighlight, cx - 5, cy, 4, 1);
            }

            // Animated gloss
            int glossOffset = (int)(2 * Math.Sin(pulsePhase * 0.07 + cx * 0.1));
            using (SolidBrush gloss = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            {
                g.FillRectangle(gloss, cx - 5 + glossOffset, cy - 5, 3, 3);
            }
            
            // Lemon texture dots
            using (SolidBrush texture = new SolidBrush(Color.FromArgb(255, 220, 60)))
            {
                g.FillRectangle(texture, cx - 3, cy - 2, 1, 1);
                g.FillRectangle(texture, cx + 2, cy + 1, 1, 1);
                g.FillRectangle(texture, cx - 1, cy + 3, 1, 1);
            }
            
            // Extra shine
            using (SolidBrush extraShine = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
            {
                g.FillEllipse(extraShine, cx + 2, cy - 7, 2, 2);
            }
            
            // Lemon tip detail
            using (SolidBrush tip = new SolidBrush(Color.FromArgb(255, 200, 30)))
            {
                g.FillRectangle(tip, cx - 1, cy - 9, 2, 2);
                g.FillRectangle(tip, cx - 1, cy + 7, 2, 2);
            }
        }

        private void DrawPixelPurplePlum(Graphics g, int cx, int cy)
        {
            // Enhanced body with gradient effect
            using (SolidBrush body = new SolidBrush(Color.FromArgb(230, 130, 255)))
            {
                g.FillRectangle(body, cx - 6, cy - 9, 12, 3);
                g.FillRectangle(body, cx - 9, cy - 6, 18, 12);
                g.FillRectangle(body, cx - 6, cy + 6, 12, 3);
            }
            
            // Body highlight
            using (SolidBrush bodyHighlight = new SolidBrush(Color.FromArgb(245, 170, 255)))
            {
                g.FillRectangle(bodyHighlight, cx - 8, cy - 5, 6, 6);
                g.FillRectangle(bodyHighlight, cx - 6, cy - 2, 4, 4);
            }

            // Enhanced shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(110, 25, 160)))
            {
                g.FillRectangle(shadow, cx + 5, cy - 5, 4, 10);
                g.FillRectangle(shadow, cx + 2, cy + 5, 4, 3);
            }

            // Enhanced swirl with animation
            int swirlOffset = (int)(1 * Math.Sin(pulsePhase * 0.05));
            using (SolidBrush swirl = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
            {
                g.FillRectangle(swirl, cx - 5 + swirlOffset, cy - 5, 3, 3);
                g.FillRectangle(swirl, cx - 2 + swirlOffset, cy - 2, 4, 4);
                g.FillRectangle(swirl, cx + 2 + swirlOffset, cy + 2, 3, 3);
            }
            
            // Swirl glow
            using (SolidBrush swirlGlow = new SolidBrush(Color.FromArgb(100, 200, 150, 255)))
            {
                g.FillRectangle(swirlGlow, cx - 6 + swirlOffset, cy - 6, 5, 5);
                g.FillRectangle(swirlGlow, cx + 1 + swirlOffset, cy + 1, 5, 5);
            }

            // Animated gloss
            int glossOffset = (int)(2 * Math.Sin(pulsePhase * 0.06 + (cx + cy) * 0.05));
            using (SolidBrush gloss = new SolidBrush(Color.FromArgb(250, 220, 255)))
            {
                g.FillRectangle(gloss, cx - 6 + glossOffset, cy - 7, 5, 2);
            }
            
            // Extra shine
            using (SolidBrush extraShine = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.FillEllipse(extraShine, cx - 3, cy - 7, 2, 2);
                g.FillEllipse(extraShine, cx + 4, cy + 3, 2, 2);
            }
            
            // Plum texture dots
            using (SolidBrush texture = new SolidBrush(Color.FromArgb(200, 100, 230)))
            {
                g.FillRectangle(texture, cx - 2, cy - 3, 1, 1);
                g.FillRectangle(texture, cx + 3, cy, 1, 1);
                g.FillRectangle(texture, cx - 1, cy + 4, 1, 1);
            }
        }
    }
}


