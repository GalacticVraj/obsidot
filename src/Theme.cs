using System;
using System.Drawing;

namespace Obsidot
{
    // One palette for everything Obsidot draws: the puck's glass, its dots, the accent, the pet, the card, the scratchpad.
    // Obsidian is the original. Pick another from the menu (THEME) and turn the bezel; it washes across the puck like ink.
    class Theme
    {
        public string Name;
        public Color Top, Bot, Wall;   // glass gradient (top -> bottom) and the edge wall under it
        public Color Ink;              // lit dots, glyphs, text
        public Color Accent;           // the one accent: seconds, pointer, timer, hearts, the pet's light
        public Color Rim, Shine;       // hairline outline, the glass highlight
        public Color Ray;              // dots drawn outside the glass, over your wallpaper (music rays, the welcome, the thread)
        public int Unlit;              // alpha of the unlit matrix dots
        public bool Bloom;             // CRT glow around lit dots

        public Color Mid { get { return Mix(Top, Bot, 0.5); } }
        public Color Soft { get { return Mix(Ink, Mid, 0.42); } }     // secondary text
        public Color Faint { get { return Mix(Ink, Mid, 0.6); } }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static readonly Theme[] All =
        {
            new Theme { Name = "OBSIDIAN",  Top = C(31, 31, 34),    Bot = C(11, 11, 12),    Wall = C(5, 5, 6),       Ink = C(255, 255, 255), Accent = C(232, 36, 32),  Rim = C(255, 255, 255), Shine = C(255, 255, 255), Ray = C(255, 255, 255), Unlit = 10, Bloom = false },
        };

        public static Theme T = All[0];
        public static int Index;
        public static void Use(int i) { Index = ((i % All.Length) + All.Length) % All.Length; T = All[Index]; }
    }
}
