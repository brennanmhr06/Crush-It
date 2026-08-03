using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;

namespace CrushIt.UI
{
    public class HomeFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private System.Windows.Forms.Timer animationTimer = null!;
        private int pulsePhase = 0;
        private readonly Random particleRand = new Random();
        private readonly List<StyleParticle> backgroundParticles = new List<StyleParticle>();

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

        private readonly NavItem currentNav = NavItem.Home;

        public HomeFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 45, 890, 80, 480));
            LoadUserData();
            StartAnimation();


            SoundHelper.StartBackgroundMusic();
            SoundHelper.SetBackgroundMusicVolume(0.3f);

            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Home";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
            this.MouseClick += HomeFrame_MouseClick;

            usernameLabel = new Label
            {
                Font = new Font("Comic Sans MS", 20, FontStyle.Bold),
                Size = new Size(320, 40),
                Location = new Point(210, 108),
                Text = currentUser.Username,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
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
                TextAlign = ContentAlignment.MiddleCenter
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

            this.FormClosed += (s, e) => animationTimer?.Stop();
        }

        private async void LoadUserData()
        {
            if (string.IsNullOrEmpty(currentUser.UserId))
            {
                currentUser.UserId = Guid.NewGuid().ToString("N");

                var usersCollection = database.GetCollection<UserAccount>("users");
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Id, currentUser.Id);
                var update = Builders<UserAccount>.Update.Set(u => u.UserId, currentUser.UserId);
                await usersCollection.UpdateOneAsync(filter, update);
            }

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
            this.Invalidate();
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
            usernameLabel.Visible = true;
            pencilIconLabel.Visible = true;
            usernameLabel.Text = "@" + currentUser.Username;
        }

        private void HomeFrame_MouseClick(object? sender, MouseEventArgs e)
        {
            if (!CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem clicked))
                return;

            if (clicked == NavItem.Levels)
            {
                MainFrame main = new MainFrame(currentUser, database);
                main.Show();
                this.Close();
            }
            else if (clicked == NavItem.Achievements)
            {
                MainFrame main = new MainFrame(currentUser, database);
                main.Show();
                this.Close();
            }
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
            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 100);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);
            DrawTitleBanner(g);
            DrawProfileCard(g);
            DrawStatsGrid(g);
            DrawProgressSection(g);
            CrushItStyleHelper.DrawNavigationBar(g, this.ClientSize.Width, this.ClientSize.Height, currentNav, pulsePhase);
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
    }
}

