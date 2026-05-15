import http from 'k6/http';
import { check, sleep } from 'k6';

/**
 * Spike Test — Ani Yük Patlaması
 * 0'dan 500'e 5 saniyede çık → 10 saniye tut → 0'a düşür
 * Sunucu ani yükte crash oluyor mu?
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/spike-test.js
 */

export const options = {
    scenarios: {
        spike: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '5s', target: 500 },  // 💥 5 saniyede 500'e çık
                { duration: '10s', target: 500 },  // 🔥 10 saniye tut
                { duration: '5s', target: 0 },    // ⬇️ Aniden düşür
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<2000'],  // Spike'ta bile p95 < 2s
        http_req_failed: ['rate<0.10'],     // Spike'ta %10 hata kabul
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

    // Rastgele endpoint'lere istek at — gerçek kullanım simülasyonu
    const endpoints = [
        '/api/v1/courses',
        '/api/v1/users?page=1&pageSize=10',
        '/api/v1/groups',
        '/api/v1/sessions/upcoming',
    ];

    const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
    const res = http.get(`${API}${endpoint}`, { headers });

    check(res, {
        'not 5xx': (r) => r.status < 500,
        'response time < 2s': (r) => r.timings.duration < 2000,
    });

    sleep(0.1); // Minimum bekleme — maksimum yük
}
