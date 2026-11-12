// RigCord - your rig, live on Discord.
// Discord Rich Presence with live CPU / GPU / RAM / VRAM stats and the game you're playing.
// Built with the .NET Framework csc.exe that ships with Windows (C# 5) - no extra dependencies.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("RigCord")]
[assembly: System.Reflection.AssemblyProduct("RigCord")]
[assembly: System.Reflection.AssemblyDescription("Your rig, live on Discord")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

static class Program
{
    public const string Name = "RigCord";
    public const string Version = "3.5";
    // Project page. Used for the clickable profile picture, the "Get RigCord" button and the footer link.
    public const string GitHubUrl = "https://github.com/Nikoladab/RigCord";
    public const string LatestReleaseApi = "https://api.github.com/repos/Nikoladab/RigCord/releases/latest";
    // The big profile picture is always the RigCord logo and always links to GitHub (that's how RigCord spreads).
    public const string LogoUrl = "https://i.ibb.co/DPZLk07n/image-7d0d417b.jpg";

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        bool created;
        Mutex mutex = new Mutex(true, "RigCord_SingleInstance", out created);
        if (!created)
        {
            try { using (EventWaitHandle ev = EventWaitHandle.OpenExisting("RigCord_Show")) ev.Set(); } catch { }
            return;
        }
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp());
        GC.KeepAlive(mutex);
    }
}

// ================================================================ Settings
class Settings
{
    public string ClientId = "";
    public string AppName = "";
    public bool ShowGame = true;
    public bool ShowNames = false;
    public bool ShowVram = true;
    public bool ShowTemp = true;
    public bool ShowElapsed = true;
    public bool Paused = false;
    public int Interval = 5;
    public string CpuName = "";
    public string GpuName = "";
    public string LargeImage = "";
    public string LinkUrl = "";
    public string ButtonLabel = "";
    public string ButtonUrl = "";
    public string IgnoreApps = "";
    public string Line1 = "", Line2 = "", GameLine1 = "", GameLine2 = ""; // custom text templates (empty = default)
    public string NotifiedVersion = "";

    public static string FilePath;
    static string legacyPath;

    static Settings()
    {
        // Portable mode: if settings.ini sits next to the exe, use that one.
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        string portable = Path.Combine(exeDir, "settings.ini");
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (File.Exists(portable)) FilePath = portable;
        else FilePath = Path.Combine(appData, "RigCord", "settings.ini");
        legacyPath = Path.Combine(appData, "PCStatsPresence", "settings.ini"); // older versions
    }

    public string Link { get { return Program.GitHubUrl; } }

    static bool B(string v) { v = v.ToLowerInvariant(); return v == "1" || v == "true" || v == "yes"; }

    public static Settings Load()
    {
        Settings s = new Settings();
        string path = File.Exists(FilePath) ? FilePath : (File.Exists(legacyPath) ? legacyPath : null);
        if (path == null) return s;
        try
        {
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                string v = line.Substring(eq + 1).Trim();
                int i;
                switch (k)
                {
                    case "clientid": s.ClientId = Regex.Replace(v, @"\D", ""); break;
                    case "appname": s.AppName = v; break;
                    case "showgame": s.ShowGame = B(v); break;
                    case "shownames": s.ShowNames = B(v); break;
                    case "showvram": s.ShowVram = B(v); break;
                    case "showtemp": s.ShowTemp = B(v); break;
                    case "showelapsed": s.ShowElapsed = B(v); break;
                    case "paused": s.Paused = B(v); break;
                    case "interval": if (int.TryParse(v, out i)) s.Interval = Math.Max(4, Math.Min(60, i)); break;
                    case "cpuname": s.CpuName = v; break;
                    case "gpuname": s.GpuName = v; break;
                    case "largeimage": s.LargeImage = v; break;
                    case "linkurl": s.LinkUrl = v; break;
                    case "buttonlabel": s.ButtonLabel = v; break;
                    case "buttonurl": s.ButtonUrl = v; break;
                    case "ignoreapps": s.IgnoreApps = v; break;
                    case "line1": s.Line1 = v; break;
                    case "line2": s.Line2 = v; break;
                    case "gameline1": s.GameLine1 = v; break;
                    case "gameline2": s.GameLine2 = v; break;
                    case "notifiedversion": s.NotifiedVersion = v; break;
                }
            }
        }
        catch { }
        if (path == legacyPath) s.Save(); // migrate to the new location
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# RigCord settings");
            sb.AppendLine("# Tip: keep this file next to RigCord.exe to make the app portable.");
            sb.AppendLine();
            sb.AppendLine("# Discord Application ID from https://discord.com/developers/applications");
            sb.AppendLine("ClientId=" + ClientId);
            sb.AppendLine("AppName=" + AppName);
            sb.AppendLine();
            sb.AppendLine("# 1 = on, 0 = off");
            sb.AppendLine("ShowGame=" + (ShowGame ? "1" : "0"));
            sb.AppendLine("ShowNames=" + (ShowNames ? "1" : "0"));
            sb.AppendLine("ShowVram=" + (ShowVram ? "1" : "0"));
            sb.AppendLine("ShowTemp=" + (ShowTemp ? "1" : "0"));
            sb.AppendLine("ShowElapsed=" + (ShowElapsed ? "1" : "0"));
            sb.AppendLine("Paused=" + (Paused ? "1" : "0"));
            sb.AppendLine();
            sb.AppendLine("# Seconds between Discord updates (4-60)");
            sb.AppendLine("Interval=" + Interval);
            sb.AppendLine();
            sb.AppendLine("# Override detected hardware names (empty = auto)");
            sb.AppendLine("CpuName=" + CpuName);
            sb.AppendLine("GpuName=" + GpuName);
            sb.AppendLine();
            sb.AppendLine("# Optional extra button on your profile (visible to others, not to yourself)");
            sb.AppendLine("ButtonLabel=" + ButtonLabel);
            sb.AppendLine("ButtonUrl=" + ButtonUrl);
            sb.AppendLine();
            sb.AppendLine("# Programs that should never be shown as a game, comma separated exe names (e.g. blender, vlc)");
            sb.AppendLine("IgnoreApps=" + IgnoreApps);
            sb.AppendLine();
            sb.AppendLine("# Custom profile text (empty = default). Placeholders:");
            sb.AppendLine("# {game} {cpu} {gpu} {temp} {ram} {ram%} {vram} {cpuname} {gpuname}");
            sb.AppendLine("Line1=" + Line1);
            sb.AppendLine("Line2=" + Line2);
            sb.AppendLine("GameLine1=" + GameLine1);
            sb.AppendLine("GameLine2=" + GameLine2);
            sb.AppendLine();
            sb.AppendLine("# internal: last update notification shown");
            sb.AppendLine("NotifiedVersion=" + NotifiedVersion);
            File.WriteAllText(FilePath, sb.ToString());
        }
        catch { }
    }
}

// ================================================================ Hardware stats
class Snapshot
{
    public double Cpu, Gpu = -1, RamUsed, RamTotal, VramUsed = -1, VramTotal, GpuTemp = -1;
    public GameInfo Game;
    public string Details = "", State = "";
}

static class Nvml
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryW(string path);
    [DllImport("nvml.dll")] static extern int nvmlInit_v2();
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetTemperature(IntPtr device, int sensor, out uint temp);

    static bool tried, ok;
    static IntPtr dev;

    public static int Temperature()
    {
        if (!tried)
        {
            tried = true;
            try
            {
                string sys = Path.Combine(Environment.SystemDirectory, "nvml.dll");
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvml.dll");
                if (File.Exists(sys)) LoadLibraryW(sys);
                else if (File.Exists(old)) LoadLibraryW(old);
                else return -1;
                ok = nvmlInit_v2() == 0 && nvmlDeviceGetHandleByIndex_v2(0, out dev) == 0;
            }
            catch { ok = false; }
        }
        if (!ok) return -1;
        try
        {
            uint t;
            if (nvmlDeviceGetTemperature(dev, 0, out t) == 0) return (int)t;
        }
        catch { }
        return -1;
    }
}

