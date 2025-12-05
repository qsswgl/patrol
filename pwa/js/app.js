/**
 * 巡更打卡 PWA - 主应用逻辑
 * 轻松软件
 */

// ============= 配置 =============
const CONFIG = {
    VERSION: 'PWA v1.0',
    API_BASE: 'https://tx.qsgl.net:5190/qsoft542/procedure',
    DUPLICATE_CHECK_MINUTES: 15,  // 重复打卡检测时间（分钟）
    DB_NAME: 'PatrolDB',
    DB_VERSION: 1
};

// ============= 全局状态 =============
let db = null;
let isNfcSupported = false;
let isNfcEnabled = false;
let nfcReader = null;
let currentCardNo = null;
let deferredPrompt = null;  // PWA 安装提示
let speechSynthesis = window.speechSynthesis;

// ============= 初始化 =============
document.addEventListener('DOMContentLoaded', async () => {
    console.log('巡更打卡 PWA 初始化...');
    
    // 初始化数据库
    await initDatabase();
    
    // 更新时间显示
    updateTime();
    setInterval(updateTime, 1000);
    
    // 检查 NFC 支持
    checkNfcSupport();
    
    // 加载打卡记录
    await loadRecords();
    
    // 检查网络状态
    updateNetworkStatus();
    window.addEventListener('online', updateNetworkStatus);
    window.addEventListener('offline', updateNetworkStatus);
    
    // 启动时缓存卡点（有网络时）
    if (navigator.onLine) {
        await cacheAllCardPoints();
        await uploadPendingRecords();
    }
    
    // 监听 PWA 安装提示（保存到全局变量，供安装弹窗使用）
    window.addEventListener('beforeinstallprompt', (e) => {
        e.preventDefault();
        window.deferredPrompt = e;
        deferredPrompt = e;
        console.log('PWA 安装事件已捕获');
    });
    
    // 监听 Service Worker 消息
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('message', handleSWMessage);
    }
});

// ============= 时间更新 =============
function updateTime() {
    const now = new Date();
    const timeEl = document.getElementById('currentTime');
    const dateEl = document.getElementById('currentDate');
    
    timeEl.textContent = now.toLocaleTimeString('zh-CN', { hour12: false });
    
    const weekdays = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'];
    dateEl.textContent = `${now.getFullYear()}年${now.getMonth() + 1}月${now.getDate()}日 ${weekdays[now.getDay()]}`;
}

// ============= 网络状态 =============
function updateNetworkStatus() {
    const statusEl = document.getElementById('networkStatus');
    if (navigator.onLine) {
        statusEl.classList.remove('show');
    } else {
        statusEl.classList.add('show');
    }
}

// ============= IndexedDB 数据库 =============
async function initDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(CONFIG.DB_NAME, CONFIG.DB_VERSION);
        
        request.onerror = () => {
            console.error('数据库打开失败');
            reject(request.error);
        };
        
        request.onsuccess = () => {
            db = request.result;
            console.log('数据库打开成功');
            resolve(db);
        };
        
        request.onupgradeneeded = (event) => {
            const database = event.target.result;
            
            // 打卡记录表
            if (!database.objectStoreNames.contains('records')) {
                const recordStore = database.createObjectStore('records', { 
                    keyPath: 'id', 
                    autoIncrement: true 
                });
                recordStore.createIndex('cardNo', 'cardNo', { unique: false });
                recordStore.createIndex('checkInTime', 'checkInTime', { unique: false });
                recordStore.createIndex('isSynced', 'isSynced', { unique: false });
            }
            
            // 卡点缓存表
            if (!database.objectStoreNames.contains('cardPoints')) {
                const cardStore = database.createObjectStore('cardPoints', { 
                    keyPath: 'cardNo' 
                });
                cardStore.createIndex('locationName', 'locationName', { unique: false });
            }
        };
    });
}

// 保存打卡记录
async function saveRecord(record) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['records'], 'readwrite');
        const store = transaction.objectStore('records');
        const request = store.add(record);
        
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

