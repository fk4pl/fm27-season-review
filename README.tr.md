# Football Manager 27 için Season Review (Sezon Değerlendirmesi)

*English: [README.md](README.md)*

Football Manager'da FM24'e kadar bulunan sezon sonu değerlendirmesini geri getiren bir BepInEx 6
eklentisi. Kulübünüz sezonun son lig maçını oynadığında mod, o sezonun hikâyesini tek bir rapora
yazar: beklentilere göre nerede bitirdiğiniz, öne çıkan anlar, oyuncularınız, transferler, finans,
sakatlıklar ve yönetimle taraftarların ne düşündüğü.

Oyunun içinde çalışır, ayrı bir uygulama gerektirmez ve oyun verisini yalnızca **okur**. Kayıt
dosyalarınıza hiçbir zaman yazmaz ve gizli değerleri (mevcut/potansiyel yetenek, gizli nitelikler,
sakatlanma eğilimi vb.) hiçbir yerde göstermez.

> **Durum: 0.2.0, erken sürüm.**
>
> **Windows:** Linux'ta (Proton) geliştirildi ve test edildi. Diğer FM27 eklentileriyle aynı
> BepInEx IL2CPP altyapısını kullanıyor, bu yüzden Windows'ta da çalışması beklenir, ama orada
> henüz deneyemedim. Denerseniz Issues'ta nasıl gittiğini yazın, doğrulanan kurulumlar buraya
> eklenecek.

## Rapor neleri içeriyor

Her rapor, tek başına açılabilen bir HTML dosyasıdır (internet ve script gerektirmez, düzgün
yazdırılır). Yanında aynı metnin Markdown kopyası da yazılır; örneğin forumda paylaşmak için.

- **Başlık:** lig sırası, puan, G-B-M ve goller tek bakışta.
- **Sezon özeti:** nerede bitirdiğiniz, sezon dökümünüz ve medyanın sezon öncesi tahminiyle
  karşılaştırma; yönetimin beklentileri ve yönetimin kendi değerlendirmesi; kupalar; öne çıkan
  anlar (en farklı galibiyet, en ağır yenilgi, en pahalı transfer, gol kralı).
- **Lig performansı:** oynanan, galibiyet, beraberlik, mağlubiyet, goller ve puan; iç saha ve
  deplasman ayrımıyla. Ligdeki en uzun galibiyet ve yenilmezlik serileri.
- **Oyuncular:** sezonun oyuncusu, en çok gol atanlar, en çok asist yapanlar, en yüksek ortalama
  puan, forma giyen genç oyuncular (21 yaş ve altı) ve ayrı bir tabloda kiralıktaki oyuncular.
- **Transferler:** gelenler ve gidenler (bedel ve tarih), kiralıklar, ödenen ve alınan
  bonservisler, net harcama.
- **Finans:** sezon başı ve şu anki banka bakiyesi, kâr/zarar (bakiyedeki değişim), oyunun
  kendi sezon gelir ve gider toplamları, öngörülen kâr/zarar, maaş gideri ve mali durum.
- **Sakatlıklar:** sakatlık sayısı, kaybedilen gün ve en uzun süre sahalardan uzak kalanlar.
- **Yönetim ve taraftarlar:** oyunun kendi sezon sonu değerlendirmeleri, olumlu yönler ve
  eleştirilerle.

Sezonun oyuncusu: kulübün resmi maçlarının en az üçte ikisinde forma giyenler arasında en yüksek
ortalama puan; eşitlikte gol ve asist toplamı belirleyici. Rapor bu kuralı belirtir.

FM27'nin zaten iyi gösterdiği şeyler (sezon sonu ödülleri, sezonun en iyi 11'i, aylık finans
raporları) tekrarlanmaz.

## Diller

Raporlar **İngilizce** (varsayılan) ve **Türkçe** olarak üretilebilir. Rapor metinlerinin
tamamı, `lang/` klasöründe dil başına tek bir düz metin dosyasındadır. Çeviri için
`lang/en.txt` dosyasını `lang/<kod>.txt` olarak kopyalayın, `=` işaretinden sonraki metinleri
çevirin ve ayarlarda `Language = <kod>` yapın. Eksik satırlar İngilizceye düşer.

