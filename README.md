# LibPlayground

Kurumsal entegrasyon projeleri için birbirinden bağımsız, yüksek performanslı .NET servisleri: SAP OData, REST, Excel, veritabanı, e-posta, loglama, güvenlik, serileştirme, dosya sistemi, tarih/tatil, metin ve koleksiyon araçları.

- **Birbirinden bağımsız servisler:** Yalnızca ihtiyacınız olan servisi kullanırsınız, servisler birbirine referans vermez.
- **Uzun ömürlü kullanım:** Servisler thread-safe'dir ve uygulama boyunca tek örnek (singleton) olarak kullanılmak üzere tasarlanmıştır.
- **Türkçe desteği:** tr-TR kültürü, TCKN/VKN/IBAN doğrulama, Türkiye resmi ve dini tatilleri, tutarı yazıyla ifade etme.
- **Bellek dostu:** Büyük veriler akış halinde okunur/yazılır, satır eşleme için çalışma anında derlenmiş kod kullanılır (reflection yok).

## Kurulum

```bash
dotnet add package LibPlayground
```

Desteklenen hedefler: .NET 10, .NET 8, .NET 5 ve .NET Framework 4.8. Paket her hedef için ayrı bir derleme içerir; genel API tüm hedeflerde aynıdır (aşağıdaki küçük farklar dışında).

### Hedef çerçeve notları

- **.NET Framework 4.8:** Eksik BCL API'leri kütüphane içinde karşılanır. `DateOnly`/`TimeOnly` için `Portable.System.DateTimeOnly`, `TimeProvider` için `Microsoft.Bcl.TimeProvider`, AES-GCM için `Microsoft.Bcl.Cryptography` paketleri kullanılır.
  - HTTP istekleri `HttpClientHandler` ile gönderilir (HTTP/2 ve Brotli açma yok); bağlantı ömrü `ServicePoint` üzerinden ayarlanır.
  - `CompressionFormat.Brotli` desteklenmez, `PlatformNotSupportedException` fırlatır.
  - ADO.NET sağlayıcılarının async transaction/dispose çağrıları senkron karşılıklarıyla çalışır.
  - `DateTimeServiceOptions.WeekendDays` tipi `IReadOnlyCollection<DayOfWeek>`'tir (`IReadOnlySet<T>` bu çerçevede yoktur).
- **.NET 5:** Desteği sona ermiş bir çerçevedir. `Microsoft.Data.SqlClient` 5.2.x ile derlenir; 6.0 ve sonrası .NET 5 için çalışma zamanı içermez.
- **.NET Framework 4.8 ve .NET 5:** Varsayılan JSON ayarlarına `DateOnly`/`TimeOnly` dönüştürücüleri (`yyyy-MM-dd`, `HH:mm:ss`) eklenir; kendi `JsonSerializerOptions` nesnenizi veriyorsanız bu tipler için dönüştürücüyü siz eklemelisiniz. `DateOnly`/`TimeOnly` veritabanı parametreleri `DATE`/`TIME` olarak gönderilir. `Europe/Istanbul` saat dilimi Windows'ta `Turkey Standard Time` olarak çözülür.

## Servisler ve namespace'ler

| Servis | Namespace | Öne çıkanlar |
|---|---|---|
| `RestService` | `CSPLibraries.Rest` | Tekrar deneme, OAuth2 client credentials, akışla indirme/yükleme |
| `SAPService` | `CSPLibraries.Sap` | CSRF token, OData v2/v4, `ODataQuery`, `$count`, MERGE, function import |
| `ExcelService` | `CSPLibraries.Excel` | Hızlı okuma, nesne eşleme, akış tabanlı xlsx yazma |
| `DatabaseService` | `CSPLibraries.Database` | Sağlayıcıdan bağımsız ADO.NET, SqlBulkCopy, transaction, sayfalama |
| `MailService` | `CSPLibraries.Mail` | SMTP bağlantı havuzu, toplu gönderim, şablon, IMAP okuma |
| `LogService` | `CSPLibraries.Logging` | `ILoggerProvider`, dosya/konsol/veritabanı, maskeleme, korelasyon |
| `SecurityService` | `CSPLibraries.Security` | AES-256-GCM, PBKDF2, HMAC, JWT (HS256), TOTP |
| `SerializationService` | `CSPLibraries.Serialization` | CSV, sabit uzunluklu dosya, JSON/NDJSON, XML + XSD, sıkıştırma |
| `DirectoryService` | `CSPLibraries.Directories` | Hızlı tarama, izleme, arşivleme, yinelenen dosya bulma |
| `DateTimeService` | `CSPLibraries.DateTimes` | İş günü, tatiller, mesai saati hesabı, SAP tarih biçimleri |
| `StringService` | `CSPLibraries.Strings` | Türkçe metin, doğrulama, maskeleme, benzerlik (extension metotlar) |
| `EnumerableService` | `CSPLibraries.Enumerables` | Parti/paralel işleme, sayfalama, `TopBy` (extension metotlar) |

## Örnekler

### REST

```csharp
using CSPLibraries.Rest;

using var api = new RestService("https://api.firma.com/v1");
api.UseClientCredentials("https://auth.firma.com/token", "clientId", "secret");

var siparisler = await api.GetAsync<List<Siparis>>("orders", new { durum = "acik", sayfa = 1 });
await api.PostAsync("orders", yeniSiparis);
await api.DownloadFileAsync("reports/42", @"C:\raporlar\42.pdf");
```

### SAP OData

