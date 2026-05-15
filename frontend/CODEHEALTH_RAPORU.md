# MURO Frontend — Kod Sağlığı Raporu

**Tarih:** 28 Şubat 2026  
**Araç:** [codehealth v1.1.0](https://github.com/EsraMulkpinar/codehealth)  
**Profil:** Next.js (App Router)  
**Taranan Dosya:** 63  
**Etkilenen Dosya:** 44  

---

## Genel Skor

| Metrik | Değer |
|--------|-------|
| **Skor** | 0 / 100 |
| **Errors** | 0 |
| **Warnings** | 392 |
| **Correctness** | 0 sorun |
| **Best Practice** | 77 sorun |
| **Performance** | 22 sorun |
| **Accessibility** | 293 sorun |

> **Not:** Error sıfır olması iyi. Skor düşüklüğü tamamen warning'lerden kaynaklanıyor. Accessibility sorunları sayıyı şişiriyor ama uygulamanın çalışmasını etkilemiyor.

---

## Sorun Detayları

### 1. `a11y-label` — Form elemanında erişilebilirlik etiketi eksik
- **Kategori:** Accessibility
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 154 adet

**Etkilenen dosyalar:**
- `src/app/dashboard/accounting/page.tsx`
- `src/app/dashboard/calendar/page.tsx`
- `src/app/dashboard/courses/page.tsx`
- `src/app/dashboard/assignments/page.tsx`
- `src/app/dashboard/questions/page.tsx`
- `src/app/dashboard/groups/page.tsx`
- `src/app/dashboard/profile/page.tsx`
- +diğer dosyalar

**Sorun:** `<input>` ve `<textarea>` gibi elemanlar `aria-label` veya `<label>` etiketi olmadan kullanılmış.

**Çözüm:**
```tsx
// ❌ Yanlış
<input type="text" placeholder="Ara..." />

// ✅ Doğru — aria-label ekle
<input type="text" placeholder="Ara..." aria-label="Arama kutusu" />

// ✅ Doğru — label etiketi kullan
<label htmlFor="search">Ara</label>
<input id="search" type="text" />
```

---

### 2. `a11y-interactive` — Tıklanabilir elemanda klavye desteği yok
- **Kategori:** Accessibility
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 68 adet

**Etkilenen dosyalar:**
- `src/components/confirm-dialog.tsx` (satır 35, 33)
- `src/components/ui.tsx` (satır 225, 222)
- `src/app/dashboard/accounting/page.tsx` (satır 185, 183, 291, 289)
- `src/app/dashboard/calendar/page.tsx` (satır 191, 179, 313, 305, 342, 362, 387, 490, 488)
- `src/app/dashboard/courses/page.tsx` (satır 628, 889, 887)
- +15 dosya daha

**Sorun:** `onClick` olan `<div>` ve `<span>` gibi elemanlarda `onKeyDown` desteği yok.

**Çözüm:**
```tsx
// ❌ Yanlış
<div onClick={handleClick}>Tıkla</div>

// ✅ Doğru — button kullan (en iyi yöntem)
<button onClick={handleClick}>Tıkla</button>

// ✅ Alternatif — klavye desteği ekle
<div
  role="button"
  tabIndex={0}
  onClick={handleClick}
  onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && handleClick(e)}
>
  Tıkla
</div>
```

---

### 3. `a11y-role` — Olay yöneticisi olan elemanda ARIA role eksik
- **Kategori:** Accessibility
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 70 adet

**Etkilenen dosyalar:** `a11y-interactive` ile aynı dosyalar.

**Çözüm:**
```tsx
// ❌ Yanlış
<div onClick={handler}>İçerik</div>

// ✅ Doğru
<div role="button" tabIndex={0} onClick={handler}>İçerik</div>

// ✅ En iyi — semantik HTML kullan
<button onClick={handler}>İçerik</button>
```

---

### 4. `index-as-key` — Listede index key olarak kullanılmış
- **Kategori:** Best Practice
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 52 adet

**Etkilenen dosyalar:**
- `src/components/ui.tsx` (satır 115, 152)
- `src/app/dashboard/page.tsx` (satır 235, 132, 264)
- `src/app/dashboard/accounting/page.tsx` (satır 51, 123, 550, 561, 631, 681)
- `src/app/dashboard/analytics/page.tsx` (satır 80, 103, 145, 185)
- `src/app/dashboard/calendar/page.tsx` (satır 362, 380, 415, 421, 413, 299, 308, 293)
- +20 dosya daha

**Sorun:** `.map((item, index) => <Component key={index} />)` kullanımı — liste sıralandığında veya filtrelendiğinde yanlış render'a neden olur.

**Çözüm:**
```tsx
// ❌ Yanlış
items.map((item, index) => (
  <Component key={index} {...item} />
))

// ✅ Doğru — benzersiz ID kullan
items.map((item) => (
  <Component key={item.id} {...item} />
))
```

---

### 5. `multiple-usestate` — Çok fazla useState kullanımı
- **Kategori:** Best Practice
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 22 adet

**Etkilenen dosyalar:**
- `src/app/dashboard/page.tsx` (satır 20)
- `src/app/dashboard/accounting/page.tsx` (satır 369)
- `src/app/dashboard/calendar/page.tsx` (satır 51)
- `src/app/dashboard/courses/page.tsx` (satır 70)
- `src/app/dashboard/assignments/page.tsx` (satır 205, 437)
- +14 dosya daha

**Sorun:** Bir bileşende 4+ `useState` çağrısı var — ilişkili state'ler gruplanabilir.

**Çözüm:**
```tsx
// ❌ Yanlış — çok fazla useState
const [name, setName] = useState('');
const [email, setEmail] = useState('');
const [phone, setPhone] = useState('');
const [loading, setLoading] = useState(false);
const [error, setError] = useState(null);

// ✅ Doğru — useReducer veya gruplanmış state
const [formState, setFormState] = useState({
  name: '', email: '', phone: ''
});
const [status, setStatus] = useState({ loading: false, error: null });
```

---

### 6. `img-not-optimized` — Next.js Image bileşeni kullanılmamış
- **Kategori:** Performance
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 12 adet

**Etkilenen dosyalar:**
- `src/app/dashboard/page.tsx` (satır 180)
- `src/app/dashboard/courses/page.tsx` (satır 70)
- `src/app/dashboard/live/page.tsx` (satır 87)
- `src/app/dashboard/profile/page.tsx` (satır 85)
- `src/app/dashboard/questions/page.tsx` (satır 69, 351, 487)
- +3 dosya daha

**Sorun:** Ham `<img>` etiketi kullanılmış — Next.js'in `<Image>` bileşeni otomatik WebP dönüşümü, lazy loading ve boyut optimizasyonu sağlar.

**Çözüm:**
```tsx
// ❌ Yanlış
<img src="/photo.jpg" alt="Fotoğraf" />

// ✅ Doğru
import Image from 'next/image';

<Image src="/photo.jpg" alt="Fotoğraf" width={800} height={600} />

// Boyutu bilinmiyorsa:
<div style={{ position: 'relative', width: '100%', height: 300 }}>
  <Image src="/photo.jpg" alt="..." fill style={{ objectFit: 'cover' }} />
</div>
```

---

### 7. `large-component` — Bileşen çok büyük (300+ satır)
- **Kategori:** Performance
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 10 adet

**Etkilenen dosyalar:**
- `src/app/dashboard/page.tsx` (satır 18)
- `src/app/dashboard/accounting/page.tsx` (satır 365)
- `src/app/dashboard/calendar/page.tsx` (satır 46)
- `src/app/dashboard/courses/page.tsx` (satır 67)
- `src/app/dashboard/groups/page.tsx` (satır 56)
- +5 dosya daha

**Sorun:** 300 satırdan büyük bileşenler okunabilirliği ve bakımı zorlaştırır.

**Çözüm:**
```tsx
// Büyük bileşeni parçala:
function CoursesPageHeader() { /* ... */ }
function CoursesPageFilters() { /* ... */ }
function CoursesPageGrid() { /* ... */ }

// State mantığını custom hook'a taşı:
function useCoursesPageLogic() {
  // useState, useEffect, handler'ları buraya taşı
  return { data, handlers };
}
```

---

### 8. `effect-set-state` — useEffect içinde çoklu setState
- **Kategori:** Best Practice
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 3 adet

**Etkilenen dosyalar:**
- `src/contexts/AuthContext.tsx` (satır 60)
- `src/app/dashboard/page.tsx` (satır 28)
- `src/app/dashboard/courses/[courseId]/page.tsx` (satır 35)

**Çözüm:**
```tsx
// ❌ Yanlış
useEffect(() => {
  setLoading(true);
  setData(result);
  setError(null);
}, [deps]);

// ✅ Doğru — useReducer ile tek dispatch
const [state, dispatch] = useReducer(reducer, initialState);
useEffect(() => {
  dispatch({ type: 'LOADED', payload: result });
}, [deps]);
```

---

### 9. `a11y-autofocus` — autoFocus erişilebilirliği bozuyor
- **Kategori:** Accessibility
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 3 adet

**Etkilenen dosyalar:**
- `src/app/dashboard/courses/page.tsx` (satır 919)
- `src/app/dashboard/questions/page.tsx` (satır 119)
- `src/app/dashboard/videos/[mediaAssetId]/page.tsx` (satır 313)

**Çözüm:**
```tsx
// ❌ Yanlış
<input autoFocus />

// ✅ Doğru — programatik focus
const inputRef = useRef<HTMLInputElement>(null);
useEffect(() => {
  if (isOpen) inputRef.current?.focus();
}, [isOpen]);

<input ref={inputRef} />
```

---

### 10. `effect-as-handler` — useEffect olay yöneticisi olarak kullanılmış
- **Kategori:** Best Practice
- **Ciddiyet:** ⚠ Warning
- **Toplam:** 1 adet

**Etkilenen dosya:**
- `src/app/dashboard/notifications/page.tsx` (satır 10)

**Çözüm:**
```tsx
// ❌ Yanlış
const [flag, setFlag] = useState(false);
useEffect(() => {
  if (flag) { /* bir şey yap */ }
}, [flag]);

// ✅ Doğru — mantığı doğrudan handler'da çalıştır
function handleAction() {
  // yan etkiyi direkt burada yap
}
```

---

## Öncelik Sırası

| Öncelik | Kural | Adet | Neden |
|---------|-------|------|-------|
| 🔴 Yüksek | `index-as-key` | 52 | Hatalı render'a neden olabilir |
| 🔴 Yüksek | `large-component` | 10 | Bakım ve okunabilirlik |
| 🟡 Orta | `img-not-optimized` | 12 | Performans kaybı |
| 🟡 Orta | `multiple-usestate` | 22 | Kod kalitesi |
| 🟡 Orta | `effect-set-state` | 3 | Gereksiz re-render |
| 🟢 Düşük | `a11y-label` | 154 | Erişilebilirlik (işlevselliği etkilemez) |
| 🟢 Düşük | `a11y-interactive` | 68 | Erişilebilirlik |
| 🟢 Düşük | `a11y-role` | 70 | Erişilebilirlik |
| 🟢 Düşük | `a11y-autofocus` | 3 | Erişilebilirlik |
| 🟢 Düşük | `effect-as-handler` | 1 | Küçük sorun |
