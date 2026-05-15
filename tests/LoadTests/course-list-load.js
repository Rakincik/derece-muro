import http from 'k6/http';
import { check, sleep } from 'k6';

/**
 * MURO Course List Load Test
 * Koşmak için: k6 run LoadTests/course-list-load.js
 * 
 * Senaryo: 500 sanal kullanıcı authenticated olarak GET /courses isteği atar.
 * Beklenti: p95 < 500ms, error rate < 1%
 */

export const options = {
    stages: [
        { duration: '10s', target: 100 },   // Ramp-up
        { duration: '20s', target: 500 },   // Peak load
        { duration: '10s', target: 0 },     // Ramp-down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],
        http_req_failed: ['rate<0.01'],
    },
};

const API_BASE = __ENV.API_URL || 'http://localhost:5000';
let authToken = '';

// Test başlamadan önce bir kez login ol
export function setup() {
    const loginRes = http.post(`${API_BASE}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com',
        password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    const body = JSON.parse(loginRes.body);
    return { token: body.token, tenantId: body.user?.tenants?.[0]?.tenantId || '' };
}

export default function (data) {
    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${data.token}`,
            'X-Tenant-Id': data.tenantId,
        },
    };

    const res = http.get(`${API_BASE}/api/v1/courses`, params);

    check(res, {
        'status 200': (r) => r.status === 200,
        'response time < 500ms': (r) => r.timings.duration < 500,
        'has items': (r) => {
            try { return JSON.parse(r.body).items !== undefined; }
            catch { return false; }
        },
    });

    sleep(0.3);
}
