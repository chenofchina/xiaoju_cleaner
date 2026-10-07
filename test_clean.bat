@echo off
chcp 936 >nul
setlocal EnableDelayedExpansion
title 小橘清理大师 v2.0
color 0E

net session >nul 2>&1
if errorlevel 1 (set "ADMIN=0") else (set "ADMIN=1")

:MENU
cls
echo ==============================================================
echo                小  橘  清  理  大  师   v2.0
echo                      喵~ 纯命令行版 CCleaner
echo ==============================================================
if "%ADMIN%"=="1" (
  echo    运行权限: 管理员      全部功能可用
) else (
  echo    运行权限: 普通用户    带 * 的功能请右键以管理员运行
)
echo --------------------------------------------------------------
call :SHOWSPACE
echo --------------------------------------------------------------
echo     [1]  用户临时文件            [2]  系统临时文件         *
echo     [3]  回收站                  [4]  浏览器缓存
echo     [5]  Windows Update 缓存   * [6]  缩略图 / 图标缓存
echo     [7]  错误报告 / 崩溃转储     [8]  预读取缓存 Prefetch  *
echo     [9]  最近使用记录            [10] 系统日志文件
echo     [11] 字体缓存                [12] 传递优化缓存 DO    *
echo     [13] DNS 缓存刷新            [14] 组件存储深度清理   *
echo     [15] 关闭休眠文件          * [16] 磁盘清理工具       *
echo --------------------------------------------------------------
echo     [A]  一键智能清理            [S]  分析 C 盘占用排行
echo     [0]  退出
echo ==============================================================
set /a TICK+=1
if %TICK% GTR 40 (echo. & echo    检测到无输入，自动退出喵... & goto :EXIT)
set "CH="
set /p "CH=请输入选项后回车: "
for /f "tokens=* delims= " %%x in ("%CH%") do set "CH=%%x"
if "%CH%"=="1"  (set "NAME=用户临时文件"   & call :BEF & call :F1  & call :DONE & goto :BACK)
if "%CH%"=="2"  (set "NAME=系统临时文件"   & call :BEF & call :F2  & call :DONE & goto :BACK)
if "%CH%"=="3"  (set "NAME=回收站"         & call :BEF & call :F3  & call :DONE & goto :BACK)
if "%CH%"=="4"  (set "NAME=浏览器缓存"     & call :BEF & call :F4  & call :DONE & goto :BACK)
if "%CH%"=="5"  (set "NAME=WU 缓存"        & call :BEF & call :F5  & call :DONE & goto :BACK)
if "%CH%"=="6"  (set "NAME=缩略图缓存"     & call :BEF & call :F6  & call :DONE & goto :BACK)
if "%CH%"=="7"  (set "NAME=错误报告"       & call :BEF & call :F7  & call :DONE & goto :BACK)
if "%CH%"=="8"  (set "NAME=Prefetch"       & call :BEF & call :F8  & call :DONE & goto :BACK)
if "%CH%"=="9"  (set "NAME=最近使用记录"   & call :BEF & call :F9  & call :DONE & goto :BACK)
if "%CH%"=="10" (set "NAME=系统日志"       & call :BEF & call :F10 & call :DONE & goto :BACK)
if "%CH%"=="11" (set "NAME=字体缓存"       & call :BEF & call :F11 & call :DONE & goto :BACK)
if "%CH%"=="12" (set "NAME=传递优化缓存"   & call :BEF & call :F12 & call :DONE & goto :BACK)
if "%CH%"=="13" (set "NAME=DNS 缓存"       & call :BEF & call :F13 & call :DONE & goto :BACK)
if "%CH%"=="14" (set "NAME=组件存储清理"   & call :BEF & call :F14 & call :DONE & goto :BACK)
if "%CH%"=="15" (set "NAME=休眠文件"       & call :BEF & call :F15 & call :DONE & goto :BACK)
if "%CH%"=="16" (set "NAME=磁盘清理工具"   & call :BEF & call :F16 & call :DONE & goto :BACK)
if /i "%CH%"=="a" (goto :ONECLICK)
if /i "%CH%"=="s" (goto :ANALYZE)
if "%CH%"=="0" (goto :EXIT)
goto :MENU

