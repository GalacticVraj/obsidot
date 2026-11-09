using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Obsidot
{
    class Target
    {
        public string Name; public int Kind; public Bitmap Img, White, LabelBmp;
        public RectangleF Icon, Label, Hit;
        public int Dmg; public bool Dead; public double DeadT, BornT = -10, FlashT = -10;
        public readonly List<PointF[]> Cracks = new List<PointF[]>();
        public float Phase;
        public readonly Spring Punch = new Spring(1, 520, 14) { Eps = 0.001 };
        public PointF C { get { return new PointF(Icon.X + Icon.Width / 2, Icon.Y + Icon.Height / 2); } }
    }

    class Part
    {
        public int T; // 0 spark, 1 pixel, 2 chunk, 3 paper, 4 debris, 5 glow
        public float X, Y, VX, VY, A, VA, Sz, Life, Max, Grav, Drag, Seed;
        public Color C; public Bitmap Bmp; public PointF[] Poly; public bool Floor;
    }

    class Wave { public float X, Y, R, VR, Life, Max, W; public Color C; }
    class Bolt { public float X, Y, TX, TY, VX, VY; }

    // Mode 2: the desktop freezes, the windows dissolve, the icons become glass targets. Pure simulation.
    class Assault : Form
    {
        readonly Crystal cr; readonly float S = U.S; readonly Rectangle scr;
        Bitmap snap, world; IntPtr worldDC, worldH;
        readonly List<Target> targets = new List<Target>();
        readonly List<Part> parts = new List<Part>();
        readonly List<Wave> waves = new List<Wave>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly List<KeyValuePair<double, Action>> later = new List<KeyValuePair<double, Action>>();
        PointF E, M; float floorY;
        public int weapon; bool fireDown; double nextFire, chargeT0 = -1, laserTick, railT = -10; PointF railA, railB;
        double t0, revealT0 = -1, exitT0 = -1, last, hitStop, shake, clearT = -1, kick;
        float shX, shY;
        double fragments, shown;
        readonly Spring slot = new Spring(0, 200, 20);
        readonly Random rnd = new Random();
        bool loaded, finished;
        static readonly Color[] WCol = { Color.FromArgb(127, 231, 255), Color.FromArgb(190, 150, 255), Color.FromArgb(255, 227, 168), Color.FromArgb(255, 150, 80) };
        static readonly string[] WName = { "PIXEL BLASTER", "LASER ARC", "CRYSTAL RAILCANNON", "BLACK HOLE" };
        double holeT0 = -1, holeTick, collapseT = -10; PointF holeP, collapseP; // weapon 4: a singularity that follows the cursor

        public Assault(Crystal cr, PointF emitter) : this(cr, emitter, null, null, null) { }

        // stagedScreen / stagedWall / stagedIcons: a made-up desktop for the README demo instead of your real screen
        public Assault(Crystal cr, PointF emitter, Bitmap stagedScreen, Bitmap stagedWall, List<DIcon> stagedIcons)
        {
            this.cr = cr;
            scr = Screen.PrimaryScreen.Bounds;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; Bounds = scr;
            TopMost = true; ShowInTaskbar = false; KeyPreview = true; BackColor = Color.Black; Text = "Obsidot";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            snap = new Bitmap(scr.Width, scr.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(snap))
                if (stagedScreen != null) g.DrawImage(stagedScreen, new Rectangle(0, 0, scr.Width, scr.Height)); else g.CopyFromScreen(scr.Location, Point.Empty, scr.Size);
            stagedWallpaper = stagedWall;
            if (stagedIcons != null) icons = stagedIcons;
            else
            {
                // only icons you can actually see become targets; everything else on screen is glass
                icons = Desk.Read(scr);
                icons.RemoveAll(d => !Desk.OnDesktop(new Point((int)(d.Icon.X + d.Icon.Width / 2), (int)(d.Icon.Y + d.Icon.Height / 2))));
            }
            E = new PointF(emitter.X - scr.X, emitter.Y - scr.Y);
            E.X = (float)U.Clamp(E.X, 60 * S, scr.Width - 60 * S); E.Y = (float)U.Clamp(E.Y, 60 * S, scr.Height - 60 * S);
            M = new PointF(scr.Width / 2f, scr.Height / 2f);
            floorY = scr.Height - 4 * S;
            t0 = last = U.Now;
            Snd.Play("shutter");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate(); Cursor.Hide(); N.timeBeginPeriod(1); Application.Idle += Loop;
            cr.Hide();
            Frame(U.Now); Present();
            BeginInvoke(new Action(LoadWorld));
        }

        List<DIcon> icons; Bitmap stagedWallpaper;
        Bitmap under;                 // the clean desktop, revealed through holes you shoot in the screen
        float cell; PointF[,] gv; int[,] hp; int gcols, grows;

        public void LoadWorld()
        {
            int w = scr.Width, h = scr.Height;
            under = stagedWallpaper != null ? new Bitmap(stagedWallpaper, scr.Size) : (Desk.Wallpaper(scr.Size) ?? new Bitmap(snap));
            BuildWorld(under, 150, 22);
            world = U.Dib(w, h, out worldDC, out worldH); // DIB-backed: GDI+ scars it, GDI blits it
            using (var g = Graphics.FromImage(world))
            {
                g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(snap, new Rectangle(0, 0, w, h));
            }
            BuildWorld(world, 110, 12);
            // glass lattice: shared jittered vertices so neighbouring holes fit like real fracture
            cell = 100 * S; gcols = (int)Math.Ceiling(w / cell); grows = (int)Math.Ceiling(h / cell);
            gv = new PointF[gcols + 1, grows + 1]; hp = new int[gcols, grows];
            for (int i = 0; i <= gcols; i++)
                for (int j = 0; j <= grows; j++)
                {
                    float jx = i > 0 && i < gcols ? (float)(rnd.NextDouble() - 0.5) * cell * 0.5f : 0, jy = j > 0 && j < grows ? (float)(rnd.NextDouble() - 0.5) * cell * 0.5f : 0;
                    gv[i, j] = new PointF(Math.Min(w, i * cell) + jx, Math.Min(h, j * cell) + jy);
                    if (i < gcols && j < grows) hp[i, j] = 3;
                }
            foreach (var d in icons)
            {
                var t = new Target { Name = d.Name, Kind = d.Kind, Phase = (float)rnd.NextDouble() * 6 };
                RectangleF ir = new RectangleF(d.Icon.X - scr.X, d.Icon.Y - scr.Y, d.Icon.Width, d.Icon.Height);
                t.Img = d.Img ?? Crop(snap, ir);
                float iw = t.Img.Width, ih = t.Img.Height;
                t.Icon = new RectangleF(ir.X + (ir.Width - iw) / 2, ir.Y + (ir.Height - ih) / 2, iw, ih);
                t.Label = new RectangleF(d.Label.X - scr.X - 10 * S, d.Label.Y - scr.Y, d.Label.Width + 20 * S, d.Label.Height + 2 * S); // a little slack so our font never ellipsizes Explorer's tight rect
                Finish(t);
                targets.Add(t);
                // the target draws itself now; lift the frozen copy off the glass
                var r = Rectangle.Round(RectangleF.Inflate(t.Hit, 2 * S, 2 * S));
                using (var g = Graphics.FromImage(world)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(under, r, r, GraphicsUnit.Pixel); }
            }
            loaded = true; revealT0 = U.Now; Snd.Play("whoosh");
        }

        // ---------------- the screen itself is glass ----------------
        void Strike(PointF p, int dmg, int w)
        {
            var t = TargetAt(p);
            if (t != null) { Hit(t, dmg, p, w); return; }
            int i = (int)U.Clamp(p.X / cell, 0, gcols - 1), j = (int)U.Clamp(p.Y / cell, 0, grows - 1);
            HitCell(i, j, dmg, p, w);
        }

        void HitCell(int i, int j, int dmg, PointF p, int w)
        {
            if (hp == null) return;
            if (hp[i, j] <= 0) { Sparks(p, WCol[w], 3, 260); return; } // already open: the shot falls into the desktop
            hp[i, j] -= dmg;
            if (hp[i, j] <= 0) { Shatter(i, j, p); return; }
            Scar(p, 3 + (3 - hp[i, j]) * 2, cell * (0.28f + 0.14f * (3 - hp[i, j])));
            Sparks(p, WCol[w], 8, 480);
            var c = world.GetPixel((int)U.Clamp(p.X, 0, world.Width - 1), (int)U.Clamp(p.Y, 0, world.Height - 1));
            for (int k = 0; k < 5; k++) parts.Add(Pixel(p, Color.FromArgb(255, c), 2.5f * S, 300, 1.1f));
            Snd.Play("crack"); shake = Math.Max(shake, 2.5 * S);
        }

        // permanent cracks, painted straight into the frozen screen
        void Scar(PointF p, int n, float len)
        {
            using (var g = Graphics.FromImage(world))
            using (var dark = new Pen(Color.FromArgb(150, 0, 0, 8), 2.4f * S) { LineJoin = LineJoin.Round })
            using (var light = new Pen(Color.FromArgb(220, 240, 248, 255), 0.9f * S) { LineJoin = LineJoin.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int b = 0; b < n; b++)
                {
                    double a = b * Math.PI * 2 / n + rnd.NextDouble() * 0.9; var pts = new List<PointF> { p }; float x = p.X, y = p.Y;
                    for (int s = 0; s < 4; s++) { a += (rnd.NextDouble() - 0.5) * 0.9; float l = len / 4 * (0.6f + (float)rnd.NextDouble() * 0.8f); x += (float)Math.Cos(a) * l; y += (float)Math.Sin(a) * l; pts.Add(new PointF(x, y)); }
                    var arr = pts.ToArray(); g.DrawLines(dark, arr); g.DrawLines(light, arr);
                }
                U.Glow(g, Color.White, p.X, p.Y, 7 * S, 0.8);
            }
        }

        // a pane of the screen breaks out from the impact point and falls; the desktop shows through
        void Shatter(int i, int j, PointF p)
        {
            var poly = new[] { gv[i, j], gv[i + 1, j], gv[i + 1, j + 1], gv[i, j + 1] };
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            foreach (var v in poly) { minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); }
            var bb = Rectangle.FromLTRB((int)Math.Max(0, minX - 2), (int)Math.Max(0, minY - 2), (int)Math.Min(world.Width, maxX + 3), (int)Math.Min(world.Height, maxY + 3));
            if (bb.Width < 3 || bb.Height < 3) return;
            var o = new PointF((float)U.Clamp(p.X, minX + 4, maxX - 4), (float)U.Clamp(p.Y, minY + 4, maxY - 4));
            using (var crop = world.Clone(bb, PixelFormat.Format32bppPArgb))
            {
                for (int k = 0; k < 4; k++)
                {
                    var tri = new[] { o, poly[k], poly[(k + 1) % 4] };
                    Shard(crop, bb, tri, o);
                }
            }
            using (var g = Graphics.FromImage(world))
            using (var path = new GraphicsPath())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                path.AddPolygon(poly);
                g.SetClip(path);
                g.DrawImage(under, bb, bb, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(110, 0, 0, 10), 7 * S)) g.DrawPolygon(pen, poly); // depth at the broken edge
                g.ResetClip();
                using (var pen = new Pen(Color.FromArgb(170, 220, 236, 255), 1.2f * S)) g.DrawPolygon(pen, poly);
            }
            var cc = new PointF((minX + maxX) / 2, (minY + maxY) / 2);
            waves.Add(new Wave { X = o.X, Y = o.Y, R = 6 * S, VR = 420 * S, Life = 0.45f, Max = 0.45f, W = 2 * S, C = Color.FromArgb(200, 225, 255) });
            Glows(cc, Color.FromArgb(170, 205, 255), 10, 200);
            Sparks(o, Color.White, 14, 650);
            fragments += 16; hitStop = Math.Max(hitStop, 0.03);
            Snd.Play("shatter"); shake = Math.Max(shake, 7 * S);
        }

        void Shard(Bitmap crop, Rectangle bb, PointF[] tri, PointF o)
        {
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            foreach (var v in tri) { minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); }
            int bw = Math.Max(2, (int)Math.Ceiling(maxX - minX) + 2), bh = Math.Max(2, (int)Math.Ceiling(maxY - minY) + 2);
            var bmp = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
            var local = new PointF[3];
            for (int k = 0; k < 3; k++) local[k] = new PointF(tri[k].X - minX + 1, tri[k].Y - minY + 1);
            using (var g = Graphics.FromImage(bmp))
            using (var tb = new TextureBrush(crop))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                tb.TranslateTransform(bb.X - minX + 1, bb.Y - minY + 1);
                g.FillPolygon(tb, local);
                using (var pen = new Pen(Color.FromArgb(150, 230, 240, 255), 1f)) g.DrawPolygon(pen, local);
            }
            float px = (minX + maxX) / 2, py = (minY + maxY) / 2, dx = px - o.X, dy = py - o.Y, d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
            float sp = (120 + (float)rnd.NextDouble() * 260) * S;
            parts.Add(new Part
            {
                T = 2, Bmp = bmp, X = px, Y = py, VX = dx / d * sp, VY = dy / d * sp - 160 * S, VA = (float)(rnd.NextDouble() - 0.5) * 300,
                Grav = 1500 * S, Drag = 0.3f, Life = 3.5f + (float)rnd.NextDouble(), Max = 4.5f, Sz = Math.Max(bw, bh) * 0.5f, Floor = true
            });
        }

        void Finish(Target t)
        {
            t.Hit = t.Label.IsEmpty ? t.Icon : RectangleF.Union(t.Icon, t.Label);
            t.Hit.Inflate(2 * S, 2 * S);
            t.White = new Bitmap(t.Img.Width, t.Img.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(t.White))
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(new ColorMatrix(new[] { new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 1, 0 }, new float[] { 1, 1, 1, 0, 1 } }));
                g.DrawImage(t.Img, new Rectangle(0, 0, t.Img.Width, t.Img.Height), 0, 0, t.Img.Width, t.Img.Height, GraphicsUnit.Pixel, ia);
            }
            if (!t.Label.IsEmpty && t.Label.Width > 2 && t.Label.Height > 2)
            {
                t.LabelBmp = new Bitmap((int)t.Label.Width + 4, (int)t.Label.Height + 4, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(t.LabelBmp))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit })
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var f = U.F("Segoe UI", 12f * S); var r = new RectangleF(1, 1, t.Label.Width, t.Label.Height);
                    using (var b = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.DrawString(t.Name, f, b, new RectangleF(r.X + 1, r.Y + 1, r.Width, r.Height), fmt);
                    using (var b = new SolidBrush(Color.White)) g.DrawString(t.Name, f, b, r, fmt);
                }
            }
        }

        static Bitmap Crop(Bitmap src, RectangleF r)
        {
            var rr = Rectangle.Round(r); rr.Intersect(new Rectangle(0, 0, src.Width, src.Height));
            if (rr.Width < 2 || rr.Height < 2) return new Bitmap(8, 8, PixelFormat.Format32bppPArgb);
            return src.Clone(rr, PixelFormat.Format32bppPArgb);
        }

        void BuildWorld(Bitmap w, int vignette, int grid)
        {
            using (var g = Graphics.FromImage(w))
            {
                using (var br = new SolidBrush(Color.FromArgb(vignette / 3, 6, 8, 20))) g.FillRectangle(br, 0, 0, w.Width, w.Height);
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(-w.Width * 0.3f, -w.Height * 0.3f, w.Width * 1.6f, w.Height * 1.6f);
                    using (var pg = new PathGradientBrush(p)) { pg.CenterColor = Color.FromArgb(0, 0, 0, 0); pg.SurroundColors = new[] { Color.FromArgb(vignette, 0, 0, 6) }; pg.FocusScales = new PointF(0.45f, 0.45f); g.FillRectangle(pg, 0, 0, w.Width, w.Height); }
                }
                float sp = 26 * S, d = 1.1f * S;
                using (var br = new SolidBrush(Color.FromArgb(grid, 220, 230, 255)))
                    for (float y = sp / 2; y < w.Height; y += sp) for (float x = sp / 2; x < w.Width; x += sp) g.FillRectangle(br, x, y, d, d);
            }
        }

        public bool Bench; double benchStart = -1, benchPaint; int benchFrames; System.Diagnostics.Stopwatch benchClock;
        public int TargetCount { get { return targets.Count; } }
        public void Demo() // shots harness: a mid-battle frame
        {
            revealT0 = U.Now - 5; double now = U.Now;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (i % 4 == 1) Hit(t, 1, t.C, 0); else if (i % 4 == 2) Hit(t, 2, new PointF(t.C.X + 5, t.C.Y - 6), 1); else if (i % 4 == 3) Hit(t, 3, t.C, 2);
            }
            HitCell(gcols / 2, grows / 2, 3, new PointF(gcols / 2 * cell + cell / 2, grows / 2 * cell + cell / 2), 2);
            var q = new PointF((gcols / 2 + 2) * cell + cell / 3, (grows / 2 - 1) * cell + cell / 2); HitCell(gcols / 2 + 2, grows / 2 - 1, 1, q, 0); HitCell(gcols / 2 + 2, grows / 2 - 1, 1, q, 0);
            weapon = 1; fireDown = true; M = targets.Count > 0 ? targets[0].C : new PointF(scr.Width * 0.3f, scr.Height * 0.4f); hitStop = 0; revealT0 = U.Now - 5;
        }

        // ---- the README demo: a scripted run through every weapon on a staged desktop ----
        public bool Scripted;
        public void Begin(double now) { Scripted = true; revealT0 = t0 = last = now; }
        static PointF Lerp(PointF a, PointF b, double k) { k = U.Clamp01(k); k = k * k * (3 - 2 * k); return new PointF((float)(a.X + (b.X - a.X) * k), (float)(a.Y + (b.Y - a.Y) * k)); }
        PointF At(float fx, float fy) { return new PointF(scr.Width * fx, scr.Height * fy); }
        public void Script(double t, double now)
        {
            Func<int, PointF> ic = i => i < targets.Count ? targets[i].C : At(0.1f, 0.2f);
            if (t < 0.7) { M = Lerp(At(0.5f, 0.5f), At(0.47f, 0.33f), t / 0.7); return; }
            if (t < 2.3)       // Pixel Blaster across the window glass, then into a folder
            {
                if (weapon != 0) SetWeapon(0);
                fireDown = true;
                M = t < 1.6 ? Lerp(At(0.47f, 0.33f), At(0.66f, 0.52f), (t - 0.7) / 0.9) : Lerp(At(0.66f, 0.52f), ic(0), (t - 1.6) / 0.35);
                return;
            }
            if (t < 2.5) { fireDown = false; M = Lerp(ic(0), ic(1), (t - 2.3) / 0.2); return; }
            if (t < 4.0)       // Laser Arc down the column of icons
            {
                if (weapon != 1) SetWeapon(1);
                fireDown = true;
                double k = (t - 2.5) / 1.5; int a = Math.Min(3, 1 + (int)(k * 3)); M = Lerp(ic(a), ic(Math.Min(a + 1, 4)), (k * 3) % 1 * 0.35);
                return;
            }
            if (t < 4.2) { fireDown = false; M = Lerp(M, ic(6), (t - 4.0) / 0.2); return; }
            if (t < 5.1)       // Crystal Railcannon: charge, then one beam through the screen
            {
                if (weapon != 2) SetWeapon(2);
                if (chargeT0 < 0 && t < 4.95) { chargeT0 = now; fireDown = true; }
                M = ic(6);                                         // straight at the Recycle Bin
                if (t >= 4.95 && chargeT0 >= 0) { fireDown = false; FireRail(now); }
                return;
            }
            if (t < 7.6)       // the Black Hole: drag it across the window, then let go
            {
                if (weapon != 3) SetWeapon(3);
                if (holeT0 < 0 && t < 7.4) { holeT0 = holeTick = now; holeP = M; fireDown = true; }
                M = t < 6.0 ? Lerp(ic(6), ic(4), (t - 5.1) / 0.9) : Lerp(ic(4), At(0.58f, 0.42f), (t - 6.0) / 1.2); // up past the last icons, into the window
                if (t >= 7.4 && holeT0 >= 0) { fireDown = false; Collapse(now); }
                return;
            }
            fireDown = false; M = Lerp(At(0.58f, 0.42f), At(0.62f, 0.38f), (t - 7.6) / 1.5);
        }

        public void DemoHole(double t) { weapon = 3; fireDown = false; holeT0 = t - 1.3; holeP = M = new PointF(scr.Width * 0.45f, scr.Height * 0.45f); }

        // ---------------- loop ----------------
        // classic game loop: run frames while the message queue is empty; DwmFlush paces each one to vblank
        void Loop(object s, EventArgs e) { N.MSG m; while (!finished && !N.PeekMessage(out m, IntPtr.Zero, 0, 0, 0)) Tick(null, null); }

        void Tick(object s, EventArgs e)
        {
            if (finished) return;
            double now = U.Now, dt = Math.Min(0.05, now - last); last = now;
            if (hitStop > 0) { hitStop -= dt; dt *= 0.06; }
            U.Mk(8);
            Step(dt, now); U.Mk(7);
            if (finished) return;
            if (Bench && loaded)
            {
                if (benchStart < 0) { benchStart = now; benchClock = System.Diagnostics.Stopwatch.StartNew(); }
                double bt = now - benchStart;
                if (bt > 1.2 && bt < 1.25 && benchFrames == 0) Demo();
                U.Prof = bt > 1.3;
                if (bt > 1.3) { var p0 = System.Diagnostics.Stopwatch.StartNew(); Frame(now); Present(); U.Mk(6); benchPaint += p0.Elapsed.TotalMilliseconds; benchFrames++; N.DwmFlush(); }
                else { Frame(now); Present(); N.DwmFlush(); }
                if (bt > 4.3 && exitT0 < 0)
                {
                    System.IO.File.AppendAllText(Crystal.BenchLog, "assault: paint " + (benchPaint / benchFrames).ToString("0.00") + " ms/frame, " + (benchFrames / 3.0).ToString("0") + " fps, " + targets.Count + " targets, " + parts.Count + " particles\r\n  sections " + U.Dump(benchFrames) + "\r\n"); U.Prof = false;
                    Exit();
                }
                return;
            }
            Frame(now); Present();
            N.DwmFlush();
        }

        public void Step(double dt, double now)
        {
            float fdt = (float)dt;
            slot.T = weapon; slot.Step(dt);
            for (int i = later.Count - 1; i >= 0; i--) if (now >= later[i].Key) { var a = later[i].Value; later.RemoveAt(i); a(); }

            if (fireDown && !Scripted && (Control.MouseButtons & MouseButtons.Left) == 0) // a missed mouse-up must never leave a weapon firing
            {
                fireDown = false; if (weapon == 2 && chargeT0 >= 0) FireRail(now); if (weapon == 3 && holeT0 >= 0) Collapse(now);
            }
            if (loaded && exitT0 < 0 && Revealed(now))
            {
                if (fireDown && weapon == 0 && now >= nextFire) { FireBolt(); nextFire = now + 0.085; }
                if (fireDown && weapon == 1 && now >= laserTick)
                {
                    laserTick = now + 0.11;
                    Strike(M, 1, 1); Sparks(M, WCol[1], 3, 300);
                    Snd.Play("zap");
                }
                if (weapon == 3 && holeT0 >= 0) HoleStep(now, dt);
                if (weapon == 2 && chargeT0 >= 0)
                {
                    double k = Charge(now);
                    for (int i = 0; i < 2; i++)
                    {
                        double a = rnd.NextDouble() * Math.PI * 2; float r = (70 - 30 * (float)k) * S;
                        parts.Add(new Part { T = 0, X = M.X + (float)Math.Cos(a) * r, Y = M.Y + (float)Math.Sin(a) * r, VX = -(float)Math.Cos(a) * r * 4, VY = -(float)Math.Sin(a) * r * 4, Life = 0.22f, Max = 0.22f, C = WCol[2], Sz = 1 });
                    }
                }
            }

            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i]; float dx = b.TX - b.X, dy = b.TY - b.Y, d = (float)Math.Sqrt(dx * dx + dy * dy), step = 5200 * S * fdt;
                if (d <= step) { bolts.RemoveAt(i); Impact(new PointF(b.TX, b.TY)); }
                else { b.VX = dx / d * 5200 * S; b.VY = dy / d * 5200 * S; b.X += dx / d * step; b.Y += dy / d * step; }
            }

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.Life -= fdt; if (p.Life <= 0) { parts[i] = parts[parts.Count - 1]; parts.RemoveAt(parts.Count - 1); continue; }
                if (p.T == 3) { p.VX += (float)Math.Cos(now * 7 * p.Seed + p.Seed * 10) * 900 * S * fdt; }
                if (holeT0 >= 0 && Pull(p, now, fdt)) { parts[i] = parts[parts.Count - 1]; parts.RemoveAt(parts.Count - 1); if (p.Bmp != null) p.Bmp.Dispose(); continue; }
                float drag = (float)Math.Exp(-p.Drag * dt); p.VX *= drag; p.VY = p.VY * drag + p.Grav * fdt;
                p.X += p.VX * fdt; p.Y += p.VY * fdt; p.A += p.VA * fdt;
                if (p.Floor && p.Y > floorY - p.Sz * 0.4f)
                {
                    p.Y = floorY - p.Sz * 0.4f;
                    if (p.VY > 0) { if (p.VY > 260 * S && p.T == 2) { /* settle */ } p.VY *= -0.32f; p.VX *= 0.72f; p.VA *= 0.6f; }
                }
            }
            for (int i = waves.Count - 1; i >= 0; i--) { var w = waves[i]; w.Life -= fdt; w.R += w.VR * fdt; w.VR *= (float)Math.Exp(-3.2 * dt); if (w.Life <= 0) waves.RemoveAt(i); }
            foreach (var t in targets) t.Punch.Step(dt);
            cr.pet.Scared = holeT0 >= 0;                                  // the pet rides on the emitter; the black hole terrifies it
            cr.pet.Tick(dt, now, new PointF(M.X - E.X, M.Y - (E.Y - 32 * S)));

            shake *= Math.Exp(-dt * 10); kick *= Math.Exp(-dt * 14);
            double sk = U.Calm ? 0 : shake;
            if (sk < 0.6) shX = shY = 0;
            else { shX = (float)Math.Round((rnd.NextDouble() - 0.5) * 2 * sk); shY = (float)Math.Round((rnd.NextDouble() - 0.5) * 2 * sk); }
            shown += (fragments - shown) * (1 - Math.Exp(-8 * dt));

            if (loaded && clearT < 0 && targets.Count > 0 && targets.TrueForAll(x => x.Dead))
            {
                clearT = now; cr.pet.Cheer(now + 0.9, true);
                later.Add(new KeyValuePair<double, Action>(now + 0.9, () => Snd.Play("clear")));
                later.Add(new KeyValuePair<double, Action>(now + 3.4, Rebirth));
            }
            if (exitT0 >= 0 && now - exitT0 > 0.55) Done();
        }

        bool Revealed(double now) { return revealT0 >= 0 && now - revealT0 > 0.25; }
        double Charge(double now) { return chargeT0 < 0 ? 0 : U.Clamp01((now - chargeT0) / 0.7); }

        void Rebirth()
        {
            double now = U.Now; int i = 0;
            foreach (var t in targets)
            {
                var tt = t; double at = now + i++ * 0.05;
                later.Add(new KeyValuePair<double, Action>(at, () =>
                {
                    tt.Dead = false; tt.Dmg = 0; tt.Cracks.Clear(); tt.BornT = U.Now; tt.Punch.Snap(1);
                    Glows(tt.C, Color.FromArgb(170, 205, 255), 8, 120);
                    if (rnd.Next(3) == 0) Snd.Play("reborn");
                }));
            }
            clearT = -1;
        }

        // ---------------- weapons ----------------
        void FireBolt()
        {
            bolts.Add(new Bolt { X = E.X, Y = E.Y, TX = M.X + (float)(rnd.NextDouble() - 0.5) * 4 * S, TY = M.Y + (float)(rnd.NextDouble() - 0.5) * 4 * S });
            kick = 1; Snd.Play("pew");
        }

        void Impact(PointF p)
        {
            for (int i = 0; i < 9; i++) parts.Add(Pixel(p, WCol[0], 2.6f * S, 380, 0.45f));
            waves.Add(new Wave { X = p.X, Y = p.Y, R = 4 * S, VR = 260 * S, Life = 0.25f, Max = 0.25f, W = 1.5f * S, C = WCol[0] });
            Strike(p, 1, 0);
        }

        void FireRail(double now)
        {
            double k = Charge(now); chargeT0 = -1;
            if (k < 0.45) { Snd.Play("fizzle"); return; }
            float dx = M.X - E.X, dy = M.Y - E.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 1) { dx = 0; dy = -1; d = 1; }
            dx /= d; dy /= d;
            railA = E; railB = new PointF(E.X + dx * 6000 * S, E.Y + dy * 6000 * S); railT = now;
            var hitSet = new HashSet<Target>(); var cellSet = new HashSet<int>();
            for (float s = 0; s < 6000 * S; s += 5 * S)
            {
                var p = new PointF(E.X + dx * s, E.Y + dy * s);
                if (p.X < -50 || p.Y < -50 || p.X > scr.Width + 50 || p.Y > scr.Height + 50) break;
                foreach (var t in targets) if (!t.Dead && t.Hit.Contains(p) && hitSet.Add(t)) Hit(t, 3, p, 2);
                if (TargetAt(p) == null && hp != null && p.X >= 0 && p.Y >= 0 && p.X < scr.Width && p.Y < scr.Height)
                {
                    int ci = (int)(p.X / cell), cj = (int)(p.Y / cell);
                    if (ci < gcols && cj < grows && cellSet.Add(ci * 1000 + cj)) HitCell(ci, cj, 3, p, 2);
                }
                if (rnd.Next(14) == 0) parts.Add(new Part { T = 0, X = p.X, Y = p.Y, VX = (float)(rnd.NextDouble() - 0.5) * 500 * S - dy * 200 * S, VY = (float)(rnd.NextDouble() - 0.5) * 500 * S + dx * 200 * S, Life = 0.5f, Max = 0.5f, C = WCol[2], Drag = 3 });
            }
            waves.Add(new Wave { X = M.X, Y = M.Y, R = 10 * S, VR = 900 * S, Life = 0.45f, Max = 0.45f, W = 3 * S, C = WCol[2] });
            shake = Math.Max(shake, 16 * S * k); kick = 2.5;
            Snd.Play("rail");
        }

        // ---- black hole ----
        double HoleK(double now) { return holeT0 < 0 ? 0 : U.Clamp01((now - holeT0) / 1.6); }
        float PullR(double k) { return (float)(90 + 230 * k) * S; }

        void HoleStep(double now, double dt)
        {
            double k = HoleK(now); float pr = PullR(k), f = (float)(1 - Math.Exp(-6 * dt));
            holeP = new PointF(holeP.X + (M.X - holeP.X) * f, holeP.Y + (M.Y - holeP.Y) * f);
            for (int i = 0; i < 3; i++) // matter spiralling in
            {
                double a = rnd.NextDouble() * Math.PI * 2; float r = pr * (0.55f + 0.45f * (float)rnd.NextDouble()), cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                parts.Add(new Part { T = 0, X = holeP.X + cs * r, Y = holeP.Y + sn * r, VX = (-sn * 0.9f - cs * 0.5f) * r * 2.2f, VY = (cs * 0.9f - sn * 0.5f) * r * 2.2f, Life = 0.5f, Max = 0.5f, Drag = 1, C = i == 0 ? Color.White : WCol[3] });
            }
            if (now < holeTick) return;
            holeTick = now + 0.3;
            foreach (var t in targets) // icons in reach crack and tremble, but only the collapse finishes them
            {
                if (t.Dead) continue;
                float dx = t.C.X - holeP.X, dy = t.C.Y - holeP.Y;
                if (dx * dx + dy * dy < pr * pr * 0.5f) { if (t.Dmg < 2) Hit(t, 1, t.C, 3); else t.Punch.V -= 5; }
            }
            if (hp != null) // the glass around it gives way, a pane at a time
            {
                double a = rnd.NextDouble() * Math.PI * 2, r = rnd.NextDouble() * pr * 0.5;
                var q = new PointF(holeP.X + (float)(Math.Cos(a) * r), holeP.Y + (float)(Math.Sin(a) * r));
                if (q.X >= 0 && q.Y >= 0 && q.X < scr.Width && q.Y < scr.Height && TargetAt(q) == null) HitCell((int)(q.X / cell), (int)(q.Y / cell), 1, q, 3);
            }
            shake = Math.Max(shake, 2.5 * S * k);
        }

        // true when the particle crossed the horizon and is gone
        bool Pull(Part p, double now, float fdt)
        {
            double k = HoleK(now); float pr = PullR(k), dx = holeP.X - p.X, dy = holeP.Y - p.Y, d2 = dx * dx + dy * dy;
            if (d2 > pr * pr) return false;
            float d = (float)Math.Sqrt(d2) + 1, horizon = (float)(8 + 18 * k) * S;
            if (d < horizon) { fragments += 1; return true; }
            float g = (float)(5200 * S * (0.3 + k)) * (1.15f - d / pr), ux = dx / d, uy = dy / d;
            p.Floor = false;
            p.VX += (ux * g - uy * g * 0.55f) * fdt; p.VY += (uy * g + ux * g * 0.55f - p.Grav * 0.85f) * fdt;
            return false;
        }

        void Collapse(double now)
        {
            double k = HoleK(now); holeT0 = -1; Snd.Stop("hole");
            if (k < 0.2) { Snd.Play("fizzle"); return; }
            float R = PullR(k) * 0.8f; var c = holeP;
            collapseT = now; collapseP = c;
            foreach (var p in parts) // everything still falling in is thrown back out
            {
                float dx = p.X - c.X, dy = p.Y - c.Y, d = (float)Math.Sqrt(dx * dx + dy * dy) + 1;
                if (d < R * 1.4f) { float sp = (float)(1500 * k) * S * (1.4f - d / (R * 1.4f)); p.VX += dx / d * sp; p.VY += dy / d * sp - 200 * S; }
            }
            foreach (var t in targets.ToArray())
            {
                if (t.Dead) continue;
                float dx = t.C.X - c.X, dy = t.C.Y - c.Y;
                if (dx * dx + dy * dy < R * R) Hit(t, 3, t.C, 3);
            }
            if (hp != null)
                for (int i = 0; i < gcols; i++)
                    for (int j = 0; j < grows; j++)
                    {
                        var q = new PointF((i + 0.5f) * cell, (j + 0.5f) * cell); float dx = q.X - c.X, dy = q.Y - c.Y;
                        if (hp[i, j] > 0 && dx * dx + dy * dy < R * R * 0.4f && TargetAt(q) == null) HitCell(i, j, 3, q, 3);
                    }
            waves.Add(new Wave { X = c.X, Y = c.Y, R = 6 * S, VR = 1400 * S, Life = 0.6f, Max = 0.6f, W = 4 * S, C = Color.White });
            later.Add(new KeyValuePair<double, Action>(now + 0.07, () => waves.Add(new Wave { X = c.X, Y = c.Y, R = 4 * S, VR = 900 * S, Life = 0.55f, Max = 0.55f, W = 3 * S, C = WCol[3] })));
            later.Add(new KeyValuePair<double, Action>(now + 0.15, () => waves.Add(new Wave { X = c.X, Y = c.Y, R = 4 * S, VR = 600 * S, Life = 0.5f, Max = 0.5f, W = 2 * S, C = WCol[1] })));
            Glows(c, WCol[3], 22, 520); Sparks(c, Color.White, 40, 1300);
            shake = Math.Max(shake, 26 * S * k); hitStop = 0.09; kick = 3;
            Snd.Play("collapse");
        }

        Target TargetAt(PointF p) { for (int i = targets.Count - 1; i >= 0; i--) if (!targets[i].Dead && targets[i].Hit.Contains(p)) return targets[i]; return null; }

        void Hit(Target t, int dmg, PointF at, int w)
        {
            if (t.Dead) return;
            double now = U.Now;
            t.Dmg += dmg; t.FlashT = now; t.Punch.V -= 9;
            var c = t.C; float hw = t.Icon.Width / 2, hh = t.Icon.Height / 2;
            var p = new PointF((float)U.Clamp(at.X - c.X, -hw * 0.8, hw * 0.8), (float)U.Clamp(at.Y - c.Y, -hh * 0.8, hh * 0.8));
            if (t.Dmg < 3) AddCracks(t, p, 2 + t.Dmg + rnd.Next(2), t.Icon.Width * (0.22f + 0.12f * t.Dmg));
            Sparks(at, WCol[w], 8, 520);
            if (t.Dmg >= 3) { Destroy(t); return; }
            Snd.Play("crack"); shake = Math.Max(shake, 2.5 * S * t.Dmg);
            if (t.Dmg == 2) for (int i = 0; i < 6; i++) parts.Add(Pixel(new PointF(c.X + p.X, c.Y + p.Y), Sample(t), 2.5f * S, 260, 1.2f));
        }

        void AddCracks(Target t, PointF p, int n, float len)
        {
            float lim = t.Icon.Width * 0.52f;
            for (int b = 0; b < n; b++)
            {
                double a = rnd.NextDouble() * Math.PI * 2; var pts = new List<PointF> { p }; float x = p.X, y = p.Y;
                int segs = 3 + rnd.Next(2);
                for (int s = 0; s < segs; s++)
                {
                    a += (rnd.NextDouble() - 0.5) * 1.2; float l = len / segs * (0.6f + (float)rnd.NextDouble() * 0.8f);
                    x = (float)U.Clamp(x + Math.Cos(a) * l, -lim, lim); y = (float)U.Clamp(y + Math.Sin(a) * l, -lim, lim);
                    pts.Add(new PointF(x, y));
                }
                t.Cracks.Add(pts.ToArray());
            }
        }

        Color Sample(Target t)
        {
            for (int k = 0; k < 6; k++)
            {
                var c = t.Img.GetPixel(rnd.Next(t.Img.Width), rnd.Next(t.Img.Height));
                if (c.A > 100) return Color.FromArgb(255, c);
            }
            return Color.FromArgb(200, 215, 240);
        }

        void Destroy(Target t)
        {
            double now = U.Now;
            t.Dead = true; t.DeadT = now; hitStop = 0.05;
            cr.pet.Cheer(now, t.Kind == Desk.KBin);
            var c = t.C; int before = parts.Count;
            Color tone = t.Kind == Desk.KFolder ? Color.FromArgb(255, 214, 160) : t.Kind == Desk.KImage ? WCol[0] : t.Kind == Desk.KPaper ? Color.White : t.Kind == Desk.KBin ? Color.FromArgb(255, 150, 120) : WCol[1];
            waves.Add(new Wave { X = c.X, Y = c.Y, R = 6 * S, VR = 520 * S, Life = 0.5f, Max = 0.5f, W = 2.4f * S, C = tone });
            later.Add(new KeyValuePair<double, Action>(now + 0.06, () => waves.Add(new Wave { X = c.X, Y = c.Y, R = 4 * S, VR = 300 * S, Life = 0.4f, Max = 0.4f, W = 1.2f * S, C = Color.White })));
            Glows(c, tone, 16, 260);
            Sparks(c, tone, 18, 700);
            switch (t.Kind)
            {
                case Desk.KFolder: Break(t, Grid(3, 3), 260, 1700, 0.9f); Snd.Play("thud"); shake = Math.Max(shake, 10 * S); break;
                case Desk.KImage: PixelBurst(t, 12); Snd.Play("pixels"); shake = Math.Max(shake, 6 * S); break;
                case Desk.KPaper: PaperBurst(t, 30); Break(t, Grid(2, 2), 160, 500, 0.5f); Snd.Play("paper"); shake = Math.Max(shake, 4 * S); break;
                case Desk.KBin:
                    Break(t, Grid(3, 2), 560, 1400, 1.2f); Debris(c, 46); Snd.Play("bin"); shake = Math.Max(shake, 18 * S);
                    for (int k = 1; k <= 3; k++)
                    {
                        var q = new PointF(c.X + (float)(rnd.NextDouble() - 0.5) * 80 * S, c.Y + (float)(rnd.NextDouble() - 0.5) * 60 * S);
                        later.Add(new KeyValuePair<double, Action>(now + k * 0.11, () => { waves.Add(new Wave { X = q.X, Y = q.Y, R = 4 * S, VR = 380 * S, Life = 0.4f, Max = 0.4f, W = 2 * S, C = Color.FromArgb(255, 190, 150) }); Debris(q, 12); Sparks(q, Color.FromArgb(255, 200, 160), 10, 600); shake = Math.Max(shake, 8 * S); }));
                    }
                    break;
                default: Break(t, Fan(9), 430, 900, 1.0f); Snd.Play("shatter"); shake = Math.Max(shake, 7 * S); break;
            }
            fragments += parts.Count - before;
        }

        static List<PointF[]> Grid(int cols, int rows)
        {
            var r = new Random(); var v = new PointF[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
                for (int j = 0; j <= rows; j++)
                {
                    float x = (float)i / cols, y = (float)j / rows;
                    if (i > 0 && i < cols) x += (float)(r.NextDouble() - 0.5) * 0.3f / cols;
                    if (j > 0 && j < rows) y += (float)(r.NextDouble() - 0.5) * 0.3f / rows;
                    v[i, j] = new PointF(x, y);
                }
            var l = new List<PointF[]>();
            for (int i = 0; i < cols; i++) for (int j = 0; j < rows; j++) l.Add(new[] { v[i, j], v[i + 1, j], v[i + 1, j + 1], v[i, j + 1] });
            return l;
        }

        static List<PointF[]> Fan(int n)
        {
            var r = new Random(); var l = new List<PointF[]>();
            var c = new PointF(0.5f + (float)(r.NextDouble() - 0.5) * 0.2f, 0.5f + (float)(r.NextDouble() - 0.5) * 0.2f);
            var b = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                double a = (i + r.NextDouble() * 0.6) * Math.PI * 2 / n, cs = Math.Cos(a), sn = Math.Sin(a), k = 0.5 / Math.Max(Math.Abs(cs), Math.Abs(sn));
                b[i] = new PointF((float)(0.5 + cs * k), (float)(0.5 + sn * k));
            }
            for (int i = 0; i < n; i++) l.Add(new[] { c, b[i], b[(i + 1) % n] });
            return l;
        }

        void Break(Target t, List<PointF[]> polys, float speed, float grav, float heavy)
        {
            var c = t.C; int W = t.Img.Width, H = t.Img.Height;
            foreach (var poly in polys)
            {
                var pts = new PointF[poly.Length]; float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
                for (int i = 0; i < poly.Length; i++) { pts[i] = new PointF(poly[i].X * W, poly[i].Y * H); minX = Math.Min(minX, pts[i].X); minY = Math.Min(minY, pts[i].Y); maxX = Math.Max(maxX, pts[i].X); maxY = Math.Max(maxY, pts[i].Y); }
                int bw = Math.Max(2, (int)Math.Ceiling(maxX - minX) + 2), bh = Math.Max(2, (int)Math.Ceiling(maxY - minY) + 2);
                var bmp = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
                var local = new PointF[pts.Length];
                for (int i = 0; i < pts.Length; i++) local[i] = new PointF(pts[i].X - minX + 1, pts[i].Y - minY + 1);
                using (var g = Graphics.FromImage(bmp))
                using (var tb = new TextureBrush(t.Img))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    tb.TranslateTransform(-minX + 1, -minY + 1);
                    g.FillPolygon(tb, local);
                    using (var pen = new Pen(Color.FromArgb(120, 230, 240, 255), 1f)) g.DrawPolygon(pen, local);
                }
                float pcx = t.Icon.X + (minX + maxX) / 2, pcy = t.Icon.Y + (minY + maxY) / 2;
                float dx = pcx - c.X, dy = pcy - c.Y, d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
                float sp = speed * S * (0.6f + (float)rnd.NextDouble() * 0.8f);
                parts.Add(new Part
                {
                    T = 2, Bmp = bmp, X = pcx, Y = pcy, VX = dx / d * sp + (float)(rnd.NextDouble() - 0.5) * 120 * S, VY = dy / d * sp - (180 + (float)rnd.NextDouble() * 220) * S * heavy,
                    VA = (float)(rnd.NextDouble() - 0.5) * 720 / heavy, Grav = grav * S, Drag = 0.4f, Life = 3.2f + (float)rnd.NextDouble() * 1.5f, Max = 4.5f, Sz = Math.Max(bw, bh), Floor = true
                });
            }
        }

        void PixelBurst(Target t, int n)
        {
            var c = t.C; float cell = t.Img.Width / (float)n;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var col = t.Img.GetPixel((int)((i + 0.5f) * t.Img.Width / n), (int)((j + 0.5f) * t.Img.Height / n));
                    if (col.A < 60) continue;
                    float x = t.Icon.X + (i + 0.5f) * t.Icon.Width / n, y = t.Icon.Y + (j + 0.5f) * t.Icon.Height / n;
                    float dx = x - c.X, dy = y - c.Y, d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.5f, sp = (220 + (float)rnd.NextDouble() * 560) * S;
                    parts.Add(new Part { T = 1, X = x, Y = y, VX = dx / d * sp, VY = dy / d * sp - 260 * S, Grav = 1300 * S, Drag = 0.7f, Sz = cell * 0.92f, C = Color.FromArgb(255, col), Life = 1.4f + (float)rnd.NextDouble() * 1.4f, Max = 2.8f, Floor = true });
                }
        }

        void PaperBurst(Target t, int n)
        {
            var c = t.C;
            for (int i = 0; i < n; i++)
            {
                var col = i % 5 == 0 ? Color.FromArgb(215, 52, 45) : i % 3 == 0 ? Color.FromArgb(200, 205, 214) : Color.FromArgb(246, 244, 238);
                parts.Add(new Part
                {
                    T = 3, X = c.X + (float)(rnd.NextDouble() - 0.5) * t.Icon.Width, Y = c.Y + (float)(rnd.NextDouble() - 0.5) * t.Icon.Height,
                    VX = (float)(rnd.NextDouble() - 0.5) * 620 * S, VY = -(150 + (float)rnd.NextDouble() * 420) * S, Grav = 300 * S, Drag = 1.6f,
                    Sz = (6 + (float)rnd.NextDouble() * 10) * S, C = col, Seed = 0.5f + (float)rnd.NextDouble(), Life = 3 + (float)rnd.NextDouble() * 1.8f, Max = 4.8f, Floor = true,
                    VA = (float)(rnd.NextDouble() - 0.5) * 300
                });
            }
        }

        void Debris(PointF c, int n)
        {
            Color[] pal = { Color.FromArgb(120, 130, 142), Color.FromArgb(86, 140, 110), Color.FromArgb(170, 160, 140), Color.FromArgb(80, 100, 160), Color.FromArgb(210, 210, 215) };
            for (int i = 0; i < n; i++)
            {
                int v = 5; var poly = new PointF[v]; float sz = (4 + (float)rnd.NextDouble() * 9) * S;
                for (int k = 0; k < v; k++) { double a = k * Math.PI * 2 / v; float r = sz * (0.5f + (float)rnd.NextDouble() * 0.6f); poly[k] = new PointF((float)Math.Cos(a) * r, (float)Math.Sin(a) * r); }
                double ang = rnd.NextDouble() * Math.PI * 2; float sp = (300 + (float)rnd.NextDouble() * 700) * S;
                parts.Add(new Part { T = 4, Poly = poly, X = c.X, Y = c.Y, VX = (float)Math.Cos(ang) * sp, VY = (float)Math.Sin(ang) * sp - 300 * S, Grav = 1500 * S, Drag = 0.5f, VA = (float)(rnd.NextDouble() - 0.5) * 1000, Sz = sz, C = pal[rnd.Next(pal.Length)], Life = 2.5f + (float)rnd.NextDouble() * 2, Max = 4.5f, Floor = true });
            }
        }

        Part Pixel(PointF p, Color c, float sz, float speed, float life)
        {
            double a = rnd.NextDouble() * Math.PI * 2; float sp = speed * S * (0.4f + (float)rnd.NextDouble());
            return new Part { T = 1, X = p.X, Y = p.Y, VX = (float)Math.Cos(a) * sp, VY = (float)Math.Sin(a) * sp - 120 * S, Grav = 900 * S, Drag = 2, Sz = sz, C = c, Life = life, Max = life, Floor = true };
        }

        void Sparks(PointF p, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2; float sp = speed * S * (0.3f + (float)rnd.NextDouble());
                parts.Add(new Part { T = 0, X = p.X, Y = p.Y, VX = (float)Math.Cos(a) * sp, VY = (float)Math.Sin(a) * sp, Grav = 500 * S, Drag = 3.5f, C = c, Life = 0.35f + (float)rnd.NextDouble() * 0.3f, Max = 0.65f });
            }
        }

        void Glows(PointF p, Color c, int n, float speed)
        {
            for (int i = 0; i < n; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2; float sp = speed * S * (float)rnd.NextDouble();
                parts.Add(new Part { T = 5, X = p.X, Y = p.Y, VX = (float)Math.Cos(a) * sp, VY = (float)Math.Sin(a) * sp - 40 * S, Grav = -30 * S, Drag = 2.2f, C = c, Sz = (6 + (float)rnd.NextDouble() * 12) * S, Life = 0.6f + (float)rnd.NextDouble() * 0.8f, Max = 1.4f });
            }
        }

        // ---------------- input ----------------
        protected override void OnMouseMove(MouseEventArgs e) { M = e.Location; }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            M = e.Location;
            if (e.Button == MouseButtons.Right) { SetWeapon((weapon + 1) % 4); return; }
            if (e.Button != MouseButtons.Left || !loaded || exitT0 >= 0) return;
            if (RestoreRect.Contains(e.Location)) { Exit(); return; }
            fireDown = true;
            if (weapon == 0) nextFire = 0;
            if (weapon == 1) laserTick = 0;
            if (weapon == 2) { chargeT0 = U.Now; Snd.Play("charge"); }
            if (weapon == 3) { holeT0 = holeTick = U.Now; holeP = M; Snd.Play("hole"); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            fireDown = false;
            if (weapon == 2 && chargeT0 >= 0) FireRail(U.Now);
            if (weapon == 3 && holeT0 >= 0) Collapse(U.Now);
        }
        protected override void OnMouseWheel(MouseEventArgs e) { SetWeapon((weapon + (e.Delta < 0 ? 1 : 3)) % 4); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Exit();
            else if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) SetWeapon(0);
            else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) SetWeapon(1);
            else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) SetWeapon(2);
            else if (e.KeyCode == Keys.D4 || e.KeyCode == Keys.NumPad4) SetWeapon(3);
        }
        void SetWeapon(int w) { if (w == weapon) return; weapon = w; chargeT0 = -1; if (holeT0 >= 0) { holeT0 = -1; Snd.Stop("hole"); } Snd.Play("tick"); }

        RectangleF RestoreRect { get { float m = 34 * S; return new RectangleF(scr.Width - m - 440 * S, scr.Height - m - 22 * S, 440 * S + m, 22 * S + m); } }
        protected override void OnDeactivate(EventArgs e) // never strand a full-screen overlay behind an Alt+Tab
        {
            base.OnDeactivate(e);
            if (U.Now - t0 > 1.0) Exit(); else BeginInvoke(new Action(Activate)); // focus can flicker while the overlay opens
        }

        public void Exit() { if (exitT0 < 0) { exitT0 = U.Now; fireDown = false; chargeT0 = -1; if (holeT0 >= 0) { holeT0 = -1; Snd.Stop("hole"); } Snd.Play("whoosh"); } }

        void Done()
        {
            if (finished) return;
            cr.pet.Scared = false;
            finished = true; Application.Idle -= Loop; N.timeEndPeriod(1); Cursor.Show();
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (snap != null) snap.Dispose(); if (world != null) world.Dispose(); if (under != null) under.Dispose();
                if (worldDC != IntPtr.Zero) { N.DeleteDC(worldDC); N.DeleteObject(worldH); worldDC = IntPtr.Zero; }
                if (backDC != IntPtr.Zero) { back.Dispose(); N.DeleteDC(backDC); N.DeleteObject(backH); N.DeleteDC(snapDC); N.DeleteObject(snapH); backDC = IntPtr.Zero; }
                foreach (var t in targets) { t.Img.Dispose(); t.White.Dispose(); if (t.LabelBmp != null) t.LabelBmp.Dispose(); }
                foreach (var p in parts) if (p.Bmp != null) p.Bmp.Dispose();
                targets.Clear(); parts.Clear();
            }
            base.Dispose(disposing);
        }

        // ---------------- render ----------------
        // Own 32-bit DIB back buffer: full-screen layers move with GDI BitBlt (memcpy speed), effects with GDI+.
        IntPtr backDC, backH, snapDC, snapH; Bitmap back;
        const int SRC = 0x00CC0020;

        void EnsureBuffers()
        {
            if (backDC != IntPtr.Zero) return;
            int w = scr.Width, h = scr.Height;
            back = U.Dib(w, h, out backDC, out backH);
            snapH = snap.GetHbitmap(); snapDC = N.CreateCompatibleDC(IntPtr.Zero); N.SelectObject(snapDC, snapH);
        }

        void Present() { IntPtr dc = N.GetDC(Handle); N.BitBlt(dc, 0, 0, scr.Width, scr.Height, backDC, 0, 0, SRC); N.ReleaseDC(Handle, dc); }
        protected override void OnPaint(PaintEventArgs e) { if (backDC != IntPtr.Zero) Present(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        public Bitmap Frame(double now)
        {
            EnsureBuffers();
            int w = scr.Width, h = scr.Height; U.Mk(15);
            if (!loaded || worldDC == IntPtr.Zero)
            {
                N.BitBlt(backDC, 0, 0, w, h, snapDC, 0, 0, SRC); N.GdiFlush();
                double fl = U.Clamp01(1 - (now - t0) / 0.2);
                using (var g = Graphics.FromImage(back))
                using (var br = new SolidBrush(Color.FromArgb((int)(130 * fl + 40), (int)(255 * fl), (int)(255 * fl), (int)(255 * fl)))) g.FillRectangle(br, 0, 0, w, h);
                return back;
            }
            int sx = (int)shX, sy = (int)shY;
            N.BitBlt(backDC, sx, sy, w, h, worldDC, 0, 0, SRC);
            if (sx > 0) N.BitBlt(backDC, 0, 0, sx, h, IntPtr.Zero, 0, 0, 0x42); // BLACKNESS on the exposed edge
            if (sx < 0) N.BitBlt(backDC, w + sx, 0, -sx, h, IntPtr.Zero, 0, 0, 0x42);
            if (sy > 0) N.BitBlt(backDC, 0, 0, w, sy, IntPtr.Zero, 0, 0, 0x42);
            if (sy < 0) N.BitBlt(backDC, 0, h + sy, w, -sy, IntPtr.Zero, 0, 0, 0x42);
            N.GdiFlush(); U.Mk(0);
            using (var g = Graphics.FromImage(back))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                var st = g.Save();
                g.TranslateTransform(shX, shY);
                DrawTargets(g, now); U.Mk(1);
                DrawParts(g, now); U.Mk(2);
                DrawWeapons(g, now); U.Mk(3);
                g.Restore(st);
                DrawHud(g, now); U.Mk(4);
                DrawCursor(g, now); U.Mk(5);
            }

            // reveal / restore: a ring of light that opens out from the crystal
            double rv = exitT0 >= 0 ? 1 - U.Clamp01((now - exitT0) / 0.5) : U.Clamp01((now - revealT0) / 0.8);
            if (rv < 1)
            {
                double far = 0; foreach (var c in new[] { new PointF(0, 0), new PointF(w, 0), new PointF(0, h), new PointF(w, h) }) far = Math.Max(far, Math.Sqrt((c.X - E.X) * (c.X - E.X) + (c.Y - E.Y) * (c.Y - E.Y)));
                float r = (float)(far * U.InOut(rv)) + 1;
                IntPtr all = N.CreateRectRgn(0, 0, w, h), hole = N.CreateEllipticRgn((int)(E.X - r), (int)(E.Y - r), (int)(E.X + r), (int)(E.Y + r));
                N.CombineRgn(all, all, hole, 4); // RGN_DIFF
                N.SelectClipRgn(backDC, all);
                N.BitBlt(backDC, 0, 0, w, h, snapDC, 0, 0, SRC);
                N.SelectClipRgn(backDC, IntPtr.Zero); N.DeleteObject(all); N.DeleteObject(hole); N.GdiFlush();
                using (var g = Graphics.FromImage(back))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(50, 140, 170, 255), 22 * S)) g.DrawEllipse(pen, E.X - r, E.Y - r, r * 2, r * 2);
                    using (var pen = new Pen(Color.FromArgb(120, 200, 220, 255), 6 * S)) g.DrawEllipse(pen, E.X - r, E.Y - r, r * 2, r * 2);
                    using (var pen = new Pen(Color.FromArgb(235, 255, 255, 255), 1.4f * S)) g.DrawEllipse(pen, E.X - r, E.Y - r, r * 2, r * 2);
                }
            }
            return back;
        }
        void DrawTargets(Graphics g, double now)
        {
            bool live = Revealed(now);
            using (var dark = new Pen(Color.FromArgb(170, 0, 0, 8), 2.4f * S) { LineJoin = LineJoin.Round })
            using (var light = new Pen(Color.FromArgb(230, 245, 250, 255), 0.9f * S) { LineJoin = LineJoin.Round })
            using (var pen = new Pen(Color.White, 1.2f * S))
                foreach (var t in targets)
                {
                    var c = t.C;
                    if (t.Dead)
                    {
                        double age = now - t.DeadT; float a = (float)(0.5 * U.Clamp01(age / 0.6));
                        pen.Color = U.A(Color.FromArgb(170, 205, 255), a * 0.6); Brackets(g, pen, t.Icon, 7 * S);
                        U.Glow(g, Color.FromArgb(170, 205, 255), c.X, c.Y, t.Icon.Width * 0.6f, 0.08 + 0.05 * Math.Sin(now * 2 + t.Phase));
                        continue;
                    }
                    double born = now - t.BornT; float k = born < 1.2 ? (float)U.Spr(born / 0.7, 6, 10) : 1;
                    if (born < 0.6) U.Glow(g, Color.FromArgb(170, 205, 255), c.X, c.Y, t.Icon.Width * 1.2f, 1 - born / 0.6);
                    float bob = live && !U.Calm ? (float)Math.Sin(now * 1.7 + t.Phase) * 1.6f * S : 0;
                    float tr = t.Dmg >= 2 ? (float)(rnd.NextDouble() - 0.5) * 2.2f * S : 0;
                    if (t.LabelBmp != null) U.DrawAlpha(g, t.LabelBmp, new RectangleF(t.Label.X - 1, t.Label.Y - 1, t.LabelBmp.Width, t.LabelBmp.Height), k);
                    float sc = (float)t.Punch.X * k;
                    if (sc <= 0.01f) continue;
                    float lx = 0, ly = 0;
                    if (holeT0 >= 0)
                    {
                        double hk = HoleK(now); float pr = PullR(hk), dx = holeP.X - c.X, dy = holeP.Y - c.Y, dd = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dd < pr) { float f = (1 - dd / pr) * (1 - dd / pr) * (float)hk; lx = dx * 0.3f * f; ly = dy * 0.3f * f; sc *= 1 - 0.3f * f; }
                    }
                    var s2 = g.Save();
                    g.TranslateTransform(c.X + tr + lx, c.Y + bob + ly);
                    g.ScaleTransform(sc, sc);
                    var dst = new RectangleF(-t.Icon.Width / 2, -t.Icon.Height / 2, t.Icon.Width, t.Icon.Height);
                    g.DrawImage(t.Img, dst);
                    foreach (var ck in t.Cracks) { g.DrawLines(dark, ck); g.DrawLines(light, ck); }
                    double fl = 1 - (now - t.FlashT) / 0.12;
                    if (fl > 0) U.DrawAlpha(g, t.White, dst, fl * 0.85);
                    g.Restore(s2);
                    if (live && t.Hit.Contains(M) && exitT0 < 0) { pen.Color = U.A(WCol[weapon], 0.75); Brackets(g, pen, RectangleF.Inflate(t.Hit, 3 * S, 3 * S), 8 * S); }
                }
        }

        static void Brackets(Graphics g, Pen p, RectangleF r, float l)
        {
            g.DrawLines(p, new[] { new PointF(r.Left, r.Top + l), new PointF(r.Left, r.Top), new PointF(r.Left + l, r.Top) });
            g.DrawLines(p, new[] { new PointF(r.Right - l, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Top + l) });
            g.DrawLines(p, new[] { new PointF(r.Left, r.Bottom - l), new PointF(r.Left, r.Bottom), new PointF(r.Left + l, r.Bottom) });
            g.DrawLines(p, new[] { new PointF(r.Right - l, r.Bottom), new PointF(r.Right, r.Bottom), new PointF(r.Right, r.Bottom - l) });
        }

        void DrawParts(Graphics g, double now)
        {
            using (var br = new SolidBrush(Color.White))
            using (var pen = new Pen(Color.White, 1.3f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                foreach (var p in parts)
                {
                    float a = Math.Min(1, p.Life / Math.Min(p.Max, 0.6f));
                    switch (p.T)
                    {
                        case 0:
                            pen.Color = U.A(p.C, a); pen.Width = 1.4f * S;
                            g.DrawLine(pen, p.X, p.Y, p.X - p.VX * 0.018f, p.Y - p.VY * 0.018f); break;
                        case 1:
                            br.Color = U.A(p.C, a); g.FillRectangle(br, p.X - p.Sz / 2, p.Y - p.Sz / 2, p.Sz, p.Sz); break;
                        case 2:
                            {
                                var s2 = g.Save(); g.TranslateTransform(p.X, p.Y); g.RotateTransform(p.A);
                                var r = new RectangleF(-p.Bmp.Width / 2f, -p.Bmp.Height / 2f, p.Bmp.Width, p.Bmp.Height);
                                U.DrawAlpha(g, p.Bmp, r, a); g.Restore(s2); break;
                            }
                        case 3:
                            {
                                var s2 = g.Save(); g.TranslateTransform(p.X, p.Y); g.RotateTransform((float)Math.Sin(now * 5 * p.Seed + p.Seed * 7) * 70 + p.A);
                                g.ScaleTransform(Math.Max(0.12f, Math.Abs((float)Math.Cos(now * 6 * p.Seed + p.Seed))), 1);
                                br.Color = U.A(p.C, a); g.FillRectangle(br, -p.Sz / 2, -p.Sz * 0.65f, p.Sz, p.Sz * 1.3f);
                                br.Color = U.A(Color.FromArgb(150, 155, 165), a * 0.6);
                                for (int l = 0; l < 3; l++) g.FillRectangle(br, -p.Sz * 0.35f, -p.Sz * 0.4f + l * p.Sz * 0.3f, p.Sz * 0.7f, 0.8f * S);
                                g.Restore(s2); break;
                            }
                        case 4:
                            {
                                var s2 = g.Save(); g.TranslateTransform(p.X, p.Y); g.RotateTransform(p.A);
                                br.Color = U.A(p.C, a); g.FillPolygon(br, p.Poly); g.Restore(s2); break;
                            }
                        case 5: U.Glow(g, p.C, p.X, p.Y, p.Sz, a * 0.7); break;
                    }
                }
                foreach (var w in waves)
                {
                    float a = w.Life / w.Max;
                    pen.Color = U.A(w.C, a * 0.85); pen.Width = w.W * (0.4f + a);
                    g.DrawEllipse(pen, w.X - w.R, w.Y - w.R, w.R * 2, w.R * 2);
                }
            }
        }

        void DrawWeapons(Graphics g, double now)
        {
            // pixel bolts
            using (var br = new SolidBrush(WCol[0]))
                foreach (var b in bolts)
                {
                    float d = (float)Math.Sqrt(b.VX * b.VX + b.VY * b.VY) + 0.01f, ux = b.VX / d, uy = b.VY / d;
                    for (int i = 0; i < 6; i++)
                    {
                        float s = (4.5f - i * 0.6f) * S; br.Color = U.A(i == 0 ? Color.White : WCol[0], 1 - i / 6f);
                        g.FillRectangle(br, b.X - ux * i * 9 * S - s / 2, b.Y - uy * i * 9 * S - s / 2, s, s);
                    }
                    U.Glow(g, WCol[0], b.X, b.Y, 14 * S, 0.6);
                }

            // laser arc
            if (fireDown && weapon == 1 && loaded && exitT0 < 0)
            {
                float dx = M.X - E.X, dy = M.Y - E.Y, d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f, nx = -dy / d, ny = dx / d;
                float bend = (float)(Math.Sin(now * 9) * 0.05 + 0.08) * Math.Min(d, 900 * S);
                var c1 = new PointF(E.X + dx * 0.3f + nx * bend, E.Y + dy * 0.3f + ny * bend);
                var c2 = new PointF(E.X + dx * 0.7f + nx * bend * 0.5f + (float)(rnd.NextDouble() - 0.5) * 10 * S, E.Y + dy * 0.7f + ny * bend * 0.5f + (float)(rnd.NextDouble() - 0.5) * 10 * S);
                using (var pen = new Pen(Color.FromArgb(40, WCol[1]), 14 * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawBezier(pen, E, c1, c2, M);
                using (var pen = new Pen(Color.FromArgb(130, WCol[1]), 4.5f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawBezier(pen, E, c1, c2, M);
                using (var pen = new Pen(Color.FromArgb(250, 250, 245, 255), 1.4f * S)) g.DrawBezier(pen, E, c1, c2, M);
                U.Glow(g, WCol[1], M.X, M.Y, 34 * S, 0.9); U.Glow(g, Color.White, M.X, M.Y, 10 * S, 1);
                if (rnd.Next(2) == 0) Sparks(M, WCol[1], 1, 360);
            }

            // rail beam
            double ra = now - railT;
            if (ra < 0.4)
            {
                float a = (float)(1 - ra / 0.4);
                using (var pen = new Pen(Color.FromArgb((int)(60 * a), WCol[2]), (26 * a + 4) * S)) g.DrawLine(pen, railA, railB);
                using (var pen = new Pen(Color.FromArgb((int)(110 * a), 90, 220, 255), 2 * S)) g.DrawLine(pen, railA.X - 3 * S, railA.Y, railB.X - 3 * S, railB.Y);
                using (var pen = new Pen(Color.FromArgb((int)(110 * a), 255, 110, 210), 2 * S)) g.DrawLine(pen, railA.X + 3 * S, railA.Y, railB.X + 3 * S, railB.Y);
                using (var pen = new Pen(Color.FromArgb((int)(255 * a), 255, 252, 240), (5 * a + 1.2f) * S)) g.DrawLine(pen, railA, railB);
            }

            // black hole: a disc of nothing, a photon ring, and a tilted accretion disc
            if (holeT0 >= 0)
            {
                double k = HoleK(now); float r = (float)(8 + 18 * k) * S, x = holeP.X, y = holeP.Y;
                U.Glow(g, WCol[3], x, y, r * 6, 0.3 + 0.35 * k);
                U.Glow(g, WCol[1], x, y, r * 3.2f, 0.45);
                U.Glow(g, Color.Black, x, y, r * 2.6f, 0.85); // the shadow: light near the hole is already gone
                using (var pen = new Pen(Color.FromArgb(28, WCol[3]), 1 * S) { DashStyle = DashStyle.Dot }) { float pr = PullR(k); g.DrawEllipse(pen, x - pr, y - pr, pr * 2, pr * 2); }
                var st = g.Save(); g.TranslateTransform(x, y); g.RotateTransform(-16);
                using (var pen = new Pen(Color.White, 1.6f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float rx = r * (1.7f + i * 0.5f), ry = rx * 0.34f;
                        pen.Color = Color.FromArgb((int)((200 - i * 30) * (0.5 + 0.5 * k)), i < 2 ? Color.FromArgb(255, 236, 210) : WCol[3]); pen.Width = (3.2f - i * 0.45f) * S;
                        g.DrawArc(pen, -rx, -ry, rx * 2, ry * 2, (float)(now * (260 - i * 40) % 360), 210);
                    }
                    using (var br = new SolidBrush(Color.Black)) g.FillEllipse(br, -r, -r, r * 2, r * 2);
                    pen.Color = Color.FromArgb(235, 255, 244, 228); pen.Width = 1.3f * S; g.DrawEllipse(pen, -r * 1.06f, -r * 1.06f, r * 2.12f, r * 2.12f);
                    pen.Color = Color.FromArgb(220, 255, 214, 170); pen.Width = 2.4f * S; float fx = r * 1.8f; g.DrawArc(pen, -fx, -fx * 0.34f, fx * 2, fx * 0.68f, 15, 150); // the near side of the disc crosses in front
                }
                g.Restore(st);
            }
            double cl = now - collapseT;
            if (cl < 0.35) U.Glow(g, Color.White, collapseP.X, collapseP.Y, (float)(60 + 500 * cl) * S, 1 - cl / 0.35);

            // emitter: the crystal, shrunk to a weapon
            float er = 17 * S * (1 + (float)kick * 0.12f);
            U.Glow(g, Color.FromArgb(120, 150, 255), E.X, E.Y, er * 4, 0.55 + 0.25 * Math.Min(1, kick));
            using (var p = new GraphicsPath())
            {
                var pts = new PointF[6];
                for (int i = 0; i < 6; i++) { double an = now * 0.7 + i * Math.PI / 3; pts[i] = new PointF(E.X + (float)Math.Cos(an) * er, E.Y + (float)Math.Sin(an) * er * 1.08f); }
                p.AddClosedCurve(pts, 0.25f);
                using (var pg = new PathGradientBrush(p)) { pg.CenterColor = Color.FromArgb(240, 60, 70, 110); pg.SurroundColors = new[] { Color.FromArgb(245, 10, 12, 22) }; g.FillPath(pg, p); }
                using (var pen = new Pen(Color.FromArgb(150, 220, 235, 255), 0.8f * S)) { g.DrawPath(pen, p); for (int i = 0; i < 6; i += 2) g.DrawLine(pen, E.X, E.Y, pts[i].X, pts[i].Y); }
            }
            if (weapon == 2 && chargeT0 >= 0) { double k = Charge(now); U.Glow(g, WCol[2], E.X, E.Y, er * (2 + 3 * (float)k), k); }
            cr.pet.DrawAt(g, E.X, E.Y - er * 0.85f, now, 1);
        }

        void DrawHud(Graphics g, double now)
        {
            int w = scr.Width, h = scr.Height;
            double a = U.Clamp01((now - revealT0 - 0.4) / 0.6) * (exitT0 >= 0 ? 1 - U.Clamp01((now - exitT0) / 0.25) : 1);
            if (!loaded || a <= 0) return;
            Color ice = Color.FromArgb(170, 205, 255), champ = Color.FromArgb(255, 214, 160);
            float m = 34 * S;
            U.Spaced(g, "OBSIDOT", U.F("Bahnschrift SemiLight", 12 * S), U.A(Color.White, a * 0.9), w / 2f, m - 6 * S, 5 * S, 1);
            U.Spaced(g, "DESKTOP ASSAULT", U.F("Bahnschrift SemiLight", 9 * S), U.A(ice, a * 0.55), w / 2f, m + 14 * S, 3 * S, 1);

            string n = ((int)Math.Round(shown)).ToString("N0");
            var f = U.F("Bahnschrift Light", 42 * S); var typo = StringFormat.GenericTypographic;
            var sz = g.MeasureString(n, f, 2000, typo);
            using (var br = new SolidBrush(U.A(Color.White, a * 0.92))) g.DrawString(n, f, br, w - m - sz.Width, m - 8 * S, typo);
            U.Spaced(g, "FRAGMENTS", U.F("Bahnschrift SemiLight", 9 * S), U.A(ice, a * 0.55), w - m, m + 42 * S, 3 * S, 2);
            int alive = 0; foreach (var t in targets) if (!t.Dead) alive++;
            if (targets.Count > 0) U.Spaced(g, alive + " TARGETS", U.F("Bahnschrift SemiLight", 9 * S), U.A(champ, a * 0.55), w - m, m + 58 * S, 3 * S, 2);

            // weapon dock
            float sw = 165 * S, dh = 50 * S, x0 = w / 2f - sw * 2f, y0 = h - m - dh;
            using (var p = U.Chamfer(x0 - 8 * S, y0, sw * 4 + 16 * S, dh, 10 * S))
            {
                using (var br = new SolidBrush(Color.FromArgb((int)(200 * a), 9, 10, 18))) g.FillPath(br, p);
                using (var pen = new Pen(Color.FromArgb((int)(60 * a), 220, 230, 255), 1 * S)) g.DrawPath(pen, p);
            }
            float sx = x0 + (float)slot.X * sw;
            using (var br = new SolidBrush(Color.FromArgb((int)(40 * a), WCol[weapon]))) g.FillRectangle(br, sx + 6 * S, y0 + 6 * S, sw - 12 * S, dh - 12 * S);
            using (var pen = new Pen(U.A(WCol[weapon], a), 2 * S)) g.DrawLine(pen, sx + 30 * S, y0 + dh - 6 * S, sx + sw - 30 * S, y0 + dh - 6 * S);
            for (int i = 0; i < 4; i++)
            {
                bool on = i == weapon; float cx = x0 + i * sw + sw / 2;
                U.Spaced(g, (i + 1).ToString(), U.F("Bahnschrift SemiLight", 9 * S), U.A(WCol[i], a * (on ? 0.9 : 0.35)), cx, y0 + 9 * S, 0, 1);
                U.Spaced(g, WName[i], U.F("Bahnschrift SemiLight", 10 * S), U.A(Color.White, a * (on ? 0.95 : 0.35)), cx, y0 + 23 * S, 2.4f * S, 1);
            }
            U.Spaced(g, "ESC  ·  ALT+K  ·  CLICK HERE TO RESTORE DESKTOP", U.F("Bahnschrift SemiLight", 9 * S), U.A(Color.White, a * (RestoreRect.Contains(M) ? 0.95 : 0.45)), w - m, h - m - 10 * S, 2.5f * S, 2);
            U.Spaced(g, "WHEEL  ·  SWITCH", U.F("Bahnschrift SemiLight", 9 * S), U.A(Color.White, a * 0.4), m, h - m - 10 * S, 2.5f * S, 0);

            if (clearT >= 0)
            {
                double c = now - clearT; double ca = U.Clamp01((c - 0.6) / 0.6) * U.Clamp01((3.4 - c) / 0.4);
                if (ca > 0)
                {
                    U.Glow(g, ice, w / 2f, h / 2f, 260 * S, ca * 0.25);
                    U.Spaced(g, "CLEAN SLATE", U.F("Bahnschrift Light", 40 * S), U.A(Color.White, ca), w / 2f, h / 2f - 34 * S, 16 * S * (float)(1 + 0.3 * (1 - U.OutCubic((c - 0.6) / 1.5))), 1);
                    U.Spaced(g, "the desktop will remember itself", U.F("Sitka Display", 17 * S, FontStyle.Italic), U.A(ice, ca * 0.75), w / 2f, h / 2f + 22 * S, 0.5f * S, 1);
                }
            }
        }

        void DrawCursor(Graphics g, double now)
        {
            if (!loaded) return;
            Color c = WCol[weapon]; float x = M.X, y = M.Y;
            using (var pen = new Pen(Color.FromArgb(230, c), 1.4f * S))
            using (var br = new SolidBrush(Color.FromArgb(240, c)))
            {
                switch (weapon)
                {
                    case 0:
                        float o = 9 * S + (float)kick * 3 * S, q = 3 * S;
                        foreach (var d in new[] { new PointF(-o, 0), new PointF(o, 0), new PointF(0, -o), new PointF(0, o) }) g.FillRectangle(br, x + d.X - q / 2, y + d.Y - q / 2, q, q);
                        g.FillRectangle(br, x - 1 * S, y - 1 * S, 2 * S, 2 * S); break;
                    case 1:
                        float r = 12 * S; g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
                        for (int i = 0; i < 3; i++) g.DrawArc(pen, x - r - 5 * S, y - r - 5 * S, (r + 5 * S) * 2, (r + 5 * S) * 2, (float)(now * 120) + i * 120, 30);
                        g.FillEllipse(br, x - 1.5f * S, y - 1.5f * S, 3 * S, 3 * S); break;
                    case 2:
                        float s = 13 * S; double k = Charge(now);
                        g.DrawPolygon(pen, new[] { new PointF(x, y - s), new PointF(x + s, y), new PointF(x, y + s), new PointF(x - s, y) });
                        if (k > 0)
                        {
                            float rr = (34 - 20 * (float)k) * S; pen.Width = 2 * S; pen.Color = Color.FromArgb((int)(120 + 135 * k), c);
                            g.DrawArc(pen, x - rr, y - rr, rr * 2, rr * 2, -90, (float)(360 * k));
                            if (k >= 1) U.Glow(g, c, x, y, 26 * S, 0.5 + 0.3 * Math.Sin(now * 30));
                        }
                        g.FillEllipse(br, x - 1.5f * S, y - 1.5f * S, 3 * S, 3 * S); break;
                    case 3:
                        if (holeT0 >= 0) break; // the hole itself is the cursor
                        float hr = 11 * S; g.DrawEllipse(pen, x - hr, y - hr, hr * 2, hr * 2);
                        for (int i = 0; i < 4; i++) { double an = now * 1.5 + i * Math.PI / 2; g.DrawLine(pen, x + (float)Math.Cos(an) * hr * 1.7f, y + (float)Math.Sin(an) * hr * 1.7f, x + (float)Math.Cos(an) * hr * 1.25f, y + (float)Math.Sin(an) * hr * 1.25f); }
                        g.FillEllipse(br, x - 3 * S, y - 3 * S, 6 * S, 6 * S); break;
                }
            }
        }
    }
}
