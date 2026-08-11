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
    public struct Particle
    {
        public float X, Y;
        public float SpeedY, SpeedX;
        public float Size;
        public float Alpha;
        public Color Color;
        public float PulseSpeed;
        public int Shape; // 0 = circle, 1 = star, 2 = diamond
    }

    public struct CandyOrbiter
    {
        public float Angle;
        public float Radius;
        public float Speed;
        public float Size;
        public float BaseSize;
        public Color Color;
        public int Type; // 0 = circle, 1 = star, 2 = heart
        public float Rotation;
        public float RotationSpeed;
        public float PulsePhase;
        public float PulseSpeed;
        public List<Particle> Trail;
    }

    public struct Sparkle
    {
        public float X, Y;
        public float Size;
        public float Alpha;
        public float Rotation;
        public float RotationSpeed;
    }

    public struct FloatingCandy
    {
        public float X, Y;
        public float SpeedY, SpeedX;
        public float Size;
        public float Alpha;
        public Color Color;
        public float Rotation;
        public float RotationSpeed;
        public int Type; // 0 = circle, 1 = star, 2 = heart, 3 = diamond
    }

    public struct WaveParticle
    {
        public float X, Y;
        public float BaseX, BaseY;
        public float Size;
        public float Alpha;
        public Color Color;
        public float Phase;
        public float PhaseSpeed;
        public float Amplitude;
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
        private List<CandyOrbiter> orbiters = new List<CandyOrbiter>();
        private List<Sparkle> sparkles = new List<Sparkle>();
        private List<FloatingCandy> floatingCandies = new List<FloatingCandy>();
        private List<WaveParticle> waveParticles = new List<WaveParticle>();
        private Random rand = new Random();

        private string currentStatusText = "READYING CANDIES...";


        private readonly IMongoCollection<UserAccount> usersCollection;
        private readonly IMongoDatabase database;
        private readonly IApiClient? apiClient;

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

            // Initialize API client for progress sync
            try
            {
                var config = ApiConfiguration.Default;
                ApiInitializer.Initialize(config);
                apiClient = ApiInitializer.GetApiClient();
            }
            catch
            {
                apiClient = null; // API unavailable, sync will be skipped
            }

            InitializeComponent();
            InitParticles();


            SoundManager.StartBackgroundMusic();


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
            // Increase background particles from 150 to 300
            for (int i = 0; i < 300; i++)
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
                    PulseSpeed = (float)(rand.NextDouble() * 0.03 + 0.01),
                    Shape = rand.Next(0, 3)
                });
            }

            // Increase orbiting candies from 12 to 20
            for (int i = 0; i < 20; i++)
            {
                float baseSize = rand.Next(15, 28);
                var orbiter = new CandyOrbiter
                {
                    Angle = (float)(i * Math.PI / 10),
                    Radius = 200 + rand.Next(-30, 30),
                    Speed = (float)(rand.NextDouble() * 0.025 + 0.015) * (rand.Next(0, 2) == 0 ? 1 : -1),
                    Size = baseSize,
                    BaseSize = baseSize,
                    Color = GetRandomCandyColor(),
                    Type = rand.Next(0, 3),
                    Rotation = rand.Next(0, 360),
                    RotationSpeed = (float)(rand.NextDouble() * 3 - 1.5),
                    PulsePhase = rand.Next(0, 100),
                    PulseSpeed = (float)(rand.NextDouble() * 0.05 + 0.02),
                    Trail = new List<Particle>()
                };
                orbiters.Add(orbiter);
            }

            // Increase sparkles from 15 to 40
            int screenWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int screenHeight = Screen.PrimaryScreen.WorkingArea.Height;
            
            for (int i = 0; i < 40; i++)
            {
                sparkles.Add(new Sparkle
                {
                    X = rand.Next(50, screenWidth - 50),
                    Y = rand.Next(100, screenHeight - 100),
                    Size = rand.Next(8, 15),
                    Alpha = rand.Next(100, 200),
                    Rotation = rand.Next(0, 360),
                    RotationSpeed = (float)(rand.NextDouble() * 2 - 1)
                });
            }

            // Add new floating candies
            for (int i = 0; i < 50; i++)
            {
                floatingCandies.Add(new FloatingCandy
                {
                    X = rand.Next(0, 550),
                    Y = rand.Next(0, 700),
                    SpeedY = (float)(rand.NextDouble() * -1.5 - 0.3),
                    SpeedX = (float)(rand.NextDouble() * 0.8 - 0.4),
                    Size = rand.Next(8, 20),
                    Alpha = rand.Next(80, 200),
                    Color = GetRandomCandyColor(),
                    Rotation = rand.Next(0, 360),
                    RotationSpeed = (float)(rand.NextDouble() * 4 - 2),
                    Type = rand.Next(0, 4)
                });
            }

            // Add new wave particles
            for (int i = 0; i < 80; i++)
            {
                float baseX = rand.Next(0, 550);
                float baseY = rand.Next(0, 700);
                waveParticles.Add(new WaveParticle
                {
                    X = baseX,
                    Y = baseY,
                    BaseX = baseX,
                    BaseY = baseY,
                    Size = rand.Next(4, 12),
                    Alpha = rand.Next(50, 150),
                    Color = GetRandomCandyColor(),
                    Phase = rand.Next(0, 100),
                    PhaseSpeed = (float)(rand.NextDouble() * 0.05 + 0.02),
                    Amplitude = rand.Next(10, 30)
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

        private void DrawPixelCandy(Graphics g, CandyType candy, int x, int y, int size)
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

            using (SolidBrush b = new SolidBrush(Color.Black))
                g.FillRectangle(b, x, y, size, size);

            Rectangle inner = new Rectangle(x + 2, y + 2, size - 4, size - 4);
            using (SolidBrush b = new SolidBrush(mainColor))
                g.FillRectangle(b, inner);

            using (SolidBrush b = new SolidBrush(lightColor))
            {
                g.FillRectangle(b, inner.X, inner.Y, inner.Width, 3);
                g.FillRectangle(b, inner.X, inner.Y, 3, inner.Height);
            }

            using (SolidBrush b = new SolidBrush(darkColor))
            {
                g.FillRectangle(b, inner.X, inner.Y + inner.Height - 3, inner.Width, 3);
                g.FillRectangle(b, inner.X + inner.Width - 3, inner.Y, 3, inner.Height);
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
                g.FillRectangle(leaf, cx - 5, cy - 8, 10, 2);
                g.FillRectangle(leaf, cx - 7, cy - 7, 3, 2);
                g.FillRectangle(leaf, cx + 4, cy - 7, 3, 2);
            }

            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 75, 100)))
            {
                g.FillRectangle(body, cx - 6, cy - 5, 12, 6);
                g.FillRectangle(body, cx - 4, cy + 1, 8, 4);
                g.FillRectangle(body, cx - 2, cy + 5, 4, 3);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(160, 20, 45)))
            {
                g.FillRectangle(shadow, cx + 4, cy - 4, 2, 4);
                g.FillRectangle(shadow, cx + 2, cy + 1, 2, 3);
                g.FillRectangle(shadow, cx, cy + 5, 2, 2);
            }

            using (SolidBrush seed = new SolidBrush(Color.FromArgb(255, 240, 120)))
            {
                g.FillRectangle(seed, cx - 3, cy - 3, 2, 2);
                g.FillRectangle(seed, cx + 1, cy - 3, 2, 2);
                g.FillRectangle(seed, cx - 1, cy, 2, 2);
                g.FillRectangle(seed, cx - 2, cy + 3, 2, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.White))
            {
                g.FillRectangle(gloss, cx - 5, cy - 4, 2, 2);
            }
        }

        private void DrawPixelBlueGummy(Graphics g, int cx, int cy)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(100, 210, 255)))
            {
                g.FillRectangle(body, cx - 3, cy - 8, 6, 2);
                g.FillRectangle(body, cx - 6, cy - 5, 12, 4);
                g.FillRectangle(body, cx - 8, cy - 1, 16, 5);
                g.FillRectangle(body, cx - 6, cy + 4, 12, 4);
                g.FillRectangle(body, cx - 3, cy + 7, 6, 2);
            }

            using (SolidBrush dark = new SolidBrush(Color.FromArgb(0, 80, 175)))
            {
                g.FillRectangle(dark, cx + 3, cy - 5, 3, 3);
                g.FillRectangle(dark, cx + 5, cy - 1, 3, 5);
                g.FillRectangle(dark, cx + 3, cy + 4, 3, 3);
                g.FillRectangle(dark, cx - 1, cy + 7, 5, 2);
            }

            using (SolidBrush shine = new SolidBrush(Color.FromArgb(220, 250, 255)))
            {
                g.FillRectangle(shine, cx - 5, cy - 4, 3, 3);
                g.FillRectangle(shine, cx - 6, cy, 3, 3);
                g.FillRectangle(shine, cx - 5, cy + 4, 2, 2);
            }
        }

        private void DrawPixelGreenApple(Graphics g, int cx, int cy)
        {
            using (SolidBrush stem = new SolidBrush(Color.FromArgb(120, 75, 30)))
            {
                g.FillRectangle(stem, cx - 1, cy - 9, 2, 3);
            }

            using (SolidBrush leaf = new SolidBrush(Color.FromArgb(110, 235, 60)))
            {
                g.FillRectangle(leaf, cx + 1, cy - 9, 3, 2);
            }

            using (SolidBrush body = new SolidBrush(Color.FromArgb(110, 230, 60)))
            {
                g.FillRectangle(body, cx - 7, cy - 5, 14, 9);
                g.FillRectangle(body, cx - 5, cy + 4, 10, 4);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(20, 110, 35)))
            {
                g.FillRectangle(shadow, cx + 4, cy - 4, 3, 8);
                g.FillRectangle(shadow, cx + 2, cy + 4, 3, 3);
                g.FillRectangle(shadow, cx - 1, cy + 6, 3, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.White))
            {
                g.FillRectangle(gloss, cx - 5, cy - 4, 2, 4);
            }
        }

        private void DrawPixelYellowLemon(Graphics g, int cx, int cy)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 240, 80)))
            {
                g.FillRectangle(body, cx - 2, cy - 8, 4, 2);
                g.FillRectangle(body, cx - 5, cy - 5, 10, 3);
                g.FillRectangle(body, cx - 7, cy - 2, 14, 5);
                g.FillRectangle(body, cx - 5, cy + 3, 10, 3);
                g.FillRectangle(body, cx - 2, cy + 6, 4, 2);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(190, 130, 0)))
            {
                g.FillRectangle(shadow, cx + 3, cy - 2, 4, 5);
                g.FillRectangle(shadow, cx + 1, cy + 3, 4, 2);
                g.FillRectangle(shadow, cx - 1, cy + 6, 2, 2);
            }

            using (SolidBrush line = new SolidBrush(Color.FromArgb(255, 180, 20)))
            {
                g.FillRectangle(line, cx - 4, cy, 8, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.White))
            {
                g.FillRectangle(gloss, cx - 4, cy - 4, 2, 2);
            }
        }

        private void DrawPixelPurplePlum(Graphics g, int cx, int cy)
        {
            using (SolidBrush body = new SolidBrush(Color.FromArgb(160, 60, 200)))
            {
                g.FillRectangle(body, cx - 5, cy - 7, 10, 2);
                g.FillRectangle(body, cx - 7, cy - 4, 14, 9);
                g.FillRectangle(body, cx - 5, cy + 5, 10, 2);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 15, 130)))
            {
                g.FillRectangle(shadow, cx + 4, cy - 3, 3, 8);
                g.FillRectangle(shadow, cx + 1, cy + 5, 3, 2);
            }

            using (SolidBrush swirl = new SolidBrush(Color.White))
            {
                g.FillRectangle(swirl, cx - 4, cy - 3, 2, 2);
                g.FillRectangle(swirl, cx - 1, cy - 1, 3, 3);
                g.FillRectangle(swirl, cx + 1, cy + 1, 2, 2);
            }

            using (SolidBrush gloss = new SolidBrush(Color.FromArgb(240, 200, 255)))
            {
                g.FillRectangle(gloss, cx - 5, cy - 5, 4, 2);
            }
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
                        Color = GetRandomCandyColor(),
                        Shape = rand.Next(0, 3)
                    });
                }
            }
            else
            {
                animationTimer.Stop();
                this.Hide(); // Hide instead of closing/disposing

                string? lastUserEmail = UserSession.GetLastUserEmail();
                System.Diagnostics.Debug.WriteLine($"Last user email: {lastUserEmail}");

                if (!string.IsNullOrEmpty(lastUserEmail))
                {
                    currentStatusText = "WELCOME BACK!";
                    this.Invalidate();

                    try
                    {
                        var existingUser = await usersCollection
                            .Find(u => u.Email.ToLower() == lastUserEmail.ToLower())
                            .FirstOrDefaultAsync();

                        System.Diagnostics.Debug.WriteLine($"Existing user found: {existingUser != null}");

                        if (existingUser != null)
                        {
                            // Sync progress with server
                            await ProgressSyncService.SyncOnLaunchAsync(existingUser, database, apiClient);

                            Form nextForm;
                            if (existingUser.HasCompletedTutorial)
                            {
                                System.Diagnostics.Debug.WriteLine("Opening MainFrame");
                                nextForm = new MainFrame(existingUser, database);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("Opening TutorialFrame");
                                nextForm = new TutorialFrame(existingUser);
                            }
                            nextForm.Show();
                            // Don't dispose - keep LoadingForm as main form
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Auto-login failed: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("Opening SignUpForm");
                Form signUp = new SignUpForm();
                signUp.Show();
                // Don't dispose - keep LoadingForm as main form
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

            // Update orbiters
            for (int i = 0; i < orbiters.Count; i++)
            {
                var orb = orbiters[i];
                orb.Angle += orb.Speed;
                orb.Rotation += orb.RotationSpeed;
                orb.PulsePhase += orb.PulseSpeed;
                
                // Pulsing size effect
                float pulseScale = 1.0f + (float)(0.3 * Math.Sin(orb.PulsePhase));
                orb.Size = orb.BaseSize * pulseScale;
                
                // Add trail particles
                if (rand.Next(0, 4) == 0)
                {
                    int centerX = 275;
                    int centerY = (int)(158 + floatAnim);
                    float orbX = centerX + (float)(Math.Cos(orb.Angle) * orb.Radius);
                    float orbY = centerY + (float)(Math.Sin(orb.Angle) * orb.Radius);
                    
                    orb.Trail.Add(new Particle
                    {
                        X = orbX,
                        Y = orbY,
                        SpeedX = (float)(rand.NextDouble() * 0.5 - 0.25),
                        SpeedY = (float)(rand.NextDouble() * 0.5 - 0.25),
                        Size = orb.Size * 0.3f,
                        Alpha = 180,
                        Color = orb.Color,
                        PulseSpeed = 0.01f,
                        Shape = 0
                    });
                }
                
                // Update trail particles
                for (int j = orb.Trail.Count - 1; j >= 0; j--)
                {
                    var trail = orb.Trail[j];
                    trail.Alpha -= 8;
                    trail.Size *= 0.95f;
                    if (trail.Alpha <= 0)
                        orb.Trail.RemoveAt(j);
                    else
                        orb.Trail[j] = trail;
                }
                
                orbiters[i] = orb;
            }

            // Update sparkles
            for (int i = 0; i < sparkles.Count; i++)
            {
                var s = sparkles[i];
                s.Rotation += s.RotationSpeed;
                s.Alpha += (float)(Math.Sin(pulsePhase * 0.1) * 2);
                s.Alpha = Math.Max(50, Math.Min(255, s.Alpha));
                sparkles[i] = s;
            }


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
                    Color = GetRandomCandyColor(),
                    Shape = rand.Next(0, 3)
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

            // Update floating candies
            for (int i = 0; i < floatingCandies.Count; i++)
            {
                var fc = floatingCandies[i];
                fc.Y += fc.SpeedY;
                fc.X += fc.SpeedX;
                fc.Rotation += fc.RotationSpeed;

                if (fc.Y < -30)
                {
                    fc.Y = 730;
                    fc.X = rand.Next(0, 550);
                }
                if (fc.X < -30) fc.X = 580;
                if (fc.X > 580) fc.X = -30;
                floatingCandies[i] = fc;
            }

            // Update wave particles
            for (int i = 0; i < waveParticles.Count; i++)
            {
                var wp = waveParticles[i];
                wp.Phase += wp.PhaseSpeed;
                wp.X = wp.BaseX + (float)(Math.Sin(wp.Phase) * wp.Amplitude);
                wp.Y = wp.BaseY + (float)(Math.Cos(wp.Phase * 0.7f) * wp.Amplitude * 0.5f);
                waveParticles[i] = wp;
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
                    if (p.Shape == 0) // Circle
                    {
                        g.FillEllipse(pBrush, p.X, p.Y, p.Size, p.Size);
                    }
                    else if (p.Shape == 1) // Star
                    {
                        DrawStar(g, p.X + p.Size / 2, p.Y + p.Size / 2, p.Size / 2, p.Size / 4, 5, pBrush);
                    }
                    else // Diamond
                    {
                        PointF[] diamond = new PointF[]
                        {
                            new PointF(p.X + p.Size / 2, p.Y),
                            new PointF(p.X + p.Size, p.Y + p.Size / 2),
                            new PointF(p.X + p.Size / 2, p.Y + p.Size),
                            new PointF(p.X, p.Y + p.Size / 2)
                        };
                        g.FillPolygon(pBrush, diamond);
                    }
                }
            }

            // Draw sparkles
            foreach (var s in sparkles)
            {
                using (SolidBrush sparkleBrush = new SolidBrush(Color.FromArgb((int)s.Alpha, 255, 255, 200)))
                {
                    DrawStar(g, s.X, s.Y, s.Size / 2, s.Size / 4, 4, sparkleBrush);
                }
            }

            // Draw floating candies
            foreach (var fc in floatingCandies)
            {
                g.TranslateTransform(fc.X, fc.Y);
                g.RotateTransform(fc.Rotation);
                
                using (SolidBrush fcBrush = new SolidBrush(Color.FromArgb((int)fc.Alpha, fc.Color)))
                {
                    if (fc.Type == 0) // Circle
                    {
                        g.FillEllipse(fcBrush, -fc.Size / 2, -fc.Size / 2, fc.Size, fc.Size);
                    }
                    else if (fc.Type == 1) // Star
                    {
                        DrawStar(g, 0, 0, fc.Size / 2, fc.Size / 4, 5, fcBrush);
                    }
                    else if (fc.Type == 2) // Heart
                    {
                        DrawHeart(g, 0, 0, fc.Size, fcBrush);
                    }
                    else // Diamond
                    {
                        PointF[] diamond = new PointF[]
                        {
                            new PointF(0, -fc.Size / 2),
                            new PointF(fc.Size / 2, 0),
                            new PointF(0, fc.Size / 2),
                            new PointF(-fc.Size / 2, 0)
                        };
                        g.FillPolygon(fcBrush, diamond);
                    }
                }
                
                g.ResetTransform();
            }

            // Draw wave particles
            foreach (var wp in waveParticles)
            {
                float waveAlpha = wp.Alpha + (float)(30 * Math.Sin(pulsePhase * 0.05 + wp.Phase));
                waveAlpha = Math.Max(30, Math.Min(255, waveAlpha));
                
                using (SolidBrush wpBrush = new SolidBrush(Color.FromArgb((int)waveAlpha, wp.Color)))
                {
                    g.FillEllipse(wpBrush, wp.X - wp.Size / 2, wp.Y - wp.Size / 2, wp.Size, wp.Size);
                }
            }

            // Draw orbiting candies around the title
            int centerX = 275;
            int centerY = (int)(158 + floatAnim);
            foreach (var orb in orbiters)
            {
                float orbX = centerX + (float)(Math.Cos(orb.Angle) * orb.Radius);
                float orbY = centerY + (float)(Math.Sin(orb.Angle) * orb.Radius);
                
                // Draw trail particles
                foreach (var trail in orb.Trail)
                {
                    using (SolidBrush trailBrush = new SolidBrush(Color.FromArgb((int)trail.Alpha, trail.Color)))
                    {
                        g.FillEllipse(trailBrush, trail.X, trail.Y, trail.Size, trail.Size);
                    }
                }
                
                // Draw main orbiter with rotation
                g.TranslateTransform(orbX, orbY);
                g.RotateTransform(orb.Rotation);
                
                using (Brush orbBrush = new SolidBrush(Color.FromArgb(255, orb.Color)))
                {
                    if (orb.Type == 0) // Circle with inner detail
                    {
                        g.FillEllipse(orbBrush, -orb.Size / 2, -orb.Size / 2, orb.Size, orb.Size);
                        // Add inner highlight
                        using (Brush highlight = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                        {
                            g.FillEllipse(highlight, -orb.Size / 4, -orb.Size / 4, orb.Size / 2, orb.Size / 2);
                        }
                    }
                    else if (orb.Type == 1) // Star
                    {
                        DrawStar(g, 0, 0, orb.Size / 2, orb.Size / 4, 5, orbBrush);
                    }
                    else // Heart
                    {
                        using (SolidBrush heartBrush = new SolidBrush(orb.Color))
                        {
                            DrawHeart(g, 0, 0, orb.Size, heartBrush);
                        }
                    }
                }
                
                g.ResetTransform();
                
                // Add glow effect with pulsing
                float glowSize = orb.Size * 1.5f;
                float glowAlpha = 60 + (float)(30 * Math.Sin(pulsePhase * 0.1 + orb.PulsePhase));
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb((int)glowAlpha, orb.Color)))
                {
                    g.FillEllipse(glowBrush, orbX - glowSize / 2, orbY - glowSize / 2, glowSize, glowSize);
                }
                
                // Add sparkles around orbiter
                if (rand.Next(0, 8) == 0)
                {
                    float sparkleX = orbX + (float)(rand.NextDouble() * orb.Size - orb.Size / 2);
                    float sparkleY = orbY + (float)(rand.NextDouble() * orb.Size - orb.Size / 2);
                    using (Brush sparkleBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 200)))
                    {
                        DrawStar(g, sparkleX, sparkleY, 4, 2, 4, sparkleBrush);
                    }
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

                // Draw pixelated candies in the progress bar
                int candySize = 28;
                int candySpacing = 32;
                int candyCount = currentFillWidth / candySpacing;
                
                // Fill the entire progress bar area with a colorful background
                Rectangle fullFillRect = new Rectangle(barX + 3, barY + 3, currentFillWidth - 6, barHeight - 6);
                using (LinearGradientBrush bgFill = new LinearGradientBrush(
                    fullFillRect, 
                    Color.FromArgb(255, 80, 180), 
                    Color.FromArgb(255, 240, 80), 
                    LinearGradientMode.Horizontal))
                {
                    g.FillRoundedRectangle(bgFill, fullFillRect, 18);
                }
                
                // Draw segment dividers for candy bar effect
                int segmentWidth = 30;
                int segmentCount = currentFillWidth / segmentWidth;
                
                for (int i = 0; i < segmentCount; i++)
                {
                    int segX = barX + 8 + i * segmentWidth;
                    int segWidth = Math.Min(segmentWidth - 2, currentFillWidth - 16 - i * segmentWidth);
                    if (segWidth <= 0) break;
                    
                    Rectangle segRect = new Rectangle(segX, barY + 3, segWidth, barHeight - 6);
                    
                    Color segColor = (i % 2 == 0) ? 
                        Color.FromArgb(255, 80, 180) : 
                        Color.FromArgb(255, 240, 80);
                    
                    using (SolidBrush segBrush = new SolidBrush(segColor))
                    {
                        g.FillRoundedRectangle(segBrush, segRect, 8);
                    }
                }
                
                // Switch to pixelated rendering for candies
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                
                for (int i = 0; i < candyCount; i++)
                {
                    int candyX = barX + 8 + i * candySpacing;
                    int candyY = barY + 4;
                    
                    CandyType candyType = (CandyType)(i % 5);
                    DrawPixelCandy(g, candyType, candyX, candyY, candySize);
                }
                
                // Switch back to anti-alias for other elements
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Draw gloss effect
                using (SolidBrush gloss = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                {
                    g.FillRoundedRectangle(gloss, new Rectangle(barX + 8, barY + 5, currentFillWidth - 16, (barHeight - 10) / 2), 8);
                }

                // Draw glow effect at the leading edge
                if (currentFillWidth > 20)
                {
                    int glowX = barX + currentFillWidth - 20;
                    using (LinearGradientBrush glowBrush = new LinearGradientBrush(
                        new Rectangle(glowX, barY, 30, barHeight),
                        Color.FromArgb(150, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRoundedRectangle(glowBrush, new Rectangle(glowX, barY + 3, 25, barHeight - 6), 8);
                    }
                }
            }


            foreach (var p in bursts)
            {
                using (SolidBrush burstBrush = new SolidBrush(Color.FromArgb((int)p.Alpha, p.Color)))
                {
                    if (p.Shape == 0) // Circle
                    {
                        g.FillEllipse(burstBrush, p.X, p.Y, p.Size, p.Size);
                    }
                    else if (p.Shape == 1) // Star
                    {
                        DrawStar(g, p.X + p.Size / 2, p.Y + p.Size / 2, p.Size / 2, p.Size / 4, 5, burstBrush);
                    }
                    else // Diamond
                    {
                        PointF[] diamond = new PointF[]
                        {
                            new PointF(p.X + p.Size / 2, p.Y),
                            new PointF(p.X + p.Size, p.Y + p.Size / 2),
                            new PointF(p.X + p.Size / 2, p.Y + p.Size),
                            new PointF(p.X, p.Y + p.Size / 2)
                        };
                        g.FillPolygon(burstBrush, diamond);
                    }
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

            // Draw corner decorations
            DrawCornerDecorations(g);
        }

        private void DrawCornerDecorations(Graphics g)
        {
            Color[] cornerColors = {
                Color.FromArgb(255, 90, 160),
                Color.FromArgb(255, 215, 0),
                Color.FromArgb(0, 230, 180),
                Color.FromArgb(170, 90, 255)
            };

            int cornerSize = 25;
            int cornerOffset = 15;

            // Top-left
            using (SolidBrush tlBrush = new SolidBrush(Color.FromArgb(200, cornerColors[0])))
            {
                DrawStar(g, cornerOffset, cornerOffset, cornerSize / 2, cornerSize / 4, 5, tlBrush);
            }

            // Top-right
            using (SolidBrush trBrush = new SolidBrush(Color.FromArgb(200, cornerColors[1])))
            {
                DrawStar(g, this.ClientSize.Width - cornerOffset, cornerOffset, cornerSize / 2, cornerSize / 4, 5, trBrush);
            }

            // Bottom-left
            using (SolidBrush blBrush = new SolidBrush(Color.FromArgb(200, cornerColors[2])))
            {
                DrawStar(g, cornerOffset, this.ClientSize.Height - cornerOffset, cornerSize / 2, cornerSize / 4, 5, blBrush);
            }

            // Bottom-right
            using (SolidBrush brBrush = new SolidBrush(Color.FromArgb(200, cornerColors[3])))
            {
                DrawStar(g, this.ClientSize.Width - cornerOffset, this.ClientSize.Height - cornerOffset, cornerSize / 2, cornerSize / 4, 5, brBrush);
            }
        }

        private void DrawStar(Graphics g, float cx, float cy, float outerRadius, float innerRadius, int points, Brush brush)
        {
            PointF[] starPoints = new PointF[points * 2];
            double angle = -Math.PI / 2;
            double step = Math.PI / points;

            for (int i = 0; i < points * 2; i++)
            {
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                starPoints[i] = new PointF(
                    cx + (float)(Math.Cos(angle) * radius),
                    cy + (float)(Math.Sin(angle) * radius)
                );
                angle += step;
            }

            g.FillPolygon(brush, starPoints);
        }

        private void DrawHeart(Graphics g, float cx, float cy, float size, Brush brush)
        {
            float scale = size / 20f;
            PointF[] heartPoints = new PointF[]
            {
                new PointF(cx, cy - 5 * scale),
                new PointF(cx + 5 * scale, cy - 10 * scale),
                new PointF(cx + 10 * scale, cy - 5 * scale),
                new PointF(cx + 10 * scale, cy),
                new PointF(cx + 5 * scale, cy + 5 * scale),
                new PointF(cx, cy),
                new PointF(cx - 5 * scale, cy + 5 * scale),
                new PointF(cx - 10 * scale, cy),
                new PointF(cx - 10 * scale, cy - 5 * scale),
                new PointF(cx - 5 * scale, cy - 10 * scale)
            };

            g.FillPolygon(brush, heartPoints);
        }
    }
}