class Stats : IDisposable
{
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalKb);

    [StructLayout(LayoutKind.Sequential)]
    class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern int PdhOpenQueryW(string src, IntPtr user, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern int PdhAddEnglishCounterW(IntPtr query, string path, IntPtr user, out IntPtr counter);
    [DllImport("pdh.dll")] static extern int PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern int PdhGetFormattedCounterArrayW(IntPtr counter, uint fmt, ref uint bufSize, out uint count, IntPtr buf);
    [DllImport("pdh.dll")] static extern int PdhCloseQuery(IntPtr query);

    const uint PDH_FMT = 0x00000200 | 0x00008000; // DOUBLE | NOCAP100
    const int PDH_MORE_DATA = unchecked((int)0x800007D2);

    public static string CpuName = "Unknown CPU", GpuName = "Unknown GPU";
    public static double VramTotalGb = 0, RamTotalGb = 0;

    long pIdle, pKernel, pUser;
    IntPtr query = IntPtr.Zero, cEngine = IntPtr.Zero, cVram = IntPtr.Zero;
    IntPtr buf = IntPtr.Zero;
    uint bufSize = 0;
    int samples = 0;
    bool pdhOk = true;

    public double Gpu = -1, VramUsedGb = -1;
    public Dictionary<int, double> Pid3D = new Dictionary<int, double>(); // 3D GPU usage per process (for game detection)

    public Stats()
    {
        long i, k, u;
        GetSystemTimes(out i, out k, out u);
        pIdle = i; pKernel = k; pUser = u;
        OpenQuery();
    }

    public static void DetectHardware(Settings s)
    {
        ulong kb;
        if (GetPhysicallyInstalledSystemMemory(out kb) && kb > 0) RamTotalGb = kb / 1048576.0;
        else { MEMORYSTATUSEX m = new MEMORYSTATUSEX(); GlobalMemoryStatusEx(m); RamTotalGb = m.ullTotalPhys / 1073741824.0; }

        CpuName = s.CpuName.Length > 0 ? s.CpuName : DetectCpu();
        string g; double vram;
        DetectGpu(out g, out vram);
        GpuName = s.GpuName.Length > 0 ? s.GpuName : g;
        VramTotalGb = vram;
    }

    public double Cpu()
    {
        long i, k, u;
        if (!GetSystemTimes(out i, out k, out u)) return 0;
        long di = i - pIdle, dk = k - pKernel, du = u - pUser;
        pIdle = i; pKernel = k; pUser = u;
        long total = dk + du; // kernel time includes idle time
        if (total <= 0) return 0;
        return Clamp((total - di) * 100.0 / total);
    }

    public double RamUsedGb()
    {
        MEMORYSTATUSEX m = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(m)) return 0;
        return (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
    }

    void OpenQuery()
    {
        CloseQuery();
        IntPtr q;
        if (PdhOpenQueryW(null, IntPtr.Zero, out q) != 0) { pdhOk = false; return; }
        IntPtr c;
        if (PdhAddEnglishCounterW(q, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out c) != 0)
        {
            PdhCloseQuery(q); pdhOk = false; return;
        }
        IntPtr v;
        if (PdhAddEnglishCounterW(q, @"\GPU Adapter Memory(*)\Dedicated Usage", IntPtr.Zero, out v) != 0) v = IntPtr.Zero;
        query = q; cEngine = c; cVram = v;
        PdhCollectQueryData(query);
        samples = 0;
    }

    void CloseQuery()
    {
        if (query != IntPtr.Zero) { PdhCloseQuery(query); query = IntPtr.Zero; cEngine = IntPtr.Zero; cVram = IntPtr.Zero; }
    }

    // Same method as Task Manager: sum per GPU engine across all processes, then take the busiest engine.
    public void SampleGpu()
    {
        if (!pdhOk) return;
        try
        {
            if (samples >= 12 || query == IntPtr.Zero) { OpenQuery(); return; } // pick up newly started games
            samples++;
            if (PdhCollectQueryData(query) != 0) return;

            Dictionary<string, double> engines = new Dictionary<string, double>();
            Dictionary<int, double> pids = new Dictionary<int, double>();
            bool ok = ReadArray(cEngine, delegate(string name, double v)
            {
                int li = name.IndexOf("luid_", StringComparison.Ordinal);
                string key = li >= 0 ? name.Substring(li) : name;
                double cur;
                engines.TryGetValue(key, out cur);
                engines[key] = cur + v;
                if (v > 0 && name.StartsWith("pid_", StringComparison.Ordinal) && name.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                {
                    int us = name.IndexOf('_', 4);
                    int pid;
                    if (us > 4 && int.TryParse(name.Substring(4, us - 4), out pid))
                    {
                        double p;
                        pids.TryGetValue(pid, out p);
                        pids[pid] = p + v;
                    }
                }
            });
            if (ok)
            {
                double max = 0;
                foreach (double v in engines.Values) if (v > max) max = v;
                Gpu = Clamp(max);
                Pid3D = pids;
            }
            if (cVram != IntPtr.Zero)
            {
                double vmax = -1;
                if (ReadArray(cVram, delegate(string name, double v) { if (v > vmax) vmax = v; }) && vmax >= 0)
                    VramUsedGb = vmax / 1073741824.0;
            }
        }
        catch { }
    }

    bool ReadArray(IntPtr counter, Action<string, double> each)
    {
        uint size = bufSize, count;
        int r = PdhGetFormattedCounterArrayW(counter, PDH_FMT, ref size, out count, buf);
        if (r == PDH_MORE_DATA || buf == IntPtr.Zero)
        {
            if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
            bufSize = Math.Max(size, 4096) + 4096;
            buf = Marshal.AllocHGlobal((int)bufSize);
            size = bufSize;
            r = PdhGetFormattedCounterArrayW(counter, PDH_FMT, ref size, out count, buf);
        }
        if (r != 0) return false;
        int ptr = IntPtr.Size;
        int itemSize = ptr == 8 ? 24 : 16;
        int valOff = ptr == 8 ? 16 : 8;
        for (int n = 0; n < count; n++)
        {
            int off = n * itemSize;
            int status = Marshal.ReadInt32(buf, off + ptr);
            if (status != 0 && status != 1) continue;
            string name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(buf, off)) ?? "";
            double v = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(buf, off + valOff));
            each(name, v);
        }
        return true;
    }

    static double Clamp(double v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }

    public void Dispose()
    {
        CloseQuery();
        if (buf != IntPtr.Zero) { Marshal.FreeHGlobal(buf); buf = IntPtr.Zero; }
    }

    static string DetectCpu()
    {
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                if (k != null) return ShortCpu(Convert.ToString(k.GetValue("ProcessorNameString")));
        }
        catch { }
        return "Unknown CPU";
    }

    static void DetectGpu(out string name, out double vramGb)
    {
        string best = null; int bestScore = -1; double bestVram = 0;
        try
        {
            using (RegistryKey cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
            {
                if (cls != null)
                {
                    foreach (string sub in cls.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey k = cls.OpenSubKey(sub))
                            {
                                if (k == null) continue;
                                string d = Convert.ToString(k.GetValue("DriverDesc"));
                                if (string.IsNullOrEmpty(d)) continue;
                                double vram = ReadVram(k);
                                int sc = Score(d) * 1000 + (int)Math.Min(999, vram);
                                if (sc > bestScore) { bestScore = sc; best = d; bestVram = vram; }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
        name = best == null ? "Unknown GPU" : ShortGpu(best);
        vramGb = bestVram;
    }

    static double ReadVram(RegistryKey k)
    {
        try
        {
            object q = k.GetValue("HardwareInformation.qwMemorySize");
            if (q is long) return (long)q / 1073741824.0;
            object m = k.GetValue("HardwareInformation.MemorySize");
            if (m is int) return (uint)(int)m / 1073741824.0;
            if (m is byte[] && ((byte[])m).Length >= 4) return BitConverter.ToUInt32((byte[])m, 0) / 1073741824.0;
        }
        catch { }
        return 0;
    }

    static int Score(string d)
    {
        string l = d.ToLowerInvariant();
        if (l.Contains("basic") || l.Contains("remote") || l.Contains("virtual") || l.Contains("parsec") || l.Contains("mirage") || l.Contains("idd")) return 0;
        if (l.Contains("geforce") || l.Contains("rtx") || l.Contains("gtx") || l.Contains("quadro") || l.Contains("radeon rx") || l.Contains("radeon pro") || l.Contains(" arc")) return 3;
        if (l.Contains("nvidia") || l.Contains("amd") || l.Contains("radeon")) return 2;
        return 1;
    }

    static string ShortCpu(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Unknown CPU";
        s = s.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "");
        s = Regex.Replace(s, @"\s+CPU\s*@.*$", "");
        s = Regex.Replace(s, @"\s+@.*$", "");
        s = Regex.Replace(s, @"\s+\S+-Core( Processor)?.*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+with Radeon.*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+Processor$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"^(Intel|AMD)\s+", "");
        s = Regex.Replace(s, @"^Core\s+(?=[iU\d])", "");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    static string ShortGpu(string s)
    {
        s = s.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "");
        s = Regex.Replace(s, @"^(NVIDIA|AMD|Intel)\s+", "");
        s = Regex.Replace(s, @"^(GeForce|Radeon)\s+(?=(RTX|GTX|RX|GT|MX))", "");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}

// ================================================================ Game detection
class GameInfo
{
    public int Pid;
    public volatile string Name = "", IconUrl = "";
    public string Path = "";
    public DateTime StartUtc;
    public volatile Image Icon;      // official icon from Discord's game list (if found)
}

class GameMeta { public string Name = "", IconUrl = ""; public Image Icon; }

// Discord's public list of detectable games -> official name + icon for an exe.
// Downloaded at most once a week and stored as a small index file.
static class GameDb
{
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RigCord");
    static readonly string IndexPath = Path.Combine(Dir, "games.idx");
    static readonly object gate = new object();
    static readonly Dictionary<string, GameMeta> cache = new Dictionary<string, GameMeta>(StringComparer.OrdinalIgnoreCase);

    public static void Resolve(GameInfo g, Action done)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                GameMeta m = Lookup(g.Path);
                if (m != null)
                {
                    // our own clean names win (Discord has e.g. "ROBLOX"), otherwise use Discord's official name
                    if (m.Name.Length > 0 && !GameDetector.IsKnown(g.Path)) g.Name = m.Name;
                    g.IconUrl = m.IconUrl;
                    g.Icon = m.Icon;
                }
            }
            catch { }
            done();
        });
    }

    static GameMeta Lookup(string path)
    {
        lock (gate)
        {
            GameMeta m;
            if (cache.TryGetValue(path, out m)) return m;
            EnsureIndex();
            m = File.Exists(IndexPath) ? Find(path) : null;
            if (m != null && m.IconUrl.Length > 0)
            {
                try { m.Icon = Image.FromStream(new MemoryStream(Web.Get(m.IconUrl))); } catch { }
            }
            cache[path] = m;
            return m;
        }
    }

    static void EnsureIndex()
    {
        if (File.Exists(IndexPath) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(IndexPath)).TotalDays < 7) return;
        try { BuildIndex(); } catch { }
    }

    static void BuildIndex()
    {
        string json;
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://discord.com/api/v10/applications/detectable");
        req.UserAgent = Program.Name + "/" + Program.Version;
        req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        req.Timeout = 30000;
        using (WebResponse resp = req.GetResponse())
        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            json = sr.ReadToEnd();

        StringBuilder sb = new StringBuilder();
        Regex exeRx = new Regex("\"executables\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        Regex idRx = new Regex("\"id\"\\s*:\\s*\"(\\d+)\"");
        Regex itemRx = new Regex("\\{[^{}]*\\}");
        Regex winRx = new Regex("\"os\"\\s*:\\s*\"win32\"");
        foreach (Match em in exeRx.Matches(json))
        {
            // keys are sorted, so this app's icon_hash / id / name follow its executables list
            int start = em.Index + em.Length;
            int next = json.IndexOf("\"executables\"", start, StringComparison.Ordinal);
            if (next < 0) next = json.Length;
            string tail = json.Substring(start, Math.Min(next - start, 6000));
            Match idm = idRx.Match(tail);
            string name = Json.Field(tail, "name");
            if (!idm.Success || string.IsNullOrEmpty(name)) continue;
            string icon = Json.Field(tail, "icon_hash") ?? "";
            foreach (Match it in itemRx.Matches(em.Groups[1].Value))
            {
                if (!winRx.IsMatch(it.Value)) continue;
                string exe = Json.Field(it.Value, "name");
                if (string.IsNullOrEmpty(exe)) continue;
                exe = exe.Replace('\\', '/').TrimStart('>').ToLowerInvariant();
                sb.Append(exe).Append('\t').Append(idm.Groups[1].Value).Append('\t').Append(icon).Append('\t')
                  .Append(name.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
            }
        }
        json = null;
        Directory.CreateDirectory(Dir);
        string tmp = IndexPath + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
        if (File.Exists(IndexPath)) File.Delete(IndexPath);
        File.Move(tmp, IndexPath);
        GC.Collect();
    }

    static GameMeta Find(string path)
    {
        string p = path.Replace('\\', '/').ToLowerInvariant();
        string file = System.IO.Path.GetFileName(p);
        string best = null; int bestLen = -1;
        string bare = null; HashSet<string> bareNames = new HashSet<string>();
        foreach (string line in File.ReadLines(IndexPath))
        {
            int t = line.IndexOf('\t');
            if (t <= 0) continue;
            string exe = line.Substring(0, t);
            if (exe.IndexOf('/') >= 0)
            {
                if (p.EndsWith("/" + exe, StringComparison.Ordinal) && exe.Length > bestLen) { bestLen = exe.Length; best = line; }
            }
            else if (exe == file)
            {
                if (bare == null) bare = line;
                bareNames.Add(line.Substring(line.LastIndexOf('\t') + 1));
            }
        }
        string use = best ?? (bareNames.Count == 1 ? bare : null); // ambiguous names like game.exe are skipped
        if (use == null) return null;
        string[] f = use.Split('\t');
        if (f.Length < 4) return null;
        GameMeta m = new GameMeta();
        m.Name = f[3];
        if (f[2].Length > 0) m.IconUrl = "https://cdn.discordapp.com/app-icons/" + f[1] + "/" + f[2] + ".png?size=128";
        return m;
    }
}

// Finds the game you're playing: the full-screen / GPU-heavy foreground app, or the app using the most 3D GPU.
class GameDetector
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, int flags, StringBuilder sb, ref int size);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    // apps that use the GPU or go full screen but aren't games
    static readonly HashSet<string> Ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "dwm", "explorer", "rigcord", "pcstatspresence", "discord", "discordptb", "discordcanary",
        "chrome", "msedge", "firefox", "opera", "opera_gx", "brave", "vivaldi", "arc", "thorium", "msedgewebview2",
        "obs64", "obs32", "obs", "streamlabs obs", "xsplit.core", "nvidia share", "nvidia overlay", "nvcontainer", "nvsphelper64",
        "steam", "steamwebhelper", "epicgameslauncher", "epicwebhelper", "battle.net", "eadesktop", "eabackgroundservice", "origin",
        "upc", "ubisoftconnect", "galaxyclient", "riotclientservices", "riotclientux", "riotclientuxrender", "playnite.desktopapp",
        "amdrsserv", "radeonsoftware", "amdow", "wallpaper32", "wallpaper64", "livelywpf", "lively", "rainmeter",
        "spotify", "vlc", "mpc-hc64", "mpc-hc", "mpc-be64", "mpv", "potplayermini64", "potplayermini", "wmplayer", "video.ui", "netflix",
        "applicationframehost", "searchhost", "searchapp", "startmenuexperiencehost", "shellexperiencehost", "textinputhost",
        "systemsettings", "lockapp", "widgets", "phoneexperiencehost", "gamebar", "gamebarftserver", "xboxpcappft",
        "code", "devenv", "idea64", "pycharm64", "rider64", "unity", "unityhub", "unrealeditor", "blender", "photoshop", "afterfx",
        "adobe premiere pro", "resolve", "davinci resolve", "capcut", "figma", "notepad++",
        "teams", "ms-teams", "zoom", "slack", "whatsapp", "telegram", "skype", "powershell", "windowsterminal", "cmd", "conhost",
        "taskmgr", "mmc", "vmware-vmx", "vmware", "virtualboxvm", "powertoys", "msiafterburner", "rtss", "hwinfo64", "cpuz", "gpu-z"
    };

    readonly Dictionary<int, string> pathCache = new Dictionary<int, string>();
    readonly int selfPid = Process.GetCurrentProcess().Id;
    readonly string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
    int pendingPid = -1;
    DateTime lastSeen;
    public GameInfo Current;

    public void Update(Dictionary<int, double> pid3d, string extraIgnore)
    {
        HashSet<string> extra = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string s in (extraIgnore ?? "").Split(',', ';'))
        {
            string t = s.Trim();
            if (t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 4);
            if (t.Length > 0) extra.Add(t);
        }

        // a game that was just added to the ignore list disappears right away
        if (Current != null && !IsCandidate(Current.Pid, extra)) { Current = null; pendingPid = -1; }

        int cand = -1;
        string title = null;
        IntPtr fg = GetForegroundWindow();
        uint fpid = 0;
        if (fg != IntPtr.Zero && fg != GetShellWindow()) GetWindowThreadProcessId(fg, out fpid);
        if (fpid != 0 && IsCandidate((int)fpid, extra))
        {
            double u;
            pid3d.TryGetValue((int)fpid, out u);
            if (IsFullscreen(fg) || u >= 5) { cand = (int)fpid; title = WindowTitle(fg); }
        }
        if (cand < 0)
        {
            double best = 25; // background apps need real GPU load to count as a game
            foreach (KeyValuePair<int, double> kv in pid3d)
                if (kv.Value >= best && IsCandidate(kv.Key, extra)) { best = kv.Value; cand = kv.Key; }
        }

        DateTime now = DateTime.UtcNow;
        if (Current != null)
        {
            if (!IsAlive(Current.Pid)) { Current = null; }
            else
            {
                double u;
                pid3d.TryGetValue(Current.Pid, out u);
                if (cand == Current.Pid || u >= 2) lastSeen = now;
                if (cand < 0 || cand == Current.Pid)
                {
                    if ((now - lastSeen).TotalSeconds > 120) Current = null;
                    pendingPid = -1;
                    return;
                }
            }
        }

        if (cand > 0)
        {
            if (pendingPid == cand) // seen twice in a row -> it's real
            {
                Current = Make(cand, title);
                lastSeen = now;
                pendingPid = -1;
            }
            else pendingPid = cand;
        }
        else pendingPid = -1;

        if (pathCache.Count > 200) pathCache.Clear();
    }

    bool IsCandidate(int pid, HashSet<string> extra)
    {
        if (pid == selfPid || pid <= 4) return false;
        string path = PathOf(pid);
        if (string.IsNullOrEmpty(path)) return false;
        if (path.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) return false;
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        return !Ignore.Contains(name) && !extra.Contains(name);
    }

    string PathOf(int pid)
    {
        string p;
        if (pathCache.TryGetValue(pid, out p)) return p;
        p = null;
        IntPtr h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (h != IntPtr.Zero)
        {
            StringBuilder sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (QueryFullProcessImageNameW(h, 0, sb, ref size)) p = sb.ToString();
            CloseHandle(h);
        }
        pathCache[pid] = p;
        return p;
    }

    static bool IsAlive(int pid)
    {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return false;
        uint code;
        bool alive = GetExitCodeProcess(h, out code) && code == 259; // STILL_ACTIVE
        CloseHandle(h);
        return alive;
    }

    static bool IsFullscreen(IntPtr h)
    {
        RECT r;
        if (!GetWindowRect(h, out r)) return false;
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        if (!GetMonitorInfo(MonitorFromWindow(h, 2), ref mi)) return false;
        return r.L <= mi.rcMonitor.L && r.T <= mi.rcMonitor.T && r.R >= mi.rcMonitor.R && r.B >= mi.rcMonitor.B;
    }

    static string WindowTitle(IntPtr h)
    {
        StringBuilder sb = new StringBuilder(256);
        GetWindowTextW(h, sb, sb.Capacity);
        return sb.ToString();
    }

    GameInfo Make(int pid, string title)
    {
        GameInfo g = new GameInfo();
        g.Pid = pid;
        g.Path = PathOf(pid) ?? "";
        g.Name = NiceName(g.Path, title);
        g.StartUtc = DateTime.UtcNow;
        try { g.StartUtc = Process.GetProcessById(pid).StartTime.ToUniversalTime(); } catch { }
        return g;
    }

    static readonly string[] Generic = { "unreal", "engine", "bootstrap", "launcher", "shipping", "game executable", "application",
        "microsoft", "windows", "java", "runtime", "-win64", "-win32", "directx", "x64", "client-win" };

    static bool GoodName(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 2 || s.Length > 40) return false;
        string l = s.ToLowerInvariant();
        foreach (string g in Generic) if (l.Contains(g)) return false;
        return true;
    }

    static string Clean(string s)
    {
        if (s == null) return null;
        s = s.Replace("\u2122", "").Replace("\u00AE", "").Replace("\u00A9", "").Replace("(TM)", "").Replace("(R)", "");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    // popular games whose exe metadata isn't a nice name
    static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { "robloxplayerbeta", "Roblox" }, { "fortniteclient-win64-shipping", "Fortnite" }, { "valorant-win64-shipping", "VALORANT" },
        { "cs2", "Counter-Strike 2" }, { "csgo", "Counter-Strike" }, { "gta5", "GTA V" }, { "gta5_enhanced", "GTA V" },
        { "r5apex", "Apex Legends" }, { "r5apex_dx12", "Apex Legends" }, { "league of legends", "League of Legends" },
        { "rocketleague", "Rocket League" }, { "overwatch", "Overwatch 2" }, { "eldenring", "ELDEN RING" },
        { "rainbowsix", "Rainbow Six Siege" }, { "rainbowsix_vulkan", "Rainbow Six Siege" }, { "destiny2", "Destiny 2" },
        { "dota2", "Dota 2" }, { "tslgame", "PUBG" }, { "witcher3", "The Witcher 3" }, { "cyberpunk2077", "Cyberpunk 2077" },
        { "rdr2", "Red Dead Redemption 2" }, { "cod", "Call of Duty" }, { "fifa23", "EA FC" }, { "fc24", "EA FC 24" }, { "fc25", "EA FC 25" },
        { "deadbydaylight-win64-shipping", "Dead by Daylight" }, { "marvel-win64-shipping", "Marvel Rivals" },
        { "thefinals", "THE FINALS" }, { "escapefromtarkov", "Escape from Tarkov" }, { "rust", "Rust" }, { "terraria", "Terraria" },
        { "hollow knight", "Hollow Knight" }, { "fallguys_client_game", "Fall Guys" }, { "among us", "Among Us" },
        { "osu!", "osu!" }, { "wow", "World of Warcraft" }, { "palworld-win64-shipping", "Palworld" }, { "helldivers2", "HELLDIVERS 2" },
        { "deltaforceclient-win64-shipping", "Delta Force" }, { "cs2_x64", "Counter-Strike 2" }, { "warthunder", "War Thunder" },
        { "aces", "War Thunder" }, { "pathofexile", "Path of Exile" }, { "pathofexile_x64", "Path of Exile" }, { "pathofexile2", "Path of Exile 2" }
    };

    public static bool IsKnown(string path)
    {
        return Known.ContainsKey(System.IO.Path.GetFileNameWithoutExtension(path ?? "") ?? "");
    }

    public static string NiceName(string path, string title)
    {
        string file = System.IO.Path.GetFileNameWithoutExtension(path) ?? "";
        string lf = file.ToLowerInvariant();
        if ((lf == "javaw" || lf == "java") && title != null && title.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0) return "Minecraft";
        string known;
        if (Known.TryGetValue(lf, out known)) return known;
        try
        {
            FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
            string d = Clean(vi.FileDescription), p = Clean(vi.ProductName);
            if (d != null) d = Regex.Replace(d, @"\s+(Game Client|Client|Game)$", "", RegexOptions.IgnoreCase);
            if (GoodName(d)) return d;
            if (GoodName(p)) return p;
        }
        catch { }
        string t = Clean(title);
        if (GoodName(t)) return t;
        file = Regex.Replace(file, @"[-_ ]?(Win64|Win32|x64|x86|Shipping|DX11|DX12|Vulkan)", "", RegexOptions.IgnoreCase);
        return file.Length > 0 ? file : "a game";
    }
}

