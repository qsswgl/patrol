// Service Worker for 巡更打卡 PWA
const CACHE_NAME = 'patrol-pwa-v1.0';
const STATIC_CACHE = 'patrol-static-v1.0';
const API_CACHE = 'patrol-api-v1.0';

// 需要缓存的静态资源
const STATIC_ASSETS = [
    '/',
    '/index.html',
    '/manifest.json',
    '/js/app.js',
    '/js/db.js',
    '/js/api.js',
    '/js/nfc.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];

// API 基础 URL
const API_BASE = 'https://tx.qsgl.net:5190/qsoft542/procedure';

// 安装事件 - 缓存静态资源
self.addEventListener('install', event => {
    console.log('[SW] 安装中...');
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(cache => {
                console.log('[SW] 缓存静态资源');
                return cache.addAll(STATIC_ASSETS);
            })
            .then(() => {
                console.log('[SW] 安装完成，跳过等待');
                return self.skipWaiting();
            })
            .catch(err => {
                console.error('[SW] 缓存失败:', err);
            })
    );
});

// 激活事件 - 清理旧缓存
self.addEventListener('activate', event => {
    console.log('[SW] 激活中...');
    event.waitUntil(
        caches.keys()
            .then(cacheNames => {
                return Promise.all(
                    cacheNames
                        .filter(name => {
                            return name.startsWith('patrol-') && 
                                   name !== STATIC_CACHE && 
                                   name !== API_CACHE;
                        })
                        .map(name => {
                            console.log('[SW] 删除旧缓存:', name);
                            return caches.delete(name);
                        })
                );
            })
            .then(() => {
                console.log('[SW] 激活完成，接管所有客户端');
                return self.clients.claim();
            })
    );
});

// 请求拦截 - 网络优先策略（API请求）/ 缓存优先策略（静态资源）
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);
    
    // API 请求 - 网络优先
    if (url.href.includes(API_BASE)) {
        event.respondWith(networkFirst(event.request));
        return;
    }
    
    // 静态资源 - 缓存优先
    event.respondWith(cacheFirst(event.request));
});

// 缓存优先策略
async function cacheFirst(request) {
    try {
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            return cachedResponse;
        }
        
        const networkResponse = await fetch(request);
        
        // 缓存成功的响应
        if (networkResponse.ok) {
            const cache = await caches.open(STATIC_CACHE);
            cache.put(request, networkResponse.clone());
        }
        
        return networkResponse;
    } catch (error) {
        console.error('[SW] 获取资源失败:', error);
        
        // 返回离线页面
        const cachedResponse = await caches.match('/index.html');
        if (cachedResponse) {
            return cachedResponse;
        }
        
        return new Response('离线状态，无法加载资源', {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }
}

// 网络优先策略
async function networkFirst(request) {
    try {
        const networkResponse = await fetch(request);
        
        // 缓存成功的 GET 请求响应
        if (networkResponse.ok && request.method === 'GET') {
            const cache = await caches.open(API_CACHE);
            cache.put(request, networkResponse.clone());
        }
        
        return networkResponse;
    } catch (error) {
        console.log('[SW] 网络请求失败，尝试缓存:', error);
        
        // 尝试从缓存获取
        const cachedResponse = await caches.match(request);
        if (cachedResponse) {
            return cachedResponse;
        }
        
        // 返回离线响应
        return new Response(JSON.stringify({
            success: false,
            message: '网络不可用，请检查网络连接',
            offline: true
        }), {
            status: 503,
            headers: { 'Content-Type': 'application/json' }
        });
    }
}

// 后台同步
self.addEventListener('sync', event => {
    console.log('[SW] 后台同步事件:', event.tag);
    
    if (event.tag === 'sync-patrol-records') {
        event.waitUntil(syncPatrolRecords());
    }
});

// 同步打卡记录
async function syncPatrolRecords() {
    try {
        // 通知客户端开始同步
        const clients = await self.clients.matchAll();
        clients.forEach(client => {
            client.postMessage({
                type: 'sync-start'
            });
        });
        
        // 同步逻辑由客户端处理，这里只是触发
        clients.forEach(client => {
            client.postMessage({
                type: 'sync-records'
            });
        });
        
        console.log('[SW] 后台同步触发成功');
    } catch (error) {
        console.error('[SW] 后台同步失败:', error);
        throw error;
    }
}

// 推送通知
self.addEventListener('push', event => {
    console.log('[SW] 收到推送:', event);
    
    const options = {
        body: event.data ? event.data.text() : '您有新的通知',
        icon: '/icons/icon-192.png',
        badge: '/icons/badge-72.png',
        vibrate: [100, 50, 100],
        data: {
            dateOfArrival: Date.now(),
            primaryKey: 1
        },
        actions: [
            {
                action: 'open',
                title: '打开应用'
            },
            {
                action: 'close',
                title: '关闭'
            }
        ]
    };
    
    event.waitUntil(
        self.registration.showNotification('巡更打卡', options)
    );
});

// 点击通知
self.addEventListener('notificationclick', event => {
    console.log('[SW] 点击通知:', event.action);
    
    event.notification.close();
    
    if (event.action === 'open' || !event.action) {
        event.waitUntil(
            clients.openWindow('/')
        );
    }
});

// 消息处理
self.addEventListener('message', event => {
    console.log('[SW] 收到消息:', event.data);
    
    if (event.data.type === 'skip-waiting') {
        self.skipWaiting();
    }
});
