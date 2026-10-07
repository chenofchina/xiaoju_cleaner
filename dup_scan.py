import os, hashlib, sys, json
from collections import defaultdict

ROOTS = [
    r"C:\Users\steam\Desktop\file (1)",
    r"C:\Users\steam\Desktop\Andboxone",
    r"C:\Users\steam\Desktop\02_开发项目",
]
SKIP_NAMES = {"beeng"}          # 主人说不看，小橘不看
SKIP_DIRS = {"$RECYCLE.BIN", "System Volume Information", "node_modules", ".git"}

MIN = 1 * 1024 * 1024

def walk(root):
    for dp, dns, fns in os.walk(root, topdown=True):
        dns[:] = [d for d in dns if d not in SKIP_DIRS and d not in SKIP_NAMES]
        for fn in fns:
            p = os.path.join(dp, fn)
            try:
                st = os.stat(p)
            except OSError:
                continue
            if st.st_size >= MIN:
                yield p, st.st_size

sizes = defaultdict(list)
for r in ROOTS:
    if os.path.isdir(r):
        for p, s in walk(r):
            sizes[s].append(p)

cands = {s: v for s, v in sizes.items() if len(v) > 1}
print(f"候选（同尺寸）文件组: {len(cands)}", flush=True)

def h(p):
    m = hashlib.md5()
    try:
        with open(p, "rb") as f:
            for chunk in iter(lambda: f.read(1024 * 1024), b""):
                m.update(chunk)
        return m.hexdigest()
    except OSError:
        return None

groups = defaultdict(list)
for s, v in cands.items():
    for p in v:
        d = h(p)
        if d:
            groups[(s, d)].append(p)

dups = [v for v in groups.values() if len(v) > 1]
dups.sort(key=lambda v: -os.path.getsize(v[0]))

total_waste = 0
out = []
for v in dups:
    sz = os.path.getsize(v[0])
    waste = sz * (len(v) - 1)
    total_waste += waste
    out.append({"size_mb": round(sz / 1048576, 1), "count": len(v), "waste_mb": round(waste / 1048576, 1), "files": v})

with open("dups.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)

print(f"确认重复组: {len(dups)}  可回收约 {total_waste/1073741824:.2f} GB", flush=True)
for g in out[:40]:
    print(f"\n[{g['size_mb']}MB x{g['count']}] 浪费{g['waste_mb']}MB")
    for p in g["files"]:
        print("   ", p)