// ================================================================ Discord IPC
class DiscordIpc
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool WaitNamedPipeW(string name, uint timeout);

    NamedPipeClientStream pipe;
    readonly object writeLock = new object();
    readonly ManualResetEvent ready = new ManualResetEvent(false);
    public volatile bool Connected;
    public volatile string Error = "";
    public volatile string Username = "";

    public bool Connect(string clientId)
    {
        Disconnect();
        Error = "";
        for (int i = 0; i < 10; i++)
        {
            string name = "discord-ipc-" + i;
            if (!WaitNamedPipeW(@"\\.\pipe\" + name, 1)) continue;
            try
            {
                NamedPipeClientStream p = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                p.Connect(1000);
                pipe = p;
                ready.Reset();
                Thread reader = new Thread(ReadLoop);
                reader.IsBackground = true;
                reader.Start(p);
                Send(0, "{\"v\":1,\"client_id\":" + Json.Str(clientId) + "}");
                if (ready.WaitOne(5000) && pipe != null && Error.Length == 0)
                {
                    Connected = true;
                    return true;
                }
                if (Error.Length == 0) Error = "Discord did not respond";
                Disconnect();
                return false;
            }
            catch { Disconnect(); }
        }
        if (Error.Length == 0) Error = "Discord is not running";
        return false;
    }

    public bool SetActivity(string activityJson)
    {
        if (!Connected) return false;
        string payload = "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Process.GetCurrentProcess().Id +
            (activityJson != null ? ",\"activity\":" + activityJson : "") +
            "},\"nonce\":\"" + Guid.NewGuid().ToString("N") + "\"}";
        return Send(1, payload);
    }

    bool Send(int op, string json)
    {
        try
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            lock (writeLock)
            {
                NamedPipeClientStream p = pipe;
                if (p == null) return false;
                p.Write(frame, 0, frame.Length);
                p.Flush();
            }
            return true;
        }
        catch { Connected = false; return false; }
    }

    void ReadLoop(object o)
    {
        NamedPipeClientStream p = (NamedPipeClientStream)o;
        byte[] hdr = new byte[8];
        try
        {
            while (true)
            {
                if (!ReadFully(p, hdr, 8)) break;
                int op = BitConverter.ToInt32(hdr, 0);
                int len = BitConverter.ToInt32(hdr, 4);
                if (len < 0 || len > 1 << 20) break;
                byte[] body = new byte[len];
                if (!ReadFully(p, body, len)) break;
                string json = Encoding.UTF8.GetString(body);
                if (op == 1)
                {
                    if (json.Contains("\"evt\":\"READY\""))
                    {
                        string u = Json.Field(json, "global_name");
                        if (string.IsNullOrEmpty(u)) u = Json.Field(json, "username");
                        Username = u ?? "";
                        ready.Set();
                    }
                    else if (json.Contains("\"evt\":\"ERROR\"")) Error = Json.Field(json, "message") ?? "Discord error";
                    else if (json.Contains("\"cmd\":\"SET_ACTIVITY\"")) Error = "";
                }
                else if (op == 2)
                {
                    Error = Json.Field(json, "message") ?? "Connection closed";
                    break;
                }
                else if (op == 3) Send(4, json);
            }
        }
        catch { }
        Connected = false;
        if (pipe == p) { try { p.Dispose(); } catch { } pipe = null; }
        ready.Set();
    }

    static bool ReadFully(Stream s, byte[] b, int len)
    {
        int got = 0;
        while (got < len)
        {
            int r = s.Read(b, got, len - got);
            if (r <= 0) return false;
            got += r;
        }
        return true;
    }

    public void Disconnect()
    {
        Connected = false;
        NamedPipeClientStream p = pipe;
        pipe = null;
        if (p != null) { try { p.Dispose(); } catch { } }
    }
}

static class Json
{
    public static string Str(string s)
    {
        StringBuilder sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c); else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    public static string Field(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (!m.Success) return null;
        try { return Regex.Unescape(m.Groups[1].Value); } catch { return m.Groups[1].Value; }
    }
}

static class Web
{
    public static byte[] Get(string url)
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        using (WebClient wc = new WebClient())
        {
            wc.Headers["User-Agent"] = Program.Name + "/" + Program.Version;
            return wc.DownloadData(url);
        }
    }
}

// ================================================================ Theme
static class Theme
{
    public static readonly Color Bg = Color.FromArgb(13, 15, 19);
    public static readonly Color Card = Color.FromArgb(22, 25, 32);
    public static readonly Color CardHi = Color.FromArgb(30, 34, 43);
    public static readonly Color Border = Color.FromArgb(36, 41, 52);
    public static readonly Color Input = Color.FromArgb(13, 15, 19);
    public static readonly Color Text = Color.FromArgb(238, 241, 246);
    public static readonly Color Muted = Color.FromArgb(139, 147, 163);
    public static readonly Color Dim = Color.FromArgb(92, 99, 114);
    public static readonly Color A1 = Color.FromArgb(99, 102, 241);   // indigo (logo left)
    public static readonly Color A2 = Color.FromArgb(34, 211, 238);   // cyan (logo right)
    public static readonly Color Green = Color.FromArgb(59, 214, 113);
    public static readonly Color Red = Color.FromArgb(242, 84, 91);
    public static readonly Color Yellow = Color.FromArgb(245, 181, 68);
    public static readonly Color Link = Color.FromArgb(56, 189, 248);

    public static Image Logo;

    static Theme()
    {
        try
        {
            Stream s = typeof(Theme).Assembly.GetManifestResourceStream("logo.png");
            if (s != null) Logo = Image.FromStream(s);
        }
        catch { }
    }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void DrawImageRounded(Graphics g, Image img, RectangleF r, float rad)
    {
        using (GraphicsPath p = Round(r, rad))
        {
            GraphicsState st = g.Save();
            g.SetClip(p, CombineMode.Intersect);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // "cover" crop so non-square images aren't stretched
            float s = Math.Max(r.Width / img.Width, r.Height / img.Height);
            float w = img.Width * s, h = img.Height * s;
            g.DrawImage(img, r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
            g.Restore(st);
        }
    }

    public static Icon MakeIcon(int size)
    {
        Bitmap bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (Logo != null) g.DrawImage(Logo, 0, 0, size, size);
            else using (SolidBrush b = new SolidBrush(A1)) g.FillEllipse(b, 0, 0, size, size);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static float Scale(Graphics g) { return g.DpiX / 96f; }

    public static void Prep(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static void FillCard(Graphics g, RectangleF r, float k, Color fill)
    {
        using (GraphicsPath p = Round(r, 10 * k))
        {
            using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
            using (Pen pen = new Pen(Border, 1)) g.DrawPath(pen, p);
        }
    }

    public static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}

// ================================================================ Custom controls
class DBControl : Control
{
    public DBControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }
    protected Color ParentBg { get { return Parent != null ? Parent.BackColor : Theme.Bg; } }
}

class Toggle : DBControl
{
    bool on, hover;
    float anim;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    public event EventHandler CheckedChanged;

    public Toggle()
    {
        Size = new Size(42, 24);
        Cursor = Cursors.Hand;
        timer.Interval = 15;
        timer.Tick += delegate
        {
            float target = on ? 1 : 0;
            anim += (target - anim) * 0.3f;
            if (Math.Abs(target - anim) < 0.02f) { anim = target; timer.Stop(); }
            Invalidate();
        };
    }

    public bool Checked
    {
        get { return on; }
        set
        {
            if (on == value) return;
            on = value;
            if (IsHandleCreated && Visible) timer.Start(); else { anim = on ? 1 : 0; Invalidate(); }
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }
    }

    public void SetSilently(bool v) { on = v; anim = v ? 1 : 0; Invalidate(); }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(BackColor);
        RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
        using (GraphicsPath p = Theme.Round(r, r.Height / 2))
        {
            Color off = hover ? Color.FromArgb(58, 64, 78) : Color.FromArgb(46, 51, 63);
            using (SolidBrush b = new SolidBrush(off)) g.FillPath(b, p);
            if (anim > 0.01f)
            {
                int a = (int)(255 * anim);
                using (LinearGradientBrush lg = new LinearGradientBrush(r, Color.FromArgb(a, Theme.A1), Color.FromArgb(a, Theme.A2), 0f))
                    g.FillPath(lg, p);
            }
        }
        float d = r.Height - 6;
        float x = r.X + 3 + (r.Width - d - 6) * anim;
        g.FillEllipse(Brushes.White, x, r.Y + 3, d, d);
    }

    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}

class CardPanel : Panel
{
    public List<int> Dividers = new List<int>();
    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
        Theme.Prep(g);
        float k = Theme.Scale(g);
        Theme.FillCard(g, new RectangleF(0, 0, Width - 1, Height - 1), k, Theme.Card);
        using (Pen pen = new Pen(Theme.Border))
            foreach (int y in Dividers)
            {
                float yy = y * k;
                g.DrawLine(pen, 16 * k, yy, Width - 16 * k, yy);
            }
    }
}

