using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver;
using NAudio.Wave;
using CrushIt.Data;
using CrushIt.Core;

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
        private readonly List<StyleParticle> backgroundParticles = new List<StyleParticle>();
        private readonly Random bgRand = new Random();

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

            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(bgRand, 30, 890, 80, 530));
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Tutorial Level";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.FormClosed += (s, e) => Application.Exit();
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };
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

            _ = Task.Run(() =>
            {
                try
                {
                    string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "SwipeSound.mp3");
                    if (File.Exists(soundPath))
                    {
                        using (var audioFile = new AudioFileReader(soundPath))
                        using (var outputDevice = new WaveOutEvent())
                        {
                            outputDevice.Init(audioFile);
                            outputDevice.Play();
                            while (outputDevice.PlaybackState == PlaybackState.Playing)
                            {
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                    }
                }
                catch
                {
                }
            });

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

                if (p.Alpha <= 0)
                    burstParticles.RemoveAt(i);
                else
                    burstParticles[i] = p;
            }

            if (levelCompleted)
            {
                completionAnimationPhase = (completionAnimationPhase + 1) % 120;
            }

            pulsePhase++;
            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 120);

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(20, 12, 28)))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            Rectangle banner = new Rectangle(50, 15, 436, 60);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(10, 5, 15)))
                g.FillRectangle(shadow, new Rectangle(banner.X + 6, banner.Y + 6, banner.Width, banner.Height));

            using (SolidBrush bBorder = new SolidBrush(Color.Black))
                g.FillRectangle(bBorder, banner);

            Rectangle bInner = new Rectangle(banner.X + 4, banner.Y + 4, banner.Width - 8, banner.Height - 8);
            using (SolidBrush bFill = new SolidBrush(Color.FromArgb(220, 50, 100)))
                g.FillRectangle(bFill, bInner);

            using (SolidBrush bHi = new SolidBrush(Color.FromArgb(255, 130, 170)))
            {
                g.FillRectangle(bHi, bInner.X, bInner.Y, bInner.Width, 4);
                g.FillRectangle(bHi, bInner.X, bInner.Y, 4, bInner.Height);
            }

            using (Font titleFont = new Font("Courier New", 18, FontStyle.Bold))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("TUTORIAL LEVEL", titleFont, Brushes.Black, new RectangleF(banner.X + 3, banner.Y + 3, banner.Width, banner.Height), sf);
                    g.DrawString("TUTORIAL LEVEL", titleFont, Brushes.Yellow, new RectangleF(banner.X, banner.Y, banner.Width, banner.Height), sf);
                }
            }

            using (Font subFont = new Font("Courier New", 11, FontStyle.Bold))
            {
                string goalText = $"SCORE: {Math.Min(GameData.TotalScore, TargetPointGoal)} / {TargetPointGoal} PTS";
                g.DrawString(goalText, subFont, Brushes.Black, new RectangleF(2, 87, 550, 30), new StringFormat { Alignment = StringAlignment.Center });
                g.DrawString(goalText, subFont, Brushes.Cyan, new RectangleF(0, 85, 550, 30), new StringFormat { Alignment = StringAlignment.Center });

                string goldText = $"GOLD: {sessionGold}";
                g.DrawString(goldText, subFont, Brushes.Black, new RectangleF(2, 102, 550, 30), new StringFormat { Alignment = StringAlignment.Center });
                g.DrawString(goldText, subFont, Brushes.Gold, new RectangleF(0, 100, 550, 30), new StringFormat { Alignment = StringAlignment.Center });
            }

            int gridTotalWidth = Cols * TileSize;
            int gridTotalHeight = Rows * TileSize;
            Rectangle boardRect = new Rectangle(GridOffsetX - 10, GridOffsetY - 10, gridTotalWidth + 20, gridTotalHeight + 20);

            using (SolidBrush bShadow = new SolidBrush(Color.FromArgb(10, 5, 15)))
                g.FillRectangle(bShadow, new Rectangle(boardRect.X + 8, boardRect.Y + 8, boardRect.Width, boardRect.Height));

            using (SolidBrush bPen = new SolidBrush(Color.Black))
                g.FillRectangle(bPen, boardRect);

            Rectangle boardInner = new Rectangle(boardRect.X + 6, boardRect.Y + 6, boardRect.Width - 12, boardRect.Height - 12);
            using (SolidBrush gridBg = new SolidBrush(Color.FromArgb(35, 20, 48)))
                g.FillRectangle(gridBg, boardInner);

            using (SolidBrush gHi = new SolidBrush(Color.FromArgb(80, 50, 100)))
            {
                g.FillRectangle(gHi, boardInner.X, boardInner.Y, boardInner.Width, 4);
                g.FillRectangle(gHi, boardInner.X, boardInner.Y, 4, boardInner.Height);
            }

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

            foreach (var p in burstParticles)
            {
                using (SolidBrush pb = new SolidBrush(Color.FromArgb((int)p.Alpha, p.ParticleColor)))
                {
                    g.FillRectangle(pb, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
                }
            }

            if (levelCompleted)
            {
                using (SolidBrush overlay = new SolidBrush(Color.FromArgb(180, 10, 5, 20)))
                {
                    g.FillRectangle(overlay, this.ClientRectangle);
                }

                int bannerWidth = 500;
                int bannerHeight = 90;
                int bannerX = (this.ClientSize.Width - bannerWidth) / 2;
                Rectangle compBanner = new Rectangle(bannerX, 240, bannerWidth, bannerHeight);
                int cornerRadius = 20;

                using (SolidBrush glow = new SolidBrush(Color.FromArgb(100, 255, 100, 200)))
                {
                    DrawRoundedRectangle(g, new Rectangle(compBanner.X - 5, compBanner.Y - 5, compBanner.Width + 10, compBanner.Height + 10), cornerRadius + 5, glow);
                }

                using (SolidBrush cShadow = new SolidBrush(Color.FromArgb(10, 5, 15)))
                {
                    DrawRoundedRectangle(g, new Rectangle(compBanner.X + 8, compBanner.Y + 8, compBanner.Width, compBanner.Height), cornerRadius, cShadow);
                }

                using (SolidBrush cBorder = new SolidBrush(Color.Black))
                {
                    DrawRoundedRectangle(g, compBanner, cornerRadius, cBorder);
                }

                Rectangle cInner = new Rectangle(compBanner.X + 6, compBanner.Y + 6, compBanner.Width - 12, compBanner.Height - 12);
                using (LinearGradientBrush cFill = new LinearGradientBrush(cInner, Color.FromArgb(255, 100, 50, 180), Color.FromArgb(255, 60, 30, 140), LinearGradientMode.Vertical))
                {
                    DrawRoundedRectangle(g, cInner, cornerRadius - 4, cFill);
                }

                Rectangle highlightRect = new Rectangle(cInner.X, cInner.Y, cInner.Width, cInner.Height / 2);
                using (LinearGradientBrush cHi = new LinearGradientBrush(highlightRect, Color.FromArgb(200, 255, 150, 220), Color.FromArgb(150, 200, 100, 180), LinearGradientMode.Vertical))
                {
                    DrawRoundedRectangle(g, new Rectangle(cInner.X, cInner.Y, cInner.Width, cInner.Height / 2), cornerRadius - 4, cHi);
                }

                DrawStar(g, compBanner.X + 30, compBanner.Y + 45, 12, Color.FromArgb(255, 255, 215, 0), completionAnimationPhase);
                DrawStar(g, compBanner.Right - 30, compBanner.Y + 45, 12, Color.FromArgb(255, 255, 215, 0), completionAnimationPhase + 30);

                int jumpOffset = (int)(6 * Math.Sin(completionAnimationPhase * Math.PI / 30));

                using (Font compFont = new Font("Arial Black", 26, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        string levelText = "LEVEL";
                        string completedText = "COMPLETED!";

                        float levelY = compBanner.Y + 22 + jumpOffset;
                        float completedY = compBanner.Y + 58 + jumpOffset;

                        g.DrawString(levelText, compFont, new SolidBrush(Color.FromArgb(150, 255, 100, 200)), new RectangleF(compBanner.X + 3, levelY + 3, compBanner.Width, 40), sf);
                        g.DrawString(completedText, compFont, new SolidBrush(Color.FromArgb(150, 255, 200, 100)), new RectangleF(compBanner.X + 3, completedY + 3, compBanner.Width, 40), sf);

                        g.DrawString(levelText, compFont, Brushes.Black, new RectangleF(compBanner.X + 2, levelY + 2, compBanner.Width, 40), sf);
                        g.DrawString(completedText, compFont, Brushes.Black, new RectangleF(compBanner.X + 2, completedY + 2, compBanner.Width, 40), sf);

                        g.DrawString(levelText, compFont, Brushes.White, new RectangleF(compBanner.X, levelY, compBanner.Width, 40), sf);
                        g.DrawString(completedText, compFont, new SolidBrush(Color.FromArgb(255, 255, 215, 0)), new RectangleF(compBanner.X, completedY, compBanner.Width, 40), sf);
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
        }

        private void DrawPixelTileAndCandy(Graphics g, CandyType candy, int x, int y, int size, bool isSelected)
        {
            Color mainColor = Color.Red;
            Color darkColor = Color.DarkRed;
            Color lightColor = Color.Pink;

            switch (candy)
            {
                case CandyType.RedStrawberry:
                    mainColor = Color.FromArgb(235, 45, 75);
                    darkColor = Color.FromArgb(135, 10, 35);
                    lightColor = Color.FromArgb(255, 140, 160);
                    break;
                case CandyType.BlueGummy:
                    mainColor = Color.FromArgb(35, 165, 245);
                    darkColor = Color.FromArgb(10, 75, 155);
                    lightColor = Color.FromArgb(150, 225, 255);
                    break;
                case CandyType.GreenApple:
                    mainColor = Color.FromArgb(45, 205, 85);
                    darkColor = Color.FromArgb(15, 105, 35);
                    lightColor = Color.FromArgb(150, 255, 175);
                    break;
                case CandyType.YellowLemon:
                    mainColor = Color.FromArgb(255, 215, 35);
                    darkColor = Color.FromArgb(170, 125, 0);
                    lightColor = Color.FromArgb(255, 245, 160);
                    break;
                case CandyType.PurplePlum:
                    mainColor = Color.FromArgb(175, 75, 215);
                    darkColor = Color.FromArgb(95, 20, 125);
                    lightColor = Color.FromArgb(225, 155, 255);
                    break;
            }

            using (SolidBrush b = new SolidBrush(isSelected ? Color.White : Color.Black))
                g.FillRectangle(b, x, y, size, size);

            Rectangle inner = new Rectangle(x + 3, y + 3, size - 6, size - 6);
            using (SolidBrush b = new SolidBrush(mainColor))
                g.FillRectangle(b, inner);

            using (SolidBrush b = new SolidBrush(lightColor))
            {
                g.FillRectangle(b, inner.X, inner.Y, inner.Width, 4);
                g.FillRectangle(b, inner.X, inner.Y, 4, inner.Height);
            }

            using (SolidBrush b = new SolidBrush(darkColor))
            {
                g.FillRectangle(b, inner.X, inner.Y + inner.Height - 4, inner.Width, 4);
                g.FillRectangle(b, inner.X + inner.Width - 4, inner.Y, 4, inner.Height);
            }

            int cx = x + (size / 2);
            int cy = y + (size / 2);

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
        }

        private void DrawPixelStrawberry(Graphics g, int cx, int cy)
        {
            using (SolidBrush leaf = new SolidBrush(Color.FromArgb(40, 190, 60)))
            {
                g.FillRectangle(leaf, cx - 6, cy - 10, 12, 3);
                g.FillRectangle(leaf, cx - 8, cy - 9, 4, 3);
                g.FillRectangle(leaf, cx + 4, cy - 9, 4, 3);
            }

            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 75, 100)))
            {
                g.FillRectangle(body, cx - 8, cy - 7, 16, 8);
                g.FillRectangle(body, cx - 6, cy + 1, 12, 6);
                g.FillRectangle(body, cx - 3, cy + 7, 6, 4);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(160, 20, 45)))
            {
                g.FillRectangle(shadow, cx + 5, cy - 6, 3, 6);
                g.FillRectangle(shadow, cx + 3, cy + 1, 3, 5);
                g.FillRectangle(shadow, cx, cy + 7, 3, 3);
            }

            using (SolidBrush seed = new SolidBrush(Color.FromArgb(255, 240, 120)))
            {
                g.FillRectangle(seed, cx - 4, cy - 4, 2, 2);
                g.FillRectangle(seed, cx + 2, cy - 4, 2, 2);
                g.FillRectangle(seed, cx - 1, cy, 2, 2);
                g.FillRectangle(seed, cx - 3, cy + 4, 2, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.White))
            {
                g.FillRectangle(gloss, cx - 6, cy - 6, 2, 3);
            }
        }

        private void DrawPixelBlueGummy(Graphics g, int cx, int cy)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(100, 210, 255)))
            {
                g.FillRectangle(body, cx - 4, cy - 10, 8, 3);
                g.FillRectangle(body, cx - 8, cy - 7, 16, 5);
                g.FillRectangle(body, cx - 10, cy - 2, 20, 6);
                g.FillRectangle(body, cx - 8, cy + 4, 16, 5);
                g.FillRectangle(body, cx - 4, cy + 9, 8, 3);
            }

            using (SolidBrush dark = new SolidBrush(Color.FromArgb(0, 80, 175)))
            {
                g.FillRectangle(dark, cx + 4, cy - 7, 4, 4);
                g.FillRectangle(dark, cx + 6, cy - 2, 4, 6);
                g.FillRectangle(dark, cx + 4, cy + 4, 4, 4);
                g.FillRectangle(dark, cx - 2, cy + 9, 6, 3);
            }

            using (SolidBrush shine = new SolidBrush(Color.FromArgb(220, 250, 255)))
            {
                g.FillRectangle(shine, cx - 6, cy - 6, 4, 4);
                g.FillRectangle(shine, cx - 8, cy - 1, 4, 4);
                g.FillRectangle(shine, cx - 6, cy + 4, 3, 3);
            }
        }

        private void DrawPixelGreenApple(Graphics g, int cx, int cy)
        {
            using (SolidBrush stem = new SolidBrush(Color.FromArgb(120, 75, 30)))
            {
                g.FillRectangle(stem, cx - 1, cy - 11, 3, 4);
            }

            using (SolidBrush leaf = new SolidBrush(Color.FromArgb(110, 235, 60)))
            {
                g.FillRectangle(leaf, cx + 2, cy - 11, 4, 3);
            }

            using (SolidBrush body = new SolidBrush(Color.FromArgb(110, 230, 60)))
            {
                g.FillRectangle(body, cx - 9, cy - 7, 18, 12);
                g.FillRectangle(body, cx - 7, cy + 5, 14, 5);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(20, 110, 35)))
            {
                g.FillRectangle(shadow, cx + 5, cy - 6, 4, 10);
                g.FillRectangle(shadow, cx + 3, cy + 5, 4, 4);
                g.FillRectangle(shadow, cx - 2, cy + 8, 4, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.White))
            {
                g.FillRectangle(gloss, cx - 6, cy - 5, 3, 5);
            }
        }

        private void DrawPixelYellowLemon(Graphics g, int cx, int cy)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 240, 80)))
            {
                g.FillRectangle(body, cx - 3, cy - 10, 6, 3);
                g.FillRectangle(body, cx - 7, cy - 7, 14, 4);
                g.FillRectangle(body, cx - 9, cy - 3, 18, 7);
                g.FillRectangle(body, cx - 7, cy + 4, 14, 4);
                g.FillRectangle(body, cx - 3, cy + 8, 6, 3);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(190, 130, 0)))
            {
                g.FillRectangle(shadow, cx + 4, cy - 3, 5, 6);
                g.FillRectangle(shadow, cx + 2, cy + 4, 5, 3);
                g.FillRectangle(shadow, cx - 1, cy + 8, 3, 2);
            }

            using (SolidBrush line = new SolidBrush(Color.FromArgb(255, 180, 20)))
            {
                g.FillRectangle(line, cx - 5, cy, 10, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.White))
            {
                g.FillRectangle(gloss, cx - 5, cy - 5, 3, 3);
            }
        }

        private void DrawPixelPurplePlum(Graphics g, int cx, int cy)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(215, 110, 255)))
            {
                g.FillRectangle(body, cx - 6, cy - 9, 12, 3);
                g.FillRectangle(body, cx - 9, cy - 6, 18, 12);
                g.FillRectangle(body, cx - 6, cy + 6, 12, 3);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 15, 130)))
            {
                g.FillRectangle(shadow, cx + 5, cy - 5, 4, 10);
                g.FillRectangle(shadow, cx + 2, cy + 5, 4, 3);
            }

            using (SolidBrush swirl = new SolidBrush(Color.White))
            {
                g.FillRectangle(swirl, cx - 5, cy - 5, 3, 3);
                g.FillRectangle(swirl, cx - 2, cy - 2, 4, 4);
                g.FillRectangle(swirl, cx + 2, cy + 2, 3, 3);
            }

            using (SolidBrush gloss = new SolidBrush(Color.FromArgb(240, 200, 255)))
            {
                g.FillRectangle(gloss, cx - 6, cy - 7, 5, 2);
            }
        }
    }
}