```csharp
using CSPLibraries.Sap;
using CSPLibraries.Sap.Datas;
using CSPLibraries.Sap.Models;

using var sap = new SAPService(new SapServiceOptions
{
    BaseUrl = "https://sap.firma.com/sap/opu/odata/sap/ZSD_ORDER_SRV",
    UserName = "RFC_USER",
    Password = "...",
    SapClient = "100"
});

var sorgu = ODataQuery.Create().WhereEquals("Plant", "1000").Select("OrderId", "Amount").Take(500);
List<Siparis> liste = await sap.GetODataListAsync<Siparis>("OrderSet", sorgu);

await sap.MergeAsync("OrderSet('4500001234')", new { Status = "C" });
```

POST/PUT/MERGE isteklerinde CSRF token otomatik alınır ve yenilenir. `/Date(...)/` ve `PT12H30M` biçimleri otomatik çözülür.

### Excel

```csharp
using CSPLibraries.Excel;
using CSPLibraries.Excel.Datas;

var excel = new ExcelService();
var okuma = new ExcelReadOptions { SkipRowsBeforeHeader = 0 };

List<Urun> urunler = excel.ReadAs<Urun>("urunler.xlsx", 0, okuma);
excel.WriteExcel(urunler, "rapor.xlsx", "Ürünler");
```

> **Dikkat:** `ExcelReadOptions.SkipRowsBeforeHeader` varsayılan olarak **1**'dir (başlık satırından önce bir başlık/rapor satırı olduğu varsayılır). Başlık ilk satırdaysa `0` verin.

Sütunlar `[ExcelColumn("Başlık", Format = "#,##0.00", Width = 20, Order = 1)]` ile özelleştirilebilir.

### Veritabanı

```csharp
using CSPLibraries.Database;

var db = new DatabaseService("Server=.;Database=Erp;Integrated Security=true;TrustServerCertificate=true");

var aktifler = await db.QueryAsync<Musteri>("SELECT * FROM Musteri WHERE Sehir IN @Sehirler", new { Sehirler = new[] { "İzmir", "Bursa" } });
await db.BulkInsertAsync("dbo.Hareket", hareketler);
await db.InTransactionAsync(async (oturum, ct) =>
{
    await oturum.ExecuteAsync("UPDATE Stok SET Miktar = Miktar - @Adet WHERE Id = @Id", new { Id = 5, Adet = 2 }, ct);
});
```

PostgreSQL, MySQL, SQLite gibi diğer sağlayıcılar `DatabaseOptions.Provider = DatabaseProvider.Custom` ve `ProviderFactory` ile kullanılır.

### Loglama

```csharp
using CSPLibraries.Logging;
using CSPLibraries.Logging.Datas;

await using var logs = new LogService(new LogServiceOptions
{
    File = new FileLogOptions { Directory = "logs", Format = LogFileFormat.Json }
});

builder.Logging.AddProvider(logs);

using (logs.BeginCorrelation())
    logs.CreateLogger<Program>().LogInformation("Sipariş {OrderId} gönderildi", 42);
```

TCKN, IBAN, kart numarası, parola ve token değerleri loglarda otomatik maskelenir.

### E-posta

```csharp
using CSPLibraries.Mail;
using CSPLibraries.Mail.Datas;
using CSPLibraries.Mail.Models;

await using var mail = new MailService(new MailServiceOptions
{
    Host = "smtp.office365.com", Port = 587, UserName = "noreply@firma.com", Password = "...",
    FromAddress = "noreply@firma.com", FromName = "Entegrasyon"
});

await mail.SendAsync(new EmailMessage
{
    To = ["muhasebe@firma.com"],
    Subject = "Günlük rapor",
    HtmlBody = mail.RenderTemplate("<p>Toplam: {{Tutar}}</p>", new { Tutar = 1250.75m }),
    Attachments = [EmailAttachment.FromFile("rapor.xlsx")]
});
```

### Güvenlik

```csharp
using CSPLibraries.Security;
using CSPLibraries.Security.Datas;

var security = new SecurityService(new SecurityOptions
{
    EncryptionKey = anahtarBase64,
    JwtSigningKey = "en-az-32-karakterlik-gizli-anahtar-degeri"
});

string sifreli = security.Encrypt("gizli veri");
string hash = security.HashPassword("P@ssw0rd!");
string token = security.CreateJwt(new Dictionary<string, object?> { ["sub"] = "u42" });
bool gecerli = security.VerifyTotp(kullaniciSecret, "123456");
```

Yeni bir anahtar üretmek için `SecurityService.GenerateKey()` kullanılır. Anahtarı ortam değişkeninde veya bir kasada (vault) saklayın.

### Serileştirme

```csharp
using CSPLibraries.Serialization;
using CSPLibraries.Serialization.Datas;

var serializer = new SerializationService();

serializer.WriteCsvFile(satirlar, "export.csv", CsvOptions.ExcelTurkish);
foreach (var satir in serializer.ReadCsvFile<Satir>("import.csv"))
    Isle(satir);

var sonuc = serializer.ValidateXml(faturaXml, "UBL-Invoice-2.1.xsd");
```

### Tarih, metin ve koleksiyon yardımcıları

```csharp
using CSPLibraries.DateTimes;
using CSPLibraries.Enumerables;
using CSPLibraries.Strings;

var takvim = new DateTimeService();
DateOnly vade = takvim.AddBusinessDays(DateOnly.FromDateTime(DateTime.Today), 5);
DateTime slaBitis = takvim.AddWorkingTime(DateTime.Now, TimeSpan.FromHours(8));

bool tcknGecerli = "10000000146".IsValidTckn();
string yaziyla = 1234.50m.AmountToWordsTr(upperCase: true);

var enBuyukler = siparisler.TopBy(10, s => s.Tutar);
await siparisler.ProcessInBatchesAsync(500, async (parti, ct) => await GonderAsync(parti, ct));
```

## Lisans

MIT
