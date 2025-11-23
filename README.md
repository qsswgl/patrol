# 巡更打卡 APP

基于 .NET MAUI 开发的跨平台 NFC 巡更打卡应用,支持 Android 和 iOS。

## 功能特性

### ✅ 已实现功能

1. **NFC 读卡功能**
   - 支持前台读卡
   - 支持息屏、锁屏后读卡(Android)
   - 自动识别 NFC 标签 ID
   - 从 NFC 标签读取位置信息

2. **打卡记录**
   - 显示打卡地点
   - 显示打卡时间
   - 读卡后语音播报地点和时间

3. **本地存储**
   - 使用 SQLite 本地数据库
   - 所有打卡记录先存储到本地
   - 支持离线打卡

4. **数据同步**
   - 自动检测网络状态
   - 有网络时自动上传到后端 API
   - 支持手动同步未上传的记录
   - 后台定时自动同步(每5分钟)

5. **用户界面**
   - 实时显示当前时间
   - 显示最近20条打卡记录
   - 显示待同步记录数量
   - 清晰的同步状态标识

## 项目结构

```
PatrolApp/
├── Models/                    # 数据模型
│   └── PatrolRecord.cs       # 巡更打卡记录模型
├── Services/                  # 服务层
│   ├── DatabaseService.cs    # SQLite 数据库服务
│   ├── NfcService.cs         # NFC 服务(跨平台接口)
│   ├── ApiService.cs         # API 调用服务
│   └── TextToSpeechService.cs # 语音播报服务
├── ViewModels/               # 视图模型
│   └── MainViewModel.cs      # 主页面视图模型
├── Views/                    # 视图
│   ├── MainPage.xaml         # 主页面 UI
│   └── MainPage.xaml.cs      # 主页面代码
├── Converters/               # 值转换器
│   └── ValueConverters.cs    # 各种数据转换器
├── Platforms/                # 平台特定代码
│   ├── Android/              # Android 平台
│   │   ├── Services/
│   │   │   └── NfcService.cs # Android NFC 实现
│   │   ├── MainActivity.cs
│   │   ├── MainApplication.cs
│   │   └── AndroidManifest.xml
│   └── iOS/                  # iOS 平台
│       ├── Services/
│       │   └── NfcService.cs # iOS NFC 实现
│       ├── AppDelegate.cs
│       ├── Program.cs
│       └── Info.plist
├── Resources/                # 资源文件
│   └── Styles/              # 样式文件
│       ├── Colors.xaml      # 颜色定义
│       └── Styles.xaml      # 通用样式
├── App.xaml                 # 应用程序定义
├── App.xaml.cs
├── AppShell.xaml            # Shell 导航
├── AppShell.xaml.cs
├── MauiProgram.cs           # 应用启动配置
└── PatrolApp.csproj         # 项目文件
```

## 技术栈

- **.NET 8.0 / .NET MAUI** - 跨平台框架
- **SQLite** - 本地数据库
- **CommunityToolkit.Mvvm** - MVVM 框架
- **NFC (Android & iOS)** - 原生 NFC 功能

### NuGet 包依赖

- Microsoft.Maui.Controls (8.0.90)
- Microsoft.Maui.Controls.Compatibility (8.0.90)
- sqlite-net-pcl (1.9.172)
- SQLitePCLRaw.bundle_green (2.1.8)
- CommunityToolkit.Mvvm (8.2.2)

## 平台要求

### Android
- 最低版本: Android 5.0 (API 21)
- 目标版本: Android 13 (API 33)
- 需要 NFC 硬件支持
- 权限要求:
  - `android.permission.NFC`
  - `android.permission.INTERNET`
  - `android.permission.ACCESS_NETWORK_STATE`
  - `android.permission.WAKE_LOCK`

### iOS
- 最低版本: iOS 11.0
- 需要 NFC 硬件支持 (iPhone 7 及以上)
- 需要在 Info.plist 中配置 NFC 权限
- 注意: iOS 不支持完全后台 NFC 读取

## 后端 API 配置

在 `Services/ApiService.cs` 中修改 API 地址:

```csharp
private const string BaseUrl = "https://your-api-endpoint.com";
```

### API 接口要求

1. **单条记录上传**
   - 端点: `POST /api/patrol/checkin`
   - 请求体:
     ```json
     {
       "location": "位置名称",
       "nfcId": "NFC标签ID",
       "checkInTime": "2024-01-01T12:00:00",
       "localId": 1
     }
     ```

2. **批量记录上传**
   - 端点: `POST /api/patrol/batch-checkin`
   - 请求体: 上述对象的数组

## 编译和运行

### 前置要求

1. 安装 Visual Studio 2022 (17.8 或更高版本)
2. 安装 .NET 8.0 SDK
3. 安装 .NET MAUI 工作负载:
   ```bash
   dotnet workload install maui
   ```

### Android 编译

```bash
# 调试版本
dotnet build -t:Run -f net8.0-android

# 发布版本
dotnet publish -f net8.0-android -c Release
```

### iOS 编译

```bash
# 调试版本
dotnet build -t:Run -f net8.0-ios

# 发布版本
dotnet publish -f net8.0-ios -c Release
```

### 在 Visual Studio 中运行

1. 打开 `PatrolApp.csproj`
2. 选择目标平台 (Android 或 iOS)
3. 选择设备或模拟器
4. 按 F5 运行

## NFC 标签配置

### 写入位置信息到 NFC 标签

为了让 APP 能够读取位置信息,需要将位置名称写入 NFC 标签的 NDEF 记录。

推荐使用 NFC Tools 等应用写入以下格式的文本记录:
- 记录类型: Text (文本)
- 内容: 位置名称,例如 "1号岗亭"、"西门入口" 等

## 使用说明

1. **启动应用**: 打开 APP,自动启用 NFC 监听
2. **打卡**: 将手机靠近 NFC 标签
3. **确认**: 听到语音播报,查看打卡信息
4. **同步**: 有网络时自动同步,或手动点击"立即同步"按钮

### 息屏/锁屏读卡 (Android)

- Android 设备支持在息屏和锁屏状态下读取 NFC 标签
- 读卡后会自动唤醒屏幕并显示打卡信息
- 无需保持应用在前台运行

## 故障排除

### Android NFC 不工作
1. 检查设备是否支持 NFC
2. 确认 NFC 功能已在系统设置中启用
3. 检查应用是否已授予 NFC 权限

### iOS NFC 不工作
1. 确认设备是 iPhone 7 或更新型号
2. 检查 Info.plist 中的 NFC 配置
3. iOS 需要应用在前台才能读取 NFC

### 数据未同步
1. 检查网络连接
2. 验证后端 API 地址配置正确
3. 查看应用日志了解具体错误

## 开发计划

- [ ] 添加用户登录功能
- [ ] 支持多用户管理
- [ ] 添加巡更路线管理
- [ ] 统计报表功能
- [ ] 导出打卡记录
- [ ] 支持蓝牙打卡
- [ ] 支持二维码打卡

## 许可证

本项目仅供学习和参考使用。

## 联系方式

如有问题或建议,请联系开发团队。
