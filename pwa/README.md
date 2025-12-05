# 巡更打卡 PWA 版本

轻松软件 - NFC巡更打卡系统 PWA 版

## 功能特性

### 🌐 跨平台支持
- **Android**: 完整支持 Web NFC，可添加到主屏幕
- **iOS**: 支持添加到主屏幕，需手动输入卡号（iOS 不支持 Web NFC）
- **桌面浏览器**: Chrome/Edge 支持 Web NFC

### 📱 核心功能
- NFC 标签读取（Android Chrome 89+）
- 手动输入卡号
- 离线打卡支持
- 本地 IndexedDB 存储
- 后台自动同步
- 语音播报
- 15分钟重复打卡提醒
- 卡点信息缓存

### 💾 离线能力
- Service Worker 缓存静态资源
- IndexedDB 存储打卡记录和卡点信息
- 网络恢复后自动同步

## 部署说明

### 1. 准备图标文件
在 `icons/` 目录下需要以下尺寸的 PNG 图标：
- icon-16.png (16x16)
- icon-32.png (32x32)
- icon-72.png (72x72)
- icon-96.png (96x96)
- icon-128.png (128x128)
- icon-144.png (144x144)
- icon-152.png (152x152)
- icon-180.png (180x180)
- icon-192.png (192x192)
- icon-384.png (384x384)
- icon-512.png (512x512)

可使用 `icons/icon.svg` 作为源文件生成各尺寸 PNG。

### 2. 配置 HTTPS
PWA 必须通过 HTTPS 访问（localhost 除外）。确保服务器配置了有效的 SSL 证书。

### 3. 部署到 Web 服务器
将整个 `pwa/` 目录部署到 Web 服务器，确保：
- `index.html` 作为默认页面
- 正确配置 MIME 类型（特别是 `.json` 和 `.js`）
- 配置正确的缓存策略

### 4. 推荐的 Nginx 配置
```nginx
server {
    listen 443 ssl http2;
    server_name patrol.example.com;
    
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;
    
    root /var/www/patrol-pwa;
    index index.html;
    
    # Service Worker
    location /sw.js {
        add_header Cache-Control "no-cache";
        add_header Service-Worker-Allowed "/";
    }
    
    # Manifest
    location /manifest.json {
        add_header Content-Type "application/manifest+json";
    }
    
    # 静态资源缓存
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
    
    # SPA 路由
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

## 使用说明

### Android 用户
1. 使用 Chrome 浏览器访问 PWA 地址
2. 点击浏览器菜单 → "添加到主屏幕"
3. 或点击页面底部的安装提示条
4. 安装后从主屏幕打开应用
5. 点击 NFC 图标开始扫描
6. 将手机靠近 NFC 标签即可打卡

### iOS 用户
1. 使用 Safari 浏览器访问 PWA 地址
2. 点击分享按钮 → "添加到主屏幕"
3. 安装后从主屏幕打开应用
4. 由于 iOS 不支持 Web NFC，请使用"手动输入卡号"功能

### 离线使用
- 首次访问需要网络，加载完成后可离线使用
- 离线打卡记录会保存到本地
- 网络恢复后自动上传到服务器

## API 接口

PWA 使用与原生 APP 相同的 API 接口：
- `POST /procedure/get_card` - 获取卡点信息
- `POST /procedure/insert_address` - 添加新巡更点
- `POST /procedure/insert_patrol` - 插入打卡记录
- `POST /procedure/get_all_cards` - 获取所有卡点（用于缓存）

## 浏览器兼容性

### Web NFC 支持
| 浏览器 | Android | iOS | Windows |
|--------|---------|-----|---------|
| Chrome | ✅ 89+ | ❌ | ❌ |
| Edge | ✅ 89+ | ❌ | ❌ |
| Samsung Internet | ✅ | - | - |
| Safari | - | ❌ | - |
| Firefox | ❌ | ❌ | ❌ |

### PWA 安装支持
| 平台 | 支持情况 |
|------|----------|
| Android Chrome | ✅ 完全支持 |
| iOS Safari | ✅ 支持（部分功能受限） |
| Windows Chrome/Edge | ✅ 支持 |
| macOS Chrome | ✅ 支持 |

## 与原生 APP 对比

| 功能 | 原生 APP | PWA |
|------|----------|-----|
| NFC 读取 | ✅ | ✅ (仅 Android) |
| 离线打卡 | ✅ | ✅ |
| 语音播报 | ✅ | ✅ |
| 自动更新 | ✅ | ✅ (自动) |
| 安装大小 | ~67MB | ~1MB |
| 安装方式 | APK | 浏览器添加 |
| iOS 支持 | ❌ | ✅ (无 NFC) |

## 目录结构

```
pwa/
├── index.html          # 主页面
├── manifest.json       # PWA 配置
├── sw.js              # Service Worker
├── js/
│   └── app.js         # 主应用逻辑
├── icons/
│   ├── icon.svg       # 源图标
│   ├── icon-192.png   # 各尺寸图标
│   └── ...
└── README.md          # 本文档
```

## 版本历史

### v1.0 (2025-12-05)
- 初始版本
- 基本打卡功能
- NFC 读取（Android）
- 离线支持
- 语音播报
- 15分钟重复打卡限制

## 技术栈

- HTML5 / CSS3 / JavaScript (ES6+)
- Web NFC API
- IndexedDB
- Service Worker
- Web Speech API
- PWA (Progressive Web App)

## 许可证

轻松软件 © 2025
