using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Obsidot
{
    class DIcon { public string Name, Path; public int Kind; public RectangleF Icon, Label; public Bitmap Img; }

    // Read-only view of the real desktop: icon positions/names from Explorer's list view, images from the shell.
    // Nothing here ever writes to a file.
    static class Desk
    {
        public const int KFile = 0, KFolder = 1, KImage = 2, KPaper = 3, KBin = 4;
        static readonly string[] Virtual = {
            "645FF040-5081-101B-9F08-00AA002F954E", // Recycle Bin
            "20D04FE0-3AEA-1069-A2D8-08002B30309D", // This PC
            "F02C1A0D-BE21-4350-88B0-7367FC96EF3C", // Network
            "5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0", // Control Panel
            "26EE0668-A00A-44D7-9371-BEB064C98683", // Control Panel (category)
            "59031A47-3F72-44A7-89C5-5595FE6B30EE", // User files
        };

        public static List<DIcon> Read(Rectangle screen)
        {
            var list = new List<DIcon>();
            IntPtr lv = ListView();
            if (lv == IntPtr.Zero || !N.IsWindowVisible(lv)) return list;
            uint pid; N.GetWindowThreadProcessId(lv, out pid);
            IntPtr hp = N.OpenProcess(0x0008 | 0x0010 | 0x0020 | 0x0400, false, pid);
            if (hp == IntPtr.Zero) return list;
            IntPtr mem = N.VirtualAllocEx(hp, IntPtr.Zero, (IntPtr)4096, 0x3000, 0x04);
            if (mem == IntPtr.Zero) { N.CloseHandle(hp); return list; }
            try
            {
                IntPtr cnt;   // LVM_GETITEMCOUNT; a hung Explorer gives up after 300 ms instead of freezing Obsidot
                if (N.SendMessageTimeout(lv, 0x1004, IntPtr.Zero, IntPtr.Zero, 0x0002, 300, out cnt) == IntPtr.Zero) return list;
                int n = (int)cnt;
                var buf = new byte[512]; IntPtr io;
                for (int i = 0; i < n && i < 400; i++)
                {
                    var d = new DIcon { Icon = Rect(hp, mem, lv, i, 1), Label = Rect(hp, mem, lv, i, 2) };
                    var item = new byte[88];
                    BitConverter.GetBytes(1).CopyTo(item, 0);   // LVIF_TEXT
                    BitConverter.GetBytes(i).CopyTo(item, 4);
                    if (IntPtr.Size == 8) { BitConverter.GetBytes(mem.ToInt64() + 512).CopyTo(item, 24); BitConverter.GetBytes(255).CopyTo(item, 32); }
                    else { BitConverter.GetBytes(mem.ToInt32() + 512).CopyTo(item, 20); BitConverter.GetBytes(255).CopyTo(item, 24); }
                    N.WriteProcessMemory(hp, IntPtr.Add(mem, 64), item, (IntPtr)item.Length, out io);
                    N.SendMessage(lv, 0x1073, (IntPtr)i, IntPtr.Add(mem, 64)); // LVM_GETITEMTEXTW
                    N.ReadProcessMemory(hp, IntPtr.Add(mem, 512), buf, (IntPtr)buf.Length, out io);
                    string s = Encoding.Unicode.GetString(buf); int z = s.IndexOf('\0'); d.Name = z >= 0 ? s.Substring(0, z) : s;
                    if (screen.Contains(Point.Round(new PointF(d.Icon.X + d.Icon.Width / 2, d.Icon.Y + d.Icon.Height / 2)))) list.Add(d);
                }
            }
            finally { N.VirtualFreeEx(hp, mem, IntPtr.Zero, 0x8000); N.CloseHandle(hp); }

            // name -> path (files are only listed, never opened for write)
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) })
            {
                try
                {
                    foreach (var e in Directory.EnumerateFileSystemEntries(dir))
                    {
                        string nm = System.IO.Path.GetFileName(e);
                        if (!map.ContainsKey(nm)) map[nm] = e;
                        if (!Directory.Exists(e)) { string bare = System.IO.Path.GetFileNameWithoutExtension(nm); if (!map.ContainsKey(bare)) map[bare] = e; }
                    }
                }
                catch { }
            }
            foreach (var g in Virtual) { string pn = "::{" + g + "}", dn = DisplayName(pn); if (dn != null && !map.ContainsKey(dn)) map[dn] = pn; }

            int px = IconPx();
            foreach (var d in list)
            {
                string p; if (!map.TryGetValue(d.Name, out p)) continue;
                d.Path = p; d.Kind = Kind(p); d.Img = Image(p, px);
            }
            return list;
        }

        static int Kind(string p)
        {
            if (p.StartsWith("::")) return p.IndexOf("645FF040", StringComparison.OrdinalIgnoreCase) >= 0 ? KBin : KFolder;
            if (Directory.Exists(p)) return KFolder;
            switch (System.IO.Path.GetExtension(p).ToLowerInvariant())
            {
                case ".png": case ".jpg": case ".jpeg": case ".gif": case ".bmp": case ".webp": case ".heic": case ".tif": case ".tiff": case ".ico": case ".svg": return KImage;
                case ".pdf": case ".doc": case ".docx": case ".txt": case ".md": case ".rtf": case ".odt": return KPaper;
            }
            return KFile;
        }

        public static int IconPx()
        {
            int sz = 48;
            try { object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Bags\1\Desktop", "IconSize", 48); if (v is int && (int)v >= 16) sz = (int)v; } catch { }
            return (int)Math.Round(sz * U.S);
        }

        // the top-level window that hosts the icons (Progman, or a WorkerW on some setups)
        public static IntPtr Host()
        {
            IntPtr pm = N.FindWindow("Progman", null);
            if (N.FindWindowEx(pm, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) return pm;
            IntPtr w = IntPtr.Zero;
            while ((w = N.FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
                if (N.FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) return w;
            return IntPtr.Zero;
        }

        static IntPtr ListView()
        {
            IntPtr def = N.FindWindowEx(Host(), IntPtr.Zero, "SHELLDLL_DefView", null);
            return def == IntPtr.Zero ? IntPtr.Zero : N.FindWindowEx(def, IntPtr.Zero, "SysListView32", null);
        }

        // is this screen point showing the desktop (not covered by an app window)?
        public static bool OnDesktop(Point p)
        {
            IntPtr root = N.GetAncestor(N.WindowFromPoint(new N.POINT { X = p.X, Y = p.Y }), 2); // GA_ROOT
            var sb = new StringBuilder(64); N.GetClassName(root, sb, 64);
            return sb.ToString() == "Progman" || sb.ToString() == "WorkerW";
        }

        static RectangleF Rect(IntPtr hp, IntPtr mem, IntPtr lv, int i, int code)
        {
            var b = new byte[16]; IntPtr io;
            BitConverter.GetBytes(code).CopyTo(b, 0);
            N.WriteProcessMemory(hp, mem, b, (IntPtr)16, out io);
            N.SendMessage(lv, 0x100E, (IntPtr)i, mem); // LVM_GETITEMRECT
            N.ReadProcessMemory(hp, mem, b, (IntPtr)16, out io);
            var a = new N.POINT { X = BitConverter.ToInt32(b, 0), Y = BitConverter.ToInt32(b, 4) };
            var c = new N.POINT { X = BitConverter.ToInt32(b, 8), Y = BitConverter.ToInt32(b, 12) };
            N.ClientToScreen(lv, ref a); N.ClientToScreen(lv, ref c);
            return RectangleF.FromLTRB(a.X, a.Y, c.X, c.Y);
        }

        static string DisplayName(string parsing)
        {
            IShellItem it; var iid = typeof(IShellItem).GUID;
            if (SHCreateItemFromParsingName(parsing, IntPtr.Zero, ref iid, out it) != 0 || it == null) return null;
            try { IntPtr s; it.GetDisplayName(0, out s); string r = Marshal.PtrToStringUni(s); Marshal.FreeCoTaskMem(s); return r; }
            catch { return null; }
            finally { Marshal.ReleaseComObject(it); }
        }

        public static Bitmap Image(string parsing, int px)
        {
            IShellItem it; var iid = typeof(IShellItem).GUID;
            if (SHCreateItemFromParsingName(parsing, IntPtr.Zero, ref iid, out it) != 0 || it == null) return null;
            try
            {
                IntPtr hb;
                if (((IShellItemImageFactory)it).GetImage(new N.SIZE { cx = px, cy = px }, 0, out hb) != 0) return null;
                try { return FromHBitmap(hb); } finally { N.DeleteObject(hb); }
            }
            catch { return null; }
            finally { Marshal.ReleaseComObject(it); }
        }

        // Image.FromHbitmap drops alpha; copy the raw 32-bit pixels (premultiplied) instead.
        static Bitmap FromHBitmap(IntPtr hb)
        {
            using (var tmp = System.Drawing.Image.FromHbitmap(hb))
            {
                int w = tmp.Width, h = tmp.Height; var r = new Rectangle(0, 0, w, h);
                var px = new byte[w * h * 4];
                var sd = tmp.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                for (int y = 0; y < h; y++) Marshal.Copy(IntPtr.Add(sd.Scan0, y * sd.Stride), px, y * w * 4, w * 4);
                tmp.UnlockBits(sd);
                bool alpha = false; for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { alpha = true; break; }
                if (!alpha) for (int i = 3; i < px.Length; i += 4) px[i] = 255;
                var res = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                var dd = res.LockBits(r, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                for (int y = 0; y < h; y++) Marshal.Copy(px, y * w * 4, IntPtr.Add(dd.Scan0, y * dd.Stride), w * 4);
                res.UnlockBits(dd);
                return res;
            }
        }

        // Rebuild the clean desktop (wallpaper only) so icons hidden behind windows still become targets.
        public static Bitmap Wallpaper(Size sz)
        {
            try
            {
                var sb = new StringBuilder(520); N.SystemParametersInfo(0x73, 520, sb, 0);
                string path = sb.ToString();
                Color bg = Color.Black;
                try { var parts = ((string)Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Colors", "Background", "0 0 0")).Split(' '); bg = Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2])); } catch { }
                string style = (Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallpaperStyle", "10") ?? "10").ToString();
                string tile = (Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "TileWallpaper", "0") ?? "0").ToString();
                var bmp = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(bg);
                    if (path.Length == 0 || !File.Exists(path)) return bmp;
                    using (var img = System.Drawing.Image.FromFile(path))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        float iw = img.Width, ih = img.Height, W = sz.Width, H = sz.Height;
                        if (tile == "1") { using (var tb = new TextureBrush(img)) g.FillRectangle(tb, 0, 0, W, H); }
                        else if (style == "2") g.DrawImage(img, 0, 0, W, H);
                        else if (style == "0") g.DrawImage(img, (W - iw) / 2, (H - ih) / 2, iw, ih);
                        else
                        {
                            float k = style == "6" ? Math.Min(W / iw, H / ih) : Math.Max(W / iw, H / ih); // fit : fill/span
                            g.DrawImage(img, (W - iw * k) / 2, (H - ih * k) / 2, iw * k, ih * k);
                        }
                    }
                }
                return bmp;
            }
            catch { return null; }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdn, out IntPtr name);
        void GetAttributes(uint mask, out uint attribs);
        void Compare(IShellItem psi, uint hint, out int order);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(N.SIZE size, int flags, out IntPtr phbm); }
}
