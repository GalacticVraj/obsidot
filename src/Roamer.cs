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
    // During a focus session it stays out as your focus buddy: on the taskbar, above every window, showing the
    // minutes left. Drift to a distracting site and it flies over onto it, cross, and counts down to closing it.
    class Roamer : LayeredForm
    {
        static readonly float S0 = U.S;
        readonly float S = U.S; readonly Pet pet; readonly Timer timer = new Timer { Interval = 15 };
        static Color Red { get { return Theme.T.Accent; } }
        static readonly Color Calm = Color.FromArgb(80, 186, 255);
        string line1; Func<string> line2; Action click, dismiss; double life, sayT, autoAt = -1;
        float x, y, vy, ground; bool falling = true; double t0, last, landT = -10, leaveT = -1;
        IntPtr perch, target; N.RECT perchRect;
        bool flying; double flyT0, flyDur; float fx0, fy0, fx1, fy1; IntPtr flyPerch; N.RECT flyRect;
        Rectangle homeWa;

        public bool Stay;                  // the focus buddy: stays out until released
        public Func<string> Idle;          // its little chip while it has nothing to say
        public Action<Point> PetMenu;         // right-click while it has nothing to say
        public bool Talking { get { return line1 != null; } }
        public bool Nagging { get { return target != IntPtr.Zero; } }
        public int AutoLeft { get { return autoAt < 0 ? 0 : (int)Math.Ceiling(autoAt - U.Now); } }
        int Foot { get { return LH - (int)(8 * S); } } // where its feet are inside this window

        public Roamer(Pet pet, string l1, string l2, Action click, double life, Action dismiss = null) : base((int)(300 * S0), (int)(200 * S0))
        {
            this.pet = pet;
            FindPerch();
            y = ground - 320 * S; t0 = last = U.Now;                     // it drops in from above
            pet.Roaming = true; pet.Asleep = false; pet.Scared = false; pet.Walking = false;
            timer.Tick += Frame;
            if (l1 != null) Say(l1, l2 == null ? null : (Func<string>)(() => l2), click, dismiss, life, IntPtr.Zero, 0);
        }

        // the focus buddy, standing on the taskbar of the screen the clock is on
        public static Roamer Buddy(Pet pet, Rectangle workingArea)
        {
            var r = new Roamer(pet, null, null, null, 0) { Stay = true };
            r.homeWa = workingArea; r.perch = IntPtr.Zero; r.ground = workingArea.Bottom; r.x = r.HomeX; r.y = r.ground - 320 * r.S;
            return r;
        }
        float HomeX { get { return homeWa.Right - 330 * S; } }

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

        // hold up a sign. `target`: fly over onto that window first. `autoSecs`: do `click` by itself after that long.
        public void Say(string l1, Func<string> l2, Action click, Action dismiss, double life, IntPtr target, double autoSecs)
        {
            line1 = l1; line2 = l2; this.click = click; this.dismiss = dismiss; this.life = life; sayT = U.Now;
            autoAt = autoSecs > 0 ? U.Now + autoSecs : -1;
            if (!falling && !flying) landT = U.Now;                       // the sign pops up again
            this.target = target;
            if (target != IntPtr.Zero) FlyTo(target);
        }

        // the sign goes away: home for a messenger, back to the taskbar for the buddy
        public void EndSay()
        {
            bool away = target != IntPtr.Zero;
            line1 = null; line2 = null; click = dismiss = null; autoAt = -1; target = IntPtr.Zero;
            if (!Stay) GoHome(); else if (away) Fly(HomeX, homeWa.Bottom, IntPtr.Zero, default(N.RECT));
        }

        // the focus session is over: go home once the current sign is done
        public void Release() { Stay = false; if (!Talking) GoHome(); }

        void FlyTo(IntPtr w)
        {
            N.RECT r; if (!N.GetWindowRect(w, out r)) return;
            var mon = Screen.FromHandle(w); Rectangle b = mon.Bounds, wa = mon.WorkingArea;
            if (r.T >= wa.Top + 70 * S && r.R - r.L >= 360 * S) Fly(r.R - 200 * S, r.T + 1, w, r);   // a title bar to stand on
            else                                                                                        // maximized or full screen: right onto the page
            {
                int top = Math.Max(r.T, b.Top), l = Math.Max(r.L, b.Left), rr = Math.Min(r.R, b.Right);
                Fly((l + rr) / 2f, top + 150 * S, w, r);
            }
        }

        void Fly(float gx, float gy, IntPtr p, N.RECT pr)
        {
            fx0 = x; fy0 = y; fx1 = gx; fy1 = gy; flyT0 = U.Now; flyPerch = p; flyRect = pr;
            flyDur = 0.55 + Math.Min(0.5, Math.Sqrt((gx - x) * (gx - x) + (gy - y) * (gy - y)) / (3000 * S));
            flying = true; falling = false; perch = IntPtr.Zero; vy = 0; Snd.Play("whoosh");
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x8; return cp; } } // TOPMOST
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SetFloat(true); timer.Start(); Frame(null, null); }

        public void GoHome() { if (leaveT < 0) { leaveT = U.Now; falling = flying = false; vy = -650 * S; Snd.Play("whoosh"); } }

        void Frame(object s, EventArgs e)
        {
            double now = U.Now, dt = Math.Min(0.05, now - last); last = now;
            if (leaveT < 0)
            {
                if (flying)   // a leap along an arc
                {
                    double k = U.Clamp01((now - flyT0) / flyDur), arc = 4 * k * (1 - k);
                    x = fx0 + (fx1 - fx0) * (float)k;
                    y = fy0 + (fy1 - fy0) * (float)k - (float)(arc * (140 * S + Math.Abs(fx1 - fx0) * 0.15f));
                    if (k >= 1) { flying = false; x = fx1; y = ground = fy1; perch = flyPerch; perchRect = flyRect; pet.Land(900 * S); landT = now; Snd.Play("boing"); }
                }
                else
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
                }
                if (Talking)
                {
                    if (autoAt > 0 && now >= autoAt && !flying)       // the countdown ran out: it does it itself
                    {
                        autoAt = -1; var act = click;
                        if (act != null) try { act(); } catch (Exception ex) { Settings.Log(ex); }
                        pet.Cheer(now, true); EndSay();
                    }
                    else if (now - sayT > life) EndSay();
                }
                else if (!Stay && now - t0 > 0.5) GoHome();
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
                double sk = flying ? 0 : U.Clamp01((now - landT - 0.08) / 0.22) * (leaveT < 0 ? 1 : a);
                string l1 = line1 ?? (Stay && Idle != null ? Idle() : null);
                if (sk > 0.01 && l1 != null) Sign(g, (float)sk, l1, Talking && line2 != null ? line2() : null, Talking ? Red : Calm);
                pet.DrawAt(g, LW / 2f, Foot, now, a);
            }
            return Surface;
        }

        // a small black glass sign above its head: a coloured dot, line 1 in the puck's dot lettering, line 2 smaller
        void Sign(Graphics g, float k, string l1, string l2, Color dot)
        {
            float p1 = 2.6f * S, d1 = 1.9f * S, p2 = 2.1f * S, d2 = 1.5f * S;
            float w1 = Dots.W3(l1) * p1 + 12 * S, w2 = string.IsNullOrEmpty(l2) ? 0 : Dots.W3(l2) * p2;
            float w = Math.Max(w1, w2) + 28 * S, h = string.IsNullOrEmpty(l2) ? 30 * S : 44 * S;
            float bx = LW / 2f - w / 2, by = Foot - 52 * S - h + (1 - k) * 8 * S;
            using (var path = U.Round(bx, by, w, h, 9 * S))
            {
                using (var br = new SolidBrush(Color.FromArgb((int)(240 * k), Theme.T.Mid))) g.FillPath(br, path);
                using (var pen = new Pen(Color.FromArgb((int)(40 * k), Theme.T.Rim), 1 * S)) g.DrawPath(pen, path);
            }
            using (var br = new SolidBrush(Color.FromArgb((int)(240 * k), Theme.T.Mid)))   // the little tail toward its head
                g.FillPolygon(br, new[] { new PointF(LW / 2f - 6 * S, by + h - 1), new PointF(LW / 2f + 6 * S, by + h - 1), new PointF(LW / 2f, by + h + 7 * S) });
            using (var br = new SolidBrush(U.A(dot, k))) g.FillEllipse(br, bx + 14 * S, by + 11 * S, 6 * S, 6 * S);
            var d = new Dots(); d.Text3At(l1, 0, 0, 0.95f * k);
            Dots.Paint(g, d, bx + 26 * S, by + 9 * S, p1, d1, Theme.T.Ink, Red);
            if (!string.IsNullOrEmpty(l2))
            {
                var d2b = new Dots(); d2b.Text3At(l2, 0, 0, 0.55f * k);
                Dots.Paint(g, d2b, bx + 14 * S, by + 27 * S, p2, d2, Theme.T.Ink, Red);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (leaveT >= 0) return;
            if (!Talking)   // the buddy, idle: a poke for a heart, right-click for the settings
            {
                if (e.Button == MouseButtons.Left) pet.Poke(U.Now);
                else if (PetMenu != null) PetMenu(Cursor.Position);
                return;
            }
            if (e.Button == MouseButtons.Left) { var act = click; if (act != null) try { act(); } catch (Exception ex) { Settings.Log(ex); } pet.Cheer(U.Now, true); Snd.Play("boing"); }
            else { var d = dismiss; if (d != null) try { d(); } catch (Exception ex) { Settings.Log(ex); } }
            EndSay();
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
