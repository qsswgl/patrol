/**
 * 生成 PWA 图标脚本
 * 运行方式: node generate-icons.js
 * 需要安装: npm install sharp
 */

const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const sizes = [16, 32, 72, 96, 128, 144, 152, 180, 192, 384, 512];
const iconsDir = path.join(__dirname, 'icons');

// 确保 icons 目录存在
if (!fs.existsSync(iconsDir)) {
    fs.mkdirSync(iconsDir, { recursive: true });
}

// 生成基础图标（如果没有 PNG 源文件，使用 SVG）
const svgPath = path.join(iconsDir, 'icon.svg');
const pngSourcePath = path.join(iconsDir, 'icon-512.png');

async function generateIcons() {
    let sourceFile = pngSourcePath;
    
    // 如果没有 512 PNG，从 SVG 生成
    if (!fs.existsSync(pngSourcePath) && fs.existsSync(svgPath)) {
        console.log('从 SVG 生成 512x512 PNG...');
        await sharp(svgPath)
            .resize(512, 512)
            .png()
            .toFile(pngSourcePath);
        console.log('✓ 已生成 icon-512.png');
    }
    
    if (!fs.existsSync(sourceFile)) {
        sourceFile = svgPath;
        if (!fs.existsSync(sourceFile)) {
            console.error('错误: 找不到源图标文件 (icon.svg 或 icon-512.png)');
            process.exit(1);
        }
    }
    
    console.log(`使用源文件: ${sourceFile}`);
    console.log('开始生成各尺寸图标...\n');
    
    for (const size of sizes) {
        const outputPath = path.join(iconsDir, `icon-${size}.png`);
        
        await sharp(sourceFile)
            .resize(size, size)
            .png()
            .toFile(outputPath);
        
        console.log(`✓ icon-${size}.png (${size}x${size})`);
    }
    
    console.log('\n✅ 所有图标生成完成！');
}

generateIcons().catch(err => {
    console.error('生成失败:', err);
    process.exit(1);
});
