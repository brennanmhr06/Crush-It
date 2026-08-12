using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;
using CrushIt.API;

namespace CrushIt.UI
{
    public enum PageType
    {
        Levels,
        Home,
        Achievements,
        Social
    }

    public class MainFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private readonly IApiClient? apiClient;

        private StyleParticle[] backgroundParticles = Array.Empty<StyleParticle>();
        private System.Windows.Forms.Timer animationTimer = null!;
        private Random particleRand = new Random();
        private int pulsePhase = 0;

        private PageType currentPage = PageType.Levels;
        private PageType targetPage = PageType.Levels;
        private float transitionProgress = 0f;
        private bool isTransitioning = false;
        private const float TransitionSpeed = 0.08f;


        private LevelNode[] levels = new LevelNode[0];
        private int levelsPerRow = 10;
        private int maxRows = 4;
        private int baseLevelNumber = 1;
        private int totalLevelsCompleted = 0;


        private bool isEditingUsername = false;
        private string editingUsername = "";
        private int usernameCursorBlinkPhase = 0;
        private Rectangle pencilIconRect = Rectangle.Empty;
        private int levelsCompleted;
        private int highestLevel;
        private int gold;
        private int highestScore;
        private int totalMatches;
        private int achievementsUnlocked;
        private int achievementsTotal;
        private int daysPlaying;
        private string memberSinceText = "--";
        private string rankTitle = "New Crusher";


        private List<Achievement> userAchievements = null!;
        private int scrollOffset = 0;
        private int targetScrollOffset = 0;
        private List<CoinParticle> coinParticles = new List<CoinParticle>();
        private bool isCoinAnimating = false;



        public MainFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;

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

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            InitializeParticles();
            LoadAllPageData();
            
            // Ensure we start on Levels page
            currentPage = PageType.Levels;
            targetPage = PageType.Levels;
            isTransitioning = false;
            transitionProgress = 0f;
            
            StartAnimation();

            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It!";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += MainFrame_KeyDown;
            this.MouseDown += MainFrame_MouseDown;
            this.FormClosed += MainFrame_FormClosed;
            this.FormClosed += (s, e) => {
                SoundManager.StopBackgroundMusic();
                if (Application.OpenForms.Count == 0)
                {
                    SoundManager.Cleanup();
                    Application.Exit();
                }
            };
            this.MouseMove += MainFrame_MouseMove;
            this.MouseLeave += MainFrame_MouseLeave;
            this.MouseWheel += MainFrame_MouseWheel;


