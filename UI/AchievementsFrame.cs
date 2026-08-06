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

        private List<StyleParticle> backgroundParticles = new List<StyleParticle>();
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
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 45, 890, 80, 530));
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


            userAchievements = userAchievements
                .OrderByDescending(a => a.IsUnlocked && !a.IsClaimed)
                .ThenByDescending(a => a.IsUnlocked && a.IsClaimed)
                .ThenBy(a => a.Type)
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
                    MainFrame main = new MainFrame(currentUser, database);
                    main.Show();
                    this.Close();
                }
            };
            this.MouseDown += AchievementsFrame_MouseDown;
            this.FormClosed += (s, e) => animationTimer?.Stop();
        }

        private void AchievementsFrame_MouseDown(object? sender, MouseEventArgs e)
        {
            if (CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem nav))
            {
                MainFrame main = new MainFrame(currentUser, database);
                main.Show();
                this.Close();
            }


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
                System.Diagnostics.Debug.WriteLine($"Failed to claim achievement: {ex.Message}");
            }


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
            MainFrame main = new MainFrame(currentUser, database);
            main.Show();
            this.Close();
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
            int achievementHeight = 80;
            int gap = 15;
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

                Color topColor, bottomColor, borderColor;
                if (achievement.IsUnlocked && !achievement.IsClaimed)
                {
                    topColor = Color.FromArgb(255, 130, 200, 130);
                    bottomColor = Color.FromArgb(255, 70, 150, 70);
                    borderColor = Color.FromArgb(255, 40, 120, 40);
                }
                else if (achievement.IsUnlocked)
                {
                    topColor = Color.FromArgb(255, 200, 160, 100);
                    bottomColor = Color.FromArgb(255, 160, 110, 50);
                    borderColor = Color.FromArgb(255, 120, 80, 30);
                }
                else
                {
                    topColor = Color.FromArgb(255, 90, 75, 115);
                    bottomColor = Color.FromArgb(255, 60, 50, 90);
                    borderColor = Color.FromArgb(255, 45, 40, 70);
                }

                CrushItStyleHelper.DrawPanel(g, achievementRect, topColor, bottomColor, borderColor);


                int iconSize = 55;
                int iconX = achievementRect.X + 20;
                int iconY = achievementRect.Y + (achievementHeight - iconSize) / 2;

                Color iconColor;
                if (achievement.IsUnlocked && !achievement.IsClaimed)
                    iconColor = Color.FromArgb(255, 80, 255, 80);
                else if (achievement.IsUnlocked)
                    iconColor = ColorTranslator.FromHtml(achievement.IconColor);
                else
                    iconColor = Color.FromArgb(150, 150, 150);


                using (SolidBrush iconShadow = new SolidBrush(Color.FromArgb(100, iconColor.R / 2, iconColor.G / 2, iconColor.B / 2)))
                {
                    g.FillRectangle(iconShadow, iconX + 4, iconY + 4, iconSize, iconSize);
                }


                using (SolidBrush iconBrush = new SolidBrush(iconColor))
                {
                    g.FillRectangle(iconBrush, iconX, iconY, iconSize, iconSize);
                }


                using (SolidBrush iconHighlight = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                {
                    g.FillRectangle(iconHighlight, iconX + 2, iconY + 2, iconSize / 3, iconSize / 3);
                }


                if (achievement.IsUnlocked)
                {
                    int glowAlpha = 80 + (int)(40 * Math.Sin(pulsePhase * Math.PI / 30));
                    Color glowColor = achievement.IsClaimed ? iconColor : Color.FromArgb(50, 255, 50);
                    using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, glowColor)))
                    {
                        g.FillRectangle(glowBrush, iconX - 4, iconY - 4, iconSize + 8, iconSize + 8);
                    }
                }


                using (Font nameFont = new Font("Comic Sans MS", 16, FontStyle.Bold))
                {
                    Color nameColor = achievement.IsUnlocked ? Color.White : Color.FromArgb(200, 200, 200);
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                    {

                        g.DrawString(achievement.Name, nameFont, Brushes.Black,
                            new RectangleF(iconX + iconSize + 25, achievementRect.Y + 11, achievementRect.Width - iconSize - 50, 25), sf);


                        using (SolidBrush nameBrush = new SolidBrush(nameColor))
                        {
                            g.DrawString(achievement.Name, nameFont, nameBrush,
                                new RectangleF(iconX + iconSize + 23, achievementRect.Y + 9, achievementRect.Width - iconSize - 50, 25), sf);
                        }
                    }
                }


                using (Font descFont = new Font("Comic Sans MS", 12))
                {
                    Color descColor = achievement.IsUnlocked ? Color.FromArgb(240, 240, 240) : Color.FromArgb(160, 160, 160);
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(achievement.Description, descFont, new SolidBrush(descColor),
                            new RectangleF(iconX + iconSize + 25, achievementRect.Y + 38, achievementRect.Width - iconSize - 50, 20), sf);
                    }
                }


                if (achievement.IsUnlocked)
                {
                    if (!achievement.IsClaimed)
                    {

                        int claimButtonWidth = 110;
                        int claimButtonHeight = 40;
                        int claimButtonX = achievementRect.Right - claimButtonWidth - 20;
                        int claimButtonY = achievementRect.Y + (achievementHeight - claimButtonHeight) / 2;

                        Rectangle claimButtonRect = new Rectangle(claimButtonX, claimButtonY, claimButtonWidth, claimButtonHeight);


                        using (SolidBrush buttonShadow = new SolidBrush(Color.FromArgb(10, 5, 15)))
                        {
                            g.FillRectangle(buttonShadow, new Rectangle(claimButtonRect.X + 4, claimButtonRect.Y + 4, claimButtonRect.Width, claimButtonRect.Height));
                        }


                        using (LinearGradientBrush buttonBrush = new LinearGradientBrush(
                            claimButtonRect, Color.FromArgb(255, 80, 220, 80), Color.FromArgb(255, 40, 180, 40), LinearGradientMode.Vertical))
                        {
                            g.FillRectangle(buttonBrush, claimButtonRect);
                        }


                        using (Pen buttonBorder = new Pen(Color.FromArgb(255, 60, 200, 60), 3))
                        {
                            g.DrawRectangle(buttonBorder, claimButtonRect);
                        }


                        Rectangle buttonHighlight = new Rectangle(claimButtonRect.X + 3, claimButtonRect.Y + 3, claimButtonRect.Width - 6, claimButtonRect.Height / 2);
                        using (LinearGradientBrush buttonHighlightBrush = new LinearGradientBrush(
                            buttonHighlight, Color.FromArgb(100, 255, 255, 255), Color.FromArgb(50, 200, 200, 255), LinearGradientMode.Vertical))
                        {
                            g.FillRectangle(buttonHighlightBrush, buttonHighlight);
                        }


                        using (Font buttonFont = new Font("Comic Sans MS", 13, FontStyle.Bold))
                        {
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString($"CLAIM", buttonFont, Brushes.Black, new RectangleF(claimButtonRect.X + 2, claimButtonRect.Y + 2, claimButtonRect.Width, claimButtonRect.Height), sf);
                                g.DrawString($"CLAIM", buttonFont, Brushes.White, claimButtonRect, sf);
                            }
                        }


                        int coinSize = 24;
                        int coinX = claimButtonRect.X + claimButtonRect.Width - coinSize - 12;
                        int coinY = claimButtonRect.Y + (claimButtonRect.Height - coinSize) / 2;


                        using (SolidBrush coinShadow = new SolidBrush(Color.FromArgb(100, 100, 50)))
                        {
                            g.FillEllipse(coinShadow, coinX + 3, coinY + 3, coinSize, coinSize);
                        }


                        using (SolidBrush coinBrush = new SolidBrush(Color.FromArgb(255, 215, 0)))
                        {
                            g.FillEllipse(coinBrush, coinX, coinY, coinSize, coinSize);
                        }
                        using (SolidBrush coinHighlight = new SolidBrush(Color.FromArgb(255, 255, 200)))
                        {
                            g.FillEllipse(coinHighlight, coinX + 3, coinY + 3, coinSize / 2, coinSize / 2);
                        }


                        using (Font goldFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
                        {
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString($"{achievement.GoldReward}g", goldFont, Brushes.Gold,
                                    new RectangleF(claimButtonRect.X + 15, claimButtonRect.Y + 22, claimButtonRect.Width - coinSize - 25, 18), sf);
                            }
                        }
                    }
                    else
                    {

                        using (Font statusFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
                        {
                            using (SolidBrush statusBrush = new SolidBrush(Color.FromArgb(255, 215, 0)))
                            {
                                string statusText = achievement.UnlockedAt.HasValue
                                    ? $"CLAIMED {achievement.UnlockedAt:MMM dd, yyyy}"
                                    : "CLAIMED";
                                g.DrawString(statusText, statusFont, statusBrush,
                                    new RectangleF(iconX + iconSize + 25, achievementRect.Y + 58, achievementRect.Width - iconSize - 50, 20));
                            }
                        }
                    }
                }
                else
                {
                    using (Font statusFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
                    {
                        using (SolidBrush statusBrush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                        {
                            g.DrawString("LOCKED", statusFont, statusBrush,
                                new RectangleF(iconX + iconSize + 25, achievementRect.Y + 58, achievementRect.Width - iconSize - 50, 20));
                        }
                    }
                }

                y += achievementHeight + gap;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);


            int scrollAmount = e.Delta / 10;
            targetScrollOffset += scrollAmount;

            this.Invalidate();
        }
    }
}