Oyunun kendisinden gelen metinler (yönetim ve taraftar görüşleri, turnuva adları) oyunun dilinde
görünür. Bir dil dosyası, yönetim hedefleri, sakatlık adları ve mali durum değerleri için küçük bir
sözlük de içerebilir; `lang/tr.txt` dosyasında böyle bir sözlük var. Sözlükte olmayan metin
oyundaki gibi gösterilir.

## Gereksinimler

- Steam'de Football Manager 27 (**27.0.1** ile test edildi, Steam build 26577676).
- **BepInEx 6.0.0-be.793**, Unity IL2CPP, Windows x64. Daha yeni bleeding-edge sürümler
  (FM27'yi be.755 ve sonrası okuyabiliyor) çalışabilir ama test edilmedi.
- Linux: Steam ve Proton (Proton Experimental ile test edildi).

## Kurulum

Önce kayıtlarınızı yedekleyin (`Documents/Sports Interactive/Football Manager 27/games`). Mod
onlara yazmaz, ama erken sürüm bir yazılım.

### 1. BepInEx kurulumu (başka FM27 BepInEx modu kullanıyorsanız atlayın)

1. <https://builds.bepinex.dev/projects/bepinex_be/793> adresinden
   `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793+dbc82f0.zip` dosyasını indirin.
2. Steam'de Football Manager 27'ye sağ tıklayın > *Yönet* > *Yerel dosyalara göz at*.
3. Zip'i bu klasöre, `fm.exe` dosyasının yanına çıkarın (`BepInEx/`, `dotnet/`, `winhttp.dll`,
   `doorstop_config.ini` oluşmalı).
4. **Yalnızca Linux/Proton:** Steam'de Football Manager 27 > *Özellikler* > *Başlatma
   seçenekleri* alanına `WINEDLLOVERRIDES="winhttp=n,b" %command%` yazın.
   Zaten başlatma seçenekleriniz varsa `WINEDLLOVERRIDES=...` kısmını onların önüne koyun;
   örneğin `WINEDLLOVERRIDES="winhttp=n,b" sarmalayici %command%`.
5. Oyunu bir kez açın, ana menüyü bekleyin (ilk açılış birkaç dakika sürer), sonra kapatın.
   `BepInEx/LogOutput.log` dosyası oluşmuş olmalı.

### 2. Season Review kurulumu

**Windows:** sürüm zip'ini çıkarın ve içindeki `BepInEx` klasörünü oyun klasörüne kopyalayıp
mevcut `BepInEx` klasörüyle birleştirin. Sonuçta
`Football Manager 27\BepInEx\plugins\SeasonReview\SeasonReview.dll` ve yanında `lang` klasörü
olmalı.

**Linux:** sürüm zip'ini çıkarın ve kurulum betiğini o klasörden çalıştırın:

```sh
./install-linux.sh                 # oyunu Steam kitaplıklarınızda bulur
./install-linux.sh --game-dir "/yol/Football Manager 27"
./install-linux.sh --uninstall     # eklentiyi kaldırır; BepInEx ve raporlar kalır
```

Betik eklentiyi kopyalar, BepInEx be.793 eksikse indirip kurmayı önerir (önce sorar ve indirilen
dosyanın sağlama toplamını kontrol eder) ve başlatma seçeneğini hatırlatır. Kurmadan önce oyunu
kapatın.

Çalıştığını görmek için `BepInEx/LogOutput.log` içinde `Season Review 0.2.0 loaded` satırını
arayın.

## Kullanım

Normal şekilde oynayın. Kulübünüzün sezonun son lig maçından sonra rapor bir kez yazılır. Sezonun
o ana kadarki bölümü için ara rapor almak isterseniz, yüklü bir kariyerde **Ctrl+F8**'e basın ve
20-40 saniye bekleyin (oyun bu sırada kullanılabilir). Varsayılan kısayol, FM View Exporter ve
FM27 Player Export'un kullandığı F9 ile çakışmaz.

Raporlar `Documents/Sports Interactive/Football Manager 27/SeasonReviews/` klasörüne kaydedilir:

- sezon sonu raporu: `<Kulüp>_<Sezon>.html` ve `.md`, örneğin `Beşiktaş_2026-27.html`
- ara rapor: `<Kulüp>_<Sezon>_<tarih>.html`, örneğin `Beşiktaş_2026-27_2026-08-31.html`

