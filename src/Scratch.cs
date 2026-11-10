using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Obsidot
{
    // A black glass notepad that slides out of the puck. Everything you type is saved as you go
    // (to %APPDATA%\Obsidot\scratchpad.txt); Esc or clicking elsewhere puts it away.
    class Scratch : Form
    {
        readonly float S = U.S;
        readonly TextBox box;
        readonly Timer anim = new Timer { Interval = 15 }, saveT = new Timer { Interval = 500 };
        double openT; int y1; bool dirty;
        Color Bg { get { return Theme.Mix(Theme.T.Mid, Theme.T.Bot, 0.35); } }
        static Color Red { get { return Theme.T.Accent; } }
        public static string File_ { get { return Path.Combine(Settings.Dir, "scratchpad.txt"); } }

        int Pad { get { return (int)(22 * S); } }
        int Head { get { return (int)(46 * S); } }
        int Foot { get { return (int)(30 * S); } }

        public Scratch(RectangleF puck, string demo = null)
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; KeyPreview = true; BackColor = Bg; Text = "Obsidot Scratchpad";
            DoubleBuffered = true;
            Size = new Size((int)(440 * S), (int)(380 * S));
            box = new TextBox
            {
                Multiline = true, BorderStyle = BorderStyle.None, BackColor = Bg, ForeColor = Theme.Mix(Theme.T.Ink, Theme.T.Mid, 0.08),
                Font = BoxFont, AcceptsTab = true, WordWrap = true, ScrollBars = ScrollBars.None,
                Bounds = new Rectangle(Pad, Head, Width - Pad * 2, Height - Head - Foot)
            };
            try { box.Text = demo ?? (File.Exists(File_) ? File.ReadAllText(File_) : ""); }
            catch (Exception ex) { Settings.Log(ex); readFailed = true; } // locked by a sync tool? then never save over the notes
            box.TextChanged += delegate { dirty = true; saveT.Stop(); saveT.Start(); Invalidate(new Rectangle(0, Height - Foot, Width, Foot)); };
            box.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.A) { box.SelectAll(); e.SuppressKeyPress = true; } }; // TextBox lacks Ctrl+A when multiline
            Controls.Add(box);
            saveT.Tick += delegate { saveT.Stop(); Save(); };

            // beside the puck: below it if there is room, otherwise above; always fully on its screen
            var wa = Screen.FromPoint(Point.Round(new PointF(puck.X + puck.Width / 2, puck.Y + puck.Height / 2))).WorkingArea;
            int x = (int)(puck.X + puck.Width / 2 - Width / 2f), y = (int)(puck.Bottom + 16 * S);
            if (y + Height > wa.Bottom) y = (int)(puck.Top - 16 * S - Height);
            x = Math.Max(wa.Left + 8, Math.Min(wa.Right - Width - 8, x)); y = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - Height - 8, y));
            y1 = y; Location = new Point(x, y + (int)(10 * S)); Opacity = 0;
            anim.Tick += Animate; openT = U.Now;
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } } // CS_DROPSHADOW

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 2; try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { } // Windows 11 rounded corners
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate(); box.Focus(); box.SelectionStart = box.TextLength; box.ScrollToCaret();
            openT = U.Now; anim.Start(); Snd.Play("open");
        }

        void Animate(object s, EventArgs e) // slide up 10 px and fade in, 160 ms
        {
            double k = U.Clamp01((U.Now - openT) / 0.16), q = 1 - (1 - k) * (1 - k) * (1 - k);
            Opacity = q; Top = y1 + (int)Math.Round(10 * S * (1 - q));
            if (k >= 1) anim.Stop();
        }

        static readonly Font BoxFont = new Font("Segoe UI Variable Text", 11.5f);
        bool readFailed;

        void Save()
        {
            if (!dirty) return;
            if (readFailed) // keep what was typed, beside the notes, instead of replacing them
            {
                try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(File_), "scratchpad.recovered.txt"), box.Text); dirty = false; }
                catch (Exception ex) { Settings.Log(ex); }
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File_));
                string tmp = File_ + ".tmp";
                File.WriteAllText(tmp, box.Text); // write aside first, then swap: a crash mid-write never eats the notes
                if (File.Exists(File_)) File.Replace(tmp, File_, null); else File.Move(tmp, File_);
                dirty = false;
            }
            catch (Exception ex) { Settings.Log(ex); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        protected override void OnDeactivate(EventArgs e) // clicking elsewhere puts it away; focus can flicker while it opens, so take it back then
        {
            base.OnDeactivate(e);
            if (U.Now - openT > 0.5) BeginInvoke(new Action(Close));
            else BeginInvoke(new Action(() => { Activate(); box.Focus(); }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e) { saveT.Stop(); Save(); anim.Stop(); Snd.Play("close"); base.OnFormClosing(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;

            float p = 2.7f * S, dot = 1.75f * S;
            // header: a red dot, the name in the puck's own dot lettering, the date on the right
            using (var br = new SolidBrush(Red)) g.FillEllipse(br, Pad, 19 * S, 7 * S, 7 * S);
            var d = new Dots(); d.Text3At("SCRATCHPAD", 0, 0, 0.92f);
            Dots.Paint(g, d, Pad + 16 * S, 17 * S, p, dot, Theme.T.Ink, Red);
            string date = DateTime.Now.ToString("ddd dd MMM", System.Globalization.CultureInfo.InvariantCulture);
            var dd = new Dots(); dd.Text3At(date, 0, 0, 0.4f);
            Dots.Paint(g, dd, Width - Pad - (Dots.W3(date) - 1) * p, 17 * S, p, dot, Theme.T.Ink, Red);
            using (var pen = new Pen(Color.FromArgb(28, Theme.T.Rim), 1)) g.DrawLine(pen, Pad, Head - 12 * S, Width - Pad, Head - 12 * S);
            // footer: how to put it away, and a word count
            float fy = Height - Foot + 9 * S;
            var f = new Dots(); f.Text3At("ESC TO CLOSE - SAVED AS YOU TYPE", 0, 0, 0.28f);
            Dots.Paint(g, f, Pad, fy, 2.2f * S, 1.4f * S, Theme.T.Ink, Red);
            int words = box.Text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            string wc = words + (words == 1 ? " WORD" : " WORDS");
            var w = new Dots(); w.Text3At(wc, 0, 0, 0.4f);
            Dots.Paint(g, w, Width - Pad - (Dots.W3(wc) - 1) * 2.2f * S, fy, 2.2f * S, 1.4f * S, Theme.T.Ink, Red);
        }

        protected override void Dispose(bool disposing) { if (disposing) { anim.Dispose(); saveT.Dispose(); } base.Dispose(disposing); }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    }
}
