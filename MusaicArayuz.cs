// Musaic Kontrol - koyu temalı arayüz, özel kontroller, Spotify / YouTube Music entegrasyonu.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MusaicKontrol
{
    // ------------------------------------------------------------------ Tema

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(18, 18, 21);
        public static readonly Color Sidebar = Color.FromArgb(11, 11, 13);
        public static readonly Color Card = Color.FromArgb(29, 29, 34);
        public static readonly Color CardHi = Color.FromArgb(40, 40, 47);
        public static readonly Color CardPress = Color.FromArgb(52, 52, 60);
        public static readonly Color Track = Color.FromArgb(58, 58, 66);
        public static readonly Color Text = Color.FromArgb(242, 242, 245);
        public static readonly Color Sub = Color.FromArgb(150, 150, 162);
        public static readonly Color Accent = Color.FromArgb(255, 106, 43);
        public static readonly Color AccentHi = Color.FromArgb(255, 136, 82);
        public static readonly Color AccentPress = Color.FromArgb(222, 86, 28);
        public static readonly Color Spotify = Color.FromArgb(30, 215, 96);
        public static readonly Color SpotifyHi = Color.FromArgb(60, 230, 120);
        public static readonly Color YouTube = Color.FromArgb(255, 0, 51);
        public static readonly Color YouTubeHi = Color.FromArgb(255, 60, 90);
        public static readonly Color Good = Color.FromArgb(46, 204, 113);
        public static readonly Color Warn = Color.FromArgb(241, 196, 15);
        public static readonly Color Bad = Color.FromArgb(231, 76, 60);

        public static Font F(float size, FontStyle style = FontStyle.Regular) { return new Font("Segoe UI", size, style); }
        public static Font Semi(float size) { return new Font("Segoe UI Semibold", size); }
        public static Font Icons(float size) { return new Font("Segoe MDL2 Assets", size); }

        // Segoe MDL2 Assets simgeleri
        public const string IcoPlay = "\uE768", IcoPause = "\uE769", IcoStop = "\uE71A", IcoNext = "\uE893",
            IcoMusic = "\uE8D6", IcoFolder = "\uE8B7", IcoSettings = "\uE713", IcoSpeaker = "\uE7F5",
            IcoVolume = "\uE767", IcoMute = "\uE74F", IcoAdd = "\uE710", IcoDelete = "\uE74D", IcoClear = "\uE894",
            IcoRefresh = "\uE72C", IcoSearch = "\uE721", IcoMinus = "\uE738", IcoLink = "\uE71B", IcoHome = "\uE80F";

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Uygulama logosu: turuncu→pembe degrade yuvarlak kare, beyaz kalın "M" ve ses dalgaları.
        // Simge dosyası (musaic.ico) da bu çizimden üretilir.
        public static void DrawLogo(Graphics g, RectangleF r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float s = r.Width / 256f;
            Func<float, float, PointF> P = (x, y) => new PointF(r.X + x * s, r.Y + y * s);

            using (var path = Round(r, 58 * s))
            {
                using (var bg = new LinearGradientBrush(new PointF(r.Left, r.Top), new PointF(r.Right, r.Bottom),
                                                        Color.FromArgb(255, 150, 60), Color.FromArgb(232, 36, 98)))
                    g.FillPath(bg, path);
                // üstte hafif parlama
                var old = g.Clip;
                g.SetClip(path);
                using (var shine = new LinearGradientBrush(new PointF(r.Left, r.Top - 1), new PointF(r.Left, r.Top + r.Height * 0.6f),
                                                           Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)))
                {
                    shine.WrapMode = WrapMode.TileFlipXY;
                    g.FillRectangle(shine, r.X, r.Y, r.Width, r.Height * 0.55f);
                }
                g.Clip = old;
            }

            // M'nin gölgesi
            var mPts = new[] { P(56, 192), P(56, 74), P(122, 154), P(188, 74), P(188, 192) };
            var shPts = mPts.Select(p => new PointF(p.X + 5 * s, p.Y + 7 * s)).ToArray();
            using (var shadow = new Pen(Color.FromArgb(60, 90, 0, 30), 34 * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(shadow, shPts);
            using (var pen = new Pen(Color.White, 34 * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(pen, mPts);

            // Ses dalgaları (sağ üst)
            if (r.Width >= 24)
                for (int i = 0; i < 2; i++)
                {
                    float rad = (26 + i * 22) * s;
                    var c = P(194, 62);
                    using (var wp = new Pen(Color.FromArgb(i == 0 ? 235 : 160, 255, 255, 255), 9 * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawArc(wp, c.X - rad, c.Y - rad, rad * 2, rad * 2, -60, 75);
                }
        }

        public static Bitmap LogoBitmap(int size)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                DrawLogo(g, new RectangleF(0, 0, size, size));
            }
            return bmp;
        }

        public static Label Lbl(Control parent, string text, Font font, Color color, int x, int y, int w = 0, int h = 0)
        {
            var l = new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, Location = new Point(x, y) };
            if (w > 0) { l.AutoSize = false; l.Size = new Size(w, h > 0 ? h : font.Height + 4); }
            else l.AutoSize = true;
            parent.Controls.Add(l);
            return l;
        }
    }

    // Yuvarlak köşeli / dairesel düz düğme (isteğe bağlı simge ile)
    class FlatButton : Control
    {
        public Color BaseColor = Theme.CardHi, HoverColor = Theme.CardPress, PressColor = Theme.Track;
        public Color TextColor = Theme.Text, SelectedColor = Theme.CardHi;
        public string Glyph;
        public float GlyphSize = 12f;
        public int Radius = 10;
        public bool Circle, Selected, AccentBar, LeftAlign;
        bool hover, down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.Semi(10f);
        }

        public void Style(Color baseC, Color hoverC, Color pressC, Color textC)
        {
            BaseColor = baseC; HoverColor = hoverC; PressColor = pressC; TextColor = textC; Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Color c = !Enabled ? Color.FromArgb(120, BaseColor) : down ? PressColor : hover ? HoverColor : Selected ? SelectedColor : BaseColor;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (c.A > 0)
                using (var b = new SolidBrush(c))
                {
                    if (Circle) g.FillEllipse(b, r);
                    else using (var p = Theme.Round(r, Radius)) g.FillPath(b, p);
                }
            if (AccentBar && Selected)
                using (var b = new SolidBrush(Theme.Accent))
                using (var p = Theme.Round(new RectangleF(0, Height * 0.25f, 4, Height * 0.5f), 2)) g.FillPath(b, p);

            Color tc = Enabled ? TextColor : Theme.Sub;
            bool hasText = !string.IsNullOrEmpty(Text), hasGlyph = !string.IsNullOrEmpty(Glyph);
            using (var tb = new SolidBrush(tc))
            using (var gf = Theme.Icons(GlyphSize))
            {
                var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                if (hasGlyph && !hasText)
                {
                    g.DrawString(Glyph, gf, tb, new RectangleF(0, 1, Width, Height), sf);
                }
                else if (LeftAlign)
                {
                    float x = 18;
                    if (hasGlyph) { g.DrawString(Glyph, gf, tb, new RectangleF(x, 0, 24, Height), sf); x += 34; }
                    sf.Alignment = StringAlignment.Near;
                    g.DrawString(Text, Font, tb, new RectangleF(x, 0, Width - x - 8, Height), sf);
                }
                else
                {
                    var ts = g.MeasureString(Text, Font);
                    float gw = hasGlyph ? GlyphSize * 1.4f + 8 : 0;
                    float total = gw + ts.Width;
                    float x = (Width - total) / 2;
                    if (hasGlyph) g.DrawString(Glyph, gf, tb, new RectangleF(x, 0, GlyphSize * 1.4f, Height), sf);
                    sf.Alignment = StringAlignment.Near;
                    g.DrawString(Text, Font, tb, new RectangleF(x + gw, 0, ts.Width + 4, Height), sf);
                }
            }
        }
    }

    // Koyu yatay kaydırıcı (0-100)
    class DarkSlider : Control
    {
        int val;
        bool drag, hover;
        public Color Fill = Theme.Accent;
        public event EventHandler ValueChanged;

        public DarkSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v == val) return;
                val = v; Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public void SetSilently(int v) { val = Math.Max(0, Math.Min(100, v)); Invalidate(); }

        void FromX(int x)
        {
            const int pad = 10;
            Value = (int)Math.Round((x - pad) * 100.0 / Math.Max(1, Width - 2 * pad));
        }

        protected override void OnMouseDown(MouseEventArgs e) { drag = true; FromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) FromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { Value += e.Delta > 0 ? 2 : -2; base.OnMouseWheel(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            const int pad = 10;
            float cy = Height / 2f, w = Width - 2 * pad;
            float fx = pad + w * val / 100f;
            using (var tb = new SolidBrush(Theme.Track))
            using (var p = Theme.Round(new RectangleF(pad, cy - 3, w, 6), 3)) g.FillPath(tb, p);
            using (var fb = new SolidBrush(Fill))
            using (var p = Theme.Round(new RectangleF(pad, cy - 3, Math.Max(6, fx - pad), 6), 3)) g.FillPath(fb, p);
            float tr = (hover || drag) ? 9 : 7;
            using (var wb = new SolidBrush(Color.White)) g.FillEllipse(wb, fx - tr, cy - tr, tr * 2, tr * 2);
        }
    }

    // Uygulama logosunu çizen küçük kutu
    class LogoBox : Control
    {
        public LogoBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.DrawLogo(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1));
        }
    }

    // Açma/kapama anahtarı (sağında açıklama metniyle)
    class DarkToggle : Control
    {
        bool on, hover;
        public string Description = "";
        public event EventHandler CheckedChanged;

        public DarkToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.Semi(10f);
        }

        public bool Checked
        {
            get { return on; }
            set { if (on == value) return; on = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }

        public void SetSilently(bool v) { on = v; Invalidate(); }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var track = new RectangleF(1, (Height - 24) / 2f, 44, 24);
            Color tc = on ? (hover ? Theme.AccentHi : Theme.Accent) : (hover ? Theme.CardPress : Theme.Track);
            using (var b = new SolidBrush(tc))
            using (var p = Theme.Round(track, 12)) g.FillPath(b, p);
            float kx = on ? track.Right - 21 : track.X + 3;
            using (var w = new SolidBrush(Color.White)) g.FillEllipse(w, kx, track.Y + 3, 18, 18);
            using (var tb = new SolidBrush(Theme.Text)) g.DrawString(Text, Font, tb, 58, string.IsNullOrEmpty(Description) ? (Height - Font.Height) / 2f : 2);
            if (!string.IsNullOrEmpty(Description))
                using (var f = Theme.F(8.5f))
                using (var sb = new SolidBrush(Theme.Sub)) g.DrawString(Description, f, sb, 59, 2 + Font.Height);
        }
    }

    // Yuvarlak köşeli kart paneli
    class Card : Panel
    {
        public Color Fill = Theme.Card;
        public Color Strip = Color.Empty;
        public int Radius = 16;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var b = new SolidBrush(Fill))
            using (var p = Theme.Round(r, Radius)) g.FillPath(b, p);
            if (Strip != Color.Empty)
            {
                var old = g.Clip;
                using (var p = Theme.Round(r, Radius)) g.SetClip(p);
                using (var b = new SolidBrush(Strip)) g.FillRectangle(b, 0, 0, Width, 5);
                g.Clip = old;
            }
            base.OnPaint(e);
        }
    }

    // Koyu, sahip-çizimli açılır liste
    class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.CardHi;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ItemHeight = 26;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var b = new SolidBrush(sel ? Theme.Accent : Theme.CardHi)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Koyu, sahip-çizimli liste
    class DarkList : ListBox
    {
        public Func<object, string> ItemText;

        public DarkList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ItemHeight = 32;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) { using (var bb = new SolidBrush(BackColor)) e.Graphics.FillRectangle(bb, e.Bounds); return; }
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var g = e.Graphics;
            using (var bg = new SolidBrush(BackColor)) g.FillRectangle(bg, e.Bounds);
            if (sel)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Theme.CardPress))
                using (var p = Theme.Round(new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4), 8)) g.FillPath(b, p);
                using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, e.Bounds.X + 2, e.Bounds.Y + 9, 3, e.Bounds.Height - 18);
            }
            string t = ItemText != null ? ItemText(Items[e.Index]) : GetItemText(Items[e.Index]);
            TextRenderer.DrawText(g, t, Font, new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height),
                sel ? Theme.Text : Color.FromArgb(215, 215, 222), sel ? Theme.CardPress : BackColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // ------------------------------------------------------------------ Uygulama başına ses yönlendirme
    // Windows 10 21H2+ / 11'in "Uygulama ses seviyesi ve aygıt tercihleri" özelliğini kullanır (EarTrumpet ile aynı yöntem).

    static class AppAudioRouter
    {
        [DllImport("combase.dll", PreserveSig = false)]
        static extern void RoGetActivationFactory(IntPtr classId, [In] ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);
        [DllImport("combase.dll", PreserveSig = false)]
        static extern void WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string src, int length, out IntPtr hstring);
        [DllImport("combase.dll", PreserveSig = false)]
        static extern void WindowsDeleteString(IntPtr hstring);

        [ComImport, Guid("ab3d4648-e242-459f-b02f-541c70306324"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        interface IAudioPolicyConfigFactory
        {
            [PreserveSig] int u01(); [PreserveSig] int u02(); [PreserveSig] int u03(); [PreserveSig] int u04();
            [PreserveSig] int u05(); [PreserveSig] int u06(); [PreserveSig] int u07(); [PreserveSig] int u08();
            [PreserveSig] int u09(); [PreserveSig] int u10(); [PreserveSig] int u11(); [PreserveSig] int u12();
            [PreserveSig] int u13(); [PreserveSig] int u14(); [PreserveSig] int u15(); [PreserveSig] int u16();
            [PreserveSig] int u17(); [PreserveSig] int u18(); [PreserveSig] int u19();
            [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, IntPtr deviceId);
            [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, int flow, int role, out IntPtr deviceId);
            [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
        }

        static IAudioPolicyConfigFactory factory;

        static IAudioPolicyConfigFactory Factory()
        {
            if (factory != null) return factory;
            const string cls = "Windows.Media.Internal.AudioPolicyConfig";
            IntPtr h;
            WindowsCreateString(cls, cls.Length, out h);
            try
            {
                Guid iid = typeof(IAudioPolicyConfigFactory).GUID;
                object o;
                RoGetActivationFactory(h, ref iid, out o);
                factory = (IAudioPolicyConfigFactory)o;
            }
            finally { WindowsDeleteString(h); }
            return factory;
        }

        // processName (uzantısız) adlı tüm süreçlerin sesini verilen aygıta yönlendirir; deviceId null ise varsayılana döndürür.
        public static int Route(string processName, string deviceId)
        {
            var f = Factory();
            IntPtr hs = IntPtr.Zero;
            if (deviceId != null)
            {
                string full = @"\\?\SWD#MMDEVAPI#" + deviceId + "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
                WindowsCreateString(full, full.Length, out hs);
            }
            int n = 0;
            try
            {
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        int a = f.SetPersistedDefaultAudioEndpoint((uint)p.Id, 0 /*eRender*/, 0 /*eConsole*/, hs);
                        int b = f.SetPersistedDefaultAudioEndpoint((uint)p.Id, 0, 1 /*eMultimedia*/, hs);
                        if (a >= 0 && b >= 0) n++;
                    }
                    catch { }
                }
            }
            finally { if (hs != IntPtr.Zero) WindowsDeleteString(hs); }
            return n;
        }
    }

    // ------------------------------------------------------------------ Ana pencere

    class MainForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        static void DarkScroll(Control c, string theme = "DarkMode_Explorer")
        {
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, theme, null);
            else c.HandleCreated += (s, e) => SetWindowTheme(c.Handle, theme, null);
        }

        // --- durum
        Renderer cur;
        readonly StreamServer server = new StreamServer();
        readonly LoopbackCapture capture = new LoopbackCapture();
        string playMode = "none"; // none | live | file
        int fileIndex = -1;
        bool userStopped;
        string lastState = "";
        DateTime playStartedAt = DateTime.MinValue;
        volatile bool polling;
        bool muted;
        int cushionMs = 400;
        bool loadingSources;
        bool ytRouted, spRouted;
        DarkToggle tglAutoRun, tglTray, tglAutoStream, tglAutoSpotify;
        NotifyIcon tray;
        bool startHidden, reallyExit, autoStreamStarted;
        DateTime stoppedSince = DateTime.MaxValue;
        static readonly string OptsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusaicKontrol", "secenekler.txt");
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        DarkToggle NewToggle(Control parent, string text, string desc, int y)
        {
            var t = new DarkToggle { Text = text, Description = desc, Location = new Point(20, y), Size = new Size(660, 44) };
            parent.Controls.Add(t);
            return t;
        }

        static Dictionary<string, string> ReadOpts()
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (File.Exists(OptsFile))
                    foreach (var line in File.ReadAllLines(OptsFile))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
            }
            catch { }
            return d;
        }

        static bool GetOpt(string key, bool def)
        {
            string v;
            return ReadOpts().TryGetValue(key, out v) ? v == "1" : def;
        }

        static void SetOpt(string key, bool value)
        {
            try
            {
                var d = ReadOpts();
                d[key] = value ? "1" : "0";
                Directory.CreateDirectory(Path.GetDirectoryName(OptsFile));
                File.WriteAllLines(OptsFile, d.Select(kv => kv.Key + "=" + kv.Value).ToArray());
            }
            catch { }
        }

        static bool AutoRunEnabled()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("MusaicKontrol") != null;
            }
            catch { return false; }
        }

        void SetAutoRun(bool on)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue("MusaicKontrol", "\"" + Application.ExecutablePath + "\" --gizli");
                    else k.DeleteValue("MusaicKontrol", false);
                }
                Log(on ? "Windows açılınca otomatik başlayacak." : "Otomatik başlatma kapatıldı.");
            }
            catch (Exception ex) { Log("Otomatik başlatma ayarlanamadı: " + ex.Message); }
        }

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Musaic Kontrol'ü aç", null, (s, e) => ShowFromTray());
            menu.Items.Add("Yayını başlat / durdur", null, (s, e) => { if (playMode == "live") StopAll(); else StartLive(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Çıkış", null, (s, e) => { reallyExit = true; tray.Visible = false; Close(); });
            tray = new NotifyIcon { Text = "Musaic Kontrol", ContextMenuStrip = menu };
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { tray.Icon = SystemIcons.Application; }
            tray.DoubleClick += (s, e) => ShowFromTray();
        }

        void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
        }

        static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusaicKontrol");
        static readonly string LastDeviceFile = Path.Combine(SettingsDir, "son_cihaz.txt");
        static readonly string FormatFile = Path.Combine(SettingsDir, "yayin_bicimi2.txt");
        static readonly string CushionFile = Path.Combine(SettingsDir, "guvenlik_payi.txt");
        static readonly string SourceFile = Path.Combine(SettingsDir, "kaynak_aygit.txt");

        // --- kontroller
        Panel sidebar, content;
        readonly List<FlatButton> navButtons = new List<FlatButton>();
        readonly List<Panel> pages = new List<Panel>();
        Label sbDot, sbName, sbIp, statusBar;
        // Çal
        Label lblBigName, lblBigSub, lblBigTitle, lblBigDesc, lblChips, lblVol;
        FlatButton btnBig, btnPlay, btnPause, btnStop, btnMute;
        DarkSlider sldVol;
        // Müzik
        Label lblSpStatus, lblYtStatus, lblMusicNote;
        FlatButton btnSpOpen, btnSpRoute, btnYtOpen, btnYtUnroute;
        // Dosyalar
        DarkList lstFiles;
        // Ayarlar
        DarkList lstDevices;
        FlatButton btnScan, btnAddIp;
        TextBox txtIp, txtLog;
        Label lblInfo, lblCushion;
        DarkCombo cmbSource, cmbFormat;

        System.Windows.Forms.Timer pollTimer, volTimer, restartTimer, routeTimer, spotifyTimer;

        public MainForm()
        {
            Text = "Musaic Kontrol";
            Font = Theme.F(9.5f);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            ClientSize = new Size(1010, 660);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildSidebar();
            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            statusBar = new Label { Dock = DockStyle.Bottom, Height = 30, ForeColor = Theme.Sub, BackColor = Theme.Bg, Font = Theme.F(9f), Padding = new Padding(40, 0, 20, 6), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            Controls.Add(content);
            Controls.Add(sidebar);
            content.Controls.Add(statusBar);

            pages.Add(BuildPlayPage());
            pages.Add(BuildMusicPage());
            pages.Add(BuildFilesPage());
            pages.Add(BuildSettingsPage());
            foreach (var p in pages) { content.Controls.Add(p); p.BringToFront(); }
            int startPage = 0;
            var args = Environment.GetCommandLineArgs();
            for (int a = 1; a + 1 < args.Length; a++) if (args[a] == "--sayfa") int.TryParse(args[a + 1], out startPage);
            ShowPage(Math.Max(0, Math.Min(3, startPage)));

            // Zamanlayıcılar
            volTimer = new System.Windows.Forms.Timer { Interval = 250 };
            volTimer.Tick += (s, e) =>
            {
                volTimer.Stop();
                int v = sldVol.Value;
                Act(null, () => Rc("SetVolume", "<InstanceID>0</InstanceID><Channel>Master</Channel><DesiredVolume>" + v + "</DesiredVolume>"));
            };
            pollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            pollTimer.Tick += (s, e) => Poll();
            pollTimer.Start();
            restartTimer = new System.Windows.Forms.Timer { Interval = 900 };
            restartTimer.Tick += (s, e) => { restartTimer.Stop(); if (playMode == "live") StartLive(); };
            routeTimer = new System.Windows.Forms.Timer { Interval = 4000 };
            routeTimer.Tick += (s, e) => ReapplyRoutes();
            routeTimer.Start();
            spotifyTimer = new System.Windows.Forms.Timer { Interval = 15000 };
            spotifyTimer.Tick += (s, e) => CheckSpotify();
            spotifyTimer.Start();

            capture.OnData = b => server.Push(b);
            capture.OnDeviceChanged = (id, rateChanged) =>
            {
                Log("Yakalanan aygıt değişti → " + DeviceName(id) + " (" + capture.SampleRate + " Hz)");
                if (rateChanged) UI(() => { if (playMode == "live") { restartTimer.Stop(); restartTimer.Start(); } });
                UI(UpdateChips);
            };
            server.Log = Log;

            try { if (File.Exists(CushionFile)) cushionMs = Math.Max(0, Math.Min(3000, int.Parse(File.ReadAllText(CushionFile).Trim()))); } catch { }
            server.CushionMs = cushionMs;
            UpdateCushionLabel();
            LoadSources();
            UpdateChips();
            UpdateBigButton();

            Load += (s, e) => OnLoaded();
            BuildTray();
            foreach (var a in Environment.GetCommandLineArgs()) if (a == "--gizli") startHidden = true;
            tray.Visible = startHidden || GetOpt("tepsi", false);
            if (GetOpt("oto_spotify", false)) spRouted = true;
            FormClosing += (s, e) =>
            {
                if (!reallyExit && e.CloseReason == CloseReason.UserClosing && GetOpt("tepsi", false))
                {
                    e.Cancel = true;
                    Hide();
                    tray.ShowBalloonTip(2500, "Musaic Kontrol", "Arka planda çalışmaya devam ediyor.", ToolTipIcon.Info);
                    return;
                }
                tray.Visible = false;
                server.LiveActive = false;
                capture.Stop();
            };
        }

        protected override void SetVisibleCore(bool value)
        {
            // --gizli ile başlatıldıysa ilk açılışta pencereyi gösterme (tepside başla)
            if (startHidden && !IsHandleCreated)
            {
                CreateHandle();
                OnLoaded();
                value = false;
                ShowInTaskbar = false;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { } // koyu başlık çubuğu
        }

        // ================================================================ Yerleşim

        void BuildSidebar()
        {
            sidebar = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Theme.Sidebar };
            var logo = new LogoBox { Location = new Point(22, 26), Size = new Size(46, 46) };
            sidebar.Controls.Add(logo);
            Theme.Lbl(sidebar, "MUSAIC", Theme.F(17f, FontStyle.Bold), Theme.Text, 76, 24);
            Theme.Lbl(sidebar, "Kontrol Merkezi", Theme.F(9f), Theme.Accent, 79, 54);

            string[] names = { "Çal", "Müzik", "Dosyalar", "Ayarlar" };
            string[] icons = { Theme.IcoPlay, Theme.IcoMusic, Theme.IcoFolder, Theme.IcoSettings };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var b = new FlatButton
                {
                    Text = names[i], Glyph = icons[i], GlyphSize = 13f, LeftAlign = true, AccentBar = true,
                    Location = new Point(12, 116 + i * 50), Size = new Size(196, 44), Font = Theme.Semi(11f)
                };
                b.Style(Theme.Sidebar, Color.FromArgb(24, 24, 28), Color.FromArgb(32, 32, 38), Color.FromArgb(200, 200, 208));
                b.SelectedColor = Color.FromArgb(30, 30, 36);
                b.Click += (s, e) => ShowPage(idx);
                navButtons.Add(b);
                sidebar.Controls.Add(b);
            }

            // Alt kısım: hoparlör durumu
            var box = new Card { Fill = Color.FromArgb(22, 22, 26), Location = new Point(12, 560), Size = new Size(196, 84), Radius = 12 };
            sidebar.Controls.Add(box);
            Theme.Lbl(box, Theme.IcoSpeaker, Theme.Icons(16f), Theme.Sub, 12, 16);
            sbName = Theme.Lbl(box, "Hoparlör aranıyor…", Theme.Semi(10f), Theme.Text, 44, 12, 146, 22);
            sbName.AutoEllipsis = true;
            sbDot = Theme.Lbl(box, "●", Theme.F(9f), Theme.Sub, 44, 36);
            sbIp = Theme.Lbl(box, "-", Theme.F(9f), Theme.Sub, 60, 37, 130, 18);
            var sbHint = Theme.Lbl(box, "", Theme.F(8.5f), Theme.Sub, 12, 58, 176, 18);
            sbHint.Name = "sbHint";
        }

        Panel NewPage(string title, string sub)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
            Theme.Lbl(p, title, Theme.F(22f, FontStyle.Bold), Theme.Text, 36, 26);
            if (sub != null) Theme.Lbl(p, sub, Theme.F(10f), Theme.Sub, 39, 72, 700, 22);
            return p;
        }

        void ShowPage(int i)
        {
            for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == i;
            for (int k = 0; k < navButtons.Count; k++) { navButtons[k].Selected = k == i; navButtons[k].Invalidate(); }
            if (i == 1) CheckSpotify();
        }

        Panel BuildPlayPage()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
            lblBigName = Theme.Lbl(p, "Musaic MP5", Theme.F(26f, FontStyle.Bold), Theme.Text, 34, 22);
            lblBigSub = Theme.Lbl(p, "Bağlanıyor…", Theme.F(10.5f), Theme.Sub, 39, 74, 700, 22);

            var now = new Card { Location = new Point(36, 116), Size = new Size(718, 250) };
            p.Controls.Add(now);
            btnBig = new FlatButton { Circle = true, Glyph = Theme.IcoPlay, GlyphSize = 34f, Location = new Point(36, 48), Size = new Size(130, 130) };
            btnBig.Style(Theme.Accent, Theme.AccentHi, Theme.AccentPress, Color.White);
            btnBig.Click += (s, e) => { if (playMode == "live") StopAll(); else StartLive(); };
            now.Controls.Add(btnBig);
            lblBigTitle = Theme.Lbl(now, "PC sesini hoparlöre gönder", Theme.F(17f, FontStyle.Bold), Theme.Text, 196, 50, 500, 34);
            lblBigDesc = Theme.Lbl(now, "YouTube, Spotify, oyunlar… seçili ses aygıtında çalan her şey Musaic'ten çalar.", Theme.F(10f), Theme.Sub, 198, 88, 490, 40);
            lblChips = Theme.Lbl(now, "", Theme.F(9f), Theme.Sub, 198, 130, 500, 20);

            btnPlay = SmallRound(now, Theme.IcoPlay, 198, 172, "Devam");
            btnPause = SmallRound(now, Theme.IcoPause, 250, 172, "Duraklat");
            btnStop = SmallRound(now, Theme.IcoStop, 302, 172, "Durdur");
            btnPlay.Click += (s, e) => { userStopped = false; Act("Devam", () => AvT("Play", "<InstanceID>0</InstanceID><Speed>1</Speed>")); };
            btnPause.Click += (s, e) => Act("Duraklat", () => AvT("Pause", "<InstanceID>0</InstanceID>"));
            btnStop.Click += (s, e) => StopAll();

            var vol = new Card { Location = new Point(36, 386), Size = new Size(718, 96) };
            p.Controls.Add(vol);
            Theme.Lbl(vol, "Ses seviyesi", Theme.Semi(10f), Theme.Sub, 24, 14);
            btnMute = new FlatButton { Circle = true, Glyph = Theme.IcoVolume, GlyphSize = 14f, Location = new Point(20, 42), Size = new Size(40, 40) };
            btnMute.Style(Theme.CardHi, Theme.CardPress, Theme.Track, Theme.Text);
            btnMute.Click += (s, e) =>
            {
                muted = !muted;
                UpdateMuteGlyph();
                bool m = muted;
                Act(m ? "Sessiz" : "Sesi aç", () => Rc("SetMute", "<InstanceID>0</InstanceID><Channel>Master</Channel><DesiredMute>" + (m ? "1" : "0") + "</DesiredMute>"));
            };
            vol.Controls.Add(btnMute);
            sldVol = new DarkSlider { Location = new Point(72, 46), Size = new Size(540, 32) };
            sldVol.ValueChanged += (s, e) => { lblVol.Text = sldVol.Value.ToString(); volTimer.Stop(); volTimer.Start(); };
            vol.Controls.Add(sldVol);
            lblVol = Theme.Lbl(vol, "–", Theme.F(18f, FontStyle.Bold), Theme.Text, 630, 44, 70, 34);
            lblVol.TextAlign = ContentAlignment.MiddleRight;

            var tip = new Card { Location = new Point(36, 502), Size = new Size(718, 76), Fill = Color.FromArgb(24, 24, 28) };
            p.Controls.Add(tip);
            Theme.Lbl(tip, Theme.IcoMusic, Theme.Icons(16f), Theme.Accent, 22, 26);
            Theme.Lbl(tip, "Spotify veya YouTube Music mi dinleyeceksiniz?  'Müzik' sekmesinden tek tıkla Musaic'te çalın —\r\nyalnızca o uygulamanın sesi hoparlöre gider, diğer sesler kulaklıkta kalır.", Theme.F(9.5f), Theme.Sub, 58, 18, 640, 42);
            return p;
        }

        FlatButton SmallRound(Control parent, string glyph, int x, int y, string tip)
        {
            var b = new FlatButton { Circle = true, Glyph = glyph, GlyphSize = 12f, Location = new Point(x, y), Size = new Size(42, 42) };
            b.Style(Theme.CardHi, Theme.CardPress, Theme.Track, Theme.Text);
            new ToolTip().SetToolTip(b, tip);
            parent.Controls.Add(b);
            return b;
        }

        FlatButton Btn(Control parent, string text, string glyph, int x, int y, int w, int h, bool accent = false)
        {
            var b = new FlatButton { Text = text, Glyph = glyph, Location = new Point(x, y), Size = new Size(w, h), Radius = h / 2 };
            if (accent) b.Style(Theme.Accent, Theme.AccentHi, Theme.AccentPress, Color.White);
            else b.Style(Theme.CardHi, Theme.CardPress, Theme.Track, Theme.Text);
            parent.Controls.Add(b);
            return b;
        }

        Panel BuildMusicPage()
        {
            var p = NewPage("Müzik", "Spotify ve YouTube Music'i doğrudan Musaic'te dinleyin.");

            // Spotify
            var sp = new Card { Location = new Point(36, 116), Size = new Size(350, 420), Strip = Theme.Spotify };
            p.Controls.Add(sp);
            Theme.Lbl(sp, "Spotify", Theme.F(20f, FontStyle.Bold), Theme.Spotify, 24, 26);
            Theme.Lbl(sp, "Bilgisayardaki Spotify üzerinden", Theme.Semi(10f), Theme.Sub, 27, 66);
            lblSpStatus = Theme.Lbl(sp, "● Kontrol ediliyor…", Theme.Semi(10f), Theme.Sub, 26, 98, 300, 22);
            Theme.Lbl(sp,
                "Spotify'ın sesi doğrudan Musaic'e gider; oyun, Discord gibi diğer sesler kulaklığınızda kalır.\r\n\r\n" +
                "Telefondan kontrol: Telefonda Spotify → 'Cihazlar' → bu bilgisayarı seçin. Ses Musaic'ten çalar.\r\n\r\n" +
                "(Spotify, hoparlörün kendi eski Spotify Connect sürümünü artık desteklemiyor.)",
                Theme.F(9.5f), Color.FromArgb(205, 205, 212), 26, 132, 300, 160);
            btnSpOpen = new FlatButton { Text = "Spotify'ı Musaic'te Çal", Glyph = Theme.IcoPlay, Location = new Point(24, 300), Size = new Size(302, 46), Radius = 23, Font = Theme.Semi(11f) };
            btnSpOpen.Style(Theme.Spotify, Theme.SpotifyHi, Color.FromArgb(24, 180, 80), Color.Black);
            btnSpOpen.Click += (s, e) => PlaySpotifyOnMusaic();
            sp.Controls.Add(btnSpOpen);
            btnSpRoute = Btn(sp, "Spotify'ı kulaklığa geri al", null, 24, 356, 302, 38);
            btnSpRoute.Font = Theme.F(9.5f);
            btnSpRoute.Click += (s, e) => UnrouteSpotify();

            // YouTube Music
            var yt = new Card { Location = new Point(404, 116), Size = new Size(350, 420), Strip = Theme.YouTube };
            p.Controls.Add(yt);
            Theme.Lbl(yt, "YouTube Music", Theme.F(20f, FontStyle.Bold), Theme.YouTube, 24, 26);
            Theme.Lbl(yt, "Ayrı pencerede, yalnızca sesi Musaic'e", Theme.Semi(10f), Theme.Sub, 27, 66);
            lblYtStatus = Theme.Lbl(yt, "● Kapalı", Theme.Semi(10f), Theme.Sub, 26, 98, 300, 22);
            Theme.Lbl(yt,
                "YouTube Music kendi penceresinde açılır ve yalnızca onun sesi hoparlöre yönlendirilir. " +
                "Oyun, Discord ve diğer sesler kulaklığınızda kalır.\r\n\r\n" +
                "İlk açılışta Google hesabınızla giriş yapın; sonraki açılışlarda hatırlanır.",
                Theme.F(9.5f), Color.FromArgb(205, 205, 212), 26, 132, 300, 160);
            btnYtOpen = new FlatButton { Text = "YouTube Music'i Aç", Glyph = Theme.IcoPlay, Location = new Point(24, 300), Size = new Size(302, 46), Radius = 23, Font = Theme.Semi(11f) };
            btnYtOpen.Style(Theme.YouTube, Theme.YouTubeHi, Color.FromArgb(200, 0, 40), Color.White);
            btnYtOpen.Click += (s, e) => OpenYouTubeMusic();
            yt.Controls.Add(btnYtOpen);
            btnYtUnroute = Btn(yt, "Yönlendirmeleri kaldır (kulaklığa dön)", null, 24, 356, 302, 38);
            btnYtUnroute.Font = Theme.F(9.5f);
            btnYtUnroute.Click += (s, e) => Unroute();

            lblMusicNote = Theme.Lbl(p, "", Theme.F(9f), Theme.Sub, 39, 552, 715, 36);
            return p;
        }

        Panel BuildFilesPage()
        {
            var p = NewPage("Dosyalar", "Bilgisayardaki müzik dosyalarını kayıpsız çalın — dosya veya klasörü listeye sürükleyip bırakabilirsiniz.");
            var c = new Card { Location = new Point(36, 116), Size = new Size(718, 470) };
            p.Controls.Add(c);
            lstFiles = new DarkList { Location = new Point(16, 16), Size = new Size(686, 370), AllowDrop = true };
            lstFiles.ItemText = o => { string f = (string)o; return Path.GetFileNameWithoutExtension(f) + "    ·  " + Path.GetExtension(f).TrimStart('.').ToUpperInvariant(); };
            c.Controls.Add(lstFiles);
            DarkScroll(lstFiles);
            var lblEmpty = Theme.Lbl(c, "Müzik dosyası veya klasörünü buraya sürükleyin ya da 'Ekle'ye basın", Theme.F(11f), Theme.Sub, 16, 180, 686, 30);
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.BringToFront();
            lblEmpty.AllowDrop = true;
            lblEmpty.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            lblEmpty.DragDrop += (s, e) => AddFilePaths((string[])e.Data.GetData(DataFormats.FileDrop));
            var emptyTimer = new System.Windows.Forms.Timer { Interval = 300 };
            emptyTimer.Tick += (s, e) => lblEmpty.Visible = lstFiles.Items.Count == 0;
            emptyTimer.Start();
            var bAdd = Btn(c, "Ekle", Theme.IcoAdd, 16, 404, 120, 44, true);
            var bPlay = Btn(c, "Çal", Theme.IcoPlay, 146, 404, 110, 44);
            var bNext = Btn(c, "Sonraki", Theme.IcoNext, 266, 404, 130, 44);
            var bDel = Btn(c, "Kaldır", Theme.IcoDelete, 444, 404, 120, 44);
            var bClr = Btn(c, "Temizle", Theme.IcoClear, 574, 404, 128, 44);
            bAdd.Click += (s, e) => AddFiles();
            bPlay.Click += (s, e) => PlayFile(lstFiles.SelectedIndex >= 0 ? lstFiles.SelectedIndex : 0);
            bNext.Click += (s, e) => PlayFile(fileIndex + 1);
            bDel.Click += (s, e) => { if (lstFiles.SelectedIndex >= 0) lstFiles.Items.RemoveAt(lstFiles.SelectedIndex); };
            bClr.Click += (s, e) => lstFiles.Items.Clear();
            lstFiles.DoubleClick += (s, e) => { if (lstFiles.SelectedIndex >= 0) PlayFile(lstFiles.SelectedIndex); };
            lstFiles.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            lstFiles.DragDrop += (s, e) => AddFilePaths((string[])e.Data.GetData(DataFormats.FileDrop));
            return p;
        }

        Panel BuildSettingsPage()
        {
            var p = NewPage("Ayarlar", null);
            p.AutoScroll = true;
            DarkScroll(p);

            // Hoparlör
            var c1 = new Card { Location = new Point(36, 80), Size = new Size(700, 214) };
            p.Controls.Add(c1);
            Theme.Lbl(c1, "Hoparlör", Theme.F(13f, FontStyle.Bold), Theme.Text, 20, 16);
            lstDevices = new DarkList { Location = new Point(20, 52), Size = new Size(420, 112), BackColor = Color.FromArgb(24, 24, 28) };
            lstDevices.SelectedIndexChanged += (s, e) => SelectDevice(lstDevices.SelectedItem as Renderer);
            c1.Controls.Add(lstDevices);
            DarkScroll(lstDevices);
            btnScan = Btn(c1, "Cihazları Ara", Theme.IcoSearch, 460, 52, 220, 40, true);
            btnScan.Click += (s, e) => Scan(null);
            var ipBox = new Card { Location = new Point(460, 102), Size = new Size(124, 38), Fill = Theme.CardHi, Radius = 10 };
            c1.Controls.Add(ipBox);
            txtIp = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.CardHi, ForeColor = Theme.Text, Font = Theme.F(10f), Location = new Point(12, 10), Width = 104 };
            ipBox.Controls.Add(txtIp);
            txtIp.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddByIp(); } };
            btnAddIp = Btn(c1, "IP ile Ekle", null, 590, 102, 90, 38);
            btnAddIp.Font = Theme.F(9.5f);
            btnAddIp.Click += (s, e) => AddByIp();
            Theme.Lbl(c1, "Bulunamazsa IP adresini girin", Theme.F(8.5f), Theme.Sub, 460, 146, 220, 18);
            lblInfo = Theme.Lbl(c1, "Henüz cihaz seçilmedi.", Theme.F(9f), Theme.Sub, 20, 178, 660, 20);

            // Ses
            var c2 = new Card { Location = new Point(36, 312), Size = new Size(700, 292) };
            p.Controls.Add(c2);
            Theme.Lbl(c2, "Ses", Theme.F(13f, FontStyle.Bold), Theme.Text, 20, 16);
            Theme.Lbl(c2, "Musaic'e gönderilecek ses aygıtı", Theme.Semi(9.5f), Theme.Sub, 20, 52);
            cmbSource = new DarkCombo { Location = new Point(20, 74), Width = 560 };
            c2.Controls.Add(cmbSource);
            DarkScroll(cmbSource, "DarkMode_CFD");
            var bRef = Btn(c2, "Yenile", Theme.IcoRefresh, 590, 72, 90, 34);
            bRef.Font = Theme.F(9.5f);
            bRef.Click += (s, e) => LoadSources();
            Theme.Lbl(c2, "Spotify/YouTube Music yönlendirmesi ve canlı yayın bu aygıtı kullanır. Önerilen: Steam Streaming Speakers (sanal, kendisi ses çıkarmaz).",
                Theme.F(8.5f), Theme.Sub, 20, 108, 660, 32);
            cmbSource.SelectedIndexChanged += (s, e) =>
            {
                if (loadingSources) return;
                var d = cmbSource.SelectedItem as AudioDevice;
                capture.DeviceId = d == null ? null : d.Id;
                try { Directory.CreateDirectory(SettingsDir); File.WriteAllText(SourceFile, d == null || d.Id == null ? "" : d.Id); } catch { }
                UpdateChips();
                UpdateMusicNote();
            };

            Theme.Lbl(c2, "Yayın biçimi", Theme.Semi(9.5f), Theme.Sub, 20, 148);
            cmbFormat = new DarkCombo { Location = new Point(20, 170), Width = 660 };
            cmbFormat.Items.AddRange(new object[] {
                "Maksimum kalite – 24-bit, Windows'un kendi hızında (dönüştürme yok)",
                "192 kHz / 24-bit – yüksek kalite, az gecikme (önerilen)",
                "48 kHz / 16-bit – standart",
                "192 kHz / 32-bit (deneysel)",
                "WAV sabit uzunluk (uyumluluk)",
                "L16 / ham PCM (uyumluluk)" });
            int savedFmt = 1;
            try { if (File.Exists(FormatFile)) savedFmt = int.Parse(File.ReadAllText(FormatFile).Trim()); } catch { }
            cmbFormat.SelectedIndex = savedFmt >= 0 && savedFmt < cmbFormat.Items.Count ? savedFmt : 1;
            cmbFormat.SelectedIndexChanged += (s, e) =>
            {
                try { Directory.CreateDirectory(SettingsDir); File.WriteAllText(FormatFile, cmbFormat.SelectedIndex.ToString()); } catch { }
                UpdateChips();
                if (playMode == "live") { restartTimer.Stop(); restartTimer.Start(); }
            };
            c2.Controls.Add(cmbFormat);
            DarkScroll(cmbFormat, "DarkMode_CFD");

            Theme.Lbl(c2, "Güvenlik payı", Theme.Semi(9.5f), Theme.Sub, 20, 222);
            var bMinus = new FlatButton { Circle = true, Glyph = Theme.IcoMinus, GlyphSize = 10f, Location = new Point(20, 244), Size = new Size(34, 34) };
            bMinus.Style(Theme.CardHi, Theme.CardPress, Theme.Track, Theme.Text);
            bMinus.Click += (s, e) => ChangeCushion(-100);
            c2.Controls.Add(bMinus);
            lblCushion = Theme.Lbl(c2, "", Theme.Semi(11f), Theme.Text, 60, 249, 110, 24);
            lblCushion.TextAlign = ContentAlignment.MiddleCenter;
            var bPlus = new FlatButton { Circle = true, Glyph = Theme.IcoAdd, GlyphSize = 10f, Location = new Point(176, 244), Size = new Size(34, 34) };
            bPlus.Style(Theme.CardHi, Theme.CardPress, Theme.Track, Theme.Text);
            bPlus.Click += (s, e) => ChangeCushion(100);
            c2.Controls.Add(bPlus);
            Theme.Lbl(c2, "Ses arada kesilip ~3 sn geriden gelmeye başlıyorsa artırın;\r\nkesilme yoksa azaltarak gecikmeyi düşürün.", Theme.F(8.5f), Theme.Sub, 228, 242, 450, 36);

            // Otomatik çalışma
            var c4 = new Card { Location = new Point(36, 622), Size = new Size(700, 300) };
            p.Controls.Add(c4);
            Theme.Lbl(c4, "Otomatik çalışma", Theme.F(13f, FontStyle.Bold), Theme.Text, 20, 16);
            Theme.Lbl(c4, "Sürekli açık bir bilgisayarı (ör. mini PC) Musaic için müzik merkezi yapmak için.", Theme.F(8.5f), Theme.Sub, 20, 44, 660, 18);
            tglAutoRun = NewToggle(c4, "Windows açılınca başlat", "Oturum açıldığında tepside gizli olarak başlar.", 72);
            tglTray = NewToggle(c4, "Kapatınca tepside çalışmaya devam et", "Pencereyi kapatmak uygulamayı kapatmaz; saatin yanındaki simgeden açılır.", 126);
            tglAutoStream = NewToggle(c4, "Yayını otomatik başlat ve koru", "Hoparlör bulununca yayını başlatır; hoparlör yeniden başlarsa yayını kendiliğinden toparlar.", 180);
            tglAutoSpotify = NewToggle(c4, "Spotify'ı her zaman Musaic'e gönder", "Bu bilgisayarda Spotify çaldığı anda sesi Musaic'e yönlendirilir.", 234);
            tglAutoRun.SetSilently(AutoRunEnabled());
            tglTray.SetSilently(GetOpt("tepsi", false));
            tglAutoStream.SetSilently(GetOpt("oto_yayin", false));
            tglAutoSpotify.SetSilently(GetOpt("oto_spotify", false));
            tglAutoRun.CheckedChanged += (s, e) => SetAutoRun(tglAutoRun.Checked);
            tglTray.CheckedChanged += (s, e) => { SetOpt("tepsi", tglTray.Checked); tray.Visible = tglTray.Checked || startHidden; };
            tglAutoStream.CheckedChanged += (s, e) => { SetOpt("oto_yayin", tglAutoStream.Checked); if (tglAutoStream.Checked && playMode == "none" && cur != null) StartLive(); };
            tglAutoSpotify.CheckedChanged += (s, e) =>
            {
                SetOpt("oto_spotify", tglAutoSpotify.Checked);
                if (tglAutoSpotify.Checked) { AudioDevice d; if (UseMusaicOutput(out d)) { spRouted = true; ReapplyRoutes(); } }
                CheckSpotify();
            };

            // Günlük
            var c3 = new Card { Location = new Point(36, 940), Size = new Size(700, 220) };
            p.Controls.Add(c3);
            Theme.Lbl(c3, "Günlük", Theme.F(13f, FontStyle.Bold), Theme.Text, 20, 16);
            txtLog = new TextBox
            {
                Location = new Point(20, 50), Size = new Size(660, 152), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Sub, Font = new Font("Consolas", 9f)
            };
            c3.Controls.Add(txtLog);
            DarkScroll(txtLog);
            var spacer = new Panel { Location = new Point(0, 1160), Size = new Size(10, 24), BackColor = Theme.Bg };
            p.Controls.Add(spacer);
            return p;
        }

        // ================================================================ Durum güncellemeleri

        void UpdateBigButton()
        {
            bool live = playMode == "live";
            btnBig.Glyph = live ? Theme.IcoStop : Theme.IcoPlay;
            btnBig.Invalidate();
            lblBigTitle.Text = live ? "Yayın açık" : "PC sesini hoparlöre gönder";
            lblBigDesc.Text = live
                ? "Seçili ses aygıtında çalan her şey şu an Musaic'ten çalıyor. Durdurmak için düğmeye basın."
                : "YouTube, Spotify, oyunlar… seçili ses aygıtında çalan her şey Musaic'ten çalar.";
        }

        void UpdateChips()
        {
            if (lblChips == null || cmbFormat == null) return;
            string[] fmt = { "Maksimum kalite (24-bit)", "192 kHz / 24-bit", "48 kHz / 16-bit", "192 kHz / 32-bit", "WAV sabit", "L16" };
            int i = cmbFormat.SelectedIndex;
            var d = cmbSource == null ? null : cmbSource.SelectedItem as AudioDevice;
            string src = d == null || d.Id == null ? "Windows varsayılanı" : ShortName(d.Name);
            lblChips.Text = (i >= 0 && i < fmt.Length ? fmt[i] : "") + "   •   Pay " + cushionMs + " ms   •   Kaynak: " + src;
        }

        static string ShortName(string n)
        {
            var m = Regex.Match(n ?? "", @"\(([^)]*)\)");
            return m.Success ? m.Groups[1].Value : n;
        }

        void UpdateMuteGlyph()
        {
            btnMute.Glyph = muted ? Theme.IcoMute : Theme.IcoVolume;
            btnMute.TextColor = muted ? Theme.Bad : Theme.Text;
            btnMute.Invalidate();
            sldVol.Fill = muted ? Theme.Track : Theme.Accent;
            sldVol.Invalidate();
        }

        void UpdateCushionLabel()
        {
            if (lblCushion != null) lblCushion.Text = cushionMs + " ms";
            UpdateChips();
        }

        void SetSpeakerStatus(string text, Color dot)
        {
            sbDot.ForeColor = dot;
            lblBigSub.Text = text;
            var hint = sidebar.Controls.Find("sbHint", true);
            if (hint.Length > 0) hint[0].Text = text.Length > 30 ? text.Substring(0, 30) + "…" : text;
        }

        // ================================================================ Açılış

        bool loadedOnce;

        void OnLoaded()
        {
            if (loadedOnce) return;
            loadedOnce = true;
            try
            {
                server.Start();
                Log("Yayın sunucusu hazır (port " + server.Port + ").");
            }
            catch (Exception ex) { Log("HATA: " + ex.Message); }

            string last = null;
            try { if (File.Exists(LastDeviceFile)) last = File.ReadAllText(LastDeviceFile).Trim(); } catch { }
            if (!string.IsNullOrEmpty(last))
            {
                Log("Son kullanılan hoparlöre bağlanılıyor…");
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var r = Upnp.Load(last);
                        if (r.AvtControl != null) UI(() => AddDevice(r, true));
                    }
                    catch { UI(() => { Log("Son hoparlöre ulaşılamadı, aranıyor…"); Scan(null); }); }
                });
            }
            else Scan(null);
            UpdateMusicNote();
        }

        void LoadSources()
        {
            loadingSources = true;
            string saved = null;
            try { if (File.Exists(SourceFile)) saved = File.ReadAllText(SourceFile).Trim(); } catch { }
            var cur0 = cmbSource.SelectedItem as AudioDevice;
            string want = cur0 != null ? cur0.Id : saved;
            cmbSource.Items.Clear();
            var def = new AudioDevice { Id = null, Name = "Windows'un varsayılan çıkışı (şu an ne seçiliyse)" };
            cmbSource.Items.Add(def);
            cmbSource.SelectedItem = def;
            try
            {
                foreach (var d in AudioDevices.ListOutputs())
                {
                    cmbSource.Items.Add(d);
                    if (!string.IsNullOrEmpty(want) && d.Id == want) cmbSource.SelectedItem = d;
                }
            }
            catch (Exception ex) { Log("Ses aygıtları listelenemedi: " + ex.Message); }
            var sel = cmbSource.SelectedItem as AudioDevice;
            capture.DeviceId = sel == null ? null : sel.Id;
            loadingSources = false;
            UpdateChips();
        }

        static string DeviceName(string id)
        {
            try { foreach (var d in AudioDevices.ListOutputs()) if (d.Id == id) return d.Name; } catch { }
            return id;
        }

        void ChangeCushion(int delta)
        {
            int next = Math.Max(0, Math.Min(3000, cushionMs + delta));
            if (next == cushionMs) return;
            cushionMs = next;
            server.CushionMs = cushionMs;
            try { Directory.CreateDirectory(SettingsDir); File.WriteAllText(CushionFile, cushionMs.ToString()); } catch { }
            UpdateCushionLabel();
            if (playMode == "live") { restartTimer.Stop(); restartTimer.Start(); }
        }

        // ================================================================ Yardımcılar

        void UI(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(a); } catch { } }
            else a();
        }

        void Log(string msg)
        {
            UI(() =>
            {
                txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\r\n");
                statusBar.Text = msg;
            });
        }

        void Act(string name, Action a)
        {
            if (cur == null) { if (name != null) Log("Önce Ayarlar'dan bir hoparlör seçin."); return; }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { a(); if (name != null) Log(name + " gönderildi."); }
                catch (Exception ex) { Log("HATA: " + ex.Message); }
            });
        }

        string AvT(string action, string args)
        {
            var r = cur;
            if (r == null || r.AvtControl == null) throw new Exception("Cihazda AVTransport servisi yok.");
            return Upnp.Soap(r.AvtControl, r.AvtType, action, args);
        }

        string Rc(string action, string args)
        {
            var r = cur;
            if (r == null || r.RcControl == null) throw new Exception("Cihaz ses kontrolünü (RenderingControl) desteklemiyor.");
            return Upnp.Soap(r.RcControl, r.RcType, action, args);
        }

        // ================================================================ Cihaz bulma

        void Scan(IPAddress unicast)
        {
            btnScan.Enabled = false;
            btnAddIp.Enabled = false;
            Log(unicast == null ? "Ağda hoparlör aranıyor (yaklaşık 4 sn)…" : unicast + " adresinde cihaz aranıyor…");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var locs = new List<string>();
                try { locs = Upnp.Discover(unicast, unicast == null ? 4000 : 3000); }
                catch (Exception ex) { Log("Arama hatası: " + ex.Message); }

                if (unicast != null && locs.Count == 0)
                {
                    foreach (var port in new[] { 2870, 49152, 49153, 49154, 8080, 80, 1400, 8200, 55000 })
                        foreach (var path in new[] { "/dmr.xml", "/description.xml", "/DeviceDescription.xml", "/rootDesc.xml" })
                            locs.Add("http://" + unicast + ":" + port + path);
                }

                int renderers = 0;
                foreach (var loc in locs)
                {
                    try
                    {
                        var r = Upnp.Load(loc);
                        if (r.AvtControl != null) { renderers++; UI(() => AddDevice(r, unicast != null)); }
                    }
                    catch { }
                }
                if (renderers == 0)
                {
                    Log("Çalabilir hoparlör bulunamadı. Hoparlörün açık ve aynı Wi-Fi'de olduğundan emin olun.");
                    UI(() => { if (cur == null) { sbName.Text = "Hoparlör bulunamadı"; SetSpeakerStatus("Hoparlör bulunamadı — Ayarlar'dan arayın", Theme.Bad); } });
                }
                UI(() => { btnScan.Enabled = true; btnAddIp.Enabled = true; });
            });
        }

        void AddByIp()
        {
            string t = txtIp.Text.Trim();
            if (t == "") return;
            IPAddress ip;
            if (!IPAddress.TryParse(t, out ip)) { Log("Geçerli bir IP adresi girin (ör. 192.168.1.45)."); return; }
            Scan(ip);
        }

        void AddDevice(Renderer r, bool select)
        {
            foreach (Renderer x in lstDevices.Items)
                if (x.Location == r.Location) { if (select) lstDevices.SelectedItem = x; return; }
            lstDevices.Items.Add(r);
            Log("Bulundu: " + r.Name + " (" + r.Host + ")");
            bool isMusaic = (r.Name + r.Manufacturer + r.Model).IndexOf("musaic", StringComparison.OrdinalIgnoreCase) >= 0;
            if (select || lstDevices.SelectedIndex < 0 || isMusaic) lstDevices.SelectedItem = r;
        }

        void SelectDevice(Renderer r)
        {
            if (r == null || r == cur) return;
            cur = r;
            lastState = "";
            lblInfo.Text = "Seçili: " + r.Name + "  ·  " + r.Manufacturer + " " + r.Model + "  ·  " + r.Host + (r.RcControl == null ? "  (ses kontrolü yok)" : "");
            lblBigName.Text = r.Name;
            sbName.Text = r.Name;
            sbIp.Text = r.Host;
            SetSpeakerStatus("Bağlı", Theme.Good);
            try { Directory.CreateDirectory(SettingsDir); File.WriteAllText(LastDeviceFile, r.Location); } catch { }
            CheckSpotify();
            if (GetOpt("oto_yayin", false) && !autoStreamStarted && playMode == "none")
            {
                autoStreamStarted = true;
                Log("Otomatik yayın başlatılıyor…");
                StartLive();
            }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string v = Upnp.GetTag(Rc("GetVolume", "<InstanceID>0</InstanceID><Channel>Master</Channel>"), "CurrentVolume");
                    string m = "";
                    try { m = Upnp.GetTag(Rc("GetMute", "<InstanceID>0</InstanceID><Channel>Master</Channel>"), "CurrentMute"); } catch { }
                    int vi;
                    if (int.TryParse(v, out vi))
                        UI(() =>
                        {
                            sldVol.SetSilently(vi);
                            lblVol.Text = sldVol.Value.ToString();
                            muted = m == "1" || m.Equals("true", StringComparison.OrdinalIgnoreCase);
                            UpdateMuteGlyph();
                        });
                }
                catch (Exception ex) { Log("Ses seviyesi okunamadı: " + ex.Message); }
            });
        }

        // ================================================================ Çalma

        void SetUriAndPlay(string url, string title, string mime, bool live)
        {
            try { AvT("Stop", "<InstanceID>0</InstanceID>"); } catch { }
            string meta = Upnp.Didl(title, url, mime, live);
            try
            {
                AvT("SetAVTransportURI", "<InstanceID>0</InstanceID><CurrentURI>" + Upnp.Esc(url) + "</CurrentURI><CurrentURIMetaData>" + Upnp.Esc(meta) + "</CurrentURIMetaData>");
            }
            catch
            {
                AvT("SetAVTransportURI", "<InstanceID>0</InstanceID><CurrentURI>" + Upnp.Esc(url) + "</CurrentURI><CurrentURIMetaData></CurrentURIMetaData>");
            }
            AvT("Play", "<InstanceID>0</InstanceID><Speed>1</Speed>");

            // Musaic ilk Play komutunu bazen yok sayıyor; birkaç saniye içinde çalmaya başlamazsa tekrar dene.
            for (int i = 0; i < 2; i++)
            {
                Thread.Sleep(2500);
                string state = "";
                try { state = Upnp.GetTag(AvT("GetTransportInfo", "<InstanceID>0</InstanceID>"), "CurrentTransportState"); } catch { }
                if (state == "PLAYING" || state == "TRANSITIONING") return;
                Log("Hoparlör başlamadı (" + state + "), Play tekrar gönderiliyor…");
                AvT("Play", "<InstanceID>0</InstanceID><Speed>1</Speed>");
            }
        }

        void StartLive()
        {
            if (cur == null) { Log("Önce Ayarlar'dan bir hoparlör seçin."); ShowPage(3); return; }
            int fmt = cmbFormat.SelectedIndex;
            var r = cur;
            btnBig.Enabled = false;
            playMode = "live";
            UpdateBigButton();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (!capture.IsRunning) capture.Start();
                    server.SampleRate = capture.SampleRate;
                    server.LiveActive = true;
                    string ip = Upnp.LocalIpFor(r.Host);
                    string url, mime = "audio/wav";
                    string baseUrl = "http://" + ip + ":" + server.Port;
                    switch (fmt)
                    {
                        case 0: url = baseUrl + "/live.wav?bits=24"; break;
                        case 1: url = baseUrl + "/live.wav?rate=192000&bits=24"; break;
                        case 3: url = baseUrl + "/live.wav?rate=192000&bits=32"; break;
                        case 4: url = baseUrl + "/live.wav?fixed=1"; break;
                        case 5: url = baseUrl + "/live.l16"; mime = "audio/L16;rate=" + capture.SampleRate + ";channels=2"; break;
                        default: url = baseUrl + "/live.wav"; break;
                    }
                    Log("PC sesi yakalanıyor (" + capture.SampleRate + " Hz), yayın başlatılıyor…");
                    userStopped = false;
                    SetUriAndPlay(url, "PC Sesi", mime, true);
                    Log("Yayın açık.");
                }
                catch (Exception ex)
                {
                    Log("HATA: " + ex.Message + "  → Ayarlar'dan başka bir 'Yayın biçimi' deneyin.");
                    playMode = "none";
                }
                UI(() => { btnBig.Enabled = true; UpdateBigButton(); });
            });
        }

        void StopAll()
        {
            userStopped = true;
            bool wasLive = playMode == "live";
            playMode = "none";
            UpdateBigButton();
            Act("Durdur", () =>
            {
                try { AvT("Stop", "<InstanceID>0</InstanceID>"); }
                finally { if (wasLive) { server.LiveActive = false; capture.Stop(); } }
            });
            if (cur == null && wasLive) { server.LiveActive = false; capture.Stop(); }
        }

        void AddFiles()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = "Ses dosyaları|*.mp3;*.flac;*.wav;*.m4a;*.aac;*.ogg;*.wma;*.aif;*.aiff|Tüm dosyalar|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK) AddFilePaths(dlg.FileNames);
            }
        }

        void AddFilePaths(string[] paths)
        {
            var exts = new[] { ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".aif", ".aiff" };
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    foreach (var f in Directory.GetFiles(p, "*.*", SearchOption.AllDirectories).OrderBy(x => x))
                        if (exts.Contains(Path.GetExtension(f).ToLowerInvariant())) lstFiles.Items.Add(f);
                }
                else if (File.Exists(p)) lstFiles.Items.Add(p);
            }
        }

        void PlayFile(int index)
        {
            if (cur == null) { Log("Önce Ayarlar'dan bir hoparlör seçin."); return; }
            if (lstFiles.Items.Count == 0) { Log("Önce listeye dosya ekleyin."); return; }
            if (index >= lstFiles.Items.Count) { Log("Listenin sonuna gelindi."); playMode = "none"; return; }
            if (index < 0) index = 0;
            string file = (string)lstFiles.Items[index];
            lstFiles.SelectedIndex = index;
            fileIndex = index;
            var r = cur;

            if (playMode == "live") { server.LiveActive = false; capture.Stop(); }
            playMode = "file";
            UpdateBigButton();
            userStopped = false;
            lastState = "";
            playStartedAt = DateTime.Now;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    int id = server.RegisterFile(file);
                    string ip = Upnp.LocalIpFor(r.Host);
                    string url = "http://" + ip + ":" + server.Port + "/f/" + id + Path.GetExtension(file).ToLowerInvariant();
                    SetUriAndPlay(url, Path.GetFileNameWithoutExtension(file), StreamServer.MimeFor(file), false);
                    Log("Çalınıyor: " + Path.GetFileName(file));
                }
                catch (Exception ex) { Log("HATA: " + ex.Message); }
            });
        }

        void Poll()
        {
            var r = cur;
            if (r == null || polling) return;
            polling = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string resp = Upnp.Soap(r.AvtControl, r.AvtType, "GetTransportInfo", "<InstanceID>0</InstanceID>");
                    string state = Upnp.GetTag(resp, "CurrentTransportState");
                    UI(() =>
                    {
                        string what = playMode == "live" ? "PC sesi yayınlanıyor" :
                                      playMode == "file" && fileIndex >= 0 && fileIndex < lstFiles.Items.Count ? Path.GetFileNameWithoutExtension((string)lstFiles.Items[fileIndex]) : "";
                        string st = Turkish(state);
                        SetSpeakerStatus(st + (what != "" ? "  •  " + what : ""), state == "PLAYING" ? Theme.Accent : Theme.Good);
                        if (playMode == "file" && !userStopped && lastState == "PLAYING" && state == "STOPPED" &&
                            (DateTime.Now - playStartedAt).TotalSeconds > 4)
                        {
                            PlayFile(fileIndex + 1);
                            return;
                        }
                        if (state != "TRANSITIONING") lastState = state;

                        // Otomatik yayın açıksa ve yayın düştüyse (ör. hoparlör yeniden başladı) 12 sn sonra toparla
                        bool shouldStream = GetOpt("oto_yayin", false) && !userStopped && (playMode == "live" || playMode == "none");
                        if (shouldStream && state != "PLAYING" && state != "TRANSITIONING")
                        {
                            if (stoppedSince == DateTime.MaxValue) stoppedSince = DateTime.Now;
                            else if ((DateTime.Now - stoppedSince).TotalSeconds > 12)
                            {
                                stoppedSince = DateTime.MaxValue;
                                Log("Yayın durmuş görünüyor; otomatik olarak yeniden başlatılıyor…");
                                StartLive();
                            }
                        }
                        else stoppedSince = DateTime.MaxValue;
                    });
                }
                catch { UI(() => SetSpeakerStatus("Hoparlöre ulaşılamıyor", Theme.Bad)); }
                finally { polling = false; }
            });
        }

        static string Turkish(string s)
        {
            switch (s)
            {
                case "PLAYING": return "Çalıyor";
                case "PAUSED_PLAYBACK": return "Duraklatıldı";
                case "STOPPED": return "Hazır";
                case "TRANSITIONING": return "Yükleniyor…";
                case "NO_MEDIA_PRESENT": return "Hazır";
                default: return s == "" ? "Bağlı" : s;
            }
        }

        // ================================================================ Spotify / YouTube Music

        // Bilgisayardaki Spotify uygulamasının durumunu gösterir
        void CheckSpotify()
        {
            if (lblSpStatus == null) return;
            bool running = Process.GetProcessesByName("Spotify").Length > 0;
            if (spRouted && running) { lblSpStatus.Text = "● Spotify → Musaic"; lblSpStatus.ForeColor = Theme.Spotify; }
            else if (running) { lblSpStatus.Text = "● Spotify açık (sesi şu an kulaklıkta)"; lblSpStatus.ForeColor = Theme.Sub; }
            else { lblSpStatus.Text = "● Spotify kapalı"; lblSpStatus.ForeColor = Theme.Sub; }
        }

        void OpenSpotify()
        {
            try { Process.Start("spotify:"); }
            catch (Exception ex) { Log("Spotify açılamadı: " + ex.Message + " — Spotify uygulamasının kurulu olduğundan emin olun."); }
        }

        void PlaySpotifyOnMusaic()
        {
            if (Process.GetProcessesByName("Spotify").Length == 0)
            {
                AudioDevice d;
                if (!UseMusaicOutput(out d)) return;
                OpenSpotify();
                spRouted = true;
                Log("Spotify açılıyor; bir şarkı başlattığınız anda sesi Musaic'e yönlendirilecek.");
                if (playMode != "live") StartLive();
            }
            else RouteApp("Spotify", "Spotify", ref spRouted);
            CheckSpotify();
        }

        void UnrouteSpotify()
        {
            try
            {
                AppAudioRouter.Route("Spotify", null);
                spRouted = false;
                CheckSpotify();
                Log("Spotify yeniden Windows'un varsayılan çıkışını (kulaklık) kullanıyor.");
            }
            catch (Exception ex) { Log("Yönlendirme kaldırılamadı: " + ex.Message); }
        }

        // Yönlendirme ve yakalama için kullanılacak sanal aygıt
        AudioDevice MusaicOutput()
        {
            var sel = cmbSource.SelectedItem as AudioDevice;
            if (sel != null && sel.Id != null) return sel;
            foreach (var o in cmbSource.Items)
            {
                var d = o as AudioDevice;
                if (d == null || d.Id == null) continue;
                string n = d.Name.ToLowerInvariant();
                if (n.Contains("musaic") || n.Contains("steam streaming speakers") || n.Contains("cable input")) return d;
            }
            return null;
        }

        void UpdateMusicNote()
        {
            if (lblMusicNote == null) return;
            var d = MusaicOutput();
            lblMusicNote.Text = d == null
                ? "Uygulama yönlendirmesi için sanal bir ses aygıtı gerekir (ör. Steam Streaming Speakers veya VB-Audio Cable). Ayarlar → Ses'ten seçin."
                : "Spotify ve YouTube Music sesi şu aygıt üzerinden Musaic'e iletilir: " + d.Name;
        }

        bool UseMusaicOutput(out AudioDevice d)
        {
            d = MusaicOutput();
            if (d == null)
            {
                Log("Sanal ses aygıtı bulunamadı. Ayarlar → Ses bölümünden bir aygıt seçin.");
                ShowPage(3);
                return false;
            }
            // Yakalama kaynağını bu aygıta sabitle
            if (cmbSource.SelectedItem != d) cmbSource.SelectedItem = d;
            capture.DeviceId = d.Id;
            return true;
        }

        void RouteApp(string processName, string label, ref bool flag)
        {
            AudioDevice d;
            if (!UseMusaicOutput(out d)) return;
            if (Process.GetProcessesByName(processName).Length == 0)
            {
                Log(label + " çalışmıyor — önce uygulamayı açın.");
                if (processName == "Spotify") OpenSpotify();
                return;
            }
            try
            {
                int n = AppAudioRouter.Route(processName, d.Id);
                flag = true;
                Log(n > 0
                    ? label + " sesi → " + d.Name + ". Musaic'e gönderiliyor."
                    : label + " çalmaya başladığı anda sesi otomatik olarak Musaic'e yönlendirilecek.");
                if (playMode != "live") StartLive();
            }
            catch (Exception ex)
            {
                Log("Uygulama yönlendirmesi yapılamadı (" + ex.Message + "). Windows Ses Karıştırıcısı'ndan " + label + " çıkışını elle '" + d.Name + "' yapın.");
            }
        }

        string FindEdge()
        {
            foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
            {
                if (string.IsNullOrEmpty(root)) continue;
                string p = Path.Combine(root, @"Microsoft\Edge\Application\msedge.exe");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        void OpenYouTubeMusic()
        {
            AudioDevice d;
            if (!UseMusaicOutput(out d)) return;
            string edge = FindEdge();
            if (edge == null)
            {
                try { Process.Start("https://music.youtube.com"); } catch { }
                Log("Microsoft Edge bulunamadı; YouTube Music tarayıcıda açıldı. Ses Karıştırıcısı'ndan tarayıcının çıkışını '" + d.Name + "' yapın.");
                return;
            }
            try
            {
                Process.Start(edge, "--app=https://music.youtube.com/ --new-window");
                ytRouted = true;
                lblYtStatus.Text = "● Açık — sesi Musaic'e gidiyor";
                lblYtStatus.ForeColor = Theme.YouTube;
                Log("YouTube Music açılıyor; bir şarkı başlattığınız anda sesi Musaic'e yönlendirilecek.");
                if (playMode != "live") StartLive();
                // Edge'in ses süreci çalmaya başlayınca oluşur; birkaç kez yeniden uygula
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    for (int i = 0; i < 6; i++) { Thread.Sleep(1500); try { AppAudioRouter.Route("msedge", d.Id); } catch { } }
                });
            }
            catch (Exception ex) { Log("YouTube Music açılamadı: " + ex.Message); }
        }

        void ReapplyRoutes()
        {
            if (!ytRouted && !spRouted) return;
            var d = MusaicOutput();
            if (d == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (ytRouted) AppAudioRouter.Route("msedge", d.Id);
                    if (spRouted) AppAudioRouter.Route("Spotify", d.Id);
                }
                catch { }
            });
        }

        void Unroute()
        {
            try
            {
                AppAudioRouter.Route("msedge", null);
                AppAudioRouter.Route("Spotify", null);
                ytRouted = spRouted = false;
                lblYtStatus.Text = "● Kapalı";
                lblYtStatus.ForeColor = Theme.Sub;
                CheckSpotify();
                Log("Uygulama yönlendirmeleri kaldırıldı; Spotify ve Edge yeniden Windows'un varsayılan çıkışını kullanıyor.");
            }
            catch (Exception ex) { Log("Yönlendirme kaldırılamadı: " + ex.Message); }
        }
    }
}
