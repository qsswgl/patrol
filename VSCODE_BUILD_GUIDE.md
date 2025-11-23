# VS Code 中编译和部署 Android 应用指南

## 前置要求

### 1. 安装必要的软件

- **VS Code** (已安装)
- **.NET 8.0 SDK**
  ```powershell
  winget install Microsoft.DotNet.SDK.8
  ```

- **Android SDK** (通过 Visual Studio Installer 或 Android Studio)
  - 打开 Visual Studio Installer
  - 修改安装，勾选 ".NET Multi-platform App UI development"
  - 或者安装 Android Studio 并配置 SDK

### 2. 安装 VS Code 扩展

在 VS Code 中按 `Ctrl+Shift+X` 打开扩展面板，安装：

1. **C# Dev Kit** (ms-dotnettools.csdevkit)
2. **.NET MAUI** (ms-dotnettools.dotnet-maui)
3. **C#** (ms-dotnettools.csharp)

VS Code 会自动提示安装这些扩展（已在 `.vscode/extensions.json` 中配置）。

### 3. 配置环境变量

确保以下环境变量已设置：

```powershell
# 查看当前环境变量
$env:ANDROID_HOME
$env:JAVA_HOME

# 如果未设置，添加环境变量（以管理员身份运行）
[System.Environment]::SetEnvironmentVariable('ANDROID_HOME', 'C:\Program Files (x86)\Android\android-sdk', 'User')
[System.Environment]::SetEnvironmentVariable('JAVA_HOME', 'C:\Program Files\Microsoft\jdk-17.0.11.9-hotspot', 'User')
```

## 编译和部署步骤

### 方法 1: 使用 VS Code 任务（推荐）

1. **打开命令面板**: `Ctrl+Shift+P`

2. **选择任务**:
   - 输入 `Tasks: Run Task`
   - 选择以下任务之一：
     - `android-build` - 编译调试版本
     - `android-run` - 编译并运行到设备/模拟器
     - `android-install` - 安装到连接的设备
     - `android-publish` - 发布 APK

3. **或者使用快捷键**:
   - `Ctrl+Shift+B` - 运行默认构建任务 (android-build)

### 方法 2: 使用集成终端

在 VS Code 中按 `` Ctrl+` `` 打开终端，然后运行：

```powershell
# 1. 清理项目
dotnet clean -f net8.0-android

# 2. 恢复依赖
dotnet restore

# 3. 编译项目
dotnet build -f net8.0-android

# 4. 运行到设备/模拟器
dotnet build -t:Run -f net8.0-android

# 5. 发布 APK (Release 版本)
dotnet publish -f net8.0-android -c Release
```

### 方法 3: 使用脚本快速部署

我已经为您创建了便捷的 PowerShell 脚本，直接运行即可：

```powershell
# 运行构建和部署脚本
.\build-android.ps1
```

## 设备配置

### 连接真实 Android 设备

1. **启用开发者选项**:
   - 进入手机 "设置" > "关于手机"
   - 连续点击 "版本号" 7 次

2. **启用 USB 调试**:
   - "设置" > "开发者选项" > "USB 调试"

3. **连接设备**:
   - 用 USB 线连接手机到电脑
   - 手机上允许 USB 调试授权

4. **验证连接**:
   ```powershell
   adb devices
   ```
   应该能看到您的设备

### 使用 Android 模拟器

1. **列出可用模拟器**:
   ```powershell
   emulator -list-avds
   ```

2. **启动模拟器**:
   ```powershell
   emulator -avd <模拟器名称>
   ```

3. **或使用 Android Studio AVD Manager 启动**

## 常见问题

### 问题 1: 找不到 Android SDK

**解决方案**:
```powershell
# 设置 ANDROID_HOME 环境变量
$env:ANDROID_HOME = "C:\Program Files (x86)\Android\android-sdk"

# 或者在项目中指定
dotnet build -f net8.0-android /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
```

### 问题 2: Java 版本问题

**解决方案**:
- 确保安装了 JDK 11 或更高版本
- 设置 JAVA_HOME 环境变量

```powershell
# 检查 Java 版本
java -version

# 应该显示 11 或更高版本
```

### 问题 3: 未检测到设备

**解决方案**:
```powershell
# 重启 ADB 服务
adb kill-server
adb start-server
adb devices
```

### 问题 4: 编译失败

**解决方案**:
```powershell
# 清理并重新编译
dotnet clean
dotnet restore
dotnet build -f net8.0-android
```

## 调试

### 查看日志

在 VS Code 终端中运行：

```powershell
# 实时查看应用日志
adb logcat -s "PatrolApp"

# 或查看所有日志
adb logcat
```

### 卸载应用

```powershell
adb uninstall com.companyname.patrolapp
```

## 生成签名的 APK

发布到 Google Play 或分发给用户时需要签名：

```powershell
# 生成密钥库（首次）
keytool -genkey -v -keystore patrol.keystore -alias patrol -keyalg RSA -keysize 2048 -validity 10000

# 发布签名的 APK
dotnet publish -f net8.0-android -c Release /p:AndroidKeyStore=true /p:AndroidSigningKeyStore=patrol.keystore /p:AndroidSigningKeyAlias=patrol /p:AndroidSigningKeyPass=你的密码 /p:AndroidSigningStorePass=你的密码
```

编译后的 APK 文件位置：
- Debug: `bin\Debug\net8.0-android\com.companyname.patrolapp-Signed.apk`
- Release: `bin\Release\net8.0-android\publish\com.companyname.patrolapp-Signed.apk`

## 快速开始

1. 在 VS Code 中打开此项目文件夹
2. 等待 VS Code 加载扩展和依赖
3. 连接 Android 设备或启动模拟器
4. 按 `Ctrl+Shift+P`，输入 `Tasks: Run Task`
5. 选择 `android-run`
6. 应用将自动编译并部署到设备

祝您开发顺利！🚀
