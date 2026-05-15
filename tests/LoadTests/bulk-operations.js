import http from 'k6/http';
import { check, sleep, group } from 'k6';

/**
 * Admin Toplu İşlem — Bulk Operations
 * Dönem başı: 500 öğrenci import + kurs oluştur + gruplara ata
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/bulk-operations.js
 */

export const options = {
    scenarios: {
        admin_bulk: {
            executor: 'per-vu-iterations',
            vus: 10,           // 10 admin paralel çalışıyor
            iterations: 50,    // Her biri 50 işlem yapar
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<3000'],  // Bulk işlemler 3s altında
        http_req_failed: ['rate<0.02'],
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';

export function setup() {
    const res = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com', password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (res.status !== 200) return {};
    const body = JSON.parse(res.body);
    return { token: body.token, tenantId: body.user?.tenants?.[0]?.tenantId || '' };
}

export default function (data) {
    if (!data.token) return;

    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    const vuId = __VU;
    const iter = __ITER;

    // ── 1. Kullanıcı listesi çek (paginated) ──
    group('01_UserList', () => {
        const page = (iter % 5) + 1;
        const res = http.get(`${API}/api/v1/users?page=${page}&pageSize=20`, { headers });
        check(res, { 'users ok': (r) => r.status === 200 });
    });

    sleep(0.3);

    // ── 2. Grup listesi ──
    group('02_GroupList', () => {
        const res = http.get(`${API}/api/v1/groups`, { headers });
        check(res, { 'groups ok': (r) => r.status === 200 });
    });

    sleep(0.3);

    // ── 3. Kurs oluştur ──
    group('03_CreateCourse', () => {
        const res = http.post(`${API}/api/v1/courses`, JSON.stringify({
            title: `Load Test Kurs VU${vuId}-${iter}`,
            description: 'k6 otomatik oluşturuldu',
            courseType: 'Recorded',
        }), { headers });
        check(res, { 'course created': (r) => r.status === 201 || r.status === 200 });
    });

    sleep(0.3);

    // ── 4. Kurs listesi (filtreleme ile) ──
    group('04_CourseSearch', () => {
        const res = http.get(`${API}/api/v1/courses?search=Load+Test&page=1&pageSize=10`, { headers });
        check(res, { 'search ok': (r) => r.status === 200 });
    });

    sleep(0.5);
}
