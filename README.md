# Musaic Kontrol

**Musaic MP5 / MP10 / MPL Wi-Fi hoparlörleri için resmi uygulamanın yerini tutan, ücretsiz ve açık kaynak Windows kontrol programı.**

Musaic şirketi kapandı ve resmi "Musaic" uygulaması artık çalışmıyor. Ama hoparlörlerin içindeki ağ çalar hâlâ sağlam: standart **UPnP/DLNA** ile kontrol edilebiliyor. Bu program onu kullanır.

*(English summary at the bottom.)*

## Özellikler

- **PC sesini hoparlöre gönderme:** YouTube, Spotify, oyunlar… bilgisayarda çalan her şey Wi-Fi üzerinden Musaic'ten çalar.
- **Spotify ve YouTube Music:** Tek tıkla yalnızca o uygulamanın sesini Musaic'e yönlendirir; oyun, Discord gibi diğer sesler kulaklıkta kalır.
- **Kayıpsız dosya çalma:** MP3, FLAC, WAV, M4A, OGG… (sürükle-bırak)
- **Yüksek kalite:** 192 kHz / 24-bit'e kadar yayın, stüdyo kalitesinde örnekleme dönüştürücü (SNR > 105 dB), "maksimum kalite" modu.
- **Düşük gecikme:** Ayarlanabilir güvenlik payıyla Bluetooth'a yakın gecikme.
- **Ses seviyesi, sessiz, duraklat/devam** kontrolü.
- **Sürekli açık müzik merkezi modu:** Windows'la başlama, tepside çalışma, yayını otomatik başlatma ve kopunca kendiliğinden toparlama.
- Koyu temalı modern arayüz. **Kurulum gerektirmez**; tek bir `.exe`.

## Kurulum

1. [Releases](../../releases) sayfasından `MusaicKontrol.exe`'yi indirin **veya** kendiniz derleyin (aşağıda).
2. Çalıştırın. Windows Güvenlik Duvarı sorarsa **izin verin** (hoparlör yayını bilgisayardan çeker).
3. Program hoparlörü ağda kendisi bulur.

### Kaynaktan derleme

Hiçbir şey kurmanıza gerek yok; Windows'un içindeki .NET Framework derleyicisi kullanılır:

```bat
derle.bat
```

## Hoparlörü Wi-Fi'ye bağlamak (resmi uygulama olmadan)

Hoparlör ağda görünmüyorsa Wi-Fi ayarı silinmiş olabilir:

1. Hoparlörün **WPS** tuşuna **5–10 saniye basılı tutun** → hoparlör `Musaic_xxxxxx` adlı şifresiz bir kurulum ağı yayınlar.
2. Bilgisayarı veya telefonu bu ağa bağlayın, tarayıcıda **`http://172.19.42.1`** adresini açın.
3. Hoparlöre bir isim verin → ev Wi-Fi ağınızı seçin → şifreyi girin → **Connect**.
4. Hoparlörü **kapatıp açın** (kurulumdan sonra ses gelmezse bu gerekli).

> Not: Birçok operatör modeminde WPS kapalıdır; yukarıdaki tarayıcı yöntemi her zaman çalışır.

## Kullanım ipuçları

| Durum | Çözüm |
|---|---|
| Ses gelmiyor | Hoparlörü kapatıp açın; telefon Bluetooth ile bağlıysa Bluetooth'u kapatın (Bluetooth ağ sesinin önüne geçer). |
| Ses arada kesilip ~3 sn geriden geliyor | Ayarlar → **Güvenlik payı**'nı 100–200 ms artırın. |
| Gecikme fazla | Güvenlik payını azaltın; "192 kHz / 24-bit" biçimini kullanın. |
| Yalnızca belirli uygulamaların sesi gitsin | Sanal bir ses aygıtı kullanın (ör. [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) veya Steam yüklüyse "Steam Streaming Speakers") ve Ayarlar → Ses'ten seçin. |

### Işık durumları (kılavuza göre)

- **Mavi**: açık · **Mor**: ağ beklemesi · **Kırmızı**: tam bekleme · **×** tuşunun ışığı: sessiz.

### Spotify hakkında

Hoparlörün kendi **Spotify Connect** desteği (2016 sürümü) Spotify tarafından artık kabul edilmiyor. Bu program bunun yerine bilgisayardaki Spotify'ın sesini hoparlöre iletir; telefondan **Spotify → Cihazlar → bilgisayarınız** seçilerek kontrol edilebilir. Sürekli açık bir bilgisayar (mini PC) ile ana bilgisayar kapalıyken de çalışır — bkz. [MINIPC_KURULUM.md](MINIPC_KURULUM.md).

## Nasıl çalışır?

- Hoparlör, ağda bir **UPnP MediaRenderer** (AVTransport + RenderingControl) olarak bulunur (SSDP).
- PC sesi Windows **WASAPI loopback** ile yakalanır, programın içindeki küçük HTTP sunucusundan kesintisiz WAV akışı olarak sunulur; hoparlöre `SetAVTransportURI` + `Play` gönderilir.
- Uygulama başına yönlendirme Windows'un "uygulama ses tercihleri" özelliğini kullanır.

## Lisans ve sorumluluk reddi

MIT lisansı. Bu proje Musaic Ltd. ile ilişkili değildir; "Musaic" adı yalnızca uyumluluğu belirtmek için kullanılmıştır.

---

### English summary

**Musaic Kontrol** is a free, open-source Windows app that replaces the discontinued official Musaic app for Musaic MP5/MP10/MPL Wi-Fi speakers. It finds the speaker via UPnP/DLNA, streams your PC's audio (WASAPI loopback, up to 192 kHz/24-bit, low latency), plays local files losslessly, controls volume, and can route only Spotify or YouTube Music to the speaker. No install needed — build with `derle.bat` (uses the .NET Framework compiler built into Windows) or grab the release `.exe`. To re-join the speaker to Wi-Fi without the app: hold WPS 5–10 s, join the `Musaic_xxxxxx` network, open `http://172.19.42.1`, pick your Wi-Fi, then power-cycle the speaker. The UI is in Turkish.
