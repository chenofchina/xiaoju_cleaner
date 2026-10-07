# -*- coding: utf-8 -*-
import os, shutil

FORBIDDEN = ['c:\\leidian']  # 雷电模拟器，绝对不动

def guarded(p):
    lp = os.path.abspath(p).lower()
    return not any(lp.startswith(f) for f in FORBIDDEN)

def dsize(p):
    t = 0
    for dp, dns, fns in os.walk(p):
        for f in fns:
            try:
                t += os.path.getsize(os.path.join(dp, f))
            except OSError:
                pass
    return t

TARGETS = [
    (r'C:\Users\steam\Desktop\Andboxone\RuntimeSdk', 'AndboxOne 运行时SDK(可用网盘重下)'),
    (r'C:\Users\steam\Desktop\file (1)\新建文件夹', '驱动包解压目录'),
    (os.path.join(os.environ['LOCALAPPDATA'], r'AndboxOne\avd'), '安卓模拟器镜像x4'),
    (r'C:\Users\steam\Desktop\file (1)\My project\MTool', 'MTool 重复副本(与 file(1)\\MTool 完全相同)'),
    (r'C:\Users\steam\Desktop\file (1)\project\AutoPyEver.exe', 'AutoPyEver 重复副本'),
]

free0 = shutil.disk_usage('C:\\').free
total = 0
for p, why in TARGETS:
    if not os.path.exists(p):
        print(f'[跳过-不存在] {p}')
        continue
    if not guarded(p):
        print(f'[拒绝-禁区] {p}')
        continue
    s = dsize(p) if os.path.isdir(p) else os.path.getsize(p)
    try:
        if os.path.isdir(p):
            shutil.rmtree(p)
        else:
            os.remove(p)
        total += s
        print(f'[删除] {p}\n        {s/1048576:.1f} MB  <- {why}')
    except Exception as e:
        print(f'[失败] {p} -> {e}')

free1 = shutil.disk_usage('C:\\').free
print(f'\n本轮释放 {total/1073741824:.2f} GB | 现可用 {free1/1073741824:.2f} GB')
