using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Obsidot
{
    // The first time Obsidot starts, it asks what to call you, so it can say GOOD MORNING VRAJ.
    // Enter keeps the name, Esc skips. Right-click the pet any time to change it.
    class NamePrompt : Form
    {
        readonly float S = U.S;
        readonly TextBox box;
        readonly Action<string> done;
        readonly Timer anim = new Timer { Interval = 15 };
        double openT; int y1; bool finished; readonly string title, question;
        static readonly Color Bg = Color.FromArgb(18, 18, 20), Field = Color.FromArgb(30, 30, 33), Red = Color.FromArgb(232, 36, 32);

        public NamePrompt(RectangleF puck, string current, Action<string> done, string title = "HELLO", string question = "What should I call you?", int max = 16)
        {
            this.done = done; this.title = title; this.question = question;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; ShowInTaskbar = true;
            TopMost = true; KeyPreview = true; BackColor = Bg; Text = "Obsidot"; DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Size = new Size((int)(360 * S), (int)(200 * S));
            box = new TextBox
            {
                BorderStyle = BorderStyle.None, BackColor = Field, ForeColor = Color.FromArgb(244, 244, 246), MaxLength = max,
                Font = new Font("Segoe UI Variable Display", 15f), TextAlign = HorizontalAlignment.Center, Text = current ?? ""
            };
            box.Bounds = new Rectangle((int)(60 * S), (int)(104 * S), Width - (int)(120 * S), box.PreferredHeight);
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Finish(box.Text); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Finish(current ?? ""); }
            };
            Controls.Add(box);
            // centred on the puck, a little below it if there is room
            var wa = Screen.FromPoint(Point.Round(new PointF(puck.X + puck.Width / 2, puck.Y + puck.Height / 2))).WorkingArea;
            int x = (int)(puck.X + puck.Width / 2 - Width / 2f), y = (int)(puck.Bottom + 14 * S);
            if (y + Height > wa.Bottom) y = (int)(puck.Top - 14 * S - Height);
            x = Math.Max(wa.Left + 8, Math.Min(wa.Right - Width - 8, x)); y = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - Height - 8, y));
            y1 = y; Location = new Point(x, y + (int)(10 * S)); Opacity = 0; openT = U.Now;
            anim.Tick += delegate
            {
                double k = U.Clamp01((U.Now - openT) / 0.2), q = 1 - (1 - k) * (1 - k) * (1 - k);
                Opacity = q; Top = y1 + (int)Math.Round(10 * S * (1 - q)); if (k >= 1) anim.Stop();
            };
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } } // CS_DROPSHADOW
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int round = 2; try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { } }
        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); box.Focus(); box.SelectAll(); openT = U.Now; anim.Start(); Snd.Play("open"); }

        void Finish(string name)
        {
            if (finished) return; finished = true;
            done(name); Snd.Play("chirp"); Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e) { if (!finished) { finished = true; done(null); } anim.Stop(); base.OnFormClosing(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            // HELLO in the puck's own dot lettering, the red second beside it
            var d = new Dots(); d.Text3At(title, 0, 0, 1f);
            float p = 4.2f * S, w = (Dots.W3(title) - 1) * p;
            Dots.Paint(g, d, Width / 2f - w / 2, 26 * S, p, 3 * S, Color.White, Red);
            using (var br = new SolidBrush(Red)) g.FillEllipse(br, Width / 2f + w / 2 + 9 * S, 40 * S, 6 * S, 6 * S);
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
            using (var br = new SolidBrush(Color.FromArgb(200, 204, 208))) g.DrawString(question, U.F("Segoe UI Variable Text", 13 * S), br, Width / 2f, 64 * S, fmt);
            // the field: a dark well with a red line under it
            var fr = new RectangleF(48 * S, 96 * S, Width - 96 * S, box.Height + 16 * S);
            using (var path = U.Round(fr.X, fr.Y, fr.Width, fr.Height, 10 * S)) using (var br = new SolidBrush(Field)) g.FillPath(br, path);
            using (var pen = new Pen(Red, 1.6f * S)) g.DrawLine(pen, fr.X + 14 * S, fr.Bottom - 1 * S, fr.Right - 14 * S, fr.Bottom - 1 * S);
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
            using (var br = new SolidBrush(Color.FromArgb(120, 150, 150, 156))) g.DrawString("ENTER TO SAVE   ·   ESC TO SKIP", U.F("Bahnschrift SemiBold", 8.5f * S), br, Width / 2f, Height - 34 * S, fmt);
            using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255), 1)) g.DrawLine(pen, 18 * S, 1, Width - 18 * S, 1);
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    }
}
