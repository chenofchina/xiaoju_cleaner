# -*- coding: utf-8 -*-
"""小橘清理大师 - 全量功能自测（提权后运行，结果写入桌面日志）"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cleaner as cl

LOG = r"C:\Users\steam\Desktop\小橘全量测试日志_%s.txt" % ("admin" if cl.ADMIN else "user")
_lines = []


def log(s):
    print(s)
    _lines.append(s)


def main():
    cl.enable_ansi()
    log("=" * 60)
    log("     小橘清理大师 v%s  全量功能自测" % cl.VERSION)
    log("=" * 60)
    log("时间       : " + time.strftime("%Y-%m-%d %H:%M:%S"))
    log("管理员权限 : " + ("是" if cl.ADMIN else "否"))
    log("初始可用   : " + cl.fmt(cl.free_bytes()))
    log("-" * 60)

    m = {k: (name, fn, adm) for k, name, fn, adm in cl.ITEMS}
    order = ["1", "3", "4", "13", "6", "7", "9", "11", "10",
             "2", "5", "8", "12", "14"]
    for k in order:
        name, fn, adm = m[k]
        if adm and not cl.ADMIN:
            log("[%2s] %-24s 跳过（需要管理员）" % (k, name))
            continue
        before = cl.free_bytes()
        t0 = time.time()
        try:
            fn()
            ok = "OK"
        except Exception as e:
            ok = "出错: %s" % e
        time.sleep(0.4)
        freed = max(0, cl.free_bytes() - before)
        log("[%2s] %-24s %-6s 释放 %-10s (%.1fs)" %
            (k, name, ok, cl.fmt(freed), time.time() - t0))

    log("-" * 60)
    log("结束可用   : " + cl.fmt(cl.free_bytes()))
    log("说明       : 未测试 15(关闭休眠) 和 16(cleanmgr 图形界面)")
    log("=" * 60)

    try:
        with open(LOG, "w", encoding="utf-8") as f:
            f.write("\n".join(_lines))
        print("\n日志已写入: " + LOG)
    except Exception as e:
        print("写日志失败:", e)
    time.sleep(3)


if __name__ == "__main__":
    main()
