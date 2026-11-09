using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Obsidot
{
    // `Obsidot.exe --shots <dir>`: renders key states to PNG (design review without a screen recorder) and times them.
    static class Shots
    {
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            var c = new Crystal();
            int W = c.W; float C = W / 2f;
            Func<Bitmap> bg = () =>
            {
                var b = new Bitmap(W, W + (int)(300 * U.S));
                using (var g = Graphics.FromImage(b))
                using (var lg = new LinearGradientBrush(new Rectangle(0, 0, b.Width, b.Height), Color.FromArgb(38, 52, 92), Color.FromArgb(150, 120, 140), 60f)) g.FillRectangle(lg, 0, 0, b.Width, b.Height);
                return b;
            };
            Action<string, double> save = (name, now) =>
            {
                c.Render(now);
                using (var b = bg()) { using (var g = Graphics.FromImage(b)) g.DrawImage(SurfaceOf(c), 0, 0, W, W); b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png); }
            };
            Action<double> run = secs => { double t = U.Now; for (int i = 0; i < secs * 60; i++) c.Step(1 / 60.0, t + i / 60.0); };

            c.birth0 = U.Now; save("birth_0.15", U.Now + 0.15); save("birth_0.45", U.Now + 0.45); save("birth_0.9", U.Now + 0.9);
            c.birth0 = U.Now - 10; run(2); save("idle", U.Now);
            var sw = Stopwatch.StartNew(); for (int i = 0; i < 60; i++) c.Render(U.Now); Console.WriteLine("crystal render ms: " + sw.Elapsed.TotalMilliseconds / 60);
            for (int rep = 0; rep < 3; rep++) { sw.Restart(); for (int i = 0; i < 60; i++) c.Render(U.Now); Console.WriteLine("crystal render ms: " + sw.Elapsed.TotalMilliseconds / 60); }
            c.hover = true; c.mouse = new PointF(C + 90 * U.S, C - 70 * U.S); run(1.5); save("hover", U.Now);
            c.menuOpen = true; c.ShotPoint(2); run(1); save("menu", U.Now);
            c.ShotPoint(5); run(0.6); save("menu_pad", U.Now);
            using (var pad = new Scratch(new RectangleF(100, 100, 200, 200), "Call Riya about the deck at 4\r\n\r\nIdeas\r\n- orbit switch\r\n- day ring"))
            { pad.CreateControl(); using (var b = new Bitmap(pad.Width, pad.Height)) { pad.DrawToBitmap(b, new Rectangle(0, 0, pad.Width, pad.Height)); b.Save(Path.Combine(dir, "scratchpad.png"), ImageFormat.Png); } }
            c.menuOpen = false; c.menuHover = -1; c.hover = false;
            c.ShotTimer(1); run(1); save("timer_set", U.Now);
            c.ShotTimer(2); run(1); save("timer_run", U.Now);
            c.ShotTimer(3); run(0.2); save("timer_done", U.Now);
            c.ShotTimer(0); run(1.5);
            c.ShotMusic(); save("music", U.Now);
            sw.Restart(); for (int i = 0; i < 60; i++) c.Render(U.Now); Console.WriteLine("crystal render with music ms: " + sw.Elapsed.TotalMilliseconds / 60);
            c.ShotBands(0); U.Prof = true; sw.Restart(); for (int i = 0; i < 60; i++) c.Render(U.Now); Console.WriteLine("  liquid only ms: " + sw.Elapsed.TotalMilliseconds / 60 + "  " + U.Dump(60)); U.Prof = false;
            c.musicK0();
            c.pet.Shot(0, U.Now); run(0.3); save("pet", U.Now);
            c.pet.Shot(1, U.Now); run(0.3); save("pet_sleep", U.Now);
            c.pet.Shot(2, U.Now); save("pet_happy", U.Now);
            c.Greet("GOOD MORNING", U.Now - 1.1); save("greet", U.Now);
            c.Greet("x", U.Now - 100);
            {
                var lit = new System.Collections.Generic.List<bool>(); var tg = c.EntranceTargets(lit);
                var scr = System.Windows.Forms.Screen.PrimaryScreen.Bounds; double t0 = U.Now;
                using (var en = new Entrance(scr, tg, lit, t0)) { en.Render(t0 + 0.75).Save(Path.Combine(dir, "entrance.png"), ImageFormat.Png); }
            }
            using (var ro = new Roamer(c.pet, "NEW FILE", "PROJECT-NOTES.PDF", null, 25)) { var rb = ro.Render(U.Now + 1); rb.Save(Path.Combine(dir, "roamer.png"), ImageFormat.Png); }
            c.pet.Roaming = false; c.pet.Shot(0, U.Now);
            c.mode = 1; c.hoops.Reset(); run(1.2); save("hoops", U.Now);
            c.mode = 0; run(1.5);
            // edge check: near the top, switching to hoops must slide the bigger puck back on screen
            var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            c.cy = wa.Top + c.rS.X * 1.15; c.mode = 1; run(2.5);
            Console.WriteLine("edge: top gap after hoops = " + (c.cy - c.rS.X - wa.Top).ToString("0") + " px (must be >= 0)");
            c.mode = 0; run(1.5);

            // card states
            var card = c.card;
            foreach (var a in new[] { 0.15, 0.45, 0.8, 1.6 }) card.Shot(a).Save(Path.Combine(dir, "card_" + a + ".png"), ImageFormat.Png);
            card.Shot(1.6, 2).Save(Path.Combine(dir, "card_thought.png"), ImageFormat.Png);
            card.Shot(1.6, 26).Save(Path.Combine(dir, "card_fact.png"), ImageFormat.Png);

            c.Greet("x", U.Now - 100);

            // game frame
            var game = new Assault(c, new PointF(1800 * U.S / 1.5f, 300));
            game.LoadWorld();
            game.Demo();
            double gt = U.Now + 2;
            for (int i = 0; i < 20; i++) game.Step(1 / 60.0, gt + i / 60.0);
            game.Frame(gt + 0.4).Save(Path.Combine(dir, "game.png"), ImageFormat.Png);
            Console.WriteLine("targets: " + game.TargetCount);
            game.DemoHole(gt + 0.4);
            for (int i = 0; i < 30; i++) game.Step(1 / 60.0, gt + 0.4 + i / 60.0);
            game.Frame(gt + 0.9).Save(Path.Combine(dir, "game_hole.png"), ImageFormat.Png);
        }

        // `Obsidot.exe --anim <dir>`: transparent frame sequences for the README's animated demos (made into APNGs by a script)
        public static void Anim(string dir)
        {
            var c = new Crystal(); int W = c.W; float C = W / 2f, S = U.S;
            c.birth0 = U.Now - 100; c.Greet("x", U.Now - 100); c.pet.Shot(0, U.Now);
            int n = 0;
            Action<string, int, double, float, int, Action<int, double>> scene = (name, frames, fps, cropS, outPx, each) =>
            {
                string d = Path.Combine(dir, name); Directory.CreateDirectory(d);
                int sd = (int)(cropS * S); double t0 = U.Now + 1000 + (n++) * 200;   // each scene on its own private timeline
                c.birth0 = t0 - 100;
                for (int i = 0; i < frames; i++)
                {
                    double t = t0 + i / fps;
                    each(i, t);
                    c.Step(1 / fps, t); c.Render(t);
                    using (var b = new Bitmap(outPx, outPx, PixelFormat.Format32bppArgb))
                    using (var g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.Transparent); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(SurfaceOf(c), new Rectangle(0, 0, outPx, outPx), new Rectangle((int)(C - sd / 2), (int)(C - sd / 2), sd, sd), GraphicsUnit.Pixel);
                        b.Save(Path.Combine(d, i.ToString("000") + ".png"), ImageFormat.Png);
                    }
                }
            };
            // the clock: the cursor drifts over it, a tap ripples, a minute wipes over
            scene("clock", 84, 24, 300, 340, (i, t) =>
            {
                double a = i / 84.0 * Math.PI * 2; c.hover = true; c.mouse = new PointF(C + (float)Math.Cos(a) * 60 * S, C + (float)Math.Sin(a * 2) * 34 * S);
                if (i == 18) c.ShotRipple(t, c.mouse);
                if (i == 44) c.ShotWipe(t);
            });
            c.hover = false;
            // music: rays out of the rim, the edge going liquid on the kick. White rays for dark pages, dark rays for light pages.
            scene("music-dark", 72, 24, 390, 380, (i, t) => c.ShotMusicAt(t, i));
            var ray = Theme.T.Ray; Theme.T.Ray = Color.FromArgb(46, 46, 52); c.ShotResetDots();
            scene("music-light", 72, 24, 390, 380, (i, t) => c.ShotMusicAt(t, i));
            Theme.T.Ray = ray; c.ShotResetDots(); c.musicK0(); c.ShotBands(0);
            // the menu: it opens, the red pointer turns round the buttons
            scene("menu", 96, 24, 420, 360, (i, t) =>
            {
                c.hover = true; if (i == 2) { c.menuOpen = true; c.ShotPoint(0); }
                if (i > 14 && i % 12 == 3) c.ShotSwing(((i - 15) / 12 + 1) % 6);
                if (i == 88) c.menuOpen = false;
            });
            c.hover = false; c.menuOpen = false; c.menuHover = -1;
            // the pet: watches a circling cursor, blinks, jumps for a heart
            scene("pet", 84, 24, 300, 300, (i, t) =>
            {
                if (i == 0) c.pet.Shot(0, t);
                double a = i / 84.0 * Math.PI * 2; c.pet.LookAt((float)Math.Cos(a) * 200 * S, (float)Math.Sin(a) * 120 * S);
                if (i == 22 || i == 58) c.pet.Poke(t);
            });
            // the focus timer: winding the arc to 25 minutes
            scene("focus", 72, 24, 300, 300, (i, t) => { if (i % 12 == 0 && i <= 48) c.ShotFocus(5 + i / 12 * 5, t); else if (i > 48) c.ShotFocus(25, t); });
            c.ShotTimer(0); Settings.Focus = 25; Settings.Name = "Vraj"; // the demo greets its author
            // a greeting, by name
            scene("greeting", 100, 20, 300, 300, (i, t) => { if (i == 0) c.Greet("GOOD MORNING VRAJ", t + 0.2); });
            c.Greet("x", U.Now - 100);
            // Vector Hoops: aim, throw, bounce
            c.mode = 1; c.hoops.Reset();
            scene("hoops", 96, 24, 340, 340, (i, t) =>
            {
                if (i == 10 || i == 54) c.hoops.Press(0, 20 * S);
                if ((i > 10 && i < 22) || (i > 54 && i < 66)) c.hoops.Drag(-((i % 44) - 10) * 1.6f * S, 20 * S + ((i % 44) - 10) * 4.2f * S);
                if (i == 22 || i == 66) c.hoops.Release();
            });
            c.mode = 0; for (int i = 0; i < 60; i++) c.Step(1 / 60.0, U.Now + 2000 + i / 60.0);
            // the wordmark, in dot lettering: light dots for dark pages, dark dots for light pages
            foreach (var v in new[] { new { n = "wordmark-dark.png", c = Color.White }, new { n = "wordmark-light.png", c = Color.FromArgb(20, 20, 22) } })
            {
                var dm = new Dots(); dm.Text3At("OBSIDOT", 0, 0, 1f);
                float p = 14, dot = 10.5f; int ww = (int)((Dots.W3("OBSIDOT") - 1) * p + dot + 40), hh = (int)(4 * p + dot + 8);
                using (var b = new Bitmap(ww, hh, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Dots.Paint(g, dm, dot / 2 + 2, dot / 2 + 4, p, dot, v.c, Color.FromArgb(232, 36, 32));
                    using (var br = new SolidBrush(Color.FromArgb(232, 36, 32))) g.FillEllipse(br, ww - dot - 4, hh - dot - 6, dot, dot); // the red second
                    b.Save(Path.Combine(dir, v.n), ImageFormat.Png);
                }
            }
            // stills without a backdrop: a thought card, and the pet holding a sign
            c.card.Shot(1.6, 2, true).Save(Path.Combine(dir, "thought-card.png"), ImageFormat.Png);
            using (var ro = new Roamer(c.pet, "NEW FILE", "PROJECT-NOTES.PDF", null, 25)) ro.Render(U.Now + 1).Save(Path.Combine(dir, "pet-sign.png"), ImageFormat.Png);
        }
        // `Obsidot.exe --assault <dir> [real|screen]`: Desktop Assault, every weapon, as frames;
        // on a made-up desktop, with `real` on your wallpaper and icons redrawn, with `screen` on a real screenshot
        public static void AssaultDemo(string dir, string mode, string window)
        {
            bool real = mode == "real" || mode == "screen";
            Directory.CreateDirectory(dir);
            var c = new Crystal(); float S = U.S;
            var scr = System.Windows.Forms.Screen.PrimaryScreen.Bounds; int w = scr.Width, h = scr.Height;
            Bitmap wall = null, screen = null; List<DIcon> icons = null;
            if (!real) screen = Staged(w, h, out wall, out icons);
            else if (mode == "screen")       // you set the screen up, press F9, and the Assault takes its own screenshot, exactly as with Alt+K
            {
                while ((GetAsyncKeyState(0x78) & 0x8000) == 0) System.Threading.Thread.Sleep(30);
                System.Threading.Thread.Sleep(400);
            }
            else       // your wallpaper and your desktop icons, redrawn: open windows never get in the shot
            {
                icons = Desk.Read(scr); wall = Desk.Wallpaper(scr.Size) ?? new Bitmap(w, h);
                screen = new Bitmap(wall);
                using (var g = Graphics.FromImage(screen))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    var f = U.F("Segoe UI", 9 * S);
                    if (window != null) using (var win = new Bitmap(window))   // one window open on top, like a real afternoon
                    {
                        var r = new RectangleF(w * 0.17f, h * 0.09f, w * 0.68f, 0); r.Height = r.Width * win.Height / win.Width + 34 * S;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        for (int k = 6; k >= 1; k--) using (var p = U.Round(r.X - k * 3 * S, r.Y - k * 2 * S, r.Width + k * 6 * S, r.Height + k * 6 * S, 12 * S)) using (var br = new SolidBrush(Color.FromArgb(14, 0, 0, 0))) g.FillPath(br, p);
                        using (var p = U.Round(r.X, r.Y, r.Width, r.Height, 8 * S)) { g.SetClip(p); using (var br = new SolidBrush(Color.FromArgb(30, 30, 34))) g.FillRectangle(br, r); }
                        using (var br = new SolidBrush(Color.FromArgb(170, 170, 178))) g.DrawString("github.com/GalacticVraj/obsidot", U.F("Segoe UI", 9.5f * S), br, r.X + 16 * S, r.Y + 8 * S);
                        using (var br = new SolidBrush(Color.FromArgb(60, 60, 64))) g.DrawString("–   □   ✕", U.F("Segoe UI", 10 * S), br, r.Right - 92 * S, r.Y + 7 * S);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(win, new RectangleF(r.X, r.Y + 34 * S, r.Width, r.Height - 34 * S));
                        g.ResetClip();
                    }
                    foreach (var d in icons)
                    {
                        if (d.Img != null) g.DrawImage(d.Img, Rectangle.Round(d.Icon));
                        var lr = new RectangleF(d.Label.X, d.Label.Y, d.Label.Width, Math.Max(d.Label.Height, 34 * S));
                        using (var sh = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.DrawString(d.Name, f, sh, new RectangleF(lr.X + S, lr.Y + S, lr.Width, lr.Height), fmt);
                        g.DrawString(d.Name, f, Brushes.White, lr, fmt);
                    }
                }
                File.WriteAllLines(Path.Combine(dir, "_icons.txt"), icons.ConvertAll(d => d.Name).ToArray());
            }
            if (screen != null) screen.Save(Path.Combine(dir, "_staged.png"), ImageFormat.Png);
            var game = new Assault(c, new PointF(w * 0.7f, h * 0.3f), screen, wall, icons);
            game.LoadWorld();
            double fps = 15, T0 = U.Now + 3000; game.Begin(T0);
            var cam = real ? new Rectangle(0, 0, w, h) : new Rectangle(0, (int)(h * 0.11f), (int)(w * 0.78f), (int)(w * 0.78f / 1.6f));   // framed on the action, 16:10
            int ow = 800, oh = real ? (int)Math.Round(800.0 * h / w) : 500;
            for (int i = 0; i < 135; i++)
            {
                double now = T0 + i / fps;
                for (int k = 1; k <= 3; k++) { double tt = now - (3 - k) / (fps * 3); game.Script(tt - T0, tt); game.Step(1 / (fps * 3), tt); } // three physics steps a frame
                var f = game.Frame(now);
                using (var b = new Bitmap(ow, oh))
                using (var g = Graphics.FromImage(b))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(f, new Rectangle(0, 0, ow, oh), cam, GraphicsUnit.Pixel);
                    b.Save(Path.Combine(dir, i.ToString("000") + ".png"), ImageFormat.Png);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

        // a night-sky wallpaper, a dark code editor, a taskbar and seven desktop icons, all drawn here
        static Bitmap Staged(int w, int h, out Bitmap wall, out List<DIcon> icons)
        {
            float S = U.S;
            wall = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(wall))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.Black, Color.Black, 90f))
                {
                    lg.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(6, 8, 22), Color.FromArgb(14, 14, 44), Color.FromArgb(36, 18, 66), Color.FromArgb(64, 22, 78) }, Positions = new[] { 0f, 0.4f, 0.75f, 1f } };
                    g.FillRectangle(lg, 0, 0, w, h);
                }
                U.Glow(g, Color.FromArgb(110, 70, 230), w * 0.72f, h * 0.32f, h * 0.42f, 0.55);   // nebulae
                U.Glow(g, Color.FromArgb(230, 60, 150), w * 0.9f, h * 0.62f, h * 0.36f, 0.4);
                U.Glow(g, Color.FromArgb(40, 140, 230), w * 0.22f, h * 0.12f, h * 0.38f, 0.35);
                var rnd = new Random(9);
                using (var br = new SolidBrush(Color.White))
                    for (int i = 0; i < 420; i++) { float s = (float)(rnd.NextDouble() * 2.2 + 0.8); br.Color = Color.FromArgb(rnd.Next(70, 230), 255, 255, 255); g.FillEllipse(br, (float)rnd.NextDouble() * w, (float)rnd.NextDouble() * h * 0.8f, s, s); }
                Color[] ridge = { Color.FromArgb(26, 16, 48), Color.FromArgb(14, 9, 28) };
                for (int L = 0; L < 2; L++)
                {
                    var pts = new List<PointF> { new PointF(0, h) }; float y0 = h * (0.8f + L * 0.06f);
                    for (int x = 0; x <= w; x += w / 30) pts.Add(new PointF(x, y0 - (float)(rnd.NextDouble() * h * 0.07 + Math.Sin(x * 0.003 + L * 2) * h * 0.03)));
                    pts.Add(new PointF(w, h));
                    using (var br = new SolidBrush(ridge[L])) g.FillPolygon(br, pts.ToArray());
                }
            }
            var screen = new Bitmap(wall);
            using (var g = Graphics.FromImage(screen))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                float wx = w * 0.11f, wy = h * 0.17f, ww = w * 0.47f, wh = h * 0.63f, bar = 40 * S, side = 170 * S;
                U.GlowR(g, Color.Black, new RectangleF(wx - 24 * S, wy + 6 * S, ww + 48 * S, wh + 50 * S), 0.7);
                using (var p = U.Round(wx, wy, ww, wh, 12 * S)) using (var br = new SolidBrush(Color.FromArgb(30, 31, 38))) g.FillPath(br, p);
                using (var p = U.Round(wx, wy, ww, bar + 12 * S, 12 * S)) using (var br = new SolidBrush(Color.FromArgb(21, 22, 28))) g.FillPath(br, p);
                using (var br = new SolidBrush(Color.FromArgb(21, 22, 28))) g.FillRectangle(br, wx, wy + bar, ww, 1);
                using (var br = new SolidBrush(Color.FromArgb(170, 176, 190))) g.DrawString("Crystal.cs  ·  obsidot", U.F("Segoe UI", 12.5f * S), br, wx + 20 * S, wy + 10 * S);
                using (var pen = new Pen(Color.FromArgb(150, 156, 170), 1.5f * S))
                {
                    float bx = wx + ww - 30 * S, by = wy + bar / 2;
                    g.DrawLine(pen, bx - 6 * S, by - 6 * S, bx + 6 * S, by + 6 * S); g.DrawLine(pen, bx + 6 * S, by - 6 * S, bx - 6 * S, by + 6 * S);
                    g.DrawRectangle(pen, bx - 52 * S, by - 6 * S, 12 * S, 12 * S); g.DrawLine(pen, bx - 100 * S, by, bx - 88 * S, by);
                }
                using (var br = new SolidBrush(Color.FromArgb(25, 26, 32))) g.FillRectangle(br, wx, wy + bar, side, wh - bar - 12 * S);       // the file tree
                string[] files = { "src", "  Assault.cs", "  Card.cs", "  Crystal.cs", "  Pet.cs", "  Sound.cs", "docs", "README.md" };
                for (int i = 0; i < files.Length; i++)
                {
                    float fy = wy + bar + 16 * S + i * 26 * S;
                    if (i == 3) { using (var br = new SolidBrush(Color.FromArgb(40, 42, 52))) g.FillRectangle(br, wx, fy - 3 * S, side, 24 * S); using (var br = new SolidBrush(Color.FromArgb(232, 36, 32))) g.FillRectangle(br, wx, fy - 3 * S, 3 * S, 24 * S); }
                    using (var br = new SolidBrush(i == 3 ? Color.FromArgb(230, 232, 238) : Color.FromArgb(140, 146, 160))) g.DrawString(files[i], U.F("Segoe UI", 12 * S), br, wx + 18 * S, fy);
                }
                // code, coloured like a dark theme
                Color kw = Color.FromArgb(198, 120, 221), ty = Color.FromArgb(229, 192, 123), st = Color.FromArgb(152, 195, 121), cm = Color.FromArgb(106, 114, 128), tx = Color.FromArgb(220, 223, 230), nm = Color.FromArgb(97, 175, 239), num = Color.FromArgb(209, 154, 102);
                var code = new[] {
                    new object[] { cm, "// the red second: a single dot travelling the rim" },
                    new object[] { kw, "void ", nm, "Seconds", tx, "(", ty, "Graphics ", tx, "g, ", ty, "float ", tx, "x, ", ty, "float ", tx, "y)" },
                    new object[] { tx, "{" },
                    new object[] { kw, "    var ", tx, "t = ", ty, "DateTime", tx, ".Now;" },
                    new object[] { kw, "    double ", tx, "sec = t.Second + t.Millisecond / ", num, "1000.0", tx, ";" },
                    new object[] { kw, "    for ", tx, "(", kw, "int ", tx, "k = ", num, "4", tx, "; k >= ", num, "0", tx, "; k--)" },
                    new object[] { tx, "    {" },
                    new object[] { kw, "        double ", tx, "th = (sec - k * ", num, "0.35", tx, ") * ", ty, "Math", tx, ".PI / ", num, "30", tx, ";" },
                    new object[] { tx, "        br.Color = ", ty, "U", tx, ".", nm, "A", tx, "(Red, a * (k == ", num, "0", tx, " ? ", num, "1", tx, " : ", num, "0.45", tx, "));" },
                    new object[] { tx, "        g.", nm, "FillEllipse", tx, "(br, x + ", ty, "Cos", tx, "(th) * r, ...);" },
                    new object[] { tx, "    }" },
                    new object[] { tx, "}" },
                    new object[] { tx, "" },
                    new object[] { cm, "// Desktop Assault: the screen is glass" },
                    new object[] { kw, "void ", nm, "Shatter", tx, "(", kw, "int ", tx, "i, ", kw, "int ", tx, "j, ", ty, "PointF ", tx, "p)" },
                    new object[] { tx, "{" },
                    new object[] { tx, "    ", nm, "Shard", tx, "(crop, bb, tri, o);   ", cm, "// panes fall away" },
                    new object[] { ty, "    Snd", tx, ".", nm, "Play", tx, "(", st, "\"shatter\"", tx, ");" },
                    new object[] { tx, "    shake = ", ty, "Math", tx, ".", nm, "Max", tx, "(shake, ", num, "7", tx, " * S);" },
                    new object[] { tx, "}" },
                };
                var mono = U.F("Consolas", 14.5f * S); float cx0 = wx + side + 22 * S, cy0 = wy + bar + 18 * S, lh = 25 * S;
                var typo = (StringFormat)StringFormat.GenericTypographic.Clone(); typo.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces; // keep the spaces between tokens
                for (int i = 0; i < code.Length; i++)
                {
                    using (var br = new SolidBrush(Color.FromArgb(78, 84, 98))) g.DrawString((i + 1).ToString().PadLeft(2), mono, br, cx0, cy0 + i * lh);
                    float x = cx0 + 34 * S; var line = code[i];
                    for (int k = 0; k + 1 < line.Length; k += 2)
                    {
                        string s = (string)line[k + 1]; if (s.Length == 0) continue;
                        using (var br = new SolidBrush((Color)line[k])) g.DrawString(s, mono, br, x, cy0 + i * lh, typo);
                        x += g.MeasureString(s, mono, 4000, typo).Width;
                    }
                }
                // a taskbar
                using (var br = new SolidBrush(Color.FromArgb(240, 18, 19, 26))) g.FillRectangle(br, 0, h - 48 * S, w, 48 * S);
                using (var br = new SolidBrush(Color.FromArgb(200, 200, 205, 220))) for (int i = -3; i <= 3; i++) using (var p = U.Round(w / 2f + i * 46 * S - 15 * S, h - 39 * S, 30 * S, 30 * S, 7 * S)) g.FillPath(br, p);
                using (var br = new SolidBrush(Color.FromArgb(230, 240, 240, 244))) g.DrawString("9:41", U.F("Segoe UI", 12 * S), br, w - 70 * S, h - 34 * S);
            }
            // seven desktop icons down the left edge
            icons = new List<DIcon>();
            string[] names = { "Projects", "Holiday.jpg", "Resume.pdf", "Notes.txt", "Music", "Game", "Recycle Bin" };
            int[] kinds = { Desk.KFolder, Desk.KImage, Desk.KPaper, Desk.KPaper, Desk.KFolder, Desk.KFile, Desk.KBin };
            float isz = 48 * S;
            for (int i = 0; i < names.Length; i++)
            {
                float x = 30 * S, y = h * 0.15f + i * 92 * S;
                var img = new Bitmap((int)isz, (int)isz, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(img)) { g.SmoothingMode = SmoothingMode.AntiAlias; Icon(g, i, isz); }
                icons.Add(new DIcon { Name = names[i], Kind = kinds[i], Img = img, Icon = new RectangleF(x, y, isz, isz), Label = new RectangleF(x - 14 * S, y + isz + 4 * S, isz + 28 * S, 20 * S) });
            }
            return screen;
        }
        static void Icon(Graphics g, int i, float s)
        {
            float u = s / 48f;
            Func<float, float, float, float, float, GraphicsPath> rr = (x, y, ww, hh, r) => U.Round(x * u, y * u, ww * u, hh * u, r * u);
            switch (i)
            {
                case 0: case 4: // folders
                    using (var p = rr(4, 10, 22, 10, 3)) using (var br = new SolidBrush(Color.FromArgb(226, 166, 44))) g.FillPath(br, p);
                    using (var p = rr(4, 14, 40, 28, 4)) using (var br = new LinearGradientBrush(new RectangleF(0, 14 * u, s, 28 * u), Color.FromArgb(255, 214, 102), Color.FromArgb(240, 178, 52), 90f)) g.FillPath(br, p);
                    if (i == 4) using (var br = new SolidBrush(Color.FromArgb(150, 90, 40, 0))) { g.FillEllipse(br, 18 * u, 30 * u, 7 * u, 6 * u); g.FillRectangle(br, 23 * u, 21 * u, 2 * u, 12 * u); }
                    break;
                case 1: // a photo
                    using (var p = rr(5, 8, 38, 32, 3)) using (var br = new SolidBrush(Color.White)) g.FillPath(br, p);
                    using (var br = new LinearGradientBrush(new RectangleF(0, 11 * u, s, 26 * u), Color.FromArgb(90, 160, 240), Color.FromArgb(250, 190, 130), 90f)) g.FillRectangle(br, 8 * u, 11 * u, 32 * u, 26 * u);
                    using (var br = new SolidBrush(Color.FromArgb(255, 240, 180))) g.FillEllipse(br, 28 * u, 14 * u, 7 * u, 7 * u);
                    using (var br = new SolidBrush(Color.FromArgb(60, 110, 80))) g.FillPolygon(br, new[] { new PointF(8 * u, 37 * u), new PointF(18 * u, 22 * u), new PointF(26 * u, 31 * u), new PointF(31 * u, 26 * u), new PointF(40 * u, 37 * u) });
                    break;
                case 2: case 3: // a PDF, a text file
                    using (var br = new SolidBrush(Color.White)) g.FillPolygon(br, new[] { new PointF(10 * u, 4 * u), new PointF(31 * u, 4 * u), new PointF(39 * u, 12 * u), new PointF(39 * u, 44 * u), new PointF(10 * u, 44 * u) });
                    using (var br = new SolidBrush(Color.FromArgb(214, 218, 226))) { g.FillPolygon(br, new[] { new PointF(31 * u, 4 * u), new PointF(39 * u, 12 * u), new PointF(31 * u, 12 * u) }); for (int k = 0; k < 4; k++) g.FillRectangle(br, 14 * u, (18 + k * 6) * u, 20 * u, 2 * u); }
                    if (i == 2) { using (var br = new SolidBrush(Color.FromArgb(214, 40, 34))) g.FillRectangle(br, 6 * u, 30 * u, 24 * u, 11 * u); using (var br = new SolidBrush(Color.White)) g.DrawString("PDF", U.F("Segoe UI Semibold", 7.5f * u), br, 8 * u, 30.5f * u); }
                    break;
                case 5: // a game
                    using (var p = rr(6, 6, 36, 36, 9)) using (var br = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(120, 80, 240), Color.FromArgb(240, 70, 110), 45f)) g.FillPath(br, p);
                    using (var br = new SolidBrush(Color.White)) g.FillPolygon(br, new[] { new PointF(20 * u, 16 * u), new PointF(33 * u, 24 * u), new PointF(20 * u, 32 * u) });
                    break;
                case 6: // the Recycle Bin
                    using (var br = new SolidBrush(Color.FromArgb(200, 214, 226, 240))) g.FillPolygon(br, new[] { new PointF(12 * u, 12 * u), new PointF(36 * u, 12 * u), new PointF(33 * u, 44 * u), new PointF(15 * u, 44 * u) });
                    using (var pen = new Pen(Color.FromArgb(150, 120, 140, 170), 1.4f * u)) { for (int k = 0; k < 3; k++) g.DrawLine(pen, (18 + k * 6) * u, 16 * u, (19 + k * 5) * u, 40 * u); }
                    using (var br = new SolidBrush(Color.FromArgb(230, 230, 236, 244))) g.FillRectangle(br, 10 * u, 8 * u, 28 * u, 4 * u);
                    break;
            }
        }
        static Bitmap SurfaceOf(LayeredForm f) { return (Bitmap)typeof(LayeredForm).GetField("Surface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(f); }
    }
}
