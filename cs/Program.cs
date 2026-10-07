using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XiaoJuCleaner
{
    internal static class Col
    {
        public const string R = "\u001b[0m";
        public const string B = "\u001b[1m";
        public const string CYAN = "\u001b[36m";
        public const string GREEN = "\u001b[32m";
        public const string YELLOW = "\u001b[33m";
        public const string RED = "\u001b[31m";
        public const string GRAY = "\u001b[90m";
        public const string MAGENTA = "\u001b[35m";
    }

    internal static class Native
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
    }

    internal static class Program
    {
        private const string VERSION = "3.0";
        private const int LINE = 60;

        private static bool Admin;

        private static string Env(string name)
        {
            return Environment.GetEnvironmentVariable(name) ?? "";
        }

        private static string SysRoot
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); }
        }

        private static string Win(params string[] parts)
        {
            string p = SysRoot;
            foreach (string s in parts) p = Path.Combine(p, s);
            return p;
        }

        private static string Local(params string[] parts)
        {
            string p = Env("LOCALAPPDATA");
            foreach (string s in parts) p = Path.Combine(p, s);
            return p;
        }

        private static string Roam(params string[] parts)
        {
            string p = Env("APPDATA");
            foreach (string s in parts) p = Path.Combine(p, s);
            return p;
        }

        // ---------------- 基础工具 ----------------

        private static void SetupConsole()
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
            try
            {
                IntPtr h = Native.GetStdHandle(-11);
                uint mode;
                if (Native.GetConsoleMode(h, out mode))
                    Native.SetConsoleMode(h, mode | 0x0004);
            }
            catch { }
        }

        private static void Clear()
        {
            try { Console.Clear(); } catch { }
        }

        private static string Fmt(long n)
        {
            const double KB = 1024.0, MB = 1024.0 * 1024.0, GB = 1024.0 * 1024.0 * 1024.0;
            if (n >= GB) return (n / GB).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
            if (n >= MB) return (n / MB).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            if (n >= KB) return (n / KB).ToString("0", CultureInfo.InvariantCulture) + " KB";
            return n.ToString(CultureInfo.InvariantCulture) + " B";
        }

        private static long FreeBytes()
        {
            try
            {
                var di = new DriveInfo("C");
                return di.AvailableFreeSpace;
            }
            catch { return 0; }
        }

        private static int DispW(string s)
        {
            int w = 0;
            foreach (char c in s) w += IsWide(c) ? 2 : 1;
            return w;
        }

        private static bool IsWide(char c)
        {
            return (c >= 0x1100 && c <= 0x115F)
                || (c >= 0x2E80 && c <= 0xA4CF)
                || (c >= 0xAC00 && c <= 0xD7A3)
                || (c >= 0xF900 && c <= 0xFAFF)
                || (c >= 0xFE30 && c <= 0xFE6F)
                || (c >= 0xFF00 && c <= 0xFF60)
                || (c >= 0xFFE0 && c <= 0xFFE6);
        }

        private static string Pad(string s, int width)
        {
            int d = width - DispW(s);
            return d > 0 ? s + new string(' ', d) : s;
        }

        // ---------------- 文件操作 ----------------

        private static long PathSize(string path)
        {
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(path);
            while (stack.Count > 0)
            {
                string d = stack.Pop();
                try
                {
                    foreach (FileSystemInfo e in new DirectoryInfo(d).EnumerateFileSystemInfos())
                    {
                        try
                        {
                            FileAttributes a = e.Attributes;
                            if ((a & FileAttributes.ReparsePoint) != 0) continue;
                            if ((a & FileAttributes.Directory) != 0) stack.Push(e.FullName);
                            else total += ((FileInfo)e).Length;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return total;
        }

        private static bool ForceDelete(string path)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch { }
            try
            {
                File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        private static void ForceDeleteTree(string dir)
        {
            try
            {
                var di = new DirectoryInfo(dir);
                foreach (FileSystemInfo e in di.EnumerateFileSystemInfos())
                {
                    try
                    {
                        if ((e.Attributes & FileAttributes.Directory) != 0 && (e.Attributes & FileAttributes.ReparsePoint) == 0)
                            ForceDeleteTree(e.FullName);
                        else
                            ForceDelete(e.FullName);
                    }
                    catch { }
                }
                try { di.Attributes = FileAttributes.Normal; } catch { }
                try { di.Delete(true); } catch { }
            }
            catch
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>清空目录内容但保留目录本身，返回释放字节数</summary>
        private static long RmDirContents(string path)
        {
            long freed = 0;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            string[] names;
            try { names = Directory.GetFileSystemEntries(path); }
            catch { return 0; }
            foreach (string p in names)
            {
                try
                {
                    FileAttributes a = File.GetAttributes(p);
                    if ((a & FileAttributes.Directory) != 0 && (a & FileAttributes.ReparsePoint) == 0)
                    {
                        freed += PathSize(p);
                        ForceDeleteTree(p);
                    }
                    else
                    {
                        long sz = 0;
                        try { sz = new FileInfo(p).Length; } catch { }
                        if (ForceDelete(p)) freed += sz;
                    }
                }
                catch { }
            }
            return freed;
        }

        private static long DelFilesDeep(string root, string pattern, bool deleteDirs = false)
        {
            long freed = 0;
            if (!Directory.Exists(root)) return 0;
            try
            {
                foreach (string f in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                {
                    long sz = 0;
                    try { sz = new FileInfo(f).Length; } catch { }
                    if (ForceDelete(f)) freed += sz;
                }
            }
            catch { }
            if (deleteDirs)
            {
                try
                {
                    foreach (string d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                    {
                        try { Directory.Delete(d, false); } catch { }
                    }
                }
                catch { }
            }
            return freed;
        }

        private static void RunHidden(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return;
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                }
            }
            catch { }
        }

        private static void RunVisible(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                using (var p = Process.Start(psi)) { if (p != null) p.WaitForExit(); }
            }
            catch { }
        }

        // ---------------- 清理项目 ----------------

        private static long CleanUserTemp()
        {
            string p = Env("TEMP");
            if (string.IsNullOrEmpty(p)) p = Path.GetTempPath();
            Console.WriteLine(Col.GRAY + "    清理 " + p + Col.R);
            return RmDirContents(p);
        }

        private static long CleanSysTemp()
        {
            Console.WriteLine(Col.GRAY + "    清理 " + Win("Temp") + Col.R);
            return RmDirContents(Win("Temp"));
        }

        private static long CleanRecycleBin()
        {
            long before = FreeBytes();
            try
            {
                Native.SHEmptyRecycleBin(IntPtr.Zero, null, 7);
            }
            catch
            {
                RunHidden("powershell.exe", "-NoProfile -NonInteractive -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"");
            }
            Thread.Sleep(300);
            return Math.Max(0, FreeBytes() - before);
        }

        private static readonly string[] BrowserRoots =
        {
            @"Google\Chrome\User Data",
            @"Microsoft\Edge\User Data",
            @"BraveSoftware\Brave-Browser\User Data",
            @"360Chrome\Chrome\User Data",
            @"Tencent\QQBrowser\User Data",
            @"Chromium\User Data"
        };

        private static readonly string[] BrowserSubDirs =
        {
            "Cache", "Code Cache", "GPUCache",
            @"Service Worker\CacheStorage", "Media Cache", "ShaderCache"
        };

        private static long CleanBrowserCache()
        {
            long freed = 0;
            foreach (string rel in BrowserRoots)
            {
                string baseDir = Local(rel.Split('\\'));
                if (!Directory.Exists(baseDir)) continue;
                string[] profiles;
                try { profiles = Directory.GetDirectories(baseDir); } catch { continue; }
                foreach (string pp in profiles)
                {
                    foreach (string sd in BrowserSubDirs)
                    {
                        string t = Path.Combine(pp, sd);
                        if (Directory.Exists(t)) freed += RmDirContents(t);
                    }
                }
            }
            string ff = Roam("Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(ff))
            {
                string[] profs;
                try { profs = Directory.GetDirectories(ff); } catch { profs = new string[0]; }
                foreach (string prof in profs)
                {
                    foreach (string cache in new[] { "cache2", "startupCache" })
                    {
                        string t = Path.Combine(prof, cache);
                        if (Directory.Exists(t)) freed += RmDirContents(t);
                    }
                }
            }
            return freed;
        }

        private static long CleanWuCache()
        {
            RunHidden("net.exe", "stop wuauserv");
            RunHidden("net.exe", "stop bits");
            Thread.Sleep(500);
            long freed = RmDirContents(Win("SoftwareDistribution", "Download"));
            RunHidden("net.exe", "start wuauserv");
            RunHidden("net.exe", "start bits");
            return freed;
        }

        private static long CleanThumbs()
        {
            long freed = 0;
            string exp = Local("Microsoft", "Windows", "Explorer");
            RunHidden("taskkill.exe", "/f /im explorer.exe");
            Thread.Sleep(1200);
            if (Directory.Exists(exp))
            {
                string[] names;
                try { names = Directory.GetFiles(exp); } catch { names = new string[0]; }
                foreach (string p in names)
                {
                    string n = Path.GetFileName(p);
                    if (n.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) ||
                        n.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase))
                    {
                        long sz = 0;
                        try { sz = new FileInfo(p).Length; } catch { }
                        if (ForceDelete(p)) freed += sz;
                    }
                }
            }
            string iconDb = Local("IconCache.db");
            if (File.Exists(iconDb))
            {
                long sz = 0;
                try { sz = new FileInfo(iconDb).Length; } catch { }
                if (ForceDelete(iconDb)) freed += sz;
            }
            try
            {
                var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                Process.Start(psi);
            }
            catch { }
            return freed;
        }

        private static long CleanCrash()
        {
            long freed = 0;
            freed += RmDirContents(Local("CrashDumps"));
            freed += DelFilesDeep(Local("Microsoft", "Windows", "WER"), "*");
            freed += DelFilesDeep(Path.Combine(Env("PROGRAMDATA"), "Microsoft", "Windows", "WER"), "*");
            freed += RmDirContents(Win("Minidump"));
            string mem = Win("MEMORY.DMP");
            if (File.Exists(mem))
            {
                long sz = 0;
                try { sz = new FileInfo(mem).Length; } catch { }
                if (ForceDelete(mem)) freed += sz;
            }
            return freed;
        }

        private static long CleanPrefetch()
        {
            return RmDirContents(Win("Prefetch"));
        }

        private static long CleanRecent()
        {
            long freed = 0;
            string rec = Roam("Microsoft", "Windows", "Recent");
            freed += RmDirContents(rec);
            foreach (string sub in new[] { "AutomaticDestinations", "CustomDestinations" })
            {
                string t = Path.Combine(rec, sub);
                if (Directory.Exists(t)) freed += RmDirContents(t);
            }
            return freed;
        }

        private static long CleanLogs()
        {
            long freed = 0;
            freed += RmDirContents(Win("Logs", "CBS"));
            freed += DelFilesDeep(Win("Logs"), "*.log");
            if (Directory.Exists(SysRoot))
            {
                try
                {
                    foreach (string f in Directory.EnumerateFiles(SysRoot, "*.log"))
                    {
                        long sz = 0;
                        try { sz = new FileInfo(f).Length; } catch { }
                        if (ForceDelete(f)) freed += sz;
                    }
                }
                catch { }
            }
            return freed;
        }

        private static long CleanFontCache()
        {
            RunHidden("net.exe", "stop FontCache");
            Thread.Sleep(600);
            long freed = RmDirContents(Win("ServiceProfiles", "LocalService", "AppData", "Local", "FontCache"));
            RunHidden("net.exe", "start FontCache");
            freed += RmDirContents(Local("FontCache"));
            return freed;
        }

        private static long CleanDeliveryOpt()
        {
            RunHidden("powershell.exe", "-NoProfile -NonInteractive -Command \"Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue\"");
            return RmDirContents(Win("ServiceProfiles", "NetworkService", "AppData", "Local",
                                     "Microsoft", "Windows", "DeliveryOptimization"));
        }

        private static long CleanDns()
        {
            RunHidden("ipconfig.exe", "/flushdns");
            RunHidden("ipconfig.exe", "/registerdns");
            return 0;
        }

        private static long CleanDism()
        {
            Console.WriteLine(Col.YELLOW + "    DISM 组件清理中，可能需要几分钟，请耐心等喵..." + Col.R);
            RunVisible("DISM.exe", "/Online /Cleanup-Image /StartComponentCleanup /ResetBase");
            return 0;
        }

        private static long CleanHibernate()
        {
            RunHidden("powercfg.exe", "/h off");
            return 0;
        }

        private static long OpenCleanMgr()
        {
            try
            {
                Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true });
            }
            catch { }
            return 0;
        }

        // ---------------- 磁盘分析 ----------------

        private static object _lock = new object();

        private static void AnalyzeDisk()
        {
            const string root = "C:\\";
            Console.WriteLine();
            Console.WriteLine(Col.CYAN + "    正在扫描 C 盘各目录占用（C# 引擎，多线程加速）..." + Col.R);
            var dirs = new List<string>();
            try
            {
                foreach (string d in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        FileAttributes a = File.GetAttributes(d);
                        if ((a & FileAttributes.ReparsePoint) != 0) continue;
                        dirs.Add(d);
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(Col.RED + "    无法读取 C 盘根目录: " + e.Message + Col.R);
                return;
            }

            var sizes = new long[dirs.Count];
            var times = new double[dirs.Count];
            int workers = Math.Min(4, Math.Max(1, Environment.ProcessorCount));
            Parallel.For(0, dirs.Count, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
            {
                var sw = Stopwatch.StartNew();
                long sz = PathSize(dirs[i]);
                sw.Stop();
                sizes[i] = sz;
                times[i] = sw.Elapsed.TotalSeconds;
                lock (_lock)
                {
                    Console.WriteLine("    " + Pad(Path.GetFileName(dirs[i]), 28) + " " +
                                      Fmt(sz).PadLeft(10) + "   " + Col.GRAY +
                                      "(" + times[i].ToString("0.0", CultureInfo.InvariantCulture) + "s)" + Col.R);
                }
            });

            var order = new int[dirs.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => sizes[b].CompareTo(sizes[a]));

            long total = 0;
            foreach (long s in sizes) total += s;

            Console.WriteLine();
            Console.WriteLine(Col.B + "    ==== C 盘目录占用 Top 15 ====" + Col.R);
            Console.WriteLine("    " + Pad("目录", 30) + "占用");
            Console.WriteLine("    " + new string('-', 44));
            long top = dirs.Count > 0 ? Math.Max(1, sizes[order[0]]) : 1;
            for (int i = 0; i < Math.Min(15, order.Length); i++)
            {
                int idx = order[i];
                int bars = (int)Math.Min(28, sizes[idx] * 28 / top);
                Console.WriteLine("    " + Pad(Path.GetFileName(dirs[idx]).Substring(0, Math.Min(28, Path.GetFileName(dirs[idx]).Length)), 28) +
                                  Fmt(sizes[idx]).PadLeft(10) + "  " + Col.GREEN +
                                  new string('\u2588', bars) + Col.R);
            }
            Console.WriteLine();
            Console.WriteLine("    已扫描总量: " + Col.B + Fmt(total) + Col.R);
        }

        // ---------------- 菜单 ----------------

        private struct Item
        {
            public string Key;
            public string Name;
            public Func<long> Fn;
            public bool NeedAdmin;
            public Item(string k, string n, Func<long> f, bool a) { Key = k; Name = n; Fn = f; NeedAdmin = a; }
        }

        private static readonly Item[] Items =
        {
            new Item("1",  "用户临时文件",     CleanUserTemp,   false),
            new Item("2",  "系统临时文件",     CleanSysTemp,    true),
            new Item("3",  "回收站",           CleanRecycleBin, false),
            new Item("4",  "浏览器缓存",       CleanBrowserCache, false),
            new Item("5",  "Windows 更新缓存", CleanWuCache,    true),
            new Item("6",  "缩略图/图标缓存",  CleanThumbs,     false),
            new Item("7",  "崩溃转储与错误报告", CleanCrash,    false),
            new Item("8",  "预读取 Prefetch",  CleanPrefetch,   true),
            new Item("9",  "最近使用记录",     CleanRecent,     false),
            new Item("10", "系统日志文件",     CleanLogs,       false),
            new Item("11", "字体缓存",         CleanFontCache,  false),
            new Item("12", "传递优化缓存",     CleanDeliveryOpt, true),
            new Item("13", "DNS 缓存刷新",     CleanDns,        false),
            new Item("14", "DISM 组件清理",    CleanDism,       true),
            new Item("15", "关闭休眠文件",     CleanHibernate,  true),
            new Item("16", "磁盘清理工具",     OpenCleanMgr,    true)
        };

        private static readonly string[] OneClickOrder = { "1", "3", "4", "6", "7", "9", "11", "13", "2", "5", "8", "10", "12" };

        private static void ShowSpace()
        {
            Console.WriteLine("    C 盘可用空间: " + Col.GREEN + Fmt(FreeBytes()) + Col.R);
        }

        private static void Banner()
        {
            Console.WriteLine(Col.CYAN + new string('=', LINE) + Col.R);
            Console.WriteLine(Col.B + Col.MAGENTA + "            小  橘  清  理  大  师   v" + VERSION + Col.R);
            Console.WriteLine(Col.GRAY + "                 喵~ 命令行版 CCleaner (C#)" + Col.R);
            Console.WriteLine(Col.CYAN + new string('=', LINE) + Col.R);
            if (Admin)
                Console.WriteLine("    运行权限: " + Col.GREEN + "管理员" + Col.R + "      全部功能可用");
            else
                Console.WriteLine("    运行权限: " + Col.YELLOW + "普通用户" + Col.R + "    带 " +
                                  Col.YELLOW + "*" + Col.R + " 的功能请右键以管理员运行");
        }

        private static void Menu()
        {
            Banner();
            Console.WriteLine(new string('-', LINE));
            ShowSpace();
            Console.WriteLine(new string('-', LINE));
            for (int r = 0; r < Items.Length; r += 2)
            {
                var sb = new StringBuilder("  ");
                for (int c = r; c < Math.Min(r + 2, Items.Length); c++)
                {
                    Item it = Items[c];
                    string star = it.NeedAdmin ? Col.YELLOW + "*" + Col.R : " ";
                    sb.Append(Col.B).Append('[').Append(it.Key.PadLeft(2)).Append(']').Append(Col.R).Append(' ');
                    sb.Append(Pad(it.Name, 20)).Append(star);
                    if (c != r + 1) sb.Append("  ");
                }
                Console.WriteLine(sb.ToString().TrimEnd());
            }
            Console.WriteLine(new string('-', LINE));
            Console.WriteLine("    " + Col.B + "[A]" + Col.R + "  一键智能清理          " +
                              Col.B + "[S]" + Col.R + "  分析 C 盘占用排行");
            Console.WriteLine("    " + Col.B + "[0]" + Col.R + "  退出");
            Console.WriteLine(Col.CYAN + new string('=', LINE) + Col.R);
        }

        private static void DoItem(string key)
        {
            foreach (Item it in Items)
            {
                if (it.Key != key) continue;
                Console.WriteLine();
                if (it.NeedAdmin && !Admin)
                {
                    Console.WriteLine(Col.YELLOW + "    跳过 [" + it.Key + "] " + it.Name + " - 需要管理员权限" + Col.R);
                    return;
                }
                Console.WriteLine(Col.CYAN + "  正在执行 [" + it.Key + "] " + it.Name + " ..." + Col.R);
                long before = FreeBytes();
                var sw = Stopwatch.StartNew();
                try { it.Fn(); }
                catch (Exception e) { Console.WriteLine(Col.RED + "    出错: " + e.Message + Col.R); }
                Thread.Sleep(400);
                sw.Stop();
                long freed = FreeBytes() - before;
                if (freed < 0) freed = 0;
                Console.WriteLine(Col.GREEN + "  [" + it.Key + "] " + it.Name + " 完成，释放约 " + Fmt(freed) +
                                  "  (" + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s)" + Col.R);
                return;
            }
            Console.WriteLine(Col.RED + "    无效选项喵~" + Col.R);
        }

        private static void OneClick()
        {
            Console.WriteLine();
            Console.WriteLine(Col.CYAN + "  一键智能清理开始喵~" + Col.R);
            long before = FreeBytes();
            var sw = Stopwatch.StartNew();
            foreach (string k in OneClickOrder)
            {
                foreach (Item it in Items)
                {
                    if (it.Key != k) continue;
                    if (it.NeedAdmin && !Admin)
                    {
                        Console.WriteLine(Col.GRAY + "    跳过 [" + it.Key + "] " + it.Name + " (需要管理员)" + Col.R);
                        continue;
                    }
                    Console.WriteLine(Col.GRAY + "    > [" + it.Key + "] " + it.Name + Col.R);
                    try { it.Fn(); }
                    catch (Exception e) { Console.WriteLine(Col.RED + "      出错: " + e.Message + Col.R); }
                }
            }
            Thread.Sleep(500);
            sw.Stop();
            long freed = Math.Max(0, FreeBytes() - before);
            Console.WriteLine();
            Console.WriteLine(Col.GREEN + Col.B + "  一键清理完成，共释放约 " + Fmt(freed) +
                              "  (" + sw.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s)" + Col.R);
        }

        // ---------------- 入口 ----------------

        private static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                {
                    var p = new WindowsPrincipal(id);
                    return p.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private static int Main(string[] args)
        {
            SetupConsole();
            Admin = IsAdmin();

            if (args.Length > 0)
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "-v" || arg == "--version")
                {
                    Console.WriteLine("小橘清理大师 v" + VERSION + " (C# 版)");
                    return 0;
                }
                if (arg == "--list")
                {
                    foreach (Item it in Items)
                        Console.WriteLine(it.Key + "\t" + it.Name + (it.NeedAdmin ? "  [需管理员]" : ""));
                    return 0;
                }
                if (arg == "--clean")
                {
                    OneClick();
                    Console.WriteLine();
                    Console.Write("    按回车退出...");
                    try { Console.ReadLine(); } catch { }
                    return 0;
                }
                if (arg == "--dryrun")
                {
                    Console.WriteLine("模拟模式：仅显示将要执行的项目");
                    foreach (string k in OneClickOrder)
                        foreach (Item it in Items)
                            if (it.Key == k)
                                Console.WriteLine("  [" + it.Key + "] " + it.Name + (it.NeedAdmin ? "  [需管理员]" : ""));
                    return 0;
                }
                if (arg == "--scan")
                {
                    AnalyzeDisk();
                    return 0;
                }
            }

            while (true)
            {
                Clear();
                Menu();
                Console.Write("    请输入选项后回车: ");
                string ch;
                try { ch = Console.ReadLine(); }
                catch { ch = null; }
                if (ch == null) break;
                ch = ch.Trim().TrimStart('\uFEFF', '\u200B', '\u00A0').Trim();
                if (ch.Length == 0) continue;
                if (ch == "0") break;
                Clear();
                if (ch.Equals("a", StringComparison.OrdinalIgnoreCase))
                {
                    OneClick();
                }
                else if (ch.Equals("s", StringComparison.OrdinalIgnoreCase))
                {
                    AnalyzeDisk();
                }
                else
                {
                    Banner();
                    Console.WriteLine(new string('-', LINE));
                    DoItem(ch);
                }
                Console.WriteLine();
                Console.Write(Col.GRAY + "    按回车返回主菜单..." + Col.R);
                try { Console.ReadLine(); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine(Col.MAGENTA + "    清理完成喵~ 尾巴摇摇，下次见！" + Col.R);
            return 0;
        }
    }
}
