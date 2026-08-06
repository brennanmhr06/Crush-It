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
        private bool isUsernameFocused = false;
        private bool isEmailFocused = false;
        private bool isPasswordFocused = false;
        private bool isButtonHovered = false;
        private bool isToggleHovered = false;
        private bool showPassword = false;

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
            StartAnimation();


            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Account";
            this.Size = new Size(550, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += SignUpForm_KeyDown;
            this.MouseClick += SignUpForm_MouseClick;
            this.MouseMove += SignUpForm_MouseMove;
            this.MouseLeave += (s, e) => { isButtonHovered = false; isToggleHovered = false; this.Cursor = Cursors.Default; this.Invalidate(); };


            int centerX = 275;
            usernameRect = new Rectangle(centerX - 165, 160, 330, 45);
            emailRect = new Rectangle(centerX - 165, 240, 330, 45);
            passwordRect = new Rectangle(centerX - 165, 320, 290, 45);
            togglePasswordRect = new Rectangle(passwordRect.Right + 10, passwordRect.Y + 10, 30, 25);
            buttonRect = new Rectangle(centerX - 125, 420, 250, 55);

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
            this.Invalidate();
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
                this.Invalidate();
                return;
            }

            if (username.Length < 3)
            {
                statusMessage = "Username must be at least 3 characters.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                statusMessage = "Enter a valid email address.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }


            if (password.Length < 8)
            {
                statusMessage = "Password must be at least 8 characters.";
                statusColor = Color.FromArgb(255, 120, 120);
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
                this.Invalidate();
                return;
            }

            if (!hasLowerCase)
            {
                statusMessage = "Password must contain a lowercase letter.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }

            if (!hasDigit)
            {
                statusMessage = "Password must contain a number.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }

            if (!hasSpecialChar)
            {
                statusMessage = "Password must contain a special character.";
                statusColor = Color.FromArgb(255, 120, 120);
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
                            MainFrame main = new MainFrame(userAccount, database);
                            main.Show();
                        }
                        else
                        {
                            TutorialFrame tutorial = new TutorialFrame(userAccount);
                            tutorial.Show();
                        }
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
                        MainFrame main = new MainFrame(existingUser, database);
                        main.Show();
                    }
                    else
                    {
                        TutorialFrame tutorial = new TutorialFrame(existingUser);
                        tutorial.Show();
                    }
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
            DrawTitleBanner(g);
            DrawInputPanel(g);
            DrawStatusMessage(g);
        }

        private void DrawTitleBanner(Graphics g)
        {
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(75, 20, 400, 55), "JOIN THE FUN");

            using (Font subFont = new Font("Comic Sans MS", 12, FontStyle.Italic))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("Sign up or log in to play!", subFont, new SolidBrush(Color.FromArgb(220, 255, 220, 255)), new Rectangle(75, 80, 400, 30), sf);
            }
        }

        private void DrawInputPanel(Graphics g)
        {
            Rectangle panelRect = new Rectangle(50, 130, 450, 380);
            CrushItStyleHelper.DrawPanel(g, panelRect, Color.FromArgb(255, 150, 110, 200), Color.FromArgb(255, 110, 70, 170), Color.FromArgb(255, 90, 60, 140));

            using (Font labelFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
            {
                g.DrawString("USERNAME", labelFont, new SolidBrush(Color.FromArgb(255, 200, 150, 255)), usernameRect.X, usernameRect.Y - 22);
            }

            DrawInputField(g, usernameRect, usernameInput, isUsernameFocused, "Choose a username...");

            using (Font labelFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
            {
                g.DrawString("EMAIL ADDRESS", labelFont, new SolidBrush(Color.FromArgb(255, 200, 150, 255)), emailRect.X, emailRect.Y - 22);
            }

            DrawInputField(g, emailRect, emailInput, isEmailFocused, "Enter your email...");

            using (Font labelFont = new Font("Comic Sans MS", 11, FontStyle.Bold))
            {
                g.DrawString("PASSWORD", labelFont, new SolidBrush(Color.FromArgb(255, 200, 150, 255)), passwordRect.X, passwordRect.Y - 22);
            }

            string passwordDisplay = showPassword ? passwordInput : new string('●', passwordInput.Length);
            DrawInputField(g, passwordRect, passwordDisplay, isPasswordFocused, "Enter your password...");

            DrawPasswordToggle(g);

            DrawPasswordStrength(g);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                using (Font statusFont = new Font("Comic Sans MS", 10, FontStyle.Bold))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    Rectangle statusRect = new Rectangle(panelRect.X, panelRect.Y + 300, panelRect.Width, 25);
                    g.DrawString(statusMessage, statusFont, new SolidBrush(statusColor), statusRect, sf);
                }
            }

            DrawButton(g);
        }

        private void DrawInputField(Graphics g, Rectangle rect, string text, bool isFocused, string placeholder)
        {
            Color bgColor = isFocused ? Color.FromArgb(255, 255, 230, 150) : Color.FromArgb(255, 255, 255, 255);
            Color borderColor = isFocused ? Color.FromArgb(255, 255, 180, 60) : Color.FromArgb(255, 200, 150, 255);

            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                rect, bgColor, Color.FromArgb(200, bgColor.R, bgColor.G, bgColor.B), LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(bgBrush, rect, 12);
            }

            using (Pen borderPen = new Pen(borderColor, 3))
            {
                g.DrawRoundedRectangle(borderPen, rect, 12);
            }

            string displayText = string.IsNullOrEmpty(text) ? placeholder : text;
            Color textColor = string.IsNullOrEmpty(text) ? Color.FromArgb(180, 150, 150, 150) : Color.FromArgb(255, 60, 40, 80);

            using (Font inputFont = new Font("Comic Sans MS", 13, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(displayText, inputFont, new SolidBrush(textColor), new Rectangle(rect.X + 15, rect.Y, rect.Width - 30, rect.Height), sf);
            }

            if (isFocused)
            {
                int cursorX = rect.X + 15 + (int)g.MeasureString(text, new Font("Comic Sans MS", 13, FontStyle.Bold)).Width;
                if (cursorX < rect.Right - 15)
                {
                    using (Pen cursorPen = new Pen(Color.FromArgb(255, 60, 40, 80), 2))
                    {
                        g.DrawLine(cursorPen, cursorX, rect.Y + 10, cursorX, rect.Bottom - 10);
                    }
                }
            }
        }

        private void DrawButton(Graphics g)
        {
            Color buttonColor = isButtonHovered ? Color.FromArgb(255, 255, 100, 150) : Color.FromArgb(255, 255, 60, 120);
            Color buttonColor2 = isButtonHovered ? Color.FromArgb(255, 255, 60, 100) : Color.FromArgb(255, 255, 30, 80);

            using (LinearGradientBrush buttonBrush = new LinearGradientBrush(
                buttonRect, buttonColor, buttonColor2, LinearGradientMode.Vertical))
            {
                g.FillRoundedRectangle(buttonBrush, buttonRect, 18);
            }

            using (Pen buttonBorder = new Pen(Color.FromArgb(255, 255, 200, 220), 3))
            {
                g.DrawRoundedRectangle(buttonBorder, buttonRect, 18);
            }

            using (Font buttonFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                CrushItStyleHelper.DrawOutlinedText(g, "CRUSH IT!", buttonFont, buttonRect, Color.White, Color.FromArgb(180, 120, 40, 80), 2, sf);
            }
        }

        private void DrawPasswordToggle(Graphics g)
        {
            Color toggleColor = isToggleHovered ? Color.FromArgb(255, 150, 220, 150) : (showPassword ? Color.FromArgb(255, 100, 200, 100) : Color.FromArgb(255, 200, 200, 200));
            using (SolidBrush toggleBrush = new SolidBrush(toggleColor))
            using (Font toggleFont = new Font("Arial", 14, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(showPassword ? "👁" : "👁‍🗨", toggleFont, toggleBrush, togglePasswordRect, sf);
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

            Rectangle strengthBarRect = new Rectangle(passwordRect.X, passwordRect.Bottom + 8, passwordRect.Width, 8);
            int filledWidth = (int)(strengthBarRect.Width * ((strength + 1) / 5.0));

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(255, 200, 200, 200)))
            {
                g.FillRoundedRectangle(bgBrush, strengthBarRect, 4);
            }

            using (SolidBrush fillBrush = new SolidBrush(strengthColor))
            {
                Rectangle filledRect = new Rectangle(strengthBarRect.X, strengthBarRect.Y, filledWidth, strengthBarRect.Height);
                g.FillRoundedRectangle(fillBrush, filledRect, 4);
            }

            using (Font strengthFont = new Font("Comic Sans MS", 9, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(strengthText, strengthFont, new SolidBrush(strengthColor), new Rectangle(strengthBarRect.X, strengthBarRect.Bottom + 2, strengthBarRect.Width, 15), sf);
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

