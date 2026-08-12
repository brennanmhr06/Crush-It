using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;

namespace CrushIt.UI
{
    public class CoinParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float SpeedX { get; set; }
        public float SpeedY { get; set; }
        public int Size { get; set; }
        public int Alpha { get; set; }
        public int Rotation { get; set; }
    }

    public class AchievementsFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private readonly IMongoCollection<UserAccount> usersCollection;

        private List<Achievement> userAchievements = null!;
        private System.Windows.Forms.Timer animationTimer = null!;
        private int pulsePhase = 0;
        private int scrollOffset = 0;
        private int targetScrollOffset = 0;

        private StyleParticle[] backgroundParticles = Array.Empty<StyleParticle>();
        private Random particleRand = new Random();


        private List<CoinParticle> coinParticles = new List<CoinParticle>();
        private bool isCoinAnimating = false;

        public AchievementsFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;
            this.usersCollection = database.GetCollection<UserAccount>("users");


            LoadUserAchievements();

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            InitializeParticles();
            StartAnimation();


            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeParticles()
        {
            backgroundParticles = CrushItStyleHelper.CreateParticles(particleRand, 30, 890, 80, 530); // Reduced from 45 to 30
        }

        private void LoadUserAchievements()
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

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Achievements";
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
            this.MouseDown += AchievementsFrame_MouseDown;
            this.MouseWheel += AchievementsFrame_MouseWheel;
            this.FormClosed += (s, e) => animationTimer?.Stop();
        }

        private void AchievementsFrame_MouseDown(object? sender, MouseEventArgs e)
        {
            if (CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem nav))
            {
                Application.Exit();
            }


            int startY = 160;
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

        private void AchievementsFrame_MouseWheel(object? sender, MouseEventArgs e)
        {
            int scrollAmount = e.Delta / 10;
            targetScrollOffset += scrollAmount;

            // Calculate scroll bounds
            int startY = 160;
            int achievementHeight = 95;
            int gap = 18;
            int totalHeight = userAchievements.Count * (achievementHeight + gap) + startY;
            int maxScroll = Math.Max(0, totalHeight - (this.ClientSize.Height - 100));

            if (targetScrollOffset < -maxScroll) targetScrollOffset = -maxScroll;
            if (targetScrollOffset > 0) targetScrollOffset = 0;

            this.Invalidate();
        }

        private async void ClaimAchievement(Achievement achievement)
        {
            achievement.IsClaimed = true;
            currentUser.Gold += achievement.GoldReward;

            try
            {
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
            LoadUserAchievements();

            StartCoinAnimation();

            this.Invalidate();
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
                    Alpha = 255,
                    Rotation = particleRand.Next(0, 360)
                });
            }
        }

        private void StartAnimation()
        {
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 30;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            pulsePhase++;

            CrushItStyleHelper.UpdateParticles(backgroundParticles, this.ClientSize.Width, 60, this.ClientSize.Height - 120);


            if (isCoinAnimating)
            {
                bool hasActiveCoins = false;
                for (int i = coinParticles.Count - 1; i >= 0; i--)
                {
                    var coin = coinParticles[i];
                    coin.X += coin.SpeedX;
                    coin.Y += coin.SpeedY;
                    coin.Rotation += 5;
                    coin.Alpha -= 3;
                    coin.Alpha = Math.Max(0, coin.Alpha);

                    if (coin.Y > this.ClientSize.Height + 50 || coin.Alpha <= 0)
                    {
                        coinParticles.RemoveAt(i);
                    }
                    else
                    {
                        coinParticles[i] = coin;
                        hasActiveCoins = true;
                    }
                }

                if (!hasActiveCoins)
                {
                    isCoinAnimating = false;
                }
            }


            if (scrollOffset != targetScrollOffset)
            {
                int diff = targetScrollOffset - scrollOffset;
                scrollOffset += diff / 10;
                if (Math.Abs(diff) < 1)
                    scrollOffset = targetScrollOffset;
            }

            this.Invalidate();
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            Application.Exit();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);

            DrawAchievementsHeader(g);
            DrawAchievementsList(g);
            CrushItStyleHelper.DrawNavigationBar(g, this.ClientSize.Width, this.ClientSize.Height, NavItem.Achievements, pulsePhase);

            if (isCoinAnimating)
                DrawCoinParticles(g);
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
                "Unlocked", unlockedCount, totalCount, Color.FromArgb(255, 255, 180, 60));
        }

        private void DrawAchievementsList(Graphics g)
        {
            int startY = 160;
            int achievementHeight = 95;
            int gap = 18;
            int availableWidth = 800;
            int startX = 50;


            int totalHeight = userAchievements.Count * (achievementHeight + gap) + startY;
            int maxScroll = Math.Max(0, totalHeight - (this.ClientSize.Height - 100));


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
                    g.FillRectangle(shadow, new Rectangle(achievementRect.X + 6, achievementRect.Y + 6, achievementRect.Width, achievementRect.Height));
                }

                // Draw main card with gradient
                using (LinearGradientBrush cardBrush = new LinearGradientBrush(
                    achievementRect, topColor, bottomColor, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(cardBrush, achievementRect);
                }

                // Draw border
                using (Pen borderPen = new Pen(borderColor, 3))
                {
                    g.DrawRectangle(borderPen, achievementRect);
                }

                // Inner highlight
                using (SolidBrush highlight = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
                {
                    g.FillRectangle(highlight, achievementRect.X + 3, achievementRect.Y + 3, achievementRect.Width - 6, 8);
                }

                // Glow effect for unlocked achievements
                if (achievement.IsUnlocked)
                {
                    int glowAlpha = 60 + (int)(30 * Math.Sin(pulsePhase * Math.PI / 25));
                    glowAlpha = Math.Max(0, Math.Min(255, glowAlpha));
                    using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, glowColor)))
                    {
                        g.FillRectangle(glowBrush, achievementRect.X - 2, achievementRect.Y - 2, achievementRect.Width + 4, achievementRect.Height + 4);
                    }
                }

                // Trophy icon for achievement
                int iconSize = 60;
                int iconX = achievementRect.X + 18;
                int iconY = achievementRect.Y + (achievementHeight - iconSize) / 2;

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
                            new RectangleF(iconX + iconSize + 20, achievementRect.Y + 12, achievementRect.Width - iconSize - 180, 28), sf);
                        g.DrawString(achievement.Name, nameFont, new SolidBrush(nameColor),
                            new RectangleF(iconX + iconSize + 18, achievementRect.Y + 10, achievementRect.Width - iconSize - 180, 28), sf);
                    }
                }

                // Achievement description
                using (Font descFont = new Font("Comic Sans MS", 12))
                {
                    Color descColor = achievement.IsUnlocked ? Color.FromArgb(245, 245, 245) : Color.FromArgb(170, 170, 170);
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                    {
                        g.DrawString(achievement.Description, descFont, new SolidBrush(descColor),
                            new RectangleF(iconX + iconSize + 20, achievementRect.Y + 42, achievementRect.Width - iconSize - 180, 35), sf);
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
                            int goldY = isCloseToUnlock ? achievementRect.Y + 55 : achievementRect.Y + 72;
                            g.DrawString(goldText, goldFont, new SolidBrush(goldColor),
                                new RectangleF(iconX + iconSize + 20, goldY, achievementRect.Width - iconSize - 50, 20), sf);
                        }
                    }
                }

                // Progress indicator for locked achievements close to unlock
                if (!achievement.IsUnlocked && isCloseToUnlock)
                {
                    int progressWidth = 120;
                    int progressHeight = 8;
                    int progressX = iconX + iconSize + 20;
                    int progressY = achievementRect.Y + 65;

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

                y += achievementHeight + gap;
            }
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
    }
}

