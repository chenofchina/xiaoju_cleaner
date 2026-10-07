# -*- coding: utf-8 -*-
"""小橘清理大师 v2.0 - 命令行版 CCleaner"""
import os
import sys
import ctypes
import shutil
import subprocess
import time

VERSION = "2.0"
NOWIN = 0x08000000  # CREATE_NO_WINDOW

# ---------- 颜色 ----------
class C:
    R = "\033[0m"; B = "\033[1m"
    CYAN = "\033[36m"; GREEN = "\033[32m"; YELLOW = "\033[33m"
    RED = "\033[31m"; GRAY = "\033[90m"; MAGENTA = "\033[35m"


def enable_ansi():
    try:
        k = ctypes.windll.kernel32
        k.SetConsoleMode(k.GetStdHandle(-11), 7)
    except Exception:
        pass


def is_admin():
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


ADMIN = is_admin()


# ---------- 工具函数 ----------
def fmt(n):
    if n >= 1024 ** 3:
        return "%.2f GB" % (n / 1024 ** 3)
    if n >= 1024 ** 2:
        return "%.1f MB" % (n / 1024 ** 2)
    if n >= 1024:
        return "%.0f KB" % (n / 1024)
    return "%d B" % n


def free_bytes():
    try:
        return shutil.disk_usage("C:\\").free
    except Exception:
        return 0


def on_rm_error(func, path, exc):
    try:
        os.chmod(path, 0o700)
        func(path)
    except Exception:
        pass


def path_size(path, skip_links=True):
    total = 0
    stack = [path]
    while stack:
        d = stack.pop()
        try:
            with os.scandir(d) as it:
                for e in it:
                    try:
                        if e.is_dir(follow_symlinks=False):
                            if skip_links and e.is_symlink():
                                continue
                            stack.append(e.path)
                        else:
                            total += e.stat(follow_symlinks=False).st_size
                    except Exception:
                        pass
        except Exception:
            pass
    return total


def rm_dir_contents(path, quiet=True):
    """清空目录内容，但保留目录本身；返回释放字节数"""
    freed = 0
    if not path or not os.path.isdir(path):
        return 0
    try:
        names = os.listdir(path)
    except Exception:
        return 0
    for name in names:
        p = os.path.join(path, name)
        try:
            if os.path.isdir(p) and not os.path.islink(p):
                freed += path_size(p)
                shutil.rmtree(p, onerror=on_rm_error)
            else:
                try:
                    sz = os.path.getsize(p)
                except Exception:
                    sz = 0
                os.remove(p)
                freed += sz
        except Exception:
            pass
    return freed


def del_glob(pattern, recursive=False):
    import glob as _g
    freed = 0
    for p in _g.glob(pattern, recursive=recursive):
        try:
            if os.path.isdir(p):
                freed += path_size(p)
                shutil.rmtree(p, onerror=on_rm_error)
            else:
                freed += os.path.getsize(p)
                os.remove(p)
        except Exception:
            pass
    return freed


def run(cmd, shell=False, timeout=None):
    try:
        return subprocess.run(cmd, shell=shell, timeout=timeout,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              creationflags=NOWIN)
    except Exception:
        return None


def ps(command, timeout=300):
    return run(["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
               timeout=timeout)


def appdata(*parts):
    return os.path.join(os.environ.get("LOCALAPPDATA", ""), *parts)


def win(*parts):
    return os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), *parts)


# ---------- 各个清理项目 ----------
def clean_user_temp():
    p = os.environ.get("TEMP") or appdata("Temp")
    print(C.GRAY + "    清理 " + p + C.R)
    return rm_dir_contents(p)


def clean_sys_temp():
    print(C.GRAY + "    清理 " + win("Temp") + C.R)
    return rm_dir_contents(win("Temp"))


def clean_recycle_bin():
    before = free_bytes()
    try:
        ctypes.windll.shell32.SHEmptyRecycleBinW(None, None, 7)
    except Exception:
        ps("Clear-RecycleBin -Force -ErrorAction SilentlyContinue", 120)
    time.sleep(0.3)
    return max(0, free_bytes() - before)


