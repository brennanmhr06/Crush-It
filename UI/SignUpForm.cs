using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;
using CrushIt.API;
using CrushIt.UI;

namespace CrushIt.UI
{
    public class ConfettiParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float SpeedX { get; set; }
        public float SpeedY { get; set; }
        public float Rotation { get; set; }
        public float RotationSpeed { get; set; }
        public Color Color { get; set; }
        public int Size { get; set; }
        public float Alpha { get; set; }
    }

    public class SignUpForm : Form
    {
        private readonly IMongoCollection<UserAccount> usersCollection;
        private readonly IMongoDatabase database;
        private readonly IApiClient? apiClient;

        private System.Windows.Forms.Timer animationTimer = null!;
        private int pulsePhase = 0;
        private readonly Random particleRand = new Random();
        private readonly List<StyleParticle> backgroundParticles = new List<StyleParticle>();

        private string usernameInput = "";
        private string emailInput = "";
        private string passwordInput = "";
        private string statusMessage = "";
        private Color statusColor = Color.White;
        private bool isProcessing = false;
        private Rectangle usernameRect;
        private Rectangle emailRect;
        private Rectangle passwordRect;
        private Rectangle togglePasswordRect;
        private Rectangle buttonRect;
        private Rectangle titleRect;
        private Rectangle subtitleRect;
        private Rectangle panelRect;
        private bool isUsernameFocused = false;
        private bool isEmailFocused = false;
        private bool isPasswordFocused = false;
        private bool isButtonHovered = false;
        private bool isToggleHovered = false;
        private bool showPassword = false;
        private float usernameFocusAlpha = 0f;
        private float emailFocusAlpha = 0f;
        private float passwordFocusAlpha = 0f;
        private float buttonScale = 1f;
        private float buttonPressDepth = 0f;
        private int shakeIntensity = 0;
        private int shakePhase = 0;
        private float loadingRotation = 0f;
        private bool showSuccessAnimation = false;
        private int successAnimationPhase = 0;
        private readonly List<ConfettiParticle> confettiParticles = new List<ConfettiParticle>();
        private float usernameLabelY = 0f;
        private float emailLabelY = 0f;
        private float passwordLabelY = 0f;

        public SignUpForm()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();


            ConfigurationHelper.Initialize();

            var client = new MongoClient(ConfigurationHelper.GetMongoConnectionString());
            this.database = client.GetDatabase(ConfigurationHelper.GetDatabaseName());
            usersCollection = database.GetCollection<UserAccount>("users");


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
                apiClient = null;
            }

            InitializeComponent();
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 35, 550, 80, 480));
            
            // Handle application lifecycle
            this.FormClosed += (s, e) => {
                if (Application.OpenForms.Count == 0)
                {
                    Application.Exit();
                }
            };
            
            // Initialize floating label positions
            usernameLabelY = usernameRect.Y - 22;
            emailLabelY = emailRect.Y - 22;
            passwordLabelY = passwordRect.Y - 22;
            
            StartAnimation();


            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Account";
            this.Size = new Size(580, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += SignUpForm_KeyDown;
            this.MouseClick += SignUpForm_MouseClick;
            this.MouseMove += SignUpForm_MouseMove;
            this.MouseLeave += (s, e) => { isButtonHovered = false; isToggleHovered = false; this.Cursor = Cursors.Default; this.Invalidate(); };

            int centerX = 290;
            titleRect = new Rectangle(centerX - 180, 25, 360, 50);
            subtitleRect = new Rectangle(centerX - 180, 80, 360, 30);
            panelRect = new Rectangle(centerX - 190, 120, 380, 380);
            
            usernameRect = new Rectangle(centerX - 150, 160, 300, 50);
            emailRect = new Rectangle(centerX - 150, 245, 300, 50);
            passwordRect = new Rectangle(centerX - 150, 330, 260, 50);
            togglePasswordRect = new Rectangle(passwordRect.Right + 15, passwordRect.Y + 12, 35, 28);
            buttonRect = new Rectangle(centerX - 120, 445, 240, 60);

            this.FormClosed += (s, e) => animationTimer?.Stop();
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
            
            // Animate focus states
            float targetAlpha = isUsernameFocused ? 1f : 0f;
            usernameFocusAlpha += (targetAlpha - usernameFocusAlpha) * 0.15f;
            
            targetAlpha = isEmailFocused ? 1f : 0f;
            emailFocusAlpha += (targetAlpha - emailFocusAlpha) * 0.15f;
            
            targetAlpha = isPasswordFocused ? 1f : 0f;
            passwordFocusAlpha += (targetAlpha - passwordFocusAlpha) * 0.15f;
            
            // Animate button scale
            float targetScale = isButtonHovered ? 1.05f : 1f;
            buttonScale += (targetScale - buttonScale) * 0.1f;
            
            // Animate button press depth
            float targetDepth = isButtonHovered ? 3f : 0f;
            buttonPressDepth += (targetDepth - buttonPressDepth) * 0.2f;
            
            // Shake animation for errors
            if (shakeIntensity > 0)
            {
                shakePhase += 8;
                shakeIntensity -= 1;
                if (shakeIntensity < 0) shakeIntensity = 0;
            }
            
            // Loading spinner rotation
            if (isProcessing)
            {
                loadingRotation += 0.15f;
            }
            
            // Floating label animation
            AnimateFloatingLabels();
            
            // Success confetti animation
            if (showSuccessAnimation)
            {
                successAnimationPhase++;
                UpdateConfetti();
                if (successAnimationPhase > 180)
                {
                    showSuccessAnimation = false;
                    confettiParticles.Clear();
                }
            }
            
            this.Invalidate();
        }
        
        private void AnimateFloatingLabels()
        {
            float targetUsernameY = (isUsernameFocused || !string.IsNullOrEmpty(usernameInput)) ? usernameRect.Y - 30 : usernameRect.Y - 22;
            usernameLabelY += (targetUsernameY - usernameLabelY) * 0.12f;
            
            float targetEmailY = (isEmailFocused || !string.IsNullOrEmpty(emailInput)) ? emailRect.Y - 30 : emailRect.Y - 22;
            emailLabelY += (targetEmailY - emailLabelY) * 0.12f;
            
            float targetPasswordY = (isPasswordFocused || !string.IsNullOrEmpty(passwordInput)) ? passwordRect.Y - 30 : passwordRect.Y - 22;
            passwordLabelY += (targetPasswordY - passwordLabelY) * 0.12f;
        }
        
        private void UpdateConfetti()
        {
            foreach (var confetti in confettiParticles)
            {
                confetti.X += confetti.SpeedX;
                confetti.Y += confetti.SpeedY;
                confetti.SpeedY += 0.15f; // Gravity
                confetti.Rotation += confetti.RotationSpeed;
                confetti.Alpha -= 0.008f;
                confetti.Alpha = Math.Max(0, confetti.Alpha);
            }
            
            confettiParticles.RemoveAll(c => c.Alpha <= 0 || c.Y > this.ClientSize.Height);
        }
        
        private void TriggerShakeAnimation()
        {
            shakeIntensity = 15;
            shakePhase = 0;
        }
        
        private void TriggerSuccessAnimation()
        {
            showSuccessAnimation = true;
            successAnimationPhase = 0;
            confettiParticles.Clear();
            
            for (int i = 0; i < 50; i++)
            {
                confettiParticles.Add(new ConfettiParticle
                {
                    X = this.ClientSize.Width / 2,
                    Y = this.ClientSize.Height / 2,
                    SpeedX = (float)(particleRand.NextDouble() * 10 - 5),
                    SpeedY = (float)(particleRand.NextDouble() * -15 - 5),
                    Rotation = 0,
                    RotationSpeed = (float)(particleRand.NextDouble() * 0.3 - 0.15),
                    Color = CrushItStyleHelper.ParticleColors[particleRand.Next(CrushItStyleHelper.ParticleColors.Length)],
                    Size = particleRand.Next(6, 12),
                    Alpha = 1f
                });
            }
        }


        private void SignUpForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
            else if (e.KeyCode == Keys.Enter && !isProcessing)
            {
                ProcessSignUp();
            }
        }

        private void SignUpForm_MouseClick(object? sender, MouseEventArgs e)
        {
            if (isProcessing) return;

            if (togglePasswordRect.Contains(e.Location))
            {
                showPassword = !showPassword;
                this.Invalidate();
                return;
            }
            else if (usernameRect.Contains(e.Location))
            {
                isUsernameFocused = true;
                isEmailFocused = false;
                isPasswordFocused = false;
            }
            else if (emailRect.Contains(e.Location))
            {
                isUsernameFocused = false;
                isEmailFocused = true;
                isPasswordFocused = false;
            }
            else if (passwordRect.Contains(e.Location))
            {
                isUsernameFocused = false;
                isEmailFocused = false;
                isPasswordFocused = true;
            }
            else if (buttonRect.Contains(e.Location))
            {
                ProcessSignUp();
                return;
            }
            else
            {
                isUsernameFocused = false;
                isEmailFocused = false;
                isPasswordFocused = false;
            }
            this.Invalidate();
        }

        private void SignUpForm_MouseMove(object? sender, MouseEventArgs e)
        {
            bool wasButtonHovered = isButtonHovered;
            bool wasToggleHovered = isToggleHovered;
            isButtonHovered = buttonRect.Contains(e.Location);
            isToggleHovered = togglePasswordRect.Contains(e.Location);
            
            this.Cursor = isToggleHovered ? Cursors.Hand : Cursors.Default;
            
            if (wasButtonHovered != isButtonHovered || wasToggleHovered != isToggleHovered)
                this.Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (isProcessing) return;
            if (e.KeyChar == (char)Keys.Back)
            {
                if (isUsernameFocused && usernameInput.Length > 0)
                    usernameInput = usernameInput.Substring(0, usernameInput.Length - 1);
                else if (isEmailFocused && emailInput.Length > 0)
                    emailInput = emailInput.Substring(0, emailInput.Length - 1);
                else if (isPasswordFocused && passwordInput.Length > 0)
                    passwordInput = passwordInput.Substring(0, passwordInput.Length - 1);
            }
            else if (!char.IsControl(e.KeyChar))
            {
                if (isUsernameFocused && usernameInput.Length < 20)
                    usernameInput += e.KeyChar;
                else if (isEmailFocused && emailInput.Length < 50)
                    emailInput += e.KeyChar;
                else if (isPasswordFocused && passwordInput.Length < 30)
                    passwordInput += e.KeyChar;
            }
            this.Invalidate();
        }

        private async void ProcessSignUp()
        {
            string username = usernameInput.Trim();
            string email = emailInput.Trim();
            string password = passwordInput;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                statusMessage = "Please fill in all details!";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            if (username.Length < 3)
            {
                statusMessage = "Username must be at least 3 characters.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                statusMessage = "Enter a valid email address.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }


            if (password.Length < 8)
            {
                statusMessage = "Password must be at least 8 characters.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            bool hasUpperCase = false;
            bool hasLowerCase = false;
            bool hasDigit = false;
            bool hasSpecialChar = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpperCase = true;
                else if (char.IsLower(c)) hasLowerCase = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecialChar = true;
            }

            if (!hasUpperCase)
            {
                statusMessage = "Password must contain an uppercase letter.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            if (!hasLowerCase)
            {
                statusMessage = "Password must contain a lowercase letter.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            if (!hasDigit)
            {
                statusMessage = "Password must contain a number.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            if (!hasSpecialChar)
            {
                statusMessage = "Password must contain a special character.";
                statusColor = Color.FromArgb(255, 120, 120);
                TriggerShakeAnimation();
                this.Invalidate();
                return;
            }

            isProcessing = true;
            statusMessage = "Checking account...";
            statusColor = Color.FromArgb(200, 255, 200);
            this.Invalidate();

            try
            {

                string deviceFingerprint = GenerateDeviceFingerprint();


                string userId = Guid.NewGuid().ToString("N");

                UserAccount? userAccount = null;
                bool useApi = apiClient != null;

                if (useApi && apiClient != null)
                {
                    statusMessage = "Connecting to server...";
                    statusColor = Color.FromArgb(200, 255, 200);
                    this.Invalidate();

                    var loginResult = await apiClient.LoginUserAsync(email, password, deviceFingerprint);

                    if (loginResult.Success)
                    {
                        if (loginResult.AccountFlagged)
                        {
                            isProcessing = false;
                            statusMessage = "Account flagged for review.";
                            statusColor = Color.FromArgb(255, 120, 120);
                            this.Invalidate();
                            MessageBox.Show("Your account has been flagged for suspicious activity. Please contact support.", "Account Flagged", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }


                        var localUser = await usersCollection.Find(u => u.Email.ToLower() == email.ToLower()).FirstOrDefaultAsync();
                        if (localUser == null)
                        {
                            var newUser = new UserAccount
                            {
                                UserId = userId,
                                Email = email,
                                Username = username,
                                Password = "",
                                HasCompletedTutorial = loginResult.HasCompletedTutorial,
                                CreatedAt = DateTime.UtcNow
                            };
                            await usersCollection.InsertOneAsync(newUser);
                            userAccount = newUser;
                        }
                        else
                        {
                            userAccount = localUser;
                        }

                        animationTimer.Stop();
                        this.Hide();
                        UserSession.SaveLastUser(email);

                        if (userAccount.HasCompletedTutorial)
                        {
                            // Check if MainFrame already exists and refresh it instead of creating new one
                            foreach (Form form in Application.OpenForms)
                            {
                                if (form is MainFrame mainFrame)
                                {
                                    mainFrame.RefreshLevelsData();
                                    mainFrame.Show();
                                    this.Dispose();
                                    TriggerSuccessAnimation();
                                    return;
                                }
                            }

                            // If no MainFrame exists, create a new one
                            MainFrame main = new MainFrame(userAccount, database);
                            main.Show();
                        }
                        else
                        {
                            TutorialFrame tutorial = new TutorialFrame(userAccount);
                            tutorial.Show();
                        }
                        this.Dispose();
                        TriggerSuccessAnimation();
                        return;
                    }


                    statusMessage = "Creating account...";
                    statusColor = Color.FromArgb(200, 255, 200);
                    this.Invalidate();
                    var registrationResult = await apiClient.RegisterUserAsync(email, password, deviceFingerprint);

                    if (registrationResult.Success)
                    {
                        if (registrationResult.RequiresManualReview)
                        {
                            statusMessage = "Account under review.";
                            statusColor = Color.FromArgb(255, 200, 100);
                            this.Invalidate();
                            MessageBox.Show("Your account is under manual review. You can play but some features may be limited.", "Account Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }


                        var newUser = new UserAccount
                        {
                            UserId = userId,
                            Email = email,
                            Username = username,
                            Password = "",
                            HasCompletedTutorial = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        await usersCollection.InsertOneAsync(newUser);
                        userAccount = newUser;

                        animationTimer.Stop();
                        this.Hide();
                        UserSession.SaveLastUser(email);

                        TutorialFrame tutorial = new TutorialFrame(userAccount);
                        tutorial.Show();
                        this.Dispose();
                        TriggerSuccessAnimation();
                        return;
                    }


                    useApi = false;
                }


                statusMessage = "Using local mode...";
                statusColor = Color.FromArgb(200, 255, 200);
                this.Invalidate();

                var existingUser = await usersCollection.Find(u => u.Email.ToLower() == email.ToLower()).FirstOrDefaultAsync();

                animationTimer.Stop();
                this.Hide();

                if (existingUser != null)
                {

                    if (existingUser.Password != password)
                    {
                        isProcessing = false;
                        statusMessage = "Incorrect password.";
                        statusColor = Color.FromArgb(255, 120, 120);
                        this.Invalidate();
                        this.Show();
                        return;
                    }


                    UserSession.SaveLastUser(email);

                    if (existingUser.HasCompletedTutorial)
                    {
                        // Check if MainFrame already exists and refresh it instead of creating new one
                        foreach (Form form in Application.OpenForms)
                        {
                            if (form is MainFrame mainFrame)
                            {
                                mainFrame.RefreshLevelsData();
                                mainFrame.Show();
                                this.Dispose();
                                TriggerSuccessAnimation();
                                return;
                            }
                        }

                        // If no MainFrame exists, create a new one
                        MainFrame main = new MainFrame(existingUser, database);
                        main.Show();
                    }
                    else
                    {
                        TutorialFrame tutorial = new TutorialFrame(existingUser);
                        tutorial.Show();
                    }
                    this.Dispose();
                    TriggerSuccessAnimation();
                }
                else
                {
                    var newUser = new UserAccount
                    {
                        UserId = userId,
                        Email = email,
                        Username = username,
                        Password = password,
                        HasCompletedTutorial = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await usersCollection.InsertOneAsync(newUser);
                    UserSession.SaveLastUser(email);

                    TutorialFrame tutorial = new TutorialFrame(newUser);
                    tutorial.Show();
                    this.Dispose();
                    TriggerSuccessAnimation();
                }
            }
            catch (Exception ex)
            {
                isProcessing = false;
                statusMessage = "Connection error.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                MessageBox.Show($"Error: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GenerateDeviceFingerprint()
        {
            return Environment.MachineName + "|" +
                   Environment.OSVersion.VersionString + "|" +
                   Environment.ProcessorCount + "|" +
                   Environment.Is64BitOperatingSystem;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);
            
            // Apply shake offset for error animation
            int shakeX = 0, shakeY = 0;
            if (shakeIntensity > 0)
            {
                shakeX = (int)(shakeIntensity * Math.Sin(shakePhase * Math.PI / 180));
                shakeY = (int)(shakeIntensity * Math.Cos(shakePhase * Math.PI / 180));
            }
            
            GraphicsState gstate = g.Save();
            g.TranslateTransform(shakeX, shakeY);
            
            DrawTitleBanner(g);
            DrawInputPanel(g);
            DrawStatusMessage(g);
            
            g.Restore(gstate);
            
            // Draw confetti on top
            if (showSuccessAnimation)
            {
                DrawConfetti(g);
            }
        }

        private void DrawTitleBanner(Graphics g)
        {
            // Enhanced title with gradient and glow
            using (LinearGradientBrush titleGradient = new LinearGradientBrush(
                titleRect, 
                Color.FromArgb(255, 255, 180, 50), 
                Color.FromArgb(255, 255, 120, 30), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(titleGradient, titleRect, 25);
            }
            
            // Glassmorphism effect
            using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                titleRect, 
                Color.FromArgb(60, 255, 255, 255), 
                Color.FromArgb(30, 255, 255, 255), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(glassBrush, titleRect, 25);
            }
            
            // Glow effect
            int glowPulse = (int)(10 * Math.Sin(pulsePhase * Math.PI / 40));
            int glowAlpha = Math.Max(0, Math.Min(255, 30 + glowPulse));
            using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 255, 200, 100)))
            {
                Rectangle glowRect = new Rectangle(titleRect.X - 2, titleRect.Y - 2, titleRect.Width + 4, titleRect.Height + 4);
                g.FillRoundedRectangle(glowBrush, glowRect, 27);
            }
            
            // Border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 255, 220, 180), 3))
            {
                g.DrawRoundedRectangle(borderPen, titleRect, 25);
            }
            
            using (Font titleFont = new Font("Comic Sans MS", 26, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, "JOIN THE FUN", titleFont, titleRect, Color.White, Color.FromArgb(200, 100, 30), 2, sf);
            }
            
            // Enhanced subtitle
            using (Font subFont = new Font("Comic Sans MS", 13, FontStyle.Italic))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(230, 255, 230, 255)))
            {
                g.DrawString("✨ Sign up or log in to play! ✨", subFont, subBrush, subtitleRect, sf);
            }
        }

        private void DrawInputPanel(Graphics g)
        {
            // Enhanced panel with better gradient and shadow
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                Rectangle shadowRect = new Rectangle(panelRect.X + 6, panelRect.Y + 6, panelRect.Width, panelRect.Height);
                g.FillRoundedRectangle(shadow, shadowRect, 20);
            }

            using (LinearGradientBrush panelGradient = new LinearGradientBrush(
                panelRect, 
                Color.FromArgb(255, 160, 120, 220), 
                Color.FromArgb(255, 120, 80, 190), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(panelGradient, panelRect, 20);
            }
            
            // Glassmorphism effect
            using (LinearGradientBrush glassBrush = new LinearGradientBrush(
                panelRect, 
                Color.FromArgb(40, 255, 255, 255), 
                Color.FromArgb(20, 255, 255, 255), 
                LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(glassBrush, panelRect, 20);
            }
            
            // Inner highlight
            Rectangle innerRect = new Rectangle(panelRect.X + 4, panelRect.Y + 4, panelRect.Width - 8, panelRect.Height - 8);
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            {
                Rectangle highlightRect = new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 8);
                g.FillRoundedRectangle(highlight, highlightRect, 16);
            }
            
            // Border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 100, 60, 160), 4))
            {
                g.DrawRoundedRectangle(borderPen, panelRect, 20);
            }

            // Username field
            DrawFloatingLabel(g, "USERNAME", usernameRect, usernameFocusAlpha, usernameLabelY, usernameInput);
            DrawInputField(g, usernameRect, usernameInput, isUsernameFocused, usernameFocusAlpha, "Choose a username...");

            // Email field
            DrawFloatingLabel(g, "EMAIL ADDRESS", emailRect, emailFocusAlpha, emailLabelY, emailInput);
            DrawInputField(g, emailRect, emailInput, isEmailFocused, emailFocusAlpha, "Enter your email...");

            // Password field
            DrawFloatingLabel(g, "PASSWORD", passwordRect, passwordFocusAlpha, passwordLabelY, passwordInput);
            string passwordDisplay = showPassword ? passwordInput : new string('●', passwordInput.Length);
            DrawInputField(g, passwordRect, passwordDisplay, isPasswordFocused, passwordFocusAlpha, "Enter your password...");

            DrawPasswordToggle(g);
            DrawPasswordStrength(g);
            DrawPasswordRequirements(g);

            DrawButton(g);
            
            // Loading spinner
            if (isProcessing)
            {
                DrawLoadingSpinner(g);
            }

            // Status message (drawn after button to appear on top)
            if (!string.IsNullOrEmpty(statusMessage))
            {
                using (Font statusFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    Rectangle statusRect = new Rectangle(panelRect.X, panelRect.Y + 310, panelRect.Width, 30);
                    using (SolidBrush statusBrush = new SolidBrush(statusColor))
                    {
                        g.DrawString(statusMessage, statusFont, statusBrush, statusRect, sf);
                    }
                }
            }
        }

        private void DrawFloatingLabel(Graphics g, string label, Rectangle fieldRect, float focusAlpha, float labelY, string inputText)
        {
            bool isFloating = (focusAlpha > 0.5f) || !string.IsNullOrEmpty(inputText);
            float fontSize = isFloating ? 9f : 11f;
            Color labelColor = isFloating ? 
                Color.FromArgb(255, 100, 180, 255) : 
                Color.FromArgb(
                    (int)(150 + 105 * focusAlpha), 
                    (int)(180 + 75 * focusAlpha), 
                    (int)(210 + 45 * focusAlpha)
                );
            
            using (Font labelFont = new Font("Comic Sans MS", fontSize, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(labelColor))
            {
                g.DrawString(label, labelFont, labelBrush, fieldRect.X, labelY);
            }
            
            // Animated underline when focused
            if (focusAlpha > 0.01f)
            {
                int underlineWidth = (int)(fieldRect.Width * focusAlpha);
                int clampedAlpha = Math.Max(0, Math.Min(255, (int)(255 * focusAlpha)));
                using (Pen underlinePen = new Pen(Color.FromArgb(clampedAlpha, 255, 200, 100), 2))
                {
                    g.DrawLine(underlinePen, fieldRect.X, (int)labelY + 15, fieldRect.X + underlineWidth, (int)labelY + 15);
                }
            }
        }

        private void DrawInputField(Graphics g, Rectangle rect, string text, bool isFocused, float focusAlpha, string placeholder)
        {
            // Animated background color
            int baseR = 255, baseG = 255, baseB = 255;
            int focusR = 255, focusG = 240, focusB = 180;
            
            int r = (int)(baseR + (focusR - baseR) * focusAlpha);
            int green = (int)(baseG + (focusG - baseG) * focusAlpha);
            int b = (int)(baseB + (focusB - baseB) * focusAlpha);
            
            Color bgColor = Color.FromArgb(255, r, green, b);
            
            // Animated border color
            int borderR = 200, borderG = 150, borderB = 255;
            int focusBorderR = 255, focusBorderG = 200, focusBorderB = 100;
            
            int br = (int)(borderR + (focusBorderR - borderR) * focusAlpha);
            int borderGreen = (int)(borderG + (focusBorderG - borderG) * focusAlpha);
            int bb = (int)(borderB + (focusBorderB - borderB) * focusAlpha);
            
            Color borderColor = Color.FromArgb(255, br, borderGreen, bb);
            int borderWidth = 3 + (int)(2 * focusAlpha);

            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                rect, bgColor, Color.FromArgb(200, bgColor.R, bgColor.G, bgColor.B), LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(bgBrush, rect, 14);
            }

            using (Pen borderPen = new Pen(borderColor, borderWidth))
            {
                g.DrawRoundedRectangle(borderPen, rect, 14);
            }
            
            // Subtle glow when focused
            if (focusAlpha > 0.01f)
            {
                int clampedAlpha = Math.Max(0, Math.Min(255, (int)(30 * focusAlpha)));
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(clampedAlpha, 255, 220, 150)))
                {
                    Rectangle glowRect = new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
                    g.FillRoundedRectangle(glowBrush, glowRect, 16);
                }
            }

            string displayText = string.IsNullOrEmpty(text) ? placeholder : text;
            Color textColor = string.IsNullOrEmpty(text) ? Color.FromArgb(180, 150, 150, 150) : Color.FromArgb(255, 60, 40, 80);

            using (Font inputFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(displayText, inputFont, new SolidBrush(textColor), new Rectangle(rect.X + 18, rect.Y, rect.Width - 36, rect.Height), sf);
            }

            if (isFocused)
            {
                int cursorX = rect.X + 18 + (int)g.MeasureString(text, new Font("Comic Sans MS", 14, FontStyle.Bold)).Width;
                if (cursorX < rect.Right - 18)
                {
                    // Animated cursor
                    int cursorAlpha = (int)(200 + 55 * Math.Sin(pulsePhase * Math.PI / 8));
                    cursorAlpha = Math.Max(0, Math.Min(255, cursorAlpha));
                    using (Pen cursorPen = new Pen(Color.FromArgb(cursorAlpha, 60, 40, 80), 2))
                    {
                        g.DrawLine(cursorPen, cursorX, rect.Y + 12, cursorX, rect.Bottom - 12);
                    }
                }
            }
        }

        private void DrawButton(Graphics g)
        {
            // Calculate scaled rectangle with 3D press effect
            int scaledWidth = (int)(buttonRect.Width * buttonScale);
            int scaledHeight = (int)(buttonRect.Height * buttonScale);
            int scaledX = buttonRect.X + (buttonRect.Width - scaledWidth) / 2;
            int scaledY = buttonRect.Y + (buttonRect.Height - scaledHeight) / 2 + (int)buttonPressDepth;
            Rectangle scaledRect = new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);
            
            // 3D shadow for press effect
            if (buttonPressDepth > 0.5f)
            {
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                {
                    Rectangle shadowRect = new Rectangle(scaledRect.X, scaledRect.Y + (int)buttonPressDepth, scaledRect.Width, scaledRect.Height);
                    g.FillRoundedRectangle(shadowBrush, shadowRect, 22);
                }
            }
            
            // Enhanced button gradient
            Color buttonColor = isButtonHovered ? Color.FromArgb(255, 255, 120, 180) : Color.FromArgb(255, 255, 80, 150);
            Color buttonColor2 = isButtonHovered ? Color.FromArgb(255, 255, 80, 140) : Color.FromArgb(255, 255, 50, 110);

            using (LinearGradientBrush buttonBrush = new LinearGradientBrush(
                scaledRect, buttonColor, buttonColor2, LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(buttonBrush, scaledRect, 22);
            }
            
            // Glow effect on hover
            if (isButtonHovered)
            {
                int glowPulse = (int)(15 * Math.Sin(pulsePhase * Math.PI / 30));
                int glowAlpha = Math.Max(0, Math.Min(255, 40 + glowPulse));
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 255, 180, 220)))
                {
                    Rectangle glowRect = new Rectangle(scaledRect.X - 3, scaledRect.Y - 3, scaledRect.Width + 6, scaledRect.Height + 6);
                    g.FillRoundedRectangle(glowBrush, glowRect, 25);
                }
            }

            using (Pen buttonBorder = new Pen(Color.FromArgb(255, 255, 220, 240), 4))
            {
                g.DrawRoundedRectangle(buttonBorder, scaledRect, 22);
            }
            
            // Inner highlight
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            {
                Rectangle highlightRect = new Rectangle(scaledRect.X + 4, scaledRect.Y + 4, scaledRect.Width - 8, 8);
                g.FillRoundedRectangle(highlight, highlightRect, 18);
            }

            // Button text with press offset
            Rectangle textRect = scaledRect;
            if (buttonPressDepth > 0.5f)
            {
                textRect = new Rectangle(scaledRect.X, scaledRect.Y + (int)(buttonPressDepth * 0.5f), scaledRect.Width, scaledRect.Height);
            }
            
            using (Font buttonFont = new Font("Comic Sans MS", 20, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string buttonText = isProcessing ? "" : "CRUSH IT!";
                CrushItStyleHelper.DrawOutlinedText(g, buttonText, buttonFont, textRect, Color.White, Color.FromArgb(200, 100, 50, 100), 2, sf);
            }
        }

        private void DrawPasswordToggle(Graphics g)
        {
            // Enhanced toggle with background
            Color bgColor = isToggleHovered ? Color.FromArgb(255, 180, 230, 180) : (showPassword ? Color.FromArgb(255, 130, 210, 130) : Color.FromArgb(255, 220, 220, 220));
            
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillRoundedRectangle(bgBrush, togglePasswordRect, 8);
            }
            
            using (Pen borderPen = new Pen(Color.FromArgb(255, 150, 150, 150), 2))
            {
                g.DrawRoundedRectangle(borderPen, togglePasswordRect, 8);
            }
            
            Color toggleColor = isToggleHovered ? Color.FromArgb(255, 80, 160, 80) : (showPassword ? Color.FromArgb(255, 60, 140, 60) : Color.FromArgb(255, 120, 120, 120));
            using (SolidBrush toggleBrush = new SolidBrush(toggleColor))
            using (Font toggleFont = new Font("Segoe UI Emoji", 16, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(showPassword ? "👁" : "👁‍🗨", toggleFont, toggleBrush, togglePasswordRect, sf);
            }
        }

        private void DrawPasswordRequirements(Graphics g)
        {
            if (string.IsNullOrEmpty(passwordInput) || passwordFocusAlpha < 0.1f)
                return;

            var requirements = new[]
            {
                ("8+ chars", passwordInput.Length >= 8),
                ("Upper", passwordInput.Any(char.IsUpper)),
                ("Lower", passwordInput.Any(char.IsLower)),
                ("Number", passwordInput.Any(char.IsDigit)),
                ("Special", passwordInput.Any(c => !char.IsLetterOrDigit(c)))
            };

            float startY = passwordRect.Bottom + 45;
            float startX = passwordRect.X;
            float itemWidth = passwordRect.Width / 5f;

            using (Font reqFont = new Font("Comic Sans MS", 8, FontStyle.Bold))
            {
                for (int i = 0; i < requirements.Length; i++)
                {
                    bool met = requirements[i].Item2;
                    Color reqColor = met ? Color.FromArgb(255, 100, 255, 100) : Color.FromArgb(255, 255, 150, 150);
                    
                    using (SolidBrush reqBrush = new SolidBrush(reqColor))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        RectangleF reqRect = new RectangleF(startX + i * itemWidth, startY, itemWidth, 20);
                        g.DrawString(requirements[i].Item1, reqFont, reqBrush, reqRect, sf);
                    }
                }
            }
        }
        
        private void DrawLoadingSpinner(Graphics g)
        {
            Rectangle spinnerRect = new Rectangle(buttonRect.X + buttonRect.Width / 2 - 15, buttonRect.Y + buttonRect.Height / 2 - 15, 30, 30);
            
            using (Pen spinnerPen = new Pen(Color.FromArgb(255, 255, 255, 255), 3))
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = loadingRotation + (i * (float)Math.PI / 4);
                    float alpha = 255 - (i * 30);
                    spinnerPen.Color = Color.FromArgb((int)alpha, 255, 255, 255);
                    
                    float x1 = spinnerRect.X + spinnerRect.Width / 2 + (float)(10 * Math.Cos(angle));
                    float y1 = spinnerRect.Y + spinnerRect.Height / 2 + (float)(10 * Math.Sin(angle));
                    float x2 = spinnerRect.X + spinnerRect.Width / 2 + (float)(15 * Math.Cos(angle));
                    float y2 = spinnerRect.Y + spinnerRect.Height / 2 + (float)(15 * Math.Sin(angle));
                    
                    g.DrawLine(spinnerPen, x1, y1, x2, y2);
                }
            }
        }
        
        private void DrawConfetti(Graphics g)
        {
            foreach (var confetti in confettiParticles)
            {
                GraphicsState gstate = g.Save();
                g.TranslateTransform(confetti.X, confetti.Y);
                g.RotateTransform(confetti.Rotation * 180 / (float)Math.PI);
                
                int clampedAlpha = Math.Max(0, Math.Min(255, (int)(255 * confetti.Alpha)));
                using (SolidBrush confettiBrush = new SolidBrush(Color.FromArgb(clampedAlpha, confetti.Color)))
                {
                    g.FillRectangle(confettiBrush, -confetti.Size / 2, -confetti.Size / 2, confetti.Size, confetti.Size);
                }
                
                g.Restore(gstate);
            }
        }

        private void DrawPasswordStrength(Graphics g)
        {
            if (string.IsNullOrEmpty(passwordInput))
                return;

            int strength = CalculatePasswordStrength(passwordInput);
            Color strengthColor;
            string strengthText;

            switch (strength)
            {
                case 0:
                case 1:
                    strengthColor = Color.FromArgb(255, 255, 100, 100);
                    strengthText = "Weak";
                    break;
                case 2:
                    strengthColor = Color.FromArgb(255, 255, 200, 100);
                    strengthText = "Fair";
                    break;
                case 3:
                    strengthColor = Color.FromArgb(255, 255, 255, 100);
                    strengthText = "Good";
                    break;
                case 4:
                    strengthColor = Color.FromArgb(255, 100, 255, 100);
                    strengthText = "Strong";
                    break;
                default:
                    strengthColor = Color.FromArgb(255, 100, 200, 255);
                    strengthText = "Very Strong";
                    break;
            }

            Rectangle strengthBarRect = new Rectangle(passwordRect.X, passwordRect.Bottom + 10, passwordRect.Width, 8);
            int filledWidth = (int)(strengthBarRect.Width * ((strength + 1) / 5.0));

            // Background bar with rounded corners
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(255, 220, 220, 220)))
            {
                g.FillRoundedRectangle(bgBrush, strengthBarRect, 5);
            }
            
            // Animated fill bar
            using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                strengthBarRect, 
                strengthColor, 
                Color.FromArgb(200, strengthColor.R, strengthColor.G, strengthColor.B), 
                LinearGradientMode.Vertical))
            {
                Rectangle filledRect = new Rectangle(strengthBarRect.X, strengthBarRect.Y, filledWidth, strengthBarRect.Height);
                g.FillRoundedRectangle(fillBrush, filledRect, 5);
            }
            
            // Glow effect on the filled portion
            if (filledWidth > 0)
            {
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(30, strengthColor)))
                {
                    Rectangle glowRect = new Rectangle(strengthBarRect.X, strengthBarRect.Y - 2, filledWidth, strengthBarRect.Height + 4);
                    g.FillRoundedRectangle(glowBrush, glowRect, 7);
                }
            }

            // Enhanced strength text
            using (Font strengthFont = new Font("Comic Sans MS", 10, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (SolidBrush textBrush = new SolidBrush(strengthColor))
            {
                g.DrawString(strengthText, strengthFont, textBrush, new Rectangle(strengthBarRect.X, strengthBarRect.Bottom + 4, strengthBarRect.Width, 18), sf);
            }
        }

        private int CalculatePasswordStrength(string password)
        {
            int strength = 0;

            if (password.Length >= 8) strength++;
            if (password.Length >= 12) strength++;

            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            if (hasUpper) strength++;
            if (hasLower) strength++;
            if (hasDigit) strength++;
            if (hasSpecial) strength++;

            return Math.Min(strength, 5);
        }

        private void DrawStatusMessage(Graphics g)
        {

        }
    }
}

