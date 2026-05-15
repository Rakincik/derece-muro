import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend } from 'k6/metrics';

/**
 * MURO "Sınav Günü" Senaryosu
 * 300 öğrenci aynı anda: login → dashboard → canlı derse katıl → 30dk kal → çık
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/exam-day.js
 *   tests\LoadTests\k6.exe run tests/LoadTests/exam-day.js --env API_URL=http://192.168.1.x:5000
 */

const joinErrors = new Counter('join_session_errors');
const joinDuration = new Trend('join_session_duration');

export const options = {
    scenarios: {
        exam_day: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '30s', target: 100 },  // İlk dalga
                { duration: '30s', target: 300 },  // Tam doluluk
                { duration: '2m', target: 300 },  // Sınav süresi (kısa demo)
                { duration: '30s', target: 0 },    // Çıkış
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<1000'],  // Yoğun yükte p95 < 1s
        http_req_failed: ['rate<0.05'],     // Hata oranı < %5
        join_session_errors: ['count<10'],  // Session join hataları < 10
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';
const STUDENT_EMAIL = 'student@demo.com';
const STUDENT_PASS = 'Demo@1234';

export default function () {
    let token, tenantId;

    // ── 1. Login ──
    group('01_Login', () => {
        const res = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
            email: STUDENT_EMAIL, password: STUDENT_PASS,
        }), { headers: { 'Content-Type': 'application/json' } });

        check(res, { 'login 200': (r) => r.status === 200 });

        if (res.status === 200) {
            const body = JSON.parse(res.body);
            token = body.token;
            tenantId = body.user?.tenants?.[0]?.tenantId || '';
        }
    });

    if (!token) return;
    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
        'X-Tenant-Id': tenantId,
    };

    sleep(1);

    // ── 2. Dashboard — upcoming sessions ──
    group('02_Dashboard', () => {
        const res = http.get(`${API}/api/v1/sessions/upcoming`, { headers });
        check(res, { 'upcoming 200': (r) => r.status === 200 });
    });

    sleep(1);

    // ── 3. Kurs listesi ──
    group('03_CourseList', () => {
        const res = http.get(`${API}/api/v1/courses`, { headers });
        check(res, { 'courses 200': (r) => r.status === 200 });
    });

    sleep(2);

    // ── 4. Canlı derse katıl (varsa) ──
    group('04_JoinSession', () => {
        // İlk kursun ilk live session'ına katılmayı dene
        const coursesRes = http.get(`${API}/api/v1/courses`, { headers });
        if (coursesRes.status === 200) {
            try {
                const courses = JSON.parse(coursesRes.body);
                const items = courses.items || courses;
                if (items && items.length > 0) {
                    const courseId = items[0].id;
                    const detailRes = http.get(`${API}/api/v1/courses/${courseId}`, { headers });
                    if (detailRes.status === 200) {
                        const detail = JSON.parse(detailRes.body);
                        const liveSession = (detail.sessions || []).find(s => s.status === 'Live');
                        if (liveSession) {
                            const start = Date.now();
                            const joinRes = http.post(
                                `${API}/api/v1/courses/${courseId}/sessions/${liveSession.id}/join`,
                                null, { headers }
                            );
                            joinDuration.add(Date.now() - start);
                            if (joinRes.status !== 200) joinErrors.add(1);
                            check(joinRes, { 'join 200': (r) => r.status === 200 });
                        }
                    }
                }
            } catch { /* parse error */ }
        }
    });

    // ── 5. Derste kalma süresi simülasyonu ──
    sleep(Math.random() * 5 + 3); // 3-8 saniye (gerçekte 30dk)
}
