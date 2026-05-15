import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Trend, Rate } from 'k6/metrics';

/**
 * BBB Webhook Flood Test
 * Senaryo: BBB sunucusu aynı anda çok sayıda webhook event gönderiyor
 * MURO webhook controller'ının yük altında performansını ölçer
 *
 * Event tipleri: user-joined, user-left, meeting-ended, recording-ready
 *
 * Koşmak için:
 *   tests\LoadTests\k6.exe run tests/LoadTests/bbb-webhook-flood.js
 */

const webhookDuration = new Trend('bbb_webhook_duration', true);
const webhookErrors = new Counter('bbb_webhook_errors');
const webhookSuccess = new Rate('bbb_webhook_success_rate');
const eventsProcessed = new Counter('bbb_events_processed');

export const options = {
    scenarios: {
        // Normal webhook akışı
        normal_events: {
            executor: 'constant-arrival-rate',
            rate: 50,              // Saniyede 50 event
            timeUnit: '1s',
            duration: '30s',
            preAllocatedVUs: 20,
            maxVUs: 50,
            exec: 'normalWebhook',
        },
        // Burst: Ders bittiğinde toplu event patlaması
        burst_events: {
            executor: 'ramping-arrival-rate',
            startRate: 0,
            timeUnit: '1s',
            stages: [
                { duration: '35s', target: 0 },    // Normal events bittikten sonra
                { duration: '5s', target: 200 },  // 💥 Saniyede 200 event
                { duration: '10s', target: 200 },  // 10s boyunca
                { duration: '5s', target: 0 },
            ],
            preAllocatedVUs: 50,
            maxVUs: 200,
            exec: 'burstWebhook',
        },
    },
    thresholds: {
        bbb_webhook_duration: ['p(95)<500'],     // Webhook p95 < 500ms
        bbb_webhook_success_rate: ['rate>0.95'],
        bbb_webhook_errors: ['count<20'],
        http_req_failed: ['rate<0.05'],
    },
};

const API = __ENV.API_URL || 'http://localhost:5000';
const WEBHOOK_URL = `${API}/api/v1/bbb/webhook/events`;

// Rastgele GUID üret
function uuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    });
}

// ── Normal Webhook Events ───────────────────────────────────────────────────
export function normalWebhook() {
    const eventTypes = ['user-joined', 'user-left', 'user-joined', 'user-joined'];
    const eventType = eventTypes[Math.floor(Math.random() * eventTypes.length)];

    const payload = {
        events: [{
            eventType,
            meetingId: `tenant_${uuid().slice(0, 8)}`,
            sessionId: uuid(),
            userId: uuid(),
            tenantId: uuid(),
        }],
    };

    const start = Date.now();
    const res = http.post(WEBHOOK_URL, JSON.stringify(payload), {
        headers: { 'Content-Type': 'application/json' },
    });
    webhookDuration.add(Date.now() - start);

    const ok = res.status === 200 || res.status === 400; // 400 = geçersiz session, beklenen
    webhookSuccess.add(ok);
    if (!ok) webhookErrors.add(1);
    if (ok) eventsProcessed.add(1);

    check(res, { 'webhook processed': (r) => r.status < 500 }); // 5xx olmamalı
}

// ── Burst Webhook Events (toplu event patlaması) ────────────────────────────
export function burstWebhook() {
    // Bir meeting bittiğinde gelen toplu eventler: tüm kullanıcılar left + meeting-ended
    const meetingId = `burst_${uuid().slice(0, 8)}`;
    const sessionId = uuid();
    const tenantId = uuid();

    const events = [];

    // 5 kullanıcı left
    for (let i = 0; i < 5; i++) {
        events.push({
            eventType: 'user-left',
            meetingId,
            sessionId,
            userId: uuid(),
            tenantId,
        });
    }

    // meeting-ended
    events.push({
        eventType: 'meeting-ended',
        meetingId,
        sessionId,
        userId: uuid(),
        tenantId,
    });

    const payload = { events };

    const start = Date.now();
    const res = http.post(WEBHOOK_URL, JSON.stringify(payload), {
        headers: { 'Content-Type': 'application/json' },
    });
    webhookDuration.add(Date.now() - start);

    const ok = res.status === 200 || res.status === 400;
    webhookSuccess.add(ok);
    if (!ok) webhookErrors.add(1);
    if (ok) eventsProcessed.add(events.length);

    check(res, {
        'burst processed': (r) => r.status < 500,
        'fast enough': (r) => r.timings.duration < 1000,
    });
}
