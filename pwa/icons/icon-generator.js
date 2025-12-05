/**
 * 简单的 PWA 图标生成器
 * 使用 Canvas 生成 PNG 图标
 * 在浏览器控制台运行此代码可以下载图标
 */

function generateIcon(size) {
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d');
    
    // 背景渐变
    const gradient = ctx.createLinearGradient(0, 0, size, size);
    gradient.addColorStop(0, '#6B46C1');
    gradient.addColorStop(1, '#553C9A');
    
    // 圆角矩形背景
    const radius = size * 0.2;
    ctx.beginPath();
    ctx.moveTo(radius, 0);
    ctx.lineTo(size - radius, 0);
    ctx.quadraticCurveTo(size, 0, size, radius);
    ctx.lineTo(size, size - radius);
    ctx.quadraticCurveTo(size, size, size - radius, size);
    ctx.lineTo(radius, size);
    ctx.quadraticCurveTo(0, size, 0, size - radius);
    ctx.lineTo(0, radius);
    ctx.quadraticCurveTo(0, 0, radius, 0);
    ctx.closePath();
    ctx.fillStyle = gradient;
    ctx.fill();
    
    // NFC 图标
    ctx.strokeStyle = 'white';
    ctx.lineWidth = size * 0.04;
    ctx.lineCap = 'round';
    
    const centerX = size * 0.45;
    const centerY = size * 0.45;
    
    // 外圈
    ctx.beginPath();
    ctx.arc(centerX, centerY, size * 0.3, 0, Math.PI * 2);
    ctx.stroke();
    
    // 中圈
    ctx.beginPath();
    ctx.arc(centerX, centerY, size * 0.2, 0, Math.PI * 2);
    ctx.stroke();
    
    // 内圈
    ctx.fillStyle = 'white';
    ctx.beginPath();
    ctx.arc(centerX, centerY, size * 0.08, 0, Math.PI * 2);
    ctx.fill();
    
    // 打勾标记
    ctx.beginPath();
    ctx.moveTo(size * 0.55, size * 0.65);
    ctx.lineTo(size * 0.65, size * 0.75);
    ctx.lineTo(size * 0.85, size * 0.55);
    ctx.strokeStyle = 'white';
    ctx.lineWidth = size * 0.05;
    ctx.stroke();
    
    return canvas.toDataURL('image/png');
}

// 下载图标
function downloadIcon(size) {
    const dataUrl = generateIcon(size);
    const link = document.createElement('a');
    link.download = `icon-${size}.png`;
    link.href = dataUrl;
    link.click();
}

// 生成所有尺寸
function downloadAllIcons() {
    const sizes = [16, 32, 72, 96, 128, 144, 152, 180, 192, 384, 512];
    sizes.forEach((size, index) => {
        setTimeout(() => downloadIcon(size), index * 500);
    });
}

// 在控制台运行: downloadAllIcons()
console.log('PWA 图标生成器已加载');
console.log('运行 downloadAllIcons() 下载所有尺寸图标');
console.log('运行 downloadIcon(512) 下载指定尺寸图标');
