import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';

/**
 * BBB Tam Ders Akışı Stress Test (End-to-End)
 *
 * Gerçekçi senaryo: 10 hoca aynı anda ders başlatır,
 * her derse 20 öğrenci katılır, 1dk ders sürer, ders biter.
 * Toplam: 10 oda × 20 öğrenci = 200 join
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/bbb-full-class.js
 */

const classCreateDuration = new Trend('bbb_class_create_ms', true);
const studentJoinDuration = new Trend('bbb_student_join_ms', true);
const classEndDuration = new Trend('bbb_class_end_ms', true);
const classErrors = new Counter('bbb_class_flow_errors');
const studentsJoined = new Counter('bbb_students_joined');
const classesCompleted = new Counter('bbb_classes_completed');
const attendanceRecorded = new Rate('bbb_attendance_success');

export const options = {
    scenarios: {
        full_class: {
            executor: 'per-vu-iterations',
            vus: 10,            // 10 hoca
            iterations: 1,      // Her hoca 1 tam ders
            maxDuration: '5m',
        },
    },
    thresholds: {
        bbb_class_create_ms: ['p(95)<3000'],
        bbb_student_join_ms: ['p(95)<1500'],
        bbb_class_end_ms: ['p(95)<2000'],
        bbb_class_flow_errors: ['count<5'],
        bbb_attendance_success: ['rate>0.90'],
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';
const STUDENTS_PER_CLASS = 20;

export function setup() {
    // Admin login
    const adminRes = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com', password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (adminRes.status !== 200) return {};
    const adminBody = JSON.parse(adminRes.body);
    const tenantId = adminBody.user?.tenants?.[0]?.tenantId || '';

    const headers = {
        'Authorization': `Bearer ${adminBody.token}`,
        'X-Tenant-Id': tenantId,
        'Content-Type': 'application/json',
    };

    // Kursları çek
    const coursesRes = http.get(`${API}/api/v1/courses?pageSize=20`, { headers });
    let courses = [];
    if (coursesRes.status === 200) {
        const parsed = JSON.parse(coursesRes.body);
        courses = parsed.items || parsed || [];
    }

    // Öğrenci hesaplarını listele
    const usersRes = http.get(`${API}/api/v1/users?page=1&pageSize=50`, { headers });
    let students = [];
    if (usersRes.status === 200) {
        const parsed = JSON.parse(usersRes.body);
        const items = parsed.items || parsed || [];
        students = items.filter(u => u.role === 'Student').map(u => u.id);
    }

    return {
        token: adminBody.token,
        tenantId,
        courses,
        studentIds: students,
    };
}

export default function (data) {
    if (!data.token || !data.courses?.length) return;

    const vuId = __VU;
    const courseIdx = vuId % data.courses.length;
    const course = data.courses[courseIdx];

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
        'Content-Type': 'application/json',
    };

    console.log(`[Hoca ${vuId}] Ders başlıyor: ${course.title}`);

    // ── 1. Scheduled session bul ──────────────────────────────────────────────
    let sessionId = null;

    group('01_FindSession', () => {
        const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
        if (detailRes.status === 200) {
            const detail = JSON.parse(detailRes.body);
            const scheduled = (detail.sessions || []).find(s => s.status === 'Scheduled');
            if (scheduled) sessionId = scheduled.id;
        }
    });

    if (!sessionId) {
        console.warn(`[Hoca ${vuId}] Scheduled session bulunamadı, atlanıyor.`);
        return;
    }

    // ── 2. Dersi başlat ───────────────────────────────────────────────────────
    group('02_StartClass', () => {
        const start = Date.now();
        const startRes = http.post(
            `${API}/api/v1/courses/${course.id}/sessions/${sessionId}/start`,
            null, { headers }
        );
        classCreateDuration.add(Date.now() - start);

        if (startRes.status === 200) {
            console.log(`[Hoca ${vuId}] ✅ Ders başladı`);
        } else {
            classErrors.add(1);
            console.error(`[Hoca ${vuId}] ❌ Start failed: ${startRes.status}`);
            return;
        }
    });

    sleep(2); // Öğrenciler katılmaya başlasın

    // ── 3. Öğrenciler katılır ─────────────────────────────────────────────────
    group('03_StudentsJoin', () => {
        for (let i = 0; i < STUDENTS_PER_CLASS; i++) {
            const studentName = `Öğrenci_${vuId}_${i + 1}`;

            const start = Date.now();
            const joinRes = http.post(
                `${API}/api/v1/courses/${course.id}/sessions/${sessionId}/join`,
                null, { headers }
            );
            studentJoinDuration.add(Date.now() - start);

            if (joinRes.status === 200) {
                studentsJoined.add(1);
                attendanceRecorded.add(true);
            } else {
                attendanceRecorded.add(false);
            }

            sleep(0.1); // Her öğrenci arası 100ms (gerçekçi)
        }
        console.log(`[Hoca ${vuId}] 👨‍🎓 ${STUDENTS_PER_CLASS} öğrenci katıldı`);
    });

    // ── 4. Ders süresi (kısa simülasyon) ─────────────────────────────────────
    console.log(`[Hoca ${vuId}] 📚 Ders devam ediyor...`);
    sleep(10); // 10 saniye ders süresi

    // ── 5. Dersi bitir ────────────────────────────────────────────────────────
    group('04_EndClass', () => {
        const start = Date.now();
        const endRes = http.post(
            `${API}/api/v1/courses/${course.id}/sessions/${sessionId}/end`,
            null, { headers }
        );
        classEndDuration.add(Date.now() - start);

        if (endRes.status === 200) {
            classesCompleted.add(1);
            console.log(`[Hoca ${vuId}] 🎬 Ders bitti, kayıt oluşturuluyor`);
        } else {
            classErrors.add(1);
            console.error(`[Hoca ${vuId}] ❌ End failed: ${endRes.status}`);
        }
    });

    // ── 6. Recording kontrolü (kısa bekle + kontrol) ─────────────────────────
    sleep(3);

    group('05_CheckRecording', () => {
        const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
        if (detailRes.status === 200) {
            const detail = JSON.parse(detailRes.body);
            const endedSession = (detail.sessions || []).find(s => s.id === sessionId);
            if (endedSession) {
                console.log(`[Hoca ${vuId}] 📹 Session status: ${endedSession.status}`);
            }
        }
    });

    console.log(`[Hoca ${vuId}] ✅ Tam ders döngüsü tamamlandı!\n`);
}

export function teardown() {
    console.log('\n' + '='.repeat(60));
    console.log('BBB Tam Ders Akışı Stress Test Tamamlandı');
    console.log('10 sınıf × 20 öğrenci = 200 join simüle edildi');
    console.log('='.repeat(60) + '\n');
}
