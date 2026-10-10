using EvidenceHub.Updates;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length != 6) throw new ArgumentException("Usage: ReleaseTool <extracted-Genesis-folder> <output> <version> <sequence> <source-commit> <release-tag>");
var input=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);var version=args[2];var sequence=long.Parse(args[3]);var commit=args[4];var tag=args[5];
Metadata.VersionOf(version);
if(!System.Text.RegularExpressions.Regex.IsMatch(tag,"^genesis-v[0-9]+\\.[0-9]+\\.[0-9]+$"))throw new IOException("Unsafe release tag.");
using var rsa=RSA.Create();
var key=Environment.GetEnvironmentVariable("RELEASE_SIGNING_KEY_PEM") ?? throw new IOException("Configure the release signing key securely in the protected publishing environment.");
rsa.ImportFromPem(key);
var publicPem=Environment.GetEnvironmentVariable("GENESIS_RELEASE_PUBLIC_KEY_PEM") ?? throw new IOException("Configure the public verification key.");
using var publicRsa=RSA.Create();publicRsa.ImportFromPem(publicPem);
if(rsa.KeySize<3072 || !rsa.ExportSubjectPublicKeyInfo().SequenceEqual(publicRsa.ExportSubjectPublicKeyInfo())) throw new IOException("Signing key and pinned Hub public key do not match.");
var previousPath=Environment.GetEnvironmentVariable("PREVIOUS_RELEASE_METADATA_PATH");
if(!string.IsNullOrEmpty(previousPath)) {
 var previous=Metadata.Verify(File.ReadAllText(previousPath),publicPem,offlineInstalled:true).Manifest;
 if(sequence<=previous.Sequence || Metadata.VersionOf(version)<=Metadata.VersionOf(previous.Version)) throw new IOException("Published release sequence and version must strictly increase.");
}
Directory.CreateDirectory(output);
string Digest(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
var packages=new List<ReleasePackage>();
foreach(var kind in new[]{"game","runtime","model"}){
 var source=kind=="game"?input:Path.Combine(input,kind=="runtime"?"brain":"model");
 var paths=Directory.GetFiles(source,"*",SearchOption.AllDirectories).Where(p=>kind!="game"||!(Path.GetRelativePath(source,p).Replace('\\','/').StartsWith("brain/")||Path.GetRelativePath(source,p).Replace('\\','/').StartsWith("model/"))).ToList();
 var entries=paths.Select(p=>(Source:p,Relative:Path.GetRelativePath(source,p).Replace('\\','/'))).ToList();
 if(kind!="game"){
  var notices=kind=="model"?new[]{"Qwen-LICENSE.txt"}:new[]{"llama.cpp-LICENSE.txt"};
  foreach(var notice in notices){var p=Path.Combine(input,"licenses",notice);if(!File.Exists(p))throw new IOException("Missing third-party license: "+notice);entries.Add((p,"licenses/"+notice));}
 }
 var archive=Path.Combine(output,"genesis-"+kind+".zip");
 using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))foreach(var entry in entries.OrderBy(e=>e.Relative,StringComparer.Ordinal)){
  Metadata.SafePath(entry.Relative);InstallManager.RejectLinks(entry.Source);
  var target=zip.CreateEntry(entry.Relative,CompressionLevel.Optimal);target.LastWriteTime=new DateTimeOffset(2020,1,1,0,0,0,TimeSpan.Zero);
  using var from=File.OpenRead(entry.Source);using var to=target.Open();from.CopyTo(to);
 }
 var hash=Digest(archive);var asset="genesis-"+kind+"-"+hash+".zip";var final=Path.Combine(output,asset);File.Move(archive,final);
 packages.Add(new(kind,"https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/"+tag+"/"+asset,new FileInfo(final).Length,hash,
  entries.Select(e=>new ReleaseFile(e.Relative,new FileInfo(e.Source).Length,Digest(e.Source))).ToArray()));
}
// Genesis0.4 compatibility epoch4: profile saves/internal statev4; no automatic cross-epoch migration.
var manifest=new ReleaseManifest(1,"genesis","stable","windows-x64",version,sequence,DateTimeOffset.UtcNow.AddDays(30),"0.3.0",4,commit,packages.ToArray());
var payload=JsonSerializer.SerializeToUtf8Bytes(manifest,Metadata.Json);
var signed=JsonSerializer.Serialize(new SignedRelease(Convert.ToBase64String(payload),Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))),Metadata.Json);
Metadata.Verify(signed,publicPem);
File.WriteAllText(Path.Combine(output,"genesis-stable.json"),signed);
File.WriteAllText(Path.Combine(output,"SHA256.txt"),string.Join("\n",Directory.GetFiles(output).Where(p=>Path.GetFileName(p)!="SHA256.txt").Select(p=>Digest(p)+"  "+Path.GetFileName(p)))+"\n");
Console.WriteLine("Created and signature-verified three component packages and genesis-stable.json. No release was published.");
