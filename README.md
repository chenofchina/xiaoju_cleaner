# 小橘清理大师

喵~ 一个命令行版的 Windows 清理工具，纯本地运行，不联网、不上传、不装服务。

- **C# 版 v3.0** —— 单文件 exe，原生编译，速度快、体积小
- **Python 版 v2.0** —— 早期版本，功能一致，作为参考保留

## 功能

| # | 项目 | 需要管理员 |
|---|------|-----------|
| 1 | 用户临时文件 | |
| 2 | 系统临时文件 | * |
| 3 | 回收站 | |
| 4 | 浏览器缓存（Chrome / Edge / Brave / 360 / QQ / Chromium / Firefox） | |
| 5 | Windows 更新缓存 | * |
| 6 | 缩略图 / 图标缓存 | |
| 7 | 崩溃转储与错误报告 | |
| 8 | 预读取 Prefetch | * |
| 9 | 最近使用记录 | |
| 10 | 系统日志文件 | |
| 11 | 字体缓存 | |
| 12 | 传递优化缓存 | * |
| 13 | DNS 缓存刷新 | |
| 14 | DISM 组件清理 | * |
| 15 | 关闭休眠文件 | * |
| 16 | 磁盘清理工具 cleanmgr | * |

另外还有：

- **一键智能清理（A）**：按安全顺序批量执行免管理员的项目
- **C 盘占用排行（S）**：多线程扫描 C 盘各目录体积，输出 Top 15 条形图

## 使用方法

双击 exe 进入交互菜单，或带参数运行：

```
XiaoJuCleaner.exe --version    查看版本
XiaoJuCleaner.exe --list       列出所有清理项目
XiaoJuCleaner.exe --dryrun     只显示一键清理会做什么，不实际删除
XiaoJuCleaner.exe --clean      直接一键智能清理
XiaoJuCleaner.exe --scan       扫描 C 盘占用排行
```

带 `*` 的项目需要右键「以管理员身份运行」。

## 下载

编译好的 exe 放在 [`release/`](release/) 目录，直接下载即可：

| 文件 | 体积 | 说明 |
|------|------|------|
| `XiaoJuCleaner-fd-win-x64.exe` | ~176 KB | 框架依赖版，需要系统已装 .NET 8 运行时 |
| `XiaoJuCleaner-sc-win-x64.exe` | ~10.5 MB | 自包含免安装版，Win10/11 x64 直接跑 |

## 从源码编译

需要 .NET 8 SDK。

```powershell
cd cs

# 框架依赖单文件（体积最小）
dotnet publish -c Release -r win-x64 --self-contained false `
  /p:PublishSingleFile=true -o ..\dist-fd

# 自包含单文件（免安装，裁剪 + 压缩）
dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true `
  /p:PublishTrimmed=true /p:TrimMode=partial -o ..\dist-sc
```

## 性能

C 盘全盘占用扫描（本机实测，约 388 GB 数据）：

| 实现 | 耗时 |
|------|------|
| Python 版（单线程） | 49 s |
| C# 版（多线程） | 22.5 s |

## 为什么重写了 C# 版

早期 Python 版用 PyInstaller 打包，生成的 exe 带一层自解压外壳，
特征与某些恶意程序高度相似，容易被卡巴斯基等杀软报成 `Agent` 类误报。

C# 版由 .NET 编译器直接产出原生 exe，没有自解压外壳，误报率显著降低。

## 目录结构

```
cs/                     C# 版源码
  Program.cs            全部逻辑（单文件）
  XiaoJuCleaner.csproj  项目文件
cleaner.py              Python 版源码
clean_artifacts.py      构建清理脚本
```

## 许可

随意使用，出问题别找小橘喵。
