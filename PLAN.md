# FENER — Geliştirme Planı

**Jam:** LightBear Uplifting Game Jam #9 · Tema: OCEAN
**Teslim:** 14 Eylül 2026, 20:01
**Bugün:** 5 Eylül 2026 → **kalan ~9,5 gün**
**Motor:** Unity + WebGL · **Kodcu:** Rumeysa (tek) · **Sanat:** tasarımcı (dışarıdan)

---

## 0. Plana başlamadan: iki uyarı

**1. Konsept dokümanı 11 gün varsayıyor, elimizde 9,5 gün var.**
Doküman yazıldığında ~11 gün vardı. Aşağıdaki takvim 9,5 güne göre yeniden sıkıştırıldı; dokümandaki gün numaralarıyla birebir örtüşmüyor.

**2. 10–12 gece × 2–3 dakika = 20–36 dakika oyun. Bu bir jam için fazla.**
Jüri ortalama 5–8 dakika oynayıp oy veriyor. 12 gece yaparsan jürinin çoğu şafağı — yani oyunun duygusal finalini — hiç görmez. Ve o final, "uplift" kriterinin tamamı.

**Önerim: 6 gece, her biri ~60–90 saniye, toplam ~8 dakika.** Zorluk eğrisini aynı tut, sadece daha hızlı tırman. Herkes sonu görsün. Kalan gece fikirleri jam sonrası sürüme kalsın.

Bu senin kararın ama geciktirilmesi pahalı bir karar — gece sayısı `NightConfig` sayısını, sanat ihtiyacını ve müzik uzunluğunu belirliyor. Bugün karar ver.

---

## 1. Mimari

Sistemleri birbirinden ayrık tut. Her biri tek bir soruya cevap versin.

### Scriptler

| Script | Sorumluluk |
|---|---|
| `LighthouseBeam.cs` | Fare pozisyonu → huzme açısı. `CurrentTarget` (huzmedeki gemi) property'sini dışarı verir. |
| `Signal.cs` | Veri tipi. `List<SignalSymbol>`, `enum SignalSymbol { Short, Long }`, eşitlik karşılaştırması. MonoBehaviour değil. |
| `SignalInput.cs` | Sol/sağ tık → çubuğa sembol ekler. 2 sn zaman aşımı, 5. sembolde boşaltma. `OnSymbolAdded` / `OnBarCleared` event'leri. |
| `SignalBar.cs` | Çubuğun UI'ı. 4 yuva, dolma animasyonu, yeşil parlama, sönme. Mantık içermez, sadece dinler. |
| `Ship.cs` | Durum makinesi: `Approaching → Signaling → Bound → Docking → Arrived`. Kendi `Signal`'ını, tipini, hedef rıhtımını taşır. |
| `ShipSpawner.cs` | Aktif `NightConfig`'e göre gemi doğurur. |
| `NightConfig.cs` | **ScriptableObject.** Gece başına ayar. Kod yazmadan tuning yapabilmen için kritik. |
| `NightManager.cs` | Gece sırası, gece bitişi, gece→şafak ilerlemesi. |
| `Harbor.cs` | Trigger. Gelen gemiyi kabul eder, `OnShipArrived(ship)` yayınlar. |
| `TownLights.cs` | Varışları sayar, kasabada bir pencere daha yakar. Kalıcı iz. |
| `NoteDisplay.cs` | Varışta 2 cümlelik notu gösterir. |
| `AudioDirector.cs` | Huzme vınlaması, tık sesleri, varış flaşı, müzik katmanları. |
| `GameDirector.cs` | Üst akış: başlık ekranı → geceler → şafak → son ekran. |

### Kritik: eşleştirme mantığı nerede yaşıyor?

`GameDirector` değil, ayrı bir `MatchResolver` içinde. Tek iş yapsın:

```
SignalInput.OnSymbolAdded  →
  target = LighthouseBeam.CurrentTarget
  if (target == null) return;                 // huzmede gemi yok, güzel ışık atışı, başka bir şey yok
  if (bar.Count < target.Signal.Count) return; // henüz karşılaştırma zamanı değil
  if (bar == target.Signal) → target.Bind()
  else → bar.Clear(), target.ReplaySignal()
```

