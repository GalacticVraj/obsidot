using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Obsidot
{
    // Right-click the pet: its settings, in the clock's own look. A switch for each option, the watched sites as chips
    // you tap on and off (or add your own), and your name. Changes apply at once; Esc or a click elsewhere closes it.
    class PetSettings : Form
    {
        readonly float S = U.S;
        readonly Action changed; readonly Action<string> renamed;
        readonly Timer anim = new Timer { Interval = 15 };
        readonly TextBox nameBox, addBox;
        double openT; int y1; bool shut;
        static readonly Color Bg = Color.FromArgb(18, 18, 20), Field = Color.FromArgb(32, 32, 36), Hair = Color.FromArgb(52, 52, 58),
                              Ink = Color.FromArgb(240, 240, 244), Mid = Color.FromArgb(150, 152, 160), Dim = Color.FromArgb(92, 94, 102), Red = Color.FromArgb(232, 36, 32);
        static readonly Font Body = new Font("Segoe UI Variable Text", 10.5f), Small = new Font("Segoe UI Variable Text", 9f),
                             Label = new Font("Bahnschrift SemiBold", 8f), Chip = new Font("Segoe UI Variable Text", 9.5f);

        class Row { public string Text; public Func<bool> Get; public Action Flip; public Func<bool> On; public RectangleF R; }
        readonly List<Row> rows = new List<Row>();
        readonly List<KeyValuePair<string, RectangleF>> chips = new List<KeyValuePair<string, RectangleF>>();
        readonly List<KeyValuePair<string, float>> labels = new List<KeyValuePair<string, float>>();
        RectangleF addChip, resetLink, nameField; object hot;
        int W { get { return (int)(400 * S); } }

        public PetSettings(Point at, Action changed, Action<string> renamed)
        {
            this.changed = changed; this.renamed = renamed;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false;
            TopMost = true; KeyPreview = true; BackColor = Bg; Text = "Obsidot pet"; DoubleBuffered = true;

            Add("WHILE YOU FOCUS", null);
            Add("Catch distracting sites", () => Settings.Guard, () => Settings.Guard = !Settings.Guard, null);
            Add("Close them by itself", () => Settings.AutoClose, () => Settings.AutoClose = !Settings.AutoClose, () => Settings.Guard);
            Add("Stay on screen, above every window", () => Settings.Buddy, () => Settings.Buddy = !Settings.Buddy, null);
            Add("SITES IT WATCHES", null);
            Add("NUDGES", null);
            Add("Break and bedtime reminders", () => Settings.Remind, () => Settings.Remind = !Settings.Remind, null);
            Add("Battery and download alerts", () => Settings.Alerts, () => Settings.Alerts = !Settings.Alerts, null);
            Add("WHAT IT CALLS YOU", null);

            nameBox = new TextBox { BorderStyle = BorderStyle.None, BackColor = Field, ForeColor = Ink, Font = Body, MaxLength = 16, Text = Settings.Name };
            nameBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitName(); ActiveControl = null; Snd.Play("tick"); } };
            addBox = new TextBox { BorderStyle = BorderStyle.None, BackColor = Field, ForeColor = Ink, Font = Chip, MaxLength = 30, Visible = false };
            addBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddSite(addBox.Text); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; e.Handled = true; HideAdd(); }
            };
            addBox.LostFocus += delegate { if (addBox.Visible) AddSite(addBox.Text); };
            Controls.Add(nameBox); Controls.Add(addBox);

            Arrange();
            // beside the cursor, above it if there's no room below (the pet usually sits on the taskbar), fully on screen
            var wa = Screen.FromPoint(at).WorkingArea;
            int x = at.X - W / 2, y = at.Y + (int)(14 * S);
            if (y + Height > wa.Bottom) y = at.Y - (int)(14 * S) - Height;
            x = Math.Max(wa.Left + 8, Math.Min(wa.Right - Width - 8, x)); y = Math.Max(wa.Top + 8, Math.Min(wa.Bottom - Height - 8, y));
            y1 = y; Location = new Point(x, y + (int)(10 * S)); Opacity = 0; openT = U.Now;
            anim.Tick += delegate
            {
                double k = U.Clamp01((U.Now - openT) / 0.18), q = 1 - (1 - k) * (1 - k) * (1 - k);
                Opacity = q; Top = y1 + (int)Math.Round(10 * S * (1 - q)); if (k >= 1) anim.Stop();
            };
        }

        void Add(string text, Func<bool> get, Action flip, Func<bool> on) { rows.Add(new Row { Text = text, Get = get, Flip = flip, On = on }); }
        void Add(string label, object _) { rows.Add(new Row { Text = label }); }

        // positions of everything; the height follows the chips
        void Arrange()
        {
            float pad = 24 * S, y = 70 * S, w = W;
            chips.Clear(); labels.Clear();
            foreach (var r in rows)
            {
                if (r.Get == null) // a section label, and what follows it
                {
                    y += 10 * S; labels.Add(new KeyValuePair<string, float>(r.Text, y)); y += 22 * S;
                    if (r.Text == "SITES IT WATCHES")
                    {
                        resetLink = new RectangleF(w - pad - 70 * S, y - 24 * S, 70 * S, 20 * S);
                        float cx = pad, ch = 30 * S, gap = 8 * S;
                        foreach (var s in AllSites())
                        {
                            float cw = TextRenderer.MeasureText(s, Chip).Width + 26 * S;
                            if (cx + cw > w - pad) { cx = pad; y += ch + gap; }
                            chips.Add(new KeyValuePair<string, RectangleF>(s, new RectangleF(cx, y, cw, ch)));
                            cx += cw + gap;
                        }
                        float aw = addBox.Visible ? 150 * S : TextRenderer.MeasureText("+ Add", Chip).Width + 26 * S;
                        if (cx + aw > w - pad) { cx = pad; y += ch + gap; }
                        addChip = new RectangleF(cx, y, aw, ch);
                        y += ch + 6 * S;
                    }
                    if (r.Text == "WHAT IT CALLS YOU") { nameField = new RectangleF(pad, y, w - pad * 2, 38 * S); y += 38 * S; }
                    continue;
                }
                r.R = new RectangleF(pad - 8 * S, y, w - pad * 2 + 16 * S, 38 * S); y += 38 * S;
            }
            y += 46 * S; // footer
            ClientSize = new Size(W, (int)y);
            nameBox.Bounds = new Rectangle((int)(nameField.X + 14 * S), (int)(nameField.Y + (nameField.Height - nameBox.PreferredHeight) / 2), (int)(nameField.Width - 28 * S), nameBox.PreferredHeight);
            addBox.Bounds = new Rectangle((int)(addChip.X + 12 * S), (int)(addChip.Y + (addChip.Height - addBox.PreferredHeight) / 2), (int)(addChip.Width - 24 * S), addBox.PreferredHeight);
            Invalidate();
        }

        static List<string> AllSites()
        {
            var all = new List<string>(Settings.DefaultDistract.Split(','));
            foreach (var s in Settings.Sites()) if (!all.Exists(a => Same(a, s))) all.Add(s.Trim());
            return all;
        }
        static bool Same(string a, string b) { return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase); }
        static bool Watched(string s) { return Array.Exists(Settings.Sites(), a => Same(a, s)); }

        void ToggleSite(string s)
        {
            var cur = new List<string>(Settings.Sites());
            if (Watched(s)) cur.RemoveAll(a => Same(a, s)); else cur.Add(s);
            Settings.Distract = string.Join(",", cur); changed();
        }

        void AddSite(string text)
        {
            text = (text ?? "").Replace(",", " ").Replace("=", " ").Trim();
            if (text.Length > 0 && !Watched(text)) { Settings.Distract = string.Join(",", new List<string>(Settings.Sites()) { text }); changed(); Snd.Play("tick"); }
            HideAdd();
        }
        void HideAdd() { if (!addBox.Visible) return; addBox.Visible = false; addBox.Text = ""; Arrange(); ActiveControl = null; }

        void CommitName()
        {
            string n = nameBox.Text.Replace("\r", "").Replace("\n", "").Replace("=", "").Trim();
            if (n != Settings.Name) renamed(n);
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } } // CS_DROPSHADOW
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int round = 2; try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { } }
        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); ActiveControl = null; openT = U.Now; anim.Start(); Snd.Play("open"); }
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (U.Now - openT > 0.25) BeginInvoke(new Action(Close)); } // a click anywhere else
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape && !addBox.Visible) { e.Handled = true; Close(); } base.OnKeyDown(e); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!shut) { shut = true; if (addBox.Visible) AddSite(addBox.Text); CommitName(); Snd.Play("close"); }
            anim.Stop(); base.OnFormClosing(e);
        }

        object HitAt(PointF p)
        {
            foreach (var r in rows) if (r.Get != null && r.R.Contains(p) && (r.On == null || r.On())) return r;
            foreach (var c in chips) if (c.Value.Contains(p) && Settings.Guard) return c.Key;
            if (addChip.Contains(p) && Settings.Guard && !addBox.Visible) return "+";
            if (resetLink.Contains(p) && Settings.Guard) return "reset";
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = HitAt(e.Location);
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
            if (!Equals(h, hot)) { hot = h; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { if (hot != null) { hot = null; Invalidate(); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ActiveControl = null;
            var h = HitAt(e.Location);
            var row = h as Row; var site = h as string;
            if (row != null) { row.Flip(); changed(); Snd.Play("haptic"); }
            else if (site == "+") { addBox.Visible = true; Arrange(); addBox.Focus(); }
            else if (site == "reset") { Settings.Distract = Settings.DefaultDistract; changed(); Arrange(); Snd.Play("tick"); }
            else if (site != null) { ToggleSite(site); Snd.Play("haptic"); }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float pad = 24 * S;
            // PET in the clock's dot lettering, the red second beside it, and a line under it
            var d = new Dots(); d.Text3At("PET", 0, 0, 1f);
            float p = 3.6f * S;
            Dots.Paint(g, d, pad, 22 * S, p, 2.6f * S, Color.White, Red);
            using (var br = new SolidBrush(Red)) g.FillEllipse(br, pad + Dots.W3("PET") * p + 4 * S, 34 * S, 5 * S, 5 * S);
            TextRenderer.DrawText(g, "your focus buddy", Small, new Point((int)(pad + Dots.W3("PET") * p + 18 * S), (int)(24 * S)), Mid, TextFormatFlags.NoPadding);
            using (var pen = new Pen(Color.FromArgb(50, 255, 255, 255), 1)) g.DrawLine(pen, 18 * S, 1, Width - 18 * S, 1);

            foreach (var l in labels)
            {
                TextRenderer.DrawText(g, l.Key, Label, new Point((int)pad, (int)l.Value), Dim, TextFormatFlags.NoPadding);
                if (l.Key == "SITES IT WATCHES")
                    TextRenderer.DrawText(g, "Reset", Small, Rectangle.Round(resetLink), Equals(hot, "reset") ? Ink : Settings.Guard ? Mid : Dim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            foreach (var r in rows)
            {
                if (r.Get == null) continue;
                bool live = r.On == null || r.On(), on = r.Get();
                if (Equals(hot, r)) using (var path = U.Round(r.R.X, r.R.Y + 2 * S, r.R.Width, r.R.Height - 4 * S, 8 * S)) using (var br = new SolidBrush(Field)) g.FillPath(br, path);
                TextRenderer.DrawText(g, r.Text, Body, new Rectangle((int)(r.R.X + 8 * S), (int)r.R.Y, (int)(r.R.Width - 70 * S), (int)r.R.Height), live ? Ink : Dim, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                Switch(g, r.R.Right - 8 * S - 40 * S, r.R.Y + r.R.Height / 2 - 11 * S, on, live);
            }

            foreach (var c in chips)
            {
                bool on = Watched(c.Key), live = Settings.Guard, hov = Equals(hot, c.Key);
                using (var path = U.Round(c.Value.X, c.Value.Y, c.Value.Width, c.Value.Height, c.Value.Height / 2))
                {
                    using (var br = new SolidBrush(on && live ? Color.FromArgb(46, Red) : Field)) g.FillPath(br, path);
                    using (var pen = new Pen(on && live ? Color.FromArgb(hov ? 255 : 200, Red) : hov ? Mid : Hair, 1.2f * S)) g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, c.Key, Chip, Rectangle.Round(c.Value), on && live ? Ink : live ? Mid : Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            using (var path = U.Round(addChip.X, addChip.Y, addChip.Width, addChip.Height, addChip.Height / 2))
            {
                using (var br = new SolidBrush(Field)) g.FillPath(br, path);
                using (var pen = new Pen(addBox.Visible ? Red : Equals(hot, "+") ? Mid : Hair, 1.2f * S) { DashStyle = addBox.Visible ? DashStyle.Solid : DashStyle.Dash }) g.DrawPath(pen, path);
            }
            if (!addBox.Visible) TextRenderer.DrawText(g, "+ Add", Chip, Rectangle.Round(addChip), Settings.Guard ? Mid : Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            using (var path = U.Round(nameField.X, nameField.Y, nameField.Width, nameField.Height, 10 * S)) using (var br = new SolidBrush(Field)) g.FillPath(br, path);
            using (var pen = new Pen(Red, 1.6f * S)) g.DrawLine(pen, nameField.X + 14 * S, nameField.Bottom - 1 * S, nameField.Right - 14 * S, nameField.Bottom - 1 * S);

            TextRenderer.DrawText(g, "CHANGES SAVE AS YOU GO   ·   ESC TO CLOSE", Label, new Rectangle(0, Height - (int)(34 * S), Width, (int)(20 * S)), Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }

        // a pill switch: red with the knob right when on
        void Switch(Graphics g, float x, float y, bool on, bool live)
        {
            float w = 40 * S, h = 22 * S; int a = live ? 255 : 90;
            using (var path = U.Round(x, y, w, h, h / 2))
            {
                using (var br = new SolidBrush(Color.FromArgb(a, on ? Red : Field))) g.FillPath(br, path);
                if (!on) using (var pen = new Pen(Color.FromArgb(a, Mid), 1.2f * S)) g.DrawPath(pen, path);
            }
            float k = 14 * S, kx = on ? x + w - k - 4 * S : x + 4 * S;
            using (var br = new SolidBrush(Color.FromArgb(a, on ? Color.White : Mid))) g.FillEllipse(br, kx, y + (h - k) / 2, k, k);
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    }
}
