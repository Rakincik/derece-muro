import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

/**
 * Soak / Endurance Test — Uzun Süreli Yük
 * 50 kullanıcı 10 dakika boyunca sürekli işlem yapar
 * Memory leak, connection pool tükenmesi, DB lock tespiti
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/soak-test.js
 *
 * ÖNEMLİ: Bu test uzun sürer (~12 dakika). Sabırlı ol 😄
 */

const memoryLeakIndicator = new Counter('slow_responses_over_3s');

export const options = {
    scenarios: {
        soak: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '1m', target: 50 },   // Ramp-up
                { duration: '10m', target: 50 },   // 10dk sürekli yük
                { duration: '1m', target: 0 },    // Ramp-down
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<1000'],
        http_req_failed: ['rate<0.01'],       // Uzun sürede bile %1 altı
        slow_responses_over_3s: ['count<20'], // 3s üstü yanıt < 20
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

    // Gerçekçi kullanıcı akışı simülasyonu
    const actions = [
        () => http.get(`${API}/api/v1/courses`, { headers }),
        () => http.get(`${API}/api/v1/courses?page=2&pageSize=10`, { headers }),
        () => http.get(`${API}/api/v1/users?page=1&pageSize=20`, { headers }),
        () => http.get(`${API}/api/v1/groups`, { headers }),
        () => http.get(`${API}/api/v1/sessions/upcoming`, { headers }),
        () => http.get(`${API}/api/v1/courses?search=test`, { headers }),
    ];

    const action = actions[Math.floor(Math.random() * actions.length)];
    const res = action();

    check(res, {
        'status ok': (r) => r.status === 200,
        'under 1s': (r) => r.timings.duration < 1000,
    });

    // Memory leak göstergesi: zamanla yavaşlayan yanıtlar
    if (res.timings.duration > 3000) {
        memoryLeakIndicator.add(1);
    }

    sleep(Math.random() * 2 + 1); // 1-3 saniye arası rastgele bekleme
}