class GradientButton : DBControl
{
    bool hover, down;
    public GradientButton() { Cursor = Cursors.Hand; Font = new Font("Segoe UI Semibold", 9.5f); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(ParentBg);
        float k = Theme.Scale(g);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        Color c1 = Theme.A1, c2 = Theme.A2;
        if (hover) { c1 = Theme.Mix(c1, Color.White, 0.12f); c2 = Theme.Mix(c2, Color.White, 0.12f); }
        if (down) { c1 = Theme.Mix(c1, Color.Black, 0.15f); c2 = Theme.Mix(c2, Color.Black, 0.15f); }
        using (GraphicsPath p = Theme.Round(r, 8 * k))
        using (LinearGradientBrush lg = new LinearGradientBrush(r, c1, c2, 20f)) g.FillPath(lg, p);
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

class InputBox : Panel
{
    public readonly TextBox Box = new TextBox();
    bool focused;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr w, string l);

    public InputBox(string text, string placeholder, Font font)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Input;
        Box.BorderStyle = BorderStyle.None;
        Box.BackColor = Theme.Input;
        Box.ForeColor = Theme.Text;
        Box.Font = font;
        Box.Text = text;
        Box.HandleCreated += delegate { SendMessageW(Box.Handle, 0x1501, (IntPtr)1, placeholder); }; // placeholder text
        Box.GotFocus += delegate { focused = true; Invalidate(); };
        Box.LostFocus += delegate { focused = false; Invalidate(); };
        Controls.Add(Box);
        Cursor = Cursors.IBeam;
        Click += delegate { Box.Focus(); };
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int pad = (int)(10 * Dpi);
        Box.SetBounds(pad, (Height - Box.Height) / 2, Width - pad * 2, Box.Height);
    }

    static float dpi;
    static float Dpi
    {
        get
        {
            if (dpi == 0) using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96f;
            return dpi;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
        Theme.Prep(g);
        float k = Theme.Scale(g);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using (GraphicsPath p = Theme.Round(r, 7 * k))
        {
            using (SolidBrush b = new SolidBrush(Theme.Input)) g.FillPath(b, p);
            if (focused) using (LinearGradientBrush lg = new LinearGradientBrush(r, Theme.A1, Theme.A2, 0f)) using (Pen pen = new Pen(lg, 1.5f * k)) g.DrawPath(pen, p);
            else using (Pen pen = new Pen(Theme.Border, 1)) g.DrawPath(pen, p);
        }
    }
}

class RingGauge : DBControl
{
    public string Label = "", Sub1 = "", Sub2 = "", ValueText = "--";
    public Color C1 = Theme.A1, C2 = Theme.A2;
    double target, shown;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

    public RingGauge()
    {
        timer.Interval = 16;
        timer.Tick += delegate
        {
            shown += (target - shown) * 0.18;
            if (Math.Abs(target - shown) < 0.2) { shown = target; timer.Stop(); }
            Invalidate();
        };
    }

    public void SetValue(double percent)
    {
        target = Math.Max(0, Math.Min(100, percent));
        if (!timer.Enabled) timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(ParentBg);
        float k = Theme.Scale(g);
        float d = 70 * k, th = 7 * k;
        RectangleF ring = new RectangleF((Width - d) / 2, 6 * k, d, d);
        using (Pen track = new Pen(Color.FromArgb(34, 39, 50), th)) g.DrawEllipse(track, ring);
        float sweep = (float)(360 * shown / 100);
        if (sweep > 0.5f)
        {
            using (LinearGradientBrush lg = new LinearGradientBrush(ring, C1, C2, 45f))
            using (Pen pen = new Pen(lg, th))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawArc(pen, ring, -90, sweep);
            }
        }
        using (Font fv = new Font("Segoe UI Semibold", 12f))
            TextRenderer.DrawText(g, ValueText, fv, Rectangle.Round(ring), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        Rectangle row = new Rectangle(0, (int)(ring.Bottom + 8 * k), Width, (int)(18 * k));
        using (Font fl = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            TextRenderer.DrawText(g, Label, fl, row, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        using (Font fs = new Font("Segoe UI", 8f))
        {
            row.Y += (int)(18 * k);
            TextRenderer.DrawText(g, Sub1, fs, row, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            row.Y += (int)(16 * k);
            TextRenderer.DrawText(g, Sub2, fs, row, Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}

// Mimics the activity card on a Discord profile
class PreviewCard : DBControl
{
    public string AppName = Program.Name, Details = "", State = "", Elapsed = "";
    public Image Picture, SmallIcon;
    public bool Paused, Offline;
    // Button area under the activity (main window only): the user's own button as others see it,
    // or a dashed "+ Add your own button" placeholder. Clicking it raises ButtonAreaClick.
    public bool ShowButtonArea;
    public string ButtonLabel = "";
    public event EventHandler ButtonAreaClick;
    bool hoverBtn;
    float lastK = 1f;

    Rectangle ButtonRect()
    {
        float k = lastK;
        return new Rectangle((int)(16 * k), (int)(126 * k), (int)(Width - 32 * k), (int)(34 * k));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool h = ShowButtonArea && ButtonRect().Contains(e.Location);
        if (h != hoverBtn) { hoverBtn = h; Cursor = h ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverBtn) { hoverBtn = false; Cursor = Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (ShowButtonArea && ButtonRect().Contains(e.Location) && ButtonAreaClick != null) ButtonAreaClick(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(ParentBg);
        float k = Theme.Scale(g);
        lastK = k;
        Theme.FillCard(g, new RectangleF(0, 0, Width - 1, Height - 1), k, Theme.Card);

        if (ShowButtonArea)
        {
            Rectangle br = ButtonRect();
            using (GraphicsPath bp = Theme.Round(br, 6 * k))
            {
                if (ButtonLabel.Length > 0 && !Paused)
                {
                    // looks like Discord's activity button
                    using (SolidBrush bb = new SolidBrush(hoverBtn ? Color.FromArgb(58, 60, 68) : Color.FromArgb(46, 48, 55))) g.FillPath(bb, bp);
                    using (Pen bpen = new Pen(Color.FromArgb(70, 72, 80), 1)) g.DrawPath(bpen, bp);
                    using (Font fb = new Font("Segoe UI Semibold", 9f))
                        TextRenderer.DrawText(g, ButtonLabel, fb, br, Theme.Text,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                }
                else if (!Paused)
                {
                    using (Pen dp = new Pen(hoverBtn ? Theme.Link : Theme.Dim, 1)) { dp.DashStyle = DashStyle.Dash; g.DrawPath(dp, bp); }
                    using (Font fb = new Font("Segoe UI", 8.5f))
                        TextRenderer.DrawText(g, "+ Add your own button (YouTube, Twitch...)", fb, br, hoverBtn ? Theme.Link : Theme.Dim,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                }
            }
        }

        using (Font fHead = new Font("Segoe UI Semibold", 8.5f))
            TextRenderer.DrawText(g, Paused ? "Paused" : "Playing", fHead, new Point((int)(16 * k), (int)(12 * k)), Theme.Muted);

        RectangleF img = new RectangleF(16 * k, 38 * k, 72 * k, 72 * k);
        Image pic = Picture ?? Theme.Logo;
        if (pic != null) Theme.DrawImageRounded(g, pic, img, 10 * k);
        if (SmallIcon != null && !Paused)
        {
            // game icon badge in the corner, like Discord's small image
            float sd = 28 * k, ring = 3 * k;
            RectangleF b = new RectangleF(img.Right - sd + 4 * k, img.Bottom - sd + 4 * k, sd, sd);
            using (SolidBrush rb = new SolidBrush(Theme.Card)) g.FillEllipse(rb, b.X - ring, b.Y - ring, sd + ring * 2, sd + ring * 2);
            using (GraphicsPath cp = new GraphicsPath())
            {
                cp.AddEllipse(b);
                GraphicsState st = g.Save();
                g.SetClip(cp, CombineMode.Intersect);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(SmallIcon, b);
                g.Restore(st);
            }
        }
        if (Paused || Offline)
            using (SolidBrush veil = new SolidBrush(Color.FromArgb(150, Theme.Card))) using (GraphicsPath p = Theme.Round(img, 10 * k)) g.FillPath(veil, p);

        int tx = (int)(img.Right + 14 * k), tw = (int)(Width - tx - 14 * k);
        TextFormatFlags f = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        using (Font fName = new Font("Segoe UI", 10f, FontStyle.Bold))
            TextRenderer.DrawText(g, AppName, fName, new Rectangle(tx, (int)(40 * k), tw, (int)(20 * k)), Theme.Text, f);
        using (Font fLine = new Font("Segoe UI", 9f))
        {
            if (Paused)
                TextRenderer.DrawText(g, "Stats are hidden from your profile", fLine, new Rectangle(tx, (int)(62 * k), tw, (int)(18 * k)), Theme.Muted, f);
            else
            {
                TextRenderer.DrawText(g, Details, fLine, new Rectangle(tx, (int)(61 * k), tw, (int)(18 * k)), Theme.Text, f);
                TextRenderer.DrawText(g, State, fLine, new Rectangle(tx, (int)(78 * k), tw, (int)(18 * k)), Theme.Text, f);
                if (Elapsed.Length > 0)
                {
                    // little gamepad + timer, like Discord
                    float gx = tx, gy = 98 * k;
                    using (SolidBrush gb = new SolidBrush(Theme.Green))
                    using (GraphicsPath pad = Theme.Round(new RectangleF(gx, gy + 2 * k, 15 * k, 9 * k), 4 * k)) g.FillPath(gb, pad);
                    using (SolidBrush cut = new SolidBrush(Theme.Card))
                    {
                        g.FillRectangle(cut, gx + 3 * k, gy + 6 * k, 4 * k, 1.2f * k);
                        g.FillRectangle(cut, gx + 4.4f * k, gy + 4.6f * k, 1.2f * k, 4 * k);
                        g.FillEllipse(cut, gx + 10 * k, gy + 5 * k, 2 * k, 2 * k);
                    }
                    using (Font fT = new Font("Segoe UI Semibold", 8.5f))
                        TextRenderer.DrawText(g, Elapsed, fT, new Point((int)(gx + 20 * k), (int)(gy - 1 * k)), Theme.Green);
                }
            }
        }
    }
}

class NowPlayingCard : DBControl
{
    public string Title = "No game detected", Sub = "Start a game and RigCord picks it up automatically";
    public Image GameIcon;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(ParentBg);
        float k = Theme.Scale(g);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        Theme.FillCard(g, r, k, Theme.Card);
        RectangleF box = new RectangleF(14 * k, (Height - 40 * k) / 2, 40 * k, 40 * k);
        using (GraphicsPath p = Theme.Round(box, 9 * k))
        {
            if (GameIcon != null)
            {
                using (SolidBrush b = new SolidBrush(Theme.CardHi)) g.FillPath(b, p);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float s = 28 * k;
                g.DrawImage(GameIcon, box.X + (box.Width - s) / 2, box.Y + (box.Height - s) / 2, s, s);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Theme.CardHi)) g.FillPath(b, p);
                // simple gamepad glyph
                using (SolidBrush gb = new SolidBrush(Theme.Dim))
                using (GraphicsPath pad = Theme.Round(new RectangleF(box.X + 9 * k, box.Y + 14 * k, 22 * k, 13 * k), 6 * k)) g.FillPath(gb, pad);
                using (SolidBrush cut = new SolidBrush(Theme.CardHi))
                {
                    g.FillRectangle(cut, box.X + 13 * k, box.Y + 19.5f * k, 6 * k, 2 * k);
                    g.FillRectangle(cut, box.X + 15 * k, box.Y + 17.5f * k, 2 * k, 6 * k);
                    g.FillEllipse(cut, box.X + 23 * k, box.Y + 18 * k, 3 * k, 3 * k);
                }
            }
        }
        int tx = (int)(box.Right + 12 * k), tw = (int)(Width - tx - 12 * k);
        TextFormatFlags f = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        using (Font ft = new Font("Segoe UI Semibold", 10f))
            TextRenderer.DrawText(g, Title, ft, new Rectangle(tx, (int)(Height / 2 - 20 * k), tw, (int)(20 * k)), GameIcon != null ? Theme.Text : Theme.Muted, f);
        using (Font fs = new Font("Segoe UI", 8.5f))
            TextRenderer.DrawText(g, Sub, fs, new Rectangle(tx, (int)(Height / 2 + 2 * k), tw, (int)(18 * k)), Theme.Muted, f);
    }
}

class Header : DBControl
{
    public string Status = "Starting...";
    public Color StatusColor = Theme.Muted;

    public Header() { BackColor = Theme.Bg; }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(Theme.Bg);
        float k = Theme.Scale(g);
        // soft brand glow along the bottom edge
        using (LinearGradientBrush glow = new LinearGradientBrush(new RectangleF(0, Height - 2 * k, Width, 2 * k), Theme.A1, Theme.A2, 0f))
            g.FillRectangle(glow, 0, Height - 2 * k, Width, 2 * k);

        if (Theme.Logo != null) Theme.DrawImageRounded(g, Theme.Logo, new RectangleF(20 * k, 16 * k, 50 * k, 50 * k), 12 * k);

        using (Font ft = new Font("Segoe UI", 19f, FontStyle.Bold))
        {
            RectangleF tr = new RectangleF(82 * k, 12 * k, 200 * k, 36 * k);
            SizeF sz = g.MeasureString(Program.Name, ft);
            using (LinearGradientBrush tb = new LinearGradientBrush(new RectangleF(tr.X, tr.Y, sz.Width, sz.Height), Theme.Mix(Theme.A1, Color.White, 0.25f), Theme.A2, 0f))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(Program.Name, ft, tb, tr.X - 4 * k, tr.Y);
            }
        }
        using (Font fs = new Font("Segoe UI", 9f))
            TextRenderer.DrawText(g, "Your rig, live on Discord", fs, new Point((int)(84 * k), (int)(48 * k)), Theme.Muted);

        // status pill
        using (Font fp = new Font("Segoe UI Semibold", 8.5f))
        {
            Size ts = TextRenderer.MeasureText(Status, fp);
            float pw = ts.Width + 34 * k, ph = 28 * k;
            RectangleF pill = new RectangleF(Width - pw - 20 * k, (Height - ph) / 2 - 1 * k, pw, ph);
            using (GraphicsPath p = Theme.Round(pill, ph / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(28, StatusColor))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(70, StatusColor))) g.DrawPath(pen, p);
            }
            float dd = 8 * k;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, StatusColor))) g.FillEllipse(b, pill.X + 10 * k, pill.Y + (ph - dd * 1.8f) / 2, dd * 1.8f, dd * 1.8f);
            using (SolidBrush b = new SolidBrush(StatusColor)) g.FillEllipse(b, pill.X + 10 * k + dd * 0.4f, pill.Y + (ph - dd) / 2, dd, dd);
            TextRenderer.DrawText(g, Status, fp, new Point((int)(pill.X + 26 * k), (int)(pill.Y + (ph - ts.Height) / 2)), StatusColor);
        }
    }
}

class DarkMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
    public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
    public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    public override Color MenuBorder { get { return Theme.Border; } }
    public override Color MenuItemBorder { get { return Theme.CardHi; } }
    public override Color MenuItemSelected { get { return Theme.CardHi; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.CardHi; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.CardHi; } }
    public override Color SeparatorDark { get { return Theme.Border; } }
    public override Color SeparatorLight { get { return Theme.Card; } }
    public override Color CheckBackground { get { return Theme.Card; } }
    public override Color CheckSelectedBackground { get { return Theme.CardHi; } }
    public override Color CheckPressedBackground { get { return Theme.CardHi; } }
}