            InitializeHomeControls();
        }

        private void InitializeHomeControls()
        {
            // Username editing is now handled inline in the DrawProfileCard method
        }

        private void InitializeParticles()
        {
            backgroundParticles = CrushItStyleHelper.CreateParticles(particleRand, 30, 890, 80, 480); // Reduced from 45 to 30
        }



        private void LoadAllPageData()
        {
            LoadLevelsData();
            LoadHomeData();
            LoadAchievementsData();
        }

        private void LoadLevelsData()
        {
            CalculateRowProgression();
            GenerateLevelsForCurrentRows();
            UpdateLevelStatus();
        }

        public void RefreshLevelsData()
        {
            LoadLevelsData();
            this.Invalidate();
        }

        private void CalculateRowProgression()
        {
            if (currentUser.CompletedLevels != null && currentUser.CompletedLevels.Count > 0)
            {
                totalLevelsCompleted = currentUser.CompletedLevels.Count;
                int completedRows = totalLevelsCompleted / levelsPerRow;

                if (completedRows >= maxRows)
                {
                    int cycles = completedRows / maxRows;
                    baseLevelNumber = cycles * maxRows * levelsPerRow + 1;
                }
                else
                {
                    baseLevelNumber = 1;
                }
            }
            else
            {
                baseLevelNumber = 1;
            }
        }

        private void GenerateLevelsForCurrentRows()
        {
            List<LevelNode> levelList = new List<LevelNode>();

            for (int row = 1; row <= maxRows; row++)
            {
                int rowLevelCount = levelsPerRow;
                int rowStartLevel = baseLevelNumber + (row - 1) * levelsPerRow;

                for (int i = 0; i < rowLevelCount; i++)
                {
                    int levelNum = rowStartLevel + i;
                    int col = i % 10;
                    int rowInSet = i / 10;

                    int x = 80 + col * 85;
                    int y = 140 + (row - 1) * 100 + (col % 2 == 0 ? 0 : 15);

                    levelList.Add(new LevelNode
                    {
                        Number = levelNum,
                        X = x,
                        Y = y,
                        Unlocked = false,
                        Completed = false
                    });
                }
            }

            levels = levelList.ToArray();
        }

        private void UpdateLevelStatus()
        {
            int maxUnlockedLevel = 1;

            if (currentUser.CompletedLevels != null && currentUser.CompletedLevels.Count > 0)
            {
                foreach (int levelNum in currentUser.CompletedLevels)
                {
                    if (levelNum > maxUnlockedLevel)
                    {
                        maxUnlockedLevel = levelNum;
                    }
                }
                maxUnlockedLevel++;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                int levelNum = levels[i].Number;
                levels[i].Completed = currentUser.CompletedLevels != null && currentUser.CompletedLevels.Contains(levelNum);
                levels[i].Unlocked = levelNum <= maxUnlockedLevel;
                
                // Set difficulty based on level number (1-5 scale)
                levels[i].Difficulty = ((levelNum - 1) % 5) + 1;
                
                // Set stars for completed levels (random for demo)
                if (levels[i].Completed)
                {
                    levels[i].Stars = (levelNum % 3) + 1; // 1-3 stars
                }
                else
                {
                    levels[i].Stars = 0;
                }
                
                // Set progress for unlocked but incomplete levels
                if (levels[i].Unlocked && !levels[i].Completed)
                {
                    levels[i].Progress = (levelNum * 17) % 100; // Sample progress data
                }
                else
                {
                    levels[i].Progress = 0;
                }
            }

            CheckRowCompletion();
        }

        private void CheckRowCompletion()
        {
            if (currentUser.CompletedLevels == null) return;

            int cycleStartLevel = baseLevelNumber;
            int cycleEndLevel = baseLevelNumber + (maxRows * levelsPerRow) - 1;

            int completedInCycle = 0;
            foreach (int levelNum in currentUser.CompletedLevels)
            {
                if (levelNum >= cycleStartLevel && levelNum <= cycleEndLevel)
                {
                    completedInCycle++;
                }
            }

            if (completedInCycle >= maxRows * levelsPerRow)
            {
                int cycles = totalLevelsCompleted / (maxRows * levelsPerRow);
                baseLevelNumber = (cycles + 1) * maxRows * levelsPerRow + 1;
                GenerateLevelsForCurrentRows();
                UpdateLevelStatusWithoutCheck();
            }
        }

        private void UpdateLevelStatusWithoutCheck()
        {
            int maxUnlockedLevel = 1;

            if (currentUser.CompletedLevels != null && currentUser.CompletedLevels.Count > 0)
            {
                foreach (int levelNum in currentUser.CompletedLevels)
                {
                    if (levelNum > maxUnlockedLevel)
                    {
                        maxUnlockedLevel = levelNum;
                    }
                }
                maxUnlockedLevel++;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                int levelNum = levels[i].Number;
                levels[i].Completed = currentUser.CompletedLevels != null && currentUser.CompletedLevels.Contains(levelNum);
                levels[i].Unlocked = levelNum <= maxUnlockedLevel;
            }
        }

        private async void LoadHomeData()
        {
            if (string.IsNullOrEmpty(currentUser.Username))
            {
                Random rand = new Random();
                currentUser.Username = "crushing" + rand.Next(1000, 9999);

                var usersCollection = database.GetCollection<UserAccount>("users");
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Id, currentUser.Id);
                var update = Builders<UserAccount>.Update.Set(u => u.Username, currentUser.Username);
                await usersCollection.UpdateOneAsync(filter, update);
            }

            levelsCompleted = currentUser.CompletedLevels?.Count ?? 0;
            highestLevel = currentUser.CompletedLevels != null && currentUser.CompletedLevels.Count > 0
                ? currentUser.CompletedLevels.Max()
                : 0;
            gold = currentUser.Gold;
            highestScore = currentUser.HighestScore;
            totalMatches = currentUser.TotalMatches;
            achievementsTotal = AchievementDefinitions.AllAchievements.Length;
            achievementsUnlocked = currentUser.Achievements?.Count(a => a.IsUnlocked) ?? 0;

            if (currentUser.CreatedAt != default)
            {
                memberSinceText = currentUser.CreatedAt.ToLocalTime().ToString("MMM dd, yyyy");
                daysPlaying = Math.Max(1, (int)(DateTime.UtcNow - currentUser.CreatedAt).TotalDays);
            }
            else
            {
                memberSinceText = "--";
                daysPlaying = 0;
            }

            rankTitle = GetRankTitle(levelsCompleted, highestScore, achievementsUnlocked);
        }

        private static string GetRankTitle(int levels, int bestScore, int achievements)
        {
            if (levels >= 30 || bestScore >= 10000) return "Legendary Crusher";
            if (levels >= 20 || bestScore >= 5000) return "Master Crusher";
            if (levels >= 10 || achievements >= 8) return "Rising Star";
            if (levels >= 5 || bestScore >= 1000) return "Skilled Matcher";
            if (levels >= 1) return "Beginner Crusher";
            return "New Crusher";
        }



        private void LoadAchievementsData()
        {
            userAchievements = currentUser.Achievements ?? new List<Achievement>();

            foreach (var definition in AchievementDefinitions.AllAchievements)
            {
                var existing = userAchievements.FirstOrDefault(a => a.Type == definition.Type);
                if (existing == null)
                {
                    var newAchievement = new Achievement(definition.Type, definition.Name, definition.Description, definition.IconColor, definition.GoldReward);
                    userAchievements.Add(newAchievement);
                }
                else
                {
                    if (existing.GoldReward == 0)
                    {
                        existing.GoldReward = definition.GoldReward;
                    }
                }
            }

            // Sort achievements: Almost completed > Ready to claim > Other locked > Claimed (at bottom)
            userAchievements = userAchievements
                .OrderByDescending(a => !a.IsUnlocked && CalculateAchievementProgress(a) >= 0.7) // Almost completed - highest priority
                .ThenByDescending(a => a.IsUnlocked && !a.IsClaimed) // Ready to claim - second priority
                .ThenBy(a => a.IsUnlocked && a.IsClaimed) // Claimed - lowest priority (goes to bottom)
                .ThenBy(a => a.Type) // Locked achievements in type order
                .ToList();
        }

        private void StartAnimation()
        {
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            pulsePhase++;
            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 120);


            if (isTransitioning)
            {
                transitionProgress += TransitionSpeed;
                if (transitionProgress >= 1f)
                {
                    transitionProgress = 1f;
                    isTransitioning = false;
                    currentPage = targetPage;
                }
            }


            UpdateControlVisibility();

            // Smooth scroll animation
            if (scrollOffset != targetScrollOffset)
            {
                int diff = targetScrollOffset - scrollOffset;
                scrollOffset += diff / 10;
                if (Math.Abs(diff) < 1)
                    scrollOffset = targetScrollOffset;
            }

            if (isCoinAnimating)
            {
                UpdateCoinParticles();
            }

            this.Invalidate();
        }

        private void MainFrame_MouseDown(object? sender, MouseEventArgs e)
        {
            if (isTransitioning) return;

            if (isEditingUsername)
            {
                // Click outside username area to finish editing
                Rectangle usernameArea = new Rectangle(150, 103, 400, 40);
                if (!usernameArea.Contains(e.Location))
                {
                    FinishEditing();
                }
                return;
            }

            // Check if pencil icon was clicked
            if (!pencilIconRect.IsEmpty && pencilIconRect.Contains(e.Location))
            {
                StartEditingUsername();
                return;
            }

            if (CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem clickedNav))
            {
                HandleNavigation(clickedNav);
                return;
            }


            if (currentPage == PageType.Levels)
            {
                HandleLevelsClick(e);
            }
            else if (currentPage == PageType.Achievements)
            {
                HandleAchievementsClick(e);
            }
            else if (currentPage == PageType.Social)
            {
                OpenSocialFrame();
            }
            else if (currentPage == PageType.Home)
            {
                HandleHomeClick(e);
            }
        }

        private void MainFrame_MouseMove(object? sender, MouseEventArgs e)
        {
            // No special handling needed
        }

        private void MainFrame_MouseLeave(object? sender, EventArgs e)
        {
            // No special handling needed
        }

        private void MainFrame_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (currentPage == PageType.Achievements)
            {
                int scrollAmount = e.Delta / 10;
                targetScrollOffset += scrollAmount;

                // Calculate scroll bounds
                int startY = 150;
                int achievementHeight = 95;
                int gap = 18;
                int totalHeight = userAchievements.Count * (achievementHeight + gap) + startY;
                int maxScroll = Math.Max(0, totalHeight - (this.ClientSize.Height - 100));

                if (targetScrollOffset < -maxScroll) targetScrollOffset = -maxScroll;
                if (targetScrollOffset > 0) targetScrollOffset = 0;

                this.Invalidate();
            }
        }

        private void HandleHomeClick(MouseEventArgs e)
        {
            // Home page clicks handled by controls
        }

        private void OpenSocialFrame()
        {
            SocialFrame socialFrame = new SocialFrame(currentUser, database);
            socialFrame.Show();
            this.Hide();
            this.Dispose();
        }

        private void HandleNavigation(NavItem nav)
        {
            SoundManager.PlaySound(SoundType.ButtonClick);
            SoundManager.PlaySound(SoundType.Navigation);

            PageType newPage = nav switch
            {
                NavItem.Home => PageType.Home,
                NavItem.Levels => PageType.Levels,
                NavItem.Achievements => PageType.Achievements,
                NavItem.Social => PageType.Social,
                _ => PageType.Levels
            };

            if (newPage != currentPage)
            {
                targetPage = newPage;
                isTransitioning = true;
                transitionProgress = 0f;
            }
        }

        private void HandleLevelsClick(MouseEventArgs e)
        {
            foreach (var level in levels)
            {
                if (level.Unlocked)
                {
                    int nodeRadius = 30;
                    int dx = e.X - level.X;
                    int dy = e.Y - level.Y;
                    if (dx * dx + dy * dy <= nodeRadius * nodeRadius)
                    {
                        SoundManager.PlaySound(SoundType.ButtonClick);
                        // Close any existing GameFrame first
                        foreach (Form form in Application.OpenForms)
                        {
                            if (form is GameFrame gameFrame)
                            {
                                gameFrame.Close();
                                gameFrame.Dispose();
                            }
                        }

                        GameFrame game = new GameFrame(currentUser, level.Number);
                        game.Show();
                        this.Hide();
                        this.Dispose();
                        break;
                    }
                }
            }
        }

        private void HandleAchievementsClick(MouseEventArgs e)
        {
            int startY = 150;
            int achievementHeight = 95;
            int gap = 18;
            int availableWidth = 800;
            int startX = 50;
            int y = startY + scrollOffset;

            foreach (var achievement in userAchievements)
            {
                if (y + achievementHeight < 100 || y > this.ClientSize.Height - 100)
                {
                    y += achievementHeight + gap;
                    continue;
                }

                Rectangle achievementRect = new Rectangle(startX, y, availableWidth, achievementHeight);

                // Click anywhere on the card to claim if it's ready
                if (achievement.IsUnlocked && !achievement.IsClaimed && achievementRect.Contains(e.X, e.Y))
                {
                    ClaimAchievement(achievement);
                    break;
                }

                y += achievementHeight + gap;
            }
        }

        private async void ClaimAchievement(Achievement achievement)
        {
            achievement.IsClaimed = true;
            currentUser.Gold += achievement.GoldReward;

            try
            {
                var usersCollection = database.GetCollection<UserAccount>("users");
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Email, currentUser.Email);
                var update = Builders<UserAccount>.Update
                    .Set(u => u.Achievements, userAchievements)
                    .Inc(u => u.Gold, achievement.GoldReward);
                await usersCollection.UpdateOneAsync(filter, update);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to claim achievement", ex);
            }

            // Re-sort achievements to move claimed ones to bottom
            LoadAchievementsData();

            StartCoinAnimation();
            LoadHomeData();
        }

        private void StartCoinAnimation()
        {
            isCoinAnimating = true;
            coinParticles.Clear();

            for (int i = 0; i < 50; i++)
            {
                coinParticles.Add(new CoinParticle
                {
                    X = particleRand.Next(0, this.ClientSize.Width),
                    Y = particleRand.Next(-100, -50),
                    SpeedX = (float)(particleRand.NextDouble() * 4 - 2),
                    SpeedY = (float)(particleRand.NextDouble() * 3 + 2),
                    Size = particleRand.Next(15, 30),
                    Alpha = 255
                });
            }
        }

        private void UpdateCoinParticles()
        {
            for (int i = coinParticles.Count - 1; i >= 0; i--)
            {
                var p = coinParticles[i];
                p.X += p.SpeedX;
                p.Y += p.SpeedY;
                p.Alpha -= 3;
                p.Alpha = Math.Max(0, p.Alpha);

                if (p.Alpha <= 0 || p.Y > this.ClientSize.Height)
                {
                    coinParticles.RemoveAt(i);
                }
            }

            if (coinParticles.Count == 0)
            {
                isCoinAnimating = false;
            }
        }


        private void StartEditingUsername()
        {
            SoundManager.PlaySound(SoundType.ButtonClick);
            isEditingUsername = true;
            editingUsername = currentUser.Username;
            this.Focus();
        }

        private async void FinishEditing()
        {
            isEditingUsername = false;
            string newUsername = editingUsername.Trim();

            if (!string.IsNullOrEmpty(newUsername) && newUsername != currentUser.Username)
            {
                currentUser.Username = newUsername;

                var usersCollection = database.GetCollection<UserAccount>("users");
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Id, currentUser.Id);
                var update = Builders<UserAccount>.Update.Set(u => u.Username, newUsername);
                await usersCollection.UpdateOneAsync(filter, update);
            }
        }

        private void UpdateControlVisibility()
        {

            bool showHomeControls = currentPage == PageType.Home && !isTransitioning;
        }

        private void MainFrame_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (isEditingUsername)
                {
                    editingUsername = currentUser.Username;
                    FinishEditing();
                }
                else
                {
                    this.Close();
                }
            }
            else if (isEditingUsername)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    FinishEditing();
                }
                else if (e.KeyCode == Keys.Back)
                {
                    if (editingUsername.Length > 0)
                    {
                        editingUsername = editingUsername.Substring(0, editingUsername.Length - 1);
                    }
                }
                else if (!e.Control && !e.Alt && e.KeyCode != Keys.ShiftKey &&
                         e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu &&
                         e.KeyCode != Keys.LButton && e.KeyCode != Keys.RButton &&
                         e.KeyCode != Keys.MButton && e.KeyCode != Keys.XButton1 &&
                         e.KeyCode != Keys.XButton2)
                {
                    char keyChar = GetCharFromKey(e.KeyCode);
                    if (keyChar != '\0' && editingUsername.Length < 20)
                    {
                        editingUsername += keyChar;
                    }
                }
            }
        }

        private char GetCharFromKey(Keys key)
        {
            // Simple mapping for common keys
            bool shiftPressed = (ModifierKeys & Keys.Shift) != 0;

            if (key >= Keys.A && key <= Keys.Z)
            {
                return shiftPressed ? (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A));
            }
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }
            if (key == Keys.Space)
            {
                return ' ';
            }
            if (key == Keys.OemMinus || key == Keys.Subtract)
            {
                return shiftPressed ? '_' : '-';
            }
            if (key == Keys.OemPeriod || key == Keys.Decimal)
            {
                return '.';
            }
            return '\0';
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);


            float currentPageOffset = isTransitioning ? -transitionProgress * this.ClientSize.Width : 0;
            float targetPageOffset = isTransitioning ? (1 - transitionProgress) * this.ClientSize.Width : 0;


            g.TranslateTransform(currentPageOffset, 0);
            DrawPage(g, currentPage);
            g.TranslateTransform(-currentPageOffset, 0);


            if (isTransitioning)
            {
                g.TranslateTransform(targetPageOffset, 0);
                DrawPage(g, targetPage);
                g.TranslateTransform(-targetPageOffset, 0);
            }


            if (isCoinAnimating)
            {
                DrawCoinParticles(g);
            }


            CrushItStyleHelper.DrawNavigationBar(g, this.ClientSize.Width, this.ClientSize.Height,
                currentPage == PageType.Home ? NavItem.Home :
                currentPage == PageType.Levels ? NavItem.Levels :
                currentPage == PageType.Achievements ? NavItem.Achievements : NavItem.Social,
                pulsePhase);
        }

        private void DrawPage(Graphics g, PageType page)
        {
            switch (page)
            {
                case PageType.Levels:
                    DrawLevelsPage(g);
                    break;
                case PageType.Home:
                    DrawHomePage(g);
                    break;
                case PageType.Achievements:
                    DrawAchievementsPage(g);
                    break;
                case PageType.Social:
                    DrawSocialPlaceholder(g);
                    break;
            }
        }

        private void DrawLevelsPage(Graphics g)
        {
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(150, 15, 600, 55), "LEVELS", 24);
            DrawLevelPath(g);

            foreach (var level in levels)
                DrawLevelNode(g, level);
        }

        private void DrawLevelPath(Graphics g)
        {
            int pathStart = 40;
            int pathEnd = 860;
            int pathHeight = 20;

            for (int row = 1; row <= maxRows; row++)
            {
                int pathY = 180 + (row - 1) * 100;
                Rectangle pathRect = new Rectangle(pathStart, pathY, pathEnd - pathStart, pathHeight);

                // Shadow
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                    g.FillRectangle(shadow, pathRect.X + 3, pathRect.Y + 3, pathRect.Width, pathRect.Height);

                // Gradient path
                using (LinearGradientBrush pathGradient = new LinearGradientBrush(
                    pathRect,
                    Color.FromArgb(255, 180, 140, 220),
                    Color.FromArgb(255, 120, 80, 180),
                    LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(pathGradient, pathRect);
                }

                // Path border
                using (Pen pathBorder = new Pen(Color.FromArgb(255, 100, 60, 160), 2))
                    g.DrawRectangle(pathBorder, pathRect);

                // Path highlight
                using (SolidBrush highlight = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillRectangle(highlight, pathRect.X + 2, pathRect.Y + 2, pathRect.Width - 4, 4);

                // Draw connection dots between levels
                for (int col = 0; col < levelsPerRow - 1; col++)
                {
                    int dotX = pathStart + 45 + col * 85;
                    int dotY = pathY + pathHeight / 2;
                    
                    using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 150)))
                        g.FillEllipse(dotBrush, dotX - 3, dotY - 3, 6, 6);
                }
            }
        }

        private void DrawLevelNode(Graphics g, LevelNode level)
        {
            int size = 64;
            int x = level.X - size / 2;
            int y = level.Y - size / 2;
            Rectangle nodeRect = new Rectangle(x, y, size, size);

            // Shadow effect
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                g.FillEllipse(shadow, x + 4, y + 4, size, size);

            Color gradientStart, gradientEnd, glowColor, borderColor;
            bool showGlow = false;

            if (level.Completed)
            {
                gradientStart = Color.FromArgb(255, 150, 255, 150);
                gradientEnd = Color.FromArgb(255, 80, 200, 80);
                glowColor = Color.FromArgb(255, 255, 255, 100);
                borderColor = Color.FromArgb(255, 50, 180, 50);
                showGlow = true;
            }
            else if (level.Unlocked)
            {
                gradientStart = Color.FromArgb(255, 220, 180, 255);
                gradientEnd = Color.FromArgb(255, 160, 100, 220);
                glowColor = Color.FromArgb(255, 255, 200, 100);
                borderColor = Color.FromArgb(255, 120, 80, 180);
                showGlow = true;
            }
            else
            {
                gradientStart = Color.FromArgb(255, 100, 85, 120);
                gradientEnd = Color.FromArgb(255, 70, 60, 95);
                glowColor = Color.FromArgb(255, 60, 50, 80);
                borderColor = Color.FromArgb(255, 55, 45, 75);
            }

            // Glow effect for unlocked/completed levels
            if (showGlow)
            {
                int glowPulse = (int)(30 * Math.Sin(pulsePhase * Math.PI / 45));
                int glowAlpha = Math.Max(0, Math.Min(255, 25 + glowPulse));
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, glowColor)))
                    g.FillEllipse(glow, x - 6, y - 6, size + 12, size + 12);
            }

            // Main circular gradient
            using (LinearGradientBrush circleGradient = new LinearGradientBrush(
                nodeRect, gradientStart, gradientEnd, LinearGradientMode.Vertical))
            {
                g.FillEllipse(circleGradient, nodeRect);
            }

            // Border
            using (Pen borderPen = new Pen(borderColor, 3))
                g.DrawEllipse(borderPen, nodeRect);

            // Inner highlight
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                g.FillEllipse(highlight, x + 4, y + 4, size - 16, size - 16);

            // Level number
            using (Font numFont = new Font("Comic Sans MS", 24, FontStyle.Bold))
            {
                Color textColor = level.Unlocked ? Color.White : Color.FromArgb(150, 130, 130, 145);
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                CrushItStyleHelper.DrawOutlinedText(g, level.Number.ToString(), numFont, nodeRect, textColor, Color.Black, 2, sf);
            }

            // Stars for completed levels
            if (level.Stars > 0)
            {
                int starSize = 10;
                int starY = y + size + 8;
                int totalStarWidth = level.Stars * (starSize + 6) - 6;
                int startX = x + (size - totalStarWidth) / 2;

                for (int i = 0; i < level.Stars; i++)
                {
                    int starX = startX + i * (starSize + 6);
                    Rectangle starRect = new Rectangle(starX, starY, starSize, starSize);
                    
                    using (SolidBrush starBrush = new SolidBrush(Color.FromArgb(255, 255, 215, 0)))
                    {
                        g.FillEllipse(starBrush, starRect);
                    }
                    using (Pen starBorder = new Pen(Color.FromArgb(255, 200, 150, 0), 1))
                    {
                        g.DrawEllipse(starBorder, starRect);
                    }
                }
            }

            // Difficulty indicator (small dots)
            if (level.Unlocked)
            {
                int dotSize = 4;
                int dotY = y - 12;
                int totalDotWidth = level.Difficulty * (dotSize + 3) - 3;
                int startX = x + (size - totalDotWidth) / 2;

                for (int i = 0; i < level.Difficulty; i++)
                {
                    int dotX = startX + i * (dotSize + 3);
                    Color dotColor = i < 2 ? Color.FromArgb(255, 100, 255, 100) : 
                                     i < 4 ? Color.FromArgb(255, 255, 200, 50) : 
                                     Color.FromArgb(255, 255, 80, 80);
                    
                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                }
            }

            // Progress bar for in-progress levels
            if (level.Unlocked && !level.Completed && level.Progress > 0)
            {
                int barWidth = size - 8;
                int barHeight = 4;
                int barX = x + 4;
                int barY = y + size + 4;

                // Background
                using (SolidBrush bgBar = new SolidBrush(Color.FromArgb(150, 50, 50, 70)))
                    g.FillRectangle(bgBar, barX, barY, barWidth, barHeight);

                // Progress
                int progressWidth = (int)(barWidth * level.Progress / 100.0);
                using (SolidBrush progressBar = new SolidBrush(Color.FromArgb(255, 100, 200, 255)))
                    g.FillRectangle(progressBar, barX, barY, progressWidth, barHeight);
            }

            // Lock icon for locked levels
            if (!level.Unlocked)
            {
                using (Font lockFont = new Font("Segoe UI Emoji", 18))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("🔒", lockFont, new SolidBrush(Color.FromArgb(150, 100, 100, 120)), 
                        new RectangleF(x, y + size + 4, size, 20), sf);
                }
            }
        }

        private void DrawHomePage(Graphics g)
        {
            DrawTitleBanner(g);
            DrawProfileCard(g);
            DrawStatsGrid(g);
            DrawProgressSection(g);
        }

        private void DrawTitleBanner(Graphics g)
        {
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(250, 15, 400, 55), "STATS");
        }

        private void DrawProfileCard(Graphics g)
        {
            Rectangle card = new Rectangle(50, 85, 800, 95);
            CrushItStyleHelper.DrawPanel(g, card, Color.FromArgb(255, 150, 110, 200), Color.FromArgb(255, 110, 70, 170), Color.FromArgb(255, 90, 60, 140));

            Rectangle avatarRect = new Rectangle(card.X + 20, card.Y + 15, 65, 65);
            using (System.Drawing.Drawing2D.LinearGradientBrush avatarBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                avatarRect,
                Color.FromArgb(255, 255, 200, 80),
                Color.FromArgb(255, 255, 140, 40),
                System.Drawing.Drawing2D.LinearGradientMode.Vertical))
            {
                g.FillEllipse(avatarBrush, avatarRect);
            }
            using (Pen avatarBorder = new Pen(Color.FromArgb(255, 120, 80, 20), 3))
            {
                g.DrawEllipse(avatarBorder, avatarRect);
            }

            string initial = string.IsNullOrEmpty(currentUser.Username)
                ? "?"
                : currentUser.Username.Substring(0, 1).ToUpper();
            using (Font avatarFont = new Font("Comic Sans MS", 28, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, initial, avatarFont, avatarRect, Color.White, Color.FromArgb(180, 80, 40, 0), 2, sf);
            }

            Rectangle rankBadge = new Rectangle(card.X + 100, card.Y + 58, 280, 24);
            using (SolidBrush rankBg = new SolidBrush(Color.FromArgb(180, 40, 20, 60)))
            {
                g.FillRectangle(rankBg, rankBadge);
            }
            using (Font rankFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("★ " + rankTitle + " ★", rankFont, Brushes.Gold, rankBadge, sf);
            }

            // Draw username with inline editing support
            Rectangle usernameRect = new Rectangle(card.X + 100, card.Y + 18, 280, 30);
            using (Font usernameFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                string displayUsername = isEditingUsername ? editingUsername : currentUser.Username;
                string usernameText = "@" + displayUsername;

                if (isEditingUsername)
                {
                    // Draw faded highlight background
                    using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 200)))
                    {
                        SizeF textSize = g.MeasureString(usernameText, usernameFont);
                        Rectangle highlightRect = new Rectangle(usernameRect.X, usernameRect.Y + 5, (int)textSize.Width + 10, 20);
                        g.FillRectangle(highlightBrush, highlightRect);
                    }

                    // Draw username text
                    g.DrawString(usernameText, usernameFont, Brushes.White, usernameRect, sf);

                    // Draw typing cursor
                    usernameCursorBlinkPhase = (usernameCursorBlinkPhase + 1) % 30;
                    if (usernameCursorBlinkPhase < 15)
                    {
                        SizeF textSize = g.MeasureString(usernameText, usernameFont);
                        int cursorX = usernameRect.X + (int)textSize.Width + 2;
                        using (Pen cursorPen = new Pen(Color.White, 2))
                        {
                            g.DrawLine(cursorPen, cursorX, usernameRect.Y + 8, cursorX, usernameRect.Y + 22);
                        }
                    }
                }
                else
                {
                    g.DrawString("@" + currentUser.Username, usernameFont, Brushes.White, usernameRect, sf);

                    // Draw pencil icon
                    pencilIconRect = new Rectangle(usernameRect.Right + 5, usernameRect.Y + 5, 25, 25);
                    using (Font pencilFont = new Font("Segoe UI Emoji", 18, FontStyle.Bold))
                    using (StringFormat pencilSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("✏️", pencilFont, Brushes.White, pencilIconRect, pencilSf);
                    }
                }
            }

            using (Font infoFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
            using (Font infoValueFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
            {
                int infoX = card.Right - 250;
                g.DrawString("Member since", infoFont, new SolidBrush(Color.FromArgb(220, 255, 255, 255)), infoX, card.Y + 18);
                g.DrawString(memberSinceText, infoValueFont, Brushes.White, infoX, card.Y + 36);

                string daysText = daysPlaying > 0 ? $"{daysPlaying} day{(daysPlaying == 1 ? "" : "s")} playing" : "Just joined!";
                g.DrawString(daysText, infoFont, new SolidBrush(Color.FromArgb(200, 255, 230, 150)), infoX, card.Y + 60);
            }
        }

        private void DrawStatsGrid(Graphics g)
        {
            Rectangle gridArea = new Rectangle(50, 195, 800, 230);
            CrushItStyleHelper.DrawPanel(g, gridArea, Color.FromArgb(255, 130, 95, 185), Color.FromArgb(255, 95, 60, 155), Color.FromArgb(255, 80, 50, 130));

            using (Font sectionFont = new Font("Comic Sans MS", 13, FontStyle.Bold))
            {
                CrushItStyleHelper.DrawOutlinedText(g, "PLAYER STATS", sectionFont, new Rectangle(gridArea.X, gridArea.Y + 6, gridArea.Width, 24), Color.White, Color.Black, 1);
            }

            int cardW = 240;
            int cardH = 85;
            int gapX = 20;
            int gapY = 12;
            int startX = gridArea.X + 25;
            int startY = gridArea.Y + 38;

            CrushItStyleHelper.DrawStatCard(g, new Rectangle(startX, startY, cardW, cardH),
                "🎯", "Levels Beaten", levelsCompleted.ToString(), Color.FromArgb(255, 100, 200, 255));
            CrushItStyleHelper.DrawStatCard(g, new Rectangle(startX + cardW + gapX, startY, cardW, cardH),
                "🏆", "Highest Level", highestLevel > 0 ? highestLevel.ToString() : "—", Color.FromArgb(255, 255, 180, 60));
            CrushItStyleHelper.DrawStatCard(g, new Rectangle(startX + (cardW + gapX) * 2, startY, cardW, cardH),
                "💰", "Gold", gold.ToString("N0"), Color.FromArgb(255, 255, 215, 50));

            CrushItStyleHelper.DrawStatCard(g, new Rectangle(startX, startY + cardH + gapY, cardW, cardH),
                "⭐", "Best Score", highestScore > 0 ? highestScore.ToString("N0") : "—", Color.FromArgb(255, 255, 140, 200));
            CrushItStyleHelper.DrawStatCard(g, new Rectangle(startX + cardW + gapX, startY + cardH + gapY, cardW, cardH),
                "💥", "Total Matches", totalMatches.ToString("N0"), Color.FromArgb(255, 140, 255, 160));
            CrushItStyleHelper.DrawStatCard(g, new Rectangle(startX + (cardW + gapX) * 2, startY + cardH + gapY, cardW, cardH),
                "🏅", "Achievements", $"{achievementsUnlocked}/{achievementsTotal}", Color.FromArgb(255, 200, 160, 255));
        }

        private void DrawProgressSection(Graphics g)
        {
            Rectangle section = new Rectangle(50, 410, 800, 55);
            CrushItStyleHelper.DrawPanel(g, section, Color.FromArgb(255, 120, 90, 175), Color.FromArgb(255, 90, 60, 145), Color.FromArgb(255, 70, 45, 120));

            int barX = section.X + 20;
            int barW = (section.Width - 60) / 2;

            CrushItStyleHelper.DrawProgressBar(g, new Rectangle(barX, section.Y + 18, barW, 22),
                "Level Progress", levelsCompleted, Math.Max(levelsCompleted, 40), Color.FromArgb(255, 80, 200, 255));

            CrushItStyleHelper.DrawProgressBar(g, new Rectangle(barX + barW + 20, section.Y + 18, barW, 22),
                "Achievements", achievementsUnlocked, achievementsTotal, Color.FromArgb(255, 255, 180, 60));
        }

        private void DrawAchievementsPage(Graphics g)
        {
            DrawAchievementsHeader(g);
            DrawAchievementsList(g);
        }

        private void DrawAchievementsHeader(Graphics g)
        {
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(250, 15, 400, 55), "ACHIEVEMENTS", 24);

            int unlockedCount = userAchievements.Count(a => a.IsUnlocked);
            int totalCount = userAchievements.Count;

            Rectangle progressSection = new Rectangle(50, 85, 800, 55);
            CrushItStyleHelper.DrawPanel(g, progressSection,
                Color.FromArgb(255, 120, 90, 175),
                Color.FromArgb(255, 90, 60, 145),
                Color.FromArgb(255, 70, 45, 120));

            CrushItStyleHelper.DrawProgressBar(g, new Rectangle(progressSection.X + 20, progressSection.Y + 18, progressSection.Width - 40, 22),
                "Achievements Progress", unlockedCount, totalCount, Color.FromArgb(255, 255, 180, 60));
        }

        private void DrawAchievementsList(Graphics g)
        {
            int startY = 150;
            int achievementHeight = 95;
            int gap = 18;
            int availableWidth = 800;
            int startX = 50;

            // Calculate scroll bounds
            int totalHeight = userAchievements.Count * (achievementHeight + gap) + startY;
            int maxScroll = Math.Max(0, totalHeight - (this.ClientSize.Height - 100));

            // Ensure scroll offset is within bounds
            if (targetScrollOffset < -maxScroll) targetScrollOffset = -maxScroll;
            if (targetScrollOffset > 0) targetScrollOffset = 0;

            int y = startY + scrollOffset;

            foreach (var achievement in userAchievements)
            {
                if (y + achievementHeight < 100 || y > this.ClientSize.Height - 100)
                {
                    y += achievementHeight + gap;
                    continue;
                }

                Rectangle achievementRect = new Rectangle(startX, y, availableWidth, achievementHeight);
                DrawAchievementItem(g, achievement, achievementRect);
                y += achievementHeight + gap;
            }
        }

        private void DrawAchievementItem(Graphics g, Achievement achievement, Rectangle rect)
        {
            // Calculate progress for locked achievements
            double progress = CalculateAchievementProgress(achievement);
            bool isCloseToUnlock = !achievement.IsUnlocked && progress >= 0.7; // 70%+ progress

            // Enhanced card styling
            Color topColor, bottomColor, borderColor, glowColor;
            if (achievement.IsUnlocked && !achievement.IsClaimed)
            {
                topColor = Color.FromArgb(255, 140, 220, 140);
                bottomColor = Color.FromArgb(255, 80, 180, 80);
                borderColor = Color.FromArgb(255, 50, 150, 50);
                glowColor = Color.FromArgb(255, 100, 255, 100);
            }
            else if (achievement.IsUnlocked)
            {
                topColor = Color.FromArgb(255, 220, 180, 120);
                bottomColor = Color.FromArgb(255, 180, 140, 70);
                borderColor = Color.FromArgb(255, 150, 100, 40);
                glowColor = Color.FromArgb(255, 255, 200, 100);
            }
            else if (isCloseToUnlock)
            {
                // Close to unlock - highlight with gold/orange
                topColor = Color.FromArgb(255, 180, 140, 100);
                bottomColor = Color.FromArgb(255, 140, 100, 60);
                borderColor = Color.FromArgb(255, 200, 150, 50);
                glowColor = Color.FromArgb(255, 255, 180, 80);
            }
            else
            {
                topColor = Color.FromArgb(255, 100, 85, 125);
                bottomColor = Color.FromArgb(255, 70, 60, 100);
                borderColor = Color.FromArgb(255, 55, 50, 80);
                glowColor = Color.FromArgb(255, 120, 120, 120);
            }

            // Draw enhanced shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
            {
                g.FillRectangle(shadow, new Rectangle(rect.X + 6, rect.Y + 6, rect.Width, rect.Height));
            }

            // Draw main card with gradient
            using (LinearGradientBrush cardBrush = new LinearGradientBrush(
                rect, topColor, bottomColor, LinearGradientMode.Vertical))
            {
                g.FillRectangle(cardBrush, rect);
            }

            // Draw border
            using (Pen borderPen = new Pen(borderColor, 3))
            {
                g.DrawRectangle(borderPen, rect);
            }

            // Inner highlight
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            {
                g.FillRectangle(highlight, rect.X + 3, rect.Y + 3, rect.Width - 6, 8);
            }

            // Glow effect for unlocked achievements
            if (achievement.IsUnlocked)
            {
                int glowAlpha = 60 + (int)(30 * Math.Sin(pulsePhase * Math.PI / 25));
                glowAlpha = Math.Max(0, Math.Min(255, glowAlpha));
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, glowColor)))
                {
                    g.FillRectangle(glowBrush, rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
                }
            }

            // Trophy icon for achievement
            int iconSize = 60;
            int iconX = rect.X + 18;
            int iconY = rect.Y + (rect.Height - iconSize) / 2;

            string trophyIcon = achievement.IsUnlocked ? "🏆" : "🔒";
            using (Font trophyFont = new Font("Segoe UI Emoji", 36))
            {
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(trophyIcon, trophyFont, Brushes.White, new RectangleF(iconX, iconY, iconSize, iconSize), sf);
                }
            }

            // Achievement name
            using (Font nameFont = new Font("Comic Sans MS", 17, FontStyle.Bold))
            {
                Color nameColor = achievement.IsUnlocked ? Color.White : Color.FromArgb(200, 200, 200);
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    g.DrawString(achievement.Name, nameFont, Brushes.Black,
                        new RectangleF(iconX + iconSize + 20, rect.Y + 12, rect.Width - iconSize - 180, 28), sf);
                    g.DrawString(achievement.Name, nameFont, new SolidBrush(nameColor),
                        new RectangleF(iconX + iconSize + 18, rect.Y + 10, rect.Width - iconSize - 180, 28), sf);
                }
            }

            // Achievement description
            using (Font descFont = new Font("Comic Sans MS", 12))
            {
                Color descColor = achievement.IsUnlocked ? Color.FromArgb(245, 245, 245) : Color.FromArgb(170, 170, 170);
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    g.DrawString(achievement.Description, descFont, new SolidBrush(descColor),
                        new RectangleF(iconX + iconSize + 20, rect.Y + 42, rect.Width - iconSize - 180, 35), sf);
                }
            }

            // Gold reward display
            if (achievement.GoldReward > 0)
            {
                using (Font goldFont = new Font("Comic Sans MS", 13, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                    {
                        string goldText = achievement.IsUnlocked ? $"+{achievement.GoldReward} Gold" : $"{achievement.GoldReward} Gold";
                        Color goldColor = achievement.IsUnlocked ? Color.FromArgb(255, 215, 0) : Color.FromArgb(180, 180, 180);
                        int goldY = isCloseToUnlock ? rect.Y + 55 : rect.Y + 72;
                        g.DrawString(goldText, goldFont, new SolidBrush(goldColor),
                            new RectangleF(iconX + iconSize + 20, goldY, rect.Width - iconSize - 50, 20), sf);
                    }
                }
            }

            // Progress indicator for locked achievements close to unlock
            if (!achievement.IsUnlocked && isCloseToUnlock)
            {
                int progressWidth = 120;
                int progressHeight = 8;
                int progressX = iconX + iconSize + 20;
                int progressY = rect.Y + 65;

                // Progress bar background
                using (SolidBrush progressBg = new SolidBrush(Color.FromArgb(150, 60, 60, 80)))
                {
                    g.FillRectangle(progressBg, progressX, progressY, progressWidth, progressHeight);
                }

                // Progress bar fill
                int fillWidth = (int)(progressWidth * progress);
                using (SolidBrush progressFill = new SolidBrush(Color.FromArgb(255, 255, 180, 60)))
                {
                    g.FillRectangle(progressFill, progressX, progressY, fillWidth, progressHeight);
                }

                // Progress percentage text - positioned to the right of progress bar
                using (Font progressFont = new Font("Comic Sans MS", 10, FontStyle.Bold))
                {
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                    {
                        g.DrawString($"{(int)(progress * 100)}%", progressFont, new SolidBrush(Color.FromArgb(255, 255, 200, 100)),
                            new RectangleF(progressX + progressWidth + 8, progressY - 2, 40, 15), sf);
                    }
                }
            }

            // Claim button or status - removed to prevent cutoff
            // Achievements can still be claimed by clicking the card
        }

        private double CalculateAchievementProgress(Achievement achievement)
        {
            // Calculate progress based on achievement type and current user stats
            switch (achievement.Type)
            {
                case AchievementType.FirstMatch:
                    return currentUser.TotalMatches > 0 ? 1.0 : 0.0;

                case AchievementType.Level1Complete:
                    return currentUser.CompletedLevels?.Contains(1) == true ? 1.0 : 0.0;

                case AchievementType.Level5Complete:
                    return currentUser.CompletedLevels?.Contains(5) == true ? 1.0 : 0.0;

                case AchievementType.Level10Complete:
                    return currentUser.CompletedLevels?.Contains(10) == true ? 1.0 : 0.0;

                case AchievementType.Score1000:
                    return Math.Min(1.0, currentUser.HighestScore / 1000.0);

                case AchievementType.Score5000:
                    return Math.Min(1.0, currentUser.HighestScore / 5000.0);

                case AchievementType.Score10000:
                    return Math.Min(1.0, currentUser.HighestScore / 10000.0);

                case AchievementType.Gold100:
                    return Math.Min(1.0, currentUser.Gold / 100.0);

                case AchievementType.Gold500:
                    return Math.Min(1.0, currentUser.Gold / 500.0);

                case AchievementType.Gold1000:
                    return Math.Min(1.0, currentUser.Gold / 1000.0);

                case AchievementType.TotalMatches100:
                    return Math.Min(1.0, currentUser.TotalMatches / 100.0);

                case AchievementType.TotalMatches500:
                    return Math.Min(1.0, currentUser.TotalMatches / 500.0);

                case AchievementType.TotalMatches1000:
                    return Math.Min(1.0, currentUser.TotalMatches / 1000.0);

                // Combo and special achievements - estimated progress
                case AchievementType.Combo3:
                case AchievementType.Combo5:
                case AchievementType.SquareMatch:
                    // These are harder to track without detailed session data
                    // Return 0 for now since we can't accurately calculate progress
                    return 0.0;

                default:
                    return 0.0;
            }
        }

        private void DrawCoinParticles(Graphics g)
        {
            foreach (var coin in coinParticles)
            {
                int clampedAlpha = Math.Max(0, Math.Min(255, coin.Alpha));
                using (SolidBrush coinBrush = new SolidBrush(Color.FromArgb(clampedAlpha, 255, 215, 0)))
                {
                    g.FillEllipse(coinBrush, (int)coin.X, (int)coin.Y, coin.Size, coin.Size);
                }

                using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(clampedAlpha, 255, 255, 200)))
                {
                    g.FillEllipse(highlightBrush, (int)coin.X + coin.Size / 4, (int)coin.Y + coin.Size / 4, coin.Size / 2, coin.Size / 2);
                }
            }
        }

        private void DrawSocialPlaceholder(Graphics g)
        {
            // This page is now handled by the dedicated SocialFrame
            // Just show a simple placeholder
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;

            using (Font font = new Font("Arial", 16))
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Loading Social...", font, brush, centerX, centerY, sf);
            }
        }


        private async void MainFrame_FormClosed(object? sender, FormClosedEventArgs e)
        {
            animationTimer?.Stop();
            SoundManager.StopBackgroundMusic();
            
            // Sync progress with server on app close
            try
            {
                await ProgressSyncService.SyncOnCloseAsync(currentUser, database, apiClient);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Sync on close failed", ex);
            }
        }
    }
}