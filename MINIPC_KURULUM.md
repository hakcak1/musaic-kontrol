# Mini PC'yi Musaic müzik merkezi yapma

Amaç: Sürekli açık küçük bir Windows bilgisayar (ör. HP EliteDesk mini) Spotify'ı çalar ve sesini Musaic hoparlöre gönderir. Ana bilgisayar kapalıyken telefondan **Spotify → Cihazlar → mini PC** seçilerek müzik Musaic'ten dinlenir.

> Bu dosya hem insanlar hem de mini PC'de çalışan bir yapay zekâ asistanı (ör. Claude Code) için adım adım yazılmıştır. Kullanıcının hesap girişleri, şifreler ve yönetici onayları gereken adımlar **kullanıcıya bırakılmalıdır**.

## 1. Programı indir ve derle

```bat
git clone <bu deponun adresi> C:\MusaicKontrol
cd C:\MusaicKontrol
derle.bat
```

Git yoksa: `winget install --id Git.Git -e` (veya Releases sayfasından `MusaicKontrol.exe`'yi `C:\MusaicKontrol` içine indirin).

## 2. Sanal ses aygıtı

Spotify'ın sesi bu aygıt üzerinden Musaic'e gider; aygıtın kendisi ses çıkarmaz.

- **Steam yüklüyse:** "Steam Streaming Speakers" zaten vardır, ek kurulum gerekmez.
- **Değilse:** [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) kurun (yönetici onayı gerekir, kurulumdan sonra yeniden başlatma isteyebilir). Windows'ta **"CABLE Input"** adlı bir çıkış aygıtı belirir.

## 3. Spotify

- Microsoft Store'dan Spotify'ı kurun: `winget install --id 9NCBCSZSJRSB -e --source msstore`
- **Kullanıcı** kendi hesabıyla giriş yapar (Premium hesapla telefondan uzaktan kontrol edilebilir).
- Spotify Ayarlar → **"Başlangıçta Spotify'ı aç"** seçeneğini açın, isteğe bağlı olarak "küçültülmüş" seçin.

## 4. Musaic Kontrol ayarları

`C:\MusaicKontrol\MusaicKontrol.exe`'yi çalıştırın (Güvenlik Duvarı sorarsa **izin verin**), sonra **Ayarlar** sekmesinde:

| Ayar | Değer |
|---|---|
| Hoparlör | Musaic MP5 (kendisi bulunur) |
| Musaic'e gönderilecek ses aygıtı | **CABLE Input** (veya Steam Streaming Speakers) |
| Yayın biçimi | 192 kHz / 24-bit (önerilen) |
| Güvenlik payı | 0 ms; ses arada kesiliyorsa 100–200 ms |
| Windows açılınca başlat | **Açık** |
| Kapatınca tepside çalışmaya devam et | **Açık** |
| Yayını otomatik başlat ve koru | **Açık** |
| Spotify'ı her zaman Musaic'e gönder | **Açık** |

## 5. Sürekli açık kalması için (kullanıcı onayıyla)

- Uyku modunu kapatın: `powercfg /change standby-timeout-ac 0`
- Elektrik kesintisinden sonra kendiliğinden açılması için BIOS'ta "After Power Loss → Power On" (isteğe bağlı).
- Programlar ancak bir kullanıcı oturumu açıkken başlar: Windows'un **otomatik oturum açma** özelliğini açmak kullanıcının kararıdır (güvenlik açısından yalnızca evde fiziksel olarak güvende olan bir bilgisayar için önerilir).

## 6. Test

1. Telefonda Spotify'ı açın → **Cihazlar** → mini PC'nin adını seçin → bir şarkı başlatın.
2. Birkaç saniye içinde ses Musaic'ten gelmeli. Gelmezse mini PC'de Musaic Kontrol → Ayarlar → **Günlük** bölümüne bakın.

## Sonraki adım: müzikle senkron ışık (planlanan)

Renkli IKEA TRÅDFRI ampuller için mini PC'ye bir USB Zigbee çubuğu (ör. Sonoff ZBDongle-E) takılacak ve ampul müzikle senkron renk/parlaklık değiştirecek.