class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkMenuColors()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Muted;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = e.ImageRectangle;
        using (Pen p = new Pen(Theme.A2, 2f))
            g.DrawLines(p, new PointF[] {
                new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.55f),
                new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.75f),
                new PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f) });
    }
}

// ================================================================ Main window
class MainForm : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int W = 860, H = 672;
    readonly TrayApp app;
    readonly Settings settings;
    Header header;
    PreviewCard preview;
    NowPlayingCard nowPlaying;
    RingGauge gCpu, gGpu, gRam, gVram;
    Toggle tgGame, tgNames, tgVram, tgTemp, tgElapsed, tgStartup, tgPause;
    InputBox idBox;
    LinkLabel advLink, setupSteps, portalLink;
    CardPanel discordCard;
    float uiK = 1f;
    CardPanel disp;
    Label appNameLabel, hint;
    LinkLabel updateLink;
    System.Windows.Forms.Timer refresh;
    string shownGamePath;

    public MainForm(TrayApp app, Settings settings)
    {
        this.app = app;
        this.settings = settings;
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None; // scaled manually below; fonts already follow DPI
        Text = Program.Name;
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(W, H);
        Icon = app.AppIcon;

        header = new Header();
        header.SetBounds(0, 0, W, 84);
        Controls.Add(header);

        // ---------- left column
        int lx = 20, lw = 420;
        AddSection("PROFILE PREVIEW", lx, 100);
        preview = new PreviewCard();
        preview.SetBounds(lx, 120, lw, 174);
        preview.ShowButtonArea = true;
        preview.ButtonAreaClick += delegate { using (AdvancedForm f = new AdvancedForm(app, settings)) { f.FocusButtonField = true; f.ShowDialog(this); } RefreshView(); };
        Controls.Add(preview);

        AddSection("LIVE USAGE", lx, 308);
        CardPanel live = new CardPanel();
        live.SetBounds(lx, 328, lw, 150);
        Controls.Add(live);
        int gw = (lw - 16) / 4;
        gCpu = MakeGauge(live, "CPU", 8, gw, Color.FromArgb(129, 140, 248), Color.FromArgb(99, 102, 241));
        gGpu = MakeGauge(live, "GPU", 8 + gw, gw, Color.FromArgb(34, 211, 238), Color.FromArgb(56, 189, 248));
        gRam = MakeGauge(live, "RAM", 8 + gw * 2, gw, Color.FromArgb(167, 139, 250), Color.FromArgb(129, 140, 248));
        gVram = MakeGauge(live, "VRAM", 8 + gw * 3, gw, Color.FromArgb(45, 212, 191), Color.FromArgb(34, 211, 238));

        AddSection("NOW PLAYING", lx, 494);
        nowPlaying = new NowPlayingCard();
        nowPlaying.SetBounds(lx, 514, lw, 64);
        Controls.Add(nowPlaying);

        hint = MakeLabel("Closing this window keeps RigCord running in the tray.", 8.5f, FontStyle.Regular, Theme.Dim);
        hint.Location = new Point(lx + 2, 600);
        Controls.Add(hint);
        updateLink = new LinkLabel();
        updateLink.AutoSize = true;
        updateLink.Font = new Font("Segoe UI Semibold", 9f);
        updateLink.LinkColor = Theme.Green;
        updateLink.ActiveLinkColor = Theme.Text;
        updateLink.LinkBehavior = LinkBehavior.HoverUnderline;
        updateLink.Location = new Point(lx, 598);
        updateLink.Visible = false;
        updateLink.LinkClicked += delegate { app.OpenUpdate(); };
        Controls.Add(updateLink);
        int fx = lx;
        fx = AddFooterLink("Advanced settings", fx, 624, Theme.Link, delegate { using (AdvancedForm f = new AdvancedForm(app, settings)) f.ShowDialog(this); RefreshView(); });
        if (settings.Link.Length > 0) fx = AddFooterLink("GitHub", fx, 624, Theme.Link, delegate { Open(settings.Link); });
        AddFooterLink("Quit RigCord", fx, 624, Theme.Red, delegate { app.Exit(); });

        // ---------- right column
        int rx = 460, rw = 380;
        AddSection("DISPLAY", rx, 100);
        advLink = new LinkLabel();
        advLink.Text = "✎ Advanced";
        advLink.AutoSize = true;
        advLink.Font = new Font("Segoe UI Semibold", 8.5f);
        advLink.LinkColor = Theme.Link;
        advLink.ActiveLinkColor = Theme.Text;
        advLink.LinkBehavior = LinkBehavior.HoverUnderline;
        advLink.Location = new Point(rx + rw - 80, 99);
        advLink.LinkClicked += delegate { using (TextEditorForm f = new TextEditorForm(app, settings)) f.ShowDialog(this); RefreshView(); };
        Controls.Add(advLink);
        disp = new CardPanel();
        disp.SetBounds(rx, 120, rw, 5 * 48);
        disp.Dividers.AddRange(new int[] { 48, 96, 144, 192 });
        Controls.Add(disp);
        tgGame = AddRow(disp, 0, "Show current game", "Detects what you're playing", settings.ShowGame);
        tgNames = AddRow(disp, 48, "Show CPU & GPU names", "e.g. Ryzen 5 2600X 22%", settings.ShowNames);
        tgVram = AddRow(disp, 96, "Show VRAM usage", "Video memory next to RAM", settings.ShowVram);
        tgTemp = AddRow(disp, 144, "Show GPU temperature", "NVIDIA cards only", settings.ShowTemp);
        tgElapsed = AddRow(disp, 192, "Show elapsed time", "Timer on your profile", settings.ShowElapsed);

        AddSection("GENERAL", rx, 376);
        CardPanel gen = new CardPanel();
        gen.SetBounds(rx, 396, rw, 96);
        gen.Dividers.Add(48);
        Controls.Add(gen);
        tgStartup = AddRow(gen, 0, "Start with Windows", "Runs quietly in the tray", TrayApp.IsStartupEnabled());
        tgPause = AddRow(gen, 48, "Pause presence", "Hide stats from your profile", settings.Paused);

        AddSection("DISCORD", rx, 508);
        CardPanel dc = new CardPanel();
        dc.SetBounds(rx, 528, rw, 84);
        Controls.Add(dc);
        idBox = new InputBox(settings.ClientId, "Discord Application ID", new Font("Consolas", 10f));
        idBox.SetBounds(14, 14, rw - 28 - 92, 34);
        dc.Controls.Add(idBox);
        GradientButton save = new GradientButton();
        save.Text = "Save";
        save.SetBounds(rw - 14 - 84, 14, 84, 34);
        save.Click += delegate { SaveDiscord(); };
        dc.Controls.Add(save);
        appNameLabel = MakeLabel("", 8.5f, FontStyle.Regular, Theme.Muted);
        appNameLabel.BackColor = Theme.Card;
        appNameLabel.Location = new Point(14, 58);
        dc.Controls.Add(appNameLabel);
        LinkLabel portal = new LinkLabel();
        portal.Text = "Developer Portal \u2197";
        portal.AutoSize = true;
        portal.BackColor = Theme.Card;
        portal.LinkColor = Theme.Link;
        portal.ActiveLinkColor = Theme.Text;
        portal.LinkBehavior = LinkBehavior.HoverUnderline;
        portal.Location = new Point(rw - 120, 58);
        portal.LinkClicked += delegate { Open("https://discord.com/developers/applications"); };
        dc.Controls.Add(portal);

        // first-run guide, shown only while no Application ID is saved (the card grows to fit it)
        const string s1 = "1.  Open the ", s1Link = "Developer Portal", s1End = " → New Application\n";
        setupSteps = new LinkLabel();
        setupSteps.Text = s1 + s1Link + s1End +
            "2.  Copy its Application ID, paste it above, press Save\n" +
            "3.  In Discord: Activity Privacy → Share your activities";
        setupSteps.LinkArea = new LinkArea(s1.Length, s1Link.Length);
        setupSteps.UseMnemonic = false;
        setupSteps.AutoSize = true;
        setupSteps.Font = new Font("Segoe UI", 8.5f);
        setupSteps.ForeColor = Theme.Text;
        setupSteps.BackColor = Theme.Card;
        setupSteps.LinkColor = Theme.Link;
        setupSteps.ActiveLinkColor = Theme.Text;
        setupSteps.LinkBehavior = LinkBehavior.HoverUnderline;
        setupSteps.Location = new Point(14, 58);
        portalLink = portal;
        setupSteps.LinkClicked += delegate { Open("https://discord.com/developers/applications"); };
        dc.Controls.Add(setupSteps);
        discordCard = dc;

        tgGame.CheckedChanged += delegate { settings.ShowGame = tgGame.Checked; app.SettingsChanged(); };
        tgNames.CheckedChanged += delegate { settings.ShowNames = tgNames.Checked; app.SettingsChanged(); };
        tgVram.CheckedChanged += delegate { settings.ShowVram = tgVram.Checked; app.SettingsChanged(); };
        tgTemp.CheckedChanged += delegate { settings.ShowTemp = tgTemp.Checked; app.SettingsChanged(); };
        tgElapsed.CheckedChanged += delegate { settings.ShowElapsed = tgElapsed.Checked; app.SettingsChanged(); };
        tgPause.CheckedChanged += delegate { settings.Paused = tgPause.Checked; app.SettingsChanged(); };
        tgStartup.CheckedChanged += delegate { TrayApp.SetStartup(tgStartup.Checked); app.SettingsChanged(); };

        float k;
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
        if (Math.Abs(k - 1f) > 0.01f)
        {
            ScaleTree(this, k);
            ClientSize = new Size((int)Math.Round(W * k), (int)Math.Round(H * k));
        }
        uiK = k;
        ResumeLayout(false);

        refresh = new System.Windows.Forms.Timer();
        refresh.Interval = 1000;
        refresh.Tick += delegate { RefreshView(); };
        refresh.Start();
        RefreshView();
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; } // WS_EX_COMPOSITED: no flicker
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1;
        try { if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4); } catch { }
        try { int corner = 2; DwmSetWindowAttribute(Handle, 33, ref corner, 4); } catch { }            // Win11 rounded corners
        try { int cap = 0x00130F0D; DwmSetWindowAttribute(Handle, 35, ref cap, 4); } catch { }         // Win11 caption color = Bg
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        refresh.Stop();
        refresh.Dispose();
        base.OnFormClosed(e);
    }

    public void SyncToggles()
    {
        tgNames.SetSilently(settings.ShowNames);
        tgGame.SetSilently(settings.ShowGame);
        tgPause.SetSilently(settings.Paused);
        tgStartup.SetSilently(TrayApp.IsStartupEnabled());
        RefreshView();
    }

    public void RefreshView()
    {
        Snapshot s = app.Latest;
        string status; Color c;
        app.GetStatus(out status, out c);
        if (header.Status != status || header.StatusColor != c) { header.Status = status; header.StatusColor = c; header.Invalidate(); }

        if (s != null)
        {
            gCpu.ValueText = Math.Round(s.Cpu) + "%"; gCpu.SetValue(s.Cpu);
            gCpu.Sub1 = Stats.CpuName; gCpu.Sub2 = "";
            gGpu.ValueText = s.Gpu < 0 ? "N/A" : Math.Round(s.Gpu) + "%"; gGpu.SetValue(Math.Max(0, s.Gpu));
            gGpu.Sub1 = Stats.GpuName; gGpu.Sub2 = s.GpuTemp >= 0 ? s.GpuTemp + "\u00B0C" : "";
            double rp = s.RamTotal > 0 ? s.RamUsed * 100 / s.RamTotal : 0;
            gRam.ValueText = Math.Round(rp) + "%"; gRam.SetValue(rp);
            gRam.Sub1 = s.RamUsed.ToString("0.0", CultureInfo.InvariantCulture) + " / " + Math.Round(s.RamTotal) + " GB"; gRam.Sub2 = "";
            if (s.VramUsed >= 0 && s.VramTotal > 0)
            {
                double vp = s.VramUsed * 100 / s.VramTotal;
                gVram.ValueText = Math.Round(vp) + "%"; gVram.SetValue(vp);
                gVram.Sub1 = s.VramUsed.ToString("0.0", CultureInfo.InvariantCulture) + " / " + Math.Round(s.VramTotal) + " GB";
            }
            else { gVram.ValueText = "N/A"; gVram.SetValue(0); gVram.Sub1 = ""; }
            gCpu.Invalidate(); gGpu.Invalidate(); gRam.Invalidate(); gVram.Invalidate();

            preview.Details = s.Details;
            preview.State = s.State;

            GameInfo game = s.Game;
            if (game != null)
            {
                nowPlaying.Title = game.Name;
                nowPlaying.Sub = settings.ShowGame ? "Shown on your profile" : "Detected, but hidden (Show current game is off)";
                if (game.Icon != null) nowPlaying.GameIcon = game.Icon;
                else if (shownGamePath != game.Path)
                {
                    try { using (Icon ic = Icon.ExtractAssociatedIcon(game.Path)) nowPlaying.GameIcon = ic.ToBitmap(); }
                    catch { nowPlaying.GameIcon = null; }
                }
                shownGamePath = game.Path;
                preview.SmallIcon = settings.ShowGame ? game.Icon : null;
            }
            else
            {
                nowPlaying.Title = "No game detected";
                nowPlaying.Sub = "Start a game and RigCord picks it up automatically";
                nowPlaying.GameIcon = null;
                preview.SmallIcon = null;
                shownGamePath = null;
            }
            nowPlaying.Invalidate();
        }

        preview.AppName = settings.AppName.Length > 0 ? settings.AppName : Program.Name;
        preview.Picture = null; // always the RigCord logo
        preview.ButtonLabel = settings.ButtonLabel.Length > 0 && settings.ButtonUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? settings.ButtonLabel : "";
        preview.Paused = settings.Paused;
        preview.Offline = !app.IsConnected;
        if (settings.ShowElapsed)
        {
            TimeSpan t = DateTime.UtcNow - app.TimerStartUtc;
            if (t.Ticks < 0) t = TimeSpan.Zero;
            preview.Elapsed = (t.TotalHours >= 1 ? ((int)t.TotalHours) + ":" : "") + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        }
        else preview.Elapsed = "";
        preview.Invalidate();

        bool upd = app.UpdateVersion.Length > 0;
        if (upd) updateLink.Text = "⬆ RigCord " + app.UpdateVersion + " is available - click to download";
        updateLink.Visible = upd;
        hint.Visible = !upd;

        // "Advanced" turns yellow while custom text overrides the Display toggles
        bool custom = (settings.Line1 + settings.Line2 + settings.GameLine1 + settings.GameLine2).Trim().Length > 0;
        string at = custom ? "✎ Advanced • custom text on" : "✎ Advanced";
        if (advLink.Text != at) advLink.Text = at;
        advLink.LinkColor = custom ? Theme.Yellow : Theme.Link;
        advLink.Left = disp.Right - advLink.Width;

        bool setup = settings.ClientId.Length == 0;
        setupSteps.Visible = setup;
        appNameLabel.Visible = !setup;
        portalLink.Visible = !setup;
        int cardH = (int)Math.Round((setup ? 124 : 84) * uiK);
        if (discordCard.Height != cardH) discordCard.Height = cardH;
        appNameLabel.Text = setup
            ? ""
            : "Shows as \"Playing " + (settings.AppName.Length > 0 ? settings.AppName : "...") + "\"";
    }

    void SaveDiscord()
    {
        string id = Regex.Replace(idBox.Box.Text, @"\D", "");
        if (id == settings.ClientId) return;
        if (id.Length < 15)
        {
            MessageBox.Show(this, "The Application ID is a long number, e.g. 1234567890123456789.\nCopy it from General Information on the Developer Portal.",
                Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        idBox.Box.Text = id;
        app.SetClientId(id);
    }

    static void Open(string url) { try { Process.Start(url); } catch { } }

    internal static void DarkTitleBar(Form f)
    {
        int on = 1;
        try { if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4); } catch { }
        try { int corner = 2; DwmSetWindowAttribute(f.Handle, 33, ref corner, 4); } catch { }
        try { int cap = 0x00130F0D; DwmSetWindowAttribute(f.Handle, 35, ref cap, 4); } catch { }
    }

    internal static float ScreenScale()
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
    }

    internal static void ScaleTree(Control parent, float k)
    {
        foreach (Control c in parent.Controls)
        {
            c.SetBounds((int)Math.Round(c.Left * k), (int)Math.Round(c.Top * k), (int)Math.Round(c.Width * k), (int)Math.Round(c.Height * k));
            ScaleTree(c, k);
        }
    }

    internal static Label MakeLabel(string text, float size, FontStyle style, Color color)
    {
        Label l = new Label();
        l.UseMnemonic = false;
        l.Text = text;
        l.AutoSize = true;
        l.Font = new Font("Segoe UI", size, style);
        l.ForeColor = color;
        l.BackColor = Color.Transparent;
        return l;
    }

    void AddSection(string text, int x, int y)
    {
        Label l = MakeLabel(text, 8f, FontStyle.Bold, Theme.Dim);
        l.Location = new Point(x + 2, y);
        Controls.Add(l);
    }

    int AddFooterLink(string text, int x, int y, Color color, LinkLabelLinkClickedEventHandler onClick)
    {
        LinkLabel l = new LinkLabel();
        l.Text = text;
        l.AutoSize = true;
        l.LinkColor = color;
        l.ActiveLinkColor = Theme.Text;
        l.LinkBehavior = LinkBehavior.HoverUnderline;
        l.Location = new Point(x, y);
        l.LinkClicked += onClick;
        Controls.Add(l);
        return x + TextRenderer.MeasureText(text, l.Font).Width + 14;
    }

    RingGauge MakeGauge(Control parent, string label, int x, int w, Color c1, Color c2)
    {
        RingGauge gg = new RingGauge();
        gg.Label = label;
        gg.C1 = c1; gg.C2 = c2;
        gg.BackColor = Theme.Card;
        gg.SetBounds(x, 8, w, 136);
        parent.Controls.Add(gg);
        return gg;
    }

    Toggle AddRow(CardPanel card, int y, string title, string sub, bool value)
    {
        Label t = MakeLabel(title, 9.5f, FontStyle.Regular, Theme.Text);
        t.BackColor = Theme.Card;
        t.Location = new Point(16, y + 6);
        card.Controls.Add(t);
        Label s = MakeLabel(sub, 8f, FontStyle.Regular, Theme.Muted);
        s.BackColor = Theme.Card;
        s.Location = new Point(17, y + 26);
        card.Controls.Add(s);
        Toggle tg = new Toggle();
        tg.BackColor = Theme.Card;
        tg.SetSilently(value);
        tg.SetBounds(card.Width - 16 - 42, y + 12, 42, 24);
        card.Controls.Add(tg);
        return tg;
    }
}

