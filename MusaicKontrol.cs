// Musaic Kontrol - Musaic MP5 (ve diğer UPnP/DLNA hoparlörler) için PC kontrol uygulaması.
// Derleme: derle.bat  (Windows'un yerleşik .NET Framework 4 C# derleyicisi kullanılır)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MusaicKontrol
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.Expect100Continue = false;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ------------------------------------------------------------------ UPnP

    class Renderer
    {
        public string Name, Manufacturer, Model, Location, Host;
        public string AvtControl, AvtType, RcControl, RcType;
        public override string ToString() { return Name + "   (" + Host + ")"; }
    }

    static class Upnp
    {
        public static IEnumerable<IPAddress> LocalIPv4()
        {
            var list = new List<IPAddress>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) list.Add(ua.Address);
            }
            return list;
        }

        // SSDP ile ağdaki cihazların açıklama (LOCATION) adreslerini toplar.
        public static List<string> Discover(IPAddress unicastTarget, int timeoutMs)
        {
            var found = new List<string>();
            var sts = new[] {
                "urn:schemas-upnp-org:device:MediaRenderer:1",
                "urn:schemas-upnp-org:service:AVTransport:1",
                "ssdp:all" };
            var clients = new List<UdpClient>();
            foreach (var ip in LocalIPv4())
            {
                try
                {
                    var c = new UdpClient(new IPEndPoint(ip, 0));
                    c.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 4);
                    clients.Add(c);
                }
                catch { }
            }
            if (clients.Count == 0) clients.Add(new UdpClient(0));

            var target = unicastTarget != null
                ? new IPEndPoint(unicastTarget, 1900)
                : new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
            foreach (var c in clients)
            {
                foreach (var st in sts)
                {
                    string msg = "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: " + st + "\r\n\r\n";
                    byte[] b = Encoding.ASCII.GetBytes(msg);
                    for (int i = 0; i < 2; i++) { try { c.Send(b, b.Length, target); } catch { } }
                }
            }

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                foreach (var c in clients)
                {
                    try
                    {
                        while (c.Available > 0)
                        {
                            IPEndPoint ep = null;
                            string resp = Encoding.ASCII.GetString(c.Receive(ref ep));
                            foreach (var line in resp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                            {
                                if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string loc = line.Substring(9).Trim();
                                    if (!found.Contains(loc)) found.Add(loc);
                                }
                            }
                        }
                    }
                    catch { }
                }
                Thread.Sleep(50);
            }
            foreach (var c in clients) c.Close();
            return found;
        }

        public static string HttpGet(string url, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.KeepAlive = false;
            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        public static Renderer Load(string location)
        {
            string xml = HttpGet(location, 4000);
            var doc = XDocument.Parse(xml);
            Func<XElement, string, string> first = (root, name) =>
            {
                var e = root.Descendants().FirstOrDefault(x => x.Name.LocalName == name);
                return e == null ? "" : e.Value.Trim();
            };
            string baseUrl = first(doc.Root, "URLBase");
            if (baseUrl == "") baseUrl = location;

            var r = new Renderer();
            r.Location = location;
            r.Host = new Uri(location).Host;
            r.Name = first(doc.Root, "friendlyName");
            r.Manufacturer = first(doc.Root, "manufacturer");
            r.Model = first(doc.Root, "modelName");
            if (r.Name == "") r.Name = r.Model == "" ? "Bilinmeyen cihaz" : r.Model;

            foreach (var s in doc.Descendants().Where(x => x.Name.LocalName == "service"))
            {
                string type = first(s, "serviceType");
                string ctrl = first(s, "controlURL");
                if (ctrl == "") continue;
                string abs = new Uri(new Uri(baseUrl), ctrl).ToString();
                if (type.Contains(":AVTransport:") && r.AvtControl == null) { r.AvtControl = abs; r.AvtType = type; }
                if (type.Contains(":RenderingControl:") && r.RcControl == null) { r.RcControl = abs; r.RcType = type; }
            }
            return r;
        }

        public static string Soap(string controlUrl, string serviceType, string action, string argsXml)
        {
            string body = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<s:Body><u:" + action + " xmlns:u=\"" + serviceType + "\">" + argsXml + "</u:" + action + "></s:Body></s:Envelope>";
            byte[] data = Encoding.UTF8.GetBytes(body);
            var req = (HttpWebRequest)WebRequest.Create(controlUrl);
            req.Method = "POST";
            req.ContentType = "text/xml; charset=\"utf-8\"";
            req.Headers.Add("SOAPACTION", "\"" + serviceType + "#" + action + "\"");
            req.Timeout = 6000;
            req.KeepAlive = false;
            req.ContentLength = data.Length;
            try
            {
                using (var rs = req.GetRequestStream()) rs.Write(data, 0, data.Length);
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch (WebException ex)
            {
                string err = "";
                if (ex.Response != null)
                {
                    try { using (var sr = new StreamReader(ex.Response.GetResponseStream())) err = sr.ReadToEnd(); } catch { }
                }
                string detail = (GetTag(err, "errorCode") + " " + GetTag(err, "errorDescription")).Trim();
                throw new Exception(action + " başarısız: " + (detail != "" ? "UPnP hata " + detail : ex.Message));
            }
        }

        public static string GetTag(string xml, string tag)
        {
            if (string.IsNullOrEmpty(xml)) return "";
            var m = Regex.Match(xml, "<(?:\\w+:)?" + tag + "(?:\\s[^>]*)?>(.*?)</(?:\\w+:)?" + tag + ">", RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        public static string Esc(string s) { return SecurityElement.Escape(s ?? ""); }

        public static string Didl(string title, string url, string mime, bool live)
        {
            string dlna = live ? "DLNA.ORG_OP=00;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01700000000000000000000000000000"
                               : "DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01700000000000000000000000000000";
            return "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\">" +
                "<item id=\"1\" parentID=\"0\" restricted=\"1\"><dc:title>" + Esc(title) + "</dc:title>" +
                "<upnp:class>object.item.audioItem.musicTrack</upnp:class>" +
                "<res protocolInfo=\"http-get:*:" + Esc(mime) + ":" + dlna + "\">" + Esc(url) + "</res></item></DIDL-Lite>";
        }

        // Hoparlöre hangi yerel IP'den ulaşıldığını bulur (yayın adresinde kullanılır).
        public static string LocalIpFor(string host)
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect(host, 1900);
                return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
            }
        }
    }

    // ------------------------------------------------------------------ WASAPI loopback (PC sesini yakalama)

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey { public Guid FmtId; public int Pid; }

    [StructLayout(LayoutKind.Sequential)]
    struct PropVariant { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    class AudioDevice
    {
        public string Id, Name;
        public override string ToString() { return Name; }
    }

    static class AudioDevices
    {
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant pv);

        // Etkin çıkış (hoparlör) aygıtlarını listeler.
        public static List<AudioDevice> ListOutputs()
        {
            var list = new List<AudioDevice>();
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDeviceCollection col;
            if (en.EnumAudioEndpoints(0 /*eRender*/, 1 /*ACTIVE*/, out col) < 0) return list;
            uint n;
            col.GetCount(out n);
            var key = new PropertyKey { FmtId = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 14 }; // FriendlyName
            for (uint i = 0; i < n; i++)
            {
                IMMDevice dev;
                if (col.Item(i, out dev) < 0) continue;
                string id;
                dev.GetId(out id);
                string name = id;
                IPropertyStore ps;
                if (dev.OpenPropertyStore(0, out ps) >= 0)
                {
                    PropVariant pv;
                    if (ps.GetValue(ref key, out pv) >= 0)
                    {
                        if (pv.vt == 31 /*VT_LPWSTR*/) name = Marshal.PtrToStringUni(pv.p);
                        PropVariantClear(ref pv);
                    }
                }
                list.Add(new AudioDevice { Id = id, Name = name });
            }
            return list;
        }
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defPeriod, out long minPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    // Varsayılan çıkış aygıtına giden sesi yakalar; stereo 32-bit float (araya geçmeli) üretir.
    class LoopbackCapture
    {
        public int SampleRate = 48000;
        public Action<float[]> OnData;
        public volatile string DeviceId; // null: Windows'un varsayılan çıkışı (değişirse otomatik takip edilir)
        public Action<string, bool> OnDeviceChanged; // (yeni aygıt kimliği, örnekleme hızı değişti mi)
        Thread thread;
        volatile bool running;
        string error;
        ManualResetEvent ready;

        static readonly Guid FloatSubFormat = new Guid("00000003-0000-0010-8000-00aa00389b71");

        public bool IsRunning { get { return running; } }

        public void Start()
        {
            if (running) return;
            error = null;
            running = true;
            ready = new ManualResetEvent(false);
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            ready.WaitOne(5000);
            if (error != null) { running = false; throw new Exception("Ses yakalama başlatılamadı: " + error); }
        }

        public void Stop()
        {
            running = false;
            if (thread != null) thread.Join(1500);
            thread = null;
        }

        static void Check(int hr)
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        }

        void Run()
        {
            bool started = false;
            while (running)
            {
                try { RunOnce(ref started); }
                catch (Exception ex)
                {
                    if (!started)
                    {
                        error = ex.Message;
                        running = false;
                        if (ready != null) ready.Set();
                        return;
                    }
                    Thread.Sleep(1000); // aygıt geçici olarak kayboldu; tekrar dene
                }
            }
        }

        static string DefaultOutputId(IMMDeviceEnumerator en)
        {
            IMMDevice d;
            if (en.GetDefaultAudioEndpoint(0, 1, out d) < 0) return null;
            string id;
            d.GetId(out id);
            return id;
        }

        // Seçili aygıtı (ya da varsayılanı) yakalar. Windows'un varsayılan çıkışı ya da seçim değişince döner,
        // böylece Run yeni aygıtla yeniden başlar.
        void RunOnce(ref bool started)
        {
            IAudioClient client = null;
            IAudioCaptureClient cap = null;
            try
            {
                var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice dev;
                string devId = DeviceId;
                bool followDefault = string.IsNullOrEmpty(devId);
                if (!followDefault) Check(en.GetDevice(devId, out dev));
                else Check(en.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out dev));
                string curId;
                dev.GetId(out curId);
                Guid iidClient = typeof(IAudioClient).GUID;
                object o;
                Check(dev.Activate(ref iidClient, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out o));
                client = (IAudioClient)o;

                IntPtr fmt;
                Check(client.GetMixFormat(out fmt));
                int tag = (ushort)Marshal.ReadInt16(fmt, 0);
                int ch = Marshal.ReadInt16(fmt, 2);
                int rate = Marshal.ReadInt32(fmt, 4);
                int align = Marshal.ReadInt16(fmt, 12);
                bool isFloat = tag == 3;
                if (tag == 0xFFFE)
                {
                    byte[] g = new byte[16];
                    Marshal.Copy(IntPtr.Add(fmt, 24), g, 0, 16);
                    isFloat = new Guid(g) == FloatSubFormat;
                }
                Check(client.Initialize(0 /*shared*/, 0x00020000 /*LOOPBACK*/, 10000000, 0, fmt, IntPtr.Zero));
                Marshal.FreeCoTaskMem(fmt);

                Guid iidCap = typeof(IAudioCaptureClient).GUID;
                Check(client.GetService(ref iidCap, out o));
                cap = (IAudioCaptureClient)o;

                bool rateChanged = started && rate != SampleRate;
                SampleRate = rate;
                Check(client.Start());
                if (!started) { started = true; ready.Set(); }
                else
                {
                    var h = OnDeviceChanged;
                    if (h != null) h(curId, rateChanged);
                }

                var sw = Stopwatch.StartNew();
                long produced = 0;
                int cb = align / ch;
                long nextCheck = 1000;
                while (running)
                {
                    Thread.Sleep(10);
                    // Aygıt değişti mi? (Windows varsayılanı ya da uygulamadaki seçim)
                    if (sw.ElapsedMilliseconds >= nextCheck)
                    {
                        nextCheck = sw.ElapsedMilliseconds + 1000;
                        string want = DeviceId;
                        if (string.IsNullOrEmpty(want) != followDefault) break;
                        if (followDefault) { string d = DefaultOutputId(en); if (d != null && d != curId) break; }
                        else if (want != devId) break;
                    }
                    bool got = false;
                    uint n;
                    Check(cap.GetNextPacketSize(out n));
                    while (n > 0)
                    {
                        IntPtr data; uint frames, flags; ulong p1, p2;
                        Check(cap.GetBuffer(out data, out frames, out flags, out p1, out p2));
                        // Ses tam hassasiyette (32-bit float, stereo, araya geçmeli) taşınır.
                        float[] outb = new float[frames * 2];
                        if ((flags & 2) == 0 && frames > 0) // 2 = AUDCLNT_BUFFERFLAGS_SILENT
                        {
                            byte[] src = new byte[frames * align];
                            Marshal.Copy(data, src, 0, src.Length);
                            for (int f = 0; f < frames; f++)
                            {
                                for (int c = 0; c < 2; c++)
                                {
                                    int off = f * align + (c < ch ? c : 0) * cb;
                                    float v;
                                    if (isFloat && cb == 4) v = BitConverter.ToSingle(src, off);
                                    else if (cb == 2) v = BitConverter.ToInt16(src, off) / 32768f;
                                    else if (cb == 3) v = ((src[off] << 8) | (src[off + 1] << 16) | (src[off + 2] << 24)) / 2147483648f;
                                    else if (cb == 4) v = BitConverter.ToInt32(src, off) / 2147483648f;
                                    else v = 0f;
                                    outb[f * 2 + c] = v;
                                }
                            }
                        }
                        Check(cap.ReleaseBuffer(frames));
                        produced += frames;
                        got = true;
                        Emit(outb);
                        Check(cap.GetNextPacketSize(out n));
                    }
                    // Hiç ses çalınmıyorsa Windows paket göndermez; yayın kopmasın diye sessizlik ekle.
                    long expected = (long)(sw.Elapsed.TotalSeconds * rate);
                    // Küçük parçalar halinde doldur ki hoparlöre giden akış düzgün olsun (20 ms).
                    if (!got && expected - produced > rate / 50)
                    {
                        long pad = expected - produced;
                        Emit(new float[pad * 2]);
                        produced += pad;
                    }
                }
                client.Stop();
            }
            finally
            {
                if (cap != null) Marshal.ReleaseComObject(cap);
                if (client != null) Marshal.ReleaseComObject(client);
            }
        }

        void Emit(float[] b)
        {
            var h = OnData;
            if (h != null && b.Length > 0) h(b);
        }
    }

    // Float stereo sesi istenen hıza (windowed-sinc, Blackman pencereli) ve bit derinliğine çevirip
    // PCM baytlarına dönüştürür. Bağlantı başına durum tutar. Hızlar aynıysa dönüştürme yapmadan yalnızca nicemler.
    class PcmEncoder
    {
        const int H = 16;        // her yanda 16 örnek -> 32 dokunuşlu filtre
        const int Phases = 512;
        readonly int bytesPerSample;
        readonly bool bigEndian;
        readonly bool resample;
        readonly double step;
        readonly float[] table;  // [faz * 2H + dokunuş]
        double[] hl = new double[4096], hr = new double[4096];
        int len;
        double t;
        readonly Random rnd = new Random();

        public int Bits { get { return bytesPerSample * 8; } }

        public PcmEncoder(int srcRate, int dstRate, int bits, bool bigEndian)
        {
            bytesPerSample = bits / 8;
            this.bigEndian = bigEndian;
            resample = srcRate != dstRate;
            if (!resample) return;
            step = (double)srcRate / dstRate;
            double fc = Math.Min(1.0, (double)dstRate / srcRate) * 0.97; // aşağı örneklemede örtüşmeyi önle
            table = new float[(Phases + 1) * 2 * H];
            for (int p = 0; p <= Phases; p++)
            {
                double frac = (double)p / Phases;
                double sum = 0;
                for (int j = 0; j < 2 * H; j++)
                {
                    double d = (j - H + 1) - frac;
                    double x = Math.PI * fc * d;
                    double sinc = Math.Abs(d) < 1e-9 ? 1.0 : Math.Sin(x) / x;
                    double wpos = (d + H) / (2.0 * H);
                    double win = (wpos <= 0 || wpos >= 1) ? 0 : 0.42 - 0.5 * Math.Cos(2 * Math.PI * wpos) + 0.08 * Math.Cos(4 * Math.PI * wpos);
                    double v = fc * sinc * win;
                    table[p * 2 * H + j] = (float)v;
                    sum += v;
                }
                // DC kazancını tam 1 yap
                for (int j = 0; j < 2 * H; j++) table[p * 2 * H + j] = (float)(table[p * 2 * H + j] / sum);
            }
            len = H;   // başta H örnek sessizlik
            t = H - 1;
        }

        public byte[] Silence(long frames)
        {
            return Encode(new float[frames * 2]);
        }

        public byte[] Encode(float[] x)
        {
            int n = x.Length / 2;
            if (!resample)
            {
                var ob = new byte[n * 2 * bytesPerSample];
                int o = 0;
                for (int i = 0; i < x.Length; i++) o = Put(ob, o, x[i]);
                return ob;
            }

            if (len + n > hl.Length)
            {
                int cap = Math.Max(hl.Length * 2, len + n);
                Array.Resize(ref hl, cap);
                Array.Resize(ref hr, cap);
            }
            for (int i = 0; i < n; i++) { hl[len + i] = x[2 * i]; hr[len + i] = x[2 * i + 1]; }
            len += n;

            var outb = new byte[((int)((len - t) / step) + 2) * 2 * bytesPerSample];
            int pos = 0;
            int taps = 2 * H;
            while ((int)t + H < len)
            {
                int i0 = (int)t;
                double pf = (t - i0) * Phases;
                int p = (int)pf;
                double a = pf - p;
                int baseA = p * taps, baseB = (p + 1) * taps;
                int s0 = i0 - H + 1;
                double sl = 0, sr = 0;
                for (int j = 0; j < taps; j++)
                {
                    double w = table[baseA + j] + (table[baseB + j] - table[baseA + j]) * a;
                    sl += hl[s0 + j] * w;
                    sr += hr[s0 + j] * w;
                }
                pos = Put(outb, pos, sl);
                pos = Put(outb, pos, sr);
                t += step;
            }
            // Artık gerekmeyen eski örnekleri at
            int drop = (int)t - H + 1;
            if (drop > 0)
            {
                Array.Copy(hl, drop, hl, 0, len - drop);
                Array.Copy(hr, drop, hr, 0, len - drop);
                len -= drop;
                t -= drop;
            }
            if (pos == outb.Length) return outb;
            var res = new byte[pos];
            Buffer.BlockCopy(outb, 0, res, 0, pos);
            return res;
        }

        int Put(byte[] b, int o, double v)
        {
            if (v > 1.0) v = 1.0; else if (v < -1.0) v = -1.0;
            if (bytesPerSample == 4)
            {
                int s = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(v * 2147483647.0)));
                if (bigEndian) { b[o] = (byte)(s >> 24); b[o + 1] = (byte)(s >> 16); b[o + 2] = (byte)(s >> 8); b[o + 3] = (byte)s; }
                else { b[o] = (byte)s; b[o + 1] = (byte)(s >> 8); b[o + 2] = (byte)(s >> 16); b[o + 3] = (byte)(s >> 24); }
                return o + 4;
            }
            if (bytesPerSample == 3)
            {
                int s = (int)Math.Max(-8388608, Math.Min(8388607, Math.Round(v * 8388607.0)));
                if (bigEndian) { b[o] = (byte)(s >> 16); b[o + 1] = (byte)(s >> 8); b[o + 2] = (byte)s; }
                else { b[o] = (byte)s; b[o + 1] = (byte)(s >> 8); b[o + 2] = (byte)(s >> 16); }
                return o + 3;
            }
            // 16 bit: TPDF dither ile nicemle (düşük seviyede bozulmayı önler)
            double d = (rnd.NextDouble() - rnd.NextDouble());
            short t16 = (short)Math.Max(-32768, Math.Min(32767, Math.Round(v * 32767.0 + d)));
            if (bigEndian) { b[o] = (byte)(t16 >> 8); b[o + 1] = (byte)t16; }
            else { b[o] = (byte)t16; b[o + 1] = (byte)(t16 >> 8); }
            return o + 2;
        }
    }

    // ------------------------------------------------------------------ HTTP yayın sunucusu

    class StreamServer
    {
        TcpListener listener;
        public int Port;
        public volatile bool LiveActive;
        public int SampleRate = 48000;
        public Action<string> Log;
        readonly List<LiveSub> subs = new List<LiveSub>();
        readonly List<string> files = new List<string>();

        // Yayın başında hoparlöre peşin gönderilen sessizlik (ms). Hoparlörün tamponu hiç boşalmasın diye
        // güvenlik payı işlevi görür: boşalırsa Musaic ~3 sn biriktirip öyle devam ediyor.
        public volatile int CushionMs = 400;

        class LiveSub
        {
            public readonly BlockingCollection<float[]> Q = new BlockingCollection<float[]>();
            public long QueuedFrames;
        }

        public const int LiveDataLen = 0x7FFFF000;

        public void Start()
        {
            for (int p = 8090; p < 8120; p++)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Any, p);
                    listener.Start();
                    Port = p;
                    break;
                }
                catch { listener = null; }
            }
            if (listener == null) throw new Exception("Yayın sunucusu için boş port bulunamadı.");
            var t = new Thread(AcceptLoop);
            t.IsBackground = true;
            t.Start();
        }

        public int RegisterFile(string path)
        {
            lock (files)
            {
                int i = files.IndexOf(path);
                if (i >= 0) return i;
                files.Add(path);
                return files.Count - 1;
            }
        }

        public void Push(float[] b)
        {
            // Hoparlör bir süre okumazsa burada en fazla 1 sn ses biriksin; fazlası (en eskiler) atılır,
            // yoksa biriken ses kalıcı gecikmeye dönüşür.
            long maxFrames = SampleRate;
            lock (subs)
                foreach (var s in subs)
                {
                    s.Q.TryAdd(b);
                    Interlocked.Add(ref s.QueuedFrames, b.Length / 2);
                    float[] old;
                    while (Interlocked.Read(ref s.QueuedFrames) > maxFrames && s.Q.TryTake(out old))
                        Interlocked.Add(ref s.QueuedFrames, -(old.Length / 2));
                }
        }

        void AcceptLoop()
        {
            while (true)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch { return; }
                var t = new Thread(() => Handle(c));
                t.IsBackground = true;
                t.Start();
            }
        }

        void Write(NetworkStream ns, string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            ns.Write(b, 0, b.Length);
        }

        void Handle(TcpClient c)
        {
            try
            {
                using (c)
                {
                    c.NoDelay = true;
                    c.SendTimeout = 15000;
                    var ns = c.GetStream();
                    var sb = new StringBuilder();
                    var buf = new byte[1];
                    while (sb.Length < 16384)
                    {
                        int r = ns.Read(buf, 0, 1);
                        if (r <= 0) return;
                        sb.Append((char)buf[0]);
                        if (sb.Length >= 4 && sb.ToString(sb.Length - 4, 4) == "\r\n\r\n") break;
                    }
                    string[] lines = sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) return;
                    string[] first = lines[0].Split(' ');
                    if (first.Length < 2) return;
                    string method = first[0].ToUpperInvariant();
                    string path = first[1];
                    string range = null;
                    foreach (var l in lines)
                        if (l.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) range = l.Substring(6).Trim();

                    if (Log != null) Log("Hoparlör istek yaptı: " + method + " " + path + (range != null ? " [" + range + "]" : ""));

                    if (path.StartsWith("/live")) ServeLive(ns, method, path);
                    else if (path.StartsWith("/f/")) ServeFile(ns, method, path, range);
                    else Write(ns, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                }
            }
            catch (Exception) { /* bağlantı koptu */ }
        }

        static byte[] WavHeader(int rate, int bits, int dataLen)
        {
            int align = 2 * bits / 8;
            dataLen -= dataLen % align;
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(dataLen + 36);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * align);
            w.Write((short)align); w.Write((short)bits);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(dataLen);
            w.Flush();
            return ms.ToArray();
        }

        static int QueryInt(string path, string key, int def)
        {
            var m = Regex.Match(path, "[?&]" + key + "=(\\d+)");
            int v;
            return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : def;
        }

        void ServeLive(NetworkStream ns, string method, string path)
        {
            bool l16 = path.StartsWith("/live.l16");
            bool fixedLen = path.Contains("fixed=1");
            // rate/bits: çıkış biçimi. Yüksek hız/bit derinliğinde saniyede daha çok veri gider,
            // hoparlörün tamponu daha çabuk dolar ve gecikme azalır.
            int outRate = l16 ? SampleRate : QueryInt(path, "rate", SampleRate);
            int outBits = l16 ? 16 : QueryInt(path, "bits", 16);
            if (outBits != 24 && outBits != 32) outBits = 16;
            var enc = new PcmEncoder(SampleRate, outRate, outBits, l16);
            string mime = l16 ? "audio/L16;rate=" + SampleRate + ";channels=2" : "audio/wav";
            var h = new StringBuilder();
            h.Append("HTTP/1.1 200 OK\r\n");
            h.Append("Content-Type: " + mime + "\r\n");
            if (fixedLen && !l16) h.Append("Content-Length: " + ((long)LiveDataLen + 44) + "\r\n");
            h.Append("Cache-Control: no-cache\r\n");
            h.Append("Connection: close\r\n");
            h.Append("transferMode.dlna.org: Streaming\r\n");
            h.Append("contentFeatures.dlna.org: DLNA.ORG_OP=00;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01700000000000000000000000000000\r\n");
            h.Append("Server: MusaicKontrol/1.0 UPnP/1.0 DLNADOC/1.50\r\n\r\n");
            Write(ns, h.ToString());
            if (method == "HEAD") return;

            if (!l16)
            {
                byte[] hdr = WavHeader(outRate, outBits, LiveDataLen);
                ns.Write(hdr, 0, hdr.Length);
            }

            var sub = new LiveSub();
            lock (subs) subs.Add(sub);
            try
            {
                int cushion = CushionMs;
                if (cushion > 0)
                {
                    byte[] sil = enc.Silence((long)SampleRate * cushion / 1000);
                    ns.Write(sil, 0, sil.Length);
                }
                while (LiveActive)
                {
                    float[] b;
                    if (!sub.Q.TryTake(out b, 1000)) continue;
                    Interlocked.Add(ref sub.QueuedFrames, -(b.Length / 2));
                    byte[] outb = enc.Encode(b);
                    ns.Write(outb, 0, outb.Length);
                }
            }
            finally
            {
                lock (subs) subs.Remove(sub);
            }
        }

        public static string MimeFor(string file)
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".mp3": return "audio/mpeg";
                case ".flac": return "audio/flac";
                case ".wav": return "audio/wav";
                case ".m4a": case ".aac": case ".mp4": return "audio/mp4";
                case ".ogg": return "audio/ogg";
                case ".wma": return "audio/x-ms-wma";
                case ".aif": case ".aiff": return "audio/aiff";
                default: return "application/octet-stream";
            }
        }

        void ServeFile(NetworkStream ns, string method, string path, string range)
        {
            string idxStr = path.Substring(3);
            int dot = idxStr.IndexOf('.');
            if (dot > 0) idxStr = idxStr.Substring(0, dot);
            int idx;
            string file = null;
            if (int.TryParse(idxStr, out idx)) lock (files) if (idx >= 0 && idx < files.Count) file = files[idx];
            if (file == null || !File.Exists(file))
            {
                Write(ns, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                return;
            }

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long len = fs.Length, start = 0, end = len - 1;
                bool partial = false;
                if (range != null && range.StartsWith("bytes="))
                {
                    string[] parts = range.Substring(6).Split(',')[0].Split('-');
                    long a, b;
                    if (parts[0] != "" && long.TryParse(parts[0], out a)) start = a;
                    else if (parts.Length > 1 && long.TryParse(parts[1], out b)) start = Math.Max(0, len - b);
                    if (parts.Length > 1 && parts[0] != "" && long.TryParse(parts[1], out b)) end = Math.Min(b, len - 1);
                    partial = true;
                }
                if (start >= len)
                {
                    Write(ns, "HTTP/1.1 416 Range Not Satisfiable\r\nContent-Range: bytes */" + len + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    return;
                }
                long count = end - start + 1;
                var h = new StringBuilder();
                h.Append(partial ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n");
                h.Append("Content-Type: " + MimeFor(file) + "\r\n");
                h.Append("Content-Length: " + count + "\r\n");
                if (partial) h.Append("Content-Range: bytes " + start + "-" + end + "/" + len + "\r\n");
                h.Append("Accept-Ranges: bytes\r\n");
                h.Append("Connection: close\r\n");
                h.Append("transferMode.dlna.org: Streaming\r\n");
                h.Append("contentFeatures.dlna.org: DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01700000000000000000000000000000\r\n");
                h.Append("Server: MusaicKontrol/1.0 UPnP/1.0 DLNADOC/1.50\r\n\r\n");
                Write(ns, h.ToString());
                if (method == "HEAD") return;

                fs.Seek(start, SeekOrigin.Begin);
                var buf = new byte[65536];
                while (count > 0)
                {
                    int r = fs.Read(buf, 0, (int)Math.Min(buf.Length, count));
                    if (r <= 0) break;
                    ns.Write(buf, 0, r);
                    count -= r;
                }
            }
        }
    }

}
