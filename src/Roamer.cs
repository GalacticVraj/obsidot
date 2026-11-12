using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace Obsidot
{
    // The pet stepping out on top of every screen, only when it has something to tell you.
    // It drops onto the title bar of the window you're using (or the taskbar), holds up a dot-matrix sign,
    // and goes home after you click it (does the thing), right-click it (dismiss), or ignore it for a while.
    // If you move the window it is sitting on, it tumbles down to the taskbar.
    class Roamer : LayeredForm
    {
        static readonly float S0 = U.S;
        readonly float S = U.S; readonly Pet pet; readonly Timer timer = new Timer { Interval = 15 };
        readonly string line1, line2; readonly Action click, dismiss; readonly double life;
        static Color Red { get { return Theme.T.Accent; } }
        float x, y, vy, ground; bool falling = true; double t0, last, landT = -10, leaveT = -1;
        IntPtr perch; N.RECT perchRect;
        int Foot { get { return LH - (int)(8 * S); } } // where its feet are inside this window

        public Roamer(Pet pet, string line1, string line2, Action click, double life, Action dismiss = null) : base((int)(300 * S0), (int)(200 * S0))
        {
            this.pet = pet; this.line1 = line1; this.line2 = line2; this.click = click; this.dismiss = dismiss; this.life = life;
            FindPerch();
            y = ground - 320 * S; t0 = last = U.Now;                     // it drops in from above
            pet.Roaming = true; pet.Asleep = false; pet.Scared = false; pet.Walking = false;
            timer.Tick += Frame;
        }

        // the title bar of the window you're using, left of its buttons; otherwise the top of the taskbar
        void FindPerch()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            perch = IntPtr.Zero; ground = wa.Bottom; x = wa.Right - 230 * S;
            IntPtr fg = N.GetForegroundWindow(); N.RECT r;
            if (fg == IntPtr.Zero || !N.IsWindowVisible(fg) || N.IsIconic(fg) || !N.GetWindowRect(fg, out r)) return;
            var sb = new StringBuilder(64); N.GetClassName(fg, sb, 64); string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return;
            var mon = Screen.FromHandle(fg).WorkingArea;
            if (r.T < mon.Top + 70 * S || r.R - r.L < 360 * S) return;   // maximized or tiny: the taskbar it is
            perch = fg; perchRect = r; ground = r.T + 1; x = r.R - 175 * S;
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x8; return cp; } } // TOPMOST
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SetFloat(true); timer.Start(); Frame(null, null); }

        public void GoHome() { if (leaveT < 0) { leaveT = U.Now; falling = false; vy = -650 * S; Snd.Play("whoosh"); } }

        void Frame(object s, EventArgs e)
        {
            double now = U.Now, dt = Math.Min(0.05, now - last); last = now;
            if (leaveT < 0)
            {
                if (perch != IntPtr.Zero) // the window moved, closed or minimized: it loses its footing
                {
                    N.RECT r;
                    if (!N.IsWindowVisible(perch) || N.IsIconic(perch) || !N.GetWindowRect(perch, out r) || r.T != perchRect.T || r.L != perchRect.L || r.R != perchRect.R)
                    {
                        perch = IntPtr.Zero; ground = Screen.FromPoint(new Point((int)x, (int)y)).WorkingArea.Bottom;
                        if (!falling) { falling = true; vy = -260 * S; pet.Sad(now); Snd.Play("boing"); }
                    }
                }
                if (falling)
                {
                    vy += 2300 * S * (float)dt; y += vy * (float)dt;
                    if (y >= ground) { y = ground; pet.Land(vy); falling = false; vy = 0; landT = now; Snd.Play("boing"); }
                }
                if (now - t0 > life) GoHome();
            }
            else
            {
                vy += 1400 * S * (float)dt; y += vy * (float)dt;           // a big hop up and away
                if (now - leaveT > 0.45) { timer.Stop(); Close(); return; }
            }
            var m = Cursor.Position;
            pet.Tick(dt, now, new PointF(m.X - x, m.Y - (y - 14 * S)));
            Render(now);
            Present((int)Math.Round(x - LW / 2f), (int)Math.Round(y - Foot));
        }

        public Bitmap Render(double now)
        {
            using (var g = Graphics.FromImage(Surface))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                float a = leaveT < 0 ? 1 : (float)(1 - U.Clamp01((now - leaveT) / 0.45));
                double sk = U.Clamp01((now - landT - 0.08) / 0.22) * (leaveT < 0 ? 1 : a);
                if (sk > 0.01) Sign(g, (float)sk);
                pet.DrawAt(g, LW / 2f, Foot, now, a);
            }
            return Surface;
        }

        // a small black glass sign above its head: a red dot, line 1 in the puck's dot lettering, line 2 smaller
        void Sign(Graphics g, float k)
        {
            float p1 = 2.6f * S, d1 = 1.9f * S, p2 = 2.1f * S, d2 = 1.5f * S;
            float w1 = Dots.W3(line1) * p1 + 12 * S, w2 = string.IsNullOrEmpty(line2) ? 0 : Dots.W3(line2) * p2;
            float w = Math.Max(w1, w2) + 28 * S, h = string.IsNullOrEmpty(line2) ? 30 * S : 44 * S;
            float bx = LW / 2f - w / 2, by = Foot - 52 * S - h + (1 - k) * 8 * S;
            using (var path = U.Round(bx, by, w, h, 9 * S))
            {
                using (var br = new SolidBrush(Color.FromArgb((int)(240 * k), Theme.T.Mid))) g.FillPath(br, path);
                using (var pen = new Pen(Color.FromArgb((int)(40 * k), Theme.T.Rim), 1 * S)) g.DrawPath(pen, path);
            }
            using (var br = new SolidBrush(Color.FromArgb((int)(240 * k), Theme.T.Mid)))   // the little tail toward its head
                g.FillPolygon(br, new[] { new PointF(LW / 2f - 6 * S, by + h - 1), new PointF(LW / 2f + 6 * S, by + h - 1), new PointF(LW / 2f, by + h + 7 * S) });
            using (var br = new SolidBrush(U.A(Red, k))) g.FillEllipse(br, bx + 14 * S, by + 11 * S, 6 * S, 6 * S);
            var d = new Dots(); d.Text3At(line1, 0, 0, 0.95f * k);
            Dots.Paint(g, d, bx + 26 * S, by + 9 * S, p1, d1, Theme.T.Ink, Red);
            if (!string.IsNullOrEmpty(line2))
            {
                var d2b = new Dots(); d2b.Text3At(line2, 0, 0, 0.55f * k);
                Dots.Paint(g, d2b, bx + 14 * S, by + 27 * S, p2, d2, Theme.T.Ink, Red);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (leaveT >= 0) return;
            if (e.Button == MouseButtons.Left) { if (click != null) try { click(); } catch (Exception ex) { Settings.Log(ex); } pet.Cheer(U.Now, true); Snd.Play("boing"); }
            else if (dismiss != null) try { dismiss(); } catch (Exception ex) { Settings.Log(ex); }
            GoHome();
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
