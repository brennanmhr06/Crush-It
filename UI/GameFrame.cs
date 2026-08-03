using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver;
using MongoDB.Bson;
using CrushIt.Data;
using CrushIt.Core;
using CrushIt.API;

namespace CrushIt.UI
{
    public class AchievementNotification
    {
        public string AchievementName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayTime { get; set; } = 0;
        public int MaxDisplayTime { get; set; } = 180;
        public float Y { get; set; } = -100;
        public float TargetY { get; set; } = 50;
    }

    public class GameFrame : Form
    {
        private int Rows;
        private int Cols;
        private int TileSize;
        private int GridOffsetX;
        private int GridOffsetY;
        private int TargetPointGoal;

        private CandyType[,] board = null!;
        private Random rand = new Random();

        private InputHandler inputController = null!;
        private bool isProcessingBoard = false;

        private List<CandyParticle> burstParticles = new List<CandyParticle>();
        private System.Windows.Forms.Timer gameLoopTimer = null!;
        private System.Windows.Forms.Timer idleTimer = null!;
        private DateTime lastInteractionTime = DateTime.UtcNow;

        // Background styling
        private List<StyleParticle> backgroundParticles = new List<StyleParticle>();
        private Random particleRand = new Random();
        private int pulsePhase = 0;

        private Point? hintMove1 = null;
        private Point? hintMove2 = null;
        private int hintAnimationPhase = 0;

        private readonly UserAccount currentUser;
        private readonly int levelNumber;
        private readonly IMongoDatabase database;
        private readonly IApiClient? apiClient;
        private bool levelCompleted = false;
        private int sessionGold = 0;
        private int completionAnimationPhase = 0;


        private int sessionMatches = 0;
        private int currentCombo = 0;
        private bool hasMadeFirstMatch = false;


        private List<AchievementNotification> achievementNotifications = new List<AchievementNotification>();
        private System.Windows.Forms.Timer notificationTimer = null!;


        private readonly IMongoCollection<UserAccount> usersCollection;

        public GameFrame(UserAccount user, int level)
        {
            this.currentUser = user;
            this.levelNumber = level;


            ConfigurationHelper.Initialize();

            var client = new MongoClient(ConfigurationHelper.GetMongoConnectionString());
            database = client.GetDatabase(ConfigurationHelper.GetDatabaseName());
            usersCollection = database.GetCollection<UserAccount>("users");

            // Initialize API client for progress sync
            try
            {
                var config = ApiConfiguration.Default;
                apiClient = new ApiClient(config.BaseUrl, config.ApiKey);
            }
            catch
            {
                apiClient = null; // API unavailable, sync will be skipped
            }


            CalculateLevelParameters(level);

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            GameData.ResetScore();
            
            // Initialize background particles
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 40, 550, 80, 480));
            
            // Start background music with low volume for gameplay
            SoundHelper.StartBackgroundMusic(0.08f); // 8% volume during gameplay (very faded)
            
            // Apply mobile scaling if needed
            MobileHelper.ApplyMobileScaling(this);

            inputController = new InputHandler(this, Rows, Cols, TileSize, GridOffsetX, GridOffsetY);
            inputController.OnSwapRequested += HandleSwapRequestedAsync;

            GenerateRandomBoard();

            gameLoopTimer = new System.Windows.Forms.Timer();
            gameLoopTimer.Interval = 16;
            gameLoopTimer.Tick += GameLoopTimer_Tick;
            gameLoopTimer.Start();

            idleTimer = new System.Windows.Forms.Timer();
            idleTimer.Interval = 20000;
            idleTimer.Tick += IdleTimer_Tick;
            idleTimer.Start();

