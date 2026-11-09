using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Obsidot
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { try { N.SetProcessDPIAware(); } catch { } }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // an ambient object should never vanish silently: log anything unexpected, keep running if we can
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Settings.Log(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Settings.Log(e.ExceptionObject);
            Settings.Load();
            if (args.Length > 0 && args[0] == "--shots") { Settings.Sound = false; Settings.NoSave = true; Snd.Init(); Shots.Run(args.Length > 1 ? args[1] : "."); return; }
            if (args.Length > 0 && args[0] == "--assault") { Settings.Sound = false; Settings.NoSave = true; Snd.Init(); Shots.AssaultDemo(args.Length > 1 ? args[1] : "."); return; }
            if (args.Length > 0 && args[0] == "--anim") { Settings.Sound = false; Settings.NoSave = true; Snd.Init(); Shots.Anim(args.Length > 1 ? args[1] : "."); return; }
            if (args.Length > 1 && args[0] == "--bench") { Settings.NoSave = true; Snd.Init(); Crystal.BenchLog = args[1]; Application.Run(new Crystal()); return; }
            bool fresh;
            using (var mx = new Mutex(true, "OBSIDOT.living.puck", out fresh))
            {
                if (!fresh) { try { EventWaitHandle.OpenExisting("OBSIDOT.summon").Set(); } catch { } return; } // already running: call it forward
                Snd.Init();
                var summon = new EventWaitHandle(false, EventResetMode.AutoReset, "OBSIDOT.summon");
                var crystal = new Crystal();
                new Thread(() => { while (summon.WaitOne()) try { crystal.BeginInvoke(new Action(crystal.Summon)); } catch { } }) { IsBackground = true }.Start();
                Application.Run(crystal);
                Settings.Log("exit: main loop ended normally");
                Settings.Save();
            }
        }
    }

    // ---------- small math / drawing kit ----------
    static class U
    {
        static readonly Stopwatch sw = Stopwatch.StartNew();
        public static double Now { get { return sw.Elapsed.TotalSeconds; } }
        public static readonly float S;
        public static readonly bool Calm; // Windows "show animations" off -> no shake / breathing

        static U()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
            int anim = 1;
            try { N.SystemParametersInfo(0x1042, 0, ref anim, 0); } catch { }
            Calm = anim == 0;
        }

        // bench-only section profiler
        public static bool Prof; public static readonly double[] P = new double[16]; static readonly Stopwatch pw = new Stopwatch();
        public static void Mk(int i) { if (!Prof) return; P[i] += pw.Elapsed.TotalMilliseconds; pw.Restart(); }
        public static string Dump(int n) { var sb = new StringBuilder(); for (int i = 0; i < P.Length; i++) { if (P[i] > 0) sb.Append(i + ":" + (P[i] / n).ToString("0.00") + "  "); P[i] = 0; } return sb.ToString(); }

        public static double Clamp01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }
        public static double Clamp(double x, double a, double b) { return x < a ? a : x > b ? b : x; }
        public static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
        public static double OutCubic(double x) { x = Clamp01(x); return 1 - Math.Pow(1 - x, 3); }
        public static double InCubic(double x) { x = Clamp01(x); return x * x * x; }
        public static double InOut(double x) { x = Clamp01(x); return x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2; }
        // damped spring response 0 -> 1 with overshoot (x in "normalized seconds")
        public static double Spr(double x, double damp, double freq) { return x <= 0 ? 0 : 1 - Math.Exp(-damp * x) * Math.Cos(freq * x); }
        public static double Bell(double x, double a, double b) { if (x <= a || x >= b) return 0; double t = (x - a) / (b - a); return Math.Sin(Math.PI * t); }

        public static Color A(Color c, double a) { return Color.FromArgb((int)Clamp(a * 255, 0, 255), c.R, c.G, c.B); }
        public static Color A(int a, int r, int g, int b) { return Color.FromArgb((int)Clamp(a, 0, 255), r, g, b); }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public static Font F(string fam, float px, FontStyle st = FontStyle.Regular)
        {
            string k = fam + "|" + Math.Round(px, 1) + "|" + st;
            Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font(fam, Math.Max(1f, px), st, GraphicsUnit.Pixel); fonts[k] = f; }
            return f;
        }

        static readonly SolidBrush tb = new SolidBrush(Color.White);
        static readonly StringFormat typo = StringFormat.GenericTypographic;
        // letter-spaced text; align 0 left / 1 center / 2 right
        public static float Spaced(Graphics g, string s, Font f, Color c, float x, float y, float sp, int align)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var w = new float[s.Length]; float tot = 0;
            for (int i = 0; i < s.Length; i++)
            {
                w[i] = s[i] == ' ' ? f.Size * 0.32f : CharW(g, f, s[i]);
                tot += w[i] + (i < s.Length - 1 ? sp : 0);
            }
            float cx = align == 1 ? x - tot / 2 : align == 2 ? x - tot : x;
            tb.Color = c;
            for (int i = 0; i < s.Length; i++) { if (s[i] != ' ') g.DrawString(s[i].ToString(), f, tb, cx, y, typo); cx += w[i] + sp; }
            return tot;
        }

        static readonly Dictionary<string, float> widths = new Dictionary<string, float>();
        static float CharW(Graphics g, Font f, char ch)
        {
            string k = f.Name + f.Size + f.Style + ch; float w;
            if (!widths.TryGetValue(k, out w)) { w = g.MeasureString(ch.ToString(), f, 1000, typo).Width; widths[k] = w; }
            return w;
        }

        static readonly Dictionary<long, Bitmap> glows = new Dictionary<long, Bitmap>();
        // Soft radial light. Sprites are cached per colour/alpha step; big ones use a 256px sprite with nearest-neighbour
        // sampling (invisible on a smooth gradient, ~6x faster than GDI+ bilinear upscaling).
        static Bitmap Sprite(Color c, double a, bool big)
        {
            int steps = big ? 16 : 24, lvl = (int)Math.Round(Clamp01(a) * steps);
            if (lvl == 0) return null;
            long k = ((long)(c.ToArgb() & 0xFFFFFF) << 16) + (lvl << 1) + (big ? 1 : 0);
            Bitmap b;
            if (!glows.TryGetValue(k, out b))
            {
                int n = big ? 256 : 64;
                b = new Bitmap(n, n, PixelFormat.Format32bppPArgb);
                using (var gg = Graphics.FromImage(b))
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(0, 0, n, n);
                    using (var pg = new PathGradientBrush(p))
                    {
                        pg.CenterColor = Color.FromArgb(lvl * 255 / steps, c);
                        pg.SurroundColors = new[] { Color.FromArgb(0, c) };
                        pg.Blend = new Blend { Positions = new[] { 0f, 0.45f, 0.8f, 1f }, Factors = new[] { 0f, 0.18f, 0.55f, 1f } };
                        gg.FillPath(pg, p);
                    }
                }
                glows[k] = b;
            }
            return b;
        }

        public static void GlowR(Graphics g, Color c, RectangleF r, double a)
        {
            if (a <= 0.01 || r.Width < 1 || r.Height < 1) return;
            bool big = Math.Max(r.Width, r.Height) > 96;
            var b = Sprite(c, a, big); if (b == null) return;
            var im = g.InterpolationMode;
            g.InterpolationMode = big ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
            g.DrawImage(b, r);
            g.InterpolationMode = im;
        }

        public static void Glow(Graphics g, Color c, float x, float y, float r, double a) { GlowR(g, c, new RectangleF(x - r, y - r, r * 2, r * 2), a); }

        // a Bitmap whose pixels live in a GDI DIB section: GDI+ can draw on it and GDI can BitBlt it, no copies
        public static Bitmap Dib(int w, int h, out IntPtr dc, out IntPtr hbmp)
        {
            var bi = new N.BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            IntPtr bits; dc = N.CreateCompatibleDC(IntPtr.Zero);
            hbmp = N.CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0); N.SelectObject(dc, hbmp);
            return new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
        }

        public static GraphicsPath Round(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath(); r = Math.Max(0.5f, Math.Min(r, Math.Min(w, h) / 2)); float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90); p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        public static void ClearGlows() { foreach (var b in glows.Values) b.Dispose(); glows.Clear(); }
        public static void DrawAlpha(Graphics g, Image img, RectangleF dst, double a)
        {
            if (a <= 0.004) return;
            if (a >= 0.996) { g.DrawImage(img, dst); return; }
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = (float)a });
                g.DrawImage(img, Rectangle.Round(dst), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }

        public static GraphicsPath Chamfer(float x, float y, float w, float h, float c)
        {
            c = Math.Min(c, Math.Min(w, h) / 2);
            var p = new GraphicsPath();
            p.AddPolygon(new[] {
                new PointF(x + c, y), new PointF(x + w - c, y), new PointF(x + w, y + c), new PointF(x + w, y + h - c),
                new PointF(x + w - c, y + h), new PointF(x + c, y + h), new PointF(x, y + h - c), new PointF(x, y + c) });
            return p;
        }
    }

    class Spring
    {
        public double X, V, T, K, D, Eps = 0.002;
        public Spring(double x, double k, double d) { X = T = x; K = k; D = d; }
        public void Step(double dt)
        {
            if (Rest) { X = T; V = 0; return; }
            while (dt > 1e-9) { double h = Math.Min(dt, 1 / 240.0); V += (K * (T - X) - D * V) * h; X += V * h; dt -= h; }
        }
        public bool Rest { get { return Math.Abs(T - X) < Eps && Math.Abs(V) < Eps * 4; } }
        public void Snap(double x) { X = T = x; V = 0; }
    }

    static class Settings
    {
        public static double X = double.NaN, Y = double.NaN;
        public static bool Sound = true, Float = false, Auto = true;
        public static int Best = 0, Focus = 25;
        public static string Seen = "", Name = ""; public static bool Asked;
        public static bool NoSave;
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Obsidot"); } }
        static string File_ { get { return Path.Combine(Dir, "obsidot.ini"); } }

        // Obsidot used to be called LUMEN: bring its settings, notes and best score along once
        static void Migrate()
        {
            try
            {
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lumen");
                if (Directory.Exists(Dir) || !Directory.Exists(old)) return;
                Directory.CreateDirectory(Dir);
                foreach (var f in Directory.GetFiles(old))
                {
                    string name = Path.GetFileName(f); if (name == "lumen.ini") name = "obsidot.ini";
                    File.Copy(f, Path.Combine(Dir, name), false);
                }
            }
            catch { }
        }

        public static void Load()
        {
            Migrate();
            try
            {
                foreach (var line in File.ReadAllLines(File_))
                {
                    int i = line.IndexOf('='); if (i < 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    if (k == "x") X = double.Parse(v, inv); else if (k == "y") Y = double.Parse(v, inv);
                    else if (k == "sound") Sound = v == "1"; else if (k == "auto") Auto = v == "1"; else if (k == "pin") Float = v == "0";
                    else if (k == "best") Best = int.Parse(v); else if (k == "cards") Seen = v; else if (k == "focus") Focus = int.Parse(v); else if (k == "name") Name = v; else if (k == "asked") Asked = v == "1"; 
                }
            }
            catch { }
        }

        public static void Log(object e)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(File_)); File.AppendAllText(Path.Combine(Path.GetDirectoryName(File_), "crash.log"), DateTime.Now + "\r\n" + e + "\r\n\r\n"); } catch { }
        }

        public static void Save()
        {
            if (NoSave) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(File_));
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                File.WriteAllText(File_, "x=" + X.ToString(inv) + "\ny=" + Y.ToString(inv) + "\nsound=" + (Sound ? 1 : 0) + "\nauto=" + (Auto ? 1 : 0) +
                    "\npin=" + (Float ? 0 : 1) + "\nbest=" + Best + "\ncards=" + Seen + "\nfocus=" + Focus + "\nname=" + Name + "\nasked=" + (Asked ? 1 : 0) + "\n");
            }
            catch { }
        }
    }

    // A window that is nothing but per-pixel-alpha pixels: no frame, no rectangle.
    class LayeredForm : Form
    {
        protected int LW, LH;
        protected Bitmap Surface;
        IntPtr memDC, hBmp, oldBmp;

        public LayeredForm(int w, int h)
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            LW = w; LH = h; Size = new Size(w, h);
            IntPtr sdc = N.GetDC(IntPtr.Zero); memDC = N.CreateCompatibleDC(sdc); N.ReleaseDC(IntPtr.Zero, sdc);
            var bi = new N.BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            IntPtr bits;
            hBmp = N.CreateDIBSection(memDC, ref bi, 0, out bits, IntPtr.Zero, 0);
            oldBmp = N.SelectObject(memDC, hBmp);
            Surface = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 /*LAYERED*/ | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/; return cp; }
        }
        protected override bool ShowWithoutActivation { get { return true; } }

        protected void Present(int x, int y)
        {
            var pt = new N.POINT { X = x, Y = y };
            var sz = new N.SIZE { cx = LW, cy = LH }; var src = new N.POINT();
            var bf = new N.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            N.UpdateLayeredWindow(Handle, IntPtr.Zero, ref pt, ref sz, memDC, ref src, 0, ref bf, 2);
        }

        // float=false: the object lives on the desktop. It stays a normal window but pins itself in the z-order directly
        // above the desktop, so every app covers it and showing the desktop reveals it. float=true: top-most.
        bool desk;
        public LayeredForm Under; // pin directly above this window instead of the desktop (the card sits on the crystal)

        public void SetFloat(bool top)
        {
            desk = !top; raised = false;
            if (!IsHandleCreated) return;
            N.SetWindowPos(Handle, top ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x13);
            if (desk) N.SetWindowPos(Handle, new IntPtr(1), 0, 0, 0, 0, 0x13); // re-sink; WndProc picks the exact slot
        }

        // While you are looking at the desktop itself, Windows raises the desktop above ordinary windows, so we rise with it.
        bool raised;
        public void Raise(bool on)
        {
            if (!desk || !IsHandleCreated || on == raised) return;
            raised = on;
            N.SetWindowPos(Handle, on ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x13);
            if (!on) N.SetWindowPos(Handle, new IntPtr(1), 0, 0, 0, 0, 0x13);
        }

        IntPtr DeskSlot()
        {
            IntPtr anchor = Under != null && Under.IsHandleCreated ? Under.Handle : Desk.Host();
            if (anchor == IntPtr.Zero) return new IntPtr(1);
            IntPtr above = N.GetWindow(anchor, 3); // GW_HWNDPREV: whatever sits just above the anchor
            return above == Handle ? IntPtr.Zero : above == IntPtr.Zero ? IntPtr.Zero : above;
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            if (m.Msg == 0x46 && desk && !raised) // WM_WINDOWPOSCHANGING: whatever asks, stay just above the desktop
            {
                int P = IntPtr.Size, fl = Marshal.ReadInt32(m.LParam, 2 * P + 16);
                IntPtr want = Marshal.ReadIntPtr(m.LParam, P);
                if ((fl & 0x4) == 0 && want != new IntPtr(-2)) // has a z-order change, and is not the topmost->normal step
                {
                    IntPtr slot = DeskSlot();
                    if (slot == IntPtr.Zero) Marshal.WriteInt32(m.LParam, 2 * P + 16, fl | 0x4); // already in place
                    else Marshal.WriteIntPtr(m.LParam, P, slot);
                }
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Surface != null) { Surface.Dispose(); Surface = null; N.SelectObject(memDC, oldBmp); N.DeleteObject(hBmp); N.DeleteDC(memDC); }
            base.Dispose(disposing);
        }
    }

    static class N
    {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }

        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT pt, ref SIZE sz, IntPtr src, ref POINT sp, int key, ref BLENDFUNCTION bf, int flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, int flags);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, int flags);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, int cmd);
        public delegate IntPtr LowLevelProc(int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, LowLevelProc fn, IntPtr mod, uint tid);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
        [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG m, IntPtr h, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a, int b, ref int v, int f);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(int a, int b, StringBuilder v, int f);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bi, int usage, out IntPtr bits, IntPtr sec, int off);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateEllipticRgn(int l, int t, int r, int b);
        [DllImport("gdi32.dll")] public static extern int CombineRgn(IntPtr dst, IntPtr a, IntPtr b, int mode);
        [DllImport("gdi32.dll")] public static extern int SelectClipRgn(IntPtr dc, IntPtr rgn);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(int access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, IntPtr size, int type, int prot);
        [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr p, IntPtr a, IntPtr size, int type);
        [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] buf, IntPtr size, out IntPtr read);
        [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] buf, IntPtr size, out IntPtr written);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("winmm.dll")] public static extern int timeBeginPeriod(int ms);
        [DllImport("winmm.dll")] public static extern int timeEndPeriod(int ms);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
    }
}
