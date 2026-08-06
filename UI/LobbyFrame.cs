using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;

namespace CrushIt.UI
{
    public class LevelNode
    {
        public int Number { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool Unlocked { get; set; }
        public bool Completed { get; set; }
    }

    public class LobbyFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private List<StyleParticle> backgroundParticles = new List<StyleParticle>();
        private System.Windows.Forms.Timer animationTimer = null!;
        private Random particleRand = new Random();
        private int pulsePhase = 0;


        private LevelNode[] levels = new LevelNode[0];
        private int levelsPerRow = 10;
        private int maxRows = 4;
        private int baseLevelNumber = 1;
        private int totalLevelsCompleted = 0;
        private int hoveredLevelIndex = -1;

        private NavItem currentNav = NavItem.Levels;

        public LobbyFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;


            CalculateRowProgression();
            GenerateLevelsForCurrentRows();
            UpdateLevelStatus();

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            InitializeParticles();
            StartAnimation();


            SoundHelper.StartBackgroundMusic();
            SoundHelper.SetBackgroundMusicVolume(0.3f);


            MobileHelper.ApplyMobileScaling(this);
        }

        private IMongoDatabase GetDatabase()
        {
            return database;
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

        private void InitializeParticles()
        {
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 45, 890, 80, 530));
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
            this.Invalidate();
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

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Lobby";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Application.Exit();
                }
            };

            this.MouseDown += LobbyFrame_MouseDown;
            this.MouseMove += LobbyFrame_MouseMove;
            this.MouseLeave += (s, e) => { hoveredLevelIndex = -1; this.Invalidate(); };
            this.FormClosed += (s, e) => animationTimer?.Stop();
        }

        private void LobbyFrame_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e == null) return;
            
            if (currentNav == NavItem.Levels)
            {
                int oldHoveredIndex = hoveredLevelIndex;
                hoveredLevelIndex = -1;
                
                foreach (var level in levels)
                {
                    int nodeRadius = 40;
                    int dx = e.X - level.X;
                    int dy = e.Y - level.Y;
                    if (dx * dx + dy * dy <= nodeRadius * nodeRadius)
                    {
                        hoveredLevelIndex = Array.IndexOf(levels, level);
                        break;
                    }
                }
                
                if (oldHoveredIndex != hoveredLevelIndex)
                {
                    bool canClick = hoveredLevelIndex >= 0 && levels[hoveredLevelIndex].Unlocked;
                    this.Cursor = hoveredLevelIndex >= 0 ? (canClick ? Cursors.Hand : Cursors.No) : Cursors.Default;
                    this.Invalidate();
                }
            }
        }

        private void LobbyFrame_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e == null) return;
            if (CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem clickedNav))
            {
                if (clickedNav == NavItem.Home || clickedNav == NavItem.Achievements)
                {
                    MainFrame main = new MainFrame(currentUser, GetDatabase());
                    main.Show();
                    this.Close();
                }
                else
                {
                    currentNav = clickedNav;
                    this.Invalidate();
                }
            }


            if (currentNav == NavItem.Levels)
            {
                foreach (var level in levels)
                {
                    if (level.Unlocked)
                    {
                        int nodeRadius = 40;
                        int dx = e.X - level.X;
                        int dy = e.Y - level.Y;
                        if (dx * dx + dy * dy <= nodeRadius * nodeRadius)
                        {

                            GameFrame game = new GameFrame(currentUser, level.Number);
                            game.Show();
                            this.Hide();
                            this.Dispose();
                            break;
                        }
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);

            if (currentNav == NavItem.Levels)
                DrawLevelsView(g);
            else if (currentNav == NavItem.Home)
                DrawHomeView(g);
            else if (currentNav == NavItem.Achievements)
                DrawAchievementsView(g);
            else if (currentNav == NavItem.Guilds)
                DrawGuildsView(g);

            CrushItStyleHelper.DrawNavigationBar(g, this.ClientSize.Width, this.ClientSize.Height, currentNav, pulsePhase);
        }

        private void DrawLevelsView(Graphics g)
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
            int levelIndex = Array.IndexOf(levels, level);
            bool isHovered = (levelIndex == hoveredLevelIndex);
            bool canInteract = isHovered && level.Unlocked;
            
            int size = canInteract ? 72 : 56;
            int x = level.X - size / 2;
            int y = level.Y - size / 2;
            Rectangle nodeRect = new Rectangle(x, y, size, size);

            Color topColor, bottomColor, borderColor;
            if (level.Completed)
            {
                topColor = canInteract ? Color.FromArgb(255, 180, 255, 180) : Color.FromArgb(255, 120, 230, 120);
                bottomColor = canInteract ? Color.FromArgb(255, 140, 220, 140) : Color.FromArgb(255, 70, 170, 70);
                borderColor = canInteract ? Color.FromArgb(255, 100, 190, 100) : Color.FromArgb(255, 40, 130, 40);
            }
            else if (level.Unlocked)
            {
                topColor = isHovered ? Color.FromArgb(255, 255, 220, 255) : Color.FromArgb(255, 200, 170, 240);
                bottomColor = isHovered ? Color.FromArgb(255, 220, 170, 255) : Color.FromArgb(255, 140, 100, 200);
                borderColor = isHovered ? Color.FromArgb(255, 180, 140, 220) : Color.FromArgb(255, 100, 70, 160);
            }
            else
            {
                topColor = isHovered ? Color.FromArgb(255, 130, 115, 150) : Color.FromArgb(255, 90, 75, 110);
                bottomColor = isHovered ? Color.FromArgb(255, 100, 90, 125) : Color.FromArgb(255, 60, 50, 85);
                borderColor = isHovered ? Color.FromArgb(255, 85, 80, 105) : Color.FromArgb(255, 45, 40, 65);
            }

            CrushItStyleHelper.DrawPanel(g, nodeRect, topColor, bottomColor, borderColor);

            if (level.Unlocked && !level.Completed)
            {
                int glowPulse = (int)(25 * Math.Sin(pulsePhase * Math.PI / 60));
                int glowSize = isHovered ? 18 : 8;
                int glowAlpha = isHovered ? 80 + glowPulse : 40 + glowPulse;
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, 255, 230, 130)))
                    g.FillEllipse(glow, x - glowSize, y - glowSize, size + glowSize * 2, size + glowSize * 2);
            }

            int fontSize = canInteract ? 28 : 22;
            using (Font numFont = new Font("Comic Sans MS", fontSize, FontStyle.Bold))
            {
                Color textColor = level.Unlocked ? Color.White : Color.FromArgb(180, 160, 160, 175);
                CrushItStyleHelper.DrawOutlinedText(g, level.Number.ToString(), numFont, nodeRect, textColor, Color.Black, 1);
            }
        }

        private void DrawPlaceholderView(Graphics g, string title, string message)
        {
            Rectangle contentPanel = new Rectangle(100, 120, 700, 300);
            CrushItStyleHelper.DrawPanel(g, contentPanel,
                Color.FromArgb(255, 130, 95, 185),
                Color.FromArgb(255, 95, 60, 155),
                Color.FromArgb(255, 80, 50, 130));

            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(250, 30, 400, 55), title);

            using (Font subFont = new Font("Comic Sans MS", 16, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, message, subFont, contentPanel, Color.FromArgb(220, 255, 255, 255), Color.Black, 1, sf);
            }
        }

        private void DrawHomeView(Graphics g)
        {
            DrawPlaceholderView(g, "STATS", "Tap STATS in the nav bar to view your profile!");
        }

        private void DrawAchievementsView(Graphics g)
        {
            DrawPlaceholderView(g, "ACHIEVEMENTS", "Tap ACHIEVEMENTS in the nav bar to view your trophies!");
        }

        private void DrawGuildsView(Graphics g)
        {
            DrawPlaceholderView(g, "GUILDS", "Guilds coming soon!");
        }
    }
}

