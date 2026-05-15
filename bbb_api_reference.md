# BigBlueButton API — Tam Parametre Referansı

> **Kaynak:** [BBB Resmi Dokümantasyon](https://docs.bigbluebutton.org/development/api/#create)
> **BBB Sürüm:** 3.0.x | **Tarih:** 27 Şubat 2026
> **Amaç:** MURO LMS canlı ders entegrasyonu için tüm kullanılabilir parametreler

---

## 1. CREATE — Meeting Oluşturma

### 1.1 Temel Parametreler

| Parametre | Zorunlu | Default | Açıklama |
|-----------|---------|---------|----------|
| `name` | ✅ Evet | — | Meeting adı. Öğrencilerin göreceği başlık |
| `meetingID` | ✅ Evet | — | Unique meeting ID. Tekrar create çağrılırsa idempotent çalışır |
| `attendeePW` | ⚠️ Deprecated | Random | Katılımcı şifresi. BBB 3.0'da `role` parametresi ile değiştirildi |
| `moderatorPW` | ⚠️ Deprecated | Random | Moderatör şifresi. BBB 3.0'da `role` parametresi ile değiştirildi |
| `welcome` | Hayır | Config | Giriş mesajı. `%%CONFNAME%%`, `%%DIALNUM%%`, `%%CONFNUM%%` placeholder destekler |
| `dialNumber` | Hayır | Config | Telefon ile bağlanma numarası |
| `voiceBridge` | Hayır | Random | Ses köprüsü numarası (5 haneli) |
| `maxParticipants` | Hayır | `0` (sınırsız) | Max katılımcı sayısı. Sunucu koruması için kritik |
| `logoutURL` | Hayır | Config | Ders bitince kullanıcının yönlendirileceği URL |
| `loginURL` | Hayır | — | Oturum süresi dolduğunda yönlendirme URL'i |

---

### 1.2 Kayıt (Recording) Parametreleri

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `record` | `false` | Kayıt özelliği aktif mi? `true` → kayıt alınır |
| `autoStartRecording` | `false` | `true` → İlk kullanıcı girdiğinde **otomatik kayıt başlar**. Hoca "başlat" butonuna basmak zorunda kalmaz |
| `allowStartStopRecording` | `true` | `false` → Moderatör kayıdı durduramaz/başlatamaz. Otomatik kayıtla birlikte kullanılmalı |
| `recordFullDurationMedia` | `false` | `true` → Kayıt duraklatılsa bile **tüm medya kaydedilir** (boşluklar dahil) |
| `notifyRecordingIsOn` | `false` | `true` → "Bu toplantı kaydediliyor" bildirimi tüm kullanıcılara gösterilir |

> [!TIP]
> **Önerilen kayıt konfigürasyonu:**
> `record=true` + `autoStartRecording=true` + `allowStartStopRecording=false` + `notifyRecordingIsOn=true`
> Bu kombinasyon kayıdın **hiçbir zaman unutulmamasını** garanti eder.

---

### 1.3 Süre & Otomatik Kapanma

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `duration` | `0` (sınırsız) | Meeting max süresi (dakika). Süre dolunca otomatik `end` çağrılır |
| `meetingExpireIfNoUserJoinedInMinutes` | `5` | Kimse girmezse kaç dakika sonra otomatik silinir |
| `meetingExpireWhenLastUserLeftInMinutes` | `1` | Son kişi çıkınca kaç dakika sonra kapanır |
| `endWhenNoModerator` | `false` | `true` → Tüm moderatörler çıkarsa meeting **otomatik biter** |
| `endWhenNoModeratorDelayInMinutes` | `1` | `endWhenNoModerator=true` ile kullanılır. Moderatör çıkınca kaç dk bekle |

> [!IMPORTANT]
> **`endWhenNoModerator=true` + `endWhenNoModeratorDelayInMinutes=5`**
> Hoca çıkarsa 5dk bekler, dönmezse ders otomatik kapanır. Hocanın interneti koptuğunda direkt kapatmaz.

---

### 1.4 Ses & Mikrofon Kontrolü

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `muteOnStart` | `false` | `true` → Tüm kullanıcılar girişte **sessiz**. Büyük sınıflarda şart |
| `allowModsToUnmuteUsers` | `false` | `true` → Moderatör öğrencinin **mikrofonunu açabilir** (soru sormak için) |
| `lockSettingsDisableMic` | `false` | `true` → Öğrenci mikrofonu **kendi başına açamaz**. Moderatör açmalı |

---

### 1.5 Kamera Kontrolü

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `webcamsOnlyForModerator` | `false` | `true` → Sadece moderatör kamera açar. Öğrenci kameraları gizlenir. Bant genişliği tasarrufu |
| `lockSettingsDisableCam` | `false` | `true` → Öğrenci kamera **açamaz** |
| `allowModsToEjectCameras` | `false` | `true` → Moderatör istenmeyen kamerayı **kapatabilir** |
| `userCameraCap` | `3` | Kişi başı max kamera (çoklu kamera desteği) |
| `meetingCameraCap` | `0` (sınırsız) | Toplam açık kamera limiti. `20` → 20'den fazla kamera açılamaz |

---

### 1.6 Chat & İletişim Kilitleri

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `lockSettingsDisablePublicChat` | `false` | `true` → Genel chat kapalı |
| `lockSettingsDisablePrivateChat` | `false` | `true` → Öğrenciler arası **özel mesaj kapalı**. Dikkat dağılmasını önler |
| `lockSettingsDisableNotes` | `false` | `true` → Paylaşılan not defteri kapalı |

---

### 1.7 Layout & Görünüm Kilitleri

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `lockSettingsLockOnJoin` | `true` | Giriş anında tüm kilit ayarları aktif mi |
| `lockSettingsLockOnJoinConfigurable` | `false` | `true` → Her kullanıcı kendi kilit ayarlarını yapılandırabilir |
| `lockSettingsHideUserList` | `false` | `true` → Katılımcı listesi öğrencilerden gizli |
| `lockSettingsHideViewersCursor` | `false` | `true` → Öğrencilerin fare imleci diğerlerine görünmez |
| `lockSettingsLockedLayout` | `false` | `true` → Öğrenci arayüz düzenini değiştiremez |

---

### 1.8 Güvenlik & Misafir Politikası

| Parametre | Default | Seçenekler |
|-----------|---------|-----------|
| `guestPolicy` | `ALWAYS_ACCEPT` | `ALWAYS_ACCEPT` — Herkes direkt girer |
| | | `ASK_MODERATOR` — Misafirler moderatör **onayıyla** girer |
| | | `ALWAYS_DENY` — Misafirler giremez |
| `allowPromoteGuestToModerator` | `false` | `true` → Misafir moderatöre yükseltilebilir |
| `allowRequestsWithoutSession` | `false` | `true` → Session olmadan API istekleri kabul edilir |
| `meetingKeepEvents` | `false` | `true` → Meeting eventleri sunucuda saklanır |

---

### 1.9 Kurumsal Görünüm (Branding)

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `bannerText` | (yok) | Üstte görünen bant yazısı: "MURO Akademi — Fizik 101" |
| `bannerColor` | (yok) | Bant arka plan rengi. Hex format: `#6D28D9` |
| `logo` | (yok) | Sol üstte görünecek kurum logosu URL'i |
| `displayBrandingArea` | `false` | `true` → Logo gösterim alanı aktif |

---

### 1.10 Layout & Arayüz

| Parametre | Default | Seçenekler |
|-----------|---------|-----------|
| `meetingLayout` | `CUSTOM_LAYOUT` | `CUSTOM_LAYOUT` — Kullanıcı seçer |
| | | `SMART_LAYOUT` — BBB otomatik optimize eder |
| | | `PRESENTATION_FOCUS` — Sunum büyük |
| | | `VIDEO_FOCUS` — Video galeri büyük |
| `moderatorOnlyMessage` | (yok) | Sadece moderatörlerin göreceği özel mesaj |
| `multiUserWhiteboardEnabled` | `true` | Çoklu kullanıcı whiteboard |

---

### 1.11 Callback URL'leri (Otomatik Bildirim)

| Parametre | Metod | Açıklama |
|-----------|-------|----------|
| `meta_endCallbackUrl` | GET | Ders bitince bu URL'e GET request atar. `?recordingmarks=true\|false` parametre eklenir |
| `meetingEndedURL` | GET | Ders bitişi callback (server-side, scalelite uyumlu). Client'a propagate edilmez |
| `meta_bbb-recording-ready-url` | POST | Kayıt hazır olunca JWT imzalı POST request. Body: `signed_parameters` (JWT) |
| `meta_analytics-callback-url` | POST | Ders bitince öğrenci engagement verisi POST edilir (JSON) |

> [!CAUTION]
> **`meta_bbb-recording-ready-url`** — Bu en kritik callback! Bunu kullanırsak `BbbRecordingSyncJob`'ın 5dk polling yapmasına gerek kalmaz. BBB kayıt hazır olduğunda **anında** bize bildirir.

#### Recording Ready Callback Detayı

POST body `signed_parameters` adlı JWT token içerir:
- **Header:** `{"typ":"JWT","alg":"HS256"}`
- **Payload:** `{"meeting_id":"...", "record_id":"..."}`
- **Secret:** BBB shared secret ile imzalanır

#### Analytics Callback Detayı — Öğrenci Engagement Verisi

```json
{
  "version": "1.0",
  "meeting_id": "MURO_SessionGuid",
  "data": {
    "duration": 3600,
    "start": "2026-02-27T19:00:00Z",
    "finish": "2026-02-27T20:00:00Z",
    "attendees": [
      {
        "ext_user_id": "öğrenci-guid",
        "name": "Ali Yılmaz",
        "moderator": false,
        "joins": ["2026-02-27T19:01:00Z"],
        "leaves": ["2026-02-27T19:58:00Z"],
        "duration": 3420,
        "engagement": {
          "chats": 12,
          "talks": 3,
          "raisehand": 5,
          "emojis": 8,
          "poll_votes": 2,
          "talk_time": 180
        }
      }
    ],
    "files": ["sunum.pdf"],
    "polls": []
  }
}
```

> [!TIP]
> Bu veri ile **öğrenci engagement dashboard** yapılabilir:
> "Ali dersin %95'ine katıldı, 12 mesaj yazdı, 5 kez el kaldırdı, 3dk konuştu"

---

### 1.12 Metadata Parametreleri

`meta_` öneki ile serbest metadata eklenebilir. `getRecordings` ve callback'lerde geri döner.

| Parametre | Önerilen Kullanım |
|-----------|------------------|
| `meta_sessionId` | MURO Session GUID — kayıt eşleştirme |
| `meta_courseId` | MURO Course GUID |
| `meta_tenantId` | MURO Tenant GUID |
| `meta_Presenter` | Hocanın tam adı |
| `meta_category` | Kurs kategorisi |
| `meta_bbb-anonymize-chat` | `true` → Chat'te isimler anonim |
| `meta_bbb-anonymize-chat-moderators` | `true` → Moderatör isimleri de anonim |

---

### 1.13 Özellik Açma/Kapama (`disabledFeatures`)

Virgülle ayrılmış string: `disabledFeatures=privateChat,virtualBackgrounds`

| Özellik Adı | Açıklama | Öneri |
|------------|----------|------|
| `breakoutRooms` | Breakout odaları | ✅ Açık — grup çalışması |
| `captions` | Altyazılar | ✅ Açık |
| `chat` | Genel + özel chat (tümü) | ✅ Açık |
| `privateChat` | Sadece özel chat | ❌ Kapalı |
| `deleteChatMessage` | Mesaj silme | ✅ Açık |
| `editChatMessage` | Mesaj düzenleme | ✅ Açık |
| `replyChatMessage` | Mesaja yanıt | ✅ Açık |
| `chatMessageReactions` | Mesaj emoji tepki | ✅ Açık |
| `externalVideos` | Harici video paylaşma (YouTube vs.) | ✅ Açık |
| `polls` | Anketler | ✅ Açık |
| `screenshare` | Ekran paylaşma | ✅ Açık |
| `sharedNotes` | Ortak not defteri | ✅ Açık |
| `virtualBackgrounds` | Sanal arka plan | ✅ Açık |
| `customVirtualBackgrounds` | Özel arka plan yükleme | ✅ Açık |
| `liveTranscription` | Canlı transkripsiyon | ✅ Açık |
| `presentation` | Sunum modülü | ✅ Açık |
| `cameraAsContent` | Kamera içerik olarak | ✅ Açık |
| `timer` | Zamanlayıcı | ✅ Açık |
| `infiniteWhiteboard` | Sonsuz whiteboard (3.0) | ✅ Açık |
| `raiseHand` | El kaldırma (3.0) | ✅ Açık |
| `userReactions` | Emoji tepki (3.0) | ✅ Açık |
| `chatEmojiPicker` | Chat emoji seçici (3.0) | ✅ Açık |
| `quizzes` | Ders içi quiz (3.0) | ✅ Açık |
| `learningDashboard` | Öğrenme analitik paneli | ✅ Açık |
| `learningDashboardDownloadSessionData` | Analitik veri indirme | ✅ Açık |
| `downloadPresentationWithAnnotations` | Açıklamalı sunum indirme | ✅ Açık |
| `downloadPresentationOriginalFile` | Orijinal dosya indirme | ✅ Açık |
| `snapshotOfCurrentSlide` | Slayt ekran görüntüsü | ✅ Açık |

> **`disabledFeaturesExclude`** ile `disabledFeatures` listesinden istisnalar tanımlanabilir.

---

### 1.14 Ön Yükleme Slaytları

Meeting oluşturulurken PDF/PPT dosyaları POST body ile gönderilebilir:

```xml
<modules>
  <module name="presentation">
    <document url="https://muro.app/files/ders-notlari.pdf"
              filename="ders-notlari.pdf"
              downloadable="true"
              removable="false"/>
  </module>
</modules>
```

| Özellik | Açıklama |
|---------|----------|
| `url` | Dosyanın URL'i |
| `filename` | Dosya adı (uzantı belirleme için) |
| `downloadable` | Öğrenci indirebilir mi (default: `false`) |
| `removable` | Moderatör silebilir mi (default: `true`) |
| `preUploadedPresentationOverrideDefault` | `false` → BBB default sunum da kalır |
| `preUploadedPresentationName` | Override sunum adı |

---

### 1.15 Client Settings Override (BBB 3.0+)

`allowOverrideClientSettingsOnCreateCall=true` aktifse, POST body ile HTML5 client ayarları geçersiz kılınabilir:

```xml
<modules>
  <module name="clientSettingsOverride">
    <![CDATA[
    {
      "public": {
        "app": {
          "appName": "MURO Akademi",
          "autoJoin": true,
          "askForConfirmationOnLeave": true,
          "defaultSettings": {
            "application": { "overrideLocale": "tr" }
          }
        }
      }
    }
    ]]>
  </module>
</modules>
```

---

### 1.16 Breakout Odaları

| Parametre | Zorunlu | Açıklama |
|-----------|---------|----------|
| `isBreakout` | — | `true` → Bu bir breakout odası |
| `parentMeetingID` | ✅ (breakout) | Ana meeting ID |
| `sequence` | ✅ (breakout) | Oda sırası |
| `freeJoin` | ✅ (breakout) | Kullanıcı istediği odaya geçebilir mi |
| `breakoutRoomsPrivateChatEnabled` | — | Breakout'ta özel chat (default: `true`) |
| `breakoutRoomsRecord` | — | Breakout kayıt (default: `false`) |

---

### 1.17 Ek Parametreler

| Parametre | Default | Açıklama |
|-----------|---------|----------|
| `learningDashboardCleanupDelayInMinutes` | `2` | Learning dashboard verisi kaç dk sonra silinir |
| `maxNumPages` | `200` | Sunum max sayfa sayısı |
| `presentationConversionCacheEnabled` | `false` | S3 tabanlı sunum cache |
| `groups` | (yok) | Kullanıcıları gruplara ayır (JSON array) |

---

## 2. JOIN — Derse Katılma

| Parametre | Zorunlu | Açıklama |
|-----------|---------|----------|
| `fullName` | ✅ Evet | Kullanıcının görünen adı |
| `meetingID` | ✅ Evet | Katılınacak meeting ID |
| `password` | ⚠️ Deprecated | `role` parametresi ile değiştirildi |
| `role` | ✅ Evet | `moderator` veya `viewer` |
| `createTime` | Hayır | Meeting'in createTime değeri (güvenlik — uyuşmazsa red) |
| `userID` | Hayır | Dış sistem kullanıcı ID'si |
| `avatarURL` | Hayır | Kullanıcı profil fotoğrafı URL'i |
| `firstName` | Hayır | Ad (API 3.0+) |
| `lastName` | Hayır | Soyad (API 3.0+) |
| `webcamBackgroundURL` | Hayır | Kamera sanal arka plan URL'i |
| `redirect` | `true` | `false` → XML response döner, redirect yapmaz |
| `errorRedirectUrl` | Hayır | Hata durumunda yönlendirme URL'i |
| `guest` | `false` | `true` → Kullanıcı misafir olarak işaretlenir |
| `bot` | `false` | `true` → Bot kullanıcı olarak işaretlenir |
| `excludeFromDashboard` | `false` | Learning Dashboard'dan hariç tut |
| `enforceLayout` | Hayır | Kullanıcıya özel layout zorlama |

---

## 3. GETRECORDINGS — Kayıtları Listeleme

| Parametre | Açıklama |
|-----------|----------|
| `meetingID` | Virgülle ayrılmış meeting ID'leri |
| `recordID` | Virgülle ayrılmış kayıt ID'leri |
| `state` | Filtreleme: `processing`, `processed`, `published`, `unpublished`, `deleted`, `any` |
| `meta_*` | Metadata filtreleme: `meta_sessionId=xxx` |
| `offset` | Sayfalama başlangıç |
| `limit` | Sayfalama limiti |

### Response İçeriği

```xml
<recording>
  <recordID>abc123</recordID>
  <meetingID>MURO_SessionGuid</meetingID>
  <name>Fizik 101 — Ders 3</name>
  <state>published</state>
  <startTime>1614556800000</startTime>
  <endTime>1614560400000</endTime>
  <duration>3600000</duration>
  <participants>25</participants>
  <playback>
    <format>
      <type>presentation</type>
      <url>https://bbb.server/playback/presentation/2.3/abc123</url>
      <length>60</length>
    </format>
  </playback>
  <metadata>
    <sessionId>guid</sessionId>
    <courseId>guid</courseId>
  </metadata>
</recording>
```

---

## 4. DİĞER API ÇAĞRILARI

| API | Metod | Açıklama |
|-----|-------|----------|
| `isMeetingRunning` | GET | Meeting aktif mi? |
| `end` | GET | Meeting'i zorla bitir |
| `getMeetingInfo` | GET | Meeting detayları (katılımcılar, süre, metadata) |
| `getMeetings` | GET | Tüm aktif meeting'leri listele |
| `publishRecordings` | GET | Kaydı yayınla/geri çek |
| `deleteRecordings` | GET | Kaydı sil |
| `updateRecordings` | POST | Kayıt metadata güncelle |
| `getRecordingTextTracks` | GET | Altyazı parçalarını listele |
| `putRecordingTextTrack` | POST | Altyazı yükle |
| `sendChatMessage` | GET | API üzerinden chat mesajı gönder |
| `insertDocument` | POST | Canlı meeting'e sunum yükle |

---

## 5. MURO İÇİN ÖNERİLEN KONFİGÜRASYON

### appsettings.json Önerisi

```json
{
  "Bbb": {
    "Url": "https://bbb.gikart.com.tr/bigbluebutton/api",
    "Secret": "doLYyYQnIjt7lpJtTdNQkxG93mRKyKHLPf8rBWvE",
    "Defaults": {
      "MuteOnStart": true,
      "AutoStartRecording": true,
      "AllowStartStopRecording": false,
      "RecordFullDurationMedia": true,
      "NotifyRecordingIsOn": true,
      "WebcamsOnlyForModerator": false,
      "AllowModsToUnmuteUsers": true,
      "AllowModsToEjectCameras": true,
      "LockDisableMic": true,
      "LockDisablePrivateChat": true,
      "LockDisablePublicChat": false,
      "LockDisableCam": false,
      "LockOnJoin": true,
      "LockLockedLayout": true,
      "GuestPolicy": "ASK_MODERATOR",
      "EndWhenNoModerator": true,
      "EndWhenNoModeratorDelayMinutes": 5,
      "MaxParticipants": 200,
      "MeetingCameraCap": 20,
      "MeetingLayout": "SMART_LAYOUT",
      "LogoutURL": "https://muro.app/dashboard/live",
      "DisabledFeatures": "privateChat",
      "RecordingReadyCallbackUrl": "https://api.muro.app/api/v1/bbb/webhook/recording-ready",
      "EndCallbackUrl": "https://api.muro.app/api/v1/bbb/webhook/meeting-ended",
      "AnalyticsCallbackUrl": "https://api.muro.app/api/v1/bbb/webhook/analytics"
    }
  }
}
```

### Desteklenen Doküman Türleri (Sunum Yükleme)

PDF, DOC, DOCX, XLS, XLSX, PPT, PPTX, TXT, RTF, ODS, ODP, ODT, JPG, JPEG, PNG

---

## 6. Parametre Sayısı Özeti

| Kategori | Parametre Sayısı |
|----------|-----------------|
| Temel | 10 |
| Kayıt | 5 |
| Süre & Kapanma | 5 |
| Ses & Mikrofon | 3 |
| Kamera | 5 |
| Chat & İletişim | 3 |
| Layout & Kilit | 5 |
| Güvenlik | 4 |
| Branding | 4 |
| Layout & Arayüz | 3 |
| Callback URL'ler | 4 |
| Metadata | 7+ (serbest) |
| Özellik Açma/Kapama | 27 |
| Sunum | 5 |
| Client Override | 2 |
| Breakout | 6 |
| Ek | 4 |
| **JOIN Parametreleri** | 15 |
| **TOPLAM** | **~120+** |
