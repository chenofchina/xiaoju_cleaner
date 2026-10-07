import os, shutil, sys

def dsize(p):
    t = 0
    for dp, dns, fns in os.walk(p):
        for f in fns:
            try:
                t += os.path.getsize(os.path.join(dp, f))
            except OSError:
                pass
    return t

TARGETS = []
base1 = r'C:\Users\steam\Desktop\02_开发项目'
for proj in ['kerdonEDR', r'zcode\EasyCapture', r'zcode\USNJournalExplorer', r'zcode\FeatherBrowser']:
    for d in ['bin', 'obj', 'publish', 'dist']:
        TARGETS.append(os.path.join(base1, proj, d))
TARGETS.append(os.path.join(base1, r'zcode\dist'))
TARGETS.append(os.path.join(base1, r'zcode\ClashClient\ClashClient.Windows\Bin\Debug'))

base2 = r'C:\Users\steam\Desktop\file (1)\My project'
for d in [r'VisionApp\app\build', r'VisionApp\.gradle', r'testedr\build', r'testedr\dist',
          r'opens\bin', r'opens\obj', r'opens\dist']:
    TARGETS.append(os.path.join(base2, d))

free0 = shutil.disk_usage('C:\\').free
total = 0
for t in TARGETS:
    if os.path.isdir(t):
        s = dsize(t)
        try:
            shutil.rmtree(t)
            total += s
            print(f'[删除] {t.replace("C:\\Users\\steam\\Desktop\\", "")}  {s/1048576:.1f} MB')
        except Exception as e:
            print(f'[失败] {t} -> {e}')
    else:
        print(f'[跳过-不存在] {t.replace("C:\\Users\\steam\\Desktop\\", "")}')

free1 = shutil.disk_usage('C:\\').free
print(f'\n小橘释放: {(free1-free0)/1073741824:.2f} GB   现在可用: {free1/1073741824:.2f} GB')
