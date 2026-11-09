using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Obsidot
{
    // The resting object: a black glass puck with a dot-matrix display behind the glass.
    class Crystal : LayeredForm
    {
        public readonly float S = U.S;
        public readonly int W;
        public double cx, cy;
        readonly Timer timer = new Timer();
        double last; bool active, birthSound;
        public double birth0;
        public int mode; // 0 temporal, 1 hoops
        public Spring rS, modeS, menuS, hoverS, ix, iy;
        public bool hover, menuOpen;
        public PointF mouse; double lastHover = -10;
        bool down, dragging, gliding;
        Point downPt; double downCx, downCy, gvx, gvy, dvx, dvy;
        readonly List<double[]> hist = new List<double[]>();
        public int menuHover = -1;
        readonly double[] nodeHov = new double[6];
        const int Nodes = 6;

        // ---- focus timer: wind the red arc like a kitchen timer, it unwinds as time passes ----
        int timerState;                 // 0 off, 1 setting, 2 running, 3 done
        DateTime timerEnd; double setT, doneT = -10; int alarmN;
        readonly Spring dial = new Spring(0, 140, 17) { Eps = 0.01 }; // arc length, in minutes
        double Remaining { get { return (timerEnd - DateTime.UtcNow).TotalSeconds; } }

        void StartTimer() { timerState = 2; timerEnd = DateTime.UtcNow.AddMinutes(Settings.Focus); Settings.Save(); Snd.Play("buzz"); Vibe(2.4f, 0.2); Wake(); }

        void StepTimer(double now)
        {
            if (timerState == 1 && now - setT > 20) timerState = 0;                 // forgotten: put the dial back
            if (timerState == 2 && Remaining <= 0) { timerState = 3; doneT = now; alarmN = 0; Wake(); Roam("FOCUS DONE", You().Length > 0 ? "NICE WORK" + You() : "CLICK ME TO CLEAR", () => { timerState = 0; }, 60, true); }
            if (timerState == 3)
            {
                if (alarmN < 4 && now >= doneT + alarmN * 0.55) { Snd.Play(alarmN < 3 ? "buzz" : "done"); Vibe(3.2f, 0.3); alarmN++; }
                if (now - doneT > 600) timerState = 0;
            }
            if (timerState == 2) dial.Snap(Math.Max(0, Remaining) / 60); else dial.T = timerState == 1 ? Settings.Focus : timerState == 3 ? 60 : 0;
        }

        // ---- music: dot rays round the outside (a spectrum, bass at twelve o'clock), and the edge turns liquid with the kick ----
        readonly float[] lr = new float[2], spec = new float[48], bands = new float[48];
        double gain = 0.2, musicT = -10, musicK, level, kickT = -10, lastBass;

        void Listen(double now, double dt)
        {
            float p = Meter.Peak(lr);
            gain = Math.Max(0.08, Math.Max(p, gain * Math.Exp(-dt / 4)));      // slow auto-gain: quiet or loud, it uses its full range
            if (p > 0.02) musicT = now;
            double lv = Math.Pow(Math.Min(1, p / gain), 0.8);
            level = lv > level ? level + (lv - level) * (1 - Math.Exp(-dt / 0.03)) : level * Math.Exp(-dt / 0.2);
            musicK += ((now - musicT < 1.5 ? 1 : 0) - musicK) * (1 - Math.Exp(-4 * dt)); if (musicK < 0.005) musicK = 0;
            if (musicK == 0) { Meter.StopCapture(); return; }
            bool spectral = Meter.Capture() && Meter.Spectrum(spec, dt);
            double bass = 0;
            for (int b = 0; b < bands.Length; b++)
            {
                double t = spectral ? spec[b] : level * (0.95 - 0.55 * b / bands.Length); // no capture: a plain level-driven fan
                bands[b] += (float)((t - bands[b]) * (1 - Math.Exp(-dt / (t > bands[b] ? 0.02 : 0.13))));
                if (b < 5) bass += (spectral ? spec[b] : lv) / 5;
            }
            if (bass > lastBass + 0.1 && bass > 0.45 && now - kickT > 0.22) kickT = now; // the kick: the low bands jump
            lastBass = bass;
        }

        double Kick(double now) { return Math.Exp(-(now - kickT) * 7); }
        readonly Spring pointer = new Spring(0, 220, 19);   // the red pointer inside the rim; in button steps
        int wheelAcc; double lastStep;
        double vibT = -10, vibA, vibD;                      // the puck's haptic shake

        void Vibe(float amp, double dur) { vibT = U.Now; vibA = amp * S; vibD = dur; Wake(); }
        public void ShotPoint(int i) { pointer.Snap(i); menuHover = i; } // shots harness
        public void ShotTimer(int s) { timerState = s; setT = doneT = U.Now; timerEnd = DateTime.UtcNow.AddMinutes(17.5); }
        public void musicK0() { musicK = 0; }
        // ---- animation harness hooks (README demos) ----
        public void ShotRipple(double t, PointF at) { rippleT = t; rippleAt = at; }
        public void ShotWipe(double t)
        {
            var o = DateTime.Now.AddMinutes(-1);
            bool h24 = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H");
            prevTime = o.ToString(h24 ? "HH" : "%h") + ":" + o.ToString("mm"); changeT = t;
        }
        public void ShotMusicAt(double t, int frame)
        {
            musicK = 1; level = 0.55 + 0.3 * Math.Abs(Math.Sin(t * 3.1));
            double beat = 0.48, ph = (t % beat) / beat; if (frame % 12 == 0) kickT = t;
            for (int b = 0; b < bands.Length; b++)
            {
                double bass = b < 8 ? 0.95 * Math.Exp(-ph * 3.2) : 0, mid = 0.25 + 0.4 * Math.Abs(Math.Sin(t * 4 + b * 0.55)) * Math.Exp(-b / 46.0);
                bands[b] = (float)U.Clamp01(Math.Max(bass, mid) * (0.85 + 0.15 * Math.Sin(t * 13 + b)));
            }
        }
        public void ShotSwing(int i) { PointTo(i); }
        public void ShotFocus(int minutes, double t) { timerState = 1; setT = t; Settings.Focus = minutes; }

        public void ShotBands(float v) { for (int b = 0; b < bands.Length; b++) bands[b] = v; }
        public void ShotMusic() { for (int b = 0; b < bands.Length; b++) bands[b] = (float)U.Clamp01(0.95 - b * 0.012 - 0.35 * Math.Abs(Math.Sin(b * 0.55))); musicK = 1; level = 0.75; kickT = U.Now - 0.06; }

        int Selected() { return ((int)Math.Round(pointer.T) % Nodes + Nodes) % Nodes; }

        void OpenMenu() { menuOpen = true; menuHover = Selected(); wheelAcc = 0; Snd.Play("buzz"); Vibe(2.6f, 0.22); Wake(); }

        // swing the pointer to button i the short way round
        void PointTo(int i)
        {
            double t = pointer.T, d = ((i - t) % Nodes + Nodes) % Nodes; if (d > Nodes / 2.0) d -= Nodes;
            pointer.T = Math.Round(t + d); menuHover = i; Snd.Play("haptic"); Vibe(1f, 0.07); Wake();
        }

        // one full notch = one step, at most ~11 steps a second; leftovers are dropped so touchpads cannot race
        protected override void OnMouseWheel(MouseEventArgs e)
        {

            if (!menuOpen && timerState == 1)
            {
                wheelAcc += e.Delta;
                if (Math.Abs(wheelAcc) < 120 || U.Now - lastStep < 0.07) return;
                int f = Settings.Focus + (wheelAcc < 0 ? 5 : -5); wheelAcc = 0; lastStep = setT = U.Now;
                if (f < 5 || f > 60) { Vibe(0.7f, 0.05); return; }      // the dial's end stops
                Settings.Focus = f; Snd.Play("haptic"); Vibe(1.3f, 0.08); Wake(); return;
            }
            if (!menuOpen) { OpenMenu(); return; }
            wheelAcc += e.Delta;
            if (Math.Abs(wheelAcc) < 120 || U.Now - lastStep < 0.09) return;
            pointer.T += wheelAcc < 0 ? 1 : -1; wheelAcc = 0; lastStep = U.Now;
            menuHover = Selected(); Snd.Play("haptic"); Vibe(1.3f, 0.08); Wake();
        }

        int NodeAt(Point e)
        {
            float hb = 24 * S;
            for (int i = 0; i < Nodes; i++) { var p = NodePos(i, (float)rS.X); if (Math.Abs(p.X - e.X) < hb && Math.Abs(p.Y - e.Y) < hb) return i; }
            return -1;
        }
        public Card card;
        public Assault game;
        public Hoops hoops;
        double quitT = -1;

        public Crystal() : base((int)(460 * U.S), (int)(460 * U.S))
        {
            W = LW;
            rS = new Spring(92 * S, 120, 14) { Eps = 0.05 };
            modeS = new Spring(0, 90, 15); menuS = new Spring(0, 260, 21); hoverS = new Spring(0, 60, 14);
            ix = new Spring(0, 160, 9) { Eps = 0.05 }; iy = new Spring(0, 160, 9) { Eps = 0.05 };
            Theme.Use(0);
            hoops = new Hoops(this);
            var wa = Screen.PrimaryScreen.WorkingArea;
            cx = double.IsNaN(Settings.X) ? wa.Right - 230 * S : Settings.X;                 // right edge, clear of title-bar buttons
            cy = double.IsNaN(Settings.Y) ? wa.Top + wa.Height * 0.45 : Settings.Y;
            Contain(false);
            Location = new Point((int)(cx - W / 2), (int)(cy - W / 2));
            card = new Card(this);
            timer.Tick += Tick;
            birth0 = U.Now + 0.35; last = U.Now;
        }

        float R0 { get { return (mode == 1 ? 142 : 98) * S; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            N.RegisterHotKey(Handle, 7, 0x0001 | 0x4000, (int)Keys.K); // ALT+K, no repeat (Alt+G belongs to Gemini)
            N.RegisterHotKey(Handle, 8, 0x0001 | 0x0004 | 0x4000, (int)Keys.N); // ALT+SHIFT+N: the scratchpad, from any app
            SetFloat(Settings.Float);
            fgHook = OnForeground; N.SetWinEventHook(3, 3, IntPtr.Zero, fgHook, 0, 0, 0); // EVENT_SYSTEM_FOREGROUND, event-driven (no polling)
            OnForeground(IntPtr.Zero, 3, N.GetForegroundWindow(), 0, 0, 0, 0);
            timer.Start(); SetActive(true);
            if (BenchLog == null && !Covered()) try { StartEntrance(); } catch (Exception ex) { Settings.Log(ex); } // the login welcome
            if (BenchLog == null) WatchDownloads();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            N.UnregisterHotKey(Handle, 7); N.UnregisterHotKey(Handle, 8); if (downloads != null) downloads.Dispose(); if (roamer != null) roamer.Close(); timer.Stop(); WatchOutside(false); if (active) N.timeEndPeriod(1);
            Settings.X = cx; Settings.Y = cy; Settings.Save();
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312) // WM_HOTKEY
            {
                if (m.WParam.ToInt32() == 8) { if (game == null) OpenScratch(); return; }
                if (game == null) StartAssault(); else game.Exit(); return;
            }
            base.WndProc(ref m);
        }

        N.WinEventProc fgHook; // kept alive for the hook

        void OnForeground(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time)
        {
            if (hwnd == Handle || (card.IsHandleCreated && hwnd == card.Handle)) return;
            var sb = new System.Text.StringBuilder(64); N.GetClassName(hwnd, sb, 64);
            bool onDesk = sb.ToString() == "Progman" || sb.ToString() == "WorkerW";
            Raise(onDesk); card.Raise(onDesk);
            if (onDesk) Wake();
            // AUTO: come back to the desktop after an hour without a card, and one unfolds by itself
            if (onDesk && Settings.Auto && game == null && mode == 0 && quitT < 0 && !card.Open && U.Now - card.LastShown > 3600)
                BeginInvoke(new Action(() => { if (!card.Open) card.ShowAuto(); }));
        }

        public static string BenchLog;
        static void Mk(int i) { U.Mk(i); } double benchT0 = -1, benchMs; int benchN;
        void Bench(double now, double ms)
        {
            if (benchT0 < 0) benchT0 = now;
            double t = now - benchT0;
            if (t < 3) { hover = true; mouse = new PointF(W / 2f + 80 * S, W / 2f - 60 * S); }
            U.Prof = t > 1 && t < 3;
            if (t > 1 && t < 3) { benchMs += ms; benchN++; }
            if (t >= 3 && benchN > 0)
            {
                System.IO.File.AppendAllText(BenchLog, "crystal active: " + (benchMs / benchN).ToString("0.00") + " ms/frame over " + benchN + " frames\r\n");
                System.IO.File.AppendAllText(BenchLog, "  sections " + U.Dump(benchN) + "\r\n");
                benchN = 0; hover = false; StartAssault(); game.Bench = true;
                game.FormClosed += delegate { quitT = U.Now; };
            }
        }

        bool covered, settled;
        void SetActive(bool a)
        {
            // touched: 60+ fps. resting: beats at 4 Hz like a 28,800 vph movement. covered by a window: nearly asleep.
            int iv = a ? 15 : covered ? 700 : musicK > 0 ? 33 : 250; // music playing: ~30 fps
            if (timer.Interval != iv) timer.Interval = iv;
            if (a == active) return;
            active = a;
            if (a) N.timeBeginPeriod(1); else N.timeEndPeriod(1);
        }

        bool Covered()
        {
            if (Settings.Float) return false;
            IntPtr fg = N.GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == Handle || !N.IsWindowVisible(fg) || N.IsIconic(fg)) return false;
            var sb = new System.Text.StringBuilder(64); N.GetClassName(fg, sb, 64);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
            N.RECT r; if (!N.GetWindowRect(fg, out r)) return false;
            double e = rS.X * 1.1;
            return r.L <= cx - e && r.T <= cy - e && r.R >= cx + e && r.B >= cy + e;
        }
        public void Wake() { SetActive(true); }

        bool Busy(double now)
        {
           return hover || down || gliding || settling || menuOpen || quitT >= 0 || now - birth0 < 2.6 || (mode == 1 && hoops.Busy) ||
                !menuS.Rest || !pointer.Rest || !dial.Rest || pet.Busy(now) || Greeting(now) || (timerState == 3 && now - doneT < 3) || now - vibT < vibD + 0.05 || !rS.Rest || !modeS.Rest || (now - lastHover < 1.5) || now - changeT < 0.6 || now - rippleT < 0.8 || !ix.Rest || !iy.Rest || !hoverS.Rest;
        }

        void Tick(object s, EventArgs e)
        {
            double now = U.Now, dt = Math.Min(0.1, now - last); last = now;
            if (!birthSound && now >= birth0) { birthSound = true; Snd.Play("birth"); }
            if (!settled && now - birth0 > 3) { settled = true; GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } // drop startup synthesis garbage
            Step(dt, now);
            if (quitT >= 0 && now - quitT > 0.55) { timer.Stop(); card.Close(); if (pad != null) pad.Close(); Close(); return; }
            covered = !Visible || (!hover && !down && !card.Open && Covered());
            if (greetPending && !covered && game == null && quitT < 0 && now > birth0 + 1.0) // first time you see it: say hello, then the pet drops in
            {
                if (!Settings.Asked) { if (namePrompt == null) AskName(); }               // the very first run asks your name first
                else { greetPending = false; Greet(Hello(), now); pet.Arrive(now + GreetDur() + 0.2); }
            }
            if (!covered && game == null) Listen(now, dt); else if (musicK > 0) { musicK = 0; Meter.StopCapture(); }
            if (!covered)
            {
                var t0 = System.Diagnostics.Stopwatch.StartNew();
                Render(now);
                if (BenchLog != null && game == null && quitT < 0) Bench(now, t0.Elapsed.TotalMilliseconds);
                Present((int)Math.Round(cx - W / 2.0), (int)Math.Round(cy - W / 2.0));
            }
            if (card.Open) card.Follow();
            SetActive(!covered && Busy(now));
            WatchOutside(menuOpen || card.Open || timerState == 1);
            if (BenchLog == null) Watch(now);
        }

        IntPtr mouseHook; N.LowLevelProc hookProc;
        void WatchOutside(bool on)
        {
            if (on && mouseHook == IntPtr.Zero) { hookProc = OnGlobalMouse; mouseHook = N.SetWindowsHookEx(14, hookProc, N.GetModuleHandle(null), 0); }
            else if (!on && mouseHook != IntPtr.Zero) { N.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
        }

        IntPtr OnGlobalMouse(int code, IntPtr w, IntPtr l)
        {
            int m = w.ToInt32();
            if (code >= 0 && (m == 0x201 || m == 0x204 || m == 0x207)) // a button went down somewhere: only where is read, nothing kept
            {
                int x = System.Runtime.InteropServices.Marshal.ReadInt32(l, 0), y = System.Runtime.InteropServices.Marshal.ReadInt32(l, 4);
                BeginInvoke(new Action(() => ClickedAt(x, y)));
            }
            return N.CallNextHookEx(IntPtr.Zero, code, w, l);
        }

        void ClickedAt(int x, int y)
        {
            double dx = x - cx, dy = y - cy, reach = menuOpen ? rS.X + 72 * S : rS.X * 1.1;
            if (dx * dx + dy * dy < reach * reach || card.PlateContains(x, y)) return;
            if (menuOpen) { menuOpen = false; menuHover = -1; Wake(); }
            if (card.Open) card.Dismiss();
            if (timerState == 1) { timerState = 0; Wake(); }
        }

        public void Step(double dt, double now)
        {
            rS.T = R0; modeS.T = mode; menuS.T = menuOpen ? 1 : 0; hoverS.T = hover ? 1 : 0;

            if (gliding)
            {
                cx += gvx * dt; cy += gvy * dt;
                double f = Math.Exp(-3.2 * dt); gvx *= f; gvy *= f;
                Contain(true);
                if (gvx * gvx + gvy * gvy < 400 * S * S) { gliding = false; Settings.X = cx; Settings.Y = cy; Settings.Save(); }
            }
            if (!dragging && !gliding)
            {
                double tx = cx, ty = cy; Inside(ref tx, ref ty, Math.Max(R0, rS.X));
                if (Math.Abs(tx - cx) + Math.Abs(ty - cy) > 0.5)
                {
                    double k = 1 - Math.Exp(-9 * dt); cx += (tx - cx) * k; cy += (ty - cy) * k; settling = true;
                }
                else if (settling) { settling = false; Settings.X = cx; Settings.Y = cy; Settings.Save(); }
            }
            double vx = dragging ? dvx : gliding ? gvx : 0, vy = dragging ? dvy : gliding ? gvy : 0;
            ix.T = U.Clamp(-vx * 0.014, -16 * S, 16 * S); iy.T = U.Clamp(-vy * 0.014, -16 * S, 16 * S);
            StepTimer(now); StepHold(now);

            {
                float pr = (float)rS.X; var mp = Control.MousePosition;
                float gy = -(float)rS.X;
                var eye = new PointF((float)(mp.X - (cx + pet.X)), (float)(mp.Y - (cy + gy - 14 * S)));
                if (pet.Step(dt, now, pr, Pet.IdleSeconds(), !covered && Visible, musicK > 0.3, kickT, eye) && game == null) Greet("WELCOME BACK" + You(), now);
            }
            foreach (var sp in new[] { rS, modeS, menuS, hoverS, ix, iy, pointer, dial }) sp.Step(dt);
            if (menuOpen && !hover && now - lastHover > 1.2) { menuOpen = false; menuHover = -1; } // drifted away: fold the menu
            for (int i = 0; i < Nodes; i++) nodeHov[i] += ((menuHover == i ? 1 : 0) - nodeHov[i]) * (1 - Math.Exp(-18 * dt));
            if (mode == 1 || modeS.X > 0.01) hoops.Step(dt, now);
        }

        bool settling;

        // nearest centre that keeps the whole puck (wall and shadow included) on its screen
        void Inside(ref double x, ref double y, double R)
        {
            var wa = Screen.FromPoint(new Point((int)x, (int)y)).WorkingArea;
            double r = R * 1.1;
            x = U.Clamp(x, wa.Left + r, wa.Right - r); y = U.Clamp(y, wa.Top + r, wa.Bottom - r * 1.08);
        }

        void Contain(bool bounce)
        {
            var wa = Screen.FromPoint(new Point((int)cx, (int)cy)).WorkingArea;
            double r = R0 * 1.1;
            if (cx < wa.Left + r) { cx = wa.Left + r; if (bounce) gvx = Math.Abs(gvx) * 0.45; }
            if (cx > wa.Right - r) { cx = wa.Right - r; if (bounce) gvx = -Math.Abs(gvx) * 0.45; }
            if (cy < wa.Top + r) { cy = wa.Top + r; if (bounce) gvy = Math.Abs(gvy) * 0.45; }
            if (cy > wa.Bottom - r) { cy = wa.Bottom - r; if (bounce) gvy = -Math.Abs(gvy) * 0.45; }
        }

        // ---------------- actions ----------------
        void Act(int i)
        {
            menuOpen = false; menuHover = -1; Vibe(2f, 0.15);
            switch (i)
            {
                case 0: if (mode == 1) SetMode(0); card.Toggle(); break;
                case 1: SetMode(mode == 1 ? 0 : 1); break;
                case 2: StartAssault(); break;
                case 3: Settings.Float = !Settings.Float; SetFloat(Settings.Float); card.SetFloat(Settings.Float); Settings.Save(); Snd.Play("tick"); break;
                case 5: OpenScratch(); break;

                case 4:
                    if (timerState >= 2) { timerState = 0; Snd.Play("close"); }
                    else { if (mode == 1) SetMode(0); if (card.Open) card.Dismiss(); timerState = 1; setT = U.Now; Snd.Play("open"); }
                    break;
            }
            Wake();
        }

        // ---- the pet, the greeting, the login entrance ----
        public readonly Pet pet = new Pet { ArriveAt = 1e12 }; // hidden until the welcome is done
        string greetText; double greetT0 = -10; bool greetPending = true;
        double GreetDur() { return greetText == null ? 0 : (34 + Dots.W3(greetText)) / 20.0; }
        bool Greeting(double now) { return greetText != null && now - greetT0 < GreetDur(); }
        public void Greet(string s, double now) { greetText = s; greetT0 = now; Wake(); }

        // your name as the dot lettering can show it (" VRAJ"), or nothing
        static string You()
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in (Settings.Name ?? "").ToUpperInvariant()) if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == ' ' || ch == '-' || ch == '.') sb.Append(ch);
            string s = sb.ToString().Trim();
            return s.Length > 0 ? " " + s : "";
        }

        NamePrompt namePrompt;
        void AskName()
        {
            if (namePrompt != null && !namePrompt.IsDisposed) { namePrompt.Activate(); return; }
            float r = (float)rS.X;
            namePrompt = new NamePrompt(new RectangleF((float)cx - r, (float)cy - r, r * 2, r * 2), Settings.Name, name =>
            {
                if (name != null) Settings.Name = name.Replace("\r", "").Replace("\n", "").Replace("=", "").Trim();
                Settings.Asked = true; Settings.Save();
                if (!greetPending && name != null && You().Length > 0) Greet("HELLO" + You(), U.Now);        // changed later: say hello by the new name
                Wake();
            });
            namePrompt.FormClosed += delegate { namePrompt.Dispose(); namePrompt = null; };
            namePrompt.Show();
        }

        static string Hello()
        {
            int h = DateTime.Now.Hour;
            return (h >= 5 && h < 12 ? "GOOD MORNING" : h >= 12 && h < 17 ? "GOOD AFTERNOON" : h >= 17 && h < 22 ? "GOOD EVENING" : "HELLO NIGHT OWL") + You();
        }

        // where the matrix dots are on screen, and which of them spell the time
        public List<PointF> EntranceTargets(List<bool> lit)
        {
            var d = new Dots(); var t = DateTime.Now; var rnd = new Random();
            bool h24 = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H");
            d.Text5(t.ToString(h24 ? "HH" : "%h") + ":" + t.ToString("mm"), "", 1, -3, 1);
            var tg = new List<PointF>(); double nr = Nr((float)rS.X); int n = (int)nr;
            for (int r = -n; r <= n; r++)
                for (int k = -n; k <= n; k++)
                {
                    if (k * k + r * r > nr * nr) continue;
                    bool on = d.On.ContainsKey(Dots.K(k, r));
                    if (on || rnd.NextDouble() < 0.32) { tg.Add(new PointF((float)cx + k * Pitch, (float)cy + r * Pitch)); lit.Add(on); }
                }
            return tg;
        }

        void StartEntrance()
        {
            double start = U.Now + 0.2; var lit = new List<bool>();
            var tg = EntranceTargets(lit);
            var en = new Entrance(Screen.FromPoint(new Point((int)cx, (int)cy)).Bounds, tg, lit, start);
            birth0 = start + 0.75; birthSound = false;           // the puck materializes as the first dots land
            en.FormClosed += delegate { en.Dispose(); GC.Collect(); };
            en.Show(); Snd.Play("magic");
        }

        // ---- the pet steps out on top of every screen when it has something useful to say ----
        Roamer roamer; readonly List<object[]> roamQueue = new List<object[]>(); // {line1, line2, action, life, urgent}
        double lastCheck = -10, activeSec; bool breakSaid; int batteryWarned; System.IO.FileSystemWatcher downloads;
        readonly Dictionary<string, double> seenFiles = new Dictionary<string, double>();

        public void Roam(string l1, string l2, Action act, double life, bool urgent)
        {
            roamQueue.RemoveAll(q => (string)q[0] == l1 && (string)q[1] == l2);
            roamQueue.Add(new object[] { l1, l2, act, life, urgent });
            NextRoam();
        }

        void NextRoam()
        {
            if (roamer != null || roamQueue.Count == 0 || game != null || quitT >= 0) return;
            var q = roamQueue[0];
            if (!(bool)q[4] && Fullscreen()) return;                       // never interrupt a film or a game; try again later
            roamQueue.RemoveAt(0);
            roamer = new Roamer(pet, (string)q[0], (string)q[1], (Action)q[2], (double)q[3]);
            roamer.FormClosed += delegate { roamer.Dispose(); roamer = null; pet.Roaming = false; pet.Arrive(U.Now + 0.1); Wake(); NextRoam(); };
            roamer.Show();
        }

        static bool Fullscreen()
        {
            IntPtr fg = N.GetForegroundWindow(); N.RECT r;
            if (fg == IntPtr.Zero || !N.GetWindowRect(fg, out r)) return false;
            var sb = new System.Text.StringBuilder(64); N.GetClassName(fg, sb, 64);
            if (sb.ToString() == "Progman" || sb.ToString() == "WorkerW") return false;
            var b = Screen.FromHandle(fg).Bounds;
            return r.L <= b.Left && r.T <= b.Top && r.R >= b.Right && r.B >= b.Bottom;
        }

        // once a second: the break counter, the battery, a stalled queue
        void Watch(double now)
        {
            if (now - lastCheck < 1) return;
            double step = lastCheck < 0 ? 0 : Math.Min(5, now - lastCheck); lastCheck = now;
            double idle = Pet.IdleSeconds();
            if (idle > 300) { activeSec = 0; breakSaid = false; }           // you took a break: the count starts over
            else if (idle < 60) activeSec += step;
            if (activeSec > 50 * 60 && !breakSaid && timerState != 2) { breakSaid = true; Roam("BREAK TIME", "50 MIN NONSTOP", null, 30, false); }
            if (breakSaid && activeSec > 70 * 60) { breakSaid = false; activeSec = 50 * 60 - 1; }  // still going 20 min later: it asks again

            var ps = SystemInformation.PowerStatus;
            if ((ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && ps.BatteryLifePercent <= 1)
            {
                int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
                if (ps.PowerLineStatus == PowerLineStatus.Online) batteryWarned = 0;
                else if (pct <= 7 && batteryWarned < 2) { batteryWarned = 2; Roam("BATTERY " + pct + "%", "PLUG IN NOW", null, 40, true); }
                else if (pct <= 15 && batteryWarned < 1) { batteryWarned = 1; Roam("BATTERY " + pct + "%", "PLUG IN SOON", null, 30, true); }
            }
            NextRoam();
        }

        // a download that finished: the browser renames its temp file to the real name, or saves it directly
        void WatchDownloads()
        {
            try
            {
                string dir = KnownFolder(new Guid("374DE290-123F-4565-9164-39C4925E467B"));
                if (dir == null || !System.IO.Directory.Exists(dir)) return;
                downloads = new System.IO.FileSystemWatcher(dir) { IncludeSubdirectories = false, NotifyFilter = System.IO.NotifyFilters.FileName, SynchronizingObject = this };
                downloads.Created += (s, e) => Downloaded(e.FullPath);
                downloads.Renamed += (s, e) => Downloaded(e.FullPath);
                downloads.EnableRaisingEvents = true;
            }
            catch (Exception ex) { Settings.Log(ex); }
        }

        void Downloaded(string path)
        {
            string name = System.IO.Path.GetFileName(path), ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (name.StartsWith(".") || name.StartsWith("~") || ext == ".crdownload" || ext == ".part" || ext == ".partial" || ext == ".tmp" || ext == ".download" || ext == ".opdownload") return;
            double t; if (seenFiles.TryGetValue(path, out t) && U.Now - t < 30) return;
            seenFiles[path] = U.Now;
            var check = new Timer { Interval = 2000 };                     // give it two seconds to settle, then make sure it is really there
            check.Tick += delegate
            {
                check.Stop(); check.Dispose();
                try
                {
                    var fi = new System.IO.FileInfo(path); if (!fi.Exists || fi.Length == 0) return;
                    using (fi.Open(System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite)) { }
                }
                catch { return; }
                string shown = System.IO.Path.GetFileNameWithoutExtension(name); if (shown.Length > 16) shown = shown.Substring(0, 15) + "-";
                Roam("NEW FILE", (shown + ext).ToUpperInvariant(), () => System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""), 25, false);
            };
            check.Start();
        }

        static string KnownFolder(Guid id)
        {
            IntPtr p; if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out p) != 0) return null;
            string s = System.Runtime.InteropServices.Marshal.PtrToStringUni(p); System.Runtime.InteropServices.Marshal.FreeCoTaskMem(p); return s;
        }
        [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id, int flags, IntPtr token, out IntPtr path);

        Scratch pad;
        public void OpenScratch()
        {
            if (pad != null && !pad.IsDisposed) { pad.Activate(); return; }
            menuOpen = false; if (card.Open) card.Dismiss();
            float r = (float)rS.X; pad = new Scratch(new RectangleF((float)cx - r, (float)cy - r, r * 2, r * 2));
            pad.FormClosed += delegate { pad.Dispose(); pad = null; };
            pad.Show();
        }

        // turning Obsidot off: hold the puck still; a red ring fills, and at full it dissolves
        double downT, holdK; bool holdTick;
        void StepHold(double now)
        {
            if (!down || dragging || mode != 0 || quitT >= 0) { holdK = 0; holdTick = false; return; }
            double h = (now - downT - 0.6) / 1.6;
            if (h > 0 && !holdTick) { holdTick = true; Snd.Play("haptic"); Vibe(1f, 0.06); }
            holdK = U.Clamp01(h);
            if (h >= 1) { quitT = now; down = false; holdK = 0; Snd.Play("close"); Vibe(3f, 0.3); card.Dismiss(); if (pad != null) pad.Close(); }
        }

        void SetMode(int m)
        {
            if (m == mode) return;
            mode = m; Snd.Play("whoosh");
            if (m == 1) { card.Dismiss(); hoops.Reset(); }
            Wake();
        }

        // second launch of Obsidot.exe lands here: leave the game if in it, otherwise rise to the front and re-crystallize
        public void Summon()
        {
            if (game != null) { game.Exit(); return; }
            Contain(false);
            SetFloat(Settings.Float);
            birth0 = U.Now + 0.05; birthSound = false; Wake();
        }

        public void StartAssault()
        {
            if (game != null) return;
            menuOpen = false; card.HideNow(); Hide();
            if (roamer != null) roamer.Close();
            pet.Asleep = false; pet.Scared = false;
            N.DwmFlush(); N.DwmFlush(); // let the compositor drop the crystal before the screen is frozen
            game = new Assault(this, new PointF((float)cx, (float)cy));
            game.FormClosed += delegate
            {
                game.Dispose(); game = null;
                Show(); birth0 = U.Now + 0.05; birthSound = false; Wake();
                U.ClearGlows(); GC.Collect();
            };
            game.Show();
        }

        // ---------------- input ----------------
        PointF NodePos(int i, float R)
        {

            double k = U.Clamp(menuS.X * 1.15 - i * 0.03 * (1 - Math.Min(1, menuS.X)), 0, 1.3); // staggered while opening, one true circle at rest
            double a = (-90 + i * 360.0 / Nodes - (1 - Math.Min(1, k)) * 50) * Math.PI / 180, d = (R + 46 * S) * k;
            return new PointF((float)(W / 2 + Math.Cos(a) * d), (float)(W / 2 + Math.Sin(a) * d));
        }

        float Dist(PointF p) { float dx = p.X - W / 2f, dy = p.Y - W / 2f; return (float)Math.Sqrt(dx * dx + dy * dy); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mouse = e.Location; hover = true; lastHover = U.Now; Wake();
            if (menuOpen)
            {
                int h = NodeAt(e.Location);
                if (h >= 0 && h != Selected()) PointTo(h); // pointing at a button swings the pointer to it; the pick stays when you move off
            }
            if (down && mode == 1) { hoops.Drag(e.X - W / 2f, e.Y - W / 2f); return; }
            if (down && mode == 0)
            {
                var sp = Control.MousePosition;
                if (!dragging && Math.Abs(sp.X - downPt.X) + Math.Abs(sp.Y - downPt.Y) > 5 * S) { dragging = true; if (card.Open) card.Dismiss(); }
                if (dragging)
                {
                    cx = downCx + sp.X - downPt.X; cy = downCy + sp.Y - downPt.Y;
                    double t = U.Now; hist.Add(new[] { t, cx, cy });
                    while (hist.Count > 2 && t - hist[0][0] > 0.09) hist.RemoveAt(0);
                    double span = Math.Max(0.008, t - hist[0][0]);
                    dvx = (cx - hist[0][1]) / span; dvy = (cy - hist[0][2]) / span;
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = false; lastHover = U.Now; Wake(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Wake();

            if (e.Button == MouseButtons.Right && !menuOpen && pet.Hit(e.Location)) { AskName(); return; } // right-click the pet: what should I call you?
            if (e.Button == MouseButtons.Right)
            {
                if (menuOpen) { menuOpen = false; menuHover = -1; } else OpenMenu();
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            if (menuOpen)
            {
                int h = NodeAt(e.Location);
                if (h >= 0) Act(h);                                   // a button
                else if (Dist(e.Location) < rS.X) Act(Selected());    // the puck itself is the crown: press to confirm
                else { menuOpen = false; menuHover = -1; }
                return;
            }
            if (pet.Hit(e.Location)) { pet.Poke(U.Now); Wake(); return; }

            if (Dist(e.Location) > rS.X * 1.08) return;
            if (timerState == 1) { StartTimer(); return; }
            if (timerState == 3) { timerState = 0; Snd.Play("tick"); Vibe(1.5f, 0.1); Wake(); return; }
            down = true; dragging = false; gliding = false; hist.Clear(); downT = U.Now;
            rippleT = U.Now; rippleAt = e.Location; // a touch sends a ripple through the matrix
            downPt = Control.MousePosition; downCx = cx; downCy = cy; dvx = dvy = 0;
            if (mode == 1) hoops.Press(e.X - W / 2f, e.Y - W / 2f);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !down) return;
            down = false; Wake();
            if (holdTick) { holdK = 0; holdTick = false; return; } // let go before the ring filled: nothing happens
            if (mode == 1) { hoops.Release(); return; }
            if (dragging)
            {
                dragging = false;
                if (U.Now - hist[hist.Count - 1][0] > 0.06) { dvx = dvy = 0; }
                gvx = U.Clamp(dvx, -4000 * S, 4000 * S); gvy = U.Clamp(dvy, -4000 * S, 4000 * S); gliding = true;
            }
            else card.Toggle();
        }

        // ---------------- rendering ----------------
        // A matte black glass puck. Behind the glass, a dot-matrix display whose unlit dots stay faintly visible,
        // like real hardware. One accent colour: the red second. No glows, no facets: a still, physical object.
        static Color Red { get { return Theme.T.Accent; } } // the one accent; red in Obsidian
        float Pitch { get { return 5.2f * S; } }
        readonly Dictionary<int, Bitmap> faces = new Dictionary<int, Bitmap>(); // per size, theme, and with/without rim lines
        double rippleT = -10; PointF rippleAt;
        string lastTime = "", prevTime = ""; double changeT = -10;

        public void Render(double now)
        {
            using (var g = Graphics.FromImage(Surface))
            {
                Mk(15);
                g.Clear(Color.Transparent); Mk(0);
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                int C = W / 2;
                double age = now - birth0;
                float grow = (float)U.Spr(Math.Max(0, age - 0.1) / 0.8, 7, 9);
                if (quitT >= 0) { double q = U.Clamp01((now - quitT) / 0.45); grow *= (float)(1 + 0.08 * Math.Sin(q * Math.PI) - U.InCubic(q) * 1.08); }
                float R = (float)rS.X * Math.Max(0, grow);
                float m = (float)U.Clamp01(modeS.X);

                if (menuOpen || menuS.X > 0.02) // alpha-1 pixels: invisible, but they keep the mouse inside the menu
                    using (var br = new SolidBrush(Color.FromArgb(1, 0, 0, 0))) { float cr = (float)rS.X + 72 * S; g.FillEllipse(br, C - cr, C - cr, cr * 2, cr * 2); }
                if (R < 2) return;

                // tilt: while it moves, the face slides against the motion and the edge wall shows
                int ox = (int)Math.Round(ix.X * 0.35), oy = (int)Math.Round(iy.X * 0.35);
                double vt = now - vibT; // haptic: a fast decaying shake, like a phone buzzing on a desk
                if (vt < vibD && !U.Calm) { double va = vibA * (1 - vt / vibD); ox += (int)Math.Round(Math.Sin(vt * Math.PI * 2 * 41) * va); oy += (int)Math.Round(Math.Cos(vt * Math.PI * 2 * 29) * va * 0.4); }
                float wall = 5 * S;

                float mus = (float)(musicK * (1 - U.Clamp01(menuS.X * 1.5))); if (R < 20 * S) mus = 0;
                if (mus > 0.01) Rays(g, C, C, R, mus);
                U.GlowR(g, Color.Black, new RectangleF(C - R * 1.05f, C + R * 0.45f, R * 2.1f, R * 0.85f), 0.55 * Math.Min(1, grow)); // contact shadow
                if (mus > 0.01) Liquid(g, C + ox, C + oy, R, now, mus, wall);
                else
                {
                    using (var br = new SolidBrush(Theme.T.Wall)) g.FillEllipse(br, C - R, C - R + wall, R * 2, R * 2);
                    using (var pen = new Pen(Color.FromArgb(30, Theme.T.Shine), 1 * S)) g.DrawArc(pen, C - R + 1, C - R + wall, R * 2 - 2, R * 2 - 1, 25, 130);
                    var face = Face(R, false); int half = face.Width / 2;
                    g.DrawImageUnscaled(face, C - half + ox, C - half + oy);
                }
                Mk(2);

                var st = g.Save();
                if (m > 0.001) using (var clip = new GraphicsPath()) { clip.AddEllipse(C + ox - R + 2 * S, C + oy - R + 2 * S, R * 2 - 4 * S, R * 2 - 4 * S); g.SetClip(clip, CombineMode.Intersect); }
                Matrix(g, C + ox, C + oy, R, now, age, m); Mk(3);
                if (m > 0.001) hoops.Draw(g, C + ox, C + oy, R, now, m);
                g.Restore(st);
                Seconds(g, C + ox, C + oy, R, age * (1 - U.Clamp01(menuS.X * 1.5)) * (1 - U.Clamp01(dial.X * 3))); Mk(4);
                if (holdK > 0.001 && R > 12 * S)
                    using (var pen = new Pen(Red, 2.6f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { float tr = R - 4.6f * S; g.DrawArc(pen, C + ox - tr, C + oy - tr, tr * 2, tr * 2, -90, (float)(360 * holdK)); }
                if (dial.X > 0.02 && R > 12 * S) TimerArc(g, C + ox, C + oy, R, now, (float)(1 - U.Clamp01(menuS.X * 1.5)));
                if (R > 20 * S && m < 0.01) pet.Draw(g, C + ox, C + oy, R, now, (float)((1 - U.Clamp01(menuS.X * 1.5)) * Math.Min(1, grow)));
                if (menuS.X > 0.01 && R > 12 * S) Pointer(g, C + ox, C + oy, R, (float)U.Clamp01(menuS.X));
                if (menuS.X > 0.01) DrawMenu(g, (float)rS.X);
            }
        }

        double Nr(float R) { return (R - 9 * S) / Pitch; } // usable matrix radius, in dots

        Bitmap Face(float R, bool bare)
        {
            int key = ((int)Math.Round(R * 2) * 16 + Theme.Index) * 2 + (bare ? 1 : 0); Bitmap b;
            if (faces.TryGetValue(key, out b)) return b;
            if (faces.Count >= 8) { foreach (var f in faces.Values) f.Dispose(); faces.Clear(); } // sizes change while it is born or quits
            return faces[key] = MakeFace(R, bare);
        }
        Bitmap MakeFace(float R, bool bare)
        {
            float ext = 0; int side = (int)Math.Ceiling(R + ext) * 2 + 8; float h = side / 2f, P = Pitch, d = P * 0.66f;
            var face = new Bitmap(side, side, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(face))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
                using (var p = new GraphicsPath())
                {
                    float E = R + ext;
                    p.AddEllipse(h - E, h - E, E * 2, E * 2);
                    using (var lg = new LinearGradientBrush(new PointF(0, h - E - 1), new PointF(0, h + E + 1), Theme.T.Top, Theme.T.Bot))
                        g.FillPath(lg, p);
                }                double nr = Nr(R); int n = (int)nr;
                using (var br = new SolidBrush(Color.FromArgb(Theme.T.Unlit, Theme.T.Ink)))
                    for (int r = -n; r <= n; r++)
                        for (int c = -n; c <= n; c++)
                            if (c * c + r * r <= nr * nr) g.FillEllipse(br, h + c * P - d / 2, h + r * P - d / 2, d, d);
                float tr = R - 4.6f * S;
                using (var br = new SolidBrush(Color.White))
                    for (int i = 0; i < 60; i++)
                    {
                        double th = i * Math.PI / 30 - Math.PI / 2; float s = (i % 5 == 0 ? 0.95f : 0.6f) * S;
                        br.Color = Color.FromArgb(i % 5 == 0 ? 70 : 30, Theme.T.Ink);
                        g.FillEllipse(br, h + (float)Math.Cos(th) * tr - s, h + (float)Math.Sin(th) * tr - s, s * 2, s * 2);
                    }
                if (!bare) using (var pen = new Pen(Color.FromArgb(48, Theme.T.Rim), 1 * S)) g.DrawEllipse(pen, h - R + 0.5f, h - R + 0.5f, R * 2 - 1, R * 2 - 1);
                if (R > 4 * S && !bare) using (var pen = new Pen(Color.FromArgb(95, Theme.T.Shine), 1.1f * S)) g.DrawArc(pen, h - R + 1.4f * S, h - R + 1.4f * S, R * 2 - 2.8f * S, R * 2 - 2.8f * S, 208, 124); // the one highlight
            }
            return face;
        }

        // the red second: a single dot travelling the rim, with a short fading tail
        void Seconds(Graphics g, float x, float y, float R, double age)
        {
            double a = U.Clamp01((age - 0.6) / 0.4); if (a <= 0) return;
            var t = DateTime.Now; double sec = t.Second + t.Millisecond / 1000.0;
            float tr = R - 4.6f * S;
            using (var br = new SolidBrush(Red))
                for (int k = 4; k >= 0; k--)
                {
                    double th = (sec - k * 0.35) * Math.PI / 30 - Math.PI / 2; float s = (k == 0 ? 2.1f : 1.5f - k * 0.2f) * S;
                    br.Color = U.A(Red, a * (k == 0 ? 1 : 0.45 - k * 0.09));
                    g.FillEllipse(br, x + (float)Math.Cos(th) * tr - s, y + (float)Math.Sin(th) * tr - s, s * 2, s * 2);
                }
        }

        // B: rays of dots out of the rim, a round spectrum analyser. Bass at twelve o'clock, treble at six, mirrored left and right.
        void Rays(Graphics g, float x, float y, float R, float k)
        {
            int top = 0; for (int b = 1; b < bands.Length; b++) if (bands[b] > bands[top]) top = b;
            float sp = 5.6f * S, dr = 1.7f * S, r0 = R + 15 * S; int nb = bands.Length;
            int kq = (int)Math.Round(k * 8); if (kq <= 0) return;
            if (dotSpr == null || dotK != kq * 16 + Theme.Index) MakeDots(kq, dr);
            int o = dotSpr[0].Width / 2;
            for (int i = 0; i < nb * 2; i++)
            {
                int b = i < nb ? i : nb * 2 - 1 - i, n = Math.Min(9, (int)Math.Round(bands[b] * 9 * k)); if (n <= 0) continue;
                double a = (i + 0.5) * Math.PI / nb - Math.PI / 2; float cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                for (int j = 0; j < n; j++)
                {
                    float d = r0 + j * sp; int si = j == n - 1 && b == top && bands[top] > 0.5 ? 9 : j; // the loudest tip is red
                    g.DrawImageUnscaled(dotSpr[si], (int)Math.Round(x + cs * d) - o, (int)Math.Round(y + sn * d) - o);
                }
            }
        }

        Bitmap[] dotSpr; int dotK; // the ray dots, pre-drawn: one per ring brightness, [9] red; rebuilt only as the effect fades in/out
        void MakeDots(int kq, float dr)
        {
            if (dotSpr != null) foreach (var b in dotSpr) b.Dispose();
            dotK = kq * 16 + Theme.Index; double k = kq / 8.0; int sz = (int)Math.Ceiling(dr * 2) + 2;
            dotSpr = new Bitmap[10];
            for (int j = 0; j < 10; j++)
            {
                dotSpr[j] = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(dotSpr[j]))
                using (var br = new SolidBrush(j == 9 ? U.A(Red, k) : U.A(Theme.T.Ray, 0.85 * (1 - j / 10.0) * k)))
                { g.SmoothingMode = SmoothingMode.AntiAlias; g.FillEllipse(br, sz / 2f - dr, sz / 2f - dr, dr * 2, dr * 2); }
            }
        }

        // D: the edge turns liquid; the kick sends a wobble round it like a speaker cone made of ink
        PointF[] Wobble(float x, float y, float R, double now, float k)
        {
            double kk = Kick(now) * k, lv = level * k; var pts = new PointF[72];
            for (int i = 0; i < pts.Length; i++) // it only ever bulges outward, so the round face drawn on top never pokes through
            {
                double a = i * Math.PI * 2 / pts.Length;
                double rr = R * (1 + (0.045 * kk + 0.022 * lv) * (0.5 + 0.5 * Math.Sin(3 * a + now * 2.3)) + (0.028 * kk + 0.014 * lv) * (0.5 + 0.5 * Math.Sin(5 * a - now * 3.1)) + 0.016 * lv * (0.5 + 0.5 * Math.Sin(8 * a + now * 5)));
                pts[i] = new PointF(x + (float)(Math.Cos(a) * rr), y + (float)(Math.Sin(a) * rr));
            }
            return pts;
        }

        void Liquid(Graphics g, float x, float y, float R, double now, float k, float wall)
        {
            var pts = Wobble(x, y, R, now, k);
            var low = new PointF[pts.Length]; for (int i = 0; i < pts.Length; i++) low[i] = new PointF(pts[i].X, pts[i].Y + wall);
            using (var br = new SolidBrush(Theme.T.Wall)) g.FillPolygon(br, low);
            using (var lg = new LinearGradientBrush(new PointF(0, y - R - 1), new PointF(0, y + R + 1), Theme.T.Top, Theme.T.Bot) { WrapMode = WrapMode.TileFlipXY })
                g.FillPolygon(lg, pts);
            var fx = Face(R, true); int half = fx.Width / 2;
            g.DrawImageUnscaled(fx, (int)x - half, (int)y - half); // the face without its rim lines: the rim is the liquid edge now
            using (var pen = new Pen(U.A(Theme.T.Rim, 0.11 + 0.4 * Kick(now) * k), 1 * S))
            {
                g.DrawPolygon(pen, pts);
                var hl = new List<PointF>(); // the one highlight, following the liquid edge
                for (int i = 0; i < pts.Length; i++)
                {
                    double deg = i * 360.0 / pts.Length; if (deg < 208 || deg > 332) continue;
                    hl.Add(new PointF(pts[i].X + (x - pts[i].X) * 1.4f * S / R, pts[i].Y + (y - pts[i].Y) * 1.4f * S / R));
                }
                pen.Color = Color.FromArgb(95, Theme.T.Shine); pen.Width = 1.1f * S; if (hl.Count > 1) g.DrawLines(pen, hl.ToArray());
            }
        }
        // the focus timer: a red arc from twelve o'clock, one full turn = 60 minutes
        void TimerArc(Graphics g, float x, float y, float R, double now, float k)
        {
            if (k <= 0.01) return;
            float tr = R - 4.6f * S, sw = (float)(Math.Min(60, dial.X) / 60 * 360);
            double a = timerState == 3 ? 0.55 + 0.45 * Math.Cos((now - doneT) * Math.PI * 2 / 1.1) : 1;
            using (var pen = new Pen(U.A(Red, a * k), 2.6f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                if (sw > 0.3f) g.DrawArc(pen, x - tr, y - tr, tr * 2, tr * 2, -90, sw);
            double th = (-90 + sw) * Math.PI / 180; float s = 2.6f * S;
            using (var br = new SolidBrush(U.A(Theme.Mix(Red, Color.White, 0.4), a * k))) g.FillEllipse(br, x + (float)Math.Cos(th) * tr - s, y + (float)Math.Sin(th) * tr - s, s * 2, s * 2);
        }

        void Pointer(Graphics g, float x, float y, float R, float k)
        {
            double th = (-90 + pointer.X * 360.0 / Nodes) * Math.PI / 180;
            float tr = R - 4.6f * S, deg = (float)(th * 180 / Math.PI), c = (float)Math.Cos(th), s = (float)Math.Sin(th);
            using (var pen = new Pen(U.A(Red, k), 2.6f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, x - tr, y - tr, tr * 2, tr * 2, deg - 7, 14);
            using (var br = new SolidBrush(U.A(Red, k)))
            {
                float n0 = R + 7 * S, n1 = R + 15 * S, w = 4.2f * S; // a small notch on the edge, aimed at the chosen button
                g.FillPolygon(br, new[] { new PointF(x + c * n1, y + s * n1), new PointF(x + c * n0 - s * w, y + s * n0 + c * w), new PointF(x + c * n0 + s * w, y + s * n0 - c * w) });
            }
        }

        void Matrix(Graphics g, float x, float y, float R, double now, double age, float m)
        {
            var dots = new Dots();
            var t = DateTime.Now;
            double lab = 0; int li = 0; for (int i = 0; i < Nodes; i++) if (nodeHov[i] > lab) { lab = nodeHov[i]; li = i; }
            lab *= U.Clamp01(menuS.X);
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            float hq = (float)U.Clamp01(holdK * 4 + (holdTick ? 0.25 : 0));
            if (hq > 0.01) { dots.Text3("TURN", -5, hq, true); dots.Text3("OFF", 1, hq, true); }
            float tA = (float)((1 - m) * (1 - lab) * (1 - hq));
            double ge = now - greetT0, gd = GreetDur();
            if (greetText != null && ge >= 0 && ge < gd) // the greeting scrolls through the dots like a marquee; the circle crops it
            {
                float gk = (float)(U.Clamp01(ge / 0.3) * U.Clamp01((gd - ge) / 0.3) * (1 - m));
                dots.Text3At(greetText, (int)Math.Round(17 - ge * 20), -2, gk); tA *= 1 - gk;
            }

            if (tA > 0.01 && timerState != 0)
            {
                bool h24t = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H");
                string clock = t.ToString(h24t ? "HH:mm" : "h:mm", inv);
                dots.Text3("FOCUS", -9, 0.5f * tA, timerState == 3);
                if (timerState == 1)
                {
                    dots.Text5(Settings.Focus.ToString(), "", 1, -3, tA);
                    dots.Text3((int)((now - setT) / 1.6) % 2 == 0 ? "MIN" : "CLICK", 5, 0.5f * tA);
                }
                else if (timerState == 2)
                {
                    int rem = (int)Math.Ceiling(Math.Max(0, Remaining));
                    dots.Text5((rem / 60).ToString("00") + ":" + (rem % 60).ToString("00"), "", 1, -3, tA);
                    dots.Text3(clock, 5, 0.5f * tA);
                }
                else
                {
                    dots.Text3("DONE", -2, tA * (float)(0.6 + 0.4 * Math.Cos((now - doneT) * Math.PI * 2 / 1.1)), true);
                    dots.Text3(clock, 5, 0.5f * tA);
                }
            }
            else if (tA > 0.01)
            {
                bool h24 = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H");
                string time = t.ToString(h24 ? "HH" : "%h") + (t.Millisecond < 500 ? ":" : ";") + t.ToString("mm");
                string bare = time.Replace(';', ':');
                if (bare != lastTime) { if (lastTime != "") { prevTime = lastTime; changeT = now; } lastTime = bare; }
                dots.Text5(time, prevTime, U.Clamp01((now - changeT) / 0.45), -3, tA);
                dots.Text3(t.ToString("ddd", inv) + (h24 ? "" : " " + t.ToString("tt", inv)), -9, 0.75f * tA);
                dots.Text3(t.ToString("dd MMM", inv), 5, 0.75f * tA);
            }
            if (lab > 0.01)
            {
                string name = Names[li];
                if (li == 1 && mode == 1) name = "CLOCK";
                if (li == 3 && Settings.Float) name = "REST ON DESK";
                if (li == 4 && timerState >= 2) name = "STOP TIMER";
                var words = name.Split(new[] { ' ' }, 2);
                if (words.Length == 1) dots.Text3(words[0], -2, (float)lab);
                else { dots.Text3(words[0], -5, (float)lab); dots.Text3(words[1], 1, (float)lab); }
            }
            if (m > 0.01)
            {
                double pop = U.Bell(now - hoops.ScoreT, 0, 0.6);
                dots.Text5(hoops.Points.ToString(), "", 1, -3, (float)((0.18 + 0.7 * pop) * m));
                dots.Text3("BEST " + Settings.Best, 15, 0.3f * m);
                if (now - hoops.SwishT < 1.2) dots.Text3("SWISH", -18, (float)(1 - (now - hoops.SwishT) / 1.2) * m, true);
                else if (hoops.Points == 0 && now - hoops.DropT < 7) dots.Text3("PULL", -18, 0.4f * m);
            }

            double nr = Nr(R), reveal = (age - 0.35) / 0.55;
            float P = Pitch, d = P * 0.66f;
            double rr = (now - rippleT) * 34, ra = 1 - (now - rippleT) / 0.7;
            int n = (int)nr;
            float mc = (mouse.X - x) / P, mr = (mouse.Y - y) / P;
            Color ink = Theme.T.Ink; bool bloom = Theme.T.Bloom;
            using (var br = new SolidBrush(Color.White))
                for (int r = -n; r <= n; r++)
                    for (int c = -n; c <= n; c++)
                    {
                        double dist = Math.Sqrt(c * c + r * r); if (dist > nr) continue;
                        float b; int k = Dots.K(c, r);
                        dots.On.TryGetValue(k, out b);
                        double extra = 0;
                        if (hover && !down) { double dm = Math.Sqrt((c - mc) * (c - mc) + (r - mr) * (r - mr)); if (dm < 5) extra = 0.2 * (1 - dm / 5) * (1 - dm / 5); }
                        if (ra > 0 && Math.Abs(dist - rr) < 1.2) extra += 0.45 * ra;
                        double v = Math.Max(b, extra) * U.Clamp01(reveal * 1.6 - dist / nr);
                        if (v < 0.03) continue;
                        bool acc = dots.Red.Contains(k);
                        if (v > 0.55) { br.Color = U.A(acc ? Red : ink, v * (bloom ? 0.13 : 0.1)); g.FillEllipse(br, x + c * P - d * 1.15f, y + r * P - d * 1.15f, d * 2.3f, d * 2.3f); } // a soft glow round lit dots: readable from across the room
                        br.Color = acc ? U.A(Red, v) : U.A(ink, v * 0.96);
                        float dd = v > 0.55 ? d * 1.22f : d;                                  // lit dots are fuller than the unlit grid
                        g.FillEllipse(br, x + c * P - dd / 2, y + r * P - dd / 2, dd, dd);
                    }
        }


        static readonly string[] Names = { "THOUGHT", "VECTOR HOOPS", "DESKTOP ASSAULT", "FLOAT ABOVE", "FOCUS TIMER", "SCRATCH PAD" };

        // menu buttons: small pucks of the same glass
        void DrawMenu(Graphics g, float R)
        {
            float k = (float)U.Clamp01(menuS.X);
            using (var pen = new Pen(Color.White, 1.3f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var br = new SolidBrush(Color.White))
            {
                for (int i = 0; i < Nodes; i++)
                {
                    var p = NodePos(i, R); float h = (float)nodeHov[i];
                    float r = 16 * S * (1 + 0.1f * h) * (float)Math.Min(1, menuS.X * 1.2);
                    if (r < 1) continue;
                    br.Color = Color.FromArgb((int)(255 * k), Theme.T.Wall); g.FillEllipse(br, p.X - r, p.Y - r + 2.5f * S, r * 2, r * 2);
                    using (var lg = new LinearGradientBrush(new PointF(0, p.Y - r - 1), new PointF(0, p.Y + r + 1), Color.FromArgb((int)(255 * k), Theme.Mix(Theme.T.Top, Theme.T.Shine, 0.02)), Color.FromArgb((int)(255 * k), Theme.T.Bot)))
                        g.FillEllipse(lg, p.X - r, p.Y - r, r * 2, r * 2);
                    pen.Width = 1 * S; pen.Color = Color.FromArgb((int)(k * (28 + 60 * h)), Theme.T.Rim); g.DrawEllipse(pen, p.X - r, p.Y - r, r * 2, r * 2);
                    pen.Color = Color.FromArgb((int)(k * 80), Theme.T.Shine);
                    if (r > 3 * S) g.DrawArc(pen, p.X - r + 1.2f * S, p.Y - r + 1.2f * S, r * 2 - 2.4f * S, r * 2 - 2.4f * S, 208, 124); // buttons pass through zero size while they open
                    pen.Width = 1.3f * S; pen.Color = h > 0.5 ? U.A(Red, k) : U.A(Theme.T.Ink, k * 0.88);
                    Glyph(g, pen, br, i, p.X, p.Y, r * 0.4f, k);
                }
            }
        }

        void Glyph(Graphics g, Pen pen, SolidBrush br, int i, float x, float y, float u, float k)
        {
            switch (i)
            {
                case 0: g.DrawPolygon(pen, new[] { new PointF(x, y - u * 1.2f), new PointF(x + u * 0.8f, y), new PointF(x, y + u * 1.2f), new PointF(x - u * 0.8f, y) }); break;
                case 1:
                    if (mode == 1) { g.DrawEllipse(pen, x - u, y - u, u * 2, u * 2); g.DrawLine(pen, x, y, x, y - u * 0.65f); g.DrawLine(pen, x, y, x + u * 0.5f, y); }
                    else { g.DrawEllipse(pen, x - u, y - u * 0.35f, u * 2, u * 0.7f); br.Color = pen.Color; g.FillEllipse(br, x - u * 0.3f, y - u * 1.2f, u * 0.6f, u * 0.6f); }
                    break;
                case 2: g.DrawEllipse(pen, x - u * 0.7f, y - u * 0.7f, u * 1.4f, u * 1.4f); g.DrawLine(pen, x - u * 1.3f, y, x - u * 0.35f, y); g.DrawLine(pen, x + u * 0.35f, y, x + u * 1.3f, y); g.DrawLine(pen, x, y - u * 1.3f, x, y - u * 0.35f); g.DrawLine(pen, x, y + u * 0.35f, x, y + u * 1.3f); break;
                case 3:
                    float s = Settings.Float ? -1 : 1;
                    g.DrawLines(pen, new[] { new PointF(x - u * 0.8f, y + s * u * 0.25f), new PointF(x, y - s * u * 0.55f), new PointF(x + u * 0.8f, y + s * u * 0.25f) });
                    g.DrawLine(pen, x - u * 0.8f, y + s * u * 0.85f, x + u * 0.8f, y + s * u * 0.85f); break;
                case 5: // a notepad
                    g.DrawRectangle(pen, x - u * 0.8f, y - u * 1.05f, u * 1.6f, u * 2.1f);
                    for (int l = 0; l < 3; l++) g.DrawLine(pen, x - u * 0.45f, y - u * 0.45f + l * u * 0.45f, x + (l == 2 ? 0.1f : 0.45f) * u, y - u * 0.45f + l * u * 0.45f);
                    break;
                case 4: // a stopwatch; its hand is red while the timer runs
                    float sy = y + u * 0.2f;
                    g.DrawEllipse(pen, x - u, sy - u, u * 2, u * 2); g.DrawLine(pen, x, sy - u, x, y - u * 1.25f); g.DrawLine(pen, x - u * 0.35f, y - u * 1.25f, x + u * 0.35f, y - u * 1.25f);
                    if (timerState >= 2) pen.Color = U.A(Red, k);
                    g.DrawLine(pen, x, sy, x + u * 0.45f, sy - u * 0.5f); break;
            }
        }
    }

    // Dot-matrix lettering, drawn dot by dot: 5x7 numerals, 3x5 caps. No installed font is involved.
    class Dots
    {
        public readonly Dictionary<int, float> On = new Dictionary<int, float>();
        public readonly HashSet<int> Red = new HashSet<int>();
        public static int K(int c, int r) { return (c + 512) * 1024 + (r + 512); }

        static readonly Dictionary<char, string> F5 = new Dictionary<char, string> {
            {'0',"01110100011000110001100011000101110"}, {'1',"00100011000010000100001000010001110"},
            {'2',"01110100010000100010001000100011111"}, {'3',"11111000100010000010000011000101110"},
            {'4',"00010001100101010010111110001000010"}, {'5',"11111100001111000001000011000101110"},
            {'6',"00110010001000011110100011000101110"}, {'7',"11111000010001000100010000100001000"},
            {'8',"01110100011000101110100011000101110"}, {'9',"01110100011000101111000010001001100"} };
        static readonly Dictionary<char, string> F3 = new Dictionary<char, string> {
            {'A',"010101111101101"},{'B',"110101110101110"},{'C',"011100100100011"},{'D',"110101101101110"},{'E',"111100110100111"},
            {'F',"111100110100100"},{'G',"011100101101011"},{'H',"101101111101101"},{'I',"111010010010111"},{'J',"001001001101010"},
            {'K',"10011010110010101001"},{'L',"100100100100111"},{'M',"1000111011101011000110001"},{'N',"1000111001101011001110001"},{'O',"010101101101010"},
            {'P',"110101110100100"},{'Q',"010101101110011"},{'R',"110101110101101"},{'S',"011100010001110"},{'T',"111010010010010"},
            {'U',"101101101101111"},{'V',"1000110001100010101000100"},{'W',"1000110001101011101110001"},{'X',"1000101010001000101010001"},{'Y',"101101010010010"},
            {'Z',"111001010100111"},{'0',"111101101101111"},{'1',"010110010010111"},{'2',"111001111100111"},{'3',"111001111001111"},
            {'4',"101101111001001"},{'5',"111100111001111"},{'6',"111100111101111"},{'7',"111001001001001"},{'8',"111101111101111"},
            {'9',"111101111001111"},{'.',"00001"},{' ',"00000"},{'-',"000000111000000"},{'/',"001001010100100"},{':',"000010000010000"} };

        // draw the lit dots of a matrix whose (0,0) sits at (x0, y0)
        public static void Paint(Graphics g, Dots d, float x0, float y0, float p, float size, Color white, Color red)
        {
            using (var br = new SolidBrush(white))
                foreach (var kv in d.On)
                {
                    int c = kv.Key / 1024 - 512, r = kv.Key % 1024 - 512;
                    br.Color = U.A(d.Red.Contains(kv.Key) ? red : white, kv.Value);
                    g.FillEllipse(br, x0 + c * p - size / 2, y0 + r * p - size / 2, size, size);
                }
        }

        public void Set(int c, int r, float b, bool red)
        {
            int k = K(c, r); float o;
            if (!On.TryGetValue(k, out o) || o < b) On[k] = b;
            if (red) Red.Add(k);
        }

        // small caps, 5 dots tall, variable width (M N V W X are 5 wide, K is 4), centred; `row` is the top row
        public void Text3(string s, int row, float b, bool red = false) { Text3At(s, -W3(s) / 2, row, b, red); }

        public static int W3(string s)
        {
            int w = -1; foreach (char ch in s.ToUpperInvariant()) { string gl; if (F3.TryGetValue(ch, out gl)) w += gl.Length / 5 + 1; }
            return w;
        }

        // left-aligned at column c
        public void Text3At(string s, int c, int row, float b, bool red = false)
        {
            if (b <= 0.01) return;
            s = s.ToUpperInvariant();
            foreach (char ch in s)
            {
                string gl; if (!F3.TryGetValue(ch, out gl)) continue;
                int gw = gl.Length / 5;
                for (int r = 0; r < 5; r++) for (int cc = 0; cc < gw; cc++) if (gl[r * gw + cc] == '1') Set(c + cc, row + r, b, red);
                c += gw + 1;
            }
        }

        // numerals with ':' (lit) or ';' (colon off). Changed digits wipe in column by column from `prev`.
        public void Text5(string s, string prev, double wipe, int row, float b)
        {
            if (b <= 0.01) return;
            int w = 0; foreach (char ch in s) w += (ch == ':' || ch == ';' ? 1 : 5) + 1;
            int c = -(w - 1) / 2;
            prev = prev.Replace(';', ':');
            bool same = prev.Length == s.Length;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == ':' || ch == ';') { if (ch == ':') { Set(c, row + 2, b, false); Set(c, row + 4, b, false); } c += 2; continue; }
                string gl; if (!F5.TryGetValue(ch, out gl)) { c += 6; continue; }
                string old = null; if (same && wipe < 1 && prev[i] != ch) F5.TryGetValue(prev[i], out old);
                for (int cc = 0; cc < 5; cc++)
                {
                    string use = old != null && cc >= wipe * 6 ? old : gl;
                    for (int r = 0; r < 7; r++) if (use[r * 5 + cc] == '1') Set(c + cc, row + r, b, false);
                }
                c += 6;
            }
        }
    }

    // Mode 3: a light orb, a hoop of light, gravity, and the glass walls of the crystal itself.
    class Hoops
    {
        readonly Crystal c; readonly float S = U.S;
        float ox, oy, vx, vy, prevY;
        Spring hx, hy; float hw;
        int score, combo; bool aiming, touched; float ax, ay, bx, by;
        float kx; double blockT = -10;                    // the pet keeps goal above the hoop
        bool Keeping { get { return score >= 2; } }       // it starts guarding from the third basket
        float KeepH { get { return 36 * S; } }            // how far above the rim it floats: steep lobs can drop in under it
        double scoreT = -10, lastBounce, swishT = -10, netT = -10, dropT;
        readonly List<PointF> trail = new List<PointF>();
        readonly Random rnd = new Random();
        public bool Busy { get { double now = U.Now; return aiming || vx * vx + vy * vy > 400 * S * S || !hx.Rest || !hy.Rest || now - scoreT < 1.5 || now - dropT < 7.5 || now - netT < 1.5; } }

        public Hoops(Crystal c) { this.c = c; hx = new Spring(0, 90, 13) { Eps = 0.05 }; hy = new Spring(0, 90, 13) { Eps = 0.05 }; Reset(); }

        float Rf { get { return (float)c.rS.X * 0.9f; } }
        float OR { get { return 8.5f * S; } }

        public void Reset()
        {
            score = 0; combo = 0; ox = 0; oy = -60 * S; vx = 0; vy = 0; trail.Clear(); dropT = U.Now;
            hw = 27 * S; hx.Snap(30 * S); hy.Snap(-40 * S); kx = 30 * S;
        }

        public void Press(float x, float y) { aiming = true; ax = bx = x; ay = by = y; }
        public void Drag(float x, float y) { if (aiming) { bx = x; by = y; } }
        public void Release()
        {
            if (!aiming) return; aiming = false;
            float dx = ax - bx, dy = ay - by;
            if (dx * dx + dy * dy < 16 * S * S) return;
            vx = (float)U.Clamp(dx * 7.5f, -1500 * S, 1500 * S); vy = (float)U.Clamp(dy * 7.5f, -1500 * S, 1500 * S);
            touched = false; Snd.Play("swish");
        }

        public void Step(double dt, double now)
        {
            hx.Step(dt); hy.Step(dt);
            if (c.mode == 1 && !c.pet.Roaming) // the keeper: resting beside the hoop for the first two baskets, then a patrol you can time
            {
                float gy = (float)hy.X - KeepH;
                float target = Keeping ? (float)(hx.X + Math.Sin(now * (0.9 + 0.07 * Math.Min(score, 10))) * hw * 2.1) : (float)(hx.X + hw * 2.3);
                float sp = (Keeping ? 140 : 90) * S;
                float mv = Math.Sign(target - kx) * Math.Min(Math.Abs(target - kx), sp * (float)dt); kx += mv;
                c.pet.Walking = Math.Abs(mv) > 0.2f * S; if (mv != 0) c.pet.Face = Math.Sign(mv);
                c.pet.LookAt(ox - kx, oy - (gy - 14 * S));
            }
            if (aiming || c.mode != 1) return;
            float g = 900 * S, rf = Rf - OR, rim = 2.6f * S;
            int n = 4; float h = (float)dt / n;
            for (int k = 0; k < n; k++)
            {
                prevY = oy;
                vy += g * h; ox += vx * h; oy += vy * h;
                float d = (float)Math.Sqrt(ox * ox + oy * oy);
                if (d > rf)
                {
                    float nx = ox / d, ny = oy / d; ox = nx * rf; oy = ny * rf;
                    float vn = vx * nx + vy * ny;
                    if (vn > 0) { vx -= 1.72f * vn * nx; vy -= 1.72f * vn * ny; vx *= 0.99f; Bounce(vn, now); }
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    float rx = (float)hx.X + s * hw, ry = (float)hy.X, dx = ox - rx, dy = oy - ry, dd = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dd < OR + rim && dd > 0.01f)
                    {
                        float nx = dx / dd, ny = dy / dd; ox = rx + nx * (OR + rim); oy = ry + ny * (OR + rim);
                        float vn = vx * nx + vy * ny; if (vn < 0) { vx -= 1.6f * vn * nx; vy -= 1.6f * vn * ny; }
                        if (!touched || now - lastBounce > 0.12) { Snd.Play("rim"); lastBounce = now; }
                        touched = true;
                    }
                }
                if (!c.pet.Roaming && Keeping) // the keeper is solid: a round body the orb bounces off
                {
                    float ky = (float)hy.X - KeepH - 14 * S, kdx = ox - kx, kdy = oy - ky, kd = (float)Math.Sqrt(kdx * kdx + kdy * kdy), kr = OR + 11 * S;
                    if (kd < kr && kd > 0.01f)
                    {
                        float nx = kdx / kd, ny = kdy / kd; ox = kx + nx * kr; oy = ky + ny * kr;
                        float vn = vx * nx + vy * ny; if (vn < 0) { vx -= 1.55f * vn * nx; vy -= 1.55f * vn * ny; }
                        if (now - blockT > 0.3) { blockT = now; touched = true; Snd.Play("boing"); c.pet.Cheer(now, false); }
                    }
                }
                if (prevY < hy.X && oy >= hy.X && vy > 0 && Math.Abs(ox - hx.X) < hw - rim - OR * 0.3f) Score(now);
            }
            trail.Insert(0, new PointF(ox, oy)); if (trail.Count > 22) trail.RemoveAt(trail.Count - 1);
        }

        void Bounce(float vn, double now)
        {
            if (vn < 90 * S || now - lastBounce < 0.07) return;
            lastBounce = now; Snd.Play(vn > 380 * S ? "bounce" : "bounce2");
        }

        void Score(double now)
        {
            score++; combo = touched ? 0 : combo + 1; scoreT = now; netT = now; if (Keeping) c.pet.Sad(now); else c.pet.Cheer(now, false);
            if (!touched) swishT = now;
            Snd.Play("score" + Math.Min(5, combo));
            if (score > Settings.Best) { Settings.Best = score; Settings.Save(); }
            double a = rnd.NextDouble() * Math.PI * 2; float rr = Rf * 0.42f;
            hx.T = Math.Cos(a) * rr * 0.9; hy.T = -Rf * 0.15 + Math.Sin(a) * rr * 0.55;
            hw = (float)U.Clamp(27 * S - score * 0.6 * S, 17 * S, 27 * S);
        }

        public int Points { get { return score; } }
        public double ScoreT { get { return scoreT; } }
        public double SwishT { get { return swishT; } }
        public double DropT { get { return dropT; } }

        public void Draw(Graphics g, float x, float y, float R, double now, float m)
        {
            float k = (float)U.Clamp01((m - 0.35) / 0.65);
            if (k <= 0) return;
            float hX = x + (float)hx.X, hY = y + (float)hy.X, eh = hw * 0.36f;
            double sway = Math.Sin((now - netT) * 18) * Math.Exp(-(now - netT) * 4) * 6 * S;
            using (var pen = new Pen(U.A(Theme.T.Ink, 0.43 * k), 1.1f * S))
            {
                g.DrawArc(pen, hX - hw, hY - eh / 2, hw * 2, eh, 180, 180); // back of the rim
                pen.Width = 0.7f * S; pen.Color = U.A(Theme.T.Ink, 0.24 * k);
                for (int i = 0; i <= 6; i++)
                {
                    double a = Math.PI * i / 6;
                    g.DrawLine(pen, hX - (float)Math.Cos(a) * hw, hY + (float)Math.Sin(a) * eh / 2, hX - (float)Math.Cos(a) * hw * 0.55f + (float)sway, hY + 24 * S);
                }
                g.DrawEllipse(pen, hX - hw * 0.55f + (float)sway, hY + 24 * S - eh * 0.27f, hw * 1.1f, eh * 0.55f);
            }
            using (var br = new SolidBrush(Color.White))
            {
                if (aiming) // where it will go
                {
                    float vx0 = (ax - bx) * 7.5f, vy0 = (ay - by) * 7.5f;
                    for (int i = 1; i < 24; i++)
                    {
                        float t = i * 0.03f, qx = ox + vx0 * t, qy = oy + vy0 * t + 450 * S * t * t;
                        if (qx * qx + qy * qy > Rf * Rf) break;
                        br.Color = U.A(Theme.T.Ink, k * 0.78 * (1 - i / 24f));
                        float d = 1.6f * S; g.FillEllipse(br, x + qx - d, y + qy - d, d * 2, d * 2);
                    }
                }
                for (int i = 2; i < trail.Count; i += 2)
                {
                    float a = 1 - i / (float)trail.Count, d = OR * 0.45f * a;
                    br.Color = U.A(Theme.T.Ink, k * 0.47 * a); g.FillEllipse(br, x + trail[i].X - d, y + trail[i].Y - d, d * 2, d * 2);
                }
                float drop = (float)U.Spr((now - dropT) / 0.6, 6, 10), o = OR * drop;
                br.Color = U.A(Theme.T.Ink, k); g.FillEllipse(br, x + ox - o, y + oy - o, o * 2, o * 2);
            }
            using (var pen = new Pen(U.A(Theme.T.Ink, 0.94 * k), 1.7f * S))
                g.DrawArc(pen, hX - hw, hY - eh / 2, hw * 2, eh, 0, 180); // front of the rim
            if (!c.pet.Roaming) c.pet.DrawAt(g, x + kx, hY - KeepH, now, k);
        }    }
}
