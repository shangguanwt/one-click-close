using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OneClickClose.Core;

namespace OneClickClose
{
    public sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color FillColor2 { get; set; }
        public Color BorderColor { get; set; }
        public Color ShadowColor { get; set; }
        public bool DrawShadow { get; set; }
        public bool DrawHighlight { get; set; }
        public bool UseGradient { get; set; }
        public LinearGradientMode GradientMode { get; set; }

        public RoundedPanel()
        {
            Radius = 8;
            FillColor = Theme.Card;
            FillColor2 = Theme.Card;
            BorderColor = Theme.Border;
            ShadowColor = Color.FromArgb(26, 0, 0, 0);
            DrawShadow = false;
            DrawHighlight = false;
            UseGradient = false;
            GradientMode = LinearGradientMode.Vertical;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent != null)
            {
                using (SolidBrush brush = new SolidBrush(Parent.BackColor))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }
            }
            else
            {
                base.OnPaintBackground(e);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle mainRect = DrawShadow
                ? new Rectangle(1, 1, Width - 4, Height - 5)
                : new Rectangle(0, 0, Width - 1, Height - 1);

            if (DrawShadow)
            {
                Rectangle shadowRect = new Rectangle(3, 4, Width - 6, Height - 7);
                if (shadowRect.Width > 0 && shadowRect.Height > 0)
                {
                    using (GraphicsPath shadowPath = RoundedPath(shadowRect, Radius + 2))
                    using (SolidBrush shadowBrush = new SolidBrush(ShadowColor))
                    {
                        e.Graphics.FillPath(shadowBrush, shadowPath);
                    }
                }
            }

            if (mainRect.Width > 0 && mainRect.Height > 0)
            {
                using (GraphicsPath path = RoundedPath(mainRect, Radius))
                using (Pen pen = new Pen(BorderColor))
                {
                    if (UseGradient)
                    {
                        using (LinearGradientBrush fill = new LinearGradientBrush(mainRect, FillColor, FillColor2, GradientMode))
                        {
                            e.Graphics.FillPath(fill, path);
                        }
                    }
                    else
                    {
                        using (SolidBrush fill = new SolidBrush(FillColor))
                        {
                            e.Graphics.FillPath(fill, path);
                        }
                    }

                    if (DrawHighlight)
                    {
                        using (Pen highlight = new Pen(Color.FromArgb(16, Color.White)))
                        {
                            e.Graphics.DrawLine(highlight, mainRect.Left + Radius, mainRect.Top + 1, mainRect.Right - Radius, mainRect.Top + 1);
                        }
                    }

                    e.Graphics.DrawPath(pen, path);
                }
            }

            base.OnPaint(e);
        }

        internal static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public sealed class ModernButton : Button
    {
        private bool hovering;
        private bool pressing;

        public int Radius { get; set; }
        public Color BaseColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color TextColor { get; set; }
        public Color BorderColor { get; set; }
        public bool DrawBorder { get; set; }

        public ModernButton()
        {
            Radius = 6;
            BaseColor = Theme.Card;
            HoverColor = Theme.RowHover;
            PressedColor = Theme.SecondaryPanel;
            TextColor = Theme.Text;
            BorderColor = Theme.ButtonBorder;
            DrawBorder = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressing = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            pressing = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressing = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = Enabled ? BaseColor : Theme.SecondaryPanel;
            if (Enabled && pressing)
            {
                color = PressedColor;
            }
            else if (Enabled && hovering)
            {
                color = HoverColor;
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPanel.RoundedPath(rect, Radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pevent.Graphics.FillPath(brush, path);
                if (DrawBorder)
                {
                    using (Pen pen = new Pen(BorderColor))
                    {
                        pevent.Graphics.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                rect,
                Enabled ? TextColor : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public sealed class PillLabel : Label
    {
        public Color FillColor { get; set; }
        public Color TextColor { get; set; }
        public Color BorderColor { get; set; }
        public int Radius { get; set; }

        public PillLabel()
        {
            FillColor = Theme.SecondaryPanel;
            TextColor = Theme.Text;
            BorderColor = Theme.Border;
            Radius = 10;
            TextAlign = ContentAlignment.MiddleCenter;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPanel.RoundedPath(rect, Radius))
            using (SolidBrush fill = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, rect, TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public sealed class ProcessGridView : DataGridView
    {
        private int hotRow = -1;

        public Color HeaderBackColor { get; set; }
        public Color RowBackColor { get; set; }
        public Color AlternateRowBackColor { get; set; }
        public Color HotRowBackColor { get; set; }
        public Color DividerColor { get; set; }
        public Color BodyTextColor { get; set; }
        public Color MutedTextColor { get; set; }
        public Color HighRiskTextColor { get; set; }
        public Color AccentBlue { get; set; }
        public Color AccentOrange { get; set; }
        public Color AccentPurple { get; set; }

        public ProcessGridView()
        {
            HeaderBackColor = Theme.Card;
            RowBackColor = Theme.CardSoft;
            AlternateRowBackColor = Theme.RowAlt;
            HotRowBackColor = Theme.RowHover;
            DividerColor = Theme.Border;
            BodyTextColor = Theme.Text;
            MutedTextColor = Theme.Muted;
            HighRiskTextColor = Color.FromArgb(120, 128, 142);
            AccentBlue = Theme.Primary;
            AccentOrange = Theme.Force;
            AccentPurple = Theme.Purple;

            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            AllowUserToResizeRows = false;
            AllowUserToResizeColumns = true;
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            BackgroundColor = Theme.Card;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.None;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            ColumnHeadersHeight = 36;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            DoubleBuffered = true;
            EnableHeadersVisualStyles = false;
            GridColor = DividerColor;
            MultiSelect = false;
            ReadOnly = true;
            RowHeadersVisible = false;
            RowTemplate.Height = 36;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            ShowCellErrors = false;
            ShowEditingIcon = false;
            ShowRowErrors = false;

            ColumnHeadersDefaultCellStyle.BackColor = Theme.Card;
            ColumnHeadersDefaultCellStyle.ForeColor = Theme.TitleText;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Card;
            ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.TitleText;
            ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 0, 0);

            DefaultCellStyle.BackColor = RowBackColor;
            DefaultCellStyle.ForeColor = BodyTextColor;
            DefaultCellStyle.SelectionBackColor = Theme.RowHover;
            DefaultCellStyle.SelectionForeColor = Theme.TitleText;
            DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F);
            DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            AlternatingRowsDefaultCellStyle.BackColor = AlternateRowBackColor;
        }

        protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                SetHotRow(e.RowIndex);
            }
            base.OnCellMouseEnter(e);
        }

        protected override void OnCellMouseLeave(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == hotRow)
            {
                SetHotRow(-1);
            }
            base.OnCellMouseLeave(e);
        }

        private void SetHotRow(int rowIndex)
        {
            if (hotRow >= 0 && hotRow < Rows.Count)
            {
                Rows[hotRow].DefaultCellStyle.BackColor = hotRow % 2 == 0 ? RowBackColor : AlternateRowBackColor;
                InvalidateRow(hotRow);
            }

            hotRow = rowIndex;
            if (hotRow >= 0 && hotRow < Rows.Count)
            {
                Rows[hotRow].DefaultCellStyle.BackColor = HotRowBackColor;
                InvalidateRow(hotRow);
            }
            else
            {
                Invalidate();
            }
        }

        protected override void OnRowPrePaint(DataGridViewRowPrePaintEventArgs e)
        {
            Color back = e.RowIndex % 2 == 0 ? RowBackColor : AlternateRowBackColor;
            if (e.RowIndex == hotRow || Rows[e.RowIndex].Selected)
            {
                back = HotRowBackColor;
            }

            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.RowBounds);
            }

            using (Pen pen = new Pen(DividerColor))
            {
                e.Graphics.DrawLine(pen, e.RowBounds.Left, e.RowBounds.Bottom - 1, e.RowBounds.Right, e.RowBounds.Bottom - 1);
            }

            e.PaintParts &= ~DataGridViewPaintParts.Background;
            base.OnRowPrePaint(e);
        }

        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                base.OnCellPainting(e);
                return;
            }

            if (Columns[e.ColumnIndex].Name == "ActionColumn")
            {
                e.Handled = true;
                PaintCellBackground(e);
                string action = Convert.ToString(e.Value);
                Color accent = ResolveActionColor(action);
                Color fill = Color.FromArgb(38, accent);
                Color fore = ControlPaint.Light(accent, 0.3f);
                Rectangle badge = GetCenteredBadge(e.CellBounds, action);
                using (GraphicsPath path = RoundedPanel.RoundedPath(badge, 11))
                using (SolidBrush brush = new SolidBrush(fill))
                using (Pen pen = new Pen(Color.FromArgb(90, accent)))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, action, e.CellStyle.Font, badge, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            if (Columns[e.ColumnIndex].Name == "IconColumn")
            {
                e.Handled = true;
                PaintCellBackground(e);
                Image image = e.Value as Image;
                if (image != null)
                {
                    Rectangle rect = new Rectangle(e.CellBounds.Left + 10, e.CellBounds.Top + 8, 18, 18);
                    e.Graphics.DrawImage(image, rect);
                }
                return;
            }

            base.OnCellPainting(e);
        }

        private void PaintCellBackground(DataGridViewCellPaintingEventArgs e)
        {
            Color back = e.RowIndex % 2 == 0 ? RowBackColor : AlternateRowBackColor;
            if (e.RowIndex == hotRow || Rows[e.RowIndex].Selected)
            {
                back = HotRowBackColor;
            }

            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.CellBounds);
            }

            using (Pen pen = new Pen(DividerColor))
            {
                e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }
        }

        private Color ResolveActionColor(string action)
        {
            if (string.Equals(action, ProcessPlanner.ActionForce, StringComparison.Ordinal))
            {
                return AccentOrange;
            }

            if (string.Equals(action, ProcessPlanner.ActionProtect, StringComparison.Ordinal))
            {
                return AccentPurple;
            }

            if (string.Equals(action, ProcessPlanner.ActionReport, StringComparison.Ordinal))
            {
                return MutedTextColor;
            }

            return AccentBlue;
        }

        private Rectangle GetCenteredBadge(Rectangle cell, string text)
        {
            int width = string.Equals(text, ProcessPlanner.ActionForce, StringComparison.Ordinal) ? 78 : 76;
            int height = 22;
            return new Rectangle(cell.Left + 8, cell.Top + (cell.Height - height) / 2, Math.Min(width, cell.Width - 16), height);
        }
    }

    public sealed class ModernProgressBar : Control
    {
        private int value;

        public int Value
        {
            get { return value; }
            set
            {
                this.value = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public Color TrackColor { get; set; }
        public Color BarColor { get; set; }
        public Color BorderColor { get; set; }
        public int Radius { get; set; }

        public ModernProgressBar()
        {
            Height = 8;
            TrackColor = Theme.SecondaryPanel;
            BarColor = Theme.Primary;
            BorderColor = Theme.Border;
            Radius = 4;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath trackPath = RoundedPanel.RoundedPath(track, Radius))
            using (SolidBrush trackBrush = new SolidBrush(TrackColor))
            using (Pen borderPen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
                e.Graphics.DrawPath(borderPen, trackPath);
            }

            if (Value > 0)
            {
                int width = Math.Max(2, (int)((Width - 1) * (Value / 100.0)));
                Rectangle bar = new Rectangle(0, 0, width, Height - 1);
                using (GraphicsPath barPath = RoundedPanel.RoundedPath(bar, Radius))
                using (SolidBrush barBrush = new SolidBrush(BarColor))
                {
                    e.Graphics.FillPath(barBrush, barPath);
                }
            }
        }
    }

    public sealed class NavItem : Control
    {
        private bool hovering;
        private bool pressing;
        private bool active;

        public string Icon { get; set; }
        public string PageId { get; set; }
        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        public Color IdleBackground { get; set; }
        public Color HoverBackground { get; set; }
        public Color ActiveBackground { get; set; }
        public Color PressedBackground { get; set; }
        public Color TextColor { get; set; }
        public Color ActiveTextColor { get; set; }
        public Color AccentColor { get; set; }
        public Color IconColor { get; set; }
        public int Radius { get; set; }

        public event EventHandler Navigated;

        public NavItem()
        {
            Icon = "";
            PageId = "";
            Height = 40;
            Radius = 6;
            IdleBackground = Color.Transparent;
            HoverBackground = Theme.NavHover;
            ActiveBackground = Theme.NavActive;
            PressedBackground = Theme.NavPressed;
            TextColor = Theme.NavText;
            ActiveTextColor = Theme.TitleText;
            AccentColor = Theme.Primary;
            IconColor = Theme.NavIcon;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; pressing = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressing = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressing = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnClick(EventArgs e) { if (Navigated != null) Navigated(this, e); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color bg = IdleBackground;
            Color fg = TextColor;
            if (active) { bg = ActiveBackground; fg = ActiveTextColor; }
            else if (pressing) { bg = PressedBackground; }
            else if (hovering) { bg = HoverBackground; }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            if (bg != Color.Transparent)
            {
                using (GraphicsPath path = RoundedPanel.RoundedPath(rect, Radius))
                using (SolidBrush brush = new SolidBrush(bg))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            if (active)
            {
                Rectangle bar = new Rectangle(2, 10, 4, Height - 20);
                using (GraphicsPath barPath = RoundedPanel.RoundedPath(bar, 2))
                using (SolidBrush brush = new SolidBrush(AccentColor))
                {
                    e.Graphics.FillPath(brush, barPath);
                }
            }

            if (!string.IsNullOrEmpty(Icon))
            {
                Rectangle iconRect = new Rectangle(12, 0, 26, Height);
                using (SolidBrush brush = new SolidBrush(active ? AccentColor : IconColor))
                using (Font iconFont = new Font("Segoe MDL2 Assets", 11F))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(Icon, iconFont, brush, iconRect, sf);
                }
            }

            if (!string.IsNullOrEmpty(Text))
            {
                Rectangle textRect = new Rectangle(40, 0, Width - 48, Height);
                using (SolidBrush brush = new SolidBrush(fg))
                using (Font textFont = new Font("Microsoft YaHei UI", 9.5F))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    e.Graphics.DrawString(Text, textFont, brush, textRect, sf);
                }
            }
        }
    }

    public sealed class SidebarPanel : Panel
    {
        private readonly List<NavItem> items = new List<NavItem>();
        private NavItem activeItem;

        public Color SidebarBackground { get; set; }
        public Color TitleColor { get; set; }
        public Color VersionColor { get; set; }
        public Color AccentColor { get; set; }
        public int SidebarWidth { get; set; }

        public event EventHandler<string> NavigationRequested;

        public SidebarPanel()
        {
            SidebarWidth = 196;
            Width = SidebarWidth;
            Dock = DockStyle.Left;
            SidebarBackground = Theme.Sidebar;
            TitleColor = Theme.TitleText;
            VersionColor = Theme.Muted;
            AccentColor = Theme.Primary;
            BackColor = SidebarBackground;
            Padding = new Padding(10, 0, 10, 0);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void AddNavItem(string pageId, string text, string icon)
        {
            NavItem item = new NavItem();
            item.PageId = pageId;
            item.Text = text;
            item.Icon = icon;
            item.Dock = DockStyle.None;
            item.Height = 44;
            item.AccentColor = AccentColor;
            item.Navigated += delegate
            {
                SetActive(item);
                if (NavigationRequested != null) NavigationRequested(this, item.PageId);
            };
            items.Add(item);
            Controls.Add(item);

            if (items.Count == 1)
            {
                SetActive(item);
            }
        }

        public void SetActive(NavItem item)
        {
            if (activeItem == item) return;
            if (activeItem != null) activeItem.Active = false;
            activeItem = item;
            if (activeItem != null) activeItem.Active = true;
        }

        public void SetActiveById(string pageId)
        {
            foreach (NavItem item in items)
            {
                if (item.PageId == pageId)
                {
                    SetActive(item);
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush bg = new SolidBrush(SidebarBackground))
            {
                e.Graphics.FillRectangle(bg, ClientRectangle);
            }

            using (SolidBrush dotBrush = new SolidBrush(AccentColor))
            {
                e.Graphics.FillEllipse(dotBrush, 16, 28, 7, 7);
            }

            using (SolidBrush titleBrush = new SolidBrush(TitleColor))
            using (Font titleFont = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold))
            {
                e.Graphics.DrawString("OneClickClose", titleFont, titleBrush, 29, 21);
            }

            using (SolidBrush subBrush = new SolidBrush(VersionColor))
            using (Font subFont = new Font("Microsoft YaHei UI", 8.5F))
            {
                e.Graphics.DrawString("关机前后台清理", subFont, subBrush, 29, 46);
            }

            using (Pen divider = new Pen(Theme.Divider))
            {
                e.Graphics.DrawLine(divider, 14, 68, Width - 14, 68);
            }

            using (SolidBrush verBrush = new SolidBrush(VersionColor))
            using (Font verFont = new Font("Microsoft YaHei UI", 8F))
            {
                e.Graphics.DrawString("v1.0", verFont, verBrush, 14, Height - 28);
            }

            using (Pen edge = new Pen(Theme.Divider))
            {
                e.Graphics.DrawLine(edge, Width - 1, 0, Width - 1, Height);
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int topY = 80;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].Top = topY + i * 44;
                items[i].Width = Width - 20;
                items[i].Left = 10;
            }
        }
    }

    public sealed class TabBar : Panel
    {
        private readonly List<Panel> tabs = new List<Panel>();
        private readonly List<string> tabIds = new List<string>();
        private int activeIndex = -1;

        public Color IdleBackground { get; set; }
        public Color ActiveBackground { get; set; }
        public Color HoverBackground { get; set; }
        public Color TextColor { get; set; }
        public Color ActiveTextColor { get; set; }
        public Color AccentColor { get; set; }
        public Color DividerColor { get; set; }

        public event EventHandler<string> TabSelected;

        public TabBar()
        {
            Height = 44;
            IdleBackground = Theme.TabIdle;
            ActiveBackground = Theme.TabActive;
            HoverBackground = Theme.TabHover;
            TextColor = Theme.Muted;
            ActiveTextColor = Color.White;
            AccentColor = Theme.Primary;
            DividerColor = Theme.Border;
            BackColor = IdleBackground;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void AddTab(string id, string text)
        {
            Panel tab = new Panel();
            tab.Tag = id;
            tab.Height = Height - 3;
            tab.Top = 0;
            tab.BackColor = Color.Transparent;
            tab.Cursor = Cursors.Hand;

            Label label = new Label();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Microsoft YaHei UI", 9.5F);
            label.ForeColor = TextColor;
            label.BackColor = Color.Transparent;
            tab.Controls.Add(label);

            tab.MouseEnter += delegate { if (tabIds.IndexOf(tab.Tag as string) != activeIndex) tab.BackColor = HoverBackground; };
            tab.MouseLeave += delegate { if (tabIds.IndexOf(tab.Tag as string) != activeIndex) tab.BackColor = Color.Transparent; };
            tab.Click += delegate
            {
                int idx = tabIds.IndexOf(tab.Tag as string);
                if (idx >= 0) SetActive(idx);
                if (TabSelected != null) TabSelected(this, tab.Tag as string);
            };
            label.Click += delegate
            {
                int idx = tabIds.IndexOf(tab.Tag as string);
                if (idx >= 0) SetActive(idx);
                if (TabSelected != null) TabSelected(this, tab.Tag as string);
            };

            tabs.Add(tab);
            tabIds.Add(id);
            Controls.Add(tab);

            if (tabs.Count == 1)
            {
                SetActive(0);
            }
            else
            {
                LayoutTabs();
            }
        }

        private void SetActive(int index)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                Label lbl = tabs[i].Controls[0] as Label;
                if (i == index)
                {
                    activeIndex = i;
                    tabs[i].BackColor = Color.Transparent;
                    if (lbl != null) lbl.ForeColor = ActiveTextColor;
                }
                else
                {
                    tabs[i].BackColor = Color.Transparent;
                    if (lbl != null) lbl.ForeColor = TextColor;
                }
            }
            Invalidate();
        }

        private void LayoutTabs()
        {
            int x = 8;
            using (Graphics g = CreateGraphics())
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    Label lbl = tabs[i].Controls[0] as Label;
                    int w = 100;
                    if (lbl != null)
                    {
                        Size measured = TextRenderer.MeasureText(g, lbl.Text, lbl.Font);
                        w = Math.Max(80, measured.Width + 32);
                    }
                    tabs[i].Width = w;
                    tabs[i].Left = x;
                    x += w + 4;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = new Pen(DividerColor))
            {
                e.Graphics.DrawLine(pen, 0, Height - 3, Width, Height - 3);
            }

            if (activeIndex >= 0 && activeIndex < tabs.Count)
            {
                Panel active = tabs[activeIndex];
                Rectangle bar = new Rectangle(active.Left + 8, Height - 3, active.Width - 16, 3);
                using (SolidBrush brush = new SolidBrush(AccentColor))
                {
                    e.Graphics.FillRectangle(brush, bar);
                }
            }
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.SecondaryPanel; } }
        public override Color ToolStripBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Theme.RowHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.RowHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.RowHover; } }
        public override Color ImageMarginGradientBegin { get { return Theme.SecondaryPanel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.SecondaryPanel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.SecondaryPanel; } }
        public override Color SeparatorDark { get { return Theme.Divider; } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
        public override Color CheckBackground { get { return Theme.RowHover; } }
        public override Color CheckPressedBackground { get { return Theme.NavPressed; } }
        public override Color CheckSelectedBackground { get { return Theme.RowHover; } }
        public override Color ButtonSelectedHighlight { get { return Theme.RowHover; } }
        public override Color ButtonSelectedBorder { get { return Color.Transparent; } }
        public override Color MenuBorder { get { return Theme.Border; } }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            base.OnRenderToolStripBorder(e);
            using (Pen pen = new Pen(Theme.Border))
            {
                Rectangle r = e.AffectedBounds;
                r.Inflate(-1, -1);
                e.Graphics.DrawRectangle(pen, r);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Rectangle r = e.Item.ContentRectangle;
            int y = r.Top + r.Height / 2;
            using (Pen pen = new Pen(Theme.Divider))
            {
                e.Graphics.DrawLine(pen, r.Left + 28, y, r.Right - 4, y);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(Theme.SecondaryPanel))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }
    }
}
