import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

/**
 * Multi-Tenant İzolasyon Yük Testi
 * Farklı tenant'lardaki admin'ler aynı anda işlem yapar
 * Cross-tenant data leak kontrolü + tenant-bazlı performans
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/multi-tenant-load.js
 */

const crossTenantErrors = new Counter('cross_tenant_data_leak');
const tenantResponseTime = new Trend('tenant_response_time');

export const options = {
    scenarios: {
        multi_tenant: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10s', target: 30 },
                { duration: '30s', target: 100 },
                { duration: '20s', target: 100 },
                { duration: '10s', target: 0 },
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<1000'],
        http_req_failed: ['rate<0.02'],
        cross_tenant_data_leak: ['count==0'],  // 0 olmalı!
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';

export function setup() {
    // Admin token al
    const res = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com', password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (res.status !== 200) return {};
    const body = JSON.parse(res.body);
    const tenants = body.user?.tenants || [];

    return {
        token: body.token,
        tenantId: tenants[0]?.tenantId || '',
        tenantName: tenants[0]?.tenantName || 'unknown',
    };
}

export default function (data) {
    if (!data.token) return;

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    // ── 1. Kendi tenant'ının kurslarını çek ──
    const start = Date.now();
    const res = http.get(`${API}/api/v1/courses`, { headers });
    tenantResponseTime.add(Date.now() - start);

    check(res, { 'courses ok': (r) => r.status === 200 });

    // ── 2. Sahte tenant ID ile erişim dene → veri gelmemeli ──
    const fakeTenantId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    const fakeHeaders = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': fakeTenantId,
    };

    const fakeRes = http.get(`${API}/api/v1/courses`, { headers: fakeHeaders });

    // Sahte tenant ile veri geliyorsa → data leak!
    if (fakeRes.status === 200) {
        try {
            const body = JSON.parse(fakeRes.body);
            const items = body.items || body || [];
            if (items.length > 0) {
                crossTenantErrors.add(1); // 🚨 DATA LEAK!
                console.error(`CROSS-TENANT LEAK: Fake tenant ${fakeTenantId} returned ${items.length} courses!`);
            }
        } catch { /* ok */ }
    }

    sleep(1);

    // ── 3. Kullanıcı listesi ──
    const usersRes = http.get(`${API}/api/v1/users?page=1&pageSize=10`, { headers });
    check(usersRes, { 'users ok': (r) => r.status === 200 });

    sleep(1);
}
