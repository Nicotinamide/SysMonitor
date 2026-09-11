# SysMonitor - 极简极速桌面悬浮遥测微件与 ZeroTier/Moon 控制器监控中枢

[![SysMonitor CI/CD](https://github.com/Nicotinamide/SysMonitor/actions/workflows/build.yml/badge.svg)](https://github.com/Nicotinamide/SysMonitor/actions/workflows/build.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20(x86__64%20%26%20ARM64)-blue.svg)](#平台支持)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> 极致性能、微秒级响应、内存占用 < 25MB。  
> 跨平台支持 **Windows (10/11)** 与 **Linux (Arch Linux / Debian / Ubuntu 等)**，原生覆盖 **x86_64** 与 **ARM64 (aarch64)** 双硬件架构。

---

## 🌟 核心特性

- **现代微晶玻璃拟态悬浮窗 (Liquid Glass Complication)**：
  - 自由贴边磁吸吸附（屏幕边缘智能吸附，支持多屏切换与 DPI 完美自适应）。
  - 毫秒级极速响应，支持拖拽移动与抽屉式详情控制台展开。
- **模块化微件体系 (Modular Complications)**：
  - **⚡ 供电监测**：电池电量百分比、充放电实时功率 (Watts)、交直流供电状态动态感知。
  - **🌐 网卡吞吐**：实时网络上下行速率计算、全活跃网卡流量动态跟踪。
  - **🔗 局域互联**：ZeroTier Node ID、Moon 轨道节点实时连接状态与最低链路延迟。
  - **💻 算力负载**：CPU 核心使用率与物理内存实时占用。
  - **智能脱敏剔除**：未配置 ZeroTier Token 时自动隐藏互联模块，窗口自适应收缩至极致小巧的双卡片。
- **真·物理跟随横排拖拽条 (Horizontal Physics Drag Bar)**：
  - 设置抽屉内置单行胶囊拖拽条，支持实时物理抓取、悬浮抬起光晕、邻居卡片让位缓动与回弹吸附。
  - 右键或单击快速启停模块（有色启用，灰色停用）。
- **ZeroTier / ztncui 成员智能管理**：
  - 智能轮询同步控制器成员设备，精准识别在线/离线/授权状态。
  - 严谨的 401/403 权限错误拦截与状态栏警告，杜绝离线缓存静默吞噬异常。
  - 成员一键搜索过滤与点击一键复制 IP/NodeID。
- **隐私与硬件级安全存储 (Zero-Trace Security)**：
  - **代码与仓库绝对脱敏**：默认配置中控制器地址、网络 ID 与 Token 全部留空。
  - **Windows 平台**：采用 Windows 原生 DPAPI 硬件加密存储于 `%LOCALAPPDATA%`。
  - **Linux 平台**：基于 `/etc/machine-id` 派生密钥进行 AES-256 本地加密并严格限制 `0600` 权限存储于 `~/.config/SysMonitor/`。
- **中英双语国际化 (i18n)**：
  - 界面语言支持简体中文与 English 一键热切换。

---

## 💻 平台与架构支持

| 操作系统 | 架构 | 编译模式 | 运行依赖 | 状态 |
| :--- | :--- | :--- | :--- | :--- |
| **Linux (Arch Linux)** | `x86_64` | Native AOT (ELF) | 零依赖 (glibc / X11) | ✅ CI/CD 全自动构建 |
| **Linux (Ubuntu / Debian)** | `x86_64` | Native AOT (ELF) | 零依赖 (glibc / X11) | ✅ CI/CD 全自动构建 |
| **Linux (ARM64 / aarch64)** | `aarch64` | Native AOT (ELF) | 零依赖 (树莓派/香橙派/ARM服务器) | ✅ CI/CD 全自动构建 |
| **Windows 10 / 11** | `x64` | Native (EXE) | 内置 .NET 4.8 / 零安装 | ✅ 绿色单文件 |

---

## 🚀 下载与运行

### 1. 从 GitHub Releases / Artifacts 直接下载

在 GitHub [Actions](../../actions) 或 [Releases](../../releases) 页面直接下载对应架构的压缩包：

- **Linux x86_64**：下载 `sysmonitor-linux-x64.tar.gz`，解压即用：
  ```bash
  tar -zxvf sysmonitor-linux-x64.tar.gz
  chmod +x sysmonitor
  ./sysmonitor
  ```
- **Linux ARM64**：下载 `sysmonitor-linux-arm64.tar.gz`，解压即用：
  ```bash
  tar -zxvf sysmonitor-linux-arm64.tar.gz
  chmod +x sysmonitor
  ./sysmonitor
  ```
- **Windows x64**：下载 `sysmonitor-windows-x64.zip`，解压后双击 `SysMonitor.exe` 即可直接运行。

---

### 2. Arch Linux 用户安装 (`PKGBUILD`)

项目在 `scripts/PKGBUILD` 提供了官方风格的打包构建文件：

```bash
cd scripts
makepkg -si
```
系统将自动编译 Native AOT 二进制并集成桌面快捷方式（支持应用启动菜单与自启动）。

---

### 3. Linux 本地源码编译

在安装有 `.NET 8.0 SDK` 和 `clang` 的 Linux 机器上：

```bash
chmod +x scripts/build-local.sh
./scripts/build-local.sh
```
编译产物将输出在 `dist/` 目录下。

---

### 4. Windows 本地极速免环境构建

Windows 系统无需安装 Visual Studio 或额外 SDK，双击运行根目录下的 `build.bat` 即可调用系统内置编译器完成秒级编译，并自动部署更新至当前桌面。

---

## 📂 项目结构

```text
SysMonitor/
├── .github/
│   └── workflows/
│       └── build.yml               # GitHub Actions CI/CD 流水线 (Linux x64/arm64 + Win x64)
├── src/
│   ├── Linux/                      # Linux 原生采集与安全持久化
│   │   ├── LinuxMonitors.cs        # /proc 与 /sys 极速采样 (CPU, RAM, 流量, 电池, Moon)
│   │   └── LinuxSecretStorage.cs   # Machine-ID + AES-256 本地密钥安全持久化
│   ├── Program.cs                  # 单实例互斥锁 (Mutex) 与应用入口
│   ├── MainWindow.cs               # 桌面微晶悬浮窗 (磁吸贴边、多屏感知、拖拽移动)
│   ├── DetailWindow.cs             # 展开式详情控制台 (成员列表、模块物理拖拽条、设置抽屉)
│   ├── MemberDirectory.cs          # ZeroTier/ztncui 成员设备智能同步引擎 (脱敏纯净)
│   ├── Monitors.cs                 # 硬件遥测数据中枢
│   ├── AppSettings.cs              # 配置持久化与模块状态
│   ├── AppTheme.cs                 # 科技暗黑主题调色板与平滑动画缓动曲线
│   ├── I18n.cs                     # 中英双语实时热切换字典
│   ├── Models.cs                   # 数据模型定义
│   ├── FlagAssets.cs               # 节点地区旗帜矢量映射字典
│   └── ToastNotification.cs        # 原生系统通知与就地气泡提示组件
├── scripts/
│   ├── PKGBUILD                    # Arch Linux 官方规范打包构建定义
│   ├── sysmonitor.desktop          # FreeDesktop 标准桌面快捷方式
│   └── build-local.sh              # Linux 本地一键编译脚本
├── tools/                          # 开发辅助与图标生成源码
│   ├── make_icon.cs                # 7 档全分辨率图标生成器
│   ├── gen_flags.py                # 旗帜字典映射脚本
│   └── gen_ico.py                  # 图标辅助脚本
├── app.ico                         # Windows 原生高分辨率图标 (16~256px)
├── app.png                         # Linux 256x256 高清桌面图标
├── build.bat                       # Windows 本地零依赖免装环境一键极速编译脚本
├── SysMonitor.csproj               # .NET 8 跨平台 Native AOT 工程定义
├── .gitignore                      # 完整的 Git 忽略规则 (防机密泄漏)
└── README.md                       # 项目主文档
```

---

## 🔒 隐私与安全性保障

* **绝不上传用户凭证**：软件代码本身完全与任何特定服务器、网络 ID、Token 解耦。
* **本地沙盒加密**：
  * Windows：通过操作系统底层 Windows Data Protection API (DPAPI) 绑定当前登录用户与硬件进行密文存储；
  * Linux：通过操作系统 `/etc/machine-id` 派生独立强密钥进行本地对称加密，文件权限限制为仅当前用户读写（`chmod 600`）。
* **开源审计**：无任何遥测回传代码，零外部未经验证网络依赖。
