# 快速开始 - VS Code Android 部署

## 🚀 最简单的方法

### 方法 1: 使用一键脚本（推荐）

在 VS Code 终端中运行：

```powershell
.\build-android.ps1
```

脚本会自动：
- ✓ 检查环境
- ✓ 清理项目
- ✓ 编译应用
- ✓ 部署到设备

### 方法 2: 使用 VS Code 任务

1. 按 `Ctrl+Shift+P`
2. 输入 `Tasks: Run Task`
3. 选择 `android-run`

### 方法 3: 使用快捷键

按 `Ctrl+Shift+B` 进行构建

## 📱 准备设备

### 连接真实设备

1. 开启开发者选项（连点版本号7次）
2. 启用 USB 调试
3. 用 USB 连接手机
4. 允许 USB 调试授权

### 验证连接

```powershell
adb devices
```

## 🔧 安装必需软件

如果提示缺少依赖，请按以下顺序安装：

### 1. .NET 8.0 SDK

```powershell
winget install Microsoft.DotNet.SDK.8
```

### 2. .NET MAUI 工作负载

```powershell
dotnet workload install maui
```

### 3. VS Code 扩展

在 VS Code 中安装：
- **C# Dev Kit** (必需)
- **.NET MAUI** (推荐)

## ⚡ 快速命令

```powershell
# 编译
dotnet build -f net8.0-android

# 编译并运行
dotnet build -t:Run -f net8.0-android

# 清理
dotnet clean -f net8.0-android

# 生成 APK
dotnet publish -f net8.0-android -c Release
```

## 🎯 常见问题

### 找不到设备？
```powershell
adb kill-server
adb start-server
adb devices
```

### 编译错误？
```powershell
dotnet clean
dotnet restore
dotnet build -f net8.0-android
```

### 查看日志
```powershell
adb logcat -s PatrolApp
```

## 📦 生成的文件位置

- **调试版**: `bin\Debug\net8.0-android\`
- **发布版**: `bin\Release\net8.0-android\publish\`

---

详细文档请查看 `VSCODE_BUILD_GUIDE.md`
