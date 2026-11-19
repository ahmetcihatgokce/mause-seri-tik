# Mause Seri Tık Chrome uzantısı için gizlilik politikası

Sürüm 1.1.0 · 19 Kasım 2026

Mause Seri Tık, seçtiğiniz sekmede seri tıklama ve isteğe bağlı oyun kontrolleri sağlar. Uzantı, işlediği verileri geliştiriciye, analiz hizmetlerine veya reklam şirketlerine iletmez. Hesap, reklam veya kullanım analizi içermez.

## Seçtiğiniz sekmede işlenen bilgiler

Uzantının menüsünü açtığınızda etkin sekmenin kimliği ve adresi, sayfanın desteklenip desteklenmediğini belirlemek için okunur. Bu bilgiler ziyaret geçmişi olarak saklanmaz. **Bu sekmede başlat** seçeneğini kullandığınızda uzantı, tıklamaları tekrarlamak için fare düğmesinin durumunu ve imlecin konumunu işler. Esc ile durdurma ve isteğe bağlı otomatik koşma için ilgili tuş olaylarını denetler. Yazdığınız metni veya tuş geçmişinizi kaydetmez. Ürettiği fare ve klavye olaylarını, kontrolleri uygulamak üzere açık sayfaya iletir.

**Renk tabanlı nişan yardımı** varsayılan olarak kapalıdır. Açtığınızda, oyun imleci kilitliyken uzantı oyun çizim yüzeyinden (canvas) geçici bir görüntü akışı alır. Seçilen rengi bulmak için görüntünün ortasındaki bölgeyi tarayıcı belleğinde inceler. Görüntüler dosyaya kaydedilmez ve ağ üzerinden gönderilmez. Sekme gizlendiğinde veya uzantı o sekmede durdurulduğunda görüntü akışı sonlandırılır.

## Saklanan tercihler

Tıklama modu, profil, hız, seri uzunluğu, nişan yardımı, hedef rengi ve otomatik koşma tercihleri cihazınızdaki Chrome profilinde tutulur (`chrome.storage.local`). Uzantı bu tercihleri Chrome hesabınız üzerinden eşitlemez. Etkinleştirme sırasında üretilen geçici oturum kimliği, sekmedeki bileşenlerin durdurma komutunu paylaşması için bellekte kullanılır. Uzantıyı Chrome'dan kaldırdığınızda kaydedilmiş tercihler de silinir.

## Chrome izinleri

- **activeTab** — Uzantı menüsünü açtığınız sekmeye geçici erişim sağlar.
- **scripting** — Başlatma komutunuzla uzantının kendi kontrol kodunu seçilen sekmede ve erişilebilen çerçevelerinde çalıştırır.
- **storage** — Tercihlerinizi cihazınızda saklar.

Gizlilik hakkında soru sormak için [Mause Seri Tık proje sayfasındaki Issues bölümünü](https://github.com/ahmetcihatgokce/mause-seri-tik/issues) kullanabilirsiniz.