// 获取所有记录
async function getRecords() {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['records'], 'readonly');
        const store = transaction.objectStore('records');
        const request = store.getAll();
        
        request.onsuccess = () => {
            const records = request.result.sort((a, b) => 
                new Date(b.checkInTime) - new Date(a.checkInTime)
            );
            resolve(records);
        };
        request.onerror = () => reject(request.error);
    });
}

// 获取待同步记录
async function getUnsyncedRecords() {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['records'], 'readonly');
        const store = transaction.objectStore('records');
        const index = store.index('isSynced');
        const request = index.getAll(false);
        
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

// 标记记录已同步
async function markRecordSynced(id) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['records'], 'readwrite');
        const store = transaction.objectStore('records');
        const getRequest = store.get(id);
        
        getRequest.onsuccess = () => {
            const record = getRequest.result;
            if (record) {
                record.isSynced = true;
                record.syncedTime = new Date().toISOString();
                const updateRequest = store.put(record);
                updateRequest.onsuccess = () => resolve();
                updateRequest.onerror = () => reject(updateRequest.error);
            }
        };
        getRequest.onerror = () => reject(getRequest.error);
    });
}

// 检查最近打卡记录（15分钟内）
async function getRecentCheckIn(cardNo, minutes = 15) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['records'], 'readonly');
        const store = transaction.objectStore('records');
        const index = store.index('cardNo');
        const request = index.getAll(cardNo);
        
        request.onsuccess = () => {
            const records = request.result;
            const cutoffTime = new Date(Date.now() - minutes * 60 * 1000);
            
            const recentRecord = records.find(r => 
                new Date(r.checkInTime) > cutoffTime
            );
            
            resolve(recentRecord || null);
        };
        request.onerror = () => reject(request.error);
    });
}

// 保存卡点信息
async function saveCardPoint(cardPoint) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['cardPoints'], 'readwrite');
        const store = transaction.objectStore('cardPoints');
        const request = store.put(cardPoint);
        
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

// 获取卡点信息
async function getCardPoint(cardNo) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(['cardPoints'], 'readonly');
        const store = transaction.objectStore('cardPoints');
        const request = store.get(cardNo);
        
        request.onsuccess = () => resolve(request.result || null);
        request.onerror = () => reject(request.error);
    });
}

// ============= NFC 功能 =============
function checkNfcSupport() {
    if ('NDEFReader' in window) {
        isNfcSupported = true;
        document.getElementById('nfcStatus').textContent = '点击开始扫描';
        document.getElementById('nfcHint').textContent = '将手机靠近NFC标签即可打卡';
    } else {
        isNfcSupported = false;
        document.getElementById('nfcStatus').textContent = '设备不支持NFC';
        document.getElementById('nfcHint').textContent = '请使用手动输入卡号功能';
        console.log('此设备不支持 Web NFC');
    }
}

async function handleNfcClick() {
    if (!isNfcSupported) {
        showToast('此设备不支持NFC，请使用手动输入');
        return;
    }
    
    if (isNfcEnabled) {
        stopNfcScan();
    } else {
        await startNfcScan();
    }
}

async function startNfcScan() {
    try {
        nfcReader = new NDEFReader();
        await nfcReader.scan();
        
        isNfcEnabled = true;
        updateNfcUI('scanning');
        speak('NFC扫描已开启');
        
        nfcReader.addEventListener('reading', handleNfcReading);
        nfcReader.addEventListener('readingerror', handleNfcError);
        
    } catch (error) {
        console.error('NFC 扫描失败:', error);
        
        if (error.name === 'NotAllowedError') {
            showToast('请允许NFC权限');
        } else if (error.name === 'NotSupportedError') {
            showToast('设备不支持NFC');
        } else {
            showToast('NFC启动失败: ' + error.message);
        }
        
        updateNfcUI('error');
    }
}

function stopNfcScan() {
    isNfcEnabled = false;
    updateNfcUI('idle');
    speak('NFC扫描已关闭');
}

