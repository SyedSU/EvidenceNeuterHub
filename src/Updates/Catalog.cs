using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace EvidenceHub.Updates;
public record CatalogDownload(string Url,long Size,string Sha256);
public record CatalogRelease(string ManifestUrl,string Version,string SourceCommit,string Platform,CatalogDownload? Download);
public record CatalogScreenshot(string Url,string Alt);
public record CatalogNotice(string Name,string Url);
public record CatalogComponent(string? Version,string? SourceCommit,CatalogNotice[] LicenseNotices);
public record CatalogComponents(CatalogComponent? Runtime,CatalogComponent? Model);
public record CatalogEntry(string Id,string Name,string Availability,string? Version,string? SourceCommit,CatalogRelease? Release,
 CatalogScreenshot[] Screenshots,CatalogNotice[] LicenseNotices,string Description,string[] Requirements,string[] ReleaseNotes,int? SaveSchema=null,CatalogComponents? Components=null);
public record CatalogGate(string Status,string[] Reasons,bool TrustConfigured,bool SignedArtifactsPublished,bool WindowsAcceptancePassed,bool LicensesApproved,bool AuthenticodeVerified);
public record ProductCatalog(int Schema,string Channel,long Sequence,DateTimeOffset Expires,bool PublicDownloads,CatalogGate ReleaseGate,CatalogEntry Infrastructure,List<CatalogEntry> Products);
public static class Catalog
{
 public static string Resource(string name){using var stream=typeof(Catalog).Assembly.GetManifestResourceStream(name) ?? throw new IOException("Catalog resource missing.");return new StreamReader(stream).ReadToEnd();}
 public static ProductCatalog Development()=>Parse(Resource("EvidenceHub.DevelopmentCatalog"),false);
 public static ProductCatalog Verify(string envelope,string publicKey,long highest)
 {
  if(string.IsNullOrWhiteSpace(publicKey))throw new IOException("Catalog trust is not configured.");
  if(System.Text.Encoding.UTF8.GetByteCount(envelope)>Metadata.MaxMetadataBytes)throw new IOException("Catalog too large.");
  var signed=JsonSerializer.Deserialize<SignedRelease>(envelope,Metadata.Json) ?? throw new IOException("Missing catalog envelope.");
  var payload=Convert.FromBase64String(signed.Payload);using var key=RSA.Create();key.ImportFromPem(publicKey);
  if(key.KeySize<3072 || !key.VerifyData(payload,Convert.FromBase64String(signed.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new IOException("Invalid catalog signature.");
  var c=Parse(System.Text.Encoding.UTF8.GetString(payload),true);
  if(c.Expires<=DateTimeOffset.UtcNow || c.Sequence<highest)throw new IOException("Catalog expired or replayed.");return c;
 }
 public static CatalogEntry ReleaseFor(ProductCatalog catalog,string product)
 {
  var entry=product=="hub"?catalog.Infrastructure:catalog.Products.Single(p=>p.Id==product);
  if(catalog.Expires<=DateTimeOffset.UtcNow || !catalog.PublicDownloads || catalog.ReleaseGate.Status!="approved" || entry.Availability!="available" || entry.Release is null)throw new IOException("Public release gate is blocked. No download is authorized; imported installations remain playable.");
  return entry;
 }
 public static void MatchGenesis(ProductCatalog catalog,ReleaseManifest manifest)
 {
  var entry=ReleaseFor(catalog,"genesis");
  if(entry.Version!=manifest.Version || entry.SourceCommit!=manifest.SourceCommit || entry.SaveSchema!=manifest.SaveSchema)throw new IOException("Genesis manifest does not match the signed product catalog.");
 }
 public static void MatchHub(ProductCatalog catalog,HubManifest manifest)
 {
  var entry=ReleaseFor(catalog,"hub");var download=entry.Release!.Download;
  if(entry.Version!=manifest.Version || entry.SourceCommit!=manifest.SourceCommit || download is null || download.Url!=manifest.Installer.Url || download.Size!=manifest.Installer.Size || download.Sha256!=manifest.Installer.Sha256)throw new IOException("Hub installer metadata does not match the signed product catalog.");
 }
 public static ProductCatalog Parse(string json,bool authenticated)
 {
  if(System.Text.Encoding.UTF8.GetByteCount(json)>Metadata.MaxMetadataBytes)throw new IOException("Catalog too large.");
  using var doc=JsonDocument.Parse(json);using var schema=JsonDocument.Parse(Resource("EvidenceHub.CatalogSchema"));Validate(doc.RootElement,schema.RootElement);
  var c=JsonSerializer.Deserialize<ProductCatalog>(json,Metadata.Json) ?? throw new IOException("Missing catalog.");
  if(c.Infrastructure.Id!="hub" || !c.Products.Select(p=>p.Id).SequenceEqual(new[]{"genesis","lab","bot","academy","flow","council"}))throw new IOException("Catalog product identities/order invalid.");
  foreach(var e in c.Products.Prepend(c.Infrastructure)) {
   if(e.Availability=="available" && (e.Version is null || e.SourceCommit is null || e.Release is null))throw new IOException("Available entry lacks provenance.");
   if(e.Release is {} r){Metadata.PackageUrl(r.ManifestUrl);if(r.Version!=e.Version || r.SourceCommit!=e.SourceCommit)throw new IOException("Catalog release provenance differs.");if(r.Download is {} d){Metadata.ImmutableUrl(d.Url);Metadata.Hash(d.Sha256);}}
   foreach(var shot in e.Screenshots)ScreenshotUrl(shot.Url);
   foreach(var notice in e.LicenseNotices.Concat(e.Components?.Runtime?.LicenseNotices ?? Array.Empty<CatalogNotice>()).Concat(e.Components?.Model?.LicenseNotices ?? Array.Empty<CatalogNotice>()))ScreenshotUrl(notice.Url);
  }
  if(c.Products[0].Release?.Download is not null)throw new IOException("Genesis uses component metadata, not a single installer.");
  if(c.PublicDownloads){var g=c.ReleaseGate;var genesis=c.Products[0];if(!authenticated || g.Status!="approved" || g.Reasons.Length!=0 || !g.TrustConfigured || !g.SignedArtifactsPublished || !g.WindowsAcceptancePassed || !g.LicensesApproved || !g.AuthenticodeVerified || c.Infrastructure.Availability!="available" || genesis.Availability!="available" || c.Infrastructure.Release?.Download is null || c.Infrastructure.LicenseNotices.Length==0 || genesis.LicenseNotices.Length==0 || !ComponentReady(genesis.Components?.Runtime) || !ComponentReady(genesis.Components?.Model))throw new IOException("Public release gate is incomplete.");}
  return c;
 }
 static bool ComponentReady(CatalogComponent? c)=>c?.Version is not null && c.SourceCommit is not null && c.LicenseNotices.Length>0;
 static void ScreenshotUrl(string url){if(string.IsNullOrEmpty(url) || url.Contains('%') || url.Contains('?') || url.Contains('#'))throw new IOException("Unsafe asset URL.");if(url.StartsWith('/') && !url.StartsWith("//")){Metadata.SafePath(url[1..]);return;}Metadata.ImmutableUrl(url);}
 static void Validate(JsonElement value,JsonElement schema)
 {
  if(schema.TryGetProperty("anyOf",out var alternatives)){foreach(var a in alternatives.EnumerateArray()){try{Validate(value,a);return;}catch(IOException){}}throw new IOException("Catalog value violates alternatives.");}
  if(schema.TryGetProperty("type",out var type)){var types=type.ValueKind==JsonValueKind.Array?type.EnumerateArray().Select(t=>t.GetString()).ToArray():new[]{type.GetString()};bool match=types.Any(t=>t switch{"object"=>value.ValueKind==JsonValueKind.Object,"array"=>value.ValueKind==JsonValueKind.Array,"string"=>value.ValueKind==JsonValueKind.String,"boolean"=>value.ValueKind is JsonValueKind.True or JsonValueKind.False,"integer"=>value.ValueKind==JsonValueKind.Number && value.TryGetInt64(out _),"null"=>value.ValueKind==JsonValueKind.Null,_=>false});if(!match)throw new IOException("Catalog value has wrong type.");}
  if(schema.TryGetProperty("const",out var constant) && value.GetRawText()!=constant.GetRawText())throw new IOException("Catalog constant invalid.");
  if(schema.TryGetProperty("enum",out var choices) && !choices.EnumerateArray().Any(x=>x.GetRawText()==value.GetRawText()))throw new IOException("Catalog enum invalid.");
  if(value.ValueKind==JsonValueKind.Object){var names=new HashSet<string>();foreach(var p in value.EnumerateObject())if(!names.Add(p.Name))throw new IOException("Duplicate catalog member.");if(schema.TryGetProperty("required",out var required))foreach(var r in required.EnumerateArray())if(!value.TryGetProperty(r.GetString()!,out _))throw new IOException("Missing required catalog member.");if(schema.TryGetProperty("properties",out var props))foreach(var p in value.EnumerateObject()){if(!props.TryGetProperty(p.Name,out var child))throw new IOException("Unknown catalog member.");Validate(p.Value,child);}}
  if(value.ValueKind==JsonValueKind.Array){var count=value.GetArrayLength();if(schema.TryGetProperty("minItems",out var min) && count<min.GetInt32() || schema.TryGetProperty("maxItems",out var max) && count>max.GetInt32())throw new IOException("Catalog array length invalid.");if(schema.TryGetProperty("items",out var items))foreach(var item in value.EnumerateArray())Validate(item,items);}
  if(value.ValueKind==JsonValueKind.String){var s=value.GetString()!;if(schema.TryGetProperty("minLength",out var min) && s.Length<min.GetInt32() || schema.TryGetProperty("pattern",out var pattern) && !Regex.IsMatch(s,pattern.GetString()!))throw new IOException("Invalid catalog text.");if(schema.TryGetProperty("format",out var format) && format.GetString()=="date-time" && (!s.EndsWith('Z') || !DateTimeOffset.TryParse(s,out _)))throw new IOException("Catalog requires UTC expiry.");}
  if(value.ValueKind==JsonValueKind.Number){if(!value.TryGetInt64(out var n) || schema.TryGetProperty("minimum",out var min) && n<min.GetInt64() || schema.TryGetProperty("maximum",out var max) && n>max.GetInt64())throw new IOException("Invalid catalog number.");}
 }
}

public sealed class GenesisReleaseSelection
{
 public VerifiedRelease? Candidate {get;private set;}
 public void Invalidate()=>Candidate=null;
 public void Check(ProductCatalog catalog,string envelope,InstallManager manager)
 {
  Candidate=null;var next=manager.VerifyNew(envelope);Catalog.MatchGenesis(catalog,next.Manifest);Candidate=next;
 }
 public VerifiedRelease RequireCurrent(ProductCatalog catalog,InstallManager manager)
 {
  try{var selected=Candidate ?? throw new IOException("Check a matching approved release before installation.");Catalog.MatchGenesis(catalog,manager.VerifyNew(selected.Envelope).Manifest);return selected;}
  catch{Candidate=null;throw;}
 }
}
