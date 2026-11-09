using System;
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

        // `Obsidot.exe --anim <dir>`: frame sequences for the README's animated demos (assembled into GIFs by a script)
        public static void Anim(string dir)
        {
            var c = new Crystal(); int W = c.W; float C = W / 2f, S = U.S;
            c.birth0 = U.Now - 100; c.Greet("x", U.Now - 100); c.pet.Shot(0, U.Now);
            int side = (int)(340 * S), outPx = 420;
            Action<string, int, double, Action<int, double>> scene = (name, frames, fps, each) =>
            {
                string d = Path.Combine(dir, name); Directory.CreateDirectory(d); int sd = name == "menu" ? (int)(420 * S) : name == "music" ? (int)(380 * S) : side;
                double t0 = U.Now + 1000 + name.Length * 100;   // a private timeline, far from the real clock
                c.birth0 = t0 - 100;
                for (int i = 0; i < frames; i++)
                {
                    double t = t0 + i / fps;
                    each(i, t);
                    c.Step(1 / fps, t); c.Render(t);
                    using (var b = new Bitmap(outPx, outPx))
                    using (var g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(22, 24, 29)); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(SurfaceOf(c), new Rectangle(0, 0, outPx, outPx), new Rectangle((int)(C - sd / 2), (int)(C - sd / 2), sd, sd), GraphicsUnit.Pixel);
                        b.Save(Path.Combine(d, i.ToString("000") + ".png"), ImageFormat.Png);
                    }
                }
            };
            // the clock: the cursor drifts over it, a tap ripples, a minute wipes over
            scene("clock", 84, 24, (i, t) =>
            {
                double a = i / 84.0 * Math.PI * 2; c.hover = true; c.mouse = new PointF(C + (float)Math.Cos(a) * 60 * S, C + (float)Math.Sin(a * 2) * 34 * S);
                if (i == 18) c.ShotRipple(t, c.mouse);
                if (i == 44) c.ShotWipe(t);
            });
            c.hover = false;
            // music: rays out of the rim, the edge going liquid on the kick
            scene("music", 72, 24, (i, t) => c.ShotMusicAt(t, i));
            c.musicK0(); c.ShotBands(0);
            // the menu: it opens, the red pointer turns round the buttons
            scene("menu", 96, 24, (i, t) =>
            {
                c.hover = true; if (i == 2) { c.menuOpen = true; c.ShotPoint(0); }
                if (i > 14 && i % 12 == 3) c.ShotSwing(((i - 15) / 12 + 1) % 6);
                if (i == 88) c.menuOpen = false;
            });
            c.hover = false; c.menuOpen = false; c.menuHover = -1;
            // the pet: watches a circling cursor, blinks, jumps for a heart
            scene("pet", 84, 24, (i, t) =>
            {
                if (i == 0) c.pet.Shot(0, t);
                double a = i / 84.0 * Math.PI * 2; c.pet.LookAt((float)Math.Cos(a) * 200 * S, (float)Math.Sin(a) * 120 * S);
                if (i == 22 || i == 58) c.pet.Poke(t);
            });
            // the focus timer: winding the arc to 25 minutes
            scene("focus", 72, 24, (i, t) => { if (i % 12 == 0 && i <= 48) c.ShotFocus(5 + i / 12 * 5, t); else if (i > 48) c.ShotFocus(25, t); });
            c.ShotTimer(0); Settings.Focus = 25; Settings.Name = "Vraj"; // the demo greets its author
            // a greeting, by name
            c.Greet("GOOD MORNING" + (string.IsNullOrEmpty(Settings.Name) ? "" : " " + Settings.Name.ToUpperInvariant()), U.Now + 1000 + 8 * 100 + 0.2);
            scene("greeting", 100, 20, (i, t) => { });
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
            // the app icon: the puck itself
            c.birth0 = U.Now - 100; c.mode = 0; c.menuOpen = false; c.hover = false; c.ShotTimer(0); c.Greet("x", U.Now - 100); c.pet.Shot(0, U.Now); c.pet.Roaming = true;
            for (int i = 0; i < 60; i++) c.Step(1 / 60.0, U.Now + i / 60.0);
            c.Render(U.Now + 1);
            float R = (float)c.rS.X; int sz = (int)(R * 2 + 12 * S);
            using (var b = new Bitmap(512, 512, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(SurfaceOf(c), new Rectangle(0, 0, 512, 512), new Rectangle((int)(C - sz / 2), (int)(C - sz / 2 + 2 * S), sz, sz), GraphicsUnit.Pixel);
                b.Save(Path.Combine(dir, "icon-512.png"), ImageFormat.Png);
            }
            c.pet.Roaming = false;
        }

        static Bitmap SurfaceOf(LayeredForm f) { return (Bitmap)typeof(LayeredForm).GetField("Surface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(f); }
    }
}
