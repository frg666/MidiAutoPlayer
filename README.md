# MidiAutoPlayer

[![License](https://img.shields.io/badge/License-GPL--3.0--NC-blue)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![WPF-UI](https://img.shields.io/badge/UI-WPF--UI%203.0.5-0078D4)](https://github.com/lepoco/wpfui)

一个用于**原神自动弹琴**的 Windows 桌面应用。支持 MIDI 与 DoMiSo 谱子，自动计算最佳移调，一键演奏。

## 功能特点

- **自动演奏**：读取谱子后自动按下对应键位，在游戏内弹奏
- **三乐器支持**：风物之诗琴（钢琴，21 键）、晚风圆号（14 键）与吉他（14 单音 + 7 和弦）自动切换键位映射
- **自动升降调**：整体平移音高，让音符尽量落入乐器音域，不做八度折返
- **命中率统计**：实时显示每首曲子的可演奏音符数与命中率（钢琴 / 圆号 / 吉他三套独立计算）
- **和弦识别**：吉他模式下，自动把时间窗口内快速出现的音归并成和弦键（C/Dm/Em/F/G/Am/G7）
- **双谱格式**：支持标准 MIDI（`.mid`）与 DoMiSo 谱子（`.dms` / `.txt`）
- **批量处理**：一键自动调整全部曲子的最佳移调；批量删除低命中率文件
- **排序与搜索**：按命中率排序、按文件名搜索
- **后台演奏**：支持后台模式（可能略有卡顿）
- **界面透明**：亚克力毛玻璃 + 全透明背景，窗口透明度可调

## 乐器键位映射

| 乐器 | 键数 | 键位布局 |
|------|------|----------|
| 风物之诗琴（钢琴） | 21 键（三排） | `Z-M` / `A-J` / `Q-U` |
| 晚风圆号 | 14 键（两排） | `A-J`（低音）/ `Q-U`（高音），无 `Z-M` 排 |
| 吉他 | 14 单音 + 7 和弦 | 单音：`A-J`（低音）/ `Q-U`（高音）；和弦：`Z X C V B N M` = C/Dm/Em/F/G/Am/G7 |

## 快捷键

| 按键 | 功能 |
|------|------|
| 小键盘 `7` | 上一曲 |
| 小键盘 `8` | 暂停 / 播放 |
| 小键盘 `9` | 下一曲 |

## 使用说明

### 快速开始

1. 将谱子放入 `Resource/Midi` 文件夹（支持 `.mid` / `.dms` / `.txt`）
2. 以**管理员身份**运行 `MidiAutoPlayer.exe`
3. 双击列表中的歌曲开始演奏（也可用小键盘 `8` 控制播放）

### 界面布局

- **MIDI 播放器**：播放标准 MIDI 文件
- **DMS/TXT 播放器**：播放 DoMiSo 谱子（含 `.dms` / `.txt` 文件）
- **设置**：一键自动调整、批量删除低命中率文件、窗口透明度调节

### 操作指南

- **自动调整**：自动找到命中率最高的移调值并保存（同时计算钢琴与圆号两套）
- **刷新游戏窗口**：检测不到原神窗口时，点击刷新
- **一键自动调整**（设置页）：扫描所有文件，逐个计算并保存最佳移调
- **批量删除**（设置页）：删除自动调整后命中率低于阈值的文件

## 技术栈

| 组件 | 说明 |
|------|------|
| 框架 | WPF（.NET 8.0） |
| UI 框架 | [WPF-UI](https://github.com/lepoco/wpfui) 3.0.5（Fluent Design + 亚克力毛玻璃） |
| MIDI 处理 | [DryWetMidi](https://github.com/melanchall/drywetmidi) 5.2.1 |
| 编码处理 | System.Text.Encoding.CodePages |

## 代码来源

本项目基于 [刻记牛杂店 (KeqingNiuza)](https://github.com/xunkong/KeqingNiuza) 修改而来。

DoMiSo 谱子（`.dms` / `.txt`）的解析与播放逻辑参照 [DoMiSo-Universal](https://github.com/Nigh/DoMiSo-Universal) 自动版实现。

## 项目结构

```
MidiAutoPlayer/
├── Core/
│   ├── Midi/               # MIDI / DoMiSo 解析、播放、移调、命中率计算
│   ├── MusicGame/          # 乐器类型、按键、操作枚举
│   └── Native/             # Win32 API 封装（热键、窗口、虚拟键）
├── View/                   # XAML 视图（MidiView、SettingsView）
├── ViewModel/              # MVVM 视图模型
├── MainWindow.xaml         # 主窗口（三栏导航 + 标题栏）
└── App.xaml                # 应用程序入口
```

## 编译

```bash
# 安装依赖
dotnet restore

# 编译
dotnet build

# 发布（自包含，win-x64）
dotnet publish -c Release -r win-x64 --self-contained true -o ./bin/Publish
```

## 许可证

本项目基于 GPL-3.0 修改，仅供学习交流使用，请勿用于商业用途。
