using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Obsidot
{
    // A card of the same black glass as the puck. A dot-matrix strip on top names what it is (THOUGHT, FACT, MAGIC);
    // below, the idea and one line of reasoning: why it is true, or why it matters.
    class Card : LayeredForm
    {
        // kind (T thought / F fact / M magic), text, why, author
        static readonly string[,] Q = {
            {"T","Waste no more time arguing about what a good man should be. Be one.","Discussion feels like progress. Only action is.","Marcus Aurelius"},
            {"T","The impediment to action advances action. What stands in the way becomes the way.","Obstacles are not the interruption to the work. They are the work.","Marcus Aurelius"},
            {"T","We suffer more often in imagination than in reality.","Most of what you fear never arrives, and what does is rarely as heavy as you pictured.","Seneca"},
            {"T","It is not that we have a short time to live, but that we waste a lot of it.","Time is not what runs out first. Attention is.","Seneca"},
            {"T","Nature does not hurry, yet everything is accomplished.","On long work, steady beats fast.","Lao Tzu"},
            {"T","Do the difficult things while they are easy.","Problems compound like interest. Pay them early.","Lao Tzu"},
            {"T","No man ever steps in the same river twice.","The river has changed, and so have you.","Heraclitus"},
            {"T","He who has a why to live can bear almost any how.","Purpose does not remove hardship. It makes it carryable.","Friedrich Nietzsche"},
            {"T","Life can only be understood backwards; but it must be lived forwards.","Clarity arrives late. Move before it does.","Søren Kierkegaard"},
            {"T","Perfection is achieved not when there is nothing more to add, but when there is nothing left to take away.","Subtraction is a skill. Every part you remove is one less thing to break.","Antoine de Saint-Exupéry"},
            {"T","Less, but better.","Focus is deciding what not to do.","Dieter Rams"},
            {"T","The best way to predict the future is to invent it.","Forecasts describe the future. Builders decide it.","Alan Kay"},
            {"T","Simplicity is prerequisite for reliability.","Every extra moving part is another way to fail.","Edsger W. Dijkstra"},
            {"T","How we spend our days is, of course, how we spend our lives.","A life is just days, stacked. Today counts.","Annie Dillard"},
            {"T","Attention is the rarest and purest form of generosity.","Presence costs nothing to give and is remembered for years.","Simone Weil"},
            {"T","Fall seven times, stand up eight.","Resilience is counted in recoveries, not in falls.","Japanese proverb"},
            {"T","The best time to plant a tree was twenty years ago. The second best time is now.","Regret about the past is a reason to start, never a reason to stop.","Proverb"},
            {"T","Well done is better than well said.","Results speak. Promises only whisper.","Benjamin Franklin"},
            {"T","In the beginner's mind there are many possibilities, but in the expert's there are few.","Curiosity keeps open the doors that expertise quietly closes.","Shunryu Suzuki"},
            {"T","Act as if what you do makes a difference. It does.","Small actions travel further than you will ever see.","William James"},
            {"T","Make it work, make it right, make it fast.","In that order. Speed is worthless until it is correct.","Kent Beck"},
            {"T","Very little is needed to make a happy life.","Most of what we chase adds weight, not joy.","Marcus Aurelius"},
            {"F","Octopuses have three hearts and blue blood.","Two hearts serve the gills, one serves the body, and copper-based haemocyanin turns the blood blue.",""},
            {"F","Honey found in ancient Egyptian tombs was still edible.","With so little water and so much acid, almost nothing can live in it.",""},
            {"F","A day on Venus is longer than its year.","Venus turns once every 243 Earth days but circles the Sun in about 225.",""},
            {"F","Bananas are berries. Strawberries are not.","Botanically, a berry grows from a single ovary. The banana does; the strawberry does not.",""},
            {"F","There are more trees on Earth than stars in the Milky Way.","Roughly three trillion trees, against a few hundred billion stars.",""},
            {"F","The sunlight on your skin left the Sun about eight minutes ago.","It crosses 150 million kilometres at 300,000 kilometres every second.",""},
            {"F","Your brain runs on about 20 watts.","Less than a light bulb, yet it uses around a fifth of your body's energy.",""},
            {"F","Sharks are older than trees.","Sharks appear about 450 million years ago; the first trees about 385 million.",""},
            {"F","Wombat droppings are cube-shaped.","The intestine wall stretches unevenly and shapes them, handy for marking rocks without them rolling away.",""},
            {"F","A teaspoon of neutron star would weigh about a billion tonnes.","Its matter is crushed so tightly that atoms collapse into neutrons.",""},
            {"F","Oxford University is older than the Aztec Empire.","Teaching at Oxford existed by 1096; Tenochtitlan was founded in 1325.",""},
            {"F","Cleopatra lived closer in time to the Moon landing than to the building of the Great Pyramid.","The pyramid rose around 2560 BC, Cleopatra died in 30 BC, Apollo 11 landed in 1969.",""},
            {"F","Water can boil and freeze at the same time.","At its triple point, 0.01 °C under very low pressure, ice, water and vapour coexist.",""},
            {"F","Butterflies taste with their feet.","Sensors on their legs tell them whether a leaf is right for their eggs.",""},
            {"F","The Eiffel Tower grows up to 15 cm taller in summer.","Its iron expands in the heat and shrinks again in the cold.",""},
            {"F","A group of flamingos is called a flamboyance.","Some collective nouns are simply poems.",""},
            {"F","Saturn would float in water, given a big enough bath.","Its average density is lower than water's.",""},
            {"F","Some turtles can breathe through their rear end.","Cloacal respiration lets them stay underwater through the winter.",""},
            {"M","The iron in your blood was forged inside stars.","Iron is made in stellar cores and scattered across space when stars explode.",""},
            {"M","When you see the Andromeda galaxy, you see light that left it 2.5 million years ago.","You are looking back to a time before humans existed.",""},
            {"M","Atoms are almost entirely empty space.","If a nucleus were a marble, the edge of its atom would be over a kilometre away.",""},
            {"M","A bolt of lightning is about five times hotter than the surface of the Sun.","It heats the air to around 30,000 °C; the Sun's surface is about 5,500 °C.",""},
            {"M","There is a planet where it rains glass, sideways.","On HD 189733b, winds of about 8,700 km/h whip molten silicate through the sky.",""},
            {"M","The coastline of Britain has no single length.","The smaller your ruler, the longer it measures. This is the coastline paradox.",""},
            {"M","Some waves glow blue when they break.","Tiny plankton called dinoflagellates flash with light when the water disturbs them.",""},
            {"M","A spacecraft launched in 1977 is still talking to us from interstellar space.","Voyager 1's signal now takes almost a full day to reach Earth.",""},
            {"M","There are more possible games of chess than atoms in the observable universe.","Around 10^120 possible games, against roughly 10^80 atoms.",""},
            {"M","About 37 trillion cells are working inside you right now, without being asked.","Each one is running its own chemistry while you read this.",""},
            {"M","Some frogs have see-through skin. You can watch their heart beat.","Glass frogs have transparent bellies, which helps them vanish against leaves.",""},
            {"M","A forest in Utah called Pando is a single living thing.","About 47,000 aspen trunks share one root system, thousands of years old.",""},
            {"M","No two snowflakes take the same path to the ground.","Each one grows around a speck of dust and changes shape with every shift in temperature and humidity on its fall.",""},
        };
        public static int Count { get { return Q.GetLength(0); } }

        readonly Crystal c; readonly float S = U.S;
        int idx = -1, state; // 0 hidden, 1 open, 2 closing
        double t0, tc, swapT = -10; bool swapped, below = true;
        Bitmap sharp, soft, strip; float cardH, textH;
        readonly Timer timer = new Timer();
        const double TEnd = 1.3;
        readonly Random rnd = new Random();
        static Color Red { get { return Theme.T.Accent; } }

        public bool Open { get { return state != 0; } }

        public Card(Crystal c) : base((int)(368 * U.S), (int)(440 * U.S))
        {
            this.c = c; Under = c; timer.Interval = 15; timer.Tick += delegate { Tick(); };
        }

        float CW { get { return 320 * S; } }
        float X0 { get { return (LW - CW) / 2; } }
        float Gap { get { return 30 * S; } }
        float Pad { get { return 22 * S; } }
        float StripH { get { return 34 * S; } }

        public double LastShown = -1e9;
        bool auto; // opened by itself: folds away again if left untouched

        public void Toggle() { if (state == 1) Dismiss(); else { auto = false; Show_(); } }
        public void ShowAuto() { if (state != 0) return; auto = true; Show_(); }
        protected override void OnMouseMove(MouseEventArgs e) { auto = false; base.OnMouseMove(e); }

        void Show_()
        {
            if (idx < 0)
            {
                idx = DateTime.Now.DayOfYear % Count; // the card of the day first
                if (Seen(idx)) idx = Pick();
            }
            else idx = Pick();
            Prepare();
            state = 1; t0 = U.Now; swapT = -10; LastShown = U.Now;
            var h = Handle; // create without showing; first frame is presented before it becomes visible
            Tick();
            if (!Visible) { Show(); SetFloat(Settings.Float); }
            Voice();
        }

        void Voice() { string k = Q[idx, 0]; Snd.Play(k == "F" ? "fact" : k == "M" ? "magic" : "chime"); }

        public void Dismiss(bool fast = false)
        {
            if (state != 1) return;
            state = 2; tc = U.Now - (fast ? 0.35 : 0); Snd.Play("close"); Wake();
        }

        public void HideNow() { if (state == 0) return; state = 0; timer.Stop(); Hide(); }

        public void Next() { auto = false; if (state != 1 || U.Now - swapT < 1.0) return; swapT = U.Now; swapped = false; Snd.Play("whoosh"); Wake(); }

        int Pick()
        {
            var fresh = new List<int>();
            for (int i = 0; i < Count; i++) if (!Seen(i) && i != idx) fresh.Add(i);
            if (fresh.Count == 0) { int r; do r = rnd.Next(Count); while (r == idx); return r; }
            return fresh[rnd.Next(fresh.Count)];
        }

        static bool Seen(int i) { return i < Settings.Seen.Length && Settings.Seen[i] == '1'; }
        static int Collected() { int n = 0; foreach (char ch in Settings.Seen) if (ch == '1') n++; return n; }

        // everything static about a card is drawn once: the text block, and the dot-matrix strip
        void Prepare()
        {
            var s = Settings.Seen.PadRight(Count, '0').ToCharArray(); s[idx] = '1'; Settings.Seen = new string(s); Settings.Save();
            if (sharp != null) { sharp.Dispose(); soft.Dispose(); strip.Dispose(); }
            string kind = Q[idx, 0], text = Q[idx, 1], why = Q[idx, 2], who = Q[idx, 3];
            if (kind == "T") text = "“" + text + "”";
            float tw = CW - Pad * 2;
            var fMain = U.F("Segoe UI Variable Display", 18.5f * S);
            var fWhy = U.F("Segoe UI Variable Text", 13 * S);
            var fWho = U.F("Segoe UI Variable Text", 12 * S);
            float hMain, hWhy, hWho = 0;
            using (var tmp = new Bitmap(1, 1)) using (var g = Graphics.FromImage(tmp))
            {
                hMain = g.MeasureString(text, fMain, (int)tw).Height;
                hWhy = g.MeasureString(why, fWhy, (int)(tw - 14 * S)).Height;
                if (who.Length > 0) hWho = g.MeasureString(who, fWho, (int)tw).Height;
            }
            float yWhy = hMain + 16 * S, yWho = yWhy + hWhy + 10 * S;
            textH = (who.Length > 0 ? yWho + hWho : yWhy + hWhy) + 2;
            sharp = new Bitmap((int)tw, (int)Math.Ceiling(textH), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(sharp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(Theme.Mix(Theme.T.Ink, Theme.T.Mid, 0.04))) g.DrawString(text, fMain, br, new RectangleF(0, 0, tw, hMain + 2));
                using (var pen = new Pen(Color.FromArgb(34, Theme.T.Rim), 1 * S)) g.DrawLine(pen, 0, hMain + 7 * S, tw, hMain + 7 * S);
                float d = 3.2f * S;
                using (var br = new SolidBrush(Red)) g.FillEllipse(br, 1 * S, yWhy + 6.5f * S - d / 2, d, d);   // the one red accent marks the reasoning
                using (var br = new SolidBrush(Theme.T.Soft)) g.DrawString(why, fWhy, br, new RectangleF(14 * S, yWhy, tw - 14 * S, hWhy + 2));
                if (who.Length > 0) using (var br = new SolidBrush(Theme.T.Faint)) g.DrawString("— " + who, fWho, br, 0, yWho);
            }
            soft = new Bitmap(Math.Max(1, sharp.Width / 7), Math.Max(1, sharp.Height / 7), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(soft)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(sharp, 0, 0, soft.Width, soft.Height); }
            cardH = StripH + 18 * S + textH + 22 * S;

            // dot-matrix strip: faint unlit field, the kind on the left, the collection count on the right
            strip = new Bitmap((int)CW, (int)StripH, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(strip))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
                float p = 2.8f * S, dd = p * 0.62f, ox = Pad - p * 0.5f, oy = (StripH - 9 * p) / 2;
                int cols = (int)((CW - Pad * 2) / p) + 1;
                using (var br = new SolidBrush(Color.FromArgb(Theme.T.Unlit + 3, Theme.T.Ink)))
                    for (int r = 0; r < 9; r++) for (int cc = 0; cc < cols; cc++) g.FillEllipse(br, ox + cc * p - dd / 2, oy + r * p - dd / 2, dd, dd);
                var dots = new Dots();
                dots.Set(0, 4, 1, true);
                dots.Text3At(kind == "F" ? "FACT" : kind == "M" ? "MAGIC" : "THOUGHT", 2, 2, 0.95f);
                string count = (idx + 1).ToString("00") + "/" + Count;
                dots.Text3At(count, cols - 1 - Dots.W3(count), 2, 0.4f);
                Dots.Paint(g, dots, ox, oy, p, dd, Theme.T.Ink, Red);
            }
        }

        void Wake() { if (!timer.Enabled || timer.Interval != 15) { timer.Interval = 15; timer.Start(); } }

        void Tick()
        {
            double now = U.Now;
            if (state == 0) { timer.Stop(); return; }
            double a = state == 2 ? TEnd - (now - tc) * 2.4 : now - t0;
            if (state == 2 && a <= 0) { state = 0; timer.Stop(); Hide(); return; }
            double sw = now - swapT;
            if (sw >= 0.28 && !swapped && swapT > 0) { swapped = true; idx = Pick(); Prepare(); Voice(); }
            Render(now, a, sw);
            Follow();
            // idle between sheens: sleep until the next one
            double g = (now - t0 - 2.5) % 9.0;
            bool busy = state == 2 || a < TEnd + 0.1 || sw < 1.3 || (g >= 0 && g < 1.2);
            if (auto && state == 1 && now - t0 > 25) { auto = false; Dismiss(); return; }
            if (busy) Wake();
            else
            {
                double next = 9.0 - g; if (auto) next = Math.Min(next, 25.2 - (now - t0));
                timer.Interval = Math.Max(16, (int)(next * 1000));
            }
        }

        public void Follow()
        {
            if (!IsHandleCreated) return;
            float R = (float)c.rS.X;
            var wa = Screen.FromPoint(new Point((int)c.cx, (int)c.cy)).WorkingArea;
            below = c.cy + R * 1.12 + Gap + cardH + 20 * S < wa.Bottom;
            int x = (int)Math.Round(U.Clamp(c.cx - LW / 2.0, wa.Left - X0, wa.Right - LW + X0));
            int y = (int)Math.Round(below ? c.cy + R * 1.12 : c.cy - R * 1.0 - 2 * S - LH); // below the puck
            Present(x, y); winX = x; winY = y;
        }

        int winX, winY;
        // screen-space hit test for the card itself (its window is larger than the glass)
        public bool PlateContains(int x, int y)
        {
            if (state == 0) return false;
            float lx = x - winX, ly = y - winY, top = below ? Gap : LH - Gap - cardH;
            return lx >= X0 && lx <= X0 + CW && ly >= top && ly <= top + cardH + 6 * S;
        }

        void Render(double now, double a, double sw)
        {
            using (var g = Graphics.FromImage(Surface))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TextRenderingHint = TextRenderingHint.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBilinear;

                double seed = U.Bell(a, 0, 0.42), wF = U.Spr((a - 0.08) / 0.5, 7, 8), hF = U.Spr((a - 0.34) / 0.7, 5.5, 9);
                double focus = U.Clamp01((a - 0.62) / 0.55), frame = U.Clamp01((a - 0.3) / 0.3);
                if (sw >= 0 && sw < 1.3)
                {
                    if (sw < 0.28) { hF *= 1 - U.InCubic(sw / 0.28) * 0.93; focus *= 1 - U.Clamp01(sw / 0.18); }
                    else { hF *= 0.07 + 0.93 * U.Spr((sw - 0.28) / 0.6, 6, 9); focus *= U.Clamp01((sw - 0.5) / 0.45); }
                }
                float cx = LW / 2f, cw = CW * (float)Math.Max(0, wF), ch = Math.Max(1.6f * S, cardH * (float)Math.Max(0, hF));
                float anchor = below ? Gap : LH - Gap;              // edge of the card nearest the puck
                float top = below ? anchor : anchor - ch;

                // a dotted line from the puck lights up toward the card
                using (var br = new SolidBrush(Color.White))
                {
                    float from = below ? 2 * S : LH - 2 * S, len = Math.Abs(anchor - from), step = 4.2f * S;
                    int n = (int)(len / step);
                    for (int i = 0; i <= n; i++)
                    {
                        float yy = from + (below ? 1 : -1) * i * step, lit = (float)U.Clamp01(a / 0.3 * (n + 3) - i);
                        br.Color = Color.FromArgb((int)(28 + 170 * lit), Theme.T.Ray);
                        float d = 1.1f * S; g.FillEllipse(br, cx - d, yy - d, d * 2, d * 2);
                    }
                    if (seed > 0.01) { float d = 3.4f * S * (float)seed; br.Color = U.A(Theme.T.Ray, seed); g.FillEllipse(br, cx - d, anchor - d, d * 2, d * 2); }
                }
                if (cw < 1) return;

                float x0 = cx - cw / 2, rad = Math.Min(16 * S, Math.Min(cw, ch) / 2);
                U.GlowR(g, Color.Black, new RectangleF(x0 - 8 * S, top + ch - 10 * S, cw + 16 * S, 30 * S), 0.5 * frame);
                if (ch > 6 * S) using (var wall = U.Round(x0, top + 4 * S, cw, ch, rad)) using (var br = new SolidBrush(Theme.T.Wall)) g.FillPath(br, wall);
                using (var plate = U.Round(x0, top, cw, ch, rad))
                {
                    using (var lg = new LinearGradientBrush(new PointF(0, top - 1), new PointF(0, top + Math.Max(ch, 2) + 1), Theme.T.Top, Theme.T.Bot))
                        g.FillPath(lg, plate);

                    var st = g.Save();
                    g.SetClip(plate);
                    float ox = cx - CW / 2, oy = top + ch / 2 - cardH / 2; // content laid out at full size, revealed by the unfolding plate
                    using (var br = new SolidBrush(Theme.Mix(Theme.T.Bot, Theme.T.Wall, 0.4))) g.FillRectangle(br, ox, oy, CW, StripH);
                    U.DrawAlpha(g, strip, new RectangleF(ox, oy, strip.Width, strip.Height), frame);
                    using (var pen = new Pen(Color.FromArgb(20, Theme.T.Rim), 1 * S)) g.DrawLine(pen, ox, oy + StripH, ox + CW, oy + StripH);
                    if (hF < 0.999) using (var br = new SolidBrush(Color.FromArgb((int)(140 * U.Clamp01(1 - hF)), 0, 0, 0))) g.FillRectangle(br, x0, below ? top + ch * 0.5f : top, cw, ch * 0.5f);

                    // the text comes into focus: a blurred layer cross-fades into the sharp one
                    float tx = ox + Pad, ty = oy + StripH + 18 * S, sc = 1 + 0.03f * (float)(1 - focus);
                    var rr = new RectangleF(tx - sharp.Width * (sc - 1) / 2, ty - sharp.Height * (sc - 1) / 2, sharp.Width * sc, sharp.Height * sc);
                    U.DrawAlpha(g, soft, rr, Math.Min(1, (1 - focus) * 2.2) * frame * 0.9);
                    U.DrawAlpha(g, sharp, rr, U.InOut(focus));

                    double line = U.Clamp01(1 - hF * 3); // a line of light while it is still unfolding
                    if (line > 0) using (var br = new SolidBrush(U.A(Theme.T.Shine, line * 0.9))) g.FillRectangle(br, x0, top, cw, ch);

                    double gl = (now - t0 - 2.5) % 9.0; // a quiet sheen every 9 s, like light crossing glass
                    if (gl >= 0 && gl < 1.2)
                    {
                        float p = (float)U.InOut(gl / 1.2) * (cw + ch + 160 * S) - 80 * S - ch;
                        using (var lg = new LinearGradientBrush(new PointF(x0 + p, top), new PointF(x0 + p + 70 * S, top + 30 * S), Color.Transparent, Color.Transparent))
                        {
                            lg.InterpolationColors = new ColorBlend { Positions = new[] { 0f, 0.5f, 1f }, Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(16, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) } };
                            g.FillPolygon(lg, new[] { new PointF(x0 + p, top), new PointF(x0 + p + 60 * S, top), new PointF(x0 + p + 60 * S + ch, top + ch), new PointF(x0 + p + ch, top + ch) });
                        }
                    }
                    g.Restore(st);
                    using (var pen = new Pen(Color.FromArgb((int)(30 + 120 * line), Theme.T.Rim), 1 * S)) g.DrawPath(pen, plate);
                    if (cw > rad * 2 + 4 * S) using (var pen = new Pen(Color.FromArgb((int)(85 * frame), Theme.T.Shine), 1.1f * S)) g.DrawLine(pen, x0 + rad, top + 1.2f * S, x0 + cw - rad, top + 1.2f * S); // the one highlight
                }
            }
        }

        public Bitmap Shot(double a, int i = -1, bool clear = false) // shots harness; clear: no backdrop
        {
            if (i >= 0) { idx = i; Prepare(); }
            if (idx < 0) { idx = 44; Prepare(); }
            t0 = U.Now - a; state = 1; below = true;
            Render(U.Now, a, -10);
            var b = new Bitmap(LW, LH);
            using (var g = Graphics.FromImage(b))
            {
                if (!clear) using (var lg = new LinearGradientBrush(new Rectangle(0, 0, LW, LH), Color.FromArgb(38, 52, 92), Color.FromArgb(150, 120, 140), 60f)) g.FillRectangle(lg, 0, 0, LW, LH);
                g.DrawImage(Surface, 0, 0, LW, LH);
            }
            return b;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Next(); else if (e.Button == MouseButtons.Right) Dismiss();
        }
    }
}