BROWSERS = [
    (r"Google\Chrome\User Data", True),
    (r"Microsoft\Edge\User Data", True),
    (r"BraveSoftware\Brave-Browser\User Data", True),
    (r"360Chrome\Chrome\User Data", True),
    (r"Tencent\QQBrowser\User Data", True),
    (r"Chromium\User Data", True),
]


def clean_browser_cache():
    freed = 0
    subdirs = ["Cache", "Code Cache", "GPUCache", "Service Worker\\CacheStorage",
               "Media Cache", "ShaderCache"]
    for rel, _ in BROWSERS:
        base = appdata(rel)
        if not os.path.isdir(base):
            continue
        for profile in os.listdir(base):
            pp = os.path.join(base, profile)
            if not os.path.isdir(pp):
                continue
            for sd in subdirs:
                t = os.path.join(pp, sd)
                if os.path.isdir(t):
                    freed += rm_dir_contents(t)
    ff = os.path.join(os.environ.get("APPDATA", ""), r"Mozilla\Firefox\Profiles")
    if os.path.isdir(ff):
        for prof in os.listdir(ff):
            for cache in ("cache2", "startupCache"):
                t = os.path.join(ff, prof, cache)
                if os.path.isdir(t):
                    freed += rm_dir_contents(t)
    return freed


def clean_wu_cache():
    run("net stop wuauserv")
    run("net stop bits")
    time.sleep(0.5)
    freed = rm_dir_contents(win("SoftwareDistribution", "Download"))
    run("net start wuauserv")
    run("net start bits")
    return freed


def clean_thumbs():
    freed = 0
    exp = appdata(r"Microsoft\Windows\Explorer")
    run(["taskkill", "/f", "/im", "explorer.exe"])
    time.sleep(1.2)
    if os.path.isdir(exp):
        for name in os.listdir(exp):
            if name.startswith(("thumbcache_", "iconcache_")):
                try:
                    p = os.path.join(exp, name)
                    freed += os.path.getsize(p)
                    os.remove(p)
                except Exception:
                    pass
    try:
        p = appdata("IconCache.db")
        if os.path.isfile(p):
            freed += os.path.getsize(p)
            os.remove(p)
    except Exception:
        pass
    try:
        subprocess.Popen("explorer.exe", creationflags=NOWIN)
    except Exception:
        pass
    return freed


def clean_crash():
    freed = 0
    freed += rm_dir_contents(appdata("CrashDumps"))
    freed += del_glob(os.path.join(appdata(r"Microsoft\Windows\WER"), "**", "*"),
                      recursive=True)
    freed += del_glob(os.path.join(os.environ.get("PROGRAMDATA", ""),
                                   r"Microsoft\Windows\WER\**\*"), recursive=True)
    freed += rm_dir_contents(win("Minidump"))
    for f in ("MEMORY.DMP",):
        try:
            t = win(f)
            if os.path.isfile(t):
                freed += os.path.getsize(t)
                os.remove(t)
        except Exception:
            pass
    return freed


def clean_prefetch():
    return rm_dir_contents(win("Prefetch"))


def clean_recent():
    freed = 0
    rec = os.path.join(os.environ.get("APPDATA", ""), r"Microsoft\Windows\Recent")
    freed += rm_dir_contents(rec)
    for sub in ("AutomaticDestinations", "CustomDestinations"):
        t = os.path.join(rec, sub)
        if os.path.isdir(t):
            freed += rm_dir_contents(t)
    return freed


def clean_logs():
    freed = 0
    freed += rm_dir_contents(win("Logs", "CBS"))
    freed += del_glob(win("Logs", "**", "*.log"), recursive=True)
    freed += del_glob(win("*.log"))
    return freed


def clean_font_cache():
    run("net stop FontCache")
    time.sleep(0.6)
    freed = rm_dir_contents(win(r"ServiceProfiles\LocalService\AppData\Local\FontCache"))
    run("net start FontCache")
    freed += rm_dir_contents(appdata("FontCache"))
    return freed


def clean_delivery_opt():
    ps("Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue", 180)
    return rm_dir_contents(win(r"ServiceProfiles\NetworkService\AppData\Local"
                               r"\Microsoft\Windows\DeliveryOptimization"))
