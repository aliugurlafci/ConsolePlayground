# LibPlayground

Kurumsal entegrasyon projeleri için birbirinden bağımsız, yüksek performanslı .NET servisleri: SAP OData, REST, Excel, veritabanı, e-posta, loglama, güvenlik, serileştirme, dosya sistemi, tarih/tatil, metin ve koleksiyon araçları.

- **Birbirinden bağımsız servisler:** Yalnızca ihtiyacınız olan servisi kullanırsınız; servisler birbirine referans vermez.
- **Uzun ömürlü kullanım:** Servisler thread-safe'dir ve uygulama boyunca tek örnek (singleton) olarak kullanılmak üzere tasarlanmıştır.
- **Türkçe desteği:** tr-TR kültürü, TCKN/VKN/IBAN doğrulama, Türkiye resmi ve dini tatilleri, tutarı yazıyla ifade etme.
- **Bellek dostu:** Büyük veriler akış halinde okunur/yazılır; satır eşleme için çalışma anında derlenmiş ifadeler kullanılır, satır başına reflection yapılmaz.
- **Çoklu hedef:** .NET 10, .NET 8, .NET 5 ve .NET Framework 4.8 için ayrı derlemeler, tüm hedeflerde aynı genel API.

## İçindekiler

- [Desteklenen çerçeveler](#desteklenen-çerçeveler)
- [Kurulum](#kurulum)
- [Servisler ve namespace'ler](#servisler-ve-namespaceler)
- [Genel kullanım ilkeleri](#genel-kullanım-ilkeleri)
- [REST](#rest)
- [SAP OData](#sap-odata)
- [Excel](#excel)
- [Veritabanı](#veritabanı)
- [E-posta](#e-posta)
- [Loglama](#loglama)
- [Güvenlik](#güvenlik)
- [Serileştirme](#serileştirme)
- [Dosya sistemi](#dosya-sistemi)
- [Tarih ve takvim](#tarih-ve-takvim)
- [Metin yardımcıları](#metin-yardımcıları)
- [Koleksiyon yardımcıları](#koleksiyon-yardımcıları)
- [Hedef çerçeve farkları](#hedef-çerçeve-farkları)
- [Derleme ve paketleme](#derleme-ve-paketleme)
- [Proje yapısı](#proje-yapısı)
- [Sürüm notları](#sürüm-notları)

## Desteklenen çerçeveler

| Hedef | Durum | Ek bağımlılıklar |
|---|---|---|
| `net10.0` | Birincil hedef, tüm özellikler | Yok |
| `net8.0` | Tam destek | `System.Text.Json`, `System.IO.Pipelines`, `Microsoft.Bcl.Memory` (10.0.x) |
| `net5.0` | Desteği sona ermiş çerçeve, tam işlev | `net8.0` bağımlılıklarına ek olarak `Portable.System.DateTimeOnly`, `Microsoft.Bcl.TimeProvider`, `System.Collections.Immutable`, `System.Threading.Channels`; `Microsoft.Data.SqlClient` 5.2.x |
| `net48` | Tam işlev, küçük farklarla | `net5.0` bağımlılıklarına ek olarak `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Bcl.Cryptography`, `System.Memory` |

Tüm hedeflerde ortak bağımlılıklar: `ExcelDataReader`, `ExcelDataReader.DataSet`, `Sylvan.Data.Excel`, `MailKit`, `Microsoft.Data.SqlClient` (net5.0 dışında 7.1.x), `Microsoft.Extensions.Logging.Abstractions`.

Eski çerçevelerde eksik olan BCL API'leri kütüphanenin içinde, dışarıya açılmayan (`internal`) yardımcılarla karşılanır; uygulamanıza ek tip veya extension metot sızmaz. Hedefler arasındaki davranış farkları [Hedef çerçeve farkları](#hedef-çerçeve-farkları) bölümünde listelenmiştir.

## Kurulum

```bash
dotnet add package LibPlayground
```

Paket, projenizin hedef çerçevesine uygun derlemeyi otomatik seçer. Örneğin `net6.0`/`net7.0` projeleri `net5.0` derlemesini, `net9.0` projeleri `net8.0` derlemesini kullanır.

## Servisler ve namespace'ler

| Servis | Namespace | Öne çıkanlar |
|---|---|---|
| `RestService` | `CSPLibraries.Rest` | Tekrar deneme, OAuth2 client credentials, JSON/XML, form, akışla indirme/yükleme |
| `SAPService` | `CSPLibraries.Sap` | CSRF token, OData v2/v4, `ODataQuery`, `$count`, sayfalama, MERGE, function import |
| `ExcelService` | `CSPLibraries.Excel` | xls/xlsx/xlsb/csv okuma, nesne eşleme, akış tabanlı xlsx yazma, çoklu sayfa |
| `DatabaseService` | `CSPLibraries.Database` | Sağlayıcıdan bağımsız ADO.NET, SqlBulkCopy, transaction, sayfalama, tekrar deneme |
| `MailService` | `CSPLibraries.Mail` | SMTP bağlantı havuzu, toplu gönderim, şablon, ek dosya, IMAP okuma |
| `LogService` | `CSPLibraries.Logging` | `ILoggerProvider`, dosya/konsol/veritabanı hedefleri, maskeleme, korelasyon |
| `SecurityService` | `CSPLibraries.Security` | AES-256-GCM, anahtar rotasyonu, PBKDF2, HMAC, JWT (HS256), TOTP |
| `SerializationService` | `CSPLibraries.Serialization` | CSV, sabit uzunluklu dosya, JSON/NDJSON, XML + XSD, XML↔JSON, sıkıştırma |
| `DirectoryService` | `CSPLibraries.Directories` | Hızlı tarama, kopyalama, atomik yazma, arşivleme, zip, izleme, yinelenen dosya bulma |
| `DateTimeService` | `CSPLibraries.DateTimes` | İş günü, resmi/dini tatiller, mesai saati hesabı, SAP tarih biçimleri |
| `StringService` | `CSPLibraries.Strings` | Türkçe metin, doğrulama, maskeleme, benzerlik (extension metotlar) |
| `EnumerableService` | `CSPLibraries.Enumerables` | Parti/paralel işleme, sayfalama, `TopBy`, pencere (extension metotlar) |

Her servisin yanında aynı kökte alt namespace'ler bulunur:

| Alt namespace | İçerik |
|---|---|
| `*.Datas` | Ayar sınıfları ve enum'lar (`RestServiceOptions`, `CsvOptions`, `FileQuery` …) |
| `*.Models` | Sonuç tipleri, öznitelikler ve istisnalar (`RestResponse`, `ExcelColumnAttribute`, `DatabaseServiceException` …) |
| `*.Interfaces` | Servis arayüzleri (`IRestService`, `IDbSession` …); bağımlılık enjeksiyonu ve test için |

## Genel kullanım ilkeleri

- **Tek örnek kullanın.** `RestService`, `SAPService`, `MailService`, `LogService` gibi bağlantı tutan servisler içeride havuz yönetir; her istek için yeni örnek oluşturmayın. DI kullanıyorsanız `AddSingleton` ile kaydedin.
- **Kaynakları serbest bırakın.** `RestService` ve `SAPService` `IDisposable`; `MailService` ve `LogService` `IAsyncDisposable`'dır. Uygulama kapanırken dispose edin; `LogService` dispose sırasında kuyruktaki logları yazar.
- **İptal belirteci verin.** Tüm async metotlar son parametre olarak `CancellationToken` alır.
- **İstisnalar servise özeldir.** Her servis kendi istisna tipini fırlatır (`RestServiceException`, `SAPServiceException`, `DatabaseServiceException`, `MailServiceException`, `SecurityServiceException`, `SerializationServiceException`, `ExcelServiceException`). HTTP istisnaları `HttpStatusCode`, `Endpoint` ve `ResponseContent` bilgisini taşır.
- **Zaman sağlayıcısı enjekte edilebilir.** `DateTimeService`, `SecurityService` ve `DirectoryService` isteğe bağlı bir `TimeProvider` alır; testlerde sabit zaman vermek için kullanılabilir.

## REST

```csharp
using CSPLibraries.Rest;
using CSPLibraries.Rest.Datas;
using CSPLibraries.Rest.Models;

using var api = new RestService(new RestServiceOptions
{
    BaseUrl = "https://api.firma.com/v1",
    TimeoutSeconds = 30,
    MaxRetries = 2
});

api.UseClientCredentials("https://auth.firma.com/oauth/token", "clientId", "secret", scope: "orders.read");

List<Siparis>? acik = await api.GetAsync<List<Siparis>>("orders", new { durum = "acik", sayfa = 1, depolar = new[] { 1, 2 } });
Siparis? yeni = await api.PostAsync<Siparis>("orders", new Siparis { No = "S-1", Tutar = 125.50m });
await api.PatchAsync("orders/S-1", new { Durum = "Kapali" });
await api.DeleteAsync("orders/S-1");

bool varMi = await api.ExistsAsync("orders/S-1");
string ham = await api.GetAsync("health");

try
{
    await api.GetAsync<Siparis>("orders/YOK");
}
catch (RestServiceException ex) when (ex.HttpStatusCode == 404)
{
    Console.WriteLine(ex.ResponseContent);
}

public sealed class Siparis
{
    public string No { get; set; } = "";
    public decimal Tutar { get; set; }
    public DateOnly Tarih { get; set; }
}
```

**Ayarlar (`RestServiceOptions`)**

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `BaseUrl` | (zorunlu) | Mutlak http/https adresi; query string veya fragment içeremez |
| `TimeoutSeconds` | 30 | Tek bir denemenin süre sınırı |
| `MaxRetries` | 2 | Geçici hatalarda tekrar sayısı (en fazla 10) |
| `DefaultFormat` | `Json` | Gövde serileştirme biçimi (`Json` / `Xml`) |
| `FollowRedirects` | `false` | Yönlendirmeleri izleme (en fazla 5) |
| `AcceptAnyServerCertificate` | `false` | Sertifika doğrulamasını kapatır; yalnızca test ortamı için |
| `ResponseFallbackEncoding` | UTF-8 | Yanıt `charset` belirtmediğinde kullanılacak kodlama |
| `JsonOptions` | Gevşek varsayılanlar | Kendi `JsonSerializerOptions` nesneniz |
| `UserAgent` | `CSPLibraries-RestService/3.0` | İstek başlığı |

**Kimlik doğrulama ve başlıklar:** `UseBasicAuth`, `UseBearerToken(string)`, `UseBearerToken(Func<CancellationToken, Task<string?>>)` (her istekte token üretir), `UseClientCredentials` (OAuth2; token önbelleğe alınır ve süresi dolmadan 60 sn önce yenilenir), `UseApiKey(başlık, değer)`, `SetDefaultHeader`, `ClearAuthentication`. Metotlar zincirlenebilir.

**Davranış ayrıntıları**

- Sorgu parametreleri anonim nesne, `IDictionary` veya `KeyValuePair` listesiyle verilir; diziler tekrar eden parametreye (`depolar=1&depolar=2`), tarihler ISO biçimine çevrilir.
- Gövde olarak `string` olduğu gibi, `byte[]` ham, diğer nesneler JSON (veya `ContentFormat.Xml` ile XML) olarak gönderilir.
- Yanıt `Content-Type`'a göre JSON veya XML olarak çözülür; baştaki BOM/boşluklar tolere edilir, boş yanıt `default` döner. JSON akış halinde okunur.
- Yalnızca idempotent istekler (GET, PUT, DELETE, HEAD, OPTIONS) 408, 429, 500, 502, 503, 504 yanıtlarında ve bağlantı hatalarında tekrar denenir. Bekleme 500 ms'den başlayıp 10 sn'ye kadar katlanır (±%20 rastgelelik); `Retry-After` başlığına (en fazla 30 sn) uyulur.

**Dosya, form ve ham yanıt**

```csharp
using CSPLibraries.Rest;
using CSPLibraries.Rest.Models;

using var api = new RestService("https://api.firma.com/v1");

long boyut = await api.DownloadFileAsync("reports/42", @"C:\raporlar\42.pdf");
using (var hedef = new MemoryStream())
    await api.DownloadAsync("reports/42", hedef);

string yukleme = await api.UploadFileAsync("files", @"C:\raporlar\42.pdf", fieldName: "file",
                                           fields: new Dictionary<string, string> { ["aciklama"] = "Aylık rapor" });

string form = await api.PostFormAsync("login", new { kullanici = "ali", sifre = "***" });

RestResponse yanit = await api.SendAsync(HttpMethod.Get, "orders/42", query: new { detay = true },
                                         headers: new Dictionary<string, string> { ["X-Kaynak"] = "erp" });
if (yanit.IsSuccess)
    Console.WriteLine(yanit.AsJson()["no"]);
else
    Console.WriteLine($"{yanit.StatusCode} {yanit.Content}");
```

`DownloadFileAsync` önce geçici bir `.part` dosyasına yazar, tamamlandığında hedefi atomik olarak değiştirir. `RestResponse`; `StatusCode`, `Content`, `ContentType`, `Headers`, `As<T>()`, `AsJson()`, `AsXml()` ve `EnsureSuccess()` sunar.

## SAP OData

```csharp
using CSPLibraries.Sap;
using CSPLibraries.Sap.Datas;
using CSPLibraries.Sap.Models;

using var sap = new SAPService(new SapServiceOptions
{
    BaseUrl = "https://sap.firma.com/sap/opu/odata/sap/ZSD_ORDER_SRV",
    UserName = "RFC_USER",
    Password = "***",
    SapClient = "100",
    SapLanguage = "TR"
});

ODataQuery sorgu = ODataQuery.Create()
    .WhereEquals("Plant", "1000")
    .WhereIn("Status", ["A", "B"])
    .Select("OrderId", "Amount", "CreatedAt")
    .OrderBy("CreatedAt", descending: true)
    .Take(500);

List<SapSiparis> liste = await sap.GetODataListAsync<SapSiparis>("OrderSet", sorgu);
ODataResult<SapSiparis> sayfa = await sap.GetODataPageAsync<SapSiparis>("OrderSet", sorgu.WithInlineCount());
long adet = await sap.GetODataCountAsync("OrderSet", "Plant eq '1000'");
SapSiparis? tek = await sap.GetODataAsync<SapSiparis>("OrderSet('4500001234')");

await foreach (SapSiparis siparis in sap.StreamODataAsync<SapSiparis>("OrderSet"))
    Console.WriteLine(siparis.OrderId);

await sap.PostAsync("OrderSet", new { OrderId = "4500001235", Amount = 10m });
await sap.MergeAsync("OrderSet('4500001234')", new { Status = "C" });
await sap.DeleteAsync("OrderSet('4500001234')");

decimal? fiyat = await sap.CallFunctionAsync<decimal?>("GetPrice", new Dictionary<string, object?> { ["Material"] = "M-01" });

public sealed class SapSiparis
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public TimeSpan CreatedTime { get; set; }
}
```

**Ayarlar (`SapServiceOptions`)**

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `BaseUrl` | (zorunlu) | OData servis kökü |
| `UserName` / `Password` | — | Basic kimlik doğrulama (sonradan `UseBasicAuth` / `UseBearerToken` da kullanılabilir) |
| `SapClient` / `SapLanguage` | — | Her isteğe `sap-client` / `sap-language` parametresi olarak eklenir |
| `DefaultFormat` | `Json` | İstek/yanıt biçimi |
| `TimeoutSeconds` / `MaxRetries` | 30 / 2 | Süre sınırı ve tekrar sayısı |
| `EnableCsrfToken` | `true` | Değiştiren isteklerde CSRF token'ı otomatik alır |
| `CsrfFetchEndpoint` | Servis kökü | Token'ın alınacağı adres |
| `ValidateResponseFormat` | `false` | Yanıt biçimi beklenenden farklıysa hata verir |
| `AcceptAnyServerCertificate`, `FollowRedirects`, `ResponseFallbackEncoding`, `JsonOptions`, `UserAgent` | — | REST servisiyle aynı anlamda |

**Davranış ayrıntıları**

- POST/PUT/PATCH/MERGE/DELETE isteklerinden önce `X-CSRF-Token: Fetch` ile token alınır, oturum çerezleri saklanır; sunucu `403 Required` dönerse token yenilenip istek bir kez tekrarlanır.
- OData v2 (`d/results`, `__next`, `__count`) ve v4 (`value`, `@odata.nextLink`, `@odata.count`) yanıtları desteklenir. `GetODataListAsync` sonraki sayfa bağlantılarını izler (`followNextLinks: false` ile kapatılabilir).
- `/Date(1700000000000)/`, `/Date(1700000000000+0180)/` ve ISO tarih biçimleri `DateTime`/`DateTimeOffset`'e; `PT12H30M` biçimi `TimeSpan`'e otomatik çözülür.
- `MergeAsync`, SAP'nin beklediği `X-HTTP-Method: MERGE` başlığıyla gönderilir.
- `ODataQuery.Literal` değerleri OData sözdizimine çevirir (metinlerde tırnak kaçışı dahil).

## Excel

```csharp
using CSPLibraries.Excel;
using CSPLibraries.Excel.Datas;
using CSPLibraries.Excel.Models;

var excel = new ExcelService();
var okuma = new ExcelReadOptions { SkipRowsBeforeHeader = 0 };

List<Urun> urunler = excel.ReadAs<Urun>("urunler.xlsx", 0, okuma);
List<Urun> ikinciSayfa = excel.ReadAs<Urun>("urunler.xlsx", "Stok", okuma);

foreach (Urun urun in excel.EnumerateAs<Urun>(ExcelSource.FromFile("buyuk.xlsx"), 0, okuma))
    Console.WriteLine(urun.Kod);

System.Data.DataTable tablo = excel.ReadWorksheet("urunler.xlsx", 0, okuma);
List<string> sayfalar = excel.GetWorksheetNames("urunler.xlsx");
List<Dictionary<string, object?>> satirlar = excel.ReadAsDictionaries("urunler.xlsx", 0, okuma);

excel.WriteExcel(urunler, "rapor.xlsx", "Ürünler");
byte[] dosya = excel.WriteExcelToBytes(urunler, "Ürünler", new ExcelWriteOptions { DateFormat = "dd.MM.yyyy", FreezeHeader = true });
excel.WriteWorkbook([ExcelSheet.Create("Ürünler", urunler), ExcelSheet.Create(tablo, "Ham Veri")], "kitap.xlsx");

public sealed class Urun
{
    [ExcelColumn("Ürün Kodu", Order = 1, Width = 14)]
    public string Kod { get; set; } = "";

    [ExcelColumn("Ürün Adı", Order = 2)]
    public string Ad { get; set; } = "";

    [ExcelColumn("Fiyat", Format = "#,##0.00", Order = 3)]
    public decimal Fiyat { get; set; }

    [ExcelColumn("Son Kullanma", Order = 4)]
    public DateOnly? SonKullanma { get; set; }

    [ExcelColumn(Ignore = true)]
    public string? IcNot { get; set; }
}
```

> **Dikkat:** `ExcelReadOptions.SkipRowsBeforeHeader` varsayılan olarak **1**'dir (başlık satırından önce bir başlık/rapor satırı olduğu varsayılır). Başlık ilk satırdaysa `0` verin.

- **Kaynaklar:** `ExcelSource` dosya yolu, `byte[]` veya `Stream` alır (örtük dönüşümler vardır). Konumlanamayan akışlar belleğe alınır. Biçim uzantıdan veya dosya imzasından anlaşılır (`.xls`, `.xlsx`, `.xlsm`, `.xlsb`, `.csv`); `ExcelReadOptions.Format` ile zorlanabilir.
- **Eşleme:** Başlıklar `[ExcelColumn("Ad")]` adına ya da özellik adına göre eşlenir; büyük/küçük harf, boşluk, `_`, `-` ve `.` farkları yok sayılır. `UseHeaderRow = false` ise `[ExcelColumn(Index = n)]` kullanılır. `DateOnly`, `TimeOnly`, `DateTime`, sayısal tipler, `bool`, `enum` ve `Nullable` türleri desteklenir.
- **Okuma ayarları:** `UseHeaderRow`, `SkipRowsBeforeHeader`, `TrimValues`, `ConvertValuesToString`, `SkipEmptyRows`, `FillMergedCells`, `Culture`, `Password`, `CsvEncoding`.
- **Yazma:** xlsx dosyası akış halinde üretilir; başlık dondurma, otomatik filtre, otomatik sütun genişliği, sayı/tarih biçimleri desteklenir. `MaxRowsPerSheet` (varsayılan 1.048.576) aşıldığında veri yeni sayfalara bölünür. Yazma ayarları: `IncludeHeader`, `FreezeHeader`, `AutoFilter`, `AutoFitColumns`, `AutoFitSampleRows`, `MinColumnWidth`, `MaxColumnWidth`, `DateFormat` (`dd.MM.yyyy`), `DateTimeFormat`, `TimeFormat`, `DecimalFormat`, `CompressionLevel`.
- Async karşılıklar: `ReadExcelFileAsync`, `ReadWorksheetAsync`, `ReadAsAsync`, `WriteWorkbookAsync`.

## Veritabanı

```csharp
using System.Data;
using CSPLibraries.Database;
using CSPLibraries.Database.Datas;
using CSPLibraries.Database.Models;

var db = new DatabaseService(new DatabaseOptions
{
    ConnectionString = "Server=.;Database=Erp;Integrated Security=true;TrustServerCertificate=true",
    CommandTimeoutSeconds = 30,
    MaxRetries = 3,
    OnCommandExecuted = log => Console.WriteLine($"{log.Elapsed.TotalMilliseconds:F0} ms  {log.CommandText}")
});

List<Musteri> aktifler = await db.QueryAsync<Musteri>(
    "SELECT * FROM Musteri WHERE Sehir IN @Sehirler AND KayitTarihi >= @Baslangic",
    new { Sehirler = new[] { "İzmir", "Bursa" }, Baslangic = new DateOnly(2025, 1, 1) });

Musteri? musteri = await db.QueryFirstOrDefaultAsync<Musteri>("SELECT * FROM Musteri WHERE Id = @Id", new { Id = 5 });
int adet = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Musteri");
int degisen = await db.ExecuteAsync("UPDATE Musteri SET Aktif = 0 WHERE Id = @Id", new { Id = 5 });

DbPagedResult<Musteri> sayfa = await db.QueryPagedAsync<Musteri>("SELECT * FROM Musteri", "Ad", pageNumber: 2, pageSize: 50);
var (musteriler, siparisler) = await db.QueryMultipleAsync<Musteri, int>("SELECT * FROM Musteri; SELECT Id FROM Siparis");

await foreach (Musteri m in db.QueryStreamAsync<Musteri>("SELECT * FROM Musteri"))
    Console.WriteLine(m.Ad);

long eklenen = await db.BulkInsertAsync("dbo.Musteri", aktifler, new BulkInsertOptions { BatchSize = 10_000, TableLock = true });

var parametreler = new DbParameterBag()
    .Add("Id", 5)
    .AddOutput("Toplam", DbType.Decimal)
    .AddReturnValue();
await db.ExecuteAsync(DbCommandText.Procedure("dbo.MusteriToplam"), parametreler);
decimal? toplam = parametreler.Get<decimal>("Toplam");

await db.InTransactionAsync(async (oturum, ct) =>
{
    await oturum.ExecuteAsync("UPDATE Stok SET Miktar = Miktar - @Adet WHERE Id = @Id", new { Id = 5, Adet = 2 }, ct);
    await oturum.ExecuteAsync("INSERT INTO Hareket (StokId, Adet) VALUES (@Id, @Adet)", new { Id = 5, Adet = -2 }, ct);
});

public sealed class Musteri
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string? Sehir { get; set; }
    public DateOnly KayitTarihi { get; set; }
    public bool Aktif { get; set; }
}
```

**Parametreler:** Anonim nesne, `IDictionary`, `KeyValuePair<string, object?>` listesi, `DbParameter` veya `DbParameterBag` verilebilir. Dizi/liste değerleri `IN @Liste` ifadesinde otomatik olarak `(@Liste0, @Liste1, …)` biçimine açılır. `DbParameterBag`; çıkış parametreleri (`AddOutput`), dönüş değeri (`AddReturnValue`), tablo değerli parametreler (`AddTableValued`) ve tip/boyut/hassasiyet ayarlarını destekler. Komut tipi ve süre sınırı `DbCommandText.Sql(...)` / `DbCommandText.Procedure(...)` ile verilir; düz `string` de kabul edilir.

**Eşleme:** Kolonlar özellik adına veya `[Column("ad")]` özniteliğine göre eşlenir (`_`, boşluk, `-` ve harf büyüklüğü yok sayılır); `[NotMapped]` alanlar atlanır. Tek kolonlu sorgular doğrudan skaler tiplere (`QueryAsync<int>`) eşlenebilir. Kurucu parametreli tipler (record) da desteklenir.

**Sorgu metotları:** `QueryAsync`, `QueryFirstOrDefaultAsync`, `QuerySingleAsync`, `QueryStreamAsync`, `QueryDataTableAsync`, `QueryMultipleAsync` (2 veya 3 sonuç kümesi), `ExecuteReaderAsync` (kendi okuma kodunuz), `ExistsAsync`, `QueryPagedAsync` (SQL Server'da `OFFSET/FETCH`, diğerlerinde `LIMIT/OFFSET`), `ExecuteAsync`, `ExecuteScalarAsync`, `ExecuteManyAsync` (aynı komutu çok parametre kümesiyle tek transaction içinde çalıştırır).

**Toplu ekleme:** SQL Server'da `SqlBulkCopy` kullanılır (`BatchSize` 5.000, `TimeoutSeconds` 600, `TableLock`, `KeepIdentity`, `KeepNulls`, `CheckConstraints`, `FireTriggers`, `ColumnMappings`). Diğer sağlayıcılarda çok satırlı `INSERT` ifadeleri üretilir (`RowsPerStatement` 100, `MaxParametersPerStatement` 1.000).

**Transaction:** `InTransactionAsync` hata olduğunda geri alır, başarılı olunca onaylar. Daha fazla kontrol için `BeginTransactionAsync` veya `OpenSessionAsync` ile bir `IDbSession` alıp `CommitAsync`/`RollbackAsync` çağırabilirsiniz; oturum `await using` ile kapatılmalıdır.

**Tekrar deneme:** Bağlantı açılırken geçici SQL Server hatalarında, komut çalışırken yalnızca güvenli hata kodlarında (deadlock 1205 ve Azure SQL kısıtlama kodları) `MaxRetries` (varsayılan 3, en fazla 10) kez, 200 ms'den 5 sn'ye kadar artan beklemeyle tekrar denenir. Transaction içindeki komutlar tekrar denenmez.

**Diğer sağlayıcılar:** PostgreSQL, MySQL, SQLite, Oracle gibi sağlayıcılar `Provider = DatabaseProvider.Custom` ve `ProviderFactory` ile kullanılır; `ParameterPrefix` ile `@` dışındaki önekler (`:`) seçilebilir.

## E-posta

```csharp
using System.Globalization;
using CSPLibraries.Mail;
using CSPLibraries.Mail.Datas;
using CSPLibraries.Mail.Models;

await using var mail = new MailService(new MailServiceOptions
{
    Host = "smtp.office365.com",
    Port = 587,
    Security = MailSecurity.Auto,
    UserName = "noreply@firma.com",
    Password = "***",
    FromAddress = "noreply@firma.com",
    FromName = "Entegrasyon"
});

string govde = mail.RenderTemplate("<p>Sayın {{Musteri.Ad}}, toplam: {{Tutar}}</p>{{{Imza}}}",
                                   new { Musteri = new { Ad = "Ali Veli" }, Tutar = 1250.75m, Imza = "<b>Muhasebe</b>" },
                                   CultureInfo.GetCultureInfo("tr-TR"));

MailSendResult sonuc = await mail.SendAsync(new EmailMessage
{
    To = ["muhasebe@firma.com; finans@firma.com"],
    Cc = ["yonetim@firma.com"],
    Subject = "Günlük rapor",
    HtmlBody = govde,
    Priority = MailPriority.High,
    Attachments =
    [
        EmailAttachment.FromFile(@"C:\raporlar\rapor.xlsx"),
        EmailAttachment.FromBytes("ozet.txt", System.Text.Encoding.UTF8.GetBytes("Özet")),
        EmailAttachment.Inline("logo", "logo.png", File.ReadAllBytes("logo.png"))
    ],
    Headers = new Dictionary<string, string> { ["X-Kaynak"] = "ERP" }
});

BulkMailResult toplu = await mail.SendBulkAsync(
    new[] { "a@firma.com", "b@firma.com" }.Select(adres => new EmailMessage { To = [adres], Subject = "Duyuru", TextBody = "Merhaba" }));
Console.WriteLine($"{toplu.Succeeded} gönderildi, {toplu.Failed} hatalı");
```

- **Adresler:** `To`, `Cc`, `Bcc` listelerinde `;` veya `,` ile ayrılmış birden fazla adres yazılabilir; geçersiz adres `MailServiceException` fırlatır.
- **Gövde:** Yalnızca `HtmlBody` verilirse düz metin karşılığı otomatik üretilir. HTML içinde `cid:logo` ile satır içi görsellere başvurulur.
- **Şablon:** `{{Yol.Alt}}` değeri HTML kodlayarak, `{{{Yol}}}` ham olarak yazar; sayı ve tarihler verilen kültürle biçimlenir. Ayrıştırılan şablonlar önbelleğe alınır.
- **Bağlantılar:** SMTP bağlantıları havuzda tutulur (`MaxConnections` 4); geçici hatalarda `MaxRetries` (3) kez tekrar denenir. `PickupDirectory` verilirse e-postalar SMTP yerine bu klasöre `.eml` olarak yazılır (test ortamları için).
- **IMAP:** `ImapHost`, `ImapPort` (993), `ImapUserName`, `ImapPassword` ayarlandığında `FetchUnreadAsync(folder, maxCount, markAsRead, attachmentDirectory)` okunmamış iletileri ekleriyle getirir; `MoveToFolderAsync` iletileri taşır.
- `TestConnectionAsync` SMTP bağlantısını ve kimlik bilgilerini doğrular.

## Loglama

```csharp
using CSPLibraries.Logging;
using CSPLibraries.Logging.Datas;
using CSPLibraries.Logging.Workers;
using Microsoft.Extensions.Logging;

await using var logs = new LogService(new LogServiceOptions
{
    ApplicationName = "Entegrasyon",
    MinimumLevel = LogLevel.Information,
    CategoryLevels = new Dictionary<string, LogLevel> { ["Microsoft"] = LogLevel.Warning },
    EnableConsole = true,
    File = new FileLogOptions { Directory = "logs", Format = LogFileFormat.Json, RetentionDays = 30, CompressRolledFiles = true }
}, additionalSinks: [new DatabaseLogSink("Server=.;Database=Log;Integrated Security=true;TrustServerCertificate=true", "dbo.AppLog", "Entegrasyon")]);

ILogger<Siparis> logger = logs.CreateLogger<Siparis>();

using (logs.BeginCorrelation())
using (logs.BeginScope(new Dictionary<string, object?> { ["OrderId"] = 42 }))
{
    logger.LogInformation("Sipariş {OrderId} gönderildi", 42);
    logs.Warning("Müşteri TCKN {Tckn} için IBAN {Iban} eksik", "10000000146", "TR33 0006 1005 1978 6457 8413 26");
}

using (logs.TimeOperation("Gece aktarımı", warningThreshold: TimeSpan.FromMinutes(5)))
    await Task.Delay(100);

int sonuc = await logs.TimeAsync("Fiyat güncelleme", async ct => { await Task.Delay(10, ct); return 1; });
await logs.FlushAsync();

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(logs));

public sealed class Siparis;
```

- **Entegrasyon:** `LogService` bir `ILoggerProvider`'dır; ASP.NET Core'da `builder.Logging.AddProvider(logs)` ile eklenir. Doğrudan kullanım için `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` metotları vardır.
- **Hedefler:** Dosya (`FileLogOptions`: metin veya JSON satırları, günlük ve `MaxFileSizeBytes` (100 MB) ile dönen dosyalar, `RetentionDays` (30) sonrası temizlik, isteğe bağlı gzip), konsol (`EnableConsole`, renkli), veritabanı (`DatabaseLogSink`; SQL Server'da `SqlBulkCopy`, diğerlerinde transaction içinde `INSERT`). Tablo oluşturma betiği için `DatabaseLogSink.GetSqlServerCreateTableScript("dbo.AppLog")` kullanılır. Kendi hedefiniz için `ILogSink` uygulayın veya `DelegateLogSink` kullanın.
- **Performans:** Loglar sınırlı bir kuyruğa (`QueueCapacity` 100.000) yazılır ve arka planda `BatchSize` (1.000) parti halinde işlenir; kuyruk dolduğunda `QueueFullMode` (varsayılan en eskiyi at) uygulanır, atılan kayıt sayısı `DroppedCount` ile izlenir ve log'a uyarı olarak düşülür.
- **Maskeleme:** `MaskSensitiveData` (varsayılan açık) TCKN, IBAN, Luhn geçerli kart numaraları, `password=`/`token=`/`api_key=` benzeri değerler ve `Bearer` token'larını mesajlarda ve yapısal özelliklerde maskeler.
- **Korelasyon:** `BeginCorrelation()` yeni bir kimlik üretir (veya verileni kullanır), iç içe çağrılar ve async akış boyunca taşınır; `CurrentCorrelationId` ile okunur.

## Güvenlik

```csharp
using System.Text;
using CSPLibraries.Security;
using CSPLibraries.Security.Datas;

string anahtar = SecurityService.GenerateKey();

var security = new SecurityService(new SecurityOptions
{
    EncryptionKey = anahtar,
    PreviousEncryptionKeys = [],
    HmacKey = "webhook-gizli-anahtari",
    JwtSigningKey = "en-az-32-karakterlik-gizli-anahtar-degeri",
    JwtIssuer = "erp",
    JwtAudience = "mobil",
    JwtLifetime = TimeSpan.FromHours(1)
});

string sifreli = security.Encrypt("gizli veri", associatedData: "musteri:42");
string acik = security.Decrypt(sifreli, associatedData: "musteri:42");
byte[] ikili = security.Encrypt(Encoding.UTF8.GetBytes("bayt dizisi"));

string hash = security.HashPassword("P@ssw0rd!");
PasswordVerificationResult dogrulama = security.VerifyPassword("P@ssw0rd!", hash);
if (dogrulama == PasswordVerificationResult.SuccessRehashNeeded)
    hash = security.HashPassword("P@ssw0rd!");

string imza = security.ComputeHmac("{\"olay\":\"odeme\"}");
bool imzaGecerli = security.VerifyHmac("{\"olay\":\"odeme\"}", "sha256=" + imza);
string sha = security.ComputeHash("metin", HashAlgorithmType.Sha512);

string token = security.CreateJwt(new Dictionary<string, object?> { ["sub"] = "u42", ["rol"] = "admin" });
var jwt = security.ValidateJwt("Bearer " + token);
Console.WriteLine(jwt.IsValid ? jwt.Subject : jwt.Error);

string secret = security.GenerateTotpSecret();
string qrUri = security.GetTotpUri(secret, "ali@firma.com", "Firma");
bool kodGecerli = security.VerifyTotp(secret, "123456");

string parola = security.GeneratePassword(16);
string kod = security.GenerateNumericCode(6);
string rastgele = security.GenerateToken();
```

- **Şifreleme:** AES-256-GCM; her şifrelemede rastgele nonce, isteğe bağlı ilişkili veri (`associatedData`) ile bağlam doğrulaması. Çıktı Base64Url'dir ve anahtar kimliği içerir; anahtarı değiştirirken eskisini `PreviousEncryptionKeys`'e ekleyerek eski verileri çözmeye devam edebilirsiniz. Değiştirilmiş veri `SecurityServiceException` ile reddedilir; `TryDecrypt` hata fırlatmaz.
- **Parola:** PBKDF2-SHA256, 600.000 iterasyon (`PasswordIterations`), 16 bayt tuz; çıktı `PBKDF2-SHA256$iterasyon$tuz$hash` biçimindedir. Daha düşük iterasyonla üretilmiş hash'ler `SuccessRehashNeeded` döner. Bir paroladan sabit anahtar türetmek için `SecurityService.DeriveKey(parola, tuz)` kullanılır.
- **HMAC/hash:** HMAC-SHA256 çıktısı küçük harf hex'tir; `VerifyHmac` hex, `sha256=` önekli hex ve Base64 imzaları sabit zamanlı karşılaştırır. `ComputeHash`/`ComputeHashAsync`/`ComputeFileHashAsync` SHA-256/384/512, SHA-1 ve MD5 destekler.
- **JWT:** HS256; `JwtSigningKey` en az 32 bayt olmalıdır. Süre, `iss`/`aud` ve imza doğrulanır, saat sapması `JwtClockSkew` (2 dk) kadar tolere edilir.
- **TOTP:** RFC 6238 (SHA-1, 6 hane, 30 sn), varsayılan ±1 adım sapma; `GetTotpUri` doğrulayıcı uygulamalar için `otpauth://` adresi üretir.
- **Rastgele değerler:** Kriptografik üreteç kullanılır; `GeneratePassword` her karakter grubundan en az bir karakter içerir ve karışabilen karakterleri (`0/O`, `1/l/I`) dışarıda bırakır.

## Serileştirme

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Compression;
using CSPLibraries.Serialization;
using CSPLibraries.Serialization.Datas;
using CSPLibraries.Serialization.Models;

var serializer = new SerializationService();

var satirlar = new List<Satir> { new() { Kod = "A1", Tutar = 12.5m, Tarih = new DateOnly(2025, 10, 20) } };

serializer.WriteCsvFile(satirlar, "export.csv", CsvOptions.ExcelTurkish);
foreach (Satir satir in serializer.ReadCsvFile<Satir>("export.csv", CsvOptions.ExcelTurkish))
    Console.WriteLine(satir.Kod);

string csv = serializer.ToCsv(satirlar, new CsvOptions { Delimiter = '|', QuoteAllFields = true });
List<Satir> geri = serializer.FromCsv<Satir>(csv, new CsvOptions { Delimiter = '|' });

string sabit = serializer.ToFixedWidth(new[] { new SabitSatir { Kod = "A1", Miktar = 42 } });
List<SabitSatir> okunan = serializer.FromFixedWidth<SabitSatir>(sabit);

string json = serializer.ToJson(satirlar, indented: true);
List<Satir>? jsonGeri = serializer.FromJson<List<Satir>>(json);

using (var ndjson = File.Create("satirlar.ndjson"))
    await serializer.WriteJsonLinesAsync(satirlar, ndjson);
using (var kaynak = File.OpenRead("satirlar.ndjson"))
{
    await foreach (Satir? satir in serializer.ReadJsonLinesAsync<Satir>(kaynak))
        Console.WriteLine(satir?.Kod);
}

string xml = serializer.ToXml(new Fatura { No = "F-1" });
Fatura? fatura = serializer.FromXml<Fatura>(xml);
XmlValidationResult sonuc = serializer.ValidateXml(xml, "Fatura.xsd");
string xmlJson = serializer.XmlToJson("<fatura><no>F-1</no></fatura>");

byte[] sikistirilmis = serializer.Compress(System.Text.Encoding.UTF8.GetBytes(json), CompressionFormat.GZip, CompressionLevel.Optimal);
string arsiv = await serializer.CompressFileAsync("export.csv", CompressionFormat.ZLib, deleteSource: true);

public sealed class Satir
{
    [Column("Ürün Kodu", Order = 1)]
    public string Kod { get; set; } = "";

    [Column(Order = 2)]
    public decimal Tutar { get; set; }

    public DateOnly Tarih { get; set; }

    [NotMapped]
    public string? Hesaplanan { get; set; }
}

public sealed class SabitSatir
{
    [FixedWidthField(1, 10)]
    public string Kod { get; set; } = "";

    [FixedWidthField(11, 8, Alignment = FieldAlignment.Right, PadChar = '0')]
    public int Miktar { get; set; }
}

public sealed class Fatura
{
    public string No { get; set; } = "";
}
```

- **CSV:** Başlıklı/başlıksız, ayraç seçimi, tırnak kaçışı, çok satırlı alanlar, BOM, kültüre göre sayı/tarih biçimi. `CsvOptions.ExcelTurkish` Türkçe Excel ile uyumludur (`;`, tr-TR, BOM, `dd.MM.yyyy`). `EscapeFormulas` `=`, `+`, `-`, `@` ile başlayan metinleri CSV enjeksiyonuna karşı kaçışlar. Sütun adı/sırası `[Column]`, hariç tutma `[NotMapped]` ile verilir; okumada başlıklar boşluk, `_`, `-` ve harf büyüklüğü yok sayılarak eşlenir. Ek araçlar: `ReadCsvRecords` (ham kayıtlar), `ReadCsvToDataTable`, `DetectCsvDelimiter`, `WriteCsvAsync`.
- **Sabit uzunluk:** `[FixedWidthField(start, length)]` — `start` 1'den başlar; `Alignment`, `PadChar`, `Format` ayarlanabilir. Çakışan alanlar hata verir; `FixedWidthOptions.TruncateOverflow` taşan değerleri keser.
- **JSON:** System.Text.Json; varsayılan ayarlar büyük/küçük harfe duyarsız okuma, yorum ve sondaki virgüle izin, metin içindeki sayıları okuma ve Türkçe karakterleri kaçışsız yazma içerir. NDJSON için `WriteJsonLinesAsync` (`IEnumerable` veya `IAsyncEnumerable`) ve `ReadJsonLinesAsync` kullanılır.
- **XML:** `XmlSerializer` tabanlı; DTD işleme kapalıdır. `ValidateXml` bir veya daha fazla XSD ile doğrular ve satır/sütun bilgili hata listesi döner. `XmlToJson` / `JsonToXml` dönüşümleri öznitelikleri `@ad`, metni `#text` olarak taşır.
- **Sıkıştırma:** GZip, Deflate, ZLib ve Brotli (.NET Framework 4.8 hariç); bellekte (`Compress`/`Decompress`), akışta (`CompressAsync`/`DecompressAsync`) veya dosyada (`CompressFileAsync`).

## Dosya sistemi

```csharp
using System.IO.Compression;
using CSPLibraries.Directories;
using CSPLibraries.Directories.Datas;
using CSPLibraries.Directories.Models;

var files = new DirectoryService(new DirectoryServiceOptions { MaxRetries = 3, MaxParallelCopies = 4 });

List<FileEntry> eskiLoglar = files.GetFiles(@"C:\uygulama\logs", new FileQuery
{
    Recursive = true,
    Extensions = ["log", "txt"],
    OlderThan = TimeSpan.FromDays(30),
    MinSizeBytes = 1
});

int silinen = await files.DeleteFilesAsync(@"C:\uygulama\logs", new FileQuery { Recursive = true, Extensions = ["tmp"] });
DirectoryCopyResult kopya = await files.CopyDirectoryAsync(@"C:\kaynak", @"D:\yedek", overwrite: false);
await files.WriteAllTextAtomicAsync(@"C:\uygulama\durum.json", "{\"son\":\"2025-10-20\"}");
string arsivYolu = await files.ArchiveFileAsync(@"C:\gelen\fatura.xml", @"C:\arsiv");
await files.CreateZipAsync(@"C:\kaynak", @"C:\paket\kaynak.zip", CompressionLevel.Optimal);
await files.ExtractZipAsync(@"C:\paket\kaynak.zip", @"C:\acilan", overwrite: true);

List<DuplicateFileGroup> kopyalar = await files.FindDuplicateFilesAsync(@"C:\belgeler");
Console.WriteLine($"{kopyalar.Sum(g => g.WastedBytes)} bayt boşa kullanılıyor");

if (await files.WaitForFileReadyAsync(@"C:\gelen\buyuk.csv", TimeSpan.FromMinutes(1)))
    Console.WriteLine("Dosya kullanıma hazır");

using var izlemeIptal = new CancellationTokenSource(TimeSpan.FromHours(1));
await foreach (FileChange degisiklik in files.WatchAsync(@"C:\gelen", "*.xml", cancellationToken: izlemeIptal.Token))
    Console.WriteLine($"{degisiklik.Kind}: {degisiklik.FullPath}");
```

- **Tarama:** `FileQuery`; `SearchPattern` (`*`, `?`), `Extensions`, `Recursive`, `OlderThan`/`NewerThan`, `MinSizeBytes`/`MaxSizeBytes`, `IncludeHidden`. Erişilemeyen klasörler sessizce atlanır, sistem dosyaları her zaman, gizli dosyalar `IncludeHidden = false` iken dışarıda kalır. Sonuçlar tembel (`EnumerateFiles`) veya liste (`GetFiles`) olarak alınır.
- **Güvenli işlemler:** Kopyalama paralel yapılır; taşıma/silme işlemleri kilitli dosyalarda `MaxRetries` kez `RetryDelay` aralıklarla tekrar denenir. Salt okunur dosyalar klasör silinirken temizlenir. `WriteAllTextAtomicAsync`/`WriteAllBytesAtomicAsync` önce geçici dosyaya yazıp hedefi tek adımda değiştirir.
- **Diğer:** `MoveFileAsync`, `MoveDirectoryAsync` (farklı sürücüler arasında kopyala + sil), `DeleteDirectoryAsync`, `EnsureDirectory`, `GetDirectorySize`, `GetDriveSpace`, `SanitizeFileName` (geçersiz karakterler ve `CON`, `PRN` gibi ayrılmış adlar), `GetUniqueFilePath` (`dosya (1).txt`).
- **Yinelenen dosyalar:** Önce boyut, büyük dosyalarda ilk 16 KB, ardından tam SHA-256 karşılaştırılır; dosyalar `MaxParallelHashing` paralellikte okunur.
- **İzleme:** `WatchAsync` bir `IAsyncEnumerable<FileChange>` döner (`Created`, `Changed`, `Deleted`, `Renamed`, `Overflow`); kısa sürede tekrarlanan olaylar `debounce` (300 ms) ile birleştirilir.

## Tarih ve takvim

```csharp
using CSPLibraries.DateTimes;
using CSPLibraries.DateTimes.Datas;
using CSPLibraries.DateTimes.Models;

var takvim = new DateTimeService(new DateTimeServiceOptions
{
    IncludeReligiousHolidays = true,
    TreatHalfDaysAsHolidays = false,
    AdditionalHolidays = [new DateOnly(2025, 12, 31)],
    ExcludedDates = []
});

DateOnly bugun = takvim.Today;
DateOnly vade = takvim.AddBusinessDays(bugun, 5);
int isGunu = takvim.CountBusinessDays(new DateOnly(2025, 10, 1), new DateOnly(2025, 11, 1));
bool tatil = takvim.IsHoliday(new DateOnly(2025, 10, 29));
IReadOnlyList<Holiday> tatiller = takvim.GetHolidays(2026);
DateTime slaBitis = takvim.AddWorkingTime(takvim.Now, TimeSpan.FromHours(8), new TimeOnly(8, 30), new TimeOnly(17, 30));
int hafta = takvim.GetIsoWeek(bugun);
string ifade = takvim.Humanize(takvim.Now.AddHours(-2));

string sapTarih = takvim.ToSapDate(bugun);
DateOnly? sapGeri = takvim.ParseSapDate("20251020");
DateTime? sapZaman = takvim.ParseSapDateTime("20251020", "134501");
```

- **Saat dilimi:** Varsayılan `Europe/Istanbul` (`TimeZoneId` ile değiştirilebilir); `Now`, `Today`, `ToLocal`, `ToUtc` bu dilime göre çalışır.
- **Tatiller:** Resmi tatiller (15 Temmuz 2017'den itibaren dahil) ve Ramazan/Kurban bayramları (Hicri takvimden hesaplanır, 1901–2076 arası) yıl bazında önbelleğe alınır. Arifeler yarım gün olarak işaretlenir; `TreatHalfDaysAsHolidays` ile tatil sayılabilir. `AdditionalHolidays` ve `ExcludedDates` ile özelleştirilir.
- **İş günü:** `IsBusinessDay`, `NextBusinessDay`, `PreviousBusinessDay`, `AddBusinessDays` (negatif değer geriye gider), `FirstBusinessDayOfMonth`, `LastBusinessDayOfMonth`, `EachBusinessDay`. Hafta sonu günleri `WeekendDays` ile değiştirilebilir (varsayılan Cumartesi, Pazar).
- **Mesai:** `IsWithinWorkingHours` ve `AddWorkingTime` varsayılan 09:00–18:00 aralığını kullanır, tatil ve hafta sonlarını atlar.
- **Yardımcılar:** `StartOfWeek`/`EndOfWeek` (Pazartesi başlangıçlı), `StartOfMonth`/`EndOfMonth`, `GetQuarter`, `GetIsoWeek`, `CalculateAge`, `EachDay`, `GetMonthName`, `GetDayName`, `TryParseTurkish` ("20 Ekim 2025"), `Humanize` ("2 saat önce").
- **SAP:** `yyyyMMdd` / `HHmmss` biçimleri; `00000000` boş tarih, `240000` gün sonu olarak yorumlanır.

## Metin yardımcıları

`using CSPLibraries.Strings;` ile `string` üzerinde extension metot olarak kullanılır.

```csharp
using CSPLibraries.Strings;

bool tckn = "10000000146".IsValidTckn();
bool iban = "TR33 0006 1005 1978 6457 8413 26".IsValidIban();
string yaziyla = 1234.50m.AmountToWordsTr(upperCase: true);
string slug = "Merhaba Dünya! Çok güzel bir gün.".ToSlug();
string maske = "ali.veli@firma.com".MaskEmail();
string telefon = "0532 123 45 67".ToE164PhoneTr() ?? "";
double benzerlik = "Çağlayan".Similarity("caglayan");
```

| Grup | Metotlar |
|---|---|
| Boşluk/null | `IsBlank`, `HasValue`, `NullIfBlank`, `CollapseWhitespace`, `NormalizeLineEndings` |
| Kesme | `Truncate`, `Left`, `Right`, `Between`, `SplitAndTrim`, `OnlyDigits` |
| Türkçe | `ToUpperTr`, `ToLowerTr`, `ToTitleCaseTr`, `EqualsTr`, `ContainsTr`, `ToSearchKey`, `ToAsciiTr`, `RemoveDiacritics` |
| Dönüşüm | `ToSlug`, `ToSnakeCase`, `ToPascalCase`, `ToBase64`, `FromBase64`, `ToSha256Hex` |
| Maskeleme | `Mask(baştan, sondan)`, `MaskEmail` |
| Doğrulama | `IsValidTckn`, `IsValidVkn`, `IsValidIban`, `IsValidEmail`, `ToE164PhoneTr`, `FormatPhoneTr`, `FormatIban` |
| Benzerlik | `LevenshteinDistance`, `Similarity` (Türkçe karakterleri normalize ederek) |
| Sayı | `ToWordsTr` (`int`/`long`), `AmountToWordsTr` (para birimi ve kuruş adı seçilebilir) |
| SAP | `ToSapAlpha` (baştaki sıfırlarla 10 hane), `FromSapAlpha` |

## Koleksiyon yardımcıları

`using CSPLibraries.Enumerables;` ile `IEnumerable<T>` ve `IAsyncEnumerable<T>` üzerinde kullanılır.

```csharp
using CSPLibraries.Enumerables;
using CSPLibraries.Enumerables.Models;

int[] tutarlar = [120, 45, 990, 15, 300];

List<int> enBuyukler = tutarlar.TopBy(3, t => t);
var (buyuk, kucuk) = tutarlar.Partition(t => t > 100);
PagedResult<int> sayfa = tutarlar.ToPagedResult(pageNumber: 1, pageSize: 2);

await tutarlar.ProcessInBatchesAsync(2, async (parti, ct) => await Task.Delay(10, ct));
await tutarlar.ParallelForEachAsync(async (t, ct) => await Task.Delay(t, ct), maxDegreeOfParallelism: 4);
int[] iki = await tutarlar.SelectParallelAsync(async (t, ct) => { await Task.Yield(); return t * 2; });

System.Data.DataTable tablo = tutarlar.Select(t => new { Tutar = t }).ToDataTable("Tutarlar");
```

| Grup | Metotlar |
|---|---|
| Null/boş | `IsNullOrEmpty`, `OrEmpty`, `WhereNotNull` |
| Arama/ayrıştırma | `Partition`, `Duplicates`, `IndexOf`, `ToDictionarySafe` (tekrar eden anahtarı atlar), `FullOuterJoin`, `Flatten` |
| Sayfalama | `ToPagedResult` (`IEnumerable` ve `IQueryable`, en fazla 10.000 kayıt/sayfa) |
| Parti/paralel | `ProcessInBatchesAsync`, `ParallelForEachAsync`, `SelectParallelAsync`, `ChunkAsync` |
| Seçim/pencere | `TopBy`, `BottomBy` (öncelik kuyruğu, O(n log k)), `Window`, `Scan`, `ChunkByWeight` |
| Dönüşüm | `ToDelimitedString`, `ToDataTable` |

## Hedef çerçeve farkları

Genel API tüm hedeflerde aynıdır. Aşağıdaki farklar, alttaki çerçevenin yeteneklerinden kaynaklanır.

| Konu | .NET 10 / .NET 8 | .NET 5 | .NET Framework 4.8 |
|---|---|---|---|
| HTTP istemcisi | `SocketsHttpHandler`, HTTP/2, gzip/deflate/brotli açma | Aynı | `HttpClientHandler`, gzip/deflate açma; bağlantı ömrü `ServicePoint` ile ayarlanır |
| `CompressionFormat.Brotli` | Desteklenir | Desteklenir | `PlatformNotSupportedException` |
| `ZLib` sıkıştırma | Kutudan `ZLibStream` | Kütüphane içi uygulama | Kütüphane içi uygulama (açarken Adler-32 sağlaması doğrulanmaz) |
| `DateOnly` / `TimeOnly` | Kutudan | `Portable.System.DateTimeOnly` | `Portable.System.DateTimeOnly` |
| `DateTimeServiceOptions.WeekendDays` | `IReadOnlySet<DayOfWeek>` | `IReadOnlySet<DayOfWeek>` | `IReadOnlyCollection<DayOfWeek>` |
| JSON'da `DateOnly`/`TimeOnly` | System.Text.Json yerleşik | Varsayılan ayarlara dönüştürücü eklenir | Varsayılan ayarlara dönüştürücü eklenir |
| ADO.NET `DateOnly`/`TimeOnly` parametresi | Sağlayıcıya olduğu gibi | `DATE`/`TIME` (`DateTime`/`TimeSpan`) olarak | `DATE`/`TIME` (`DateTime`/`TimeSpan`) olarak |
| ADO.NET async dispose/transaction | Sağlayıcının async API'leri | Sağlayıcının async API'leri | Senkron karşılıkları |
| `Microsoft.Data.SqlClient` | 7.1.x | 5.2.x | 7.1.x |
| `Europe/Istanbul` saat dilimi | Doğrudan | Windows'ta `Turkey Standard Time`'a çevrilir | `Turkey Standard Time`'a çevrilir |
| Null akış uyarıları | Açık | Açık | Kapalı (BCL açıklamaları yok) |

Ek notlar:

- .NET Framework 4.8 ve .NET 5'te kendi `JsonSerializerOptions` nesnenizi veriyorsanız `DateOnly`/`TimeOnly` için dönüştürücüyü kendiniz eklemelisiniz.
- .NET 5 desteği sona ermiş bir çerçevedir; bağımlılıkların netstandard derlemeleri kullanılır ve derleme sırasında destek uyarıları bilinçli olarak gizlenir.
- .NET Framework 4.8 uygulamalarında, SDK tarzı olmayan eski projeler için bağımlılık sürümleri arasında binding redirect gerekebilir (SDK tarzı projelerde otomatik üretilir).

## Derleme ve paketleme

Gereksinim: .NET 10 SDK (C# 14 kullanılır). `net48` ve `net5.0` başvuru derlemeleri makinede yoksa SDK bunları NuGet üzerinden otomatik indirir.

```bash
dotnet build -c Release
dotnet build -c Release -f net48
dotnet pack -c Release -o artifacts
```

- Paket, `EnablePackageValidation` ile hedefler arasındaki API uyumluluğunu denetler.
- CI ortamlarında (`CI`, `TF_BUILD`, `GITHUB_ACTIONS`) deterministik derleme için `ContinuousIntegrationBuild` otomatik açılır.
- Sembol paketi (`.snupkg`) ve README pakete eklenir.

## Proje yapısı

```text
LibPlayground.csproj
Lib/
  Compat/                 Eski hedefler için internal API karşılıkları (#if ile hedefe göre derlenir)
  RestService/            Her servis aynı düzeni izler:
    RestService.cs          Genel servis sınıfı
    Constants/              Varsayılan değerler ve sabitler
    Datas/                  Ayar sınıfları ve enum'lar
    Interfaces/             Genel arayüzler
    Models/                 Sonuç tipleri, öznitelikler, istisnalar
    Workers/                İç uygulama sınıfları
  SAPService/ ExcelService/ DatabaseService/ MailService/ LogService/
  SecurityService/ SerializationService/ DirectoryService/
  DateTimeService/ StringService/ EnumerableService/
```

Kök namespace `CSPLibraries`'tir; klasör adları namespace'lerle birebir eşleşmez (örn. `Lib/RestService` → `CSPLibraries.Rest`).

## Sürüm notları

- **1.0.1:** .NET 8, .NET 5 ve .NET Framework 4.8 hedefleri eklendi. Eski hedeflerde `DateOnly`/`TimeOnly` için JSON dönüştürücüleri ve veritabanı parametre dönüşümleri, `Europe/Istanbul` saat dilimi çözümü ve kütüphane içi ZLib desteği eklendi.
- **1.0.0:** İlk sürüm (.NET 10).

## Lisans

MIT
