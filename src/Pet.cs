using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Obsidot
{
    // A tiny black-glass creature: dot eyes that follow your cursor, a red antenna light, blinks and little hops.
    // Home is the top of the puck. It also rides along in the games, and steps out on top of any screen
    // when it has something to tell you (see Roamer).
    class Pet
    {
        readonly float S = U.S; readonly Random rnd = new Random();
        public float X;                                   // where along the top of the puck it sits, from the centre
        float tx, jy, vy, lookX, lookY;                   // walk target, jump height (up +) and speed, gaze
        readonly Spring squash = new Spring(0, 240, 9);   // + squashed flat, - stretched tall
        double blinkT = -10, nextBlink = 3, nextWander = 25, heartT = -10, wakeT = -10, sleptAt, lastKick = -10, cheerT = -10, sadT = -10;
        public bool Asleep, Walking, Scared, Roaming; public int Face = 1;
        public double ArriveAt = -1;                      // it drops in from above at this time; hidden before
        RectangleF hit;

        public bool Busy(double now)
        {
            return Walking || jy > 0.1f || vy != 0 || !squash.Rest || now - heartT < 1.2 || now - wakeT < 1.6 || now - blinkT < 0.2 || now - sadT < 1.2 || (ArriveAt > 0 && now < ArriveAt + 1.5);
        }

        public bool Visible(double now) { return !Roaming && (ArriveAt < 0 || now >= ArriveAt); }
        public bool Hit(PointF p) { return hit.Contains(p); }
        public bool Grounded { get { return jy <= 0.1f && vy == 0; } }

        public void Arrive(double at) { ArriveAt = at; jy = 150 * S; vy = 0; }
        public void Land(float speed) { squash.V += Math.Min(10, speed / (55 * S)); }

        public void Poke(double now)
        {
            if (Asleep) { WakeUp(now, false); return; }
            if (Grounded) vy = 330 * S;
            heartT = now; squash.V -= 6; Snd.Play("boing");
        }

        // a happy hop; quiet unless asked (the games are loud enough)
        public void Cheer(double now, bool heart)
        {
            if (now - cheerT < 0.35) return;
            cheerT = now; Asleep = false;
            if (Grounded) vy = 230 * S;
            squash.V -= 4; if (heart) heartT = now;
        }
        public void Sad(double now) { sadT = now; squash.V += 4; }

        bool WakeUp(double now, bool fromAway)
        {
            Asleep = false; wakeT = now; vy = 260 * S; squash.V -= 7; heartT = now + 0.35; Snd.Play("chirp");
            return fromAway && now - sleptAt > 600;
        }

        public void LookAt(float dx, float dy) { float d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f, k = Math.Min(1, d / (160 * S)); lookX = dx / d * k; lookY = dy / d * k; }

        // the body's own motion: blinks, hops, squash, gaze. Used everywhere it appears.
        public void Tick(double dt, double now, PointF eyeTo)
        {
            if (!Asleep && now > nextBlink) { blinkT = now; nextBlink = now + 2.2 + rnd.NextDouble() * 4.5; }
            if (jy > 0 || vy != 0)
            {
                vy -= 1500 * S * (float)dt; jy += vy * (float)dt;
                if (jy <= 0) { jy = 0; Land(-vy); vy = 0; }
            }
            squash.Step(dt);
            float d = (float)Math.Sqrt(eyeTo.X * eyeTo.X + eyeTo.Y * eyeTo.Y) + 0.01f, k = Math.Min(1, d / (160 * S));
            float wx = eyeTo.X / d * k, wy = eyeTo.Y / d * k, f = (float)(1 - Math.Exp(-10 * dt));
            lookX += (wx - lookX) * f; lookY += (wy - lookY) * f;
        }

        // life at home on the puck: sleeping, waking, wandering along the rim, bobbing to music.
        // Returns true when it just woke after a long time away (the puck says welcome back).
        public bool Step(double dt, double now, float R, double idle, bool seen, bool music, double kickT, PointF eyeToMouse)
        {
            if (!Visible(now)) return false;
            bool welcome = false;
            if (!Asleep && idle > 180 && Grounded) { Asleep = true; sleptAt = now; Walking = false; }
            if (Asleep && idle < 1.5 && seen) welcome = WakeUp(now, true);          // you're back, and it can see you
            if (!Asleep && !Walking && now > nextWander && !music)
            {
                float lim = R * 0.42f; tx = (float)((rnd.NextDouble() * 2 - 1) * lim);
                if (Math.Abs(tx - X) > 12 * S) Walking = true;
                nextWander = now + 14 + rnd.NextDouble() * 26;
            }
            if (Walking)
            {
                float dir = Math.Sign(tx - X); Face = (int)dir;
                X += dir * 44 * S * (float)dt;
                if (Grounded) { vy = 95 * S; squash.V += 2; }                          // it walks in little hops
                if (Math.Sign(tx - X) != dir) { X = tx; Walking = false; }
            }
            if (music && !Asleep && kickT != lastKick) { lastKick = kickT; if (Grounded && now - kickT < 0.1) { vy = 120 * S; squash.V += 3; } } // bobs to the kick
            Tick(dt, now, eyeToMouse);
            return welcome;
        }

        static readonly string[] Heart = { ".X.X.", "XXXXX", "XXXXX", ".XXX.", "..X.." };
        static readonly string[] Zed = { "XXXX", "..X.", ".X..", "XXXX" };
        static Color Red { get { return Theme.T.Accent; } }

        // at home: sitting on the rim of the puck
        public void Draw(Graphics g, float cx, float cy, float R, double now, float a)
        {
            if (!Visible(now) || a <= 0.01f) { hit = RectangleF.Empty; return; }
            float baseY = cy - (float)Math.Sqrt(Math.Max(0, R * R - X * X)) + 1.5f * S; // the rim right under it
            hit = DrawAt(g, cx + X, baseY, now, a);
        }

        // anywhere: (px, groundY) is the point between its feet. Returns the area it covers.
        public RectangleF DrawAt(Graphics g, float px, float groundY, double now, float a)
        {
            if (Scared) px += (float)Math.Sin(now * 70) * 1.2f * S;                 // trembling
            double breath = Math.Sin(now * (Asleep ? 1.5 : 2.7)) * (Asleep ? 0.05 : 0.022);
            float sq = (float)U.Clamp(squash.X * 0.07 + breath + (Scared ? 0.22 : 0), -0.35, 0.4);
            float w = 30 * S * (1 + sq), h = 22 * S * (1 - sq), feet = 3 * S, by = groundY - feet - jy; // by: bottom of the body
            var area = new RectangleF(px - 20 * S, by - h - 14 * S, 40 * S, groundY - (by - h - 14 * S) + 2 * S);

            using (var br = new SolidBrush(Color.FromArgb((int)(90 * a * Math.Max(0.2, 1 - jy / (100 * S))), 0, 0, 0)))
                g.FillEllipse(br, px - w * 0.42f, groundY - 2.2f * S, w * 0.84f, 4.4f * S); // contact shadow
            using (var br = new SolidBrush(Color.FromArgb((int)(255 * a), Theme.T.Wall)))                  // feet; the back one lifts while walking
            {
                float lift = Walking ? (float)Math.Abs(Math.Sin(now * 14)) * 1.6f * S : 0;
                g.FillEllipse(br, px - 9 * S, by - 1 * S - lift, 7 * S, feet + 1.5f * S);
                g.FillEllipse(br, px + 2 * S, by - 1 * S, 7 * S, feet + 1.5f * S);
            }
            var body = new RectangleF(px - w / 2, by - h, w, h);
            using (var lg = new LinearGradientBrush(new PointF(0, body.Top - 1), new PointF(0, body.Bottom + 1), Color.FromArgb((int)(255 * a), Theme.Mix(Theme.T.Top, Theme.T.Shine, 0.03)), Color.FromArgb((int)(255 * a), Theme.T.Bot)))
                g.FillEllipse(lg, body);
            using (var pen = new Pen(Color.FromArgb((int)(40 * a), Theme.T.Rim), 1 * S)) g.DrawEllipse(pen, body);
            using (var pen = new Pen(Color.FromArgb((int)(110 * a), Theme.T.Shine), 1.1f * S)) g.DrawArc(pen, body.X + 2 * S, body.Y + 1.6f * S, body.Width - 4 * S, body.Height - 3 * S, 205, 70); // glass highlight

            // antenna with a red light: steady while awake, a slow standby pulse while asleep, flickering when scared
            float ax = px + Face * 1.5f * S, ay = body.Top + 1 * S, tipX = ax + (float)Math.Sin(now * 2.1) * 1.5f * S - Face * 2 * S, tipY = ay - 8 * S;
            using (var pen = new Pen(Color.FromArgb((int)(200 * a), Theme.Mix(Theme.T.Bot, Theme.T.Ink, 0.15)), 1.3f * S)) g.DrawBezier(pen, ax, ay, ax, ay - 4 * S, tipX, tipY + 3 * S, tipX, tipY);
            double led = Asleep ? 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(now * 2.2)) : Scared ? (Math.Sin(now * 40) > 0 ? 1 : 0.3) : 1;
            using (var br = new SolidBrush(U.A(Red, led * a))) g.FillEllipse(br, tipX - 2 * S, tipY - 2 * S, 4 * S, 4 * S);

            // dot eyes
            float p = 1.9f * S, ds = 1.55f * S;
            float ex = px + Face * 0.8f * S + lookX * 2.4f * S, ey = by - h * 0.56f + lookY * 1.6f * S;
            bool happy = now - heartT < 1.1 || now - wakeT < 1.6 || now - cheerT < 0.6, sad = now - sadT < 1.1;
            using (var br = new SolidBrush(U.A(Theme.T.Ink, 0.95 * a)))
                for (int e = -1; e <= 1; e += 2)
                {
                    float x0 = ex + e * 6.2f * S;
                    if (Asleep || Scared) { for (int i = -1; i <= 1; i++) g.FillEllipse(br, x0 + i * p - ds / 2, ey + (Scared ? 0 : p) - ds / 2, ds, ds); } // squeezed shut
                    else if (sad) { g.FillEllipse(br, x0 - p - ds / 2, ey - (e < 0 ? 0 : p) - ds / 2 + p, ds, ds); g.FillEllipse(br, x0 - ds / 2, ey + p / 2 - ds / 2, ds, ds); g.FillEllipse(br, x0 + p - ds / 2, ey - (e < 0 ? p : 0) - ds / 2 + p, ds, ds); } // droopy
                    else if (happy) { g.FillEllipse(br, x0 - p - ds / 2, ey - ds / 2, ds, ds); g.FillEllipse(br, x0 - ds / 2, ey - p - ds / 2, ds, ds); g.FillEllipse(br, x0 + p - ds / 2, ey - ds / 2, ds, ds); } // ^ ^
                    else if (now - blinkT < 0.13) { g.FillEllipse(br, x0 - p / 2 - ds / 2, ey - ds / 2, ds, ds); g.FillEllipse(br, x0 + p / 2 - ds / 2, ey - ds / 2, ds, ds); }
                    else for (int r = -1; r <= 1; r++) for (int c = 0; c < 2; c++) g.FillEllipse(br, x0 + (c - 0.5f) * p - ds / 2, ey + r * p - ds / 2, ds, ds); // open: 2 x 3 dots
                }

            if (Asleep) // z z z drifting up and away
                for (int i = 0; i < 3; i++)
                {
                    double t = ((now * 0.45) + i / 3.0) % 1;
                    Pattern(g, Zed, px + 12 * S + (float)(t * 14 * S), body.Top - 4 * S - (float)(t * 26 * S), (1.5f + (float)t * 0.9f) * S, Theme.T.Ray, a * (float)(Math.Sin(t * Math.PI) * 0.85));
                }
            double ht = now - heartT;
            if (ht >= 0 && ht < 1.1) Pattern(g, Heart, px - 5 * S, body.Top - 14 * S - (float)(ht * 18 * S), 2.4f * S, Red, a * (float)(1 - ht / 1.1));
            return area;
        }

        static void Pattern(Graphics g, string[] rows, float x, float y, float p, Color c, float a)
        {
            if (a <= 0.01f) return;
            using (var br = new SolidBrush(U.A(c, a)))
                for (int r = 0; r < rows.Length; r++)
                    for (int k = 0; k < rows[r].Length; k++)
                        if (rows[r][k] == 'X') g.FillEllipse(br, x + k * p - p * 0.4f, y + r * p - p * 0.4f, p * 0.8f, p * 0.8f);
        }

        // seconds since the last keyboard or mouse input anywhere
        public static double IdleSeconds()
        {
            var li = new LASTINPUTINFO { cbSize = 8 };
            return GetLastInputInfo(ref li) ? (uint)(Environment.TickCount - (int)li.dwTime) / 1000.0 : 0;
        }
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize, dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO li);

        // shots harness
        public void Shot(int s, double now) { ArriveAt = -1; Roaming = false; Asleep = s == 1; Scared = s == 3; if (s == 2) { heartT = now - 0.3; jy = 10 * S; } if (s == 0) { lookX = 0.8f; lookY = 0.3f; } }
    }
}
