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
    public enum SocialViewMode
    {   
        Friends,
        Search,
        PendingRequests,
        SentRequests
    }

    public class SocialFrame : Form
    {
        private readonly UserAccount currentUser;
        private readonly IMongoDatabase database;
        private readonly SocialRepository socialRepository;

        private SocialViewMode currentMode = SocialViewMode.Friends;
        private List<Friend> friends = new List<Friend>();
        private List<FriendRequest> pendingRequests = new List<FriendRequest>();
        private List<FriendRequest> sentRequests = new List<FriendRequest>();
        private List<UserProfile> searchResults = new List<UserProfile>();

        private readonly NavItem currentNav = NavItem.Social;

        // UI State
        private string searchQuery = "";
        private string statusMessage = "";
        private Color statusColor = Color.White;
        private bool isProcessing = false;

        // Input rectangles
        private Rectangle searchRect;
        private Rectangle backButtonRect;
        private Rectangle searchButtonRect;
        private Rectangle friendsTabRect;
        private Rectangle searchTabRect;
        private Rectangle pendingTabRect;
        private Rectangle sentTabRect;

        // Hover states
        private bool isSearchFocused = false;
        private int hoveredUserIndex = -1;
        private bool isBackButtonHovered = false;
        private bool isSearchButtonHovered = false;
        private bool isFriendsTabHovered = false;
        private bool isSearchTabHovered = false;
        private bool isPendingTabHovered = false;
        private bool isSentTabHovered = false;

        // Background styling
        private List<StyleParticle> backgroundParticles = new List<StyleParticle>();
        private System.Windows.Forms.Timer animationTimer = null!;
        private Random particleRand = new Random();
        private int pulsePhase = 0;

        public SocialFrame(UserAccount user, IMongoDatabase db)
        {
            this.currentUser = user;
            this.database = db;
            this.socialRepository = new SocialRepository(db);

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            InitializeComponent();
            InitializeParticles();
            LoadInitialData();
            StartAnimation();

            SoundManager.StartBackgroundMusic();
            SoundManager.SetBackgroundMusicVolume(0.3f);

            MobileHelper.ApplyMobileScaling(this);
        }

        private void InitializeComponent()
        {
            this.Text = "Crush It! - Social";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.KeyPreview = true;
            this.KeyDown += SocialFrame_KeyDown;
            this.MouseClick += SocialFrame_MouseClick;
            this.MouseMove += SocialFrame_MouseMove;
            this.MouseLeave += SocialFrame_MouseLeave;
            this.FormClosed += (s, e) => 
            {
                animationTimer?.Stop();
                SoundManager.StopBackgroundMusic();
                if (Application.OpenForms.Count == 0)
                {
                    SoundManager.Cleanup();
                }
            };

            // Initialize rectangles
            searchRect = new Rectangle(80, 95, 500, 40);
            backButtonRect = new Rectangle(30, 30, 100, 35);
            searchButtonRect = new Rectangle(600, 95, 120, 40);
            
            // Tab rectangles
            int tabY = 60;
            int tabWidth = 200;
            int tabSpacing = 20;
            int startX = 50;
            
            friendsTabRect = new Rectangle(startX, tabY, tabWidth, 35);
            searchTabRect = new Rectangle(startX + tabWidth + tabSpacing, tabY, tabWidth, 35);
            pendingTabRect = new Rectangle(startX + (tabWidth + tabSpacing) * 2, tabY, tabWidth, 35);
            sentTabRect = new Rectangle(startX + (tabWidth + tabSpacing) * 3, tabY, tabWidth, 35);
        }

        private void InitializeParticles()
        {
            backgroundParticles.AddRange(CrushItStyleHelper.CreateParticles(particleRand, 45, 890, 80, 530));
        }

        private async void LoadInitialData()
        {
            try
            {
                await LoadFriends();
                await LoadPendingRequests();
                await LoadSentRequests();
                statusMessage = $"Loaded {friends.Count} friends";
                statusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                statusMessage = "Error loading social data: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }
            this.Invalidate();
        }

        private async Task LoadFriends()
        {
            friends = await socialRepository.GetFriendsAsync(currentUser.UserId);
        }

        private async Task LoadPendingRequests()
        {
            pendingRequests = await socialRepository.GetPendingFriendRequestsAsync(currentUser.UserId);
        }

        private async Task LoadSentRequests()
        {
            sentRequests = await socialRepository.GetSentFriendRequestsAsync(currentUser.UserId);
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

        private void SocialFrame_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                ReturnToMainFrame();
            }
        }

        private void ReturnToMainFrame()
        {
            MainFrame mainFrame = new MainFrame(currentUser, database);
            mainFrame.Show();
            this.Hide();
            this.Dispose();
        }

        private async void SocialFrame_MouseClick(object? sender, MouseEventArgs e)
        {
            if (isProcessing) return;

            SoundManager.PlaySound(SoundType.ButtonClick);

            // Check navbar clicks
            if (CrushItStyleHelper.TryGetNavClick(e.X, e.Y, this.ClientSize.Width, this.ClientSize.Height, out NavItem clickedNav))
            {
                if (clickedNav == NavItem.Home)
                {
                    SoundManager.PlaySound(SoundType.Navigation);
                    ReturnToMainFrame();
                }
                else if (clickedNav == NavItem.Levels)
                {
                    SoundManager.PlaySound(SoundType.Navigation);
                    ReturnToMainFrame();
                }
                else if (clickedNav == NavItem.Achievements)
                {
                    SoundManager.PlaySound(SoundType.Navigation);
                    ReturnToMainFrame();
                }
                return;
            }

            // Check tab clicks
            if (friendsTabRect.Contains(e.Location))
            {
                currentMode = SocialViewMode.Friends;
                this.Invalidate();
                return;
            }
            if (searchTabRect.Contains(e.Location))
            {
                currentMode = SocialViewMode.Search;
                this.Invalidate();
                return;
            }
            if (pendingTabRect.Contains(e.Location))
            {
                currentMode = SocialViewMode.PendingRequests;
                this.Invalidate();
                return;
            }
            if (sentTabRect.Contains(e.Location))
            {
                currentMode = SocialViewMode.SentRequests;
                this.Invalidate();
                return;
            }

            // Check search button
            if (searchButtonRect.Contains(e.Location) && currentMode == SocialViewMode.Search)
            {
                await PerformSearch();
                return;
            }

            // Handle user list clicks based on current mode
            HandleUserListClick(e);
        }

        private async void HandleUserListClick(MouseEventArgs e)
        {
            int startY = 120;
            int itemHeight = 60;
            int gap = 12;
            int startX = 50;
            int y = startY;

            var userList = currentMode switch
            {
                SocialViewMode.Friends => friends.Cast<object>().ToList(),
                SocialViewMode.Search => searchResults.Cast<object>().ToList(),
                SocialViewMode.PendingRequests => pendingRequests.Cast<object>().ToList(),
                SocialViewMode.SentRequests => sentRequests.Cast<object>().ToList(),
                _ => new List<object>()
            };

            for (int i = 0; i < userList.Count; i++)
            {
                Rectangle itemRect = new Rectangle(startX, y, 800, itemHeight);

                if (itemRect.Contains(e.Location))
                {
                    await HandleItemClick(i, currentMode);
                    break;
                }

                y += itemHeight + gap;
            }
        }

        private async Task HandleItemClick(int index, SocialViewMode mode)
        {
            try
            {
                isProcessing = true;
                this.Invalidate();

                switch (mode)
                {
                    case SocialViewMode.Friends:
                        await HandleFriendClick(index);
                        break;
                    case SocialViewMode.Search:
                        await HandleSearchResultClick(index);
                        break;
                    case SocialViewMode.PendingRequests:
                        await HandlePendingRequestClick(index);
                        break;
                    case SocialViewMode.SentRequests:
                        await HandleSentRequestClick(index);
                        break;
                }
            }
            catch (Exception ex)
            {
                statusMessage = "Error: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }
            finally
            {
                isProcessing = false;
                this.Invalidate();
            }
        }

        private async Task HandleFriendClick(int index)
        {
            var friend = friends[index];
            
            // Simple context menu logic - for now just remove friend
            var result = MessageBox.Show(
                $"Remove {friend.FriendUsername} from friends?",
                "Remove Friend",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await socialRepository.RemoveFriendAsync(currentUser.UserId, friend.FriendId);
                await LoadFriends();
                statusMessage = $"Removed {friend.FriendUsername} from friends";
                statusColor = Color.FromArgb(120, 255, 120);
            }
        }

        private async Task HandleSearchResultClick(int index)
        {
            var user = searchResults[index];
            
            var result = MessageBox.Show(
                $"Send friend request to {user.Username}?",
                "Send Friend Request",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await socialRepository.SendFriendRequestAsync(
                    currentUser.UserId, 
                    currentUser.Username, 
                    user.UserId, 
                    user.Username);
                
                await LoadSentRequests();
                statusMessage = $"Friend request sent to {user.Username}";
                statusColor = Color.FromArgb(120, 255, 120);
            }
        }

        private async Task HandlePendingRequestClick(int index)
        {
            var request = pendingRequests[index];
            
            var result = MessageBox.Show(
                $"Accept friend request from {request.FromUsername}?",
                "Accept Friend Request",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await socialRepository.AcceptFriendRequestAsync(currentUser.UserId, request.FromUserId);
                await LoadFriends();
                await LoadPendingRequests();
                statusMessage = $"You are now friends with {request.FromUsername}";
                statusColor = Color.FromArgb(120, 255, 120);
            }
            else if (result == DialogResult.No)
            {
                await socialRepository.DeclineFriendRequestAsync(currentUser.UserId, request.FromUserId);
                await LoadPendingRequests();
                statusMessage = $"Declined friend request from {request.FromUsername}";
                statusColor = Color.FromArgb(255, 200, 120);
            }
        }

        private async Task HandleSentRequestClick(int index)
        {
            var request = sentRequests[index];
            
            var result = MessageBox.Show(
                $"Cancel friend request to {request.ToUsername}?",
                "Cancel Friend Request",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                await socialRepository.DeclineFriendRequestAsync(request.ToUserId, currentUser.UserId);
                await LoadSentRequests();
                statusMessage = $"Cancelled friend request to {request.ToUsername}";
                statusColor = Color.FromArgb(255, 200, 120);
            }
        }

        private async Task PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                statusMessage = "Please enter a search query";
                statusColor = Color.FromArgb(255, 200, 120);
                this.Invalidate();
                return;
            }

            try
            {
                isProcessing = true;
                this.Invalidate();

                searchResults = await socialRepository.SearchUsersAsync(searchQuery, currentUser.UserId);
                statusMessage = $"Found {searchResults.Count} users";
                statusColor = Color.FromArgb(120, 255, 120);
            }
            catch (Exception ex)
            {
                statusMessage = "Search error: " + ex.Message;
                statusColor = Color.FromArgb(255, 120, 120);
            }
            finally
            {
                isProcessing = false;
                this.Invalidate();
            }
        }

        private void SocialFrame_MouseMove(object? sender, MouseEventArgs e)
        {
            bool wasBackHovered = isBackButtonHovered;
            bool wasSearchHovered = isSearchButtonHovered;
            bool wasFriendsTabHovered = isFriendsTabHovered;
            bool wasSearchTabHovered = isSearchTabHovered;
            bool wasPendingTabHovered = isPendingTabHovered;
            bool wasSentTabHovered = isSentTabHovered;

            isBackButtonHovered = backButtonRect.Contains(e.Location);
            isSearchButtonHovered = searchButtonRect.Contains(e.Location);
            isFriendsTabHovered = friendsTabRect.Contains(e.Location);
            isSearchTabHovered = searchTabRect.Contains(e.Location);
            isPendingTabHovered = pendingTabRect.Contains(e.Location);
            isSentTabHovered = sentTabRect.Contains(e.Location);

            // Check hover over user list items
            int startY = 120;
            int itemHeight = 60;
            int gap = 12;
            int startX = 50;
            int y = startY;
            
            var userList = currentMode switch
            {
                SocialViewMode.Friends => friends.Cast<object>().ToList(),
                SocialViewMode.Search => searchResults.Cast<object>().ToList(),
                SocialViewMode.PendingRequests => pendingRequests.Cast<object>().ToList(),
                SocialViewMode.SentRequests => sentRequests.Cast<object>().ToList(),
                _ => new List<object>()
            };

            hoveredUserIndex = -1;
            for (int i = 0; i < userList.Count; i++)
            {
                Rectangle itemRect = new Rectangle(startX, y, 800, itemHeight);
                if (itemRect.Contains(e.Location))
                {
                    hoveredUserIndex = i;
                    break;
                }
                y += itemHeight + gap;
            }

            this.Cursor = (isBackButtonHovered || isSearchButtonHovered || isFriendsTabHovered || 
                          isSearchTabHovered || isPendingTabHovered || isSentTabHovered || 
                          hoveredUserIndex >= 0) ? Cursors.Hand : Cursors.Default;

            if (wasBackHovered != isBackButtonHovered || wasSearchHovered != isSearchButtonHovered ||
                wasFriendsTabHovered != isFriendsTabHovered || wasSearchTabHovered != isSearchTabHovered ||
                wasPendingTabHovered != isPendingTabHovered || wasSentTabHovered != isSentTabHovered)
            {
                this.Invalidate();
            }
        }

        private void SocialFrame_MouseLeave(object? sender, EventArgs e)
        {
            isBackButtonHovered = false;
            isSearchButtonHovered = false;
            isFriendsTabHovered = false;
            isSearchTabHovered = false;
            isPendingTabHovered = false;
            isSentTabHovered = false;
            hoveredUserIndex = -1;
            this.Cursor = Cursors.Default;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            CrushItStyleHelper.SetupQualityRendering(e.Graphics);

            // Draw background
            CrushItStyleHelper.DrawCartoonBackground(e.Graphics, this.ClientRectangle, pulsePhase);
            CrushItStyleHelper.DrawBackgroundParticles(e.Graphics, backgroundParticles);

            // Draw title
            Rectangle titleRect = new Rectangle(50, 15, 300, 40);
            CrushItStyleHelper.DrawTitleBanner(e.Graphics, titleRect, "SOCIAL");

            // Draw tabs
            DrawTabs(e.Graphics);

            // Draw content based on current mode
            switch (currentMode)
            {
                case SocialViewMode.Friends:
                    DrawFriendsList(e.Graphics);
                    break;
                case SocialViewMode.Search:
                    DrawSearchInterface(e.Graphics);
                    break;
                case SocialViewMode.PendingRequests:
                    DrawPendingRequests(e.Graphics);
                    break;
                case SocialViewMode.SentRequests:
                    DrawSentRequests(e.Graphics);
                    break;
            }

            // Draw status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                using (Font statusFont = new Font("Arial", 12))
                using (SolidBrush statusBrush = new SolidBrush(statusColor))
                {
                    e.Graphics.DrawString(statusMessage, statusFont, statusBrush, 50, this.ClientSize.Height - 120);
                }
            }

            // Draw navbar
            CrushItStyleHelper.DrawNavigationBar(e.Graphics, this.ClientSize.Width, this.ClientSize.Height, currentNav, pulsePhase);
        }

        private void DrawTabs(Graphics g)
        {
            Color activeColor = Color.FromArgb(255, 100, 200, 100);
            Color inactiveColor = Color.FromArgb(255, 80, 80, 120);
            Color hoverColor = Color.FromArgb(255, 100, 100, 140);

            DrawTab(g, friendsTabRect, "FRIENDS", currentMode == SocialViewMode.Friends, isFriendsTabHovered, activeColor, inactiveColor, hoverColor);
            DrawTab(g, searchTabRect, "SEARCH", currentMode == SocialViewMode.Search, isSearchTabHovered, activeColor, inactiveColor, hoverColor);
            DrawTab(g, pendingTabRect, "INCOMING", currentMode == SocialViewMode.PendingRequests, isPendingTabHovered, activeColor, inactiveColor, hoverColor);
            DrawTab(g, sentTabRect, "OUTGOING", currentMode == SocialViewMode.SentRequests, isSentTabHovered, activeColor, inactiveColor, hoverColor);
        }

        private void DrawTab(Graphics g, Rectangle rect, string text, bool isActive, bool isHovered, Color activeColor, Color inactiveColor, Color hoverColor)
        {
            Color bgColor = isActive ? activeColor : (isHovered ? hoverColor : inactiveColor);
            
            using (SolidBrush brush = new SolidBrush(bgColor))
            {
                g.FillRectangle(brush, rect);
            }

            using (Font font = new Font("Arial", 11, isActive ? FontStyle.Bold : FontStyle.Regular))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }

        private void DrawFriendsList(Graphics g)
        {
            if (friends.Count == 0)
            {
                DrawEmptyState(g, "No friends yet", "Search for users to add friends!");
                return;
            }

            int startY = 120;
            int itemHeight = 60;
            int gap = 12;
            int startX = 50;
            int y = startY;

            for (int i = 0; i < friends.Count; i++)
            {
                var friend = friends[i];
                Rectangle itemRect = new Rectangle(startX, y, 800, itemHeight);
                
                Color bgColor = i == hoveredUserIndex ? Color.FromArgb(255, 100, 100, 140) : Color.FromArgb(200, 70, 70, 100);
                DrawUserItem(g, itemRect, friend.FriendUsername, $"Matches: {friend.TotalMatches} | Best: {friend.HighestScore}", bgColor);
                
                y += itemHeight + gap;
            }
        }

        private void DrawSearchInterface(Graphics g)
        {
            // Draw search box
            Color searchColor = isSearchFocused ? Color.FromArgb(255, 173, 216, 230) : Color.White;
            using (SolidBrush brush = new SolidBrush(searchColor))
            {
                g.FillRectangle(brush, searchRect);
            }

            using (Pen pen = new Pen(Color.FromArgb(255, 100, 100, 150), 2))
            {
                g.DrawRectangle(pen, searchRect);
            }

            using (Font font = new Font("Arial", 12))
            using (SolidBrush textBrush = new SolidBrush(Color.Black))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                string displayText = string.IsNullOrEmpty(searchQuery) ? "Search users..." : searchQuery;
                g.DrawString(displayText, font, textBrush, searchRect.X + 10, searchRect.Y + searchRect.Height / 2, sf);
            }

            // Draw search button
            Color buttonColor = isSearchButtonHovered ? Color.FromArgb(255, 100, 200, 100) : Color.FromArgb(255, 80, 180, 80);
            using (SolidBrush brush = new SolidBrush(buttonColor))
            {
                g.FillRectangle(brush, searchButtonRect);
            }

            using (Font font = new Font("Arial", 11, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("SEARCH", font, textBrush, searchButtonRect, sf);
            }

            // Draw search results
            if (searchResults.Count == 0 && !string.IsNullOrEmpty(searchQuery))
            {
                DrawEmptyState(g, "No results found", "Try a different search term");
            }
            else if (searchResults.Count > 0)
            {
                int startY = 150;
                int itemHeight = 60;
                int gap = 12;
                int startX = 50;
                int y = startY;

                for (int i = 0; i < searchResults.Count; i++)
                {
                    var user = searchResults[i];
                    Rectangle itemRect = new Rectangle(startX, y, 800, itemHeight);
                    
                    Color bgColor = i == hoveredUserIndex ? Color.FromArgb(255, 100, 100, 140) : Color.FromArgb(200, 70, 70, 100);
                    DrawUserItem(g, itemRect, user.Username, $"Matches: {user.TotalMatches} | Best: {user.HighestScore} | Gold: {user.Gold}", bgColor);
                    
                    y += itemHeight + gap;
                }
            }
        }

        private void DrawPendingRequests(Graphics g)
        {
            if (pendingRequests.Count == 0)
            {
                DrawEmptyState(g, "No pending requests", "When someone sends you a friend request, it will appear here");
                return;
            }

            int startY = 120;
            int itemHeight = 60;
            int gap = 12;
            int startX = 50;
            int y = startY;

            for (int i = 0; i < pendingRequests.Count; i++)
            {
                var request = pendingRequests[i];
                Rectangle itemRect = new Rectangle(startX, y, 800, itemHeight);
                
                Color bgColor = i == hoveredUserIndex ? Color.FromArgb(255, 100, 100, 140) : Color.FromArgb(200, 70, 70, 100);
                DrawUserItem(g, itemRect, request.FromUsername, "Wants to be your friend! Click to accept/decline", bgColor);
                
                y += itemHeight + gap;
            }
        }

        private void DrawSentRequests(Graphics g)
        {
            if (sentRequests.Count == 0)
            {
                DrawEmptyState(g, "No sent requests", "Friend requests you send will appear here");
                return;
            }

            int startY = 120;
            int itemHeight = 60;
            int gap = 12;
            int startX = 50;
            int y = startY;

            for (int i = 0; i < sentRequests.Count; i++)
            {
                var request = sentRequests[i];
                Rectangle itemRect = new Rectangle(startX, y, 800, itemHeight);
                
                Color bgColor = i == hoveredUserIndex ? Color.FromArgb(255, 100, 100, 140) : Color.FromArgb(200, 70, 70, 100);
                DrawUserItem(g, itemRect, request.ToUsername, "Pending... Click to cancel", bgColor);
                
                y += itemHeight + gap;
            }
        }

        private void DrawUserItem(Graphics g, Rectangle rect, string username, string details, Color bgColor)
        {
            using (SolidBrush brush = new SolidBrush(bgColor))
            {
                g.FillRectangle(brush, rect);
            }

            using (Pen pen = new Pen(Color.FromArgb(255, 100, 100, 150), 2))
            {
                g.DrawRectangle(pen, rect);
            }

            using (Font nameFont = new Font("Arial", 14, FontStyle.Bold))
            using (Font detailsFont = new Font("Arial", 10))
            using (SolidBrush nameBrush = new SolidBrush(Color.White))
            using (SolidBrush detailsBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                g.DrawString(username, nameFont, nameBrush, rect.X + 15, rect.Y + 10);
                g.DrawString(details, detailsFont, detailsBrush, rect.X + 15, rect.Y + 35);
            }
        }

        private void DrawEmptyState(Graphics g, string title, string message)
        {
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;

            using (Font titleFont = new Font("Arial", 18, FontStyle.Bold))
            using (Font messageFont = new Font("Arial", 12))
            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            using (SolidBrush messageBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(title, titleFont, titleBrush, centerX, centerY - 20, sf);
                g.DrawString(message, messageFont, messageBrush, centerX, centerY + 20, sf);
            }
        }
    }
}