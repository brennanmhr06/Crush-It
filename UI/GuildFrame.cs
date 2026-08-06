using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.Core;

namespace CrushIt.UI
{
    public enum GuildViewMode
    {   
        Browse,
        MyGuild,
        CreateGuild,
        GuildDetails
    }

    public class GuildFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private readonly GuildRepository guildRepository;

        private GuildViewMode currentMode = GuildViewMode.Browse;
        private Guild? selectedGuild;
        private List<Guild> displayedGuilds = new List<Guild>();

        private readonly NavItem currentNav = NavItem.Guilds;

        // UI State
        private string guildNameInput = "";
        private string guildDescriptionInput = "";
        private string searchQuery = "";
        private string statusMessage = "";
        private Color statusColor = Color.White;
        private bool isProcessing = false;

        // Input rectangles
        private Rectangle searchRect;
        private Rectangle backButtonRect;
        private Rectangle nameInputRect;
        private Rectangle descriptionInputRect;
        private Rectangle createGuildButtonRect;
        private Rectangle joinButtonRect;
        private Rectangle leaveButtonRect;

        // Hover states
        private bool isSearchFocused = false;
        private bool isNameFocused = false;
        private bool isDescriptionFocused = false;
        private int hoveredGuildIndex = -1;
        private bool isBackButtonHovered = false;
        private bool isJoinButtonHovered = false;
        private bool isLeaveButtonHovered = false;
        private bool isCreateGuildButtonHovered = false;

        // Background styling
        private List<StyleParticle> backgroundParticles = new List<StyleParticle>();
        private System.Windows.Forms.Timer animationTimer = null!;
        private Random particleRand = new Random();
        private int pulsePhase = 0;

        public GuildFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;
            this.guildRepository = new GuildRepository(db);

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            InitializeParticles();
            LoadInitialData();
            StartAnimation();

            SoundHelper.StartBackgroundMusic();
            SoundHelper.SetBackgroundMusicVolume(0.3f);

            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Guilds";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += GuildFrame_KeyDown;
            this.MouseClick += GuildFrame_MouseClick;
            this.MouseMove += GuildFrame_MouseMove;
            this.MouseLeave += GuildFrame_MouseLeave;
            this.FormClosed += (s, e) => 
            {
                animationTimer?.Stop();
                SoundHelper.StopBackgroundMusic();
            };