:BACK
echo.
echo    按任意键返回主菜单...
pause >nul
goto :MENU

======================= 功能模块 =======================

:F1
echo    正在清理用户临时文件...
del /f /q /s "%TEMP%\*.*" >nul 2>&1
for /d %%i in ("%TEMP%\*") do rd /s /q "%%i" >nul 2>&1
goto :eof

:F2
if "%ADMIN%"=="0" (echo    跳过 - 需要管理员权限 & goto :eof)
echo    正在清理系统临时文件...
del /f /q /s "%SystemRoot%\Temp\*.*" >nul 2>&1
for /d %%i in ("%SystemRoot%\Temp\*") do rd /s /q "%%i" >nul 2>&1
goto :eof

:F3
echo    正在清空回收站...
powershell -NoProfile -InputFormat None -Command "Clear-RecycleBin -Force -ErrorAction SilentlyContinue" >nul 2>&1
goto :eof

:F4
echo    正在清理浏览器缓存...
for %%B in ("%LOCALAPPDATA%\Google\Chrome\User Data" "%LOCALAPPDATA%\Microsoft\Edge\User Data" "%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data" "%LOCALAPPDATA%\360Chrome\Chrome\User Data" "%LOCALAPPDATA%\Tencent\QQBrowser\User Data") do (
  if exist "%%~B" (
    for /d %%p in ("%%~B\*") do (
      if exist "%%p\Cache" rd /s /q "%%p\Cache" >nul 2>&1
      if exist "%%p\Code Cache" rd /s /q "%%p\Code Cache" >nul 2>&1
      if exist "%%p\GPUCache" rd /s /q "%%p\GPUCache" >nul 2>&1
      if exist "%%p\Service Worker" rd /s /q "%%p\Service Worker" >nul 2>&1
    )
  )
)
for /d /r "%LOCALAPPDATA%\Mozilla\Firefox\Profiles" %%p in (cache2) do rd /s /q "%%p" >nul 2>&1
echo    浏览器缓存清理完毕
goto :eof

:F5
if "%ADMIN%"=="0" (echo    跳过 - 需要管理员权限 & goto :eof)
echo    正在清理 Windows Update 缓存...
net stop wuauserv >nul 2>&1
net stop bits >nul 2>&1
rd /s /q "%SystemRoot%\SoftwareDistribution\Download" >nul 2>&1
md "%SystemRoot%\SoftwareDistribution\Download" >nul 2>&1
net start wuauserv >nul 2>&1
net start bits >nul 2>&1
goto :eof

:F6
echo    正在清理缩略图 / 图标缓存...
taskkill /f /im explorer.exe >nul 2>&1
ping -n 2 127.0.0.1 >nul 2>&1
del /f /q "%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*.db" >nul 2>&1
del /f /q "%LOCALAPPDATA%\Microsoft\Windows\Explorer\iconcache_*.db" >nul 2>&1
del /f /q "%LOCALAPPDATA%\IconCache.db" >nul 2>&1
start explorer.exe
goto :eof

:F7
echo    正在清理错误报告 / 崩溃转储...
del /f /q /s "%LOCALAPPDATA%\CrashDumps\*.*" >nul 2>&1
rd /s /q "%LOCALAPPDATA%\Microsoft\Windows\WER" >nul 2>&1
rd /s /q "%PROGRAMDATA%\Microsoft\Windows\WER" >nul 2>&1
del /f /q /s "%SystemRoot%\Minidump\*.*" >nul 2>&1
del /f /q "%SystemRoot%\MEMORY.DMP" >nul 2>&1
goto :eof

:F8
if "%ADMIN%"=="0" (echo    跳过 - 需要管理员权限 & goto :eof)
echo    正在清理预读取缓存...
del /f /q /s "%SystemRoot%\Prefetch\*.*" >nul 2>&1
goto :eof

