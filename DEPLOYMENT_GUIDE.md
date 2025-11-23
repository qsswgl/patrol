# 巡更打卡应用 - 部署指南

## ✅ 部署状态

应用已成功编译并安装到Android设备!

- **应用名称**: 巡更打卡
- **包名**: com.companyname.patrolapp
- **版本**: 1.0 (Build 1)
- **安装路径**: `/data/app/~~TqPFVIggxw2ikRri3CitZA==/com.companyname.patrolapp-sCz-g0fqTLZeWjb-vmun4w==/base.apk`

## 📱 如何启动应用

### 方法一:从应用抽屉启动(推荐)

1. 在您的Android手机上,打开应用抽屉(App Drawer)
2. 查找名为 **"巡更打卡"** 的应用图标
3. 点击图标即可打开应用

**注意**: .NET MAUI应用无法通过adb命令直接启动,这是正常现象。必须从设备的应用抽屉手动启动。

### 方法二:从设置中启动

如果在应用抽屉中找不到图标:

1. 打开手机 **设置** → **应用** (或应用管理)
2. 查找 **"巡更打卡"** 或搜索 `com.companyname.patrolapp`
3. 点击应用名称
4. 点击 **"打开"** 按钮

### 方法三:重启设备

如果上述方法都找不到应用:

1. 重启手机
2. 重启后应用图标应该会出现在应用抽屉中

## 🔄 后续更新部署

当代码有修改后,使用以下PowerShell命令重新部署:

```powershell
# 方式一:使用构建脚本(推荐)
.\build-android.ps1

# 方式二:手动命令
dotnet build PatrolApp.csproj -f net9.0-android -c Debug
adb uninstall com.companyname.patrolapp
adb install "bin\Debug\net9.0-android\com.companyname.patrolapp-Signed.apk"
```

## ✨ 应用功能

1. **NFC读取**: 
   - 前台读卡:应用运行时靠近NFC标签自动读取
   - 后台读卡:应用最小化或锁屏时也能读取(需要先启动应用一次)

2. **语音播报**: 读取成功后自动语音播报打卡点名称

3. **本地存储**: 所有打卡记录保存在SQLite数据库中,离线可用

4. **自动同步**: 每5分钟自动同步未上传的记录到服务器

5. **记录查看**: 主界面显示所有打卡记录,按时间倒序排列

## 🔧 配置说明

### API服务器地址配置

编辑 `Services/ApiService.cs` 文件,修改第11行:

```csharp
private const string BaseUrl = "https://your-api-endpoint.com";
```

将 `https://your-api-endpoint.com` 替换为实际的API地址。

### NFC标签准备

使用NFC写入工具(如 **NFC Tools** app)在NFC标签上写入NDEF文本记录:

- **记录类型**: Text (文本)
- **内容**: 打卡点名称,例如: `1号岗亭`、`2号岗亭`、`大门` 等

## 🐛 故障排除

### 问题1: 找不到应用图标

**解决方法**:
1. 重启手机
2. 检查是否在工作资料或其他用户配置文件中
3. 在设置→应用中搜索包名 `com.companyname.patrolapp`

### 问题2: NFC无法读取

**检查项**:
1. 确认手机NFC功能已开启(设置→连接→NFC)
2. 确认应用已授予所需权限
3. 确认NFC标签有效且已写入NDEF文本记录
4. 尝试重启应用

### 问题3: 无法同步到服务器

**检查项**:
1. 确认手机有网络连接
2. 确认API服务器地址配置正确
3. 查看应用内的错误提示
4. 检查服务器API是否正常运行

## 📝 开发环境

- **.NET SDK**: 9.0.305
- **框架**: .NET MAUI 9.0
- **目标平台**: Android API 35 (最低 API 21)
- **开发工具**: Visual Studio Code
- **构建工具**: dotnet CLI

## 📞 技术支持

如有问题,请检查以下文件:

- `需求.txt` - 原始需求文档
- `PatrolApp.csproj` - 项目配置
- `.vscode/tasks.json` - VS Code构建任务
- `build-android.ps1` - Android构建脚本

---

**部署时间**: 2025年11月19日  
**部署状态**: ✅ 成功