Linux'ta bu klasör oyunun Proton prefix'i içindedir:
`~/.local/share/Steam/steamapps/compatdata/4530370/pfx/drive_c/users/steamuser/Documents/Sports Interactive/Football Manager 27/SeasonReviews/`

Sezonun bitmesini beklemeden denemek için deneme amaçlı bir kariyer kullanın: son lig maçından
kısa süre öncesine kadar tatile çıkın, sonra devam edin.

## Oyun içi panel

Yüklü bir kariyerde **F10**'a basarak sezon değerlendirmesini oyunun içinde görebilirsiniz. Panel,
o an yüklü olan kariyerin canlı verilerini okur (önce birkaç saniye "Sezon verileri okunuyor..."
yazar) ve HTML dosyasının oluşturulduğu raporun aynısını, `Language` ayarındaki dilde kurar. Yedi
sekmesi var: Özet, Lig, Oyuncular, Transferler, Finans, Sakatlıklar ve Yönetim. Kapat düğmesiyle,
**Esc** ile ya da yeniden **F10** ile kapanır. Ana menüde, kariyer yüklü değilken, yüklü bir kariyer
olmadığını söyler.

> Önce oyun penceresine tıklayın, sonra F10'a basın. Tuş yalnızca oyun penceresi odaktayken
> çalışır.

Panel bir ektir. Asıl çıktı HTML ve Markdown rapor olmaya devam eder ve eskisi gibi yazılır; panel
diske hiçbir şey yazmaz (ne raporları ne de raporu yazılmış sezonların listesini). Ctrl+F8 ve
otomatik rapor, panel açıkken ya da yüklenirken de çalışır.

Panel kendi katmanını oyunun üstüne çizer. Oyunun ekranlarını ya da kayıtlarınızı asla
değiştirmez; bir hatayla karşılaşırsa raporları etkilemeden oturumun geri kalanında kendini
kapatır. Modun geri kalanı gibi Linux'ta (Proton) geliştirildi ve test edildi; Windows için
baştaki nota bakın.

## Ayarlar

`BepInEx/config/io.github.seasonreview.fm27.cfg` (ilk açılışta oluşur):

| Ayar | Varsayılan | Anlamı |
|---|---|---|
| `Enabled` | `true` | Modu açar veya kapatır. |
| `AutoReport` | `true` | Son lig maçından sonra raporu otomatik yazar. |
| `Language` | `en` | `en` veya `tr`; ya da `lang/<kod>.txt` dosyası olan herhangi bir kod. |
| `OutputFolder` | boş | Raporların klasörü; boşsa yukarıdaki `SeasonReviews` klasörü. |
| `Hotkey` | `Ctrl+F8` | Ara rapor. Biçim: `[Ctrl+][Shift+][Alt+]<F1-F24, A-Z, 0-9>`. |
| `PanelEnabled` | `true` | `[Panel]` bölümü. Oyun içi paneli açar veya kapatır. |
| `PanelHotkey` | `F10` | Paneli açar ve kapatır. Biçim `Hotkey` ile aynı. |
| `PanelPosition` | `Center` | Panelin yeri: `Center`, `TopLeft`, `TopRight`, `BottomLeft` veya `BottomRight`. |
| `ProbeHotkey` | `false` | Geliştirici: Shift+kısayol, açık ekranın özellik adlarını döker. |
| `RawDump` | `false` | Geliştirici: biçimlendirilmemiş veriyi de raporun yanına yazar. |

Raporu yazılmış sezonlar `BepInEx/config/io.github.seasonreview.fm27.reported.txt` dosyasında
tutulur. O sezon için otomatik raporu yeniden istiyorsanız ilgili satırı silin.

## Bilinen sınırlar

- **Windows:** Linux'ta (Proton) geliştirildi ve test edildi. Diğer FM27 eklentileriyle aynı BepInEx
  IL2CPP altyapısını kullanıyor, bu yüzden Windows'ta da çalışması beklenir, ama orada henüz
  deneyemedim. Denerseniz Issues'ta nasıl gittiğini yazın, doğrulanan kurulumlar buraya eklenecek.