async function handleNfcReading(event) {
    const { serialNumber } = event;
    
    // 将序列号转换为卡号格式
    const cardNo = serialNumber.replace(/:/g, '-').toUpperCase();
    console.log('读取到NFC卡:', cardNo);
    
    await processCard(cardNo);
}

function handleNfcError(event) {
    console.error('NFC 读取错误:', event);
    showToast('NFC读取失败，请重试');
    updateNfcUI('error');
    
    setTimeout(() => {
        if (isNfcEnabled) {
            updateNfcUI('scanning');
        }
    }, 2000);
}

function updateNfcUI(state) {
    const icon = document.getElementById('nfcIcon');
    const status = document.getElementById('nfcStatus');
    const hint = document.getElementById('nfcHint');
    
    icon.classList.remove('scanning', 'success', 'error');
    
    switch (state) {
        case 'scanning':
            icon.classList.add('scanning');
            status.textContent = '扫描中...';
            hint.textContent = '请将手机靠近NFC标签';
            break;
        case 'success':
            icon.classList.add('success');
            status.textContent = '打卡成功';
            break;
        case 'error':
            icon.classList.add('error');
            status.textContent = '读取失败';
            hint.textContent = '请重试';
            break;
        default:
            status.textContent = '点击开始扫描';
            hint.textContent = '将手机靠近NFC标签即可打卡';
    }
}

// ============= 打卡处理 =============
async function processCard(cardNo) {
    showLoading('正在处理...');
    currentCardNo = cardNo;
    
    try {
        // 检查15分钟内是否已打卡
        const recentRecord = await getRecentCheckIn(cardNo, CONFIG.DUPLICATE_CHECK_MINUTES);
        if (recentRecord) {
            const lastTime = new Date(recentRecord.checkInTime);
            const timeStr = lastTime.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
            
            hideLoading();
            updateNfcUI('success');
            showLocation(`${recentRecord.location} (已打卡)`);
            speak(`${recentRecord.location}在${timeStr}已打卡，无需再打卡`);
            
            setTimeout(() => {
                if (isNfcEnabled) updateNfcUI('scanning');
            }, 3000);
            return;
        }
        
        // 有网络时调用API
        if (navigator.onLine) {
            await processCardOnline(cardNo);
        } else {
            await processCardOffline(cardNo);
        }
        
    } catch (error) {
        console.error('打卡处理失败:', error);
        hideLoading();
        updateNfcUI('error');
        showToast('打卡失败: ' + error.message);
    }
}

async function processCardOnline(cardNo) {
    try {
        // 调用 API 获取卡点信息
        const cardInfo = await apiGetCard(cardNo);
        
        if (!cardInfo || !cardInfo.locationName) {
            // 新卡，需要输入位置
            hideLoading();
            updateNfcUI('idle');
            showNewPointModal();
            return;
        }
        
        // 执行打卡
        await doCheckIn(cardNo, cardInfo.locationName, true);
        
    } catch (error) {
        console.error('在线处理失败:', error);
        // 回退到离线模式
        await processCardOffline(cardNo);
    }
}

async function processCardOffline(cardNo) {
    // 从缓存获取卡点信息
    let locationName = null;
    
    // 1. 先从卡点缓存获取
    const cachedPoint = await getCardPoint(cardNo);
    if (cachedPoint && cachedPoint.locationName) {
        locationName = cachedPoint.locationName;
    } else {
        // 2. 从历史记录获取
        const records = await getRecords();
        const existingRecord = records.find(r => 
            r.cardNo === cardNo && !r.location.startsWith('离线-')
        );
        
        if (existingRecord) {
            locationName = existingRecord.location;
        } else {
            // 3. 使用卡号作为临时标识
            locationName = `离线-${cardNo.substring(0, 8)}`;
        }
    }
    
    // 执行离线打卡
    await doCheckIn(cardNo, locationName, false);
}

