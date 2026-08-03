using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;

namespace CrushIt.UI
{
    public struct Particle
    {
        public float X, Y;
        public float SpeedY, SpeedX;
        public float Size;
        public float Alpha;
        public Color Color;
        public float PulseSpeed;
    }

    public class LoadingForm : Form
    {
        private System.Windows.Forms.Timer animationTimer = null!;
        private int progressValue = 0;
        private float floatAnim = 0f;
        private bool floatUp = true;
        private int pulsePhase = 0;

        private List<Particle> particles = new List<Particle>();
        private List<Particle> bursts = new List<Particle>();
        private Random rand = new Random();

        private string currentStatusText = "READYING CANDIES...";


        private readonly IMongoCollection<UserAccount> usersCollection;
        private readonly IMongoDatabase database;

        public LoadingForm()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();


            ConfigurationHelper.Initialize();

            var client = new MongoClient(ConfigurationHelper.GetMongoConnectionString());
            this.database = client.GetDatabase(ConfigurationHelper.GetDatabaseName());
            usersCollection = database.GetCollection<UserAccount>("users");

            InitializeComponent();
            InitParticles();


            SoundHelper.StartBackgroundMusic();


            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Loading";
            this.Size = new Size(550, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 30;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        private void InitParticles()
        {
            for (int i = 0; i < 150; i++)
            {
                particles.Add(new Particle
                {
                    X = rand.Next(0, 550),
                    Y = rand.Next(0, 700),
                    SpeedY = (float)(rand.NextDouble() * -1.8 - 0.2),
                    SpeedX = (float)(rand.NextDouble() * 1.0 - 0.5),
                    Size = rand.Next(3, 10),
                    Alpha = rand.Next(60, 180),
                    Color = GetRandomCandyColor(),
                    PulseSpeed = (float)(rand.NextDouble() * 0.03 + 0.01)
                });
            }
        }

        private Color GetRandomCandyColor()
        {
            Color[] palette = {
                Color.FromArgb(255, 90, 160),
                Color.FromArgb(255, 215, 0),
                Color.FromArgb(0, 230, 180),
                Color.FromArgb(170, 90, 255),
                Color.FromArgb(255, 120, 50)
            };
            return palette[rand.Next(palette.Length)];
        }

        private async void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            if (progressValue < 100)
            {
                progressValue += 1;

                if (progressValue < 30)
                    currentStatusText = $"MIXING INGREDIENTS... {progressValue}%";
                else if (progressValue < 65)
                    currentStatusText = $"WRAPPING CHOCOLATES... {progressValue}%";
                else if (progressValue < 90)
                    currentStatusText = $"POLISHING SUGAR CUBES... {progressValue}%";
                else
                    currentStatusText = $"PREPARING GAME BOARD... {progressValue}%";


                float barWidth = 380f;
                float currentFillWidth = barWidth * (progressValue / 100f);
                float tipX = 85 + currentFillWidth;
                float tipY = 395;

                for (int i = 0; i < 5; i++)
                {
                    bursts.Add(new Particle
                    {
                        X = tipX + rand.Next(-4, 4),
                        Y = tipY + rand.Next(-6, 6),
                        SpeedX = (float)(rand.NextDouble() * 3.0 - 1.5),
                        SpeedY = (float)(rand.NextDouble() * -2.5 - 0.5),
                        Size = rand.Next(4, 9),
                        Alpha = 255,
                        Color = GetRandomCandyColor()
                    });
                }
            }
            else
            {
                animationTimer.Stop();
                this.Hide();


                string? lastUserEmail = UserSession.GetLastUserEmail();

                if (!string.IsNullOrEmpty(lastUserEmail))
                {
                    currentStatusText = "WELCOME BACK!";
                    this.Invalidate();

                    try
                    {
                        var existingUser = await usersCollection
                            .Find(u => u.Email.ToLower() == lastUserEmail.ToLower())
                            .FirstOrDefaultAsync();

                        if (existingUser != null)
                        {
                            if (existingUser.HasCompletedTutorial)
                            {
                                MainFrame main = new MainFrame(existingUser, database);
                                main.Show();
                            }
                            else
                            {
                                TutorialFrame tutorial = new TutorialFrame(existingUser);
                                tutorial.Show();
                            }
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Auto-login failed: {ex.Message}");
                    }
                }


                SignUpForm signUp = new SignUpForm();
                signUp.Show();
                return;
            }


            if (floatUp)
            {
                floatAnim += 0.12f;
                if (floatAnim >= 7f) floatUp = false;
            }
            else
            {
                floatAnim -= 0.12f;
                if (floatAnim <= -7f) floatUp = true;
            }


            pulsePhase = (pulsePhase + 1) % 120;


            if (rand.Next(0, 3) == 0)
            {
                bursts.Add(new Particle
                {
                    X = rand.Next(0, 550),
                    Y = rand.Next(0, 700),
                    SpeedX = (float)(rand.NextDouble() * 2.0 - 1.0),
                    SpeedY = (float)(rand.NextDouble() * 2.0 - 1.0),
                    Size = rand.Next(2, 6),
                    Alpha = 255,
                    Color = GetRandomCandyColor()
                });
            }


            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                p.Y += p.SpeedY;
                p.X += p.SpeedX;

                if (p.Y < -20)
                {
                    p.Y = 720;
                    p.X = rand.Next(0, 550);
                }
                particles[i] = p;
            }


            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var p = bursts[i];
                p.X += p.SpeedX;
                p.Y += p.SpeedY;
                p.Alpha -= 12;

                if (p.Alpha <= 0)
                    bursts.RemoveAt(i);
                else
                    bursts[i] = p;
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;


            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(50, 12, 70),
                Color.FromArgb(18, 6, 30),
                60f))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }


            foreach (var p in particles)
            {
                float pulseAlpha = p.Alpha + (float)(20 * Math.Sin(pulsePhase * p.PulseSpeed));
                pulseAlpha = Math.Max(30, Math.Min(255, pulseAlpha));
                using (SolidBrush pBrush = new SolidBrush(Color.FromArgb((int)pulseAlpha, p.Color)))
                {
                    g.FillEllipse(pBrush, p.X, p.Y, p.Size, p.Size);
                }
            }


            int logoY = (int)(95 + floatAnim);
            Rectangle bannerRect = new Rectangle(50, logoY, 436, 125);


            int shadowPulseAlpha = 90 + (int)(20 * Math.Sin(pulsePhase * Math.PI / 60));
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(shadowPulseAlpha, 0, 0, 0)))
            {
                g.FillRoundedRectangle(shadow, new Rectangle(55, logoY + 8, 436, 125), 26);
            }


            using (LinearGradientBrush bannerBrush = new LinearGradientBrush(
                bannerRect, Color.FromArgb(240, 255, 60, 150), Color.FromArgb(200, 160, 30, 200), LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(bannerBrush, bannerRect, 26);
            }


            using (LinearGradientBrush innerGlow = new LinearGradientBrush(
                new Rectangle(bannerRect.X + 10, bannerRect.Y + 10, bannerRect.Width - 20, bannerRect.Height - 20),
                Color.FromArgb(60, 255, 255, 255),
                Color.FromArgb(20, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(innerGlow, new Rectangle(bannerRect.X + 10, bannerRect.Y + 10, bannerRect.Width - 20, bannerRect.Height - 20), 20);
            }


            int borderPulseAlpha = 255 + (int)(30 * Math.Sin(pulsePhase * Math.PI / 60));
            borderPulseAlpha = Math.Max(200, Math.Min(255, borderPulseAlpha));
            using (Pen bannerBorder = new Pen(Color.FromArgb(borderPulseAlpha, 235, 130), 4))
            {
                g.DrawRoundedRectangle(bannerBorder, bannerRect, 26);
            }


            using (Font titleFont = new Font("Arial Black", 36, FontStyle.Bold))
            {
                string title = "CRUSH IT!";
                RectangleF textRect = new RectangleF(50, logoY + 18, 436, 80);

                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {

                    g.DrawString(title, titleFont, Brushes.Black, new RectangleF(54, logoY + 22, 436, 80), sf);


                    using (LinearGradientBrush goldBrush = new LinearGradientBrush(
                        textRect, Color.FromArgb(255, 255, 180), Color.FromArgb(255, 180, 0), LinearGradientMode.Vertical))
                    {
                        g.DrawString(title, titleFont, goldBrush, textRect, sf);
                    }
                }
            }


            using (Font subFont = new Font("Segoe UI Black", 10, FontStyle.Bold))
            {
                g.DrawString("✦ SWEET MATCH-3 PUZZLE ✦",
                    subFont,
                    Brushes.MistyRose,
                    new RectangleF(0, logoY + 138, 550, 25),
                    new StringFormat { Alignment = StringAlignment.Center });
            }


            int barX = 85, barY = 380, barWidth = 380, barHeight = 36;


            int barShadowAlpha = 100 + (int)(15 * Math.Sin(pulsePhase * Math.PI / 60));
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(barShadowAlpha, 0, 0, 0)))
            {
                g.FillRoundedRectangle(shadow, new Rectangle(barX + 3, barY + 5, barWidth, barHeight), 18);
            }


            using (LinearGradientBrush slotBrush = new LinearGradientBrush(
                new Rectangle(barX, barY, barWidth, barHeight),
                Color.FromArgb(50, 20, 60),
                Color.FromArgb(30, 10, 40),
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(slotBrush, new Rectangle(barX, barY, barWidth, barHeight), 18);
            }


            int barBorderAlpha = 180 + (int)(30 * Math.Sin(pulsePhase * Math.PI / 60));
            barBorderAlpha = Math.Max(150, Math.Min(255, barBorderAlpha));
            using (Pen barBorder = new Pen(Color.FromArgb(barBorderAlpha, 255, 105, 180), 3))
            {
                g.DrawRoundedRectangle(barBorder, new Rectangle(barX, barY, barWidth, barHeight), 18);
            }


            int currentFillWidth = (int)(barWidth * (progressValue / 100f));
            if (currentFillWidth > 18)
            {
                Rectangle fillRect = new Rectangle(barX + 3, barY + 3, currentFillWidth - 6, barHeight - 6);


                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    fillRect,
                    Color.FromArgb(255, 80, 180),
                    Color.FromArgb(255, 240, 80),
                    LinearGradientMode.Horizontal))
                {
                    g.FillRoundedRectangle(fillBrush, fillRect, 14);
                }


                using (SolidBrush gloss = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                {
                    g.FillRoundedRectangle(gloss, new Rectangle(barX + 8, barY + 5, currentFillWidth - 16, (barHeight - 10) / 2), 8);
                }


                using (LinearGradientBrush fillGlow = new LinearGradientBrush(
                    new Rectangle(fillRect.X + 4, fillRect.Y + 4, fillRect.Width - 8, fillRect.Height - 8),
                    Color.FromArgb(40, 255, 255, 255),
                    Color.FromArgb(10, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillRoundedRectangle(fillGlow, new Rectangle(fillRect.X + 4, fillRect.Y + 4, fillRect.Width - 8, fillRect.Height - 8), 10);
                }
            }


            foreach (var p in bursts)
            {
                using (SolidBrush bBrush = new SolidBrush(Color.FromArgb((int)p.Alpha, p.Color)))
                {
                    g.FillEllipse(bBrush, p.X, p.Y, p.Size, p.Size);
                }
            }


            using (Font statusFont = new Font("Arial Black", 11, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {

                g.DrawString(currentStatusText,
                    statusFont,
                    Brushes.Black,
                    new RectangleF(3, 428, 550, 30),
                    sf);


                int statusAlpha = 255 + (int)(20 * Math.Sin(pulsePhase * Math.PI / 60));
                statusAlpha = Math.Max(235, Math.Min(255, statusAlpha));
                using (SolidBrush statusBrush = new SolidBrush(Color.FromArgb(statusAlpha, 255, 255, 255)))
                {
                    g.DrawString(currentStatusText,
                        statusFont,
                        statusBrush,
                        new RectangleF(0, 425, 550, 30),
                        sf);
                }
            }


            int frameAlpha = 255 + (int)(30 * Math.Sin(pulsePhase * Math.PI / 60));
            frameAlpha = Math.Max(220, Math.Min(255, frameAlpha));
            using (Pen borderPen = new Pen(Color.FromArgb(frameAlpha, 140, 100, 200), 8))
            {
                g.DrawRectangle(borderPen, 4, 4, this.ClientSize.Width - 8, this.ClientSize.Height - 8);
            }


            using (Pen innerPen = new Pen(Color.FromArgb(150, 255, 200, 240), 2))
            {
                g.DrawRectangle(innerPen, 6, 6, this.ClientSize.Width - 12, this.ClientSize.Height - 12);
            }
        }
    }
}