            // Initialize rectangles
            searchRect = new Rectangle(80, 95, 500, 40);
            backButtonRect = new Rectangle(30, 30, 100, 35);
            nameInputRect = new Rectangle(100, 130, 700, 40);
            descriptionInputRect = new Rectangle(100, 190, 700, 80);
            createGuildButtonRect = new Rectangle(350, 300, 200, 50);
            joinButtonRect = new Rectangle(380, 450, 140, 45);
            leaveButtonRect = new Rectangle(750, 30, 40, 40);
        }

        private void InitializeParticles()
        {
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 45, 890, 80, 530));
        }

        private async void LoadInitialData()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentUser.GuildId))
                {
                    currentMode = GuildViewMode.MyGuild;
                    selectedGuild = await guildRepository.GetGuildByIdAsync(currentUser.GuildId);
                }
                else
                {
                    currentMode = GuildViewMode.Browse;
                    displayedGuilds = await guildRepository.GetSearchableGuildsAsync(currentUser);
                    statusMessage = $"Loaded {displayedGuilds.Count} guilds";
                    statusColor = Color.FromArgb(120, 255, 120);
                }
            }
            catch (Exception ex)
            {
                statusMessage = "Error loading guilds: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }
            this.Invalidate();
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

        private void GuildFrame_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (currentMode == GuildViewMode.GuildDetails || currentMode == GuildViewMode.CreateGuild)
                {
                    currentMode = GuildViewMode.Browse;
                    selectedGuild = null;
                    this.Invalidate();
                }
                else
                {
                    ReturnToMainFrame();
                }
            }
        }

        private void ReturnToMainFrame()
        {
            MainFrame mainFrame = new MainFrame(currentUser, database);
            mainFrame.Show();
            this.Hide();
            this.Dispose();
        }

        private async void GuildFrame_MouseClick(object? sender, MouseEventArgs e)
        {
            if (isProcessing) return;

            // Check navbar clicks
            if (CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem clickedNav))
            {
                if (clickedNav == NavItem.Home)
                {
                    ReturnToMainFrame();
                }
                else if (clickedNav == NavItem.Levels)
                {
                    ReturnToMainFrame();
                }
                else if (clickedNav == NavItem.Achievements)
                {
                    ReturnToMainFrame();
                }
                return;
            }

            if (backButtonRect.Contains(e.Location))
            {
                if (currentMode == GuildViewMode.GuildDetails || currentMode == GuildViewMode.CreateGuild)
                {
                    currentMode = GuildViewMode.Browse;
                    selectedGuild = null;
                    this.Invalidate();
                }
                return;
            }

            if (currentMode == GuildViewMode.Browse)
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
                    currentMode = GuildViewMode.GuildDetails;
                    this.Invalidate();
                    return;
                }
            }
            else if (currentMode == GuildViewMode.CreateGuild)
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
                    await CreateGuildAsync();
                    return;
                }
                else
                {
                    isNameFocused = false;
                    isDescriptionFocused = false;
                }
            }
            else if (currentMode == GuildViewMode.GuildDetails)
            {
                if (joinButtonRect.Contains(e.Location) && selectedGuild != null)
                {
                    await JoinGuildAsync();
                    return;
                }
                else if (leaveButtonRect.Contains(e.Location))
                {
                    await LeaveGuildAsync();
                    return;
                }
            }
            else if (currentMode == GuildViewMode.MyGuild)
            {
                if (leaveButtonRect.Contains(e.Location))
                {
                    await LeaveGuildAsync();
                    return;
                }
            }

            this.Invalidate();
        }

        private void GuildFrame_MouseMove(object? sender, MouseEventArgs e)
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

            // Filter hover states based on current mode
            if (currentMode == GuildViewMode.Browse)
            {
                isLeaveButtonHovered = false;
                isJoinButtonHovered = false;
            }
            else if (currentMode == GuildViewMode.MyGuild)
            {
                isJoinButtonHovered = false;
            }
            else if (currentMode == GuildViewMode.GuildDetails)
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
            else if (currentMode == GuildViewMode.CreateGuild)
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
            if (currentMode == GuildViewMode.Browse && displayedGuilds.Count > 0)
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

        private void GuildFrame_MouseLeave(object? sender, EventArgs e)
        {
            hoveredGuildIndex = -1;
            isBackButtonHovered = false;
            isJoinButtonHovered = false;
            isLeaveButtonHovered = false;
            isCreateGuildButtonHovered = false;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (isProcessing) return;
            if (e.KeyChar == (char)Keys.Back)
            {
                if (isSearchFocused && searchQuery.Length > 0)
                    searchQuery = searchQuery.Substring(0, searchQuery.Length - 1);
                else if (isNameFocused && guildNameInput.Length > 0)
                    guildNameInput = guildNameInput.Substring(0, guildNameInput.Length - 1);
                else if (isDescriptionFocused && guildDescriptionInput.Length > 0)
                    guildDescriptionInput = guildDescriptionInput.Substring(0, guildDescriptionInput.Length - 1);
            }
            else if (!char.IsControl(e.KeyChar))
            {
                if (isSearchFocused && searchQuery.Length < 30)
                    searchQuery += e.KeyChar;
                else if (isNameFocused && guildNameInput.Length < 30)
                    guildNameInput += e.KeyChar;
                else if (isDescriptionFocused && guildDescriptionInput.Length < 150)
                    guildDescriptionInput += e.KeyChar;
            }
            this.Invalidate();
        }

        private async Task CreateGuildAsync()
        {
            string name = guildNameInput.Trim();
            string description = guildDescriptionInput.Trim();

            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                statusMessage = "Guild name must be at least 3 characters.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }

            if (string.IsNullOrEmpty(description))
            {
                statusMessage = "Please provide a description.";
                statusColor = Color.FromArgb(255, 120, 120);
                this.Invalidate();
                return;
            }

            isProcessing = true;
            statusMessage = "Creating guild...";
            statusColor = Color.White;
            this.Invalidate();

            try
            {
                var guild = await guildRepository.CreateGuildAsync(name, description, currentUser);
                if (guild?.Id != null)
                {
                    currentUser.GuildId = guild.Id;
                    currentUser.GuildName = guild.Name;
                    currentUser.GuildRole = GuildRole.Leader;

                    selectedGuild = guild;
                    currentMode = GuildViewMode.GuildDetails;
                    statusMessage = "Guild created successfully!";
                    statusColor = Color.FromArgb(120, 255, 120);
                }
                else
                {
                    statusMessage = "Error: Failed to create guild.";
                    statusColor = Color.FromArgb(255, 120, 120);
                }
            }
            catch (Exception ex)
            {
                statusMessage = "Error: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }

            isProcessing = false;
            this.Invalidate();
        }

        private async Task JoinGuildAsync()
        {
            if (selectedGuild == null || selectedGuild.Id == null) return;

            isProcessing = true;
            statusMessage = "Joining guild...";
            statusColor = Color.White;
            this.Invalidate();

            try
            {
                await guildRepository.JoinGuildAsync(selectedGuild.Id, currentUser);
                currentUser.GuildId = selectedGuild.Id;
                currentUser.GuildName = selectedGuild.Name;
                currentUser.GuildRole = GuildRole.Member;

                selectedGuild = await guildRepository.GetGuildByIdAsync(selectedGuild.Id);
                currentMode = GuildViewMode.MyGuild;
                statusMessage = "Joined guild successfully!";
                statusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                statusMessage = "Error: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }

            isProcessing = false;
            this.Invalidate();
        }

        private async Task LeaveGuildAsync()
        {
            isProcessing = true;
            statusMessage = "Leaving guild...";
            statusColor = Color.White;
            this.Invalidate();

            try
            {
                await guildRepository.LeaveGuildAsync(currentUser);
                currentUser.GuildId = null;
                currentUser.GuildName = null;
                currentUser.GuildRole = GuildRole.Member;

                currentMode = GuildViewMode.Browse;
                selectedGuild = null;
                displayedGuilds = await guildRepository.GetSearchableGuildsAsync(currentUser);
                statusMessage = "Left guild successfully!";
                statusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                statusMessage = "Error: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }

            isProcessing = false;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            CrushItStyleHelper.SetupQualityRendering(g);

            // Draw background
            CrushItStyleHelper.DrawCartoonBackground(g, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(g, backgroundParticles);

            // Draw title banner
            DrawTitleBanner(g);

            // Draw content based on current mode
            switch (currentMode)
            {
                case GuildViewMode.Browse:
                    DrawBrowseMode(g);
                    break;
                case GuildViewMode.MyGuild:
                    DrawMyGuildMode(g);
                    break;
                case GuildViewMode.CreateGuild:
                    DrawCreateGuildMode(g);
                    break;
                case GuildViewMode.GuildDetails:
                    DrawGuildDetailsMode(g);
                    break;
            }

            // Draw navigation bar
            CrushItStyleHelper.DrawNavigationBar(g, this.ClientSize.Width, this.ClientSize.Height, currentNav, pulsePhase);

            // Draw status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                using (Font statusFont = new Font("Segoe UI", 12, FontStyle.Bold))
                using (Brush statusBrush = new SolidBrush(statusColor))
                {
                    SizeF statusSize = g.MeasureString(statusMessage, statusFont);
                    g.DrawString(statusMessage, statusFont, statusBrush, 
                        (this.ClientSize.Width - statusSize.Width) / 2, 470);
                }
            }
        }

        private void DrawTitleBanner(Graphics g)
        {
            string title = currentMode switch
            {
                GuildViewMode.Browse => "GUILD HALL",
                GuildViewMode.MyGuild => "MY GUILD",
                GuildViewMode.CreateGuild => "CREATE GUILD",
                GuildViewMode.GuildDetails => "GUILD DETAILS",
                _ => "GUILDS"
            };
            CrushItStyleHelper.DrawTitleBanner(g, new Rectangle(250, 15, 400, 55), title);
        }



        private void DrawBrowseMode(Graphics g)
        {
            // Draw search panel with enhanced styling
            Rectangle searchPanel = new Rectangle(50, 85, 800, 60);
            CrushItStyleHelper.DrawPanel(g, searchPanel, 
                Color.FromArgb(255, 160, 120, 200), 
                Color.FromArgb(255, 120, 80, 160), 
                Color.FromArgb(255, 100, 60, 140));

            // Draw search bar with better positioning
            DrawInputBox(g, searchRect, searchQuery.Length == 0 ? "🔍 Search guilds..." : searchQuery, 
                isSearchFocused, isSearchFocused);

            // Draw decorative separator
            using (Pen separator = new Pen(Color.FromArgb(255, 255, 200, 120), 2))
            {
                g.DrawLine(separator, 50, 155, 850, 155);
            }

            // Draw guild list with enhanced cards
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
                // Draw enhanced no guilds message with icon
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
        }

        private void DrawMyGuildMode(Graphics g)
        {
            if (selectedGuild == null) return;

            // Draw guild info with enhanced styling
            DrawGuildDetails(g, selectedGuild, new Rectangle(50, 85, 800, 350));

            // Draw decorative separator
            using (Pen separator = new Pen(Color.FromArgb(255, 255, 200, 120), 2))
            {
                g.DrawLine(separator, 50, 445, 850, 445);
            }

            // Draw circular leave button
            DrawCircularButton(g, leaveButtonRect, "🚪", isLeaveButtonHovered, Color.FromArgb(255, 120, 80, 80));
        }

        private void DrawCreateGuildMode(Graphics g)
        {
            // Draw back button
            DrawButton(g, backButtonRect, "← Back", isBackButtonHovered);

            // Draw enhanced create panel
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

            // Draw name input with better styling
            DrawInputBox(g, nameInputRect, guildNameInput.Length == 0 ? "📝 Guild Name (3-30 chars)" : guildNameInput, 
                isNameFocused, isNameFocused);

            // Draw description input with better styling
            DrawMultiLineInputBox(g, descriptionInputRect, 
                guildDescriptionInput.Length == 0 ? "📋 Guild Description (max 150 chars)" : guildDescriptionInput, 
                isDescriptionFocused, isDescriptionFocused);

            // Draw enhanced create button
            DrawButton(g, createGuildButtonRect, "✨ Create Guild", isCreateGuildButtonHovered, 
                isProcessing ? Color.Gray : Color.FromArgb(100, 220, 100));
        }

        private void DrawGuildDetailsMode(Graphics g)
        {
            if (selectedGuild == null) return;

            // Draw back button
            DrawButton(g, backButtonRect, "← Back", isBackButtonHovered);

            // Draw guild info with enhanced positioning
            DrawGuildDetails(g, selectedGuild, new Rectangle(50, 85, 800, 350));

            // Draw action buttons
            bool isMember = !string.IsNullOrEmpty(currentUser.GuildId) && currentUser.GuildId == selectedGuild.Id;
            if (isMember)
            {
                // Member can leave guild
            }
            else
            {
                bool canJoin = selectedGuild.CanJoin(currentUser);
                DrawButton(g, joinButtonRect, canJoin ? "🤝 Join Guild" : "🔒 Cannot Join", 
                    isJoinButtonHovered, canJoin ? Color.FromArgb(100, 220, 100) : Color.Gray);
            }
        }

        private void DrawGuildCard(Graphics g, Guild guild, Rectangle rect, bool isHovered)
        {
            // Enhanced panel style with better colors
            CrushItStyleHelper.DrawPanel(g, rect, 
                isHovered ? Color.FromArgb(255, 180, 140, 220) : Color.FromArgb(255, 150, 110, 190),
                isHovered ? Color.FromArgb(255, 140, 100, 180) : Color.FromArgb(255, 110, 70, 150),
                isHovered ? Color.FromArgb(255, 120, 80, 160) : Color.FromArgb(255, 90, 50, 130));

            // Draw decorative accent bar
            using (SolidBrush accent = new SolidBrush(isHovered ? Color.FromArgb(255, 255, 200, 100) : Color.FromArgb(255, 255, 160, 80)))
            {
                g.FillRectangle(accent, rect.X + 5, rect.Y + 5, 4, rect.Height - 10);
            }

            // Draw guild name with better font
            using (Font nameFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
            using (Brush nameBrush = new SolidBrush(Color.White))
            {
                g.DrawString(guild.Name, nameFont, nameBrush, rect.X + 20, rect.Y + 10);
            }

            // Draw member count with icon
            using (Font infoFont = new Font("Segoe UI", 12, FontStyle.Bold))
            using (Brush infoBrush = new SolidBrush(Color.FromArgb(255, 230, 230, 255)))
            {
                g.DrawString($"👥 {guild.MemberCount}/{guild.MaxMembers}", infoFont, infoBrush, 
                    rect.X + 20, rect.Y + 38);
            }

            // Draw join status with badge styling
            string statusText = guild.JoinStatus.ToString();
            Color statusColor = guild.JoinStatus == GuildJoinStatus.Open ? Color.FromArgb(100, 255, 100) :
                               guild.JoinStatus == GuildJoinStatus.InviteOnly ? Color.FromArgb(255, 200, 100) :
                               Color.FromArgb(255, 100, 100);
            
            // Draw status badge
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

            // Draw required level with better styling
            using (Font levelFont = new Font("Segoe UI", 11, FontStyle.Bold))
            using (Brush levelBrush = new SolidBrush(Color.FromArgb(255, 200, 200, 220)))
            {
                g.DrawString($"⚔️ Lv.{guild.RequiredLevel}+", levelFont, levelBrush, rect.Right - 135, rect.Y + 35);
            }
        }

        private void DrawGuildDetails(Graphics g, Guild guild, Rectangle rect)
        {
            // Enhanced panel style with better colors
            CrushItStyleHelper.DrawPanel(g, rect, 
                Color.FromArgb(255, 160, 120, 200), 
                Color.FromArgb(255, 120, 80, 160), 
                Color.FromArgb(255, 100, 60, 140));

            // Draw decorative accent bar
            using (SolidBrush accent = new SolidBrush(Color.FromArgb(255, 255, 200, 100)))
            {
                g.FillRectangle(accent, rect.X + 5, rect.Y + 5, 4, rect.Height - 10);
            }

            // Draw guild name with icon
            using (Font nameFont = new Font("Comic Sans MS", 22, FontStyle.Bold))
            using (Brush nameBrush = new SolidBrush(Color.White))
            {
                g.DrawString($"🏰 {guild.Name}", nameFont, nameBrush, rect.X + 20, rect.Y + 15);
            }

            // Draw description with better styling
            using (Font descFont = new Font("Segoe UI", 12, FontStyle.Italic))
            using (Brush descBrush = new SolidBrush(Color.FromArgb(255, 240, 240, 255)))
            {
                g.DrawString(guild.Description, descFont, descBrush, rect.X + 20, rect.Y + 50);
            }

            // Draw stats section with enhanced styling
            Rectangle statsRect = new Rectangle(rect.X + 20, rect.Y + 90, 350, 200);
            CrushItStyleHelper.DrawPanel(g, statsRect, 
                Color.FromArgb(255, 140, 100, 180), 
                Color.FromArgb(255, 110, 70, 150), 
                Color.FromArgb(255, 90, 50, 130));

            // Draw stats section title
            using (Font statsTitleFont = new Font("Comic Sans MS", 14, FontStyle.Bold))
            using (Brush statsTitleBrush = new SolidBrush(Color.FromArgb(255, 255, 200, 100)))
            {
                g.DrawString("📊 Guild Stats", statsTitleFont, statsTitleBrush, statsRect.X + 15, statsRect.Y + 15);
            }

            // Draw stats with icons
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

            // Draw top members section with enhanced styling
            Rectangle membersRect = new Rectangle(rect.X + 390, rect.Y + 90, 370, 200);
            CrushItStyleHelper.DrawPanel(g, membersRect, 
                Color.FromArgb(255, 140, 100, 180), 
                Color.FromArgb(255, 110, 70, 150), 
                Color.FromArgb(255, 90, 50, 130));

            // Draw top members title
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

            // Draw button with gradient
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect, 
                isHovered ? Color.FromArgb(255, 140, 190, 240) : Color.FromArgb(255, 120, 170, 220),
                isHovered ? Color.FromArgb(255, 100, 150, 200) : Color.FromArgb(255, 80, 130, 180),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // Draw border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 200, 220, 255), 2))
            {
                g.DrawRectangle(borderPen, rect);
            }

            // Draw text
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

            // Draw circular background
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
            {
                g.FillEllipse(bgBrush, rect);
            }

            // Draw circular border
            using (Pen borderPen = new Pen(Color.FromArgb(255, 255, 255, 255), 2))
            {
                g.DrawEllipse(borderPen, rect);
            }

            // Draw emoji in center
            using (Font emojiFont = new Font("Segoe UI Emoji", 18, FontStyle.Bold))
            using (Brush emojiBrush = new SolidBrush(Color.White))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(emoji, emojiFont, emojiBrush, rect, format);
            }
        }
    }
}
