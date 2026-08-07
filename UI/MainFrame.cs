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
        Guilds
    }

    public class MainFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private readonly IApiClient? apiClient;

        private List<StyleParticle> backgroundParticles = new List<StyleParticle>();
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


        private Label usernameLabel = null!;
        private Label pencilIconLabel = null!;
        private TextBox usernameEditBox = null!;
        private bool isEditingUsername = false;
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
        private List<CoinParticle> coinParticles = new List<CoinParticle>();
        private bool isCoinAnimating = false;

        // Guild state
        private readonly GuildRepository guildRepository;
        private CrushIt.UI.GuildViewMode guildViewMode = CrushIt.UI.GuildViewMode.Browse;
        private Guild? selectedGuild;
        private List<Guild> displayedGuilds = new List<Guild>();

        // Guild UI State
        private string guildNameInput = "";
        private string guildDescriptionInput = "";
        private string searchQuery = "";
        private string guildStatusMessage = "";
        private Color guildStatusColor = Color.White;
        private bool isGuildProcessing = false;

        // Guild input rectangles
        private Rectangle searchRect;
        private Rectangle backButtonRect;
        private Rectangle nameInputRect;
        private Rectangle descriptionInputRect;
        private Rectangle createGuildButtonRect;
        private Rectangle joinButtonRect;
        private Rectangle leaveButtonRect;

        // Guild hover states
        private bool isSearchFocused = false;
        private bool isNameFocused = false;
        private bool isDescriptionFocused = false;
        private int hoveredGuildIndex = -1;
        private bool isBackButtonHovered = false;
        private bool isJoinButtonHovered = false;
        private bool isLeaveButtonHovered = false;
        private bool isCreateGuildButtonHovered = false;

        public MainFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;
            this.guildRepository = new GuildRepository(db);

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
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
            this.KeyPress += MainFrame_KeyPress;
            this.MouseDown += MainFrame_MouseDown;
            this.FormClosed += MainFrame_FormClosed;
            this.FormClosed += (s, e) => {
                if (Application.OpenForms.Count == 0)
                {
                    Application.Exit();
                }
            };
            this.MouseMove += MainFrame_MouseMove;
            this.MouseLeave += MainFrame_MouseLeave;


            InitializeHomeControls();
            InitializeGuildRectangles();
        }

        private void InitializeHomeControls()
        {
            usernameLabel = new Label
            {
                Font = new Font("Comic Sans MS", 20, FontStyle.Bold),
                Size = new Size(320, 40),
                Location = new Point(210, 108),
                Text = currentUser.Username,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            pencilIconLabel = new Label
            {
                Font = new Font("Segoe UI Emoji", 16, FontStyle.Bold),
                Size = new Size(40, 40),
                Location = new Point(530, 108),
                Text = "✏️",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            pencilIconLabel.Click += PencilIconLabel_Click;

            usernameEditBox = new TextBox
            {
                Font = new Font("Comic Sans MS", 20, FontStyle.Bold),
                Size = new Size(320, 40),
                Location = new Point(210, 108),
                Text = currentUser.Username,
                BackColor = Color.FromArgb(255, 173, 216, 230),
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            usernameEditBox.TextChanged += UsernameEditBox_TextChanged;
            usernameEditBox.KeyDown += UsernameEditBox_KeyDown;
            usernameEditBox.LostFocus += UsernameEditBox_LostFocus;

            this.Controls.Add(usernameLabel);
            this.Controls.Add(pencilIconLabel);
            this.Controls.Add(usernameEditBox);
        }

        private void InitializeParticles()
        {
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 45, 890, 80, 480));
        }

        private void InitializeGuildRectangles()
        {
            searchRect = new Rectangle(80, 95, 500, 40);
            backButtonRect = new Rectangle(30, 30, 100, 35);
            nameInputRect = new Rectangle(100, 130, 700, 40);
            descriptionInputRect = new Rectangle(100, 190, 700, 80);
            createGuildButtonRect = new Rectangle(350, 300, 200, 50);
            joinButtonRect = new Rectangle(380, 450, 140, 45);
            leaveButtonRect = new Rectangle(750, 30, 40, 40);
        }

        private void LoadAllPageData()
        {
            LoadLevelsData();
            LoadHomeData();
            LoadAchievementsData();
            LoadGuildsData();
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
                    int y = 130 + (row - 1) * 80 + (col % 2 == 0 ? 0 : 20);

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

            usernameLabel.Text = "@" + currentUser.Username;
            usernameEditBox.Text = currentUser.Username;

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

        private async void LoadGuildsData()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentUser.GuildId))
                {
                    guildViewMode = CrushIt.UI.GuildViewMode.MyGuild;
                    selectedGuild = await guildRepository.GetGuildByIdAsync(currentUser.GuildId);
                }
                else
                {
                    guildViewMode = CrushIt.UI.GuildViewMode.Browse;
                    displayedGuilds = await guildRepository.GetSearchableGuildsAsync(currentUser);
                    guildStatusMessage = $"Loaded {displayedGuilds.Count} guilds";
                    guildStatusColor = Color.FromArgb(120, 255, 120);
                }
            }
            catch (Exception ex)
            {
                guildStatusMessage = "Error loading guilds: " + ex.Message;
                guildStatusColor = Color.FromArgb(255, 120, 120);
            }
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

            userAchievements = userAchievements
                .OrderByDescending(a => a.IsUnlocked && !a.IsClaimed)
                .ThenByDescending(a => a.IsUnlocked && a.IsClaimed)
                .ThenBy(a => a.Type)
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


            if (isCoinAnimating)
            {
                UpdateCoinParticles();
            }

            this.Invalidate();
        }

        private void MainFrame_MouseDown(object? sender, MouseEventArgs e)
        {
            if (isTransitioning) return;

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
            else if (currentPage == PageType.Guilds)
            {
                HandleGuildsClick(e);
            }
            else if (currentPage == PageType.Home)
            {
                HandleHomeClick(e);
            }
        }

        private void MainFrame_MouseMove(object? sender, MouseEventArgs e)
        {
            if (currentPage == PageType.Guilds)
            {
                HandleGuildsMouseMove(e);
            }
        }

        private void MainFrame_MouseLeave(object? sender, EventArgs e)
        {
            if (currentPage == PageType.Guilds)
            {
                HandleGuildsMouseLeave();
            }
        }

        private void HandleHomeClick(MouseEventArgs e)
        {
            // Home page clicks handled by controls
        }

        private void HandleGuildsMouseMove(MouseEventArgs e)
        {
            bool wasBackButtonHovered = isBackButtonHovered;
            bool wasJoinButtonHovered = isJoinButtonHovered;
            bool wasLeaveButtonHovered = isLeaveButtonHovered;
            bool wasCreateGuildButtonHovered = isCreateGuildButtonHovered;
            int oldHoveredIndex = hoveredGuildIndex;

            // Check hover states for all buttons
            isBackButtonHovered = backButtonRect.Contains(e.Location);
            isCreateGuildButtonHovered = createGuildButtonRect.Contains(e.Location);
            isJoinButtonHovered = joinButtonRect.Contains(e.Location);
            isLeaveButtonHovered = leaveButtonRect.Contains(e.Location);

            // Check create guild button in browse mode
            if (guildViewMode == CrushIt.UI.GuildViewMode.Browse)
            {
                Rectangle createGuildButton = new Rectangle(325, 450, 250, 40);
                if (createGuildButton.Contains(e.Location))
                {
                    isCreateGuildButtonHovered = true;
                }
            }

            // Filter hover states based on current mode
            if (guildViewMode == CrushIt.UI.GuildViewMode.Browse)
            {
                isLeaveButtonHovered = false;
                isJoinButtonHovered = false;
            }
            else if (guildViewMode == CrushIt.UI.GuildViewMode.MyGuild)
            {
                isJoinButtonHovered = false;
            }
            else if (guildViewMode == CrushIt.UI.GuildViewMode.GuildDetails)
            {
                bool isMember = !string.IsNullOrEmpty(currentUser.GuildId) && currentUser.GuildId == selectedGuild?.Id;
                if (!isMember)
                {
                    isLeaveButtonHovered = false;
                }
                else
                {
                    isJoinButtonHovered = false;
                }
            }
            else if (guildViewMode == CrushIt.UI.GuildViewMode.CreateGuild)
            {
                isLeaveButtonHovered = false;
                isJoinButtonHovered = false;
            }

            // Update cursor
            this.Cursor = (isBackButtonHovered || isJoinButtonHovered ||
                          isLeaveButtonHovered ||
                          isCreateGuildButtonHovered) ? Cursors.Hand : Cursors.Default;

            // Check guild list hover
            hoveredGuildIndex = -1;
            if (guildViewMode == CrushIt.UI.GuildViewMode.Browse && displayedGuilds.Count > 0)
            {
                int startY = 170;
                for (int i = 0; i < displayedGuilds.Count; i++)
                {
                    int y = startY + i * 70;
                    if (y >= 160 && y <= 470)
                    {
                        Rectangle guildRect = new Rectangle(50, y, 800, 55);
                        if (guildRect.Contains(e.Location))
                        {
                            hoveredGuildIndex = i;
                            break;
                        }
                    }
                }
            }

            if (wasBackButtonHovered != isBackButtonHovered ||
                wasJoinButtonHovered != isJoinButtonHovered ||
                wasLeaveButtonHovered != isLeaveButtonHovered ||
                wasCreateGuildButtonHovered != isCreateGuildButtonHovered ||
                oldHoveredIndex != hoveredGuildIndex)
            {
                this.Invalidate();
            }
        }

        private void HandleGuildsMouseLeave()
        {
            hoveredGuildIndex = -1;
            isBackButtonHovered = false;
            isJoinButtonHovered = false;
            isLeaveButtonHovered = false;
            isCreateGuildButtonHovered = false;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }

        private void MainFrame_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (currentPage == PageType.Guilds)
            {
                HandleGuildsKeyPress(e);
            }
        }

        private void HandleGuildsKeyPress(KeyPressEventArgs e)
        {
            if (isGuildProcessing) return;
            if (e.KeyChar == (char)Keys.Back)
            {
                if (isSearchFocused && searchQuery.Length > 0)
                    searchQuery = searchQuery.Substring(0, searchQuery.Length - 1);
                else if (isNameFocused && guildNameInput.Length > 0)
                    guildNameInput = guildNameInput.Substring(0, guildNameInput.Length - 1);
                else if (isDescriptionFocused && guildDescriptionInput.Length > 0)
                    guildDescriptionInput = guildDescriptionInput.Substring(0, guildDescriptionInput.Length - 1);
                this.Invalidate();
                return;
            }

            if (e.KeyChar == (char)Keys.Enter)
            {
                if (isSearchFocused)
                {
                    SearchGuildsAsync();
                }
                else if (isNameFocused && guildViewMode == CrushIt.UI.GuildViewMode.CreateGuild)
                {
                    isNameFocused = false;
                    isDescriptionFocused = true;
                    this.Invalidate();
                }
                else if (isDescriptionFocused && guildViewMode == CrushIt.UI.GuildViewMode.CreateGuild)
                {
                    CreateGuildAsync();
                }
                return;
            }

            if (e.KeyChar >= 32 && e.KeyChar <= 126)
            {
                if (isSearchFocused && searchQuery.Length < 30)
                {
                    searchQuery += e.KeyChar;
                    this.Invalidate();
                }
                else if (isNameFocused && guildNameInput.Length < 30)
                {
                    guildNameInput += e.KeyChar;
                    this.Invalidate();
                }
                else if (isDescriptionFocused && guildDescriptionInput.Length < 150)
                {
                    guildDescriptionInput += e.KeyChar;
                    this.Invalidate();
                }
            }
        }

        private async void SearchGuildsAsync()
        {
            if (isGuildProcessing) return;
            isGuildProcessing = true;

            try
            {
                displayedGuilds = await guildRepository.GetSearchableGuildsAsync(currentUser);
                guildStatusMessage = $"Found {displayedGuilds.Count} guilds";
                guildStatusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                guildStatusMessage = "Error searching guilds: " + ex.Message;
                guildStatusColor = Color.FromArgb(255, 120, 120);
            }
            finally
            {
                isGuildProcessing = false;
                this.Invalidate();
            }
        }

        private async void CreateGuildAsync()
        {
            if (isGuildProcessing) return;
            if (string.IsNullOrWhiteSpace(guildNameInput))
            {
                guildStatusMessage = "Guild name is required";
                guildStatusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }

            isGuildProcessing = true;

            try
            {
                var newGuild = await guildRepository.CreateGuildAsync(guildNameInput, guildDescriptionInput, currentUser);

                guildViewMode = CrushIt.UI.GuildViewMode.MyGuild;
                selectedGuild = newGuild;
                guildStatusMessage = "Guild created successfully!";
                guildStatusColor = Color.FromArgb(120, 255, 120);

                guildNameInput = "";
                guildDescriptionInput = "";
            }
            catch (Exception ex)
            {
                guildStatusMessage = "Error creating guild: " + ex.Message;
                guildStatusColor = Color.FromArgb(255, 120, 120);
            }
            finally
            {
                isGuildProcessing = false;
                this.Invalidate();
            }
        }

        private async void JoinGuildAsync()
        {
            if (isGuildProcessing || selectedGuild == null) return;
            isGuildProcessing = true;

            try
            {
                await guildRepository.JoinGuildAsync(selectedGuild.Id ?? "", currentUser);

                guildViewMode = CrushIt.UI.GuildViewMode.MyGuild;
                selectedGuild = await guildRepository.GetGuildByIdAsync(selectedGuild.Id ?? "");
                guildStatusMessage = "Joined guild successfully!";
                guildStatusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                guildStatusMessage = "Error joining guild: " + ex.Message;
                guildStatusColor = Color.FromArgb(255, 120, 120);
            }
            finally
            {
                isGuildProcessing = false;
                this.Invalidate();
            }
        }

        private async void LeaveGuildAsync()
        {
            if (isGuildProcessing) return;
            isGuildProcessing = true;

            try
            {
                await guildRepository.LeaveGuildAsync(currentUser);

                guildViewMode = CrushIt.UI.GuildViewMode.Browse;
                selectedGuild = null;
                displayedGuilds = await guildRepository.GetSearchableGuildsAsync(currentUser);
                guildStatusMessage = "Left guild successfully";
                guildStatusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                guildStatusMessage = "Error leaving guild: " + ex.Message;
                guildStatusColor = Color.FromArgb(255, 120, 120);
            }
            finally
            {
                isGuildProcessing = false;
                this.Invalidate();
            }
        }

        private void HandleNavigation(NavItem nav)
        {
            PageType newPage = nav switch
            {
                NavItem.Home => PageType.Home,
                NavItem.Levels => PageType.Levels,
                NavItem.Achievements => PageType.Achievements,
                NavItem.Guilds => PageType.Guilds,
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
            int achievementHeight = 70;
            int gap = 15;
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

                if (achievement.IsUnlocked && !achievement.IsClaimed)
                {
                    int claimButtonWidth = 100;
                    int claimButtonHeight = 35;
                    int claimButtonX = achievementRect.Right - claimButtonWidth - 15;
                    int claimButtonY = achievementRect.Y + (achievementHeight - claimButtonHeight) / 2;

                    Rectangle claimButtonRect = new Rectangle(claimButtonX, claimButtonY, claimButtonWidth, claimButtonHeight);

                    if (claimButtonRect.Contains(e.X, e.Y))
                    {
                        ClaimAchievement(achievement);
                        break;
                    }
                }

                y += achievementHeight + gap;
            }
        }

        private void HandleGuildsClick(MouseEventArgs e)
        {
            if (isGuildProcessing) return;

            if (backButtonRect.Contains(e.Location))
            {
                if (guildViewMode == CrushIt.UI.GuildViewMode.GuildDetails || guildViewMode == CrushIt.UI.GuildViewMode.CreateGuild)
                {
                    guildViewMode = CrushIt.UI.GuildViewMode.Browse;
                    selectedGuild = null;
                    this.Invalidate();
                }
                return;
            }

            if (guildViewMode == CrushIt.UI.GuildViewMode.Browse)
            {
                if (searchRect.Contains(e.Location))
                {
                    isSearchFocused = true;
                    isNameFocused = false;
                    isDescriptionFocused = false;
                }
                else
                {
                    isSearchFocused = false;
                }

                // Check guild list clicks
                if (hoveredGuildIndex >= 0 && hoveredGuildIndex < displayedGuilds.Count && displayedGuilds.Count > 0)
                {
                    selectedGuild = displayedGuilds[hoveredGuildIndex];
                    guildViewMode = CrushIt.UI.GuildViewMode.GuildDetails;
                    this.Invalidate();
                    return;
                }

                // Check create guild button
                Rectangle createGuildButton = new Rectangle(325, 450, 250, 40);
                if (createGuildButton.Contains(e.Location))
                {
                    guildViewMode = CrushIt.UI.GuildViewMode.CreateGuild;
                    guildNameInput = "";
                    guildDescriptionInput = "";
                    this.Invalidate();
                    return;
                }
            }
            else if (guildViewMode == CrushIt.UI.GuildViewMode.CreateGuild)
            {
                if (nameInputRect.Contains(e.Location))
                {
                    isNameFocused = true;
                    isDescriptionFocused = false;
                }
                else if (descriptionInputRect.Contains(e.Location))
                {
                    isNameFocused = false;
                    isDescriptionFocused = true;
                }
                else if (createGuildButtonRect.Contains(e.Location))
                {
                    CreateGuildAsync();
                    return;
                }
                else
                {
                    isNameFocused = false;
                    isDescriptionFocused = false;
                }
            }
            else if (guildViewMode == CrushIt.UI.GuildViewMode.GuildDetails)
            {
                if (joinButtonRect.Contains(e.Location) && selectedGuild != null)
                {
                    JoinGuildAsync();
                    return;
                }
                else if (leaveButtonRect.Contains(e.Location))
                {
                    LeaveGuildAsync();
                    return;
                }
            }
            else if (guildViewMode == CrushIt.UI.GuildViewMode.MyGuild)
            {
                if (leaveButtonRect.Contains(e.Location))
                {
                    LeaveGuildAsync();
                    return;
                }
            }

            this.Invalidate();
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
                System.Diagnostics.Debug.WriteLine($"Failed to claim achievement: {ex.Message}");
            }

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


        private void PencilIconLabel_Click(object? sender, EventArgs e)
        {
            isEditingUsername = true;
            usernameLabel.Visible = false;
            pencilIconLabel.Visible = false;
            usernameEditBox.Visible = true;
            usernameEditBox.Focus();
            usernameEditBox.SelectAll();
        }

        private async void UsernameEditBox_TextChanged(object? sender, EventArgs e)
        {
            if (isEditingUsername)
            {
                string newUsername = usernameEditBox.Text.Trim();
                if (!string.IsNullOrEmpty(newUsername) && newUsername != currentUser.Username)
                {
                    currentUser.Username = newUsername;

                    var usersCollection = database.GetCollection<UserAccount>("users");
                    var filter = Builders<UserAccount>.Filter.Eq(u => u.Id, currentUser.Id);
                    var update = Builders<UserAccount>.Update.Set(u => u.Username, newUsername);
                    await usersCollection.UpdateOneAsync(filter, update);
                }
            }
        }

        private void UsernameEditBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                FinishEditing();
            else if (e.KeyCode == Keys.Escape)
            {
                usernameEditBox.Text = currentUser.Username;
                FinishEditing();
            }
        }

        private void UsernameEditBox_LostFocus(object? sender, EventArgs e)
        {
            if (isEditingUsername)
                FinishEditing();
        }

        private void FinishEditing()
        {
            isEditingUsername = false;
            usernameEditBox.Visible = false;
            usernameLabel.Visible = currentPage == PageType.Home;
            pencilIconLabel.Visible = currentPage == PageType.Home;
            usernameLabel.Text = "@" + currentUser.Username;
        }

        private void UpdateControlVisibility()
        {

            bool showHomeControls = currentPage == PageType.Home && !isTransitioning;

            usernameLabel.Visible = showHomeControls && !isEditingUsername;
            pencilIconLabel.Visible = showHomeControls && !isEditingUsername;
            usernameEditBox.Visible = isEditingUsername;
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
                currentPage == PageType.Achievements ? NavItem.Achievements : NavItem.Guilds,
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
                case PageType.Guilds:
                    DrawGuildsPage(g);
                    break;
            }
        }

        private void DrawLevelsPage(Graphics g)
        {
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(150, 15, 600, 55), "HOME", 24);
            DrawLevelPath(g);

            foreach (var level in levels)
                DrawLevelNode(g, level);
        }

        private void DrawLevelPath(Graphics g)
        {
            int pathStart = 40;
            int pathEnd = 860;
            int pathHeight = 60;

            for (int row = 1; row <= maxRows; row++)
            {
                int pathY = 160 + (row - 1) * 80;
                Rectangle pathRect = new Rectangle(pathStart, pathY, pathEnd - pathStart, pathHeight);
                CrushItStyleHelper.DrawPanel(g, pathRect,
                    Color.FromArgb(255, 140, 100, 185),
                    Color.FromArgb(255, 100, 65, 150),
                    Color.FromArgb(255, 80, 50, 120));
            }
        }

        private void DrawLevelNode(Graphics g, LevelNode level)
        {
            int size = 56;
            int x = level.X - size / 2;
            int y = level.Y - size / 2;
            Rectangle nodeRect = new Rectangle(x, y, size, size);

            Color topColor, bottomColor, borderColor;
            if (level.Completed)
            {
                topColor = Color.FromArgb(255, 120, 230, 120);
                bottomColor = Color.FromArgb(255, 70, 170, 70);
                borderColor = Color.FromArgb(255, 40, 130, 40);
            }
            else if (level.Unlocked)
            {
                topColor = Color.FromArgb(255, 200, 170, 240);
                bottomColor = Color.FromArgb(255, 140, 100, 200);
                borderColor = Color.FromArgb(255, 100, 70, 160);
            }
            else
            {
                topColor = Color.FromArgb(255, 90, 75, 110);
                bottomColor = Color.FromArgb(255, 60, 50, 85);
                borderColor = Color.FromArgb(255, 45, 40, 65);
            }

            CrushItStyleHelper.DrawPanel(g, nodeRect, topColor, bottomColor, borderColor);

            if (level.Unlocked && !level.Completed)
            {
                int glowPulse = (int)(20 * Math.Sin(pulsePhase * Math.PI / 60));
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(40 + glowPulse, 255, 220, 120)))
                    g.FillEllipse(glow, x - 4, y - 4, size + 8, size + 8);
            }

            using (Font numFont = new Font("Comic Sans MS", 22, FontStyle.Bold))
            {
                Color textColor = level.Unlocked ? Color.White : Color.FromArgb(180, 160, 160, 175);
                CrushItStyleHelper.DrawOutlinedText(g, level.Number.ToString(), numFont, nodeRect, textColor, Color.Black, 1);
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
            Rectangle section = new Rectangle(50, 440, 800, 55);
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
            int achievementHeight = 70;
            int gap = 15;
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
                DrawAchievementItem(g, achievement, achievementRect);
                y += achievementHeight + gap;
            }
        }

        private void DrawAchievementItem(Graphics g, Achievement achievement, Rectangle rect)
        {
            Color topColor, bottomColor, borderColor;
            if (achievement.IsClaimed)
            {
                topColor = Color.FromArgb(255, 120, 200, 120);
                bottomColor = Color.FromArgb(255, 80, 160, 80);
                borderColor = Color.FromArgb(255, 50, 120, 50);
            }
            else if (achievement.IsUnlocked)
            {
                topColor = Color.FromArgb(255, 200, 180, 100);
                bottomColor = Color.FromArgb(255, 160, 140, 60);
                borderColor = Color.FromArgb(255, 120, 100, 40);
            }
            else
            {
                topColor = Color.FromArgb(255, 80, 70, 100);
                bottomColor = Color.FromArgb(255, 50, 40, 70);
                borderColor = Color.FromArgb(255, 40, 30, 60);
            }

            CrushItStyleHelper.DrawPanel(g, rect, topColor, bottomColor, borderColor);


            Rectangle iconRect = new Rectangle(rect.X + 15, rect.Y + 10, 50, 50);
            Color iconColor = ColorTranslator.FromHtml(achievement.IconColor);
            using (SolidBrush iconBrush = new SolidBrush(iconColor))
                g.FillEllipse(iconBrush, iconRect);


            using (Font nameFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (Font descFont = new Font("Comic Sans MS", 10))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                Color textColor = achievement.IsUnlocked ? Color.White : Color.FromArgb(180, 160, 160, 175);
                CrushItStyleHelper.DrawOutlinedText(g, achievement.Name, nameFont,
                    new Rectangle(rect.X + 80, rect.Y + 15, 400, 20),
                    textColor, Color.Black, 1, sf);

                CrushItStyleHelper.DrawOutlinedText(g, achievement.Description, descFont,
                    new Rectangle(rect.X + 80, rect.Y + 40, 400, 15),
                    Color.FromArgb(255, 200, 200, 220), Color.Black, 1, sf);
            }


            if (achievement.IsUnlocked && !achievement.IsClaimed)
            {
                int claimButtonWidth = 100;
                int claimButtonHeight = 35;
                int claimButtonX = rect.Right - claimButtonWidth - 15;
                int claimButtonY = rect.Y + (rect.Height - claimButtonHeight) / 2;

                Rectangle claimButtonRect = new Rectangle(claimButtonX, claimButtonY, claimButtonWidth, claimButtonHeight);
                CrushItStyleHelper.DrawPanel(g, claimButtonRect,
                    Color.FromArgb(255, 100, 200, 100),
                    Color.FromArgb(255, 60, 160, 60),
                    Color.FromArgb(255, 40, 120, 40));

                using (Font claimFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    CrushItStyleHelper.DrawOutlinedText(g, $"CLAIM {achievement.GoldReward}", claimFont, claimButtonRect, Color.White, Color.Black, 1, sf);
                }
            }
            else if (achievement.IsClaimed)
            {
                int claimedX = rect.Right - 80;
                int claimedY = rect.Y + (rect.Height - 30) / 2;
                Rectangle claimedRect = new Rectangle(claimedX, claimedY, 70, 30);

                using (Font claimedFont = new Font("Comic Sans MS", 10, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    CrushItStyleHelper.DrawOutlinedText(g, "CLAIMED", claimedFont, claimedRect, Color.FromArgb(255, 150, 255, 150), Color.Black, 1, sf);
                }
            }
        }

        private void DrawCoinParticles(Graphics g)
        {
            foreach (var coin in coinParticles)
            {
                using (SolidBrush coinBrush = new SolidBrush(Color.FromArgb(coin.Alpha, 255, 215, 0)))
                {
                    g.FillEllipse(coinBrush, (int)coin.X, (int)coin.Y, coin.Size, coin.Size);
                }

                using (SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(coin.Alpha, 255, 255, 200)))
                {
                    g.FillEllipse(highlightBrush, (int)coin.X + coin.Size / 4, (int)coin.Y + coin.Size / 4, coin.Size / 2, coin.Size / 2);
                }
            }
        }

        private void DrawGuildsPage(Graphics g)
        {
            // Draw title banner
            string title = guildViewMode switch
            {
                CrushIt.UI.GuildViewMode.Browse => "GUILD HALL",
                CrushIt.UI.GuildViewMode.MyGuild => "MY GUILD",
                CrushIt.UI.GuildViewMode.CreateGuild => "CREATE GUILD",
                CrushIt.UI.GuildViewMode.GuildDetails => "GUILD DETAILS",
                _ => "GUILDS"
            };
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(250, 15, 400, 55), title);

            // Draw content based on current mode
            switch (guildViewMode)
            {
                case CrushIt.UI.GuildViewMode.Browse:
                    DrawGuildBrowseMode(g);
                    break;
                case CrushIt.UI.GuildViewMode.MyGuild:
                    DrawGuildMyGuildMode(g);
                    break;
                case CrushIt.UI.GuildViewMode.CreateGuild:
                    DrawGuildCreateMode(g);
                    break;
                case CrushIt.UI.GuildViewMode.GuildDetails:
                    DrawGuildDetailsMode(g);
                    break;
            }

            // Draw status message (hidden for cleaner UI - can be enabled for debugging)
            // if (!string.IsNullOrEmpty(guildStatusMessage))
            // {
            //     using (Font statusFont = new Font("Segoe UI", 12, FontStyle.Bold))
            //     using (Brush statusBrush = new SolidBrush(guildStatusColor))
            //     {
            //         SizeF statusSize = g.MeasureString(guildStatusMessage, statusFont);
            //         g.DrawString(guildStatusMessage, statusFont, statusBrush,
            //             (this.ClientSize.Width - statusSize.Width) / 2, 470);
            //     }
            // }
        }

        private void DrawGuildBrowseMode(Graphics g)
        {
            // Draw search panel
            Rectangle searchPanel = new Rectangle(50, 85, 800, 60);
            CrushItStyleHelper.DrawPanel(g, searchPanel,
                Color.FromArgb(255, 160, 120, 200),
                Color.FromArgb(255, 120, 80, 160),
                Color.FromArgb(255, 100, 60, 140));

            // Draw search bar
            DrawInputBox(g, searchRect, searchQuery.Length == 0 ? "🔍 Search guilds..." : searchQuery,
                isSearchFocused, isSearchFocused);

            // Draw decorative separator
            using (Pen separator = new Pen(Color.FromArgb(255, 255, 200, 120), 2))
            {
                g.DrawLine(separator, 50, 155, 850, 155);
            }

            // Draw guild list
            if (displayedGuilds.Count > 0)
            {
                int startY = 170;
                for (int i = 0; i < displayedGuilds.Count; i++)
                {
                    int y = startY + i * 70;
                    if (y >= 160 && y <= 470)
                    {
                        DrawGuildCard(g, displayedGuilds[i], new Rectangle(50, y, 800, 60), i == hoveredGuildIndex && hoveredGuildIndex >= 0);
                    }
                }
            }
            else
            {
                // Draw no guilds message
                Rectangle noGuildsPanel = new Rectangle(50, 200, 800, 120);
                CrushItStyleHelper.DrawPanel(g, noGuildsPanel,
                    Color.FromArgb(255, 150, 110, 190),
                    Color.FromArgb(255, 110, 70, 150),
                    Color.FromArgb(255, 90, 50, 130));

                using (Font noGuildsFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
                using (Font subFont = new Font("Segoe UI", 12))
                using (Brush noGuildsBrush = new SolidBrush(Color.FromArgb(255, 220, 220, 255)))
                using (Brush subBrush = new SolidBrush(Color.FromArgb(255, 180, 180, 200)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("🏰 No Guilds Found", noGuildsFont, noGuildsBrush, new Rectangle(50, 220, 800, 40), sf);
                    g.DrawString("Be the first to create a guild and lead your team to victory!", subFont, subBrush, new Rectangle(50, 270, 800, 30), sf);
                }
            }

            // Draw create guild button
            Rectangle createGuildButton = new Rectangle(325, 450, 250, 40);
            DrawButton(g, createGuildButton, "✨ Create New Guild", false, Color.FromArgb(100, 200, 100));
        }

        private void DrawGuildMyGuildMode(Graphics g)
        {
            if (selectedGuild == null) return;

            // Draw guild info
            DrawGuildDetails(g, selectedGuild, new Rectangle(50, 85, 800, 350));

            // Draw decorative separator
            using (Pen separator = new Pen(Color.FromArgb(255, 255, 200, 120), 2))
            {
                g.DrawLine(separator, 50, 445, 850, 445);
            }

            // Draw circular leave button
            DrawCircularButton(g, leaveButtonRect, "🚪", isLeaveButtonHovered, Color.FromArgb(255, 120, 80, 80));
        }

        private void DrawGuildCreateMode(Graphics g)
        {
            // Draw back button
            DrawButton(g, backButtonRect, "← Back", isBackButtonHovered);

            // Draw create panel
            Rectangle createPanel = new Rectangle(50, 85, 800, 380);
            CrushItStyleHelper.DrawPanel(g, createPanel,
                Color.FromArgb(255, 160, 120, 200),
                Color.FromArgb(255, 120, 80, 160),
                Color.FromArgb(255, 100, 60, 140));

            // Draw decorative accent bar
            using (SolidBrush accent = new SolidBrush(Color.FromArgb(255, 255, 200, 100)))
            {
                g.FillRectangle(accent, 55, 90, 4, 370);
            }

            // Draw section title
            using (Font titleFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(Color.White))
            {
                g.DrawString("🏰 Create Your Guild", titleFont, titleBrush, 70, 100);
            }

            // Draw name input
            DrawInputBox(g, nameInputRect, guildNameInput.Length == 0 ? "📝 Guild Name (3-30 chars)" : guildNameInput,
                isNameFocused, isNameFocused);

            // Draw description input
            DrawMultiLineInputBox(g, descriptionInputRect,
                guildDescriptionInput.Length == 0 ? "📋 Guild Description (max 150 chars)" : guildDescriptionInput,
                isDescriptionFocused, isDescriptionFocused);

            // Draw create button
            DrawButton(g, createGuildButtonRect, "✨ Create Guild", isCreateGuildButtonHovered,
                isGuildProcessing ? Color.Gray : Color.FromArgb(100, 220, 100));
        }

        private void DrawGuildDetailsMode(Graphics g)
        {
            if (selectedGuild == null) return;

            // Draw back button
            DrawButton(g, backButtonRect, "← Back", isBackButtonHovered);

            // Draw guild info
            DrawGuildDetails(g, selectedGuild, new Rectangle(50, 85, 800, 350));

            // Draw action buttons
            bool isMember = !string.IsNullOrEmpty(currentUser.GuildId) && currentUser.GuildId == selectedGuild.Id;
            if (!isMember)
            {
                bool canJoin = selectedGuild.CanJoin(currentUser);
                DrawButton(g, joinButtonRect, canJoin ? "🤝 Join Guild" : "🔒 Cannot Join",
                    isJoinButtonHovered, canJoin ? Color.FromArgb(100, 220, 100) : Color.Gray);
            }
        }

        private void DrawGuildCard(Graphics g, Guild guild, Rectangle rect, bool isHovered)
        {
            CrushItStyleHelper.DrawPanel(g, rect,
                isHovered ? Color.FromArgb(255, 180, 140, 220) : Color.FromArgb(255, 150, 110, 190),
                isHovered ? Color.FromArgb(255, 140, 100, 180) : Color.FromArgb(255, 110, 70, 150),
                isHovered ? Color.FromArgb(255, 120, 80, 160) : Color.FromArgb(255, 90, 50, 130));

            // Draw decorative accent bar
            using (SolidBrush accent = new SolidBrush(isHovered ? Color.FromArgb(255, 255, 200, 100) : Color.FromArgb(255, 255, 160, 80)))
            {
                g.FillRectangle(accent, rect.X + 5, rect.Y + 5, 4, rect.Height - 10);
            }

            // Draw guild name
            using (Font nameFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
            using (Brush nameBrush = new SolidBrush(Color.White))
            {
                g.DrawString(guild.Name, nameFont, nameBrush, rect.X + 20, rect.Y + 10);
            }

            // Draw member count
            using (Font infoFont = new Font("Segoe UI", 12, FontStyle.Bold))
            using (Brush infoBrush = new SolidBrush(Color.FromArgb(255, 230, 230, 255)))
            {
                g.DrawString($"👥 {guild.MemberCount}/{guild.MaxMembers}", infoFont, infoBrush,
                    rect.X + 20, rect.Y + 38);
            }

            // Draw join status
            string statusText = guild.JoinStatus.ToString();
            Color statusColor = guild.JoinStatus == GuildJoinStatus.Open ? Color.FromArgb(100, 255, 100) :
                               guild.JoinStatus == GuildJoinStatus.InviteOnly ? Color.FromArgb(255, 200, 100) :
                               Color.FromArgb(255, 100, 100);

            Rectangle statusBadge = new Rectangle(rect.Right - 140, rect.Y + 8, 120, 20);
            using (SolidBrush statusBg = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                g.FillRectangle(statusBg, statusBadge);
            }
            using (Font statusFont = new Font("Segoe UI", 11, FontStyle.Bold))
            using (Brush statusBrush = new SolidBrush(statusColor))
            {
                g.DrawString(statusText, statusFont, statusBrush, rect.Right - 135, rect.Y + 8);
            }

            // Draw required level
            using (Font levelFont = new Font("Segoe UI", 11, FontStyle.Bold))
            using (Brush levelBrush = new SolidBrush(Color.FromArgb(255, 200, 200, 220)))
            {
                g.DrawString($"⚔️ Lv.{guild.RequiredLevel}+", levelFont, levelBrush, rect.Right - 135, rect.Y + 35);
            }
        }

        private void DrawGuildDetails(Graphics g, Guild guild, Rectangle rect)
        {
            CrushItStyleHelper.DrawPanel(g, rect,
                Color.FromArgb(255, 160, 120, 200),
                Color.FromArgb(255, 120, 80, 160),
                Color.FromArgb(255, 100, 60, 140));

            // Draw decorative accent bar
            using (SolidBrush accent = new SolidBrush(Color.FromArgb(255, 255, 200, 100)))
            {
                g.FillRectangle(accent, rect.X + 5, rect.Y + 5, 4, rect.Height - 10);
            }

            // Draw guild name
            using (Font nameFont = new Font("Comic Sans MS", 22, FontStyle.Bold))
            using (Brush nameBrush = new SolidBrush(Color.White))
            {
                g.DrawString($"🏰 {guild.Name}", nameFont, nameBrush, rect.X + 20, rect.Y + 15);
            }

            // Draw description
            using (Font descFont = new Font("Segoe UI", 12, FontStyle.Italic))
            using (Brush descBrush = new SolidBrush(Color.FromArgb(255, 240, 240, 255)))
            {
                g.DrawString(guild.Description, descFont, descBrush, rect.X + 20, rect.Y + 50);
            }

            // Draw stats section
            Rectangle statsRect = new Rectangle(rect.X + 20, rect.Y + 90, 350, 200);
            CrushItStyleHelper.DrawPanel(g, statsRect,
                Color.FromArgb(255, 140, 100, 180),
                Color.FromArgb(255, 110, 70, 150),
                Color.FromArgb(255, 90, 50, 130));

            using (Font statsTitleFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (Brush statsTitleBrush = new SolidBrush(Color.FromArgb(255, 255, 200, 100)))
            {
                g.DrawString("📊 Guild Stats", statsTitleFont, statsTitleBrush, statsRect.X + 15, statsRect.Y + 15);
            }

            using (Font statsFont = new Font("Segoe UI", 12, FontStyle.Bold))
            using (Brush statsBrush = new SolidBrush(Color.White))
            {
                int y = statsRect.Y + 45;
                g.DrawString($"👑 Leader: {guild.LeaderUsername}", statsFont, statsBrush, statsRect.X + 15, y);
                g.DrawString($"👥 Members: {guild.MemberCount}/{guild.MaxMembers}", statsFont, statsBrush, statsRect.X + 15, y + 30);
                g.DrawString($"⭐ Total Score: {guild.TotalMemberScore}", statsFont, statsBrush, statsRect.X + 15, y + 60);
                g.DrawString($"🔓 Status: {guild.JoinStatus}", statsFont, statsBrush, statsRect.X + 15, y + 90);
                g.DrawString($"⚔️ Required Level: {guild.RequiredLevel}+", statsFont, statsBrush, statsRect.X + 15, y + 120);
            }

            // Draw top members section
            Rectangle membersRect = new Rectangle(rect.X + 390, rect.Y + 90, 370, 200);
            CrushItStyleHelper.DrawPanel(g, membersRect,
                Color.FromArgb(255, 140, 100, 180),
                Color.FromArgb(255, 110, 70, 150),
                Color.FromArgb(255, 90, 50, 130));

            using (Font memberTitleFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (Brush memberTitleBrush = new SolidBrush(Color.FromArgb(255, 255, 200, 100)))
            {
                g.DrawString("🏆 Top Members", memberTitleFont, memberTitleBrush, membersRect.X + 15, membersRect.Y + 15);
            }

            var topMembers = guild.Members.OrderByDescending(m => m.HighestScore).Take(5).ToList();
            using (Font memberFont = new Font("Segoe UI", 11, FontStyle.Bold))
            using (Brush memberBrush = new SolidBrush(Color.FromArgb(255, 230, 230, 255)))
            {
                for (int i = 0; i < topMembers.Count; i++)
                {
                    var member = topMembers[i];
                    string roleIcon = member.Role == GuildRole.Leader ? "👑" :
                                     member.Role == GuildRole.Officer ? "⭐" : "•";
                    g.DrawString($"{i + 1}. {roleIcon} {member.Username} - {member.HighestScore} pts",
                        memberFont, memberBrush, membersRect.X + 15, membersRect.Y + 45 + i * 28);
                }
            }
        }

        private void DrawInputBox(Graphics g, Rectangle rect, string text, bool isFocused, bool isValid)
        {
            Color bgColor = isFocused ? Color.FromArgb(255, 120, 80, 160) : Color.FromArgb(255, 100, 70, 150);
            Color borderColor = isFocused ? Color.FromArgb(255, 180, 140, 220) : Color.FromArgb(255, 140, 100, 180);

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            using (Pen borderPen = new Pen(borderColor, 2))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            using (Font textFont = new Font("Segoe UI", 12))
            using (Brush textBrush = new SolidBrush(isFocused ? Color.White : Color.FromArgb(220, 220, 240)))
            {
                g.DrawString(text, textFont, textBrush, rect.X + 10, rect.Y + 10);
            }
        }

        private void DrawMultiLineInputBox(Graphics g, Rectangle rect, string text, bool isFocused, bool isValid)
        {
            Color bgColor = isFocused ? Color.FromArgb(255, 120, 80, 160) : Color.FromArgb(255, 100, 70, 150);
            Color borderColor = isFocused ? Color.FromArgb(255, 180, 140, 220) : Color.FromArgb(255, 140, 100, 180);

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            using (Pen borderPen = new Pen(borderColor, 2))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            using (Font textFont = new Font("Segoe UI", 11))
            using (Brush textBrush = new SolidBrush(isFocused ? Color.White : Color.FromArgb(220, 220, 240)))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            {
                g.DrawString(text, textFont, textBrush, new RectangleF(rect.X + 10, rect.Y + 10, rect.Width - 20, rect.Height - 20), format);
            }
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, bool isHovered, Color? customColor = null)
        {
            Color baseColor = customColor ?? Color.FromArgb(255, 100, 150, 200);
            Color hoverColor = customColor ?? Color.FromArgb(255, 120, 170, 220);
            Color bgColor = isHovered ? hoverColor : baseColor;

            using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect,
                isHovered ? Color.FromArgb(255, 140, 190, 240) : Color.FromArgb(255, 120, 170, 220),
                isHovered ? Color.FromArgb(255, 100, 150, 200) : Color.FromArgb(255, 80, 130, 180),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, rect);
            }

            using (Pen borderPen = new Pen(Color.FromArgb(255, 200, 220, 255), 2))
            {
                g.DrawRectangle(borderPen, rect);
            }

            using (Font textFont = new Font("Comic Sans MS", 12, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, textFont, textBrush, rect, format);
            }
        }

        private void DrawCircularButton(Graphics g, Rectangle rect, string emoji, bool isHovered, Color baseColor)
        {
            Color bgColor = isHovered ? Color.FromArgb(255, Math.Min(255, baseColor.R + 30), Math.Min(255, baseColor.G + 30), Math.Min(255, baseColor.B + 30)) : baseColor;

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillEllipse(bgBrush, rect);
            }

            using (Pen borderPen = new Pen(Color.FromArgb(255, 255, 255, 255), 2))
            {
                g.DrawEllipse(borderPen, rect);
            }

            using (Font emojiFont = new Font("Segoe UI Emoji", 18, FontStyle.Bold))
            using (Brush emojiBrush = new SolidBrush(Color.White))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(emoji, emojiFont, emojiBrush, rect, format);
            }
        }

        private async void MainFrame_FormClosed(object? sender, FormClosedEventArgs e)
        {
            animationTimer?.Stop();
            
            // Sync progress with server on app close
            try
            {
                await ProgressSyncService.SyncOnCloseAsync(currentUser, database, apiClient);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sync on close failed: {ex.Message}");
            }
        }
    }
}