- **Oyun güncellemeleri modu bozabilir.** FM27'nin resmi mod desteği yok; mod, oyunun arayüz için
  tuttuğu iç verileri okur ve bunlar her yamada değişebilir. Okunamayan bölümler çökme yerine
  "Veri alınamadı" gösterir, ama bir yamadan sonra yeni sürüm gerekebilir.
- **Tatille sezon sonunu geçerseniz rapor kaçabilir.** Mod, oyun boştayken her oyun günü bir kez
  kontrol yapar. Son lig maçından önceden yeni sezona kadar oyun hiç durmadan tatildeyseniz sezon,
  kontrol çalışmadan değişebilir ve o sezonun raporu yazılmaz. Tatili sezon bitmeden sonlandırın ya
  da son lig maçından sonra Ctrl+F8'e basın.
- **Kupa tur adları gösterilmez.** Kupa satırlarında döküm ve son maç var, tur ("yarı final" vb.)
  yok.
- **"Sakatlık nedeniyle kaçırılan maç" toplamı yok.** Oyunda böyle bir değer yok; raporda sakatlık
  sayısı ve kaybedilen gün var.
- **Kiralıklar:** oyunun sezon istatistikleri, oyuncunun hangi kulüpte oynadığına bakmadan tüm
  kulüp maçlarını sayar. Bu yüzden bu sezon kiralık gönderilen oyuncular kulüp istatistiklerine
  alınmaz ve ayrı bir tabloda (çoğunlukla kiralık kulübündeki) rakamlarıyla gösterilir. Sezon
  içinde gelen oyuncular `*` ile işaretlenir, çünkü rakamları önceki kulüpteki maçları da
  içerebilir. Önceki sezonlarda yapılmış kiralıklar tanınmaz.
- **Oyundan gelen metinler oyunun dilinde kalır.** Yönetim ve taraftar görüşleri ile turnuva adları
  oyundan alıntıdır. Oyun İngilizceyken Türkçe raporda iki dili birlikte görürsünüz.
- **Ligin bitmesinden sonra oynanan kupa maçları** (örneğin bir hafta sonraki kupa finali) sezon
  sonu raporunda yer almaz.
- **Maç sonuçlarından hesaplanan lig satırı:** lig tablosu okunamazsa lig dökümü kulübün maç
  sonuçlarından hesaplanır ve rapor bunu belirtir; bu durumda puan silme cezaları dahil olmaz.
- Son maçtan önce alınmış bir kaydı yükleyip devam ederseniz sonuçlar değişebilir; rapor sezonu
  sonunda nasıl oynadıysanız öyle anlatır.

## Uyumluluk

Mod hiçbir Harmony yaması kullanmaz ve oyunda hiçbir şeyi değiştirmez. FM View Exporter,
FMDataDisplay ve FM27 Exporter gibi diğer BepInEx 6 FM27 modlarıyla birlikte çalışacak şekilde
tasarlandı, ama bu birliktelik test edilmedi.

## Kaynaktan derleme

Gereksinimler: .NET SDK 8 veya üstü ve `BepInEx/interop/` klasörünün oluşması için FM27'nin
BepInEx ile en az bir kez açılmış olması. Oyunun dosyaları yalnızca yerel derleme referansıdır;
bu depoda ve sürüm paketinde yer almazlar.

```sh
dotnet test tests/SeasonReview.Tests                         # oyun gerekmez
dotnet build src/SeasonReview -c Release                      # eklentiyi oyuna kopyalar
dotnet build src/SeasonReview -c Release -p:GameDir="C:\...\Football Manager 27"
scripts/package.sh                                            # dist/SeasonReview-<sürüm>.zip üretir
```

Oyun verisinin nasıl okunduğuna dair notlar [docs/DISCOVERY.md](docs/DISCOVERY.md) dosyasında.
`tools/InteropScan`, oyun derlemesinden hangi Unity metotlarının çıkarıldığını gösteren bir
geliştirici aracıdır (README'sine bakın); sürüm paketinde yer almaz.

## Lisans

MIT, bkz. [LICENSE](LICENSE). Diğer FM27 eklentilerinden kod kopyalanmadı; yalnızca yaklaşımları
incelendi (FM View Exporter, FM27 Player Export, FMDataDisplay, FM27 Exporter). Football Manager,
Sports Interactive ve SEGA'nın ticari markasıdır; bu proje onlarla bağlantılı değildir.