async function doCheckIn(cardNo, locationName, isOnline) {
    const record = {
        cardNo: cardNo,
        location: locationName,
        checkInTime: new Date().toISOString(),
        isSynced: false
    };
    
    // 保存到本地数据库
    await saveRecord(record);
    
    // 如果在线，尝试同步到服务器
    if (isOnline) {
        try {
            const success = await apiInsertPatrol(cardNo, locationName);
            if (success) {
                record.isSynced = true;
            }
        } catch (error) {
            console.error('同步失败:', error);
        }
    }
    
    hideLoading();
    updateNfcUI('success');
    showLocation(locationName);
    
    // 语音播报
    if (isOnline) {
        speak(`${locationName}打卡成功`);
    } else {
        if (locationName.startsWith('离线-')) {
            speak('打卡成功，无网暂未上传');
        } else {
            speak(`${locationName}打卡成功，无网暂未上传`);
        }
    }
    
    // 刷新记录列表
    await loadRecords();
    
    // 恢复扫描状态
    setTimeout(() => {
        if (isNfcEnabled) updateNfcUI('scanning');
    }, 3000);
}

// ============= 手动输入 =============
function showManualInput() {
    document.getElementById('manualModal').classList.add('show');
    document.getElementById('cardInput').value = '';
    document.getElementById('cardInput').focus();
}

function hideManualInput() {
    document.getElementById('manualModal').classList.remove('show');
}

async function submitManualCard() {
    const cardNo = document.getElementById('cardInput').value.trim();
    
    if (!cardNo) {
        showToast('请输入卡号');
        return;
    }
    
    hideManualInput();
    await processCard(cardNo.toUpperCase());
}

// ============= 新巡更点 =============
function showNewPointModal() {
    document.getElementById('newPointModal').classList.add('show');
    document.getElementById('locationInput').value = '';
    document.getElementById('locationInput').focus();
    speak('该卡未登记，请输入巡更点位置');
}

function hideNewPointModal() {
    document.getElementById('newPointModal').classList.remove('show');
    speak('已取消添加巡更点');
}

async function submitNewPoint() {
    const locationName = document.getElementById('locationInput').value.trim();
    
    if (!locationName) {
        showToast('请输入巡更点位置');
        return;
    }
    
    hideNewPointModal();
    showLoading('正在添加巡更点...');
    
    try {
        // 调用 API 添加巡更点
        const error = await apiInsertAddress(currentCardNo, locationName);
        
        if (error) {
            hideLoading();
            showToast('添加失败: ' + error);
            speak('添加巡更点失败');
            return;
        }
        
        // 保存到本地缓存
        await saveCardPoint({
            cardNo: currentCardNo,
            locationName: locationName,
            type: '巡更点'
        });
        
        hideLoading();
        showToast('添加成功');
        speak(`添加${locationName}巡更点成功，请重新打卡`);
        
        // 保存添加记录
        await saveRecord({
            cardNo: currentCardNo,
            location: `[新增] ${locationName}`,
            checkInTime: new Date().toISOString(),
            isSynced: true
        });
        
        await loadRecords();
        
    } catch (error) {
        hideLoading();
        showToast('添加失败: ' + error.message);
        speak('添加巡更点失败');
    }
}

// ============= 记录列表 =============
async function loadRecords() {
    try {
        const records = await getRecords();
        const listEl = document.getElementById('recordList');
        const badgeEl = document.getElementById('unsyncedBadge');
        
        // 筛选今日记录
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        
        const todayRecords = records.filter(r => 
            new Date(r.checkInTime) >= today
        );
        
        // 统计待同步
        const unsyncedRecords = await getUnsyncedRecords();
        if (unsyncedRecords.length > 0) {
            badgeEl.textContent = `${unsyncedRecords.length} 待同步`;
            badgeEl.style.display = 'inline';
        } else {
            badgeEl.style.display = 'none';
        }
        
        // 渲染列表
        if (todayRecords.length === 0) {
            listEl.innerHTML = `
                <div class="empty-records">
                    <svg viewBox="0 0 24 24">
                        <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14z"/>
                    </svg>
                    <p>暂无打卡记录</p>
                </div>
            `;
            return;
        }
        
        listEl.innerHTML = todayRecords.slice(0, 20).map(record => {
            const time = new Date(record.checkInTime);
            const timeStr = time.toLocaleTimeString('zh-CN', { 
                hour: '2-digit', 
                minute: '2-digit', 
                second: '2-digit' 
            });
            
            return `
                <div class="record-item">
                    <div class="record-icon">
                        <svg viewBox="0 0 24 24">
                            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                        </svg>
                    </div>
                    <div class="record-info">
                        <div class="record-location">${record.location}</div>
                        <div class="record-time">${timeStr}</div>
                    </div>
                    <span class="record-status ${record.isSynced ? 'synced' : 'pending'}">
                        ${record.isSynced ? '已同步' : '待同步'}
                    </span>
                </div>
            `;
        }).join('');
        
    } catch (error) {
        console.error('加载记录失败:', error);
    }
}

