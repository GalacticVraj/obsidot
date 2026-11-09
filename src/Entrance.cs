using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Obsidot
{
    // The login welcome: dots fly in from the edges of the screen along curved paths and settle into the puck's matrix
    // just as it materializes. A click-through, top-most layered window that exists for about two seconds.
    class Entrance : LayeredForm
    {
        class Dot { public PointF A, B, Ctl; public double T0, Dur; public bool Lit; }
        readonly List<Dot> dots = new List<Dot>();
        readonly Rectangle scr; readonly double t0; readonly float S = U.S;
        readonly Timer timer = new Timer { Interval = 15 };
        public double End { get; private set; }

        public Entrance(Rectangle scr, List<PointF> targets, List<bool> lit, double start) : base(scr.Width, scr.Height)
        {
            this.scr = scr; t0 = start;
            var rnd = new Random();
            for (int i = 0; i < targets.Count; i++)
            {
                var b = new PointF(targets[i].X - scr.X, targets[i].Y - scr.Y);
                PointF a; int edge = rnd.Next(4); float u = (float)rnd.NextDouble(), m = 30 * S; // a point just past a screen edge
                if (edge == 0) a = new PointF(u * scr.Width, -m); else if (edge == 1) a = new PointF(scr.Width + m, u * scr.Height);
                else if (edge == 2) a = new PointF(u * scr.Width, scr.Height + m); else a = new PointF(-m, u * scr.Height);
                float mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2, dx = b.X - a.X, dy = b.Y - a.Y, bend = (float)(rnd.NextDouble() * 0.5 + 0.2) * (i % 2 == 0 ? 1 : -1);
                var d = new Dot { A = a, B = b, Ctl = new PointF(mx - dy * bend, my + dx * bend), Lit = lit[i] };
                d.T0 = lit[i] ? 0.35 + rnd.NextDouble() * 0.25 : rnd.NextDouble() * 0.45;   // the dots of the time arrive last
                d.Dur = 0.85 + rnd.NextDouble() * 0.4;
                dots.Add(d);
                End = Math.Max(End, start + d.T0 + d.Dur);
            }
            timer.Tick += delegate { double now = U.Now; Render(now); Present(scr.X, scr.Y); if (now > End + 0.05) { timer.Stop(); Close(); } };
        }

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x20 /*TRANSPARENT: clicks pass through*/ | 0x8 /*TOPMOST*/; return cp; } }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SetFloat(true); timer.Start(); }

        public Bitmap Render(double now)
        {
            using (var g = Graphics.FromImage(Surface))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(Color.White))
                using (var pen = new Pen(Color.White, 1.2f * S) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    foreach (var d in dots)
                    {
                        double k = (now - t0 - d.T0) / d.Dur; if (k <= 0 || k >= 1) continue;
                        PointF p = At(d, U.InOut(k)), q = At(d, U.InOut(Math.Max(0, k - 0.035)));
                        float a = (float)Math.Min(1, k * 5) * (d.Lit ? 1 : 0.7f), s = (d.Lit ? 2.1f : 1.6f) * S;
                        pen.Color = U.A(Theme.T.Ray, a * 0.35); g.DrawLine(pen, q, p);                          // a short light trail
                        br.Color = U.A(Theme.T.Ray, a); g.FillEllipse(br, p.X - s, p.Y - s, s * 2, s * 2);
                    }
            }
            return Surface;
        }

        static PointF At(Dot d, double t) // quadratic Bezier
        {
            float u = (float)(1 - t), v = (float)t;
            return new PointF(u * u * d.A.X + 2 * u * v * d.Ctl.X + v * v * d.B.X, u * u * d.A.Y + 2 * u * v * d.Ctl.Y + v * v * d.B.Y);
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
