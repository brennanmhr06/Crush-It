using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CrushIt.UI
{
    public enum NavItem { Home, Levels, Achievements, Guilds }

    public class StyleParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float SpeedX { get; set; }
        public float SpeedY { get; set; }
        public int Size { get; set; }
        public int Alpha { get; set; }
        public Color ParticleColor { get; set; }
    }

    public static class CrushItStyleHelper
    {
        private static string[] navLabels = { "HOME", "LEVELS", "ACHIEVES", "GUILDS" };

        public static readonly Color[] ParticleColors = {
            Color.FromArgb(235, 45, 75),
            Color.FromArgb(35, 165, 245),
            Color.FromArgb(255, 215, 35),
            Color.FromArgb(45, 205, 85),
            Color.FromArgb(175, 75, 215),
            Color.FromArgb(255, 255, 255)
        };

        public static void SetupQualityRendering(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }

        public static List<StyleParticle> CreateParticles(Random rand, int count, int width, int minY, int maxY)
        {
            var particles = new List<StyleParticle>();
            for (int i = 0; i < count; i++)
            {
                Color baseColor = ParticleColors[rand.Next(ParticleColors.Length)];
                particles.Add(new StyleParticle
                {
                    X = rand.Next(10, width - 10),
                    Y = rand.Next(minY, maxY),
                    SpeedX = (float)(rand.NextDouble() * 1.2 - 0.6),
                    SpeedY = (float)(rand.NextDouble() * 1.2 - 0.6),
                    Size = rand.Next(2, 6),
                    Alpha = rand.Next(15, 45),
                    ParticleColor = baseColor
                });
            }
            return particles;
        }

        public static void UpdateParticles(List<StyleParticle> particles, int width, int minY, int maxY)
        {
            foreach (var p in particles)
            {
                p.X += p.SpeedX;
                p.Y += p.SpeedY;
                if (p.X < 0) p.X = width;
                if (p.X > width) p.X = 0;
                if (p.Y < minY) p.Y = maxY;
                if (p.Y > maxY) p.Y = minY;
            }
        }

        public static void DrawCartoonBackground(Graphics g, Rectangle bounds, int pulsePhase)
        {
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(255, 110, 60, 170),
                Color.FromArgb(255, 70, 35, 120),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            Color[] stripeColors = {
                Color.FromArgb(255, 255, 160, 90),
                Color.FromArgb(255, 160, 230, 255),
                Color.FromArgb(255, 255, 230, 110),
                Color.FromArgb(255, 190, 255, 160),
                Color.FromArgb(255, 230, 160, 255)
            };

            int segmentWidth = 48;
            for (int i = 0; i < bounds.Width / segmentWidth + 2; i++)
            {
                int segmentX = (pulsePhase * 2 + i * segmentWidth) % (bounds.Width + segmentWidth) - segmentWidth;
                int waveOffset = (int)(8 * Math.Sin(i * 0.3 + pulsePhase * 0.04));
                int waveY = bounds.Y + 40 + waveOffset;

                using (SolidBrush stripe = new SolidBrush(Color.FromArgb(35, stripeColors[i % stripeColors.Length])))
                {
                    g.FillRectangle(stripe, bounds.X + segmentX + 4, waveY, segmentWidth - 8, 12);
                }
            }
        }

        public static void DrawBackgroundParticles(Graphics g, IEnumerable<StyleParticle> particles)
        {
            foreach (var p in particles)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(p.Alpha, p.ParticleColor)))
                {
                    g.FillEllipse(brush, p.X, p.Y, p.Size, p.Size);
                }
            }
        }

        public static void DrawPanel(Graphics g, Rectangle rect, Color topColor, Color bottomColor, Color borderColor)
        {
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                g.FillRectangle(shadow, rect.X + 5, rect.Y + 5, rect.Width, rect.Height);

            using (SolidBrush border = new SolidBrush(borderColor))
                g.FillRectangle(border, rect);

            Rectangle inner = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
            using (LinearGradientBrush fill = new LinearGradientBrush(inner, topColor, bottomColor, LinearGradientMode.Vertical))
                g.FillRectangle(fill, inner);

            using (SolidBrush hi = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            {
                g.FillRectangle(hi, inner.X, inner.Y, inner.Width, 5);
                g.FillRectangle(hi, inner.X, inner.Y, 5, inner.Height);
            }
        }

        public static void DrawOutlinedText(Graphics g, string text, Font font, Rectangle bounds, Color fill, Color outline, int offset, StringFormat? sf = null)
        {
            sf ??= new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (SolidBrush outlineBrush = new SolidBrush(outline))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            {
                for (int dx = -offset; dx <= offset; dx++)
                {
                    for (int dy = -offset; dy <= offset; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        g.DrawString(text, font, outlineBrush, new RectangleF(bounds.X + dx, bounds.Y + dy, bounds.Width, bounds.Height), sf);
                    }
                }
                g.DrawString(text, font, fillBrush, bounds, sf);
            }
        }

        public static void DrawTitleBanner(Graphics g, Rectangle rect, string title, int titleFontSize = 22)
        {
            DrawPanel(g, rect, Color.FromArgb(255, 255, 140, 60), Color.FromArgb(255, 255, 100, 30), Color.FromArgb(255, 80, 40));
            using (Font titleFont = new Font("Comic Sans MS", titleFontSize, FontStyle.Bold))
            {
                DrawOutlinedText(g, title, titleFont, rect, Color.White, Color.Black, 2);
            }
        }

        public static bool TryGetNavClick(int clickX, int clickY, int clientWidth, int clientHeight, out NavItem nav)
        {
            int navY = clientHeight - 90;
            int navHeight = 80;
            int navWidth = clientWidth / 4;

            if (clickY >= navY && clickY <= navY + navHeight)
            {
                int navIndex = clickX / navWidth;
                if (navIndex >= 0 && navIndex < 4)
                {
                    nav = (NavItem)navIndex;
                    return true;
                }
            }

            nav = NavItem.Home;
            return false;
        }

        public static void DrawNavigationBar(Graphics g, int clientWidth, int clientHeight, NavItem currentNav, int pulsePhase)
        {
            int navY = clientHeight - 90;
            int navHeight = 80;
            int navWidth = clientWidth / 4;

            NavItem[] items = { NavItem.Home, NavItem.Levels, NavItem.Achievements, NavItem.Guilds };

            for (int i = 0; i < 4; i++)
            {
                int itemX = i * navWidth;
                int centerX = itemX + navWidth / 2;
                int centerY = navY + navHeight / 2;

                if (items[i] == currentNav)
                {
                    int glowPulse = (int)(15 * Math.Sin(pulsePhase * Math.PI / 60));
                    using (SolidBrush glow = new SolidBrush(Color.FromArgb(60 + glowPulse, 255, 200, 100)))
                        g.FillRectangle(glow, itemX + 2, navY + 2, navWidth - 4, navHeight - 4);
                }

                Rectangle itemRect = new Rectangle(itemX + 5, navY + 5, navWidth - 10, navHeight - 10);
                Color itemBgTop = items[i] == currentNav
                    ? Color.FromArgb(255, 200, 160, 240)
                    : Color.FromArgb(255, 120, 100, 160);
                Color itemBgBottom = items[i] == currentNav
                    ? Color.FromArgb(255, 160, 120, 200)
                    : Color.FromArgb(255, 80, 60, 120);

                using (LinearGradientBrush itemBg = new LinearGradientBrush(itemRect, itemBgTop, itemBgBottom, LinearGradientMode.Vertical))
                    g.FillRectangle(itemBg, itemRect);

                Color borderColor = items[i] == currentNav
                    ? Color.FromArgb(255, 255, 220, 120)
                    : Color.FromArgb(255, 100, 80, 140);

                using (SolidBrush border = new SolidBrush(borderColor))
                {
                    g.FillRectangle(border, itemX + 5, navY + 5, navWidth - 10, 4);
                    g.FillRectangle(border, itemX + 5, navY + 5, 4, navHeight - 10);
                    g.FillRectangle(border, itemX + 5, navY + navHeight - 9, navWidth - 10, 4);
                    g.FillRectangle(border, itemX + navWidth - 9, navY + 5, 4, navHeight - 10);
                }

                if (items[i] == currentNav)
                {
                    using (SolidBrush highlight = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    {
                        g.FillRectangle(highlight, itemX + 9, navY + 9, navWidth - 18, 4);
                        g.FillRectangle(highlight, itemX + 9, navY + 9, 4, navHeight - 18);
                    }

                    for (int d = 0; d < 4; d++)
                    {
                        int dotX = itemX + 15 + d * 20 + (int)(3 * Math.Sin(pulsePhase * 0.1 + d));
                        int dotY = navY + 15 + (int)(2 * Math.Cos(pulsePhase * 0.1 + d));
                        using (SolidBrush dot = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                            g.FillEllipse(dot, dotX, dotY, 4, 4);
                    }
                }

                DrawRainbowPixelText(g, navLabels[i], centerX, centerY, items[i] == currentNav);
            }
        }

        public static void DrawRainbowPixelText(Graphics g, string text, int cx, int cy, bool isActive)
        {
            Color[] rainbowColors = [
                Color.FromArgb(255, 255, 100, 100),
                Color.FromArgb(255, 255, 200, 100),
                Color.FromArgb(255, 100, 255, 100),
                Color.FromArgb(255, 100, 200, 255),
                Color.FromArgb(255, 200, 100, 255),
                Color.FromArgb(255, 255, 100, 200)
            ];

            using (Font pixelFont = new Font("Impact", 16, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // Draw thick black outline
                for (int dx = -3; dx <= 3; dx++)
                {
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int alpha = isActive ? 200 : 120;
                        g.DrawString(text, pixelFont, new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)), cx + dx, cy + dy, sf);
                    }
                }

                // Draw white inner outline for pop
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int alpha = isActive ? 150 : 80;
                        g.DrawString(text, pixelFont, new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)), cx + dx, cy + dy, sf);
                    }
                }

                // Draw rainbow text character by character
                for (int i = 0; i < text.Length; i++)
                {
                    Color charColor = rainbowColors[i % rainbowColors.Length];
                    if (!isActive)
                    {
                        charColor = Color.FromArgb(140, charColor.R, charColor.G, charColor.B);
                    }

                    char c = text[i];
                    SizeF charSize = g.MeasureString(c.ToString(), pixelFont);
                    float charX = cx - (text.Length * charSize.Width / 2) + (i * charSize.Width);
                    float charY = cy - charSize.Height / 2;

                    g.DrawString(c.ToString(), pixelFont, new SolidBrush(charColor), charX, charY);
                }

                // Add highlight effect for active items
                if (isActive)
                {
                    using (SolidBrush highlight = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                    {
                        g.DrawString(text, pixelFont, highlight, cx - 2, cy - 2, sf);
                    }
                }
            }
        }

        public static void DrawStatCard(Graphics g, Rectangle rect, string icon, string label, string value, Color accent)
        {
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                g.FillRectangle(shadow, rect.X + 3, rect.Y + 3, rect.Width, rect.Height);

            Rectangle inner = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            using (LinearGradientBrush bg = new LinearGradientBrush(
                inner, Color.FromArgb(255, 70, 45, 110), Color.FromArgb(255, 50, 30, 90), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, inner);
            }

            using (SolidBrush accentBar = new SolidBrush(accent))
                g.FillRectangle(accentBar, inner.X, inner.Y, 5, inner.Height);

            using (Font iconFont = new Font("Segoe UI Emoji", 18))
                g.DrawString(icon, iconFont, Brushes.White, inner.X + 12, inner.Y + 10);

            using (Font labelFont = new Font("Comic Sans MS", 10, FontStyle.Bold))
                g.DrawString(label, labelFont, new SolidBrush(Color.FromArgb(200, 220, 220, 240)), inner.X + 50, inner.Y + 12);

            using (Font valueFont = new Font("Comic Sans MS", 18, FontStyle.Bold))
            {
                DrawOutlinedText(g, value, valueFont, new Rectangle(inner.X + 48, inner.Y + 32, inner.Width - 55, 40), Color.White, Color.Black, 1);
            }
        }

        public static void DrawProgressBar(Graphics g, Rectangle barRect, string label, int current, int max, Color fillColor)
        {
            using (Font labelFont = new Font("Comic Sans MS", 9, FontStyle.Bold))
                g.DrawString(label, labelFont, Brushes.White, barRect.X, barRect.Y - 14);

            using (SolidBrush track = new SolidBrush(Color.FromArgb(180, 30, 20, 50)))
                g.FillRectangle(track, barRect);

            float pct = max > 0 ? Math.Min(1f, (float)current / max) : 0f;
            if (pct > 0)
            {
                Rectangle fillRect = new Rectangle(barRect.X, barRect.Y, (int)(barRect.Width * pct), barRect.Height);
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    fillRect, fillColor, Color.FromArgb(Math.Max(0, fillColor.R - 40), Math.Max(0, fillColor.G - 40), Math.Max(0, fillColor.B - 40)), LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(fill, fillRect);
                }
            }

            using (Pen border = new Pen(Color.FromArgb(200, 255, 255, 255), 2))
                g.DrawRectangle(border, barRect);

            string pctText = max > 0 ? $"{current}/{max}" : "0/0";
            using (Font pctFont = new Font("Comic Sans MS", 8, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(pctText, pctFont, Brushes.White, barRect, sf);
            }
        }
    }
}
