# FloatNote / 浮签

FloatNote / 浮签 是一款轻量级 Windows 桌面浮动便签和待办应用，基于 WPF 构建。

## 当前 MVP 功能

- 置顶浮动便签窗口
- 笔记文本自动保存
- 待办支持标题和可选内容
- 待办支持折叠和展开
- 添加、完成、隐藏已完成、删除待办
- 双击标记当前待办
- 悬浮球入口，支持贴边停靠
- 悬停预览当前待办
- 浅色和深色主题切换
- 保存主窗口位置和大小
- 系统托盘显示、隐藏、退出
- 全局快捷键：`Ctrl + Alt + N`
- 本地 JSON 存储

## 运行

```powershell
dotnet run
```

## 构建

```powershell
dotnet build
```

## 发布 exe

框架依赖发布：

```powershell
dotnet publish -c Release -r win-x64
```

自包含发布：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## 数据

应用数据存储在：

```text
%LOCALAPPDATA%\FloatNote\app-state.json
```