// ================================================================ Profile text editor
class Chip : DBControl
{
    bool hover;
    public Chip(string text) { Text = text; Cursor = Cursors.Hand; Font = new Font("Consolas", 9f); BackColor = Theme.Bg; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Prep(g);
        g.Clear(ParentBg);
        float k = Theme.Scale(g);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using (GraphicsPath p = Theme.Round(r, r.Height / 2))
        {
            using (SolidBrush b = new SolidBrush(hover ? Theme.CardHi : Theme.Card)) g.FillPath(b, p);
            if (hover) using (LinearGradientBrush lg = new LinearGradientBrush(r, Theme.A1, Theme.A2, 0f)) using (Pen pen = new Pen(lg, 1.2f * k)) g.DrawPath(pen, p);
            else using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), hover ? Theme.Text : Theme.A2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

class TextEditorForm : Form
{
    const int W = 560, H = 636;
    readonly TrayApp app;
    readonly Settings settings;
    readonly InputBox l1, l2, g1, g2;
    readonly PreviewCard preview;
    readonly Label previewLabel;
    TextBox last;
    bool gameMode;

    public TextEditorForm(TrayApp app, Settings settings)
    {
        this.app = app;
        this.settings = settings;
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None;
        Text = "Advanced display";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(W, H);
        Icon = app.AppIcon;

        Label title = MainForm.MakeLabel("Advanced display", 14f, FontStyle.Bold, Theme.Text);
        title.Location = new Point(20, 14);
        Controls.Add(title);
        Label sub = MainForm.MakeLabel("Your own lines override the Display toggles. Empty line = use Display settings.", 9f, FontStyle.Regular, Theme.Muted);
        sub.Location = new Point(22, 46);
        Controls.Add(sub);

        Section("WHEN YOU'RE NOT IN A GAME", 80);
        l1 = Input(settings.Line1, "Line 1   (default: CPU {cpu} • GPU {gpu})", 100, false);
        l2 = Input(settings.Line2, "Line 2   (default: RAM {ram})", 140, false);
        Section("WHEN YOU'RE IN A GAME", 188);
        g1 = Input(settings.GameLine1, "Line 1   (default: In {game})", 208, true);
        g2 = Input(settings.GameLine2, "Line 2   (default: CPU {cpu} • GPU {gpu} • RAM {ram})", 248, true);

        Section("PLACEHOLDERS  -  click to insert", 296);
        string[] tokens = { "{game}", "{cpu}", "{gpu}", "{temp}", "{ram}", "{ram%}", "{vram}", "{cpuname}", "{gpuname}" };
        int x = 20, y = 316;
        foreach (string t in tokens)
        {
            int w = TextRenderer.MeasureText(t, new Font("Consolas", 9f)).Width + 22;
            if (x + w > W - 20) { x = 20; y += 34; }
            Chip c = new Chip(t);
            c.SetBounds(x, y, w, 28);
            string token = t;
            c.Click += delegate { Insert(token); };
            Controls.Add(c);
            x += w + 8;
        }
        Label ex = MainForm.MakeLabel("Example:  Grinding {game} on a {gpuname}", 8.5f, FontStyle.Italic, Theme.Dim);
        ex.Location = new Point(22, y + 38);
        Controls.Add(ex);

        previewLabel = MainForm.MakeLabel("PREVIEW", 8f, FontStyle.Bold, Theme.Dim);
        previewLabel.Location = new Point(22, 418);
        Controls.Add(previewLabel);
        preview = new PreviewCard();
        preview.SetBounds(20, 438, W - 40, 128);
        Controls.Add(preview);

        LinkLabel reset = new LinkLabel();
        reset.Text = "Reset to default";
        reset.AutoSize = true;
        reset.LinkColor = Theme.Muted;
        reset.ActiveLinkColor = Theme.Text;
        reset.LinkBehavior = LinkBehavior.HoverUnderline;
        reset.Location = new Point(22, 594);
        reset.LinkClicked += delegate { l1.Box.Text = ""; l2.Box.Text = ""; g1.Box.Text = ""; g2.Box.Text = ""; };
        Controls.Add(reset);
        GradientButton save = new GradientButton();
        save.Text = "Save";
        save.SetBounds(W - 20 - 110, 584, 110, 36);
        save.Click += delegate { Save(); };
        Controls.Add(save);
        LinkLabel cancel = new LinkLabel();
        cancel.Text = "Cancel";
        cancel.AutoSize = true;
        cancel.LinkColor = Theme.Muted;
        cancel.ActiveLinkColor = Theme.Text;
        cancel.LinkBehavior = LinkBehavior.HoverUnderline;
        cancel.Location = new Point(W - 20 - 110 - 70, 594);
        cancel.LinkClicked += delegate { Close(); };
        Controls.Add(cancel);

        float k = MainForm.ScreenScale();
        if (Math.Abs(k - 1f) > 0.01f)
        {
            MainForm.ScaleTree(this, k);
            ClientSize = new Size((int)Math.Round(W * k), (int)Math.Round(H * k));
        }
        ResumeLayout(false);
        last = l1.Box;
        UpdatePreview();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.DarkTitleBar(this); }

    void Section(string text, int y)
    {
        Label l = MainForm.MakeLabel(text, 8f, FontStyle.Bold, Theme.Dim);
        l.Location = new Point(22, y);
        Controls.Add(l);
    }

    InputBox Input(string text, string placeholder, int y, bool game)
    {
        InputBox b = new InputBox(text, placeholder, new Font("Segoe UI", 9.5f));
        b.SetBounds(20, y, W - 40, 34);
        b.Box.TextChanged += delegate { UpdatePreview(); };
        b.Box.GotFocus += delegate { last = b.Box; if (gameMode != game) { gameMode = game; UpdatePreview(); } };
        Controls.Add(b);
        return b;
    }

    void Insert(string token)
    {
        if (last == null) return;
        int pos = last.SelectionStart;
        last.Text = last.Text.Remove(pos, last.SelectionLength).Insert(pos, token);
        last.SelectionStart = pos + token.Length;
        last.Focus();
    }

    void UpdatePreview()
    {
        Snapshot s = app.Latest ?? new Snapshot();
        string gameName = null;
        if (gameMode) gameName = s.Game != null ? s.Game.Name : "Your Game";
        string d, st;
        app.Compose(s, gameName, gameMode ? g1.Box.Text : l1.Box.Text, gameMode ? g2.Box.Text : l2.Box.Text, out d, out st);
        preview.Details = d;
        preview.State = st;
        preview.AppName = settings.AppName.Length > 0 ? settings.AppName : Program.Name;
        preview.Picture = null; // RigCord logo
        preview.SmallIcon = gameMode && s.Game != null ? s.Game.Icon : null;
        preview.Elapsed = settings.ShowElapsed ? "12:34" : "";
        previewLabel.Text = gameMode ? "PREVIEW  -  IN A GAME" : "PREVIEW  -  NOT IN A GAME";
        preview.Invalidate();
    }

    void Save()
    {
        settings.Line1 = l1.Box.Text.Trim();
        settings.Line2 = l2.Box.Text.Trim();
        settings.GameLine1 = g1.Box.Text.Trim();
        settings.GameLine2 = g2.Box.Text.Trim();
        app.SettingsChanged();
        Close();
    }
}

// ================================================================ Advanced settings
class AdvancedForm : Form
{
    const int W = 560, H = 632;
    readonly TrayApp app;
    readonly Settings settings;
    readonly InputBox interval, cpuName, gpuName, btnLabel, btnUrl, ignore;
    readonly Label error;
    // true when opened from the preview's button area: start in the button text field
    public bool FocusButtonField;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (FocusButtonField)
        {
            btnLabel.Box.Focus();
            btnLabel.Box.SelectionStart = btnLabel.Box.Text.Length;
            btnLabel.Box.SelectionLength = 0;
        }
    }

    public AdvancedForm(TrayApp app, Settings settings)
    {
        this.app = app;
        this.settings = settings;
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None;
        Text = "Advanced settings";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(W, H);
        Icon = app.AppIcon;

        Label title = MainForm.MakeLabel("Advanced settings", 14f, FontStyle.Bold, Theme.Text);
        title.Location = new Point(20, 14);
        Controls.Add(title);
        Label sub = MainForm.MakeLabel("Fine-tune RigCord. Leave a field empty to use the default.", 9f, FontStyle.Regular, Theme.Muted);
        sub.Location = new Point(22, 46);
        Controls.Add(sub);

        // ---- updates
        Section("DISCORD UPDATES", 80);
        Label il = MainForm.MakeLabel("Seconds between profile updates", 9.5f, FontStyle.Regular, Theme.Text);
        il.Location = new Point(22, 104);
        Controls.Add(il);
        Label ih = MainForm.MakeLabel("4 - 60   (lower = more live, Discord limits very fast updates)", 8f, FontStyle.Regular, Theme.Muted);
        ih.Location = new Point(23, 124);
        Controls.Add(ih);
        interval = Input(settings.Interval.ToString(), "5", W - 20 - 90, 100, 90);

        // ---- hardware names
        Section("HARDWARE NAMES", 160);
        cpuName = Input(settings.CpuName, "CPU name   (detected: " + Stats.CpuName + ")", 20, 180, W - 40);
        gpuName = Input(settings.GpuName, "GPU name   (detected: " + Stats.GpuName + ")", 20, 220, W - 40);

        // ---- links
        Section("YOUR BUTTON", 268);
        Label lk = MainForm.MakeLabel("Add a button of your own under your activity (e.g. your YouTube or Twitch).", 9f, FontStyle.Regular, Theme.Muted);
        lk.Location = new Point(22, 296);
        Controls.Add(lk);
        btnLabel = Input(settings.ButtonLabel, "Extra button text", 20, 328, 170);
        btnUrl = Input(settings.ButtonUrl, "Extra button link   https://...", 198, 328, W - 20 - 198);
        Label bh = MainForm.MakeLabel("⚠  You won't see your own button, but everyone else will (Discord's rule).", 9f, FontStyle.Bold, Theme.Yellow);
        bh.Location = new Point(22, 368);
        Controls.Add(bh);

        // ---- game detection
        Section("GAME DETECTION", 396);
        ignore = Input(settings.IgnoreApps, "Never treat these as games, e.g.  blender, vlc, photoshop", 20, 416, W - 40);
        Snapshot s = app.Latest;
        if (s != null && s.Game != null && !string.IsNullOrEmpty(s.Game.Path))
        {
            string exe = Path.GetFileNameWithoutExtension(s.Game.Path);
            Chip c = new Chip("+ ignore " + s.Game.Name);
            c.Font = new Font("Segoe UI", 8.5f);
            int w = TextRenderer.MeasureText(c.Text, c.Font).Width + 24;
            c.SetBounds(20, 456, w, 26);
            c.Click += delegate
            {
                string cur = ignore.Box.Text.Trim();
                if (cur.IndexOf(exe, StringComparison.OrdinalIgnoreCase) < 0)
                    ignore.Box.Text = cur.Length == 0 ? exe : cur.TrimEnd(',', ' ') + ", " + exe;
            };
            Controls.Add(c);
        }
        else
        {
            Label gh = MainForm.MakeLabel("Tip: while a game runs, a quick \"ignore\" button shows up here.", 8f, FontStyle.Regular, Theme.Dim);
            gh.Location = new Point(23, 460);
            Controls.Add(gh);
        }

        // ---- files
        Section("SETTINGS FILE", 498);
        string mode = Settings.FilePath.StartsWith(Path.GetDirectoryName(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)
            ? "Portable mode (next to RigCord.exe)" : "Saved in your user folder";
        Label fm = MainForm.MakeLabel(mode, 9f, FontStyle.Regular, Theme.Muted);
        fm.Location = new Point(22, 518);
        Controls.Add(fm);
        LinkLabel open = new LinkLabel();
        open.Text = "Open settings.ini";
        open.AutoSize = true;
        open.LinkColor = Theme.Link;
        open.ActiveLinkColor = Theme.Text;
        open.LinkBehavior = LinkBehavior.HoverUnderline;
        open.Location = new Point(W - 20 - 110, 518);
        open.LinkClicked += delegate { app.OpenSettingsFile(); };
        Controls.Add(open);

        // ---- buttons
        error = MainForm.MakeLabel("", 8.5f, FontStyle.Regular, Theme.Red);
        error.Location = new Point(22, 554);
        Controls.Add(error);
        LinkLabel reset = new LinkLabel();
        reset.Text = "Reset to default";
        reset.AutoSize = true;
        reset.LinkColor = Theme.Muted;
        reset.ActiveLinkColor = Theme.Text;
        reset.LinkBehavior = LinkBehavior.HoverUnderline;
        reset.Location = new Point(22, 590);
        reset.LinkClicked += delegate
        {
            interval.Box.Text = "5";
            foreach (InputBox b in new InputBox[] { cpuName, gpuName, btnLabel, btnUrl, ignore }) b.Box.Text = "";
        };
        Controls.Add(reset);
        LinkLabel cancel = new LinkLabel();
        cancel.Text = "Cancel";
        cancel.AutoSize = true;
        cancel.LinkColor = Theme.Muted;
        cancel.ActiveLinkColor = Theme.Text;
        cancel.LinkBehavior = LinkBehavior.HoverUnderline;
        cancel.Location = new Point(W - 20 - 110 - 70, 590);
        cancel.LinkClicked += delegate { Close(); };
        Controls.Add(cancel);
        GradientButton save = new GradientButton();
        save.Text = "Save";
        save.SetBounds(W - 20 - 110, 580, 110, 36);
        save.Click += delegate { Save(); };
        Controls.Add(save);

        float k = MainForm.ScreenScale();
        if (Math.Abs(k - 1f) > 0.01f)
        {
            MainForm.ScaleTree(this, k);
            ClientSize = new Size((int)Math.Round(W * k), (int)Math.Round(H * k));
        }
        ResumeLayout(false);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); MainForm.DarkTitleBar(this); }

    void Section(string text, int y)
    {
        Label l = MainForm.MakeLabel(text, 8f, FontStyle.Bold, Theme.Dim);
        l.Location = new Point(22, y);
        Controls.Add(l);
    }

    InputBox Input(string text, string placeholder, int x, int y, int w)
    {
        InputBox b = new InputBox(text, placeholder, new Font("Segoe UI", 9.5f));
        b.SetBounds(x, y, w, 34);
        b.Box.TextChanged += delegate { error.Text = ""; };
        Controls.Add(b);
        return b;
    }

    void Save()
    {
        int iv;
        string ivText = interval.Box.Text.Trim();
        if (ivText.Length == 0) iv = 5;
        else if (!int.TryParse(ivText, out iv) || iv < 4 || iv > 60) { Fail("Update interval must be a number from 4 to 60.", interval); return; }

        string bl = btnLabel.Box.Text.Trim(), bu = btnUrl.Box.Text.Trim();
        if (bl.Length > 0 && bu.Length == 0) { Fail("Add a link for your extra button (or clear its text).", btnUrl); return; }
        if (bu.Length > 0 && bl.Length == 0) { Fail("Give your extra button some text.", btnLabel); return; }
        if (bu.Length > 0 && !bu.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { Fail("The button link must start with https://", btnUrl); return; }
        if (bl.Length > 32) { Fail("Button text can be at most 32 characters (Discord's limit).", btnLabel); return; }

        settings.Interval = iv;
        settings.CpuName = cpuName.Box.Text.Trim();
        settings.GpuName = gpuName.Box.Text.Trim();
        settings.ButtonLabel = bl;
        settings.ButtonUrl = bu;
        settings.IgnoreApps = ignore.Box.Text.Trim();
        Stats.DetectHardware(settings); // apply name overrides
        app.SettingsChanged();
        Close();
    }

    void Fail(string msg, InputBox field)
    {
        error.Text = msg;
        field.Box.Focus();
        field.Box.SelectAll();
    }
}

// ================================================================ Tray app (controller)
class TrayApp : ApplicationContext
{
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "RigCord";
    const string Sep = " \u2022 ";

    public readonly Icon AppIcon;
    public volatile Snapshot Latest;
    public volatile Image AppImage, CustomImage;
    public volatile string AppIconUrl = "";
    public readonly DateTime StartTime = DateTime.UtcNow;
    public bool IsConnected { get { return discord.Connected; } }
    public DateTime TimerStartUtc
    {
        get
        {
            Snapshot s = Latest;
            return (s != null && s.Game != null && settings.ShowGame) ? s.Game.StartUtc : StartTime;
        }
    }

    readonly NotifyIcon tray;
    readonly ToolStripMenuItem statusItem, namesItem, gameItem, pauseItem;
    readonly Settings settings;
    readonly DiscordIpc discord = new DiscordIpc();
    readonly GameDetector games = new GameDetector();
    readonly AutoResetEvent wake = new AutoResetEvent(false);
    readonly EventWaitHandle showSignal;
    readonly SynchronizationContext ui;
    volatile bool running = true, forceSend = false, reconnectNow = false;
    MainForm window;

    public TrayApp()
    {
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        settings = Settings.Load();
        Stats.DetectHardware(settings);
        MigrateStartup();
        AppIcon = Theme.MakeIcon(64);

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new DarkMenuRenderer();
        menu.Font = new Font("Segoe UI", 9.5f);
        ToolStripMenuItem openItem = new ToolStripMenuItem("Open RigCord");
        openItem.Font = new Font(menu.Font, FontStyle.Bold);
        openItem.Click += delegate { ShowWindow(); };
        statusItem = new ToolStripMenuItem("Starting...");
        statusItem.Enabled = false;
        gameItem = MakeCheck("Show current game", settings.ShowGame, delegate(bool v) { settings.ShowGame = v; });
        namesItem = MakeCheck("Show CPU && GPU names", settings.ShowNames, delegate(bool v) { settings.ShowNames = v; });
        pauseItem = MakeCheck("Pause presence", settings.Paused, delegate(bool v) { settings.Paused = v; });
        ToolStripMenuItem exitItem = new ToolStripMenuItem("Quit");
        exitItem.Click += delegate { Exit(); };
        menu.Items.Add(openItem);
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(gameItem);
        menu.Items.Add(namesItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        tray = new NotifyIcon();
        tray.Icon = Theme.MakeIcon(SystemInformation.SmallIconSize.Width);
        tray.Text = Program.Name;
        tray.ContextMenuStrip = menu;
        tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowWindow(); };
        tray.BalloonTipClicked += delegate { OpenUpdate(); };
        tray.Visible = true;

        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "RigCord_Show");
        Thread sig = new Thread(delegate()
        {
            while (running) { if (showSignal.WaitOne(1000) && running) ui.Post(delegate { ShowWindow(); }, null); }
        });
        sig.IsBackground = true;
        sig.Start();

        if (settings.ClientId.Length > 0) FetchAppInfo();

        Thread worker = new Thread(WorkLoop);
        worker.IsBackground = true;
        worker.Start();

        string[] args = Environment.GetCommandLineArgs();
        bool fromStartup = args.Length > 1 && args[1] == "--tray";
        if (!fromStartup) ShowWindow();
    }

    ToolStripMenuItem MakeCheck(string text, bool value, Action<bool> set)
    {
        ToolStripMenuItem m = new ToolStripMenuItem(text);
        m.CheckOnClick = true;
        m.Checked = value;
        m.Click += delegate { set(m.Checked); SettingsChanged(); };
        return m;
    }

    // ---------- worker
    void WorkLoop()
    {
        Stats stats = new Stats();
        string lastSent = null;
        bool clearedForPause = false;
        DateTime lastSendTime = DateTime.MinValue, lastConnectTry = DateTime.MinValue;
        DateTime nextUpdateCheck = DateTime.UtcNow.AddSeconds(20);
        GameInfo resolvedGame = null;
        int loops = 0;
        Thread.Sleep(1000);

        while (running)
        {
            stats.SampleGpu();
            try { games.Update(stats.Pid3D, settings.IgnoreApps); } catch { }
            Snapshot s = new Snapshot();
            s.Cpu = stats.Cpu();
            s.Gpu = stats.Gpu;
            s.RamUsed = stats.RamUsedGb();
            s.RamTotal = Stats.RamTotalGb;
            s.VramUsed = stats.VramUsedGb;
            s.VramTotal = Stats.VramTotalGb;
            s.GpuTemp = Nvml.Temperature();
            s.Game = games.Current;
            if (s.Game != null && s.Game != resolvedGame)
            {
                // look up the official name + icon in Discord's game list (in the background)
                resolvedGame = s.Game;
                GameDb.Resolve(s.Game, delegate { forceSend = true; wake.Set(); });
            }
            BuildLines(s);
            Latest = s;

            if (DateTime.UtcNow >= nextUpdateCheck)
            {
                nextUpdateCheck = DateTime.UtcNow.AddHours(12);
                CheckForUpdates();
            }

            if (settings.ClientId.Length > 0)
            {
                if (reconnectNow) { reconnectNow = false; discord.Disconnect(); lastConnectTry = DateTime.MinValue; }
                if (!discord.Connected && (DateTime.UtcNow - lastConnectTry).TotalSeconds >= 15)
                {
                    lastConnectTry = DateTime.UtcNow;
                    if (discord.Connect(settings.ClientId)) { lastSent = null; clearedForPause = false; }
                }
                if (discord.Connected)
                {
                    if (settings.Paused)
                    {
                        if (!clearedForPause && discord.SetActivity(null)) { clearedForPause = true; lastSent = null; }
                    }
                    else
                    {
                        clearedForPause = false;
                        string activity = BuildActivity(s);
                        double since = (DateTime.UtcNow - lastSendTime).TotalSeconds;
                        // Discord allows ~5 updates / 20 s, so never send faster than the interval
                        if ((activity != lastSent && since >= settings.Interval - 0.5) || since >= 60 || (forceSend && since >= 2))
                        {
                            forceSend = false;
                            if (discord.SetActivity(activity)) { lastSent = activity; lastSendTime = DateTime.UtcNow; }
                        }
                    }
                }
            }

            UpdateTrayStatus();
            if (++loops % 15 == 1 && window == null) TrimMemory();

            int wait = window != null ? 1500 : settings.Interval * 1000;
            wake.WaitOne(wait);
        }
        stats.Dispose();
    }

    void BuildLines(Snapshot s)
    {
        GameInfo game = settings.ShowGame ? s.Game : null;
        string t1 = game != null ? settings.GameLine1 : settings.Line1;
        string t2 = game != null ? settings.GameLine2 : settings.Line2;
        string d, st;
        Compose(s, game == null ? null : game.Name, t1, t2, out d, out st);
        s.Details = d; s.State = st;
    }

    // Builds the two profile lines. Empty template = default layout (which follows the toggles).
    public void Compose(Snapshot s, string gameName, string t1, string t2, out string details, out string state)
    {
        string cpuPct = Math.Round(s.Cpu) + "%";
        string gpuPct = s.Gpu < 0 ? "N/A" : Math.Round(s.Gpu) + "%";
        string temp = s.GpuTemp >= 0 ? s.GpuTemp + "\u00B0C" : "";
        string ramVal = s.RamUsed.ToString("0.0", CultureInfo.InvariantCulture) + "/" + Math.Round(s.RamTotal) + " GB";
        string vramVal = (s.VramUsed >= 0 && s.VramTotal > 0) ? s.VramUsed.ToString("0.0", CultureInfo.InvariantCulture) + "/" + Math.Round(s.VramTotal) + " GB" : "";

        string cpu = settings.ShowNames ? Stats.CpuName + " " + cpuPct : "CPU " + cpuPct;
        string gpu = settings.ShowNames ? Stats.GpuName + " " + gpuPct : "GPU " + gpuPct;
        if (settings.ShowTemp && temp.Length > 0) gpu += " " + temp;
        string ram = "RAM " + ramVal;
        if (settings.ShowVram && vramVal.Length > 0) ram += Sep + "VRAM " + vramVal;

        string def1, def2;
        if (gameName != null) { def1 = "In " + gameName; def2 = cpu + Sep + gpu + Sep + ram; }
        else { def1 = cpu + Sep + gpu; def2 = ram; }

        Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        v["game"] = gameName ?? "";
        v["cpu"] = cpuPct; v["gpu"] = gpuPct; v["temp"] = temp;
        v["ram"] = ramVal; v["ram%"] = (s.RamTotal > 0 ? Math.Round(s.RamUsed * 100 / s.RamTotal) : 0) + "%";
        v["vram"] = vramVal; v["cpuname"] = Stats.CpuName; v["gpuname"] = Stats.GpuName;

        details = Trunc(Fill(t1, v, def1), 128);
        state = Trunc(Fill(t2, v, def2), 128);
    }

    static string Fill(string template, Dictionary<string, string> v, string fallback)
    {
        if (string.IsNullOrEmpty(template) || template.Trim().Length == 0) return fallback;
        string r = Regex.Replace(template, @"\{([a-zA-Z]+%?)\}", delegate(Match m)
        {
            string val;
            return v.TryGetValue(m.Groups[1].Value, out val) ? val : m.Value;
        });
        // tidy up separators left behind by empty values (e.g. no temperature)
        r = Regex.Replace(r, @"\s{2,}", " ");
        r = Regex.Replace(r, @"([\u2022|\u00B7/-])(\s*[\u2022|\u00B7/-])+", "$1");
        r = r.Trim(' ', '\u2022', '|', '\u00B7', '-', '/');
        return r.Length >= 2 ? r : fallback; // Discord needs at least 2 characters
    }

    string BuildActivity(Snapshot s)
    {
        StringBuilder a = new StringBuilder("{");
        a.Append("\"details\":").Append(Json.Str(s.Details));
        a.Append(",\"state\":").Append(Json.Str(s.State));
        if (settings.ShowElapsed)
        {
            DateTime start = (settings.ShowGame && s.Game != null) ? s.Game.StartUtc : StartTime;
            a.Append(",\"timestamps\":{\"start\":").Append((long)(start - new DateTime(1970, 1, 1)).TotalSeconds).Append("}");
        }

        string link = Program.GitHubUrl;
        string image = Program.LogoUrl;
        GameInfo game = settings.ShowGame ? s.Game : null;
        bool small = game != null && game.IconUrl.Length > 0;
        if (image.Length > 0 || small)
        {
            List<string> parts = new List<string>();
            if (image.Length > 0)
            {
                string hover = link.Length > 0 ? "RigCord \u2022 Get It Here" : "RigCord";
                parts.Add("\"large_image\":" + Json.Str(image));
                parts.Add("\"large_text\":" + Json.Str(hover));
                if (link.Length > 0) parts.Add("\"large_url\":" + Json.Str(link)); // clickable picture (newer Discord)
            }
            if (small) // game icon in the corner, game name on hover
            {
                parts.Add("\"small_image\":" + Json.Str(game.IconUrl));
                parts.Add("\"small_text\":" + Json.Str(Trunc(game.Name, 128)));
            }
            a.Append(",\"assets\":{").Append(string.Join(",", parts.ToArray())).Append("}");
        }

        List<string> buttons = new List<string>();
        if (settings.ButtonLabel.Length > 0 && settings.ButtonUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            buttons.Add("{\"label\":" + Json.Str(Trunc(settings.ButtonLabel, 32)) + ",\"url\":" + Json.Str(settings.ButtonUrl) + "}");
        if (buttons.Count > 0) a.Append(",\"buttons\":[").Append(string.Join(",", buttons.ToArray())).Append("]");

        a.Append(",\"instance\":false}");
        return a.ToString();
    }

    static string Trunc(string s, int n) { return s.Length <= n ? s : s.Substring(0, n - 3) + "..."; }

    // ---------- status
    public void GetStatus(out string text, out Color color)
    {
        if (settings.ClientId.Length == 0) { text = "Setup needed"; color = Theme.Yellow; return; }
        if (discord.Connected)
        {
            if (settings.Paused) { text = "Paused"; color = Theme.Yellow; return; }
            string u = discord.Username;
            text = "Live" + (u.Length > 0 ? " as " + u : "");
            color = Theme.Green;
            return;
        }
        string err = discord.Error;
        if (err.Length > 0 && err.IndexOf("client id", StringComparison.OrdinalIgnoreCase) >= 0) { text = "Invalid Application ID"; color = Theme.Red; return; }
        text = err.IndexOf("not running", StringComparison.OrdinalIgnoreCase) >= 0 ? "Discord is not running" : "Waiting for Discord";
        color = Theme.Muted;
    }

    void UpdateTrayStatus()
    {
        ui.Post(delegate
        {
            string t; Color c;
            GetStatus(out t, out c);
            statusItem.Text = t;
            Snapshot s = Latest;
            string tip = Program.Name + " - " + t + (s != null && s.Game != null ? "\nIn " + s.Game.Name : "");
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }, null);
    }

    // ---------- actions
    public void SettingsChanged()
    {
        settings.Save();
        namesItem.Checked = settings.ShowNames;
        gameItem.Checked = settings.ShowGame;
        pauseItem.Checked = settings.Paused;
        if (window != null) window.SyncToggles();
        forceSend = true;
        wake.Set();
    }

    public void SetClientId(string id)
    {
        bool changed = id != settings.ClientId;
        settings.ClientId = id;
        if (changed) { settings.AppName = ""; AppImage = null; AppIconUrl = ""; }
        settings.Save();
        FetchAppInfo();
        reconnectNow = true;
        forceSend = true;
        wake.Set();
    }

    public void SetImage(string url)
    {
        if (url != settings.LargeImage)
        {
            settings.LargeImage = url;
            settings.Save();
            CustomImage = null;
            forceSend = true;
            wake.Set();
        }
        FetchCustomImage();
    }

    void FetchCustomImage()
    {
        string url = settings.LargeImage;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) { CustomImage = null; return; }
        ThreadPool.QueueUserWorkItem(delegate
        {
            Image img = null;
            try { img = Image.FromStream(new MemoryStream(Web.Get(url))); } catch { }
            ui.Post(delegate
            {
                if (url != settings.LargeImage) return;
                CustomImage = img;
                if (window != null) window.RefreshView();
            }, null);
        });
    }

    // name + icon of your Discord application (preview, and default profile picture)
    void FetchAppInfo()
    {
        string id = settings.ClientId;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string name = null, iconUrl = null; Image img = null;
            try
            {
                string json = Encoding.UTF8.GetString(Web.Get("https://discord.com/api/v10/applications/" + id + "/rpc"));
                name = Json.Field(json, "name");
                string icon = Json.Field(json, "icon");
                if (!string.IsNullOrEmpty(icon))
                {
                    iconUrl = "https://cdn.discordapp.com/app-icons/" + id + "/" + icon + ".png?size=512";
                    img = Image.FromStream(new MemoryStream(Web.Get(iconUrl)));
                }
            }
            catch { }
            ui.Post(delegate
            {
                if (id != settings.ClientId) return;
                if (!string.IsNullOrEmpty(name) && name != settings.AppName) { settings.AppName = name; settings.Save(); }
                if (iconUrl != null && iconUrl != AppIconUrl) { AppIconUrl = iconUrl; forceSend = true; wake.Set(); }
                if (img != null) AppImage = img;
                if (window != null) window.RefreshView();
            }, null);
        });
    }

    // ---------- update check (GitHub Releases)
    public volatile string UpdateVersion = "", UpdateUrl = "";

    void CheckForUpdates()
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string json = Encoding.UTF8.GetString(Web.Get(Program.LatestReleaseApi));
                string tag = Json.Field(json, "tag_name");
                string url = Json.Field(json, "html_url");
                if (string.IsNullOrEmpty(tag) || !IsNewer(tag, Program.Version)) return;
                UpdateVersion = tag;
                UpdateUrl = string.IsNullOrEmpty(url) ? Program.GitHubUrl + "/releases/latest" : url;
                ui.Post(delegate
                {
                    if (settings.NotifiedVersion != tag)
                    {
                        settings.NotifiedVersion = tag;
                        settings.Save();
                        tray.ShowBalloonTip(8000, "RigCord " + tag + " is available", "Click here to download the new version.", ToolTipIcon.Info);
                    }
                    if (window != null) window.RefreshView();
                }, null);
            }
            catch { } // no internet / no release yet
        });
    }

    static bool IsNewer(string tag, string current)
    {
        Version a, b;
        string t = Regex.Replace(tag, @"[^\d.]", "");
        if (t.IndexOf('.') < 0) t += ".0";
        string c = current.IndexOf('.') < 0 ? current + ".0" : current;
        return Version.TryParse(t, out a) && Version.TryParse(c, out b) && a > b;
    }

    public void OpenUpdate() { try { Process.Start(UpdateUrl.Length > 0 ? UpdateUrl : Program.GitHubUrl + "/releases/latest"); } catch { } }

    public void OpenSettingsFile()
    {
        settings.Save();
        try { Process.Start("notepad.exe", "\"" + Settings.FilePath + "\""); } catch { }
    }

    void ShowWindow()
    {
        if (window == null || window.IsDisposed)
        {
            window = new MainForm(this, settings);
            window.FormClosed += delegate { window = null; TrimMemory(); };
            window.Show();
            wake.Set();
        }
        if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
        window.Activate();
    }

    static void MigrateStartup()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k != null && k.GetValue("PCStatsPresence") != null)
                {
                    k.DeleteValue("PCStatsPresence", false);
                    k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                }
            }
        }
        catch { }
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }
        catch { return false; }
    }

    public static void SetStartup(bool on)
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                else k.DeleteValue(RunName, false);
            }
        }
        catch { }
    }

    static void TrimMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
    }

    public void Exit()
    {
        running = false;
        wake.Set();
        try
        {
            if (discord.Connected) { discord.SetActivity(null); Thread.Sleep(150); }
            discord.Disconnect();
        }
        catch { }
        if (window != null) { window.Close(); window = null; }
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }
}
