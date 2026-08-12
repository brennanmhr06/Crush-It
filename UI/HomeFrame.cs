using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;
using CrushIt.UI;

namespace CrushIt.UI
{
    public class HomeFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private System.Windows.Forms.Timer animationTimer = null!;
        private int pulsePhase = 0;
        private readonly Random particleRand = new Random();
        private StyleParticle[] backgroundParticles = Array.Empty<StyleParticle>();

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

            // Set particles to use form size
            backgroundParticles = CrushItStyleHelper.CreateParticles(particleRand, 30, 890, 80, 530); // Reduced from 45 to 30
            
            LoadUserData();
            StartAnimation();


            SoundManager.StartBackgroundMusic();
            SoundManager.SetBackgroundMusicVolume(0.3f);

            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Home";
            this.Size = new Size(900, 650);
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

            this.FormClosed += (s, e) => {
                animationTimer?.Stop();
                SoundManager.StopBackgroundMusic();
                if (Application.OpenForms.Count == 0)
                {
                    SoundManager.Cleanup();
                    Application.Exit();
                }
            };
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
            SoundManager.PlaySound(SoundType.ButtonClick);
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

            SoundManager.PlaySound(SoundType.ButtonClick);

            if (clicked == NavItem.Levels)
            {
                SoundManager.PlaySound(SoundType.Navigation);
            {
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
            }
            else if (clicked == NavItem.Achievements)
            {
                SoundManager.PlaySound(SoundType.Navigation);
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
            // Draw enhanced progression path with winding layout
            int pathY = 195;
            int pathHeight = 300;
            int pathStart = 50;
            int pathEnd = 850;
            
            // Main path container
            Rectangle pathRect = new Rectangle(pathStart, pathY, pathEnd - pathStart, pathHeight);
            CrushItStyleHelper.DrawPanel(g, pathRect, Color.FromArgb(255, 130, 95, 185), Color.FromArgb(255, 95, 60, 155), Color.FromArgb(255, 80, 50, 130));

            using (Font sectionFont = new Font("Comic Sans MS", 13, FontStyle.Bold))
            {
                CrushItStyleHelper.DrawOutlinedText(g, "YOUR JOURNEY", sectionFont, new Rectangle(pathRect.X, pathRect.Y + 6, pathRect.Width, 24), Color.White, Color.Black, 1);
            }

            // Draw winding path connections
            DrawWindingPath(g, pathRect);

            // Create stat nodes along a winding path (S-curve pattern)
            int nodeSize = 70;
            int nodeSpacing = 110;
            int startNodeX = pathRect.X + 80;
            int nodeY = pathRect.Y + 50;
            
            // S-curve layout for more interesting path
            // Row 1 (left to right)
            DrawStatNode(g, new Rectangle(startNodeX, nodeY, nodeSize, nodeSize),
                "🎯", levelsCompleted.ToString(), "Levels", Color.FromArgb(255, 100, 200, 255), true);
            
            DrawStatNode(g, new Rectangle(startNodeX + nodeSpacing, nodeY, nodeSize, nodeSize),
                "🏆", highestLevel > 0 ? highestLevel.ToString() : "—", "Best Level", Color.FromArgb(255, 255, 180, 60), highestLevel > 0);
            
            DrawStatNode(g, new Rectangle(startNodeX + nodeSpacing * 2, nodeY, nodeSize, nodeSize),
                "⭐", highestScore > 0 ? highestScore.ToString("N0") : "—", "High Score", Color.FromArgb(255, 255, 140, 200), highestScore > 1000);
            
            // Row 2 (right to left for winding effect)
            int row2Y = nodeY + nodeSize + 30;
            
            DrawStatNode(g, new Rectangle(startNodeX + nodeSpacing * 2, row2Y, nodeSize, nodeSize),
                "🏅", $"{achievementsUnlocked}/{achievementsTotal}", "Achieved", Color.FromArgb(255, 200, 160, 255), achievementsUnlocked >= 5);
            
            DrawStatNode(g, new Rectangle(startNodeX + nodeSpacing, row2Y, nodeSize, nodeSize),
                "💰", gold.ToString("N0"), "Gold", Color.FromArgb(255, 255, 215, 50), gold >= 1000);
            
            DrawStatNode(g, new Rectangle(startNodeX, row2Y, nodeSize, nodeSize),
                "💥", totalMatches.ToString("N0"), "Matches", Color.FromArgb(255, 140, 255, 160), totalMatches >= 100);
            
            // Row 3 (left to right for continuation)
            int row3Y = row2Y + nodeSize + 30;
            
            DrawStatNode(g, new Rectangle(startNodeX, row3Y, nodeSize, nodeSize),
                "📅", daysPlaying.ToString(), "Days", Color.FromArgb(255, 170, 220, 255), daysPlaying >= 7);
            
            DrawStatNode(g, new Rectangle(startNodeX + nodeSpacing, row3Y, nodeSize, nodeSize),
                "🎖️", rankTitle.Replace(" Crusher", ""), "Rank", Color.FromArgb(255, 255, 160, 80), levelsCompleted >= 10);
            
            // Draw milestone markers along the path
            DrawMilestoneMarkers(g, pathRect, startNodeX, nodeY, nodeSize, nodeSpacing);
        }

        private void DrawWindingPath(Graphics g, Rectangle pathRect)
        {
            // Draw a winding path line connecting the nodes
            using (Pen pathPen = new Pen(Color.FromArgb(100, 255, 255, 255), 4))
            {
                pathPen.DashPattern = new float[] { 8, 4 };
                
                // Create S-curve path
                Point[] pathPoints = new Point[]
                {
                    new Point(pathRect.X + 40, pathRect.Y + 85),
                    new Point(pathRect.X + 400, pathRect.Y + 85),
                    new Point(pathRect.X + 400, pathRect.Y + 115),
                    new Point(pathRect.X + 720, pathRect.Y + 115),
                    new Point(pathRect.X + 720, pathRect.Y + 145),
                    new Point(pathRect.X + 200, pathRect.Y + 145),
                    new Point(pathRect.X + 200, pathRect.Y + 175),
                    new Point(pathRect.X + 530, pathRect.Y + 175)
                };
                
                g.DrawLines(pathPen, pathPoints);
            }
            
            // Add glowing dots at path junctions
            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 200)))
            {
                Point[] junctions = new Point[]
                {
                    new Point(pathRect.X + 400, pathRect.Y + 85),
                    new Point(pathRect.X + 720, pathRect.Y + 115),
                    new Point(pathRect.X + 200, pathRect.Y + 145)
                };
                
                foreach (var junction in junctions)
                {
                    int pulse = (int)(5 * Math.Sin(pulsePhase * Math.PI / 30));
                    g.FillEllipse(glowBrush, junction.X - 8 - pulse, junction.Y - 8 - pulse, 16 + pulse * 2, 16 + pulse * 2);
                }
            }
        }

        private void DrawMilestoneMarkers(Graphics g, Rectangle pathRect, int startX, int startY, int nodeSize, int spacing)
        {
            // Draw small milestone indicators along the path
            int[] milestoneX = { startX + spacing * 3, startX + spacing * 3, startX + spacing * 2 };
            int[] milestoneY = { startY, startY + nodeSize + 30, startY + nodeSize * 2 + 60 };
            
            for (int i = 0; i < milestoneX.Length; i++)
            {
                int mx = milestoneX[i] + nodeSize + 15;
                int my = milestoneY[i] + nodeSize / 2;
                
                bool isReached = i < levelsCompleted / 10;
                
                Color markerColor = isReached ? Color.FromArgb(255, 100, 255, 150) : Color.FromArgb(255, 100, 100, 120);
                
                using (SolidBrush markerBrush = new SolidBrush(markerColor))
                {
                    g.FillEllipse(markerBrush, mx - 6, my - 6, 12, 12);
                }
                
                using (Pen markerPen = new Pen(Color.FromArgb(255, 255, 255, 255), 2))
                {
                    g.DrawEllipse(markerPen, mx - 6, my - 6, 12, 12);
                }
                
                if (isReached)
                {
                    // Add star for reached milestones
                    using (Font starFont = new Font("Segoe UI Emoji", 10, FontStyle.Bold))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("⭐", starFont, Brushes.White, new RectangleF(mx - 8, my - 8, 16, 16), sf);
                    }
                }
            }
        }

        private void DrawStatNode(Graphics g, Rectangle rect, string icon, string value, string label, Color themeColor, bool isCompleted)
        {
            // Node background with path styling
            Color topColor, bottomColor, borderColor;
            
            if (isCompleted)
            {
                topColor = Color.FromArgb(255, Math.Min(255, themeColor.R + 30), Math.Min(255, themeColor.G + 30), Math.Min(255, themeColor.B + 30));
                bottomColor = themeColor;
                borderColor = Color.FromArgb(255, Math.Max(0, themeColor.R - 40), Math.Max(0, themeColor.G - 40), Math.Max(0, themeColor.B - 40));
            }
            else
            {
                topColor = Color.FromArgb(255, 120, 110, 140);
                bottomColor = Color.FromArgb(255, 90, 80, 110);
                borderColor = Color.FromArgb(255, 70, 60, 90);
            }

            CrushItStyleHelper.DrawPanel(g, rect, topColor, bottomColor, borderColor);

            // Glow effect for completed nodes
            if (isCompleted)
            {
                int glowPulse = (int)(15 * Math.Sin(pulsePhase * Math.PI / 60));
                int glowAlpha = Math.Max(0, Math.Min(255, 30 + glowPulse));
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(glowAlpha, themeColor)))
                {
                    g.FillEllipse(glow, rect.X - 5, rect.Y - 5, rect.Width + 10, rect.Height + 10);
                }
            }

            // Icon
            using (Font iconFont = new Font("Segoe UI Emoji", 20, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(icon, iconFont, Brushes.White, new RectangleF(rect.X, rect.Y + 8, rect.Width, 30), sf);
            }

            // Value
            using (Font valueFont = new Font("Comic Sans MS", 16, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, value, valueFont, new Rectangle(rect.X, rect.Y + 32, rect.Width, 24), Color.White, Color.FromArgb(150, 0, 0, 0), 1, sf);
            }

            // Label
            using (Font labelFont = new Font("Comic Sans MS", 9, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(label, labelFont, new SolidBrush(Color.FromArgb(220, 255, 255, 255)), new RectangleF(rect.X, rect.Y + 52, rect.Width, 16), sf);
            }
        }

        private void DrawProgressSection(Graphics g)
        {
            Rectangle section = new Rectangle(50, 460, 800, 55);
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

