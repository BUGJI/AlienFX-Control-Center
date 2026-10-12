# AlienFX Control Center

[English](#english) | [简体中文](#简体中文)

A modern WinUI 3 control panel for Dell Alienware laptops, built on top of the
[AlienFX-Tools](https://github.com/T-Troll/alienfx-tools) command-line utilities.

一款基于 [AlienFX-Tools](https://github.com/T-Troll/alienfx-tools) 命令行工具的
戴尔外星人笔记本现代化 WinUI 3 控制面板。

---

## English

> [!WARNING]
> **Project status**
> - This project has **only been tested on an Alienware M16 R2**. Other models may
>   work but are untested — use at your own risk.
> - The code was written largely by **AI** and has not been fully reviewed.
> - The project is **incomplete and not yet polished**; expect rough edges, bugs and
>   breaking changes.

### Features

- **Lights** — control AlienFX lighting per device and per light, with an on-screen
  keyboard matrix for individual key selection.
  - Scope presets: All / WASD / Arrow keys / F-keys.
  - Built-in color picker plus a quick color palette.
  - Export the detected light layout to JSON for calibration.
- **Fans** — monitor fan RPM, max RPM and temperature sensors, unlock fan control
  and apply custom fan speed percentages.
- **Settings** — switch UI language (English / 简体中文), toggle auto-apply, choose
  theme, show/hide raw debug output, rematch and manage device calibration
  profiles, and point the app at your AlienFX-Tools folder.
- Per-device calibration profiles with import/export.

### Requirements

- Windows 10 version 1809 (build 17763) or later.
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source.
- [AlienFX-Tools](https://github.com/T-Troll/alienfx-tools) installed, providing
  `alienfx-cli.exe` and `alienfan-cli.exe`.

### Finding the tools

The app automatically looks for the CLI tools in:

1. A folder configured in **Settings** (your override).
2. `%LOCALAPPDATA%\AlienFX Tools`
3. `%ProgramFiles%\AlienFX Tools`
4. `%ProgramFiles(x86)%\AlienFX Tools`

If your tools live elsewhere, browse to the folder in **Settings → Tools**.

### Build & run

```powershell
dotnet build AlienFXControlCenter.csproj -c Debug
dotnet run --project AlienFXControlCenter.csproj
```

### Tech stack

- WinUI 3 / Windows App SDK (unpackaged, self-contained)
- CommunityToolkit.Mvvm
- .NET 10 targeting `net10.0-windows`

### License

Released under the [MIT License](LICENSE).

This project is an independent front-end and is not affiliated with Dell, Alienware,
or the AlienFX-Tools authors. AlienFX-Tools is a separate project under its own license.

---

## 简体中文

> [!WARNING]
> **项目状态**
> - 本项目**仅在 Alienware M16 R2 上测试过**。其他型号可能可用但未经验证，请自行承担风险。
> - 代码主要由 **AI** 编写，尚未经过完整审查。
> - 项目**尚不完善**，可能存在粗糙之处、缺陷以及破坏性变更。

### 功能

- **灯光** — 按设备、按灯珠控制 AlienFX 灯光，并提供屏幕键盘矩阵用于单独选键。
  - 范围预设：全部 / WASD / 方向键 / F 功能键。
  - 内置取色器与快捷调色板。
  - 可将检测到的灯光布局导出为 JSON，用于校准。
- **风扇** — 监控风扇转速、最高转速与温度传感器，解锁风扇控制并应用自定义风扇转速百分比。
- **设置** — 切换界面语言（English / 简体中文）、自动应用、主题、显示/隐藏原始调试输出、
  重新匹配与管理设备校准配置，并指定 AlienFX-Tools 所在文件夹。
- 支持按设备校准配置的导入与导出。

### 环境要求

- Windows 10 版本 1809（内部版本 17763）或更高。
- 从源码构建需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)。
- 安装 [AlienFX-Tools](https://github.com/T-Troll/alienfx-tools)，其中包含
  `alienfx-cli.exe` 与 `alienfan-cli.exe`。

### 工具查找路径

应用会按顺序自动查找命令行工具：

1. **设置**中指定的文件夹（自定义路径）。
2. `%LOCALAPPDATA%\AlienFX Tools`
3. `%ProgramFiles%\AlienFX Tools`
4. `%ProgramFiles(x86)%\AlienFX Tools`

如果工具位于其它位置，请在 **设置 → 工具** 中手动选择对应文件夹。

### 构建与运行

```powershell
dotnet build AlienFXControlCenter.csproj -c Debug
dotnet run --project AlienFXControlCenter.csproj
```

### 技术栈

- WinUI 3 / Windows App SDK（非打包、自包含）
- CommunityToolkit.Mvvm
- .NET 10，目标框架 `net10.0-windows`

### 许可证

本项目基于 [MIT 许可证](LICENSE) 发布。

本项目为独立前端，与 Dell、Alienware 及 AlienFX-Tools 作者无关联。
AlienFX-Tools 是遵循其自身许可证的独立项目。
