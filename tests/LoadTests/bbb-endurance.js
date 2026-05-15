import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend, Gauge, Rate } from 'k6/metrics';

/**
 * BBB Sunucu Sağlık & Dayanıklılık Testi
 * 15 dakika boyunca sürekli BBB sağlık kontrolü + session lifecycle döngüsü
 * Memory leak, connection pool, WebSocket degradation tespiti
 *
 * ⚠️ Bu test uzun sürer (~17 dakika)
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/bbb-endurance.js
 */

// ── Custom Metrics ──────────────────────────────────────────────────────────
const healthCheckDuration = new Trend('bbb_health_check_duration', true);
const healthCheckSuccess = new Rate('bbb_health_check_success');
const sessionStartDuration = new Trend('bbb_session_start_duration', true);
const sessionEndDuration = new Trend('bbb_session_end_duration', true);
const degradedResponses = new Counter('bbb_degraded_responses_over_2s');
const totalCycles = new Counter('bbb_lifecycle_cycles_completed');

// Zaman bazlı performans takibi
const minute1_latency = new Trend('bbb_minute_01_latency', true);
const minute5_latency = new Trend('bbb_minute_05_latency', true);
const minute10_latency = new Trend('bbb_minute_10_latency', true);
const minute15_latency = new Trend('bbb_minute_15_latency', true);

export const options = {
    scenarios: {
        // Sürekli sağlık kontrolü (düşük yük, uzun süre)
        health_monitor: {
            executor: 'constant-vus',
            vus: 5,
            duration: '15m',
            exec: 'healthMonitor',
        },
        // Periyodik session lifecycle (orta yük)
        lifecycle: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '1m', target: 10 },   // 10 paralel lifecycle
                { duration: '13m', target: 10 },   // 13dk sürekli
                { duration: '1m', target: 0 },    // Ramp-down
            ],
            exec: 'sessionLifecycle',
        },
        // Artan yük (son 5 dk'da yükü artır — degradation tespiti)
        increasing_load: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10m', target: 0 },    // İlk 10dk sabit
                { duration: '2m', target: 30 },   // Son 5dk'da yükü artır
                { duration: '2m', target: 50 },
                { duration: '1m', target: 0 },
            ],
            exec: 'heavyLoad',
        },
    },
    thresholds: {
        bbb_health_check_duration: ['p(95)<1000'],
        bbb_health_check_success: ['rate>0.95'],       // %95 üstü sağlıklı
        bbb_session_start_duration: ['p(95)<3000'],
        bbb_degraded_responses_over_2s: ['count<30'],  // 15dk'da max 30 yavaş yanıt
        http_req_failed: ['rate<0.05'],
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';
const testStartTime = Date.now();

export function setup() {
    const res = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com', password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (res.status !== 200) return {};
    const body = JSON.parse(res.body);
    const tenantId = body.user?.tenants?.[0]?.tenantId || '';

    const headers = {
        'Authorization': `Bearer ${body.token}`,
        'X-Tenant-Id': tenantId,
        'Content-Type': 'application/json',
    };

    const coursesRes = http.get(`${API}/api/v1/courses?pageSize=20`, { headers });
    let courses = [];
    if (coursesRes.status === 200) {
        const parsed = JSON.parse(coursesRes.body);
        courses = parsed.items || parsed || [];
    }

    return { token: body.token, tenantId, courses };
}

// ── Sağlık Kontrolü ─────────────────────────────────────────────────────────
export function healthMonitor(data) {
    if (!data.token) return;

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    const endpoints = [
        '/api/v1/courses',
        '/api/v1/sessions/upcoming',
        '/api/v1/groups',
    ];

    const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
    const start = Date.now();
    const res = http.get(`${API}${endpoint}`, { headers });
    const duration = Date.now() - start;

    healthCheckDuration.add(duration);
    healthCheckSuccess.add(res.status === 200);

    // Zaman bazlı metrik — performans zamanla kötüleşiyor mu?
    const elapsedMinutes = (Date.now() - testStartTime) / 60000;
    if (elapsedMinutes <= 1) minute1_latency.add(duration);
    else if (elapsedMinutes <= 5) minute5_latency.add(duration);
    else if (elapsedMinutes <= 10) minute10_latency.add(duration);
    else minute15_latency.add(duration);

    if (duration > 2000) {
        degradedResponses.add(1);
        console.warn(`⚠️ Degraded response: ${endpoint} → ${duration}ms at minute ${Math.floor(elapsedMinutes)}`);
    }

    check(res, {
        'health ok': (r) => r.status === 200,
        'under 1s': (r) => r.timings.duration < 1000,
    });

    sleep(5); // 5 saniyede bir health check
}

// ── Session Lifecycle Döngüsü ───────────────────────────────────────────────
export function sessionLifecycle(data) {
    if (!data.token || !data.courses?.length) return;

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
        'Content-Type': 'application/json',
    };

    const courseIdx = __VU % data.courses.length;
    const course = data.courses[courseIdx];

    // Kurs detayı çek
    const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
    if (detailRes.status !== 200) { sleep(5); return; }

    const detail = JSON.parse(detailRes.body);
    const scheduled = (detail.sessions || []).find(s => s.status === 'Scheduled');
    if (!scheduled) { sleep(5); return; }

    // Start
    const startBegin = Date.now();
    const startRes = http.post(
        `${API}/api/v1/courses/${course.id}/sessions/${scheduled.id}/start`,
        null, { headers }
    );
    sessionStartDuration.add(Date.now() - startBegin);
    check(startRes, { 'started': (r) => r.status === 200 });

    // Derste kal (kısa)
    sleep(Math.random() * 5 + 3);

    // End
    const endBegin = Date.now();
    const endRes = http.post(
        `${API}/api/v1/courses/${course.id}/sessions/${scheduled.id}/end`,
        null, { headers }
    );
    sessionEndDuration.add(Date.now() - endBegin);
    check(endRes, { 'ended': (r) => r.status === 200 });

    totalCycles.add(1);
    sleep(10); // Sonraki döngü öncesi bekle
}

// ── Artan Yük (Son 5dk) ─────────────────────────────────────────────────────
export function heavyLoad(data) {
    if (!data.token) return;

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    // Ağır sorgular — DB'ye yük bindir
    const queries = [
        '/api/v1/courses?page=1&pageSize=50',
        '/api/v1/users?page=1&pageSize=50',
        '/api/v1/courses?search=test&page=1&pageSize=20',
        '/api/v1/groups',
        '/api/v1/sessions/upcoming',
    ];

    for (const q of queries) {
        const res = http.get(`${API}${q}`, { headers });
        check(res, { 'heavy ok': (r) => r.status === 200 });
    }

    sleep(1);
}

// ── Teardown ────────────────────────────────────────────────────────────────
export function teardown(data) {
    const totalMinutes = Math.floor((Date.now() - testStartTime) / 60000);
    console.log(`\n${'='.repeat(60)}`);
    console.log(`BBB Endurance Test Tamamlandı — ${totalMinutes} dakika`);
    console.log(`Önemli: minute_01 vs minute_15 latency karşılaştır!`);
    console.log(`Eğer minute_15 >> minute_01 ise → memory leak olabilir!`);
    console.log(`${'='.repeat(60)}\n`);
}
