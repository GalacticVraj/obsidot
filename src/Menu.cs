using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Obsidot
{
    // The pet's right-click menu in the clock's own look: black glass, white text, a red dot for "on".
    class DarkMenu : ToolStripProfessionalRenderer
    {
        static readonly Color Bg = Color.FromArgb(24, 24, 27), Hot = Color.FromArgb(46, 46, 52), Line = Color.FromArgb(56, 56, 62),
                              Ink = Color.FromArgb(236, 236, 240), Dim = Color.FromArgb(112, 112, 120);
        public static readonly Font Font = new Font("Segoe UI", 9.5f);

        public DarkMenu() : base(new Colors()) { RoundedEdges = false; }

        public static ContextMenuStrip Make()
        {
            return new ContextMenuStrip { Renderer = new DarkMenu(), Font = Font, ShowImageMargin = false, ShowCheckMargin = true, BackColor = Bg, ForeColor = Ink };
        }

        // a submenu that looks the same
        public static ToolStripMenuItem Sub(string text)
        {
            var i = new ToolStripMenuItem(text);
            i.DropDown.Renderer = new DarkMenu(); i.DropDown.BackColor = Bg; i.DropDown.ForeColor = Ink; i.DropDown.Font = Font;
            ((ToolStripDropDownMenu)i.DropDown).ShowImageMargin = false; ((ToolStripDropDownMenu)i.DropDown).ShowCheckMargin = true;
            return i;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { using (var b = new SolidBrush(Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using (var p = new Pen(Line)) e.Graphics.DrawRectangle(p, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using (var b = new SolidBrush(Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds); }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var r = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using (var b = new SolidBrush(e.Item.Selected && e.Item.Enabled ? Hot : Bg)) e.Graphics.FillRectangle(b, r);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Ink : Dim; base.OnRenderItemText(e); }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Line)) e.Graphics.DrawLine(p, 10, y, e.Item.Width - 10, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)   // "on" is a red dot
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var r = e.ImageRectangle;
            float d = Math.Min(r.Width, r.Height) * 0.42f;
            using (var b = new SolidBrush(e.Item.Enabled ? Theme.T.Accent : Dim)) g.FillEllipse(b, r.X + r.Width / 2f - d / 2, r.Y + r.Height / 2f - d / 2, d, d);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Ink; base.OnRenderArrow(e); }

        class Colors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Hot; } }
            public override Color MenuItemBorder { get { return Hot; } }
            public override Color MenuBorder { get { return Line; } }
            public override Color ToolStripDropDownBackground { get { return Bg; } }
            public override Color ImageMarginGradientBegin { get { return Bg; } }
            public override Color ImageMarginGradientMiddle { get { return Bg; } }
            public override Color ImageMarginGradientEnd { get { return Bg; } }
            public override Color SeparatorDark { get { return Line; } }
            public override Color SeparatorLight { get { return Line; } }
        }
    }
}