def clean_dns():
    run(["ipconfig", "/flushdns"])
    run(["ipconfig", "/registerdns"], timeout=60)
    return 0


def clean_dism():
    print(C.YELLOW + "    DISM 组件清理中，可能需要几分钟，请耐心等喵..." + C.R)
    try:
        subprocess.run(["DISM", "/Online", "/Cleanup-Image", "/StartComponentCleanup",
                        "/ResetBase"], creationflags=NOWIN)
    except Exception as e:
        print("    出错:", e)
    return 0


def clean_hibernate():
    run(["powercfg", "/h", "off"])
    return 0


def open_cleanmgr():
    try:
        subprocess.Popen("cleanmgr.exe")
    except Exception:
        pass
    return 0


# ---------- 磁盘占用分析 ----------
def analyze_disk():
    root = "C:\\"
    print()
    print(C.CYAN + "    正在扫描 C 盘各目录占用（Python 引擎，比 PowerShell 快很多）..." + C.R)
    items = []
    try:
        entries = [e for e in os.scandir(root)
                   if e.is_dir(follow_symlinks=False) and not e.is_symlink()]
    except Exception as e:
        print(C.RED + "    无法读取 C 盘根目录: %s" % e + C.R)
        return
    total = 0
    for e in entries:
        t0 = time.time()
        size = path_size(e.path)
        total += size
        items.append((e.name, size))
        print("    %-28s %10s   %s(%.1fs)%s" %
              (e.name, fmt(size), C.GRAY, time.time() - t0, C.R))
    items.sort(key=lambda x: -x[1])
    print()
    print(C.B + "    ==== C 盘目录占用 Top 15 ====" + C.R)
    print("    %-30s %s" % ("目录", "占用"))
    print("    " + "-" * 44)
    for name, size in items[:15]:
        bar = "█" * min(28, int(size / (items[0][1] or 1) * 28))
        print("    %-30s %10s  %s%s%s" % (name[:28], fmt(size), C.GREEN, bar, C.R))
    print()
    print("    已扫描总量: %s" % C.B + fmt(total) + C.R)


# ---------- 菜单项定义 ----------
# (键, 名称, 函数, 是否需要管理员, 特殊标记)
ITEMS = [
    ("1", "用户临时文件", clean_user_temp, False),
    ("2", "系统临时文件", clean_sys_temp, True),
    ("3", "回收站", clean_recycle_bin, False),
    ("4", "浏览器缓存", clean_browser_cache, False),
    ("5", "Windows Update 缓存", clean_wu_cache, True),
    ("6", "缩略图 / 图标缓存", clean_thumbs, False),
    ("7", "错误报告 / 崩溃转储", clean_crash, False),
    ("8", "预读取缓存 Prefetch", clean_prefetch, True),
    ("9", "最近使用记录", clean_recent, False),
    ("10", "系统日志文件", clean_logs, False),
    ("11", "字体缓存", clean_font_cache, False),
    ("12", "传递优化缓存 DO", clean_delivery_opt, True),
    ("13", "DNS 缓存刷新", clean_dns, False),
    ("14", "组件存储深度清理 DISM", clean_dism, True),
    ("15", "关闭休眠文件", clean_hibernate, True),
    ("16", "磁盘清理工具 cleanmgr", open_cleanmgr, True),
]
ONE_CLICK = ["1", "3", "4", "6", "7", "9", "11", "13", "2", "5", "8", "10", "12"]


def show_space():
    fb = free_bytes()
    print("    C 盘可用空间: %s%s%s" % (C.GREEN, fmt(fb), C.R))


def disp_w(s):
    import unicodedata
    return sum(2 if unicodedata.east_asian_width(c) in "WF" else 1 for c in s)


def pad(s, width):
    return s + " " * max(0, width - disp_w(s))


def banner():
    print(C.CYAN + "=" * 62 + C.R)
    print(C.B + C.MAGENTA + "                 小  橘  清  理  大  师   v" + VERSION + C.R)
    print(C.GRAY + "                      喵~ 命令行版 CCleaner" + C.R)
    print(C.CYAN + "=" * 62 + C.R)
    if ADMIN:
        print("    运行权限: " + C.GREEN + "管理员" + C.R + "      全部功能可用")
    else:
        print("    运行权限: " + C.YELLOW + "普通用户" + C.R +
              "    带 " + C.YELLOW + "*" + C.R + " 的功能请右键以管理员运行")


