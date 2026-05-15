import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter } from 'k6/metrics';

/**
 * Concurrent Session Start — 50 hoca aynı anda ders başlatır
 * BBB API yükünü test eder
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/concurrent-session-start.js
 */

const startErrors = new Counter('session_start_errors');

export const options = {
    scenarios: {
        instructors: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10s', target: 20 },
                { duration: '20s', target: 50 },
                { duration: '10s', target: 0 },
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<2000'],  // BBB API yavaş olabilir — 2s
        http_req_failed: ['rate<0.05'],
        session_start_errors: ['count<5'],
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

    // Kurs listesinden session bul
    group('FindAndStartSession', () => {
        const coursesRes = http.get(`${API}/api/v1/courses?pageSize=5`, { headers });
        check(coursesRes, { 'courses ok': (r) => r.status === 200 });

        if (coursesRes.status === 200) {
            try {
                const courses = JSON.parse(coursesRes.body);
                const items = courses.items || courses;
                if (items && items.length > 0) {
                    const courseId = items[0].id;
                    const detail = http.get(`${API}/api/v1/courses/${courseId}`, { headers });

                    if (detail.status === 200) {
                        const course = JSON.parse(detail.body);
                        const scheduled = (course.sessions || []).find(s => s.status === 'Scheduled');
                        if (scheduled) {
                            const startRes = http.post(
                                `${API}/api/v1/courses/${courseId}/sessions/${scheduled.id}/start`,
                                null, { headers }
                            );
                            if (startRes.status !== 200) startErrors.add(1);
                            check(startRes, { 'start ok': (r) => r.status === 200 });
                        }
                    }
                }
            } catch { /* ignore */ }
        }
    });

    sleep(2);
}