:F9
echo    正在清理最近使用记录 / 跳转列表...
del /f /q /s "%APPDATA%\Microsoft\Windows\Recent\*.*" >nul 2>&1
del /f /q /s "%APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations\*.*" >nul 2>&1
del /f /q /s "%APPDATA%\Microsoft\Windows\Recent\CustomDestinations\*.*" >nul 2>&1
goto :eof

:F10
echo    正在清理系统日志...
del /f /q /s "%SystemRoot%\Logs\CBS\*.*" >nul 2>&1
del /f /q /s "%SystemRoot%\Logs\*.log" >nul 2>&1
del /f /q /s "%SystemRoot%\*.log" >nul 2>&1
goto :eof

:F11
echo    正在清理字体缓存...
net stop FontCache >nul 2>&1
del /f /q /s "%SystemRoot%\ServiceProfiles\LocalService\AppData\Local\FontCache\*.*" >nul 2>&1
net start FontCache >nul 2>&1
del /f /q /s "%LOCALAPPDATA%\FontCache\*.*" >nul 2>&1
goto :eof

:F12
if "%ADMIN%"=="0" (echo    跳过 - 需要管理员权限 & goto :eof)
echo    正在清理传递优化缓存...
powershell -NoProfile -InputFormat None -Command "Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue" >nul 2>&1
goto :eof

:F13
echo    正在刷新 DNS 缓存...
ipconfig /flushdns >nul 2>&1
ipconfig /registerdns >nul 2>&1
goto :eof

:F14
if "%ADMIN%"=="0" (echo    跳过 - 需要管理员权限 & goto :eof)
echo    正在深度清理组件存储，可能要几分钟，请耐心等喵...
DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase
goto :eof

:F15
if "%ADMIN%"=="0" (echo    跳过 - 需要管理员权限 & goto :eof)
echo    正在关闭休眠并删除 hiberfil.sys...
powercfg /h off
goto :eof

:F16
echo    正在启动 Windows 磁盘清理...
start cleanmgr
goto :eof

======================= 一键清理 =======================

:ONECLICK
cls
set "NAME=一键智能清理"
call :BEF
echo.
echo    开始一键智能清理...
echo.
call :F1
call :F3
call :F4
call :F6
call :F7
call :F9
call :F11
call :F13
if "%ADMIN%"=="1" (
  call :F2
  call :F5
  call :F8
  call :F10
  call :F12
)
call :DONE
goto :BACK

======================= 空间统计 =======================

:BEF
for /f %%a in ('powershell -NoProfile -InputFormat None -Command "[math]::Floor((Get-PSDrive C).Free/1MB)"') do set "B=%%a"
exit /b

:FREEB
for /f %%a in ('powershell -NoProfile -InputFormat None -Command "[math]::Floor((Get-PSDrive C).Free/1MB)"') do set "CURMB=%%a"
exit /b

:SHOWSPACE
call :FREEB
set /a G=%CURMB%/1024
set /a M=%CURMB%%%%1024
echo    C 盘可用空间: %G% GB %M% MB
exit /b

:DONE
call :FREEB
set /a D=%B%-%CURMB%
if %D% LSS 0 set /a D=0
echo.
if %D% GEQ 1024 (
  set /a DG=%D%/1024
  echo    [ !NAME! ] 完成，释放约 !DG! GB
) else (
  echo    [ !NAME! ] 完成，释放约 %D% MB
)
goto :eof

:ANALYZE
cls
echo    正在分析 C 盘各目录占用，可能要 1-2 分钟喵...
echo.
powershell -NoProfile -InputFormat None -Command "Get-ChildItem 'C:\' -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object { $s=(Get-ChildItem $_.FullName -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum; [PSCustomObject]@{ '目录'=$_.Name; 'GB'=[math]::Round($s/1GB,2) } } | Sort-Object 'GB' -Descending | Select-Object -First 15 | Format-Table -AutoSize"
echo.
echo    按任意键返回主菜单...
pause >nul
goto :MENU

:EXIT
echo.
echo    清理完成喵~ 尾巴摇摇，下次见！
ping -n 2 127.0.0.1 >nul
exit /b
