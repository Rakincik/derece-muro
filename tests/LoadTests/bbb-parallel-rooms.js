import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend, Gauge } from 'k6/metrics';

/**
 * BBB Paralel Oda Açma Stress Test
 * 50 oda aynı anda açılır → BBB sunucu kaç oda kaldırır?
 * Her odaya 5 kullanıcı join eder (toplam ~250 join)
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/bbb-parallel-rooms.js
 *   tests\LoadTests\k6.exe run tests/LoadTests/bbb-parallel-rooms.js --env API_URL=http://your-server:5000
 */

// ── Custom Metrics ──────────────────────────────────────────────────────────
const roomCreateDuration = new Trend('bbb_room_create_duration', true);
const roomCreateErrors = new Counter('bbb_room_create_errors');
const joinUrlDuration = new Trend('bbb_join_url_duration', true);
const joinErrors = new Counter('bbb_join_errors');
const activeRooms = new Gauge('bbb_active_rooms');
const totalRoomsCreated = new Counter('bbb_total_rooms_created');

export const options = {
    scenarios: {
        // Faz 1: Odaları oluştur (hocalar)
        instructors: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '10s', target: 10 },   // 10 hoca sırayla
                { duration: '20s', target: 50 },   // 50 hoca paralel
                { duration: '30s', target: 50 },   // Hepsi odalarını açık tutuyor
                { duration: '10s', target: 0 },    // Ramp-down
            ],
            exec: 'instructorFlow',
        },
        // Faz 2: Öğrenciler katılır (biraz gecikmeli)
        students: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '15s', target: 0 },    // 15s bekle (odalar açılsın)
                { duration: '15s', target: 100 },  // 100 öğrenci katılmaya başlar
                { duration: '20s', target: 200 },  // 200 öğrenci
                { duration: '10s', target: 0 },    // Ramp-down
            ],
            exec: 'studentFlow',
        },
    },
    thresholds: {
        bbb_room_create_duration: ['p(95)<3000'],  // Oda açma p95 < 3s
        bbb_room_create_errors: ['count<5'],       // Max 5 hata
        bbb_join_url_duration: ['p(95)<1000'],     // Join URL < 1s
        bbb_join_errors: ['count<10'],
        http_req_failed: ['rate<0.05'],
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';

// ── Setup: Admin token al ───────────────────────────────────────────────────
export function setup() {
    const res = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com', password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (res.status !== 200) {
        console.error('Login failed:', res.status, res.body);
        return {};
    }

    const body = JSON.parse(res.body);
    const tenantId = body.user?.tenants?.[0]?.tenantId || '';

    // Mevcut kursları çek
    const headers = {
        'Authorization': `Bearer ${body.token}`,
        'X-Tenant-Id': tenantId,
        'Content-Type': 'application/json',
    };

    const coursesRes = http.get(`${API}/api/v1/courses?pageSize=50`, { headers });
    let courses = [];
    if (coursesRes.status === 200) {
        const parsed = JSON.parse(coursesRes.body);
        courses = parsed.items || parsed || [];
    }

    return {
        token: body.token,
        tenantId,
        courses,
    };
}

// ── Hoca Akışı: Kurs bul → Session bul → Start ─────────────────────────────
export function instructorFlow(data) {
    if (!data.token || !data.courses || data.courses.length === 0) return;

    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    const vuId = __VU;

    group('01_FindCourse', () => {
        const courseIdx = vuId % data.courses.length;
        const course = data.courses[courseIdx];

        // Kurs detayını çek (session listesi için)
        const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
        if (detailRes.status !== 200) return;

        const detail = JSON.parse(detailRes.body);
        const sessions = detail.sessions || [];
        const scheduled = sessions.find(s => s.status === 'Scheduled');

        if (!scheduled) {
            // Session yoksa oluştur
            const createRes = http.post(
                `${API}/api/v1/courses/${course.id}/sessions`,
                JSON.stringify({
                    title: `Stress Session VU${vuId}`,
                    durationMinutes: 30,
                    recordingEnabled: false,
                }),
                { headers }
            );
            check(createRes, { 'session created': (r) => r.status === 201 || r.status === 200 });
            return; // Sonraki iterasyonda start edecek
        }

        sleep(1);

        // Session'ı başlat
        group('02_StartSession', () => {
            const start = Date.now();
            const startRes = http.post(
                `${API}/api/v1/courses/${course.id}/sessions/${scheduled.id}/start`,
                null,
                { headers }
            );
            const duration = Date.now() - start;
            roomCreateDuration.add(duration);

            if (startRes.status === 200) {
                totalRoomsCreated.add(1);
                activeRooms.add(1);
                check(startRes, { 'session started': () => true });
            } else {
                roomCreateErrors.add(1);
                // Zaten Live olabilir — sorun değil
                if (startRes.status !== 409 && startRes.status !== 400) {
                    console.error(`Start failed VU${vuId}: ${startRes.status} ${startRes.body}`);
                }
            }
        });
    });

    // Odada kal
    sleep(Math.random() * 10 + 5);
}

// ── Öğrenci Akışı: Kurs bul → Live session bul → Join ──────────────────────
export function studentFlow(data) {
    if (!data.token || !data.courses || data.courses.length === 0) return;

    const headers = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    group('StudentJoin', () => {
        // Rastgele bir kurs seç
        const courseIdx = Math.floor(Math.random() * data.courses.length);
        const course = data.courses[courseIdx];

        const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
        if (detailRes.status !== 200) return;

        const detail = JSON.parse(detailRes.body);
        const liveSession = (detail.sessions || []).find(s => s.status === 'Live');

        if (!liveSession) {
            sleep(2);
            return; // Henüz live session yok
        }

        const start = Date.now();
        const joinRes = http.post(
            `${API}/api/v1/courses/${course.id}/sessions/${liveSession.id}/join`,
            null,
            { headers }
        );
        joinUrlDuration.add(Date.now() - start);

        if (joinRes.status === 200) {
            check(joinRes, { 'joined session': () => true });
        } else {
            joinErrors.add(1);
        }
    });

    // Derste kal
    sleep(Math.random() * 8 + 3);
}

// ── Teardown: Sonuç özeti ───────────────────────────────────────────────────
export function teardown(data) {
    console.log('=== BBB Parallel Rooms Stress Test Tamamlandı ===');
}
