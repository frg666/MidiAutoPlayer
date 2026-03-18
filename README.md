# MidiAutoPlayer

一个用于原神自动弹琴的桌面应用程序。

## 功能特点

- **自动演奏**: 自动读取MIDI文件进行弹琴
- **自动升降调**: 每个歌曲独立存储移调设置，互不影响&#x20;
- **批量处理**: 支持一键自动调整所有歌曲的移调
- **后台演奏**: 支持后台模式演奏
- **快捷键**: 小键盘：7上一曲；8暂停/开始；9下一曲

## 技术栈

- **框架**: WPF (.NET 8.0)
- **UI框架**: [WPF-UI](https://github.com/lepoco/wpfui) (Fluent Design)
- **MIDI处理**: [DryWetMidi](https://github.com/melanchall/drywetmidi)

## 代码来源

本项目基于 [刻记牛杂店 (KeqingNiuza)](https://github.com/xunkong/KeqingNiuza) 修改而来。

## 使用说明

### 快速开始

1. 将MIDI文件放入 `Resource/Midi` 文件夹
2. 以管理员身份运行 `MidiAutoPlayer.exe`
3. 点击"自动调整"自动调整
4. 双击列表中的歌曲开始演奏

### 操作指南

- **双击歌曲**: 开始演奏
- **自动调整**: 自动找到命中率最高的移调值
- **刷新游戏窗口**: 如果没有检测到游戏窗口，点击此按钮刷新

## 项目结构

```
MidiAutoPlayer/
├── Core/
│   ├── Midi/           # MIDI处理核心
│   ├── MusicGame/      # 游戏按键映射
│   └── Native/         # Win32 API封装
├── View/               # XAML视图
├── ViewModel/          # MVVM视图模型
├── App.xaml           # 应用程序入口
└── MainWindow.xaml   # 主窗口
```

## 编译

```bash
# 安装依赖
dotnet restore

# 编译
dotnet build

# 发布（自包含）
dotnet publish -c Release -r win-x64 --self-contained true -o ./bin/Publish
```

## 许可证

本项目仅供学习交流使用，请勿用于商业用途。
