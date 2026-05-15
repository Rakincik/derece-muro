# Ortam Değişkenleri

Tüm yapılandırma `appsettings.json` ve environment variable'lar ile yönetilir.
Environment variable'lar `__` (çift alt çizgi) ile nested key'leri temsil eder.

## Zorunlu Değişkenler

| Değişken | Açıklama | Örnek |
|----------|----------|-------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL bağlantı dizesi | `Host=localhost;Port=5432;Database=muro;Username=muro_user;Password=xxx` |
| `ConnectionStrings__Redis` | Redis bağlantı dizesi | `localhost:6379,password=xxx` |
| `Jwt__Secret` | JWT imzalama anahtarı (min 32 karakter) | `MuroV2SuperSecretKey...` |

## JWT Yapılandırması

| Değişken | Varsayılan | Açıklama |
|----------|------------|----------|
| `Jwt__Issuer` | `MURO` | Token veren |
| `Jwt__Audience` | `MURO` | Token hedefi |
| `Jwt__AccessTokenExpiryHours` | `2` | Access token süresi (saat) |
| `Jwt__RefreshTokenExpiryDays` | `7` | Refresh token süresi (gün) |

## BigBlueButton

| Değişken | Açıklama |
|----------|----------|
| `Bbb__Url` | BBB API URL (`https://bbb.example.com/bigbluebutton/api`) |
| `Bbb__Secret` | BBB shared secret |
| `Bbb__WebhookSharedSecret` | Webhook doğrulama anahtarı |
| `Bbb__DefaultAttendeePw` | Varsayılan katılımcı şifresi |
| `Bbb__DefaultModeratorPw` | Varsayılan moderatör şifresi |
| `Bbb__Defaults__LogoutURL` | Ders bitince yönlendirme URL |

## AI Servisleri

| Değişken | Açıklama |
|----------|----------|
| `Gemini__ApiKey` | Google Gemini API anahtarı |
| `EdgeTts__BaseUrl` | Edge TTS servis URL (varsayılan: `http://localhost:5050`) |

## Bildirimler

### E-posta (SMTP)

| Değişken | Açıklama |
|----------|----------|
| `Notifications__Email__Enabled` | `true` / `false` |
| `Notifications__Email__SmtpHost` | SMTP sunucu (ör: `smtp.gmail.com`) |
| `Notifications__Email__SmtpPort` | Port (ör: `587`) |
| `Notifications__Email__Username` | SMTP kullanıcı adı |
| `Notifications__Email__Password` | SMTP şifre |
| `Notifications__Email__FromName` | Gönderen adı |
| `Notifications__Email__FromEmail` | Gönderen e-posta |

### SMS

| Değişken | Açıklama |
|----------|----------|
| `Notifications__Sms__Enabled` | `true` / `false` |
| `Notifications__Sms__Provider` | SMS sağlayıcı (ör: `netgsm`) |
| `Notifications__Sms__Username` | API kullanıcı adı |
| `Notifications__Sms__Password` | API şifre |

## VEP Entegrasyonu

| Değişken | Açıklama |
|----------|----------|
| `Vep__WebhookUrl` | VEP kontrol paneli webhook URL |
| `Vep__WebhookSecret` | Webhook doğrulama anahtarı |

## Diğer

| Değişken | Varsayılan | Açıklama |
|----------|------------|----------|
| `Tenant__BaseDomain` | `localhost` | Multi-tenant domain çözümlemesi |
| `Webhook__Secret` | — | Genel webhook doğrulama anahtarı |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` veya `Production` |

## Ortam Dosyaları

| Dosya | Kullanım |
|-------|----------|
| `appsettings.json` | Varsayılan yapılandırma (geliştirme) |
| `appsettings.Production.json` | Production override'ları |

> ⚠️ **Üretim ortamında** secret'ları asla `appsettings.json`'a yazmayın. Environment variable veya secret manager kullanın.
