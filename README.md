# Mause Seri Tık

Sol fare tuşuna basınca ilk tıklaman normal çalışır. Tuş basılı kaldığı sürece ek tıklama üretilir. Tuşu bıraktığın anda tekrar durur. Chrome eklentisi ve Windows uygulaması ayrı ayrı kullanılabilir.

[En son sürümü indir](https://github.com/ahmetcihatgokce/mause-seri-tik/releases/latest)

| Paket | Kullanım alanı | Hedef hız |
| --- | --- | ---: |
| `Mause-Seri-Tik-Chrome.zip` | Etkinleştirdiğin Chrome sekmesi | En fazla 250 tık/sn |
| `Mause-Seri-Tik.exe` | Windows 10 veya 11'de seçtiğin pencere | En fazla 500 tık/sn |

## Chrome

1. `Mause-Seri-Tik-Chrome.zip` dosyasını bir klasöre çıkar.
2. Chrome adres çubuğuna `chrome://extensions` yaz. **Geliştirici modu** seçeneğini aç ve **Paketlenmemiş öğe yükle** düğmesine bas.
3. `manifest.json` dosyasını içeren klasörü seç.
4. Kullanacağın sekmeyi aç. Eklentinin simgesinden modu ve hızı seçip **Bu sekmede başlat** düğmesine bas.

Sol tuşla dene. `Esc` eklentiyi o sayfada kapatır. Sekmeyi yenilediğinde veya yeni bir sayfa açtığında eklentiyi yeniden etkinleştir. Chrome sürümü web sayfasının ana çerçevesinde çalışır. Sayfa kodundan oluşturulan olayların `isTrusted` değeri `false` olduğundan bazı oyunlar bunları yok sayabilir.

## Windows

1. `Mause-Seri-Tik.exe` dosyasını aç. Kurulum gerektirmez.
2. Modu ve hızı ayarla. **Sınırlı seri** modunda bir basışta en fazla kaç tık olacağını seç. Windows varsayılan olarak saniyede 30 tık hedefler.
3. Kullanacağın pencereye geç ve `F7` ile pencereyi seç. Uygulamadaki gecikmeli pencere seçimini de kullanabilirsin.
4. `F8` ile seri tıklamayı aç veya kapat. Herhangi bir anda `F9` ile durdur.

Program, seçili pencere öndeyken ve imleç pencerenin üzerindeyken sol tuşu işler. Windows uygulaması imzasızdır. Windows, daha yüksek yetkiyle çalışan uygulamalara giriş göndermeyi kısıtlayabilir. Oyunun giriş işleme şekli, kare hızı ve kendi atış hızı sınırı, kaç tıklamanın işleneceğini belirler.

| Mod | Ne zaman durur? |
| --- | --- |
| Basılı tut | Sol tuş bırakıldığında |
| Sınırlı seri | Sol tuş bırakıldığında veya ayarlanan toplam tıklama sayısına ulaşıldığında |

Çok kısa bir dokunuş, bir sonraki tıklama için gereken süreden önce sona ererse tek tık olarak kalır.

## Kaynak kodundan paketleme

Windows'ta deponun kökünden çalıştır:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\pack.ps1
```

Çıktılar `dist/` altında oluşur. `SHA256SUMS.txt` dağıtım dosyalarının SHA-256 değerlerini içerir. Windows derlemesi .NET Framework ile gelen C# derleyicisini kullanır. Chrome eklentisi geliştirme sırasında doğrudan `chrome-extension/` klasöründen yüklenebilir.

MIT lisansı · [Lisans metni](LICENSE)
