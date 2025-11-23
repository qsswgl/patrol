# 一键编译和部署脚本
# 使用方法: .\build-android.ps1

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  巡更打卡 APP - Android 构建脚本" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 检查 .NET SDK
Write-Host "检查 .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "错误: 未找到 .NET SDK，请先安装 .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}
Write-Host "✓ .NET SDK 版本: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# 检查 Android SDK
Write-Host "检查 Android SDK..." -ForegroundColor Yellow
if (-not $env:ANDROID_HOME) {
    Write-Host "警告: ANDROID_HOME 环境变量未设置" -ForegroundColor Yellow
    Write-Host "尝试使用默认路径..." -ForegroundColor Yellow
    $env:ANDROID_HOME = "C:\Program Files (x86)\Android\android-sdk"
}
Write-Host "✓ Android SDK: $env:ANDROID_HOME" -ForegroundColor Green
Write-Host ""

# 检查连接的设备
Write-Host "检查 Android 设备..." -ForegroundColor Yellow
$devices = adb devices
if ($LASTEXITCODE -ne 0) {
    Write-Host "错误: ADB 未找到，请确保 Android SDK 已正确安装" -ForegroundColor Red
    exit 1
}

$deviceCount = ($devices | Select-String "device$" | Measure-Object).Count
if ($deviceCount -eq 0) {
    Write-Host "警告: 未检测到 Android 设备或模拟器" -ForegroundColor Yellow
    Write-Host "请连接设备或启动模拟器后继续..." -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "是否继续构建? (y/n)"
    if ($continue -ne "y") {
        exit 0
    }
} else {
    Write-Host "✓ 检测到 $deviceCount 个设备" -ForegroundColor Green
    Write-Host $devices -ForegroundColor Gray
}
Write-Host ""

# 选择构建模式
Write-Host "请选择构建模式:" -ForegroundColor Cyan
Write-Host "1. Debug (调试版本 - 快速)"
Write-Host "2. Release (发布版本 - 优化)"
Write-Host "3. 只编译不部署"
Write-Host "4. 生成 APK 文件"
$choice = Read-Host "请输入选项 (1-4)"

$config = "Debug"
$task = "Run"

switch ($choice) {
    "1" { 
        $config = "Debug"
        $task = "Run"
        Write-Host "选择: Debug 模式 + 部署" -ForegroundColor Green
    }
    "2" { 
        $config = "Release"
        $task = "Run"
        Write-Host "选择: Release 模式 + 部署" -ForegroundColor Green
    }
    "3" { 
        $config = "Debug"
        $task = "Build"
        Write-Host "选择: 仅编译" -ForegroundColor Green
    }
    "4" { 
        $config = "Release"
        $task = "Publish"
        Write-Host "选择: 生成 APK" -ForegroundColor Green
    }
    default { 
        Write-Host "无效选项，使用默认: Debug + 部署" -ForegroundColor Yellow
        $config = "Debug"
        $task = "Run"
    }
}
Write-Host ""

# 清理
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "步骤 1/4: 清理项目" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
dotnet clean PatrolApp.csproj -f net9.0-android
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ 清理失败" -ForegroundColor Red
    exit 1
}
Write-Host "✓ 清理完成" -ForegroundColor Green
Write-Host ""

# 恢复依赖
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "步骤 2/4: 恢复 NuGet 包" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ 恢复依赖失败" -ForegroundColor Red
    exit 1
}
Write-Host "✓ 依赖恢复完成" -ForegroundColor Green
Write-Host ""

# 编译
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "步骤 3/4: 编译项目" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

if ($task -eq "Publish") {
    # 发布模式
    dotnet publish PatrolApp.csproj -f net9.0-android -c $config /p:AndroidPackageFormat=apk
} else {
    # 普通编译
    dotnet build PatrolApp.csproj -f net9.0-android -c $config
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ 编译失败" -ForegroundColor Red
    exit 1
}
Write-Host "✓ 编译完成" -ForegroundColor Green
Write-Host ""

# 部署/发布
if ($task -eq "Run") {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "步骤 4/4: 部署到设备" -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    
    dotnet build PatrolApp.csproj -t:Run -f net9.0-android -c $config
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ 部署失败" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ 部署完成" -ForegroundColor Green
    Write-Host ""
    Write-Host "应用已启动! 请查看您的 Android 设备。" -ForegroundColor Green
    
} elseif ($task -eq "Publish") {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "步骤 4/4: 查找生成的 APK" -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    
    $apkPath = "bin\Release\net8.0-android\publish\*.apk"
    $apkFiles = Get-ChildItem -Path $apkPath -ErrorAction SilentlyContinue
    
    if ($apkFiles) {
        Write-Host "✓ APK 文件已生成:" -ForegroundColor Green
        foreach ($apk in $apkFiles) {
            Write-Host "  - $($apk.FullName)" -ForegroundColor Cyan
            Write-Host "    大小: $([math]::Round($apk.Length / 1MB, 2)) MB" -ForegroundColor Gray
        }
    } else {
        Write-Host "✗ 未找到 APK 文件" -ForegroundColor Red
    }
} else {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "步骤 4/4: 编译完成" -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "✓ 仅编译完成，未部署到设备" -ForegroundColor Green
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  构建完成！" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 显示日志选项
if ($task -eq "Run") {
    Write-Host "提示: 要查看应用日志，请运行:" -ForegroundColor Yellow
    Write-Host "  adb logcat -s PatrolApp" -ForegroundColor Cyan
    Write-Host ""
    
    $viewLogs = Read-Host "是否现在查看日志? (y/n)"
    if ($viewLogs -eq "y") {
        Write-Host "按 Ctrl+C 停止查看日志" -ForegroundColor Yellow
        adb logcat -s PatrolApp
    }
}
