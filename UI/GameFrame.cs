using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver;
using MongoDB.Bson;
using CrushIt.Data;
using CrushIt.Core;
using CrushIt.API;
using CrushIt.UI;

namespace CrushIt.UI
{
    public class AchievementNotification
    {
        public string AchievementName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayTime { get; set; } = 0;
        public int MaxDisplayTime { get; set; } = 180;
        public float Y { get; set; } = -120;
        public float TargetY { get; set; } = 30;
    }

    public class MatchInfo
    {
        public List<Point> MatchedPoints { get; set; } = new List<Point>();
        public int MatchLength { get; set; }
        public bool IsHorizontal { get; set; }
        public Point CreationPoint { get; set; }
        public CandyType CandyType { get; set; }
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
        private StyleParticle[] backgroundParticles = Array.Empty<StyleParticle>();
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
                if (!ApiInitializer.IsInitialized)
                {
                    ApiInitializer.Initialize(config);
                }
                apiClient = ApiInitializer.GetApiClient();
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
            
            // Initialize background particles - use form size
            backgroundParticles = CrushItStyleHelper.CreateParticles(particleRand, 25, 890, 80, 530); // Reduced from 40 to 25
            
            // Start background music with low volume for gameplay
            SoundManager.StartBackgroundMusic(0.08f); // 8% volume during gameplay (very faded)
            
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
            GridOffsetX = (900 - gridTotalWidth) / 2;
            GridOffsetY = 130 + (12 - Rows) * 8;

            TargetPointGoal = 1000 + (level - 1) * 500;
        }

        private void InitializeComponent()
        {
            this.Text = $"Crush It! - Level {levelNumber}";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.FormClosed += (s, e) =>
            {
                idleTimer?.Stop();
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
                ResetIdleTimer();
                if (e.KeyCode == Keys.Escape)
                {
                    // Close any existing GameFrames before showing MainFrame
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form is GameFrame existingGame && existingGame != this)
                        {
                            existingGame.Close();
                            existingGame.Dispose();
                        }
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
            };
            this.MouseDown += (s, e) => { ResetIdleTimer(); };
        }