// ============= 同步记录 =============
async function syncRecords() {
    if (!navigator.onLine) {
        showToast('无网络连接');
        return;
    }
    
    const syncBtn = document.getElementById('syncBtn');
    syncBtn.disabled = true;
    syncBtn.textContent = '⏳ 同步中...';
    
    try {
        const unsyncedRecords = await getUnsyncedRecords();
        
        if (unsyncedRecords.length === 0) {
            showToast('没有待同步的记录');
            return;
        }
        
        let successCount = 0;
        
        for (const record of unsyncedRecords) {
            try {
                // 对于离线记录，先查询真实位置名
                let locationName = record.location;
                if (record.location.startsWith('离线-')) {
                    const cardInfo = await apiGetCard(record.cardNo);
                    if (cardInfo && cardInfo.locationName) {
                        locationName = cardInfo.locationName;
                    } else {
                        continue; // 卡未登记，跳过
                    }
                }
                
                const success = await apiInsertPatrol(record.cardNo, locationName);
                if (success) {
                    await markRecordSynced(record.id);
                    successCount++;
                }
            } catch (error) {
                console.error('同步记录失败:', error);
            }
        }
        
        await loadRecords();
        showToast(`已同步 ${successCount}/${unsyncedRecords.length} 条记录`);
        
        if (successCount > 0) {
            speak(`上传了${successCount}条打卡记录`);
        }
        
    } catch (error) {
        showToast('同步失败: ' + error.message);
    } finally {
        syncBtn.disabled = false;
        syncBtn.textContent = '🔄 同步打卡记录';
    }
}

// 启动时上传待同步记录
async function uploadPendingRecords() {
    const unsyncedRecords = await getUnsyncedRecords();
    if (unsyncedRecords.length === 0) return;
    
    console.log(`发现 ${unsyncedRecords.length} 条待同步记录`);
    
    let successCount = 0;
    for (const record of unsyncedRecords) {
        try {
            let locationName = record.location;
            if (record.location.startsWith('离线-')) {
                const cardInfo = await apiGetCard(record.cardNo);
                if (cardInfo && cardInfo.locationName) {
                    locationName = cardInfo.locationName;
                } else {
                    continue;
                }
            }
            
            const success = await apiInsertPatrol(record.cardNo, locationName);
            if (success) {
                await markRecordSynced(record.id);
                successCount++;
            }
        } catch (error) {
            console.error('上传记录失败:', error);
        }
    }
    
    if (successCount > 0) {
        await loadRecords();
        speak(`上传了${successCount}条打卡记录`);
    }
}

// ============= 缓存卡点 =============
async function cacheAllCardPoints() {
    try {
        const cardPoints = await apiGetAllCards();
        
        for (const point of cardPoints) {
            await saveCardPoint({
                cardNo: point.cardNo,
                locationName: point.locationName,
                type: point.type || '巡更点'
            });
        }
        
        console.log(`已缓存 ${cardPoints.length} 个卡点`);
    } catch (error) {
        console.error('缓存卡点失败:', error);
    }
}

// ============= API 接口 =============
async function apiGetCard(cardNo) {
    const response = await fetch(`${CONFIG.API_BASE}/get_card`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ CardNo: cardNo })
    });
    
    if (!response.ok) return null;
    
    const data = await response.json();
    
    if (data.Result === '0' && data.Message) {
        return {
            cardNo: cardNo,
            locationName: data.Message
        };
    }
    
    return null;
}