Bu blok oyunun kalbi. Başka hiçbir yere eşleştirme kodu yazma.

### Huzme hedefleme

Fizik kullanma, gereksiz. Açı hesabı yeterli:

- Huzme yönü ile gemi yönü arasındaki açı `< coneAngle / 2` ise gemi konide.
- Konide birden fazla gemi varsa **açısı en küçük olanı** seç. Tek hedef.
- Hedeflenen gemiyi belli et: hafif parlama + vınlama sesi. Doküman buna "hedeflendi hissi" diyor, oyunun okunabilirliği buna bağlı.
- **Koniyi geniş tut.** Dokümanda özellikle yazılmış. Dar koni oyunu bir nişan alma oyununa çevirir, oysa bu bir hafıza oyunu.

### Karar vermen gereken iki kural (doküman söylemiyor)

**a) Çubuk doluyken huzmeyi başka gemiye çevirirsen ne olur?**
Öneri: **çubuk korunur**, yeni geminin uzunluğuna göre karşılaştırılır. Kazara doğru eşleşme olursa bu bir hediyedir, ceza yok felsefesine uyar. Çubuğu silmek cezalandırıcı hissettirir.

**b) Bağlı gemi huzmeden çıkarsa?**
Öneri: **bağlı kalır.** Doküman "huzme artık onun rotasıdır" diyor — yani gemi son verilen yönü izler, sürekli huzme altında tutmak gerekmez. Aksi halde iki gemiyi aynı anda yönetmek imkânsız olur ve 5. maddedeki gerilim çalışmaz.

### Bağlı geminin dönüşü

```
hedefAci = huzmeAcisi
mevcutAci = Mathf.MoveTowardsAngle(mevcutAci, hedefAci, donusHizi * Time.deltaTime)
```

`donusHizi` gemi tipine göre değişir — kayık hızlı, yük gemisi ağır. `Lerp` değil `MoveTowardsAngle` kullan; Lerp sonsuza kadar yaklaşır, asla varmaz, "sürüklenme" hissi verir.

### NightConfig alanları

```
int  geceNo
List<ShipSpawn> gemiler      // { gemiTipi, sekansUzunlugu, dogumZamani, hedefRihtim }
int  esZamanliMaxGemi
bool sekansSurekliGorunur    // gece 1-2: true
bool sisAktif                // gece 5+
bool ikinciRihtimAktif
```

Her gece bir asset dosyası. Editörden dengeleyebilirsin, build almadan.

---

## 2. WebGL tuzakları — bunları gün 10'da değil gün 1'de çöz

Bunlar jam'leri batıran şeyler. Hepsi bilinen, hepsi önceden çözülür.

**Sağ tık tarayıcı menüsünü açar.**
Oyunun kontrolünün yarısı sağ tık. Varsayılan WebGL şablonunda sağ tıklayınca tarayıcının bağlam menüsü açılır ve oyun kullanılamaz hale gelir. Kendi WebGL şablonunu oluştur, `index.html` içine:

```js
canvas.addEventListener('contextmenu', e => e.preventDefault());
```

**Bunu bugün test et.** Editörde çalışır, tarayıcıda çalışmaz — geç fark edersen felaket.

**Ses ilk tıklamaya kadar çalmaz.**
Tarayıcılar kullanıcı etkileşimi olmadan sesi engeller. Çözüm zaten tasarımda var: bir başlık ekranı koy, "Başla" butonuna basılınca oyun ve ses birlikte başlasın.

**Build ayarları (Player Settings):**
- Compression Format: **Brotli**
- Managed Stripping Level: **High**
- Code Optimization: **Size** (Master build)
- Exceptions: **None** veya Explicitly Thrown Only
- Sprite'lar: **Crunch Compression** açık, kalite ~50
- Müzik: Vorbis + **Streaming**; kısa efektler: **Decompress On Load**
- Canvas: 1280×720 (1920 gereksiz, dosya büyütür)

