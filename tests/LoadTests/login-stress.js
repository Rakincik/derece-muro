import http from 'k6/http';
import { check, sleep } from 'k6';

/**
 * MURO Login Stress Test
 * Koşmak için: k6 run LoadTests/login-stress.js
 * 
 * Senaryo: 100 sanal kullanıcı 30 saniye boyunca sürekli login yapar.
 * Beklenti: p95 < 500ms, error rate < 1%
 */

export const options = {
    stages: [
        { duration: '10s', target: 50 },   // 10s'de 50'ye ramp-up
        { duration: '20s', target: 100 },   // 20s'de 100'e çık
        { duration: '10s', target: 0 },     // 10s'de ramp-down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],   // p95 yanıt süresi < 500ms
        http_req_failed: ['rate<0.01'],     // Hata oranı < %1
    },
};

const API_BASE = __ENV.API_URL || 'http://localhost:5000';

export default function () {
    const payload = JSON.stringify({
        email: 'admin@demo.com',
        password: 'Demo@1234',
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const res = http.post(`${API_BASE}/api/v1/auth/login`, payload, params);

    check(res, {
        'status 200': (r) => r.status === 200,
        'has token': (r) => {
            try { return JSON.parse(r.body).token !== undefined; }
            catch { return false; }
        },
        'response time < 500ms': (r) => r.timings.duration < 500,
    });

    sleep(0.5); // 500ms bekleme
}
