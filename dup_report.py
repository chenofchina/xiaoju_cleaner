import json, os

d = json.load(open('dups.json', encoding='utf-8'))
other = [g for g in d if 'Andboxone' not in g['files'][0]]

def short(p):
    return p.replace(r'C:\Users\steam\Desktop\file (1)\\'[0:-1], 'file(1)\\').replace(
        'C:\\Users\\steam\\Desktop\\', '')

print('=== 重复文件：Andboxone 之外（>=5MB）===')
big = sorted([g for g in other if g['size_mb'] >= 5], key=lambda x: -x['waste_mb'])
for g in big:
    print(f"[{g['size_mb']}MB x{g['count']}] 浪费 {g['waste_mb']}MB")
    for p in g['files']:
        print('    ', short(p))
small = [g for g in other if g['size_mb'] < 5]
print(f"...另有 {len(small)} 组 <5MB 的小重复，合计 {sum(g['waste_mb'] for g in small):.1f} MB")
print(f"非Andboxone小计浪费: {sum(g['waste_mb'] for g in other)/1024:.2f} GB")
