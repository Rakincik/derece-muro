# MURO Load Tests — k6

## Koşma Komutları

```powershell
# k6 yolu
$K6 = "tests\LoadTests\k6.exe"

# 1. Login Stress (100 VU)
& $K6 run tests/LoadTests/login-stress.js

# 2. Course List (500 VU)
& $K6 run tests/LoadTests/course-list-load.js

# 3. Sınav Günü (300 VU, 3.5dk)
& $K6 run tests/LoadTests/exam-day.js

# 4. BBB Session Start (50 VU)
& $K6 run tests/LoadTests/concurrent-session-start.js

# 5. Admin Bulk (10 admin, 500 işlem)
& $K6 run tests/LoadTests/bulk-operations.js

# 6. Spike Test (0→500, 20s)
& $K6 run tests/LoadTests/spike-test.js

# 7. Soak Test (50 VU, 12dk ⚠️)
& $K6 run tests/LoadTests/soak-test.js

# 8. Dashboard Raporları (30 VU)
& $K6 run tests/LoadTests/dashboard-reports.js

# 9. Multi-Tenant İzolasyon (100 VU)
& $K6 run tests/LoadTests/multi-tenant-load.js

# ── BBB Stress Tests ──

# 10. BBB Paralel Oda (50 oda, 200 join)
& $K6 run tests/LoadTests/bbb-parallel-rooms.js

# 11. BBB Recording Yük (20 ders, polling)
& $K6 run tests/LoadTests/bbb-recording-load.js

# 12. BBB Endurance (15dk, memory leak tespiti ⚠️)
& $K6 run tests/LoadTests/bbb-endurance.js

# 13. BBB Webhook Flood (200 event/s burst)
& $K6 run tests/LoadTests/bbb-webhook-flood.js

# 14. BBB Tam Ders (10 sınıf × 20 öğrenci)
& $K6 run tests/LoadTests/bbb-full-class.js
```

## Farklı Sunucu Adresi

```powershell
& $K6 run tests/LoadTests/exam-day.js --env API_URL=http://192.168.1.x:5000
```

## Thresholds

| Test | p95 | Error Rate | Özel |
|------|-----|:----------:|------|
| login-stress | <500ms | <1% | — |
| course-list | <500ms | <1% | — |
| exam-day | <1s | <5% | join errors <10 |
| session-start | <2s | <5% | start errors <5 |
| bulk-ops | <3s | <2% | — |
| spike | <2s | <10% | — |
| soak | <1s | <1% | slow >3s <20 |
| dashboard | <1.5s | <2% | report <2s |
| multi-tenant | <1s | <2% | **leak = 0!** |