def menu():
    banner()
    print("-" * 62)
    show_space()
    print("-" * 62)
    row = [(0, 2), (2, 4), (4, 6), (6, 8), (8, 10), (10, 12), (12, 14), (14, 16)]
    for a, b in row:
        line = "    "
        for key, name, _, adm in ITEMS[a:b]:
            star = C.YELLOW + "*" + C.R if adm else " "
            keytxt = C.B + ("[%2s]" % key) + C.R
            line += "%s %s%s  " % (keytxt, pad(name, 22), star)
        print(line.rstrip())
    print("-" * 62)
    print("    " + C.B + "[A]" + C.R + "  一键智能清理            " +
          C.B + "[S]" + C.R + "  分析 C 盘占用排行")
    print("    " + C.B + "[0]" + C.R + "  退出")
    print(C.CYAN + "=" * 62 + C.R)


def do_item(key):
    for k, name, fn, adm in ITEMS:
        if k == key:
            print()
            if adm and not ADMIN:
                print(C.YELLOW + "    跳过 [%s] %s - 需要管理员权限" % (k, name) + C.R)
                return
            print(C.CYAN + "  正在执行 [%s] %s ..." % (k, name) + C.R)
            before = free_bytes()
            t0 = time.time()
            try:
                fn()
            except Exception as e:
                print(C.RED + "    出错: %s" % e + C.R)
            time.sleep(0.4)
            freed = free_bytes() - before
            if freed < 0:
                freed = 0
            print(C.GREEN + "  [%s] %s 完成，释放约 %s  (%.1fs)" %
                  (k, name, fmt(freed), time.time() - t0) + C.R)
            return
    print(C.RED + "    无效选项喵~" + C.R)


def one_click():
    print()
    print(C.CYAN + "  一键智能清理开始喵~" + C.R)
    before = free_bytes()
    t0 = time.time()
    for k in ONE_CLICK:
        for kk, name, fn, adm in ITEMS:
            if kk == k:
                if adm and not ADMIN:
                    print(C.GRAY + "    跳过 [%s] %s (需要管理员)" % (k, name) + C.R)
                    continue
                print(C.GRAY + "    > [%s] %s" % (k, name) + C.R)
                try:
                    fn()
                except Exception as e:
                    print(C.RED + "      出错: %s" % e + C.R)
    time.sleep(0.5)
    freed = max(0, free_bytes() - before)
    print()
    print(C.GREEN + C.B + "  一键清理完成，共释放约 %s  (%.1fs)" %
          (fmt(freed), time.time() - t0) + C.R)


def main():
    enable_ansi()
    if len(sys.argv) > 1:
        arg = sys.argv[1].lower()
        if arg in ("-v", "--version"):
            print("小橘清理大师 v" + VERSION)
            return
        if arg == "--list":
            for k, name, _, adm in ITEMS:
                print("%s\t%s%s" % (k, name, "  [需管理员]" if adm else ""))
            return
        if arg == "--clean":
            one_click()
            input("\n    按回车退出...")
            return
        if arg == "--dryrun":
            print("模拟模式：仅显示将要执行的项目")
            for k in ONE_CLICK:
                for kk, name, fn, adm in ITEMS:
                    if kk == k:
                        print("  [%s] %s%s" % (k, name, "  [需管理员]" if adm else ""))
            return
    while True:
        os.system("cls")
        menu()
        try:
            ch = input("    请输入选项后回车: ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            break
        if ch == "":
            continue
        if ch == "0":
            break
        if ch.lower() == "a":
            os.system("cls")
            one_click()
        elif ch.lower() == "s":
            os.system("cls")
            analyze_disk()
        else:
            os.system("cls")
            banner()
            print("-" * 62)
            do_item(ch)
        print()
        input(C.GRAY + "    按回车返回主菜单..." + C.R)
    print()
    print(C.MAGENTA + "    清理完成喵~ 尾巴摇摇，下次见！" + C.R)
    time.sleep(0.8)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n    已取消喵~")
