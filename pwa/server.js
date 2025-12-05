/**
 * PWA 本地测试服务器
 * 运行方式: node server.js
 * 访问地址: http://localhost:8080
 * 
 * 注意: Web NFC 需要 HTTPS，本地测试只能使用手动输入功能
 * 要测试 NFC，需要部署到 HTTPS 服务器
 */

const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 8080;
const ROOT = __dirname;

const MIME_TYPES = {
    '.html': 'text/html',
    '.css': 'text/css',
    '.js': 'application/javascript',
    '.json': 'application/json',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.gif': 'image/gif',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2'
};

const server = http.createServer((req, res) => {
    let filePath = path.join(ROOT, req.url === '/' ? 'index.html' : req.url);
    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';
    
    fs.readFile(filePath, (err, content) => {
        if (err) {
            if (err.code === 'ENOENT') {
                // 文件不存在，返回 index.html (SPA 路由)
                fs.readFile(path.join(ROOT, 'index.html'), (err, content) => {
                    if (err) {
                        res.writeHead(500);
                        res.end('Server Error');
                    } else {
                        res.writeHead(200, { 'Content-Type': 'text/html' });
                        res.end(content);
                    }
                });
            } else {
                res.writeHead(500);
                res.end('Server Error');
            }
        } else {
            // 设置缓存头
            const headers = { 'Content-Type': contentType };
            
            // Service Worker 不缓存
            if (req.url === '/sw.js') {
                headers['Cache-Control'] = 'no-cache';
                headers['Service-Worker-Allowed'] = '/';
            }
            
            res.writeHead(200, headers);
            res.end(content);
        }
    });
});

server.listen(PORT, () => {
    console.log(`
╔════════════════════════════════════════════════╗
║                                                ║
║   巡更打卡 PWA 本地测试服务器                    ║
║                                                ║
║   访问地址: http://localhost:${PORT}             ║
║                                                ║
║   注意:                                        ║
║   - Web NFC 需要 HTTPS 才能使用                 ║
║   - 本地测试请使用"手动输入卡号"功能              ║
║                                                ║
║   按 Ctrl+C 停止服务器                          ║
║                                                ║
╚════════════════════════════════════════════════╝
    `);
});
