import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';

/**
 * BBB Kayıt (Recording) Yük Testi
 * Senaryo: 20 ders bitti → getRecordings sürekli çağrılıyor
 * BBB kayıt işleme sırası şişiyor mu? API yanıt süreleri artıyor mu?
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/bbb-recording-load.js
 */

const getRecordingsDuration = new Trend('bbb_get_recordings_duration', true);
const recordingApiErrors = new Counter('bbb_recording_api_errors');
const recordingsFound = new Counter('bbb_recordings_found');
const recordingReadyRate = new Rate('bbb_recording_ready_rate');

export const options = {
    scenarios: {
        // Faz 1: Dersler başlat → bitir (recording oluştur)
        session_lifecycle: {
            executor: 'per-vu-iterations',
            vus: 10,
            iterations: 2,       // Her VU 2 ders başlatıp bitirir = 20 ders
            exec: 'sessionLifecycle',
            maxDuration: '2m',
        },
        // Faz 2: Recording API'yi sürekli sorgula (polling simülasyonu)
        recording_polling: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '30s', target: 0 },    // İlk 30s bekle (dersler bitsin)
                { duration: '10s', target: 20 },   // 20 worker polling başlasın
                { duration: '60s', target: 20 },   // 1dk sürekli polling
                { duration: '10s', target: 0 },
            ],
            exec: 'recordingPolling',
        },
    },
    thresholds: {
        bbb_get_recordings_duration: ['p(95)<2000'],
        bbb_recording_api_errors: ['count<10'],
        http_req_failed: ['rate<0.05'],
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';

export function setup() {
    const res = http.post(`${API}/api/v1/auth/login`, JSON.stringify({
        email: 'admin@demo.com', password: 'Demo@1234',
    }), { headers: { 'Content-Type': 'application/json' } });

    if (res.status !== 200) return {};
    const body = JSON.parse(res.body);
    const tenantId = body.user?.tenants?.[0]?.tenantId || '';

    const headers = {
        'Authorization': `Bearer ${body.token}`,
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

    return { token: body.token, tenantId, courses };
}

// ── Faz 1: Ders başlat → kısa bekle → bitir (kayıt oluşsun) ───────────────
export function sessionLifecycle(data) {
    if (!data.token || !data.courses?.length) return;

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
        'Content-Type': 'application/json',
    };

    const vuId = __VU;
    const iter = __ITER;
    const courseIdx = (vuId + iter) % data.courses.length;
    const course = data.courses[courseIdx];

    group('01_FindScheduledSession', () => {
        const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
        if (detailRes.status !== 200) return;

        const detail = JSON.parse(detailRes.body);
        const scheduled = (detail.sessions || []).find(s => s.status === 'Scheduled');
        if (!scheduled) return;

        // Başlat
        group('02_StartSession', () => {
            const startRes = http.post(
                `${API}/api/v1/courses/${course.id}/sessions/${scheduled.id}/start`,
                null, { headers }
            );
            check(startRes, { 'started': (r) => r.status === 200 });
        });

        // Kısa ders süresi simülasyonu
        sleep(5);

        // Bitir (recording oluşsun)
        group('03_EndSession', () => {
            const endRes = http.post(
                `${API}/api/v1/courses/${course.id}/sessions/${scheduled.id}/end`,
                null, { headers }
            );
            check(endRes, { 'ended': (r) => r.status === 200 });
        });
    });

    sleep(2);
}

// ── Faz 2: Recording API'yi polling et ──────────────────────────────────────
export function recordingPolling(data) {
    if (!data.token || !data.courses?.length) return;

    const headers = {
        'Authorization': `Bearer ${data.token}`,
        'X-Tenant-Id': data.tenantId,
    };

    group('RecordingPolling', () => {
        // Tüm kursların session'larını tara
        const courseIdx = Math.floor(Math.random() * data.courses.length);
        const course = data.courses[courseIdx];

        const detailRes = http.get(`${API}/api/v1/courses/${course.id}`, { headers });
        if (detailRes.status !== 200) return;

        const detail = JSON.parse(detailRes.body);
        const endedSessions = (detail.sessions || []).filter(s => s.status === 'Ended');

        // Her ended session için recording durumunu kontrol et
        for (const session of endedSessions.slice(0, 3)) { // max 3 tane
            const start = Date.now();

            // Recordings endpoint'i (eğer varsa)
            // Yoksa session detayından recording bilgisi çek
            const recRes = http.get(
                `${API}/api/v1/courses/${course.id}/sessions/${session.id}`,
                { headers }
            );
            getRecordingsDuration.add(Date.now() - start);

            if (recRes.status === 200) {
                try {
                    const recData = JSON.parse(recRes.body);
                    if (recData.recordingUrl || recData.recording) {
                        recordingsFound.add(1);
                        recordingReadyRate.add(true);
                    } else {
                        recordingReadyRate.add(false);
                    }
                } catch { /* ignore */ }
            } else {
                recordingApiErrors.add(1);
            }
        }
    });

    sleep(3); // 3 saniyede bir polling (BbbRecordingSyncJob 5dk, biz daha agresif)
}