**itch.io'ya gün 2'de "draft" olarak yükle ve her gün güncelle.** Teslim gününde ilk kez yükleme yapma. Frame boyutunu 1280×720 ayarla, "Click to launch in fullscreen" işaretle.

---

## 3. Git kurulumu (Unity'ye özel)

Repo hazır ama Unity için iki ayar şart, yoksa proje git'te bozulur:

**Edit → Project Settings → Editor:**
- Version Control → Mode: **Visible Meta Files**
- Asset Serialization → Mode: **Force Text**

**`.gitignore` kontrolü.** GitHub'da seçtiğin şablon Unity ise sorun yok. Değilse şunlar mutlaka ignore edilmeli: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`.

**`.meta` dosyalarını commit et.** Bunlar ignore edilirse Unity tüm referansları kaybeder.

**Git LFS'e girme.** Tek kodcusun, asset'ler PNG, jam 9 gün. LFS kurulum sürtünmesi kazandırdığından fazlasını götürür.

**Akış (solo için basit tut):**
- `main` her zaman build alınabilir durumda kalsın.
- Her sistem bitince commit at: `feat: sinyal eşleştirme sistemi`
- Riskli bir denemeye girerken branch aç, tutmazsa branch'i sil.
- Her gün akşam `git push`. Bilgisayar bozulursa jam bitmez.

---

## 4. Takvim (9,5 gün)

Prensip: **oynanabilir bir şey her zaman elinde olsun.** Her günün sonunda build alınabiliyor olmalı.

| Gün | Tarih | Hedef |
|---|---|---|
| **1** | 5 Eyl (bugün) | Unity projesi + git ayarları + **boş sahneyle WebGL build alıp itch'e yükle** (pipeline doğrulaması). Sağ tık menüsü fix'ini test et. **Tasarımcıya asset listesini gönder.** Gece sayısına karar ver. |
| **2** | 6 Eyl | Fener + huzme + fare kontrolü + tek gemi + sinyal girişi + eşleştirme + bağlanma. Kutu grafiklerle. **Akşam: eğlenceli mi? Değilse dur ve konuş.** |
| **3** | 7 Eyl | Bağlı gemi yönlendirme, rıhtım, varış, çarpışma-sekme. Feel ayarı: dönüş hızı, koni genişliği. |
| **4** | 8 Eyl | Çoklu gemi, `NightConfig`, `NightManager`, ilk 3 gece baştan sona oynanabilir. |
| **5** | 9 Eyl | Tasarımcıdan gelen sanatın entegrasyonu. Kasaba aydınlanma sistemi. |
| **6** | 10 Eyl | Ses ve müzik. Notlar. Gece→şafak geçişi. |
| **7** | 11 Eyl | Kalan geceler, sis, (varsa) ikinci rıhtım. **İçerik burada biter.** |
| **8** | 12 Eyl | Polish. **Başkasına oynat** — sen izle, konuşma, not al. Sonra düzelt. |
| **9** | 13 Eyl | itch.io sayfası, ekran görüntüsü, GIF, açıklama metni. Final build. |
| **10** | 14 Eyl | Yedek gün. **Öğlene kadar teslim et.** 20:01'e bırakma. |

**Gün 2 kontrol noktası ciddi.** Prototip eğlenceli değilse sanata para/zaman yatırmadan konsepti değiştirmek hâlâ mümkün. Bu, dokümanın da söylediği şey.

---

## 5. Tasarımcıya gönderilecek asset listesi

Bunu **bugün** gönder, tasarımcı bekliyor olmasın. Kutu grafiklerle prototipe devam edersin.

**Önce iki soruyu birlikte cevaplayın:**
1. **Bakış açısı:** Deniz yandan mı, yukarıdan mı, 3/4 açıyla mı görünüyor? Kod bunu bilmeden gemi hareketi yazılamaz. En pratik: hafif tepeden bakan 2D, gemiler yukarıdan görünür.
2. **Çözünürlük:** 1280×720 referans. Sprite'lar 2× (yani 2560×1440 referansına göre) çizilsin, ölçekleme kaybı olmasın.