            notificationTimer = new System.Windows.Forms.Timer();
            notificationTimer.Interval = 16;
            notificationTimer.Tick += NotificationTimer_Tick;
            notificationTimer.Start();
        }

        private void CalculateLevelParameters(int level)
        {
            Rows = Math.Min(8 + (level - 1) / 2, 12);
            Cols = Math.Min(8 + (level - 1) / 2, 10);

            TileSize = Math.Max(54 - (level - 1) * 2, 40);

            int gridTotalWidth = Cols * TileSize;
            int gridTotalHeight = Rows * TileSize;
            GridOffsetX = (550 - gridTotalWidth) / 2;
            GridOffsetY = 160 + (12 - Rows) * 8;

            TargetPointGoal = 1000 + (level - 1) * 500;
        }

        private void InitializeComponent()
        {
            this.Text = $"Crush It! - Level {levelNumber}";
            this.Size = new Size(550, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.FormClosed += (s, e) =>
            {
                idleTimer?.Stop();
                Application.Exit();
            };
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                ResetIdleTimer();
                if (e.KeyCode == Keys.Escape)
                {
                    MainFrame main = new MainFrame(currentUser, database);
                    main.Show();
                    this.Hide();
                    this.Dispose();
                }
            };
            this.MouseDown += (s, e) => { ResetIdleTimer(); };
        }

        private void GenerateRandomBoard()
        {
            board = new CandyType[Rows, Cols];
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
            ResetIdleTimer();
            if (isProcessingBoard) return;
            isProcessingBoard = true;

            SoundHelper.PlaySwipeSound();

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

        private (Point?, Point?) FindBestMove()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    if (c < Cols - 1)
                    {
                        CandyType temp = board[r, c];
                        board[r, c] = board[r, c + 1];
                        board[r, c + 1] = temp;

                        List<Point> matches = FindMatches();
                        if (matches.Count > 0)
                        {
                            board[r, c + 1] = board[r, c];
                            board[r, c] = temp;
                            return (new Point(c, r), new Point(c + 1, r));
                        }

                        board[r, c + 1] = board[r, c];
                        board[r, c] = temp;
                    }

                    if (r < Rows - 1)
                    {
                        CandyType temp = board[r, c];
                        board[r, c] = board[r + 1, c];
                        board[r + 1, c] = temp;

                        List<Point> matches = FindMatches();
                        if (matches.Count > 0)
                        {
                            board[r + 1, c] = board[r, c];
                            board[r, c] = temp;
                            return (new Point(c, r), new Point(c, r + 1));
                        }

                        board[r + 1, c] = board[r, c];
                        board[r, c] = temp;
                    }
                }
            }

            return (null, null);
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            if (isProcessingBoard || levelCompleted)
                return;

            var (move1, move2) = FindBestMove();
            if (move1.HasValue && move2.HasValue)
            {
                hintMove1 = move1;
                hintMove2 = move2;
                hintAnimationPhase = 0;
            }
        }

        private void ResetIdleTimer()
        {
            lastInteractionTime = DateTime.UtcNow;
            hintMove1 = null;
            hintMove2 = null;
            hintAnimationPhase = 0;
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
                if (matches.Count == 0)
                {
                    currentCombo = 0;
                    break;
                }

                List<Point> squareMatches = FindSquareMatches();
                HashSet<Point> explosionPoints = new HashSet<Point>();


                sessionMatches += matches.Count;
                currentCombo++;
                hasMadeFirstMatch = true;

                foreach (Point pt in matches)
                {
                    CandyType type = board[pt.Y, pt.X];
                    GameData.AddPoints(type);

                    int goldEarned = CandyGoldValues.GetGoldValue(type, levelNumber);
                    sessionGold += goldEarned;

                    int x = GridOffsetX + pt.X * TileSize + TileSize / 2;
                    int y = GridOffsetY + pt.Y * TileSize + TileSize / 2;
                    SpawnParticles(x, y, GetCandyColor(type));


                    if (goldEarned > 0)
                    {
                        SpawnGoldParticles(x, y, goldEarned);
                    }
                }

                if (squareMatches.Count > 0)
                {
                    SoundHelper.PlayCandyMatchSound();

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
                            .AddToSet(u => u.CompletedLevels, levelNumber)
                            .Inc(u => u.Gold, sessionGold)
                            .Inc(u => u.TotalMatches, sessionMatches)
                            .Set(u => u.HighestScore, Math.Max(currentUser.HighestScore, GameData.TotalScore));
                        await usersCollection.UpdateOneAsync(filter, update);

                        if (currentUser.CompletedLevels == null)
                            currentUser.CompletedLevels = new List<int>();
                        if (!currentUser.CompletedLevels.Contains(levelNumber))
                            currentUser.CompletedLevels.Add(levelNumber);
                        currentUser.Gold += sessionGold;
                        currentUser.TotalMatches += sessionMatches;
                        currentUser.HighestScore = Math.Max(currentUser.HighestScore, GameData.TotalScore);


                        await CheckAndUnlockAchievements();

                        // Sync progress with server after level completion
                        _ = ProgressSyncService.SyncAfterLevelAsync(currentUser, database, apiClient);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to update level completion: {ex.Message}");
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

        private async Task CheckAndUnlockAchievements()
        {
            List<AchievementType> newlyUnlocked = new List<AchievementType>();


            if (hasMadeFirstMatch && !IsAchievementUnlocked(AchievementType.FirstMatch))
            {
                newlyUnlocked.Add(AchievementType.FirstMatch);
            }


            if (currentUser.CompletedLevels.Contains(1) && !IsAchievementUnlocked(AchievementType.Level1Complete))
            {
                newlyUnlocked.Add(AchievementType.Level1Complete);
            }
            if (currentUser.CompletedLevels.Contains(5) && !IsAchievementUnlocked(AchievementType.Level5Complete))
            {
                newlyUnlocked.Add(AchievementType.Level5Complete);
            }
            if (currentUser.CompletedLevels.Contains(10) && !IsAchievementUnlocked(AchievementType.Level10Complete))
            {
                newlyUnlocked.Add(AchievementType.Level10Complete);
            }


            if (GameData.TotalScore >= 1000 && !IsAchievementUnlocked(AchievementType.Score1000))
            {
                newlyUnlocked.Add(AchievementType.Score1000);
            }
            if (GameData.TotalScore >= 5000 && !IsAchievementUnlocked(AchievementType.Score5000))
            {
                newlyUnlocked.Add(AchievementType.Score5000);
            }
            if (GameData.TotalScore >= 10000 && !IsAchievementUnlocked(AchievementType.Score10000))
            {
                newlyUnlocked.Add(AchievementType.Score10000);
            }


            if (currentUser.Gold >= 100 && !IsAchievementUnlocked(AchievementType.Gold100))
            {
                newlyUnlocked.Add(AchievementType.Gold100);
            }
            if (currentUser.Gold >= 500 && !IsAchievementUnlocked(AchievementType.Gold500))
            {
                newlyUnlocked.Add(AchievementType.Gold500);
            }
            if (currentUser.Gold >= 1000 && !IsAchievementUnlocked(AchievementType.Gold1000))
            {
                newlyUnlocked.Add(AchievementType.Gold1000);
            }


            if (currentCombo >= 3 && !IsAchievementUnlocked(AchievementType.Combo3))
            {
                newlyUnlocked.Add(AchievementType.Combo3);
            }
            if (currentCombo >= 5 && !IsAchievementUnlocked(AchievementType.Combo5))
            {
                newlyUnlocked.Add(AchievementType.Combo5);
            }





            if (currentUser.TotalMatches >= 100 && !IsAchievementUnlocked(AchievementType.TotalMatches100))
            {
                newlyUnlocked.Add(AchievementType.TotalMatches100);
            }
            if (currentUser.TotalMatches >= 500 && !IsAchievementUnlocked(AchievementType.TotalMatches500))
            {
                newlyUnlocked.Add(AchievementType.TotalMatches500);
            }
            if (currentUser.TotalMatches >= 1000 && !IsAchievementUnlocked(AchievementType.TotalMatches1000))
            {
                newlyUnlocked.Add(AchievementType.TotalMatches1000);
            }


            if (newlyUnlocked.Count > 0)
            {
                await UnlockAchievements(newlyUnlocked);
            }
        }

        private bool IsAchievementUnlocked(AchievementType type)
        {
            if (currentUser.Achievements == null)
                return false;

            foreach (var achievement in currentUser.Achievements)
            {
                if (achievement.Type == type && achievement.IsUnlocked)
                    return true;
            }
            return false;
        }

        private async Task UnlockAchievements(List<AchievementType> typesToUnlock)
        {
            var unlockedAchievements = new List<Achievement>();

            foreach (var type in typesToUnlock)
            {
                var definition = AchievementDefinitions.GetAchievementByType(type);
                if (definition != null)
                {
                    var newAchievement = new Achievement(type, definition.Name, definition.Description, definition.IconColor, definition.GoldReward);
                    newAchievement.IsUnlocked = true;
                    newAchievement.UnlockedAt = DateTime.UtcNow;
                    unlockedAchievements.Add(newAchievement);


                    ShowAchievementNotification(newAchievement);
                }
            }


            if (currentUser.Achievements == null)
                currentUser.Achievements = new List<Achievement>();

            foreach (var unlocked in unlockedAchievements)
            {

                var existing = currentUser.Achievements.FirstOrDefault(a => a.Type == unlocked.Type);
                if (existing != null)
                {
                    currentUser.Achievements.Remove(existing);
                }
                currentUser.Achievements.Add(unlocked);
            }


            try
            {
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Email, currentUser.Email);
                var update = Builders<UserAccount>.Update.Set(u => u.Achievements, currentUser.Achievements);
                await usersCollection.UpdateOneAsync(filter, update);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update achievements: {ex.Message}");
            }
        }

        private void ShowAchievementNotification(Achievement achievement)
        {
            var notification = new AchievementNotification
            {
                AchievementName = achievement.Name,
                Description = achievement.Description,
                Y = -100,
                TargetY = 50 + (achievementNotifications.Count * 60)
            };

            achievementNotifications.Add(notification);
        }

        private void NotificationTimer_Tick(object? sender, EventArgs e)
        {
            for (int i = achievementNotifications.Count - 1; i >= 0; i--)
            {
                var notification = achievementNotifications[i];
                notification.DisplayTime++;


                if (notification.Y < notification.TargetY)
                {
                    notification.Y += 5;
                    if (notification.Y > notification.TargetY)
                        notification.Y = notification.TargetY;
                }


                if (notification.DisplayTime >= notification.MaxDisplayTime)
                {
                    achievementNotifications.RemoveAt(i);
                }
            }

            this.Invalidate();
        }

        private void DrawAchievementNotifications(Graphics g)
        {
            foreach (var notification in achievementNotifications)
            {
                int notificationWidth = 400;
                int notificationHeight = 80;
                int notificationX = (this.ClientSize.Width - notificationWidth) / 2;
                int notificationY = (int)notification.Y;

                Rectangle notificationRect = new Rectangle(notificationX, notificationY, notificationWidth, notificationHeight);


                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(10, 5, 15)))
                {
                    g.FillRectangle(shadow, new Rectangle(notificationRect.X + 4, notificationRect.Y + 4, notificationRect.Width, notificationRect.Height));
                }


                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(50, 180, 50)))
                {
                    g.FillRectangle(bgBrush, notificationRect);
                }


                using (Pen borderPen = new Pen(Color.FromArgb(30, 150, 30), 3))
                {
                    g.DrawRectangle(borderPen, notificationRect);
                }


                using (Font nameFont = new Font("Comic Sans MS", 16, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("ACHIEVEMENT UNLOCKED!", nameFont, Brushes.White, new RectangleF(notificationRect.X, notificationRect.Y + 5, notificationRect.Width, 25), sf);
                    }
                }


                using (Font achievementFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(notification.AchievementName, achievementFont, Brushes.Yellow, new RectangleF(notificationRect.X, notificationRect.Y + 30, notificationRect.Width, 20), sf);
                    }
                }


                using (Font descFont = new Font("Comic Sans MS", 10))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(notification.Description, descFont, Brushes.White, new RectangleF(notificationRect.X, notificationRect.Y + 50, notificationRect.Width, 25), sf);
                    }
                }
            }
        }

        private void GameLoopTimer_Tick(object? sender, EventArgs e)
        {
            // Update background particles
            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 100);
            pulsePhase++;

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

            if (hintMove1.HasValue && hintMove2.HasValue)
            {
                hintAnimationPhase = (hintAnimationPhase + 1) % 60;
            }

            if (levelCompleted)
            {
                completionAnimationPhase = (completionAnimationPhase + 1) % 120;
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // Draw cartoon background with particles (like HomeFrame/LobbyFrame)
            CrushItStyleHelper.SetupQualityRendering(g);
            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);

            // Set up pixelated rendering for the game board
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            // Draw styled level banner
            Rectangle banner = new Rectangle(50, 5, 436, 60);
            CrushItStyleHelper.DrawPanel(g, banner, Color.FromArgb(255, 220, 80, 120), Color.FromArgb(255, 190, 60, 100), Color.FromArgb(255, 160, 50, 80));
            
            using (Font titleFont = new Font("Comic Sans MS", 20, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, $"LEVEL {levelNumber}", titleFont, banner, Color.White, Color.FromArgb(100, 60, 20, 0), 3, sf);
            }

            // Draw styled score and gold panels
            Rectangle scorePanel = new Rectangle(50, 75, 220, 45);
            CrushItStyleHelper.DrawPanel(g, scorePanel, Color.FromArgb(255, 100, 180, 220), Color.FromArgb(255, 70, 150, 190), Color.FromArgb(255, 50, 120, 160));
            
            using (Font scoreFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string scoreText = $"SCORE: {Math.Min(GameData.TotalScore, TargetPointGoal)} / {TargetPointGoal}";
                CrushItStyleHelper.DrawOutlinedText(g, scoreText, scoreFont, scorePanel, Color.White, Color.FromArgb(100, 0, 50, 100), 2, sf);
            }

            Rectangle goldPanel = new Rectangle(280, 75, 220, 45);
            CrushItStyleHelper.DrawPanel(g, goldPanel, Color.FromArgb(255, 220, 180, 80), Color.FromArgb(255, 190, 150, 50), Color.FromArgb(255, 160, 120, 30));
            
            using (Font goldFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string goldText = $"GOLD: {sessionGold}";
                CrushItStyleHelper.DrawOutlinedText(g, goldText, goldFont, goldPanel, Color.White, Color.FromArgb(100, 80, 40, 0), 2, sf);
            }


            DrawAchievementNotifications(g);

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

            if (hintMove1.HasValue && hintMove2.HasValue && !isProcessingBoard)
            {
                Point p1 = hintMove1.Value;
                Point p2 = hintMove2.Value;

                int x1 = GridOffsetX + p1.X * TileSize + TileSize / 2;
                int y1 = GridOffsetY + p1.Y * TileSize + TileSize / 2;
                int x2 = GridOffsetX + p2.X * TileSize + TileSize / 2;
                int y2 = GridOffsetY + p2.Y * TileSize + TileSize / 2;

                int pulseAlpha = 100 + (int)(80 * Math.Sin(hintAnimationPhase * Math.PI / 30));
                using (SolidBrush hintGlow = new SolidBrush(Color.FromArgb(pulseAlpha, 255, 255, 100)))
                {
                    g.FillEllipse(hintGlow, x1 - TileSize/2 - 5, y1 - TileSize/2 - 5, TileSize + 10, TileSize + 10);
                    g.FillEllipse(hintGlow, x2 - TileSize/2 - 5, y2 - TileSize/2 - 5, TileSize + 10, TileSize + 10);
                }

                float arrowProgress = (float)(0.5 + 0.5 * Math.Sin(hintAnimationPhase * Math.PI / 30));
                int arrowX = (int)(x1 + (x2 - x1) * arrowProgress);
                int arrowY = (int)(y1 + (y2 - y1) * arrowProgress);

                using (SolidBrush arrowBrush = new SolidBrush(Color.FromArgb(255, 255, 200)))
                {
                    g.FillEllipse(arrowBrush, arrowX - 8, arrowY - 8, 16, 16);
                }

                using (Pen linePen = new Pen(Color.FromArgb(pulseAlpha, 255, 255, 100), 3))
                {
                    g.DrawLine(linePen, x1, y1, x2, y2);
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
            using (SolidBrush body = new SolidBrush(Color.FromArgb(160, 60, 200)))
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

        private void SpawnGoldParticles(int x, int y, int goldAmount)
        {
            int particleCount = Math.Min(goldAmount * 2, 20);
            for (int i = 0; i < particleCount; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = 1 + (float)(rand.NextDouble() * 2);
                float speedX = (float)Math.Cos(angle) * speed;
                float speedY = (float)Math.Sin(angle) * speed - 1;

                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = speedX,
                    SpeedY = speedY,
                    Size = 5 + rand.Next(5),
                    Alpha = 255,
                    ParticleColor = Color.FromArgb(255, 215, 0)
                });
            }
        }
    }
}