        private void GenerateRandomBoard()
        {
            board = new CandyType[Rows, Cols];
            Array values = Enum.GetValues(typeof(CandyType));
            
            // Filter out special candies for initial board generation
            List<CandyType> basicCandies = new List<CandyType>();
            foreach (CandyType ct in values)
            {
                if (!ct.IsSpecial())
                {
                    basicCandies.Add(ct);
                }
            }

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    CandyType type;
                    do
                    {
                        type = basicCandies[rand.Next(basicCandies.Count)];
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

            SoundManager.PlaySound(SoundType.Swipe);

            await inputController.AnimateSwapAsync(p1, p2);

            CandyType type1 = board[p1.Y, p1.X];
            CandyType type2 = board[p2.Y, p2.X];
            
            // Store original positions for revert
            CandyType originalType1 = type1;
            CandyType originalType2 = type2;
            
            CandyType temp = board[p1.Y, p1.X];
            board[p1.Y, p1.X] = board[p2.Y, p2.X];
            board[p2.Y, p2.X] = temp;

            // Check for special candy swaps
            bool specialSwap = false;
            
            // Color bomb + any candy = activate color bomb
            if (type1.IsColorBomb() || type2.IsColorBomb())
            {
                specialSwap = true;
                Point bombPoint = type1.IsColorBomb() ? p2 : p1;
                CandyType bombType = type1.IsColorBomb() ? type1 : type2;
                CandyType targetType = type1.IsColorBomb() ? type2 : type1;
                
                HashSet<Point> explosionPoints = new HashSet<Point>();
                
                // Set the target color for the color bomb
                if (targetType != (CandyType)(-1) && !targetType.IsSpecial())
                {
                    for (int r = 0; r < Rows; r++)
                    {
                        for (int c = 0; c < Cols; c++)
                        {
                            if (board[r, c] == targetType || board[r, c] == targetType.GetStripedVariant())
                            {
                                explosionPoints.Add(new Point(c, r));
                            }
                        }
                    }
                    
                    // Also clear the bomb itself
                    explosionPoints.Add(bombPoint);
                    
                    // Process the explosion
                    await ProcessSpecialSwapExplosion(explosionPoints, bombPoint, targetType);
                }
                else
                {
                    // If targeting a special candy, revert swap
                    await inputController.AnimateSwapAsync(p2, p1, isRevert: true);
                    board[p1.Y, p1.X] = originalType1;
                    board[p2.Y, p2.X] = originalType2;
                }
            }
            // Striped candy + striped candy = cross explosion
            else if (type1.IsStriped() && type2.IsStriped())
            {
                specialSwap = true;
                HashSet<Point> explosionPoints = new HashSet<Point>();
                
                // First striped candy effect
                if (type1.IsHorizontalStriped())
                {
                    for (int c = 0; c < Cols; c++)
                        explosionPoints.Add(new Point(c, p1.Y));
                }
                else
                {
                    for (int r = 0; r < Rows; r++)
                        explosionPoints.Add(new Point(p1.X, r));
                }
                
                // Second striped candy effect
                if (type2.IsHorizontalStriped())
                {
                    for (int c = 0; c < Cols; c++)
                        explosionPoints.Add(new Point(c, p2.Y));
                }
                else
                {
                    for (int r = 0; r < Rows; r++)
                        explosionPoints.Add(new Point(p2.X, r));
                }
                
                await ProcessSpecialSwapExplosion(explosionPoints, p1, type1);
            }
            // Striped candy + regular candy = activate striped candy
            else if (type1.IsStriped() || type2.IsStriped())
            {
                specialSwap = true;
                Point stripedPoint = type1.IsStriped() ? p2 : p1;
                CandyType stripedType = type1.IsStriped() ? type1 : type2;
                
                HashSet<Point> explosionPoints = new HashSet<Point>();
                
                if (stripedType.IsHorizontalStriped())
                {
                    for (int c = 0; c < Cols; c++)
                        explosionPoints.Add(new Point(c, stripedPoint.Y));
                }
                else
                {
                    for (int r = 0; r < Rows; r++)
                        explosionPoints.Add(new Point(stripedPoint.X, r));
                }
                
                await ProcessSpecialSwapExplosion(explosionPoints, stripedPoint, stripedType);
            }

            if (!specialSwap)
            {
                List<Point> matches = FindMatches();
                if (matches.Count > 0)
                {
                    await ProcessMatchesCascade();
                }
                else
                {
                    // Revert the swap if no matches found
                    await inputController.AnimateSwapAsync(p2, p1, isRevert: true);
                    board[p1.Y, p1.X] = originalType1;
                    board[p2.Y, p2.X] = originalType2;
                }
            }

            isProcessingBoard = false;
            this.Invalidate();
        }

        private async Task ProcessSpecialSwapExplosion(HashSet<Point> explosionPoints, Point origin, CandyType type)
        {
            sessionMatches += explosionPoints.Count;
            currentCombo++;
            hasMadeFirstMatch = true;

            foreach (Point pt in explosionPoints)
            {
                CandyType candyType = board[pt.Y, pt.X];
                
                // Check if this is a special candy being activated (chain reaction)
                if (candyType.IsSpecial() && pt != origin)
                {
                    await ActivateSpecialCandy(pt, candyType, explosionPoints);
                }
                
                GameData.AddPoints(candyType);

                int goldEarned = CandyGoldValues.GetGoldValue(candyType, levelNumber);
                sessionGold += goldEarned;

                int x = GridOffsetX + pt.X * TileSize + TileSize / 2;
                int y = GridOffsetY + pt.Y * TileSize + TileSize / 2;
                SpawnParticles(x, y, GetCandyColor(candyType));

                if (goldEarned > 0)
                {
                    SpawnGoldParticles(x, y, goldEarned);
                }
            }

            // Clear all exploded points
            foreach (Point pt in explosionPoints)
            {
                board[pt.Y, pt.X] = (CandyType)(-1);
            }

            this.Invalidate();
            await Task.Delay(200);

            // Check for level completion
            if (GameData.TotalScore >= TargetPointGoal)
            {
                levelCompleted = true;
                SoundManager.PlaySound(SoundType.LevelComplete);
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

                    _ = ProgressSyncService.SyncAfterLevelAsync(currentUser, database, apiClient);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to update level completion", ex);
                }

                await Task.Delay(3000);

                // Close any existing GameFrames before showing MainFrame
                foreach (Form form in Application.OpenForms)
                {
                    if (form is GameFrame existingGame && existingGame != this)
                    {
                        existingGame.Close();
                        existingGame.Dispose();
                    }
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
                return;
            }

            // Refill board and process cascades
            await RefillBoardAndProcessCascades();
        }

        private async Task RefillBoardAndProcessCascades()
        {
            // Refill board
            Array values = Enum.GetValues(typeof(CandyType));
            
            List<CandyType> basicCandies = new List<CandyType>();
            foreach (CandyType ct in values)
            {
                if (!ct.IsSpecial())
                {
                    basicCandies.Add(ct);
                }
            }
            
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
                            board[r, c] = basicCandies[rand.Next(basicCandies.Count)];
                        }
                    }
                }
            }

            this.Invalidate();
            await Task.Delay(250);

            // Process any cascades
            await ProcessMatchesCascade();
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

            // Check for square matches (2x2, 3x3, 4x4, etc.)
            for (int r = 0; r < Rows - 1; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    CandyType type = board[r, c];
                    if (type != (CandyType)(-1))
                    {
                        // Find the maximum square size starting from this position
                        int maxSquareSize = Math.Min(Rows - r, Cols - c);
                        
                        for (int size = 2; size <= maxSquareSize; size++)
                        {
                            bool isSquareMatch = true;
                            
                            // Check if all tiles in the square match
                            for (int sr = 0; sr < size && isSquareMatch; sr++)
                            {
                                for (int sc = 0; sc < size && isSquareMatch; sc++)
                                {
                                    if (board[r + sr, c + sc] != type)
                                    {
                                        isSquareMatch = false;
                                    }
                                }
                            }
                            
                            if (isSquareMatch)
                            {
                                for (int sr = 0; sr < size; sr++)
                                {
                                    for (int sc = 0; sc < size; sc++)
                                    {
                                        matchedPoints.Add(new Point(c + sc, r + sr));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return new List<Point>(matchedPoints);
        }

        private List<MatchInfo> FindMatchesDetailed()
        {
            List<MatchInfo> matches = new List<MatchInfo>();
            HashSet<Point> processedPoints = new HashSet<Point>();

            // Check horizontal matches
            for (int r = 0; r < Rows; r++)
            {
                int matchLength = 1;
                int matchStart = 0;
                
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
                            MatchInfo match = new MatchInfo
                            {
                                MatchLength = matchLength,
                                IsHorizontal = true,
                                CandyType = board[r, c],
                                CreationPoint = new Point(matchStart + matchLength / 2, r)
                            };
                            
                            for (int i = 0; i < matchLength; i++)
                            {
                                Point pt = new Point(matchStart + i, r);
                                match.MatchedPoints.Add(pt);
                                processedPoints.Add(pt);
                            }
                            matches.Add(match);
                        }
                        matchLength = 1;
                        matchStart = c + 1;
                    }
                }
            }

            // Check vertical matches
            for (int c = 0; c < Cols; c++)
            {
                int matchLength = 1;
                int matchStart = 0;
                
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
                            // Check if this match overlaps with already processed horizontal matches
                            bool isOverlap = false;
                            for (int i = 0; i < matchLength; i++)
                            {
                                Point pt = new Point(c, matchStart + i);
                                if (processedPoints.Contains(pt))
                                {
                                    isOverlap = true;
                                    break;
                                }
                            }
                            
                            if (!isOverlap)
                            {
                                MatchInfo match = new MatchInfo
                                {
                                    MatchLength = matchLength,
                                    IsHorizontal = false,
                                    CandyType = board[matchStart, c],
                                    CreationPoint = new Point(c, matchStart + matchLength / 2)
                                };
                                
                                for (int i = 0; i < matchLength; i++)
                                {
                                    Point pt = new Point(c, matchStart + i);
                                    match.MatchedPoints.Add(pt);
                                }
                                matches.Add(match);
                            }
                        }
                        matchLength = 1;
                        matchStart = r + 1;
                    }
                }
            }

            return matches;
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
                    if (type != (CandyType)(-1))
                    {
                        // Find the maximum square size starting from this position
                        int maxSquareSize = Math.Min(Rows - r, Cols - c);
                        
                        for (int size = 2; size <= maxSquareSize; size++)
                        {
                            bool isSquareMatch = true;
                            
                            // Check if all tiles in the square match
                            for (int sr = 0; sr < size && isSquareMatch; sr++)
                            {
                                for (int sc = 0; sc < size && isSquareMatch; sc++)
                                {
                                    if (board[r + sr, c + sc] != type)
                                    {
                                        isSquareMatch = false;
                                    }
                                }
                            }
                            
                            if (isSquareMatch)
                            {
                                // Add all points in the square
                                for (int sr = 0; sr < size; sr++)
                                {
                                    for (int sc = 0; sc < size; sc++)
                                    {
                                        Point pt = new Point(c + sc, r + sr);
                                        if (!squareMatches.Contains(pt))
                                        {
                                            squareMatches.Add(pt);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return squareMatches;
        }

        private async Task ActivateSpecialCandy(Point pt, CandyType type, HashSet<Point> explosionPoints)
        {
            SoundManager.PlaySound(SoundType.SpecialMove);
            
            var config = PowerupsConfig.Instance;
            
            if (type.IsStriped())
            {
                // Striped candy clears row or column
                bool isHorizontal = type.IsHorizontalStriped();
                
                if (isHorizontal)
                {
                    // Clear entire row
                    for (int c = 0; c < Cols; c++)
                    {
                        if (c != pt.X) // Don't double-count the candy itself
                        {
                            explosionPoints.Add(new Point(c, pt.Y));
                        }
                    }
                    
                    // Horizontal explosion effect
                    int explosionX = GridOffsetX + pt.X * TileSize + TileSize / 2;
                    int explosionY = GridOffsetY + pt.Y * TileSize + TileSize / 2;
                    SpawnHorizontalLineExplosion(explosionX, explosionY, GetCandyColor(type.GetBaseType()));
                }
                else
                {
                    // Clear entire column
                    for (int r = 0; r < Rows; r++)
                    {
                        if (r != pt.Y) // Don't double-count the candy itself
                        {
                            explosionPoints.Add(new Point(pt.X, r));
                        }
                    }
                    
                    // Vertical explosion effect
                    int explosionX = GridOffsetX + pt.X * TileSize + TileSize / 2;
                    int explosionY = GridOffsetY + pt.Y * TileSize + TileSize / 2;
                    SpawnVerticalLineExplosion(explosionX, explosionY, GetCandyColor(type.GetBaseType()));
                }
                
                SoundManager.PlaySound(SoundType.CandyMatch);
            }
            else if (type.IsColorBomb())
            {
                // Color bomb clears all candies of a random color
                // First, determine which color to clear (use the color of the candy it was swapped with)
                CandyType colorToClear = DetermineColorBombTarget(pt);
                
                if (colorToClear != CandyType.ColorBomb)
                {
                    for (int r = 0; r < Rows; r++)
                    {
                        for (int c = 0; c < Cols; c++)
                        {
                            if (board[r, c] == colorToClear || board[r, c] == colorToClear.GetStripedVariant())
                            {
                                explosionPoints.Add(new Point(c, r));
                            }
                        }
                    }
                    
                    // Color explosion effect
                    int explosionX = GridOffsetX + pt.X * TileSize + TileSize / 2;
                    int explosionY = GridOffsetY + pt.Y * TileSize + TileSize / 2;
                    SpawnColorExplosion(explosionX, explosionY, GetCandyColor(colorToClear));
                    
                    SoundManager.PlaySound(SoundType.CandyMatch);
                }
            }
        }

        private CandyType DetermineColorBombTarget(Point bombPoint)
        {
            // Find adjacent candies to determine target color
            CandyType[] adjacentColors = new CandyType[4];
            int foundColors = 0;
            
            // Check adjacent cells
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = bombPoint.X + dx[i];
                int ny = bombPoint.Y + dy[i];
                
                if (nx >= 0 && nx < Cols && ny >= 0 && ny < Rows)
                {
                    CandyType neighborType = board[ny, nx];
                    if (neighborType != (CandyType)(-1) && !neighborType.IsSpecial())
                    {
                        adjacentColors[foundColors++] = neighborType.GetBaseType();
                    }
                }
            }
            
            // Return first found color, or random if none found
            if (foundColors > 0)
            {
                return adjacentColors[rand.Next(foundColors)];
            }
            
            // Fallback to random color
            Array values = Enum.GetValues(typeof(CandyType));
            foreach (CandyType ct in values)
            {
                if (!ct.IsSpecial())
                {
                    return ct;
                }
            }
            
            return CandyType.RedStrawberry; // Ultimate fallback
        }

        private void SpawnHorizontalLineExplosion(int x, int y, Color color)
        {
            for (int i = 0; i < PowerupsConfig.Instance.PowerupSettings.ParticleCount; i++)
            {
                float angle = (rand.Next(2) == 0) ? 0 : (float)Math.PI; // Left or right
                float speed = 5 + rand.Next(5);
                
                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = (float)Math.Cos(angle) * speed,
                    SpeedY = (float)(rand.NextDouble() - 0.5) * 2,
                    Size = 4 + rand.Next(4),
                    Alpha = 1.0f,
                    ParticleColor = color
                });
            }
        }

        private void SpawnVerticalLineExplosion(int x, int y, Color color)
        {
            for (int i = 0; i < PowerupsConfig.Instance.PowerupSettings.ParticleCount; i++)
            {
                float angle = (rand.Next(2) == 0) ? (float)Math.PI / 2 : (float)-Math.PI / 2; // Up or down
                float speed = 5 + rand.Next(5);
                
                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = (float)(rand.NextDouble() - 0.5) * 2,
                    SpeedY = (float)Math.Sin(angle) * speed,
                    Size = 4 + rand.Next(4),
                    Alpha = 1.0f,
                    ParticleColor = color
                });
            }
        }

        private void SpawnColorExplosion(int x, int y, Color color)
        {
            for (int i = 0; i < PowerupsConfig.Instance.PowerupSettings.ParticleCount * 2; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = 3 + rand.Next(5);
                
                burstParticles.Add(new CandyParticle
                {
                    X = x,
                    Y = y,
                    SpeedX = (float)Math.Cos(angle) * speed,
                    SpeedY = (float)Math.Sin(angle) * speed,
                    Size = 3 + rand.Next(5),
                    Alpha = 1.0f,
                    ParticleColor = color
                });
            }
        }

        private async Task ProcessMatchesCascade()
        {
            while (true)
            {
                // Find all matches including those created by previous cascades
                List<MatchInfo> matchInfos = FindMatchesDetailed();
                List<Point> allMatchedPoints = new List<Point>();
                
                // Collect all matched points from detailed matches
                foreach (MatchInfo match in matchInfos)
                {
                    allMatchedPoints.AddRange(match.MatchedPoints);
                }
                
                // Also find square matches
                List<Point> squareMatches = FindSquareMatches();
                allMatchedPoints.AddRange(squareMatches);
                
                if (allMatchedPoints.Count == 0)
                {
                    currentCombo = 0;
                    break;
                }

                HashSet<Point> explosionPoints = new HashSet<Point>();
                List<Point> specialCandyCreations = new List<Point>();

                sessionMatches += allMatchedPoints.Count;
                currentCombo++;
                hasMadeFirstMatch = true;

                // Process regular matches and create special candies
                foreach (MatchInfo match in matchInfos)
                {
                    // Check if we should create a special candy
                    CandyType specialCandyType = CandyType.RedStrawberry; // default
                    bool shouldCreateSpecial = false;
                    Point creationPoint = match.CreationPoint;

                    if (match.MatchLength == 4 && levelNumber >= 5)
                    {
                        // Create striped candy
                        specialCandyType = match.CandyType.GetStripedVariant();
                        shouldCreateSpecial = true;
                    }
                    else if (match.MatchLength >= 5 && levelNumber >= 7)
                    {
                        // Create color bomb
                        specialCandyType = CandyType.ColorBomb;
                        shouldCreateSpecial = true;
                    }

                    // Process matched points
                    foreach (Point pt in match.MatchedPoints)
                    {
                        CandyType type = board[pt.Y, pt.X];
                        
                        // Check if this is a special candy being activated
                        if (type.IsSpecial())
                        {
                            await ActivateSpecialCandy(pt, type, explosionPoints);
                        }
                        
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

                    // Create special candy if conditions met
                    if (shouldCreateSpecial && creationPoint.X >= 0 && creationPoint.X < Cols && 
                        creationPoint.Y >= 0 && creationPoint.Y < Rows)
                    {
                        board[creationPoint.Y, creationPoint.X] = specialCandyType;
                        specialCandyCreations.Add(creationPoint);
                        
                        // Don't clear the creation point
                        match.MatchedPoints.Remove(creationPoint);
                    }
                }

                if (squareMatches.Count > 0)
                {
                    SoundManager.PlaySound(SoundType.CandyMatch);

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

                // Clear all matched points including square matches
                foreach (MatchInfo match in matchInfos)
                {
                    foreach (Point pt in match.MatchedPoints)
                    {
                        board[pt.Y, pt.X] = (CandyType)(-1);
                    }
                }

                // Clear square matches
                foreach (Point pt in squareMatches)
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
                        Logger.LogError("Failed to update level completion", ex);
                    }

                    await Task.Delay(3000);

                    // Close any existing GameFrames before showing MainFrame
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form is GameFrame existingGame && existingGame != this)
                        {
                            existingGame.Close();
                            existingGame.Dispose();
                        }
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
                    return;
                }

                // Refill board and continue cascading
                await RefillBoardAndProcessCascades();
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

        private void DrawRotatedStar(Graphics g, int x, int y, int size, Color color, float rotation)
        {
            using (SolidBrush starBrush = new SolidBrush(color))
            {
                Point[] starPoints = new Point[10];
                for (int i = 0; i < 10; i++)
                {
                    double angle = i * Math.PI / 5 - Math.PI / 2 + rotation;
                    double radius = (i % 2 == 0) ? size : size / 2;
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
                Logger.LogError("Failed to update achievements", ex);
            }
        }

        private void ShowAchievementNotification(Achievement achievement)
        {
            SoundManager.PlaySound(SoundType.Achievement);
            
            var notification = new AchievementNotification
            {
                AchievementName = achievement.Name,
                Description = achievement.Description,
                Y = -120,
                TargetY = 30 + (achievementNotifications.Count * 75)
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
                int notificationWidth = 450;
                int notificationHeight = 100;
                int notificationX = (this.ClientSize.Width - notificationWidth) / 2;
                int notificationY = (int)notification.Y;

                Rectangle notificationRect = new Rectangle(notificationX, notificationY, notificationWidth, notificationHeight);

                // Enhanced shadow with blur effect
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                {
                    g.FillRectangle(shadow, new Rectangle(notificationRect.X + 6, notificationRect.Y + 6, notificationRect.Width, notificationRect.Height));
                }

                // Gradient background
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    notificationRect,
                    Color.FromArgb(255, 255, 215, 0),
                    Color.FromArgb(255, 255, 140, 0),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgBrush, notificationRect);
                }

                // Inner highlight
                using (SolidBrush highlight = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                {
                    g.FillRectangle(highlight, notificationRect.X + 4, notificationRect.Y + 4, notificationRect.Width - 8, 8);
                }

                // Border with glow effect
                using (Pen borderPen = new Pen(Color.FromArgb(255, 255, 200, 50), 4))
                {
                    g.DrawRectangle(borderPen, notificationRect);
                }

                // Trophy icon
                using (Font trophyFont = new Font("Segoe UI Emoji", 32))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("🏆", trophyFont, Brushes.Gold, new RectangleF(notificationRect.X + 20, notificationRect.Y + 20, 50, 60), sf);
                    }
                }

                // Achievement title
                using (Font titleFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                    {
                        g.DrawString("ACHIEVEMENT UNLOCKED!", titleFont, Brushes.White, new RectangleF(notificationRect.X + 80, notificationRect.Y + 12, notificationRect.Width - 100, 20), sf);
                    }
                }

                // Achievement name
                using (Font nameFont = new Font("Comic Sans MS", 16, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                    {
                        g.DrawString(notification.AchievementName, nameFont, Brushes.Gold, new RectangleF(notificationRect.X + 80, notificationRect.Y + 35, notificationRect.Width - 100, 25), sf);
                    }
                }

                // Achievement description
                using (Font descFont = new Font("Comic Sans MS", 11))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                    {
                        g.DrawString(notification.Description, descFont, Brushes.White, new RectangleF(notificationRect.X + 80, notificationRect.Y + 60, notificationRect.Width - 100, 30), sf);
                    }
                }
            }
        }

        private void GameLoopTimer_Tick(object? sender, EventArgs e)
        {
            // Update background particles
            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 120);
            pulsePhase++;

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
            Rectangle banner = new Rectangle(200, 10, 500, 50);
            CrushItStyleHelper.DrawPanel(g, banner, Color.FromArgb(255, 220, 80, 120), Color.FromArgb(255, 190, 60, 100), Color.FromArgb(255, 160, 50, 80));

            using (Font titleFont = new Font("Comic Sans MS", 20, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, $"LEVEL {levelNumber}", titleFont, banner, Color.White, Color.FromArgb(100, 60, 20, 0), 3, sf);
            }

            // Draw professional score and gold panels
            DrawProfessionalScorePanel(g, 50, 70, 380, 45, GameData.TotalScore, TargetPointGoal);
            DrawProfessionalGoldPanel(g, 440, 70, 380, 45, sessionGold);


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
                int clampedAlpha = Math.Max(0, Math.Min(255, (int)p.Alpha));
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(clampedAlpha, p.ParticleColor)))
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
                pulseAlpha = Math.Max(0, Math.Min(255, pulseAlpha));
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

                int clampedPulseAlpha = Math.Max(0, Math.Min(255, pulseAlpha));
                using (Pen linePen = new Pen(Color.FromArgb(clampedPulseAlpha, 255, 255, 100), 3))
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
                Rectangle compBanner = new Rectangle(bannerX, 280, bannerWidth, bannerHeight);
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
            Color glowColor = Color.White;

            // Get base type for special candies
            CandyType baseType = candy.GetBaseType();
            
            // For powerup candies, use a completely different color scheme - much more vibrant
            bool isPowerup = candy.IsStriped() || candy.IsColorBomb();
            
            switch (baseType)
            {
                case CandyType.RedStrawberry:
                    if (isPowerup)
                    {
                        // Powerup version - very bright and bold red
                        mainColor = Color.FromArgb(255, 200, 50, 80);
                        darkColor = Color.FromArgb(255, 120, 20, 40);
                        lightColor = Color.FromArgb(255, 255, 150, 180);
                        glowColor = Color.FromArgb(255, 255, 100, 150);
                    }
                    else
                    {
                        mainColor = Color.FromArgb(235, 45, 75);
                        darkColor = Color.FromArgb(135, 10, 35);
                        lightColor = Color.FromArgb(255, 140, 160);
                        glowColor = Color.FromArgb(255, 100, 50, 80);
                    }
                    break;
                case CandyType.BlueGummy:
                    if (isPowerup)
                    {
                        // Powerup version - very bright and bold blue
                        mainColor = Color.FromArgb(255, 50, 150, 255);
                        darkColor = Color.FromArgb(255, 20, 80, 180);
                        lightColor = Color.FromArgb(255, 150, 220, 255);
                        glowColor = Color.FromArgb(255, 100, 180, 255);
                    }
                    else
                    {
                        mainColor = Color.FromArgb(35, 165, 245);
                        darkColor = Color.FromArgb(10, 75, 155);
                        lightColor = Color.FromArgb(150, 225, 255);
                        glowColor = Color.FromArgb(255, 50, 100, 180);
                    }
                    break;
                case CandyType.GreenApple:
                    if (isPowerup)
                    {
                        // Powerup version - very bright and bold green
                        mainColor = Color.FromArgb(255, 50, 200, 80);
                        darkColor = Color.FromArgb(255, 20, 120, 40);
                        lightColor = Color.FromArgb(255, 150, 255, 180);
                        glowColor = Color.FromArgb(255, 100, 255, 150);
                    }
                    else
                    {
                        mainColor = Color.FromArgb(45, 205, 85);
                        darkColor = Color.FromArgb(15, 105, 35);
                        lightColor = Color.FromArgb(150, 255, 175);
                        glowColor = Color.FromArgb(255, 50, 150, 80);
                    }
                    break;
                case CandyType.YellowLemon:
                    if (isPowerup)
                    {
                        // Powerup version - very bright and bold yellow
                        mainColor = Color.FromArgb(255, 255, 180, 50);
                        darkColor = Color.FromArgb(255, 200, 120, 20);
                        lightColor = Color.FromArgb(255, 255, 230, 150);
                        glowColor = Color.FromArgb(255, 255, 200, 100);
                    }
                    else
                    {
                        mainColor = Color.FromArgb(255, 215, 35);
                        darkColor = Color.FromArgb(170, 125, 0);
                        lightColor = Color.FromArgb(255, 245, 160);
                        glowColor = Color.FromArgb(255, 200, 150, 50);
                    }
                    break;
                case CandyType.PurplePlum:
                    if (isPowerup)
                    {
                        // Powerup version - very bright and bold purple
                        mainColor = Color.FromArgb(255, 180, 80, 255);
                        darkColor = Color.FromArgb(255, 120, 40, 180);
                        lightColor = Color.FromArgb(255, 230, 160, 255);
                        glowColor = Color.FromArgb(255, 200, 120, 255);
                    }
                    else
                    {
                        mainColor = Color.FromArgb(175, 75, 215);
                        darkColor = Color.FromArgb(95, 20, 125);
                        lightColor = Color.FromArgb(225, 155, 255);
                        glowColor = Color.FromArgb(255, 100, 50, 180);
                    }
                    break;
                case CandyType.ColorBomb:
                    // Color bomb - bright white core with rainbow glow
                    mainColor = Color.FromArgb(255, 255, 255);
                    darkColor = Color.FromArgb(200, 200, 200);
                    lightColor = Color.FromArgb(255, 255, 255);
                    glowColor = Color.FromArgb(255, 220, 180, 255);
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

            switch (candy.GetBaseType())
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
                case CandyType.ColorBomb:
                    // Color bomb has its own effect, no additional pixel drawing needed
                    break;
            }
            
            // Top shine overlay
            DrawCandyShine(g, inner, pulsePhase);

            // Draw special candy effects
            if (candy.IsStriped())
            {
                // Draw rainbow tile background first (subtle)
                DrawRainbowTileBackground(g, inner, pulsePhase);
                
                // Add outer glow - more subtle
                int outerGlowPulse = (int)(15 * Math.Sin(pulsePhase * Math.PI / 12));
                using (SolidBrush outerGlow = new SolidBrush(Color.FromArgb(30 + outerGlowPulse, 255, 255, 255)))
                {
                    Rectangle glowRect = new Rectangle(inner.X - 2, inner.Y - 2, inner.Width + 4, inner.Height + 4);
                    g.FillRoundedRectangle(outerGlow, glowRect, 8);
                }
            }
            else if (candy.IsColorBomb())
            {
                DrawColorBombEffect(g, inner, pulsePhase);
            }
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

        private void DrawRainbowTileBackground(Graphics g, Rectangle rect, int phase)
        {
            // Subtle animated rainbow gradient for tile background - much more transparent
            Color[] rainbowColors = new Color[]
            {
                Color.FromArgb(60, 255, 50, 50),
                Color.FromArgb(60, 255, 150, 0),
                Color.FromArgb(60, 255, 255, 50),
                Color.FromArgb(60, 50, 255, 50),
                Color.FromArgb(60, 50, 150, 255),
                Color.FromArgb(60, 150, 50, 255)
            };
            
            // Create animated rainbow gradient
            int colorOffset = (phase / 3) % rainbowColors.Length;
            
            using (LinearGradientBrush rainbowBrush = new LinearGradientBrush(
                rect,
                rainbowColors[colorOffset],
                rainbowColors[(colorOffset + 3) % rainbowColors.Length],
                LinearGradientMode.Horizontal))
            {
                g.FillRoundedRectangle(rainbowBrush, rect, 6);
            }
            
            // Add subtle shimmer effect - very transparent
            int shimmerOffset = (phase / 5) % (rect.Width + 20);
            int shimmerX = rect.X - 10 + shimmerOffset;
            
            if (shimmerX > rect.X - 20 && shimmerX < rect.Right + 20)
            {
                using (LinearGradientBrush shimmerBrush = new LinearGradientBrush(
                    new Rectangle(shimmerX, rect.Y, 15, rect.Height),
                    Color.FromArgb(0, 255, 255, 255),
                    Color.FromArgb(30, 255, 255, 255),
                    LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(shimmerBrush, new Rectangle(shimmerX, rect.Y, 15, rect.Height));
                }
            }
        }

        private void DrawColorBombEffect(Graphics g, Rectangle rect, int phase)
        {
            // Draw subtle rainbow tile background
            DrawRainbowTileBackground(g, rect, phase);
            
            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;
            
            // Add outer glow - more subtle
            int outerGlowPulse = (int)(15 * Math.Sin(phase * Math.PI / 20));
            using (SolidBrush outerGlow = new SolidBrush(Color.FromArgb(25 + outerGlowPulse, 255, 255, 255)))
            {
                Rectangle glowRect = new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6);
                g.FillRoundedRectangle(outerGlow, glowRect, 8);
            }
            
            // Central pulsing core - more subtle
            int centerPulse = (int)(8 * Math.Sin(phase * Math.PI / 15));
            using (SolidBrush centerGlow = new SolidBrush(Color.FromArgb(60 + centerPulse, 255, 255, 255)))
            {
                g.FillEllipse(centerGlow, centerX - 4, centerY - 4, 8, 8);
            }
            
            using (SolidBrush centerCore = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
            {
                g.FillEllipse(centerCore, centerX - 2, centerY - 2, 4, 4);
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
            
            // Extra sparkle
            using (SolidBrush sparkle = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
            {
                g.FillEllipse(sparkle, cx - 2, cy - 8, 2, 2);
                g.FillEllipse(sparkle, cx + 3, cy + 6, 2, 2);
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

        private void DrawProfessionalScorePanel(Graphics g, int x, int y, int width, int height, int currentScore, int targetScore)
        {
            // Ensure minimum dimensions
            width = Math.Max(width, 200);
            height = Math.Max(height, 30);
            
            // Background with gradient
            Rectangle panelRect = new Rectangle(x, y, width, height);
            
            // Add shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                g.FillRoundedRectangle(shadow, new Rectangle(x + 3, y + 3, width, height), 8);
            }
            
            // Main gradient background
            using (LinearGradientBrush bgGradient = new LinearGradientBrush(
                panelRect,
                Color.FromArgb(255, 70, 130, 180),
                Color.FromArgb(255, 40, 90, 140),
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(bgGradient, panelRect, 8);
            }
            
            // Inner glow highlight
            int innerWidth = Math.Max(width - 4, 1);
            int innerHeight = Math.Max(height / 2, 1);
            using (LinearGradientBrush innerGlow = new LinearGradientBrush(
                new Rectangle(x + 2, y + 2, innerWidth, innerHeight),
                Color.FromArgb(100, 255, 255, 255),
                Color.FromArgb(50, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(innerGlow, new Rectangle(x + 2, y + 2, innerWidth, innerHeight), 6);
            }
            
            // Border
            using (Pen border = new Pen(Color.FromArgb(255, 100, 160, 210), 2))
            {
                g.DrawRoundedRectangle(border, panelRect, 8);
            }
            
            // Icon
            using (Font iconFont = new Font("Segoe UI Symbol", 18, FontStyle.Bold))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("⭐", iconFont, new SolidBrush(Color.FromArgb(255, 255, 220, 100)), new Rectangle(x + 25, y, 30, height), sf);
                }
            }
            
            // Label
            int labelWidth = Math.Max(width - 80, 1);
            using (Font labelFont = new Font("Segoe UI", 9, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("SCORE", labelFont, new SolidBrush(Color.FromArgb(255, 200, 230, 255)), new Rectangle(x + 60, y + 5, labelWidth, height / 2), sf);
            }
            
            // Score value
            using (Font valueFont = new Font("Segoe UI", 14, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                string scoreText = $"{currentScore} / {targetScore}";
                g.DrawString(scoreText, valueFont, new SolidBrush(Color.White), new Rectangle(x + 60, y + height / 2, labelWidth, height / 2), sf);
            }
            
            // Progress bar
            int progressWidth = Math.Max(width - 40, 1);
            int progressHeight = Math.Max(4, 1);
            int progressX = x + 30;
            int progressY = y + height - 8;
            float progress = Math.Min(1f, (float)currentScore / targetScore);
            
            // Progress bar background
            using (SolidBrush progressBg = new SolidBrush(Color.FromArgb(150, 30, 70, 100)))
            {
                g.FillRoundedRectangle(progressBg, new Rectangle(progressX, progressY, progressWidth, progressHeight), 2);
            }
            
            // Progress bar fill
            int fillWidth = Math.Max((int)(progressWidth * progress), 1);
            using (LinearGradientBrush progressGradient = new LinearGradientBrush(
                new Rectangle(progressX, progressY, fillWidth, progressHeight),
                Color.FromArgb(255, 100, 200, 255),
                Color.FromArgb(255, 150, 230, 255),
                LinearGradientMode.Horizontal))
            {
                g.FillRoundedRectangle(progressGradient, new Rectangle(progressX, progressY, fillWidth, progressHeight), 2);
            }
        }

        private void DrawProfessionalGoldPanel(Graphics g, int x, int y, int width, int height, int gold)
        {
            // Ensure minimum dimensions
            width = Math.Max(width, 200);
            height = Math.Max(height, 30);
            
            // Background with gradient
            Rectangle panelRect = new Rectangle(x, y, width, height);
            
            // Add shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                g.FillRoundedRectangle(shadow, new Rectangle(x + 3, y + 3, width, height), 8);
            }
            
            // Main gradient background
            using (LinearGradientBrush bgGradient = new LinearGradientBrush(
                panelRect,
                Color.FromArgb(255, 200, 150, 50),
                Color.FromArgb(255, 170, 120, 30),
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(bgGradient, panelRect, 8);
            }
            
            // Inner glow highlight
            int innerWidth = Math.Max(width - 4, 1);
            int innerHeight = Math.Max(height / 2, 1);
            using (LinearGradientBrush innerGlow = new LinearGradientBrush(
                new Rectangle(x + 2, y + 2, innerWidth, innerHeight),
                Color.FromArgb(100, 255, 255, 200),
                Color.FromArgb(50, 255, 255, 200),
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(innerGlow, new Rectangle(x + 2, y + 2, innerWidth, innerHeight), 6);
            }
            
            // Border
            using (Pen border = new Pen(Color.FromArgb(255, 220, 180, 80), 2))
            {
                g.DrawRoundedRectangle(border, panelRect, 8);
            }
            
            // Icon
            using (Font iconFont = new Font("Segoe UI Symbol", 18, FontStyle.Bold))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("💰", iconFont, new SolidBrush(Color.FromArgb(255, 255, 230, 150)), new Rectangle(x + 25, y, 30, height), sf);
                }
            }
            
            // Label
            int labelWidth = Math.Max(width - 80, 1);
            using (Font labelFont = new Font("Segoe UI", 9, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("GOLD", labelFont, new SolidBrush(Color.FromArgb(255, 255, 230, 200)), new Rectangle(x + 60, y + 5, labelWidth, height / 2), sf);
            }
            
            // Gold value
            using (Font valueFont = new Font("Segoe UI", 14, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(gold.ToString(), valueFont, new SolidBrush(Color.White), new Rectangle(x + 60, y + height / 2, labelWidth, height / 2), sf);
            }
            
            // Decorative shine effect
            int shineX = x + (int)(width * 0.7) + (int)(10 * Math.Sin(pulsePhase * 0.05));
            int shineY = y + 5;
            int shineHeight = Math.Max(height - 10, 1);
            
            using (LinearGradientBrush shine = new LinearGradientBrush(
                new Rectangle(shineX, shineY, 15, shineHeight),
                Color.FromArgb(0, 255, 255, 255),
                Color.FromArgb(100, 255, 255, 200),
                LinearGradientMode.Horizontal))
            {
                g.FillRoundedRectangle(shine, new Rectangle(shineX, shineY, 15, shineHeight), 4);
            }
        }
    }
}