**Teknik format:** PNG-24 + alfa · pivot noktaları belirtilmiş · tek palet · katmanlar ayrı dosya

| # | Asset | Not |
|---|---|---|
| 1 | Fener kulesi | Silüet + lamba. Lamba ayrı katman (dönebilsin). |
| 2 | Huzme konisi | Yumuşak gradyan, additive blend'e uygun. Uçları soft. |
| 3 | Gemi tipi ×3 | Kayık (küçük/çevik), orta, yük gemisi (büyük/ağır). Her biri için: gövde + ışık noktası + **bağlıyken parlama varyantı**. |
| 4 | Kasaba | Karanlık taban + **12–20 yanan pencere, her biri ayrı PNG.** Kod bunları tek tek açacak. Tek birleşik görsel işe yaramaz. |
| 5 | Rıhtım ×2 | İkinci rıhtım renk kodlu (gemi rengiyle eşleşecek). |
| 6 | Deniz | Tekrarlanabilir doku veya gradyan + dalga overlay. |
| 7 | Gökyüzü | Gece→şafak gradyanı. Kodda geçiş yapılacak, 3–4 ara renk yeterli. |
| 8 | Sis | Yumuşak noise dokusu, overlay için. |
| 9 | UI | 4 yuvalı çubuk çerçevesi, ● ve — sembolleri, not kartı çerçevesi. |
| 10 | Font | **Türkçe karakter desteği şart: ı İ ğ Ğ ş Ş ç Ç ö Ö ü Ü.** Birçok dekoratif font bunları içermez — indirmeden önce kontrol ettir. |

**Öncelik sırası** (tasarımcı hepsini yetiştiremezse): 3 → 4 → 1 → 2 → 9 → 7 → 6 → 5 → 8 → 10

---

## 6. Kesme sırası

Geri kalırsan sırayla bunları at. Bu sırayı şimdi belirle ki panik anında tartışmayasın.

1. İkinci rıhtım
2. Sis
3. 3. gemi tipi (2 tiple idare et)
4. Gece sayısını 6'dan 4'e indir

**Asla kesme:**
- Huzme kontrolünün hissi
- Eşleştirme anının geri bildirimi (yeşil parlama + gemi cevap flaşı)
- Varış kutlaması + kasabada yanan pencere
- Gece→şafak finali
- Notlar (metin, ucuz, "uplift" kriterinin tamamı)

---

## 7. Jam'i kazandıran iki şey

Dokümanın kendi analizi: **oylar 10–15 kişiden geliyor, ilk 30 saniye ve ekran görüntüsü belirleyici.**

**İlk 30 saniye:** 1. gece hiçbir açıklama olmadan öğretmeli. Tek gemi, 2'lik sekans, sekans geminin üstünde sürekli görünür. Oyuncu tıklar, eşleşir, gemi bağlanır. Tutorial metni yok — mekanik kendini anlatsın.

**Ekran görüntüsü:** Bu görüntüyü kazara elde etmeye çalışma, **bilerek tasarla.** En iyi kare muhtemelen: karanlık deniz, üç gemi, huzme birinin üstünde, kasabanın yarısı yanmış, gökyüzü şafağa dönmeye başlamış. Gün 5'te sanat girdiğinde bu kareyi kurmak için bir sahne ayarla.

**GIF:** 3–4 saniye. Sinyal gelir → tıklarsın → gemi yeşil parlar → huzmeyi izlemeye başlar. Döngünün tamamı tek GIF'te görünsün.

---

## 8. İlk üç iş

1. Gece sayısına karar ver (6 öneriyorum).
2. Tasarımcıya asset listesini ve iki soruyu gönder.
3. Unity projesini aç, git ayarlarını yap, **boş sahneyle bir WebGL build alıp sağ tık davranışını tarayıcıda test et.**
