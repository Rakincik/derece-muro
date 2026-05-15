import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Trend } from 'k6/metrics';

/**
 * Dashboard & Rapor Çekme Yük Testi
 * 30 admin aynı anda yoklama, analitik, kullanıcı listesi çeker
 * Raporlama sorguları genelde en yavaş — index eksikliğini gösterir
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/dashboard-reports.js
 */

const dashboardLoad = new Trend('dashboard_load_time');
const reportLoad = new Trend('report_load_time');

export const options = {
    scenarios: {
        admins: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10s', target: 10 },
                { duration: '30s', target: 30 },
                { duration: '20s', target: 30 },
                { duration: '10s', target: 0 },
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<1500'],
        dashboard_load_time: ['p(95)<1000'],
        report_load_time: ['p(95)<2000'],
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
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    // ── 1. Dashboard: upcoming sessions ──
    group('01_Dashboard', () => {
        const start = Date.now();
        const res = http.get(`${API}/api/v1/sessions/upcoming`, { headers });
        dashboardLoad.add(Date.now() - start);
        check(res, { 'dashboard ok': (r) => r.status === 200 });
    });

    sleep(1);

    // ── 2. Kullanıcı listesi (paginated, farklı sayfalar) ──
    group('02_UserReport', () => {
        const page = Math.floor(Math.random() * 5) + 1;
        const start = Date.now();
        const res = http.get(`${API}/api/v1/users?page=${page}&pageSize=50`, { headers });
        reportLoad.add(Date.now() - start);
        check(res, { 'users ok': (r) => r.status === 200 });
    });

    sleep(1);

    // ── 3. Kurs listesi + detay çekme ──
    group('03_CourseDetail', () => {
        const coursesRes = http.get(`${API}/api/v1/courses?pageSize=5`, { headers });
        check(coursesRes, { 'courses ok': (r) => r.status === 200 });

        if (coursesRes.status === 200) {
            try {
                const items = JSON.parse(coursesRes.body).items || [];
                if (items.length > 0) {
                    const courseId = items[Math.floor(Math.random() * items.length)].id;
                    const start = Date.now();
                    const detailRes = http.get(`${API}/api/v1/courses/${courseId}`, { headers });
                    reportLoad.add(Date.now() - start);
                    check(detailRes, { 'detail ok': (r) => r.status === 200 });
                }
            } catch { /* ignore */ }
        }
    });

    sleep(1);

    // ── 4. Grup listesi ──
    group('04_Groups', () => {
        const res = http.get(`${API}/api/v1/groups`, { headers });
        check(res, { 'groups ok': (r) => r.status === 200 });
    });

    sleep(2);
}