async function apiInsertAddress(cardNo, locationName) {
    const response = await fetch(`${CONFIG.API_BASE}/insert_address`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ CardNo: cardNo, LocationName: locationName })
    });
    
    if (!response.ok) {
        return `HTTP ${response.status}`;
    }
    
    const data = await response.json();
    
    if (data.Result === '-1') {
        return data.Message || '添加失败';
    }
    
    return null; // 成功返回 null
}

async function apiInsertPatrol(cardNo, locationName) {
    const response = await fetch(`${CONFIG.API_BASE}/insert_patrol`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ CardNo: cardNo, LocationName: locationName })
    });
    
    if (!response.ok) return false;
    
    const data = await response.json();
    return data.Result !== '-1';
}

async function apiGetAllCards() {
    try {
        const response = await fetch(`${CONFIG.API_BASE}/get_all_cards`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: '{}'
        });
        
        if (!response.ok) return [];
        
        const data = await response.json();
        return Array.isArray(data) ? data : [];
    } catch (error) {
        console.error('获取所有卡点失败:', error);
        return [];
    }
}

// ============= UI 辅助函数 =============
function showLocation(location) {
    const el = document.getElementById('locationDisplay');
    el.textContent = location;
    el.classList.add('show');
    
    setTimeout(() => {
        el.classList.remove('show');
    }, 5000);
}

function showToast(message, duration = 2000) {
    const toast = document.getElementById('toast');
    toast.textContent = message;
    toast.classList.add('show');
    
    setTimeout(() => {
        toast.classList.remove('show');
    }, duration);
}

function showLoading(text = '加载中...') {
    const overlay = document.getElementById('loadingOverlay');
    document.getElementById('loadingText').textContent = text;
    overlay.classList.add('show');
}

function hideLoading() {
    document.getElementById('loadingOverlay').classList.remove('show');
}

// ============= 语音播报 =============
function speak(text) {
    if (!speechSynthesis) return;
    
    // 取消之前的语音
    speechSynthesis.cancel();
    
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'zh-CN';
    utterance.rate = 1.0;
    utterance.pitch = 1.0;
    
    speechSynthesis.speak(utterance);
}

// ============= PWA 安装 =============
function showInstallBanner() {
    // 检查是否已安装
    if (window.matchMedia('(display-mode: standalone)').matches) {
        return;
    }
    
    document.getElementById('installBanner').classList.add('show');
}

function hideInstallBanner() {
    document.getElementById('installBanner').classList.remove('show');
}

async function installApp() {
    if (!deferredPrompt) {
        // iOS 设备
        if (/iPhone|iPad|iPod/.test(navigator.userAgent)) {
            showToast('请点击 Safari 分享按钮，选择"添加到主屏幕"');
        }
        return;
    }
    
    deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    
    if (outcome === 'accepted') {
        showToast('应用安装成功！');
    }
    
    deferredPrompt = null;
    hideInstallBanner();
}

// ============= 导航切换 =============
function switchTab(tab) {
    const navItems = document.querySelectorAll('.nav-item');
    navItems.forEach(item => item.classList.remove('active'));
    event.currentTarget.classList.add('active');
    
    // 根据 tab 切换显示内容
    console.log('切换到:', tab);
}

// ============= Service Worker 消息处理 =============
function handleSWMessage(event) {
    console.log('收到 SW 消息:', event.data);
    
    switch (event.data.type) {
        case 'sync-records':
            syncRecords();
            break;
        case 'sync-start':
            showToast('后台同步开始...');
            break;
    }
}

// ============= 注册后台同步 =============
async function registerBackgroundSync() {
    if ('serviceWorker' in navigator && 'sync' in window.registration) {
        try {
            await window.registration.sync.register('sync-patrol-records');
            console.log('后台同步已注册');
        } catch (error) {
            console.error('后台同步注册失败:', error);
        }
    }
}

// 页面可见性变化时触发同步
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && navigator.onLine) {
        uploadPendingRecords();
    }
});
