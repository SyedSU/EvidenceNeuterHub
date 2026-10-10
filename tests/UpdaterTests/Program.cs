using EvidenceHub;
using EvidenceHub.Updates;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var folder = Path.Combine(Path.GetTempPath(), "hub-updater-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
using var key = RSA.Create(3072);
var publicKey = key.ExportSubjectPublicKeyInfoPem();
int passed = 0;
void Assert(bool check, string label) { if (!check) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
async Task Reject(Func<Task> action, string label)
{ try { await action(); } catch (Exception e) when (e is IOException or CryptographicException or JsonException or OperationCanceledException or FormatException) { passed++; Console.WriteLine("PASS " + label); return; } throw new Exception("Expected rejection: " + label); }
string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
string Sign(ReleaseManifest manifest) {
 var payload=JsonSerializer.SerializeToUtf8Bytes(manifest,Metadata.Json);
 return JsonSerializer.Serialize(new SignedRelease(Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))),Metadata.Json);
}
var exe = new byte[128]; exe[0]=0x4d; exe[1]=0x5a; exe[60]=64; exe[64]=0x50; exe[65]=0x45; exe[68]=0x64; exe[69]=0x86;
var handler = new PackagesHandler();
ReleasePackage Pack(string kind, string name, byte[] content, string tag) {
 using var output=new MemoryStream();
 using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)) { var entry=zip.CreateEntry(name);entry.LastWriteTime=new DateTimeOffset(2020,1,1,0,0,0,TimeSpan.Zero);using var stream=entry.Open();stream.Write(content); }
 var data=output.ToArray(); var url="https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/"+tag+"/"+kind+".zip";
 handler.Data[url]=data;
 return new(kind,url,data.Length,Hash(data),new[]{new ReleaseFile(name,content.Length,Hash(content))});
}
ReleaseManifest Fixture(string version,long seq) => new(1,"genesis","stable","windows-x64",version,seq,DateTimeOffset.UtcNow.AddDays(2),"0.2.0",1,new string('a',40),new[]{
 Pack("game","EvidenceGenesis.exe",exe,"fixture-"+seq), Pack("runtime","llama-server.exe",Encoding.UTF8.GetBytes("test-runtime"),"fixture-runtime"), Pack("model","genesis.gguf",new byte[200000],"fixture-model")});
using var client = new DownloadClient(handler);
var saves=Path.Combine(folder,"external-saves");Directory.CreateDirectory(saves);File.WriteAllText(Path.Combine(saves,"save.json"),"personal save");
var manager=new InstallManager(Path.Combine(folder,"managed"),publicKey,client,saves);
var first=Fixture("0.3.0",1);var envelope=Sign(first);
try {
 if(OperatingSystem.IsWindows()) {
  var gameFixture=Path.GetFullPath("artifacts/gamefixture/EvidenceGenesis.exe");
  using var running=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(gameFixture){UseShellExecute=false}) ?? throw new Exception("Running-game fixture did not start");
  try {await Task.Delay(100);await Reject(()=>manager.RollbackAsync(CancellationToken.None),"actual running game process blocks rollback");await Reject(()=>Task.Run(()=>manager.ClearDownloadStorage()),"actual running game process blocks storage cleanup");}
  finally{if(!running.HasExited)running.Kill();await running.WaitForExitAsync();}
 }else Console.WriteLine("SKIP Windows-only actual running-game process fixture; Windows CI executes it.");
 var canceledStage=Path.Combine(folder,"canceled-handoff");Directory.CreateDirectory(canceledStage);File.WriteAllText(Path.Combine(canceledStage,"cancel.requested"),"cancel");
 using(var parent=System.Diagnostics.Process.GetCurrentProcess())await Reject(()=>HubUpdate.WaitForParentExitAsync(parent,canceledStage,CancellationToken.None),"canceled bootstrap handoff cannot install after later Hub exit");
 var catalogPath=Path.Combine(AppContext.BaseDirectory,"development.json"); var devCatalog=File.ReadAllText(catalogPath);
 Assert(Catalog.Parse(devCatalog,false).Products[0].SaveSchema==4,"canonical catalog saveSchema4");
 await Reject(()=>Task.Run(()=>Catalog.ReleaseFor(Catalog.Parse(devCatalog,false),"genesis")),"blocked development catalog cannot authorize transfers");
 await Reject(()=>Task.Run(()=>Catalog.Parse(devCatalog.Replace("\"publicDownloads\": false","\"publicDownloads\": true"),false)),"unsigned catalog cannot enable downloads");
 await Reject(()=>Task.Run(()=>Catalog.Parse(devCatalog.Replace("\"schema\": 1","\"schema\": 1, \"unknown\": true"),false)),"catalog unknown fields rejected");
 await Reject(()=>Task.Run(()=>Catalog.Parse(devCatalog.Replace("\"requirements\": [","\"missingRequirements\": ["),false)),"catalog required fields rejected");
 Assert(Catalog.Parse(devCatalog.Replace("\"screenshots\": []","\"screenshots\": [{\"url\":\"/assets/workshop.png\",\"alt\":\"Actual capture\"}]").Replace("\"licenseNotices\": []","\"licenseNotices\": [{\"name\":\"Notice\",\"url\":\"/licenses/genesis.txt\"}]"),false).Products.Count==6,"catalog accepts safe root-relative assets and notices");
 foreach(var unsafeUrl in new[]{"//evil.test/a","/assets/%2e%2e/x","/assets/%2f%2fevil","https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/genesis-stable/image.png"})await Reject(()=>Task.Run(()=>Catalog.Parse(devCatalog.Replace("\"screenshots\": []","\"screenshots\": [{\"url\":\""+unsafeUrl+"\",\"alt\":\"capture\"}]"),false)),"catalog unsafe asset URL "+unsafeUrl);
 var catPayload=Encoding.UTF8.GetBytes(devCatalog);var catEnvelope=JsonSerializer.Serialize(new SignedRelease(Convert.ToBase64String(catPayload),Convert.ToBase64String(key.SignData(catPayload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))),Metadata.Json);
 Assert(Catalog.Verify(catEnvelope,publicKey,0).Products.Count==6,"signed catalog distinct from Genesis manifest");
 await Reject(()=>Task.Run(()=>Catalog.Verify(catEnvelope,publicKey,2)),"catalog replay rejected");
 await Reject(()=>Task.Run(()=>Catalog.Verify(catEnvelope,"",0)),"catalog missing key fails closed");
 var development=Catalog.Parse(devCatalog,false);var approvedGenesis=development.Products[0] with{Availability="available",Version="0.4.0",SourceCommit=new string('a',40),Release=new CatalogRelease("https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/genesis-v0.4.0/manifest.json","0.4.0",new string('a',40),"windows-x64",null)};
 var approved=development with{PublicDownloads=true,Expires=DateTimeOffset.UtcNow.AddDays(1),ReleaseGate=new CatalogGate("approved",Array.Empty<string>(),true,true,true,true,true),Products=development.Products.Select(p=>p.Id=="genesis"?approvedGenesis:p).ToList()};
 await Reject(()=>Task.Run(()=>Catalog.ReleaseFor(approved with{Expires=DateTimeOffset.UtcNow.AddSeconds(-1)},"genesis")),"previously approved expired catalog cannot authorize new transfers");
 await Reject(()=>Task.Run(()=>Catalog.MatchGenesis(approved,first)),"signed Genesis manifest must match catalog version and save epoch");
 var selection=new GenesisReleaseSelection();await Reject(()=>Task.Run(()=>selection.Check(approved,envelope,manager)),"mismatched signed release cannot become an install candidate");Assert(selection.Candidate is null,"mismatch leaves no install candidate");
 var matching=approved with{Products=approved.Products.Select(p=>p.Id=="genesis"?p with{Version=first.Version,SaveSchema=first.SaveSchema,Release=p.Release! with{Version=first.Version}}:p).ToList()};selection.Check(matching,envelope,manager);Assert(selection.Candidate is not null,"matching approved release becomes an install candidate");
 await Reject(()=>Task.Run(()=>selection.RequireCurrent(development,manager)),"refreshed blocked catalog invalidates checked candidate");Assert(selection.Candidate is null,"blocked catalog leaves no install candidate");
 var hubManifest = new HubManifest(1,"hub","stable","windows-x64","0.3.1",1,DateTimeOffset.UtcNow.AddDays(1),new string('a',40),"0.2.0",19041,new HubInstaller("https://github.com/SyedSU/EvidenceNeuter-Releases/releases/download/hub-v0.3.1/setup.exe",exe.Length,Hash(exe),new string('A',64)));
 string HubSign(HubManifest value) { var data=JsonSerializer.SerializeToUtf8Bytes(value,Metadata.Json);return JsonSerializer.Serialize(new SignedRelease(Convert.ToBase64String(data),Convert.ToBase64String(key.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))),Metadata.Json); }
 var hubEnvelope=HubSign(hubManifest);
 Assert(HubUpdate.Verify(hubEnvelope,publicKey,new string('A',64),0,19041).Version=="0.3.1","Hub signed manifest verified");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(hubEnvelope,"",new string('A',64),0,19041)),"Hub missing key fails closed");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(hubEnvelope,publicKey,"",0,19041)),"Hub missing publisher fails closed");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(hubEnvelope,publicKey,new string('B',64),0,19041)),"Hub arbitrary publisher rejected");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(HubSign(hubManifest with {Expires=DateTimeOffset.UtcNow.AddSeconds(-1)}),publicKey,new string('A',64),0,19041)),"Hub expired metadata rejected");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(hubEnvelope,publicKey,new string('A',64),1,19041)),"Hub replay rejected");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(hubEnvelope,publicKey,new string('A',64),0,18000)),"Hub Windows requirement enforced");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(HubSign(hubManifest with {MinimumHubVersion="9.0.0"}),publicKey,new string('A',64),0,19041)),"Hub minimum version enforced");
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(HubSign(hubManifest with {Version="0.2.0"}),publicKey,new string('A',64),0,19041)),"Hub downgrade rejected");
 var damaged=JsonSerializer.Deserialize<SignedRelease>(hubEnvelope,Metadata.Json)!; var damagedBytes=Convert.FromBase64String(damaged.Payload);damagedBytes[20]^=1;
 await Reject(()=>Task.Run(()=>HubUpdate.Verify(JsonSerializer.Serialize(damaged with {Payload=Convert.ToBase64String(damagedBytes)},Metadata.Json),publicKey,new string('A',64),0,19041)),"Hub payload tamper rejected");
 var installerPath=Path.Combine(folder,"hub-setup.exe");File.WriteAllBytes(installerPath,exe);
 await Reject(()=>HubUpdate.VerifyInstallerAsync(hubManifest,installerPath,CancellationToken.None),"unsigned fixture installer never executes");
 File.AppendAllText(installerPath,"tamper");
 await Reject(()=>HubUpdate.VerifyInstallerAsync(hubManifest,installerPath,CancellationToken.None),"staged Hub installer tamper rejected");
 Assert(Metadata.Verify(envelope,publicKey).Manifest.Version=="0.3.0","signature and version verified");
 await Reject(()=>Task.Run(()=>Metadata.Verify(envelope,"")),"missing production trust fails closed");
 using(var other=RSA.Create(3072)) await Reject(()=>Task.Run(()=>Metadata.Verify(envelope,other.ExportSubjectPublicKeyInfoPem())),"wrong signing key");
 await Reject(()=>Task.Run(()=>Metadata.Verify(Sign(first with { Expires=DateTimeOffset.UtcNow.AddMinutes(-1)}),publicKey)),"expired metadata");
 await Reject(()=>Task.Run(()=>Metadata.Verify(Sign(first with {Schema=2}),publicKey)),"unsupported schema");
 await Reject(()=>Task.Run(()=>Metadata.Verify(Sign(first with {MinimumHubVersion="9.0.0"}),publicKey)),"newer Hub required");
 await Reject(()=>Task.Run(()=>Metadata.Verify(Sign(first with {Platform="linux"}),publicKey)),"unsupported platform");
 await Reject(()=>Task.Run(()=>Metadata.Verify(Sign(first with {Packages=first.Packages.Select(p=>p.Kind=="game"?p with {Url="https://example.org/game.zip"}:p).ToArray()}),publicKey)),"package URL allowlist");
 foreach(var path in new[]{"../evil.exe","/evil","a\\b","brain/../x","CON.txt","file:stream","x. ","a//b"})
  await Reject(()=>Task.Run(()=>Metadata.SafePath(path)),"reject unsafe path "+path);
 var reports=new List<TransferProgress>(); var progress=new InlineProgress(p=>reports.Add(p));
 await manager.InstallAsync(envelope,null,progress,CancellationToken.None);
 Assert(handler.Requests.Count==3,"three real component transfer pipelines");
 Assert(reports.Any(p=>p.Phase=="Downloading" && p.Bytes>0 && p.Total>0),"actual byte progress");
 Assert(reports.Any(p=>p.BytesPerSecond>0),"download speed measured from transferred bytes");
 var current=await manager.CurrentAsync(CancellationToken.None);Assert(current is not null && current.HasModel && current.HasRuntime,"complete installed layout and offline verification");
 Assert(manager.State().HighestSequence==1,"activation persists sequence");
 Assert(File.ReadAllText(Path.Combine(saves,"save.json"))=="personal save","save original untouched");
 Assert(Directory.GetFiles(Path.Combine(manager.Root,"save-backups"),"save.json",SearchOption.AllDirectories).Length==1,"save backup created");
 var second=Fixture("0.3.1",2); // Synthetic update metadata, never a published game version.
 exe[100]=1;second=second with {Packages=second.Packages.Select(p=>p.Kind=="game"?Pack("game","EvidenceGenesis.exe",exe,"fixture-new"):p).ToArray()};
 var secondEnvelope=Sign(second); handler.Requests.Clear();
 await manager.InstallAsync(secondEnvelope,null,progress,CancellationToken.None);
 Assert(handler.Requests.Count==1 && handler.Requests[0].Contains("fixture-new"),"unchanged runtime/model not downloaded");
 Assert(manager.State().Previous is not null,"previous version retained");
 await Reject(()=>manager.InstallAsync(envelope,null,null,CancellationToken.None),"replay and downgrade rejected");
 var snapshot=File.ReadAllText(Path.Combine(manager.Root,"active.json"));
 var third=Fixture("0.3.2",3);exe[101]=2;third=third with{Packages=third.Packages.Select(p=>p.Kind=="game"?Pack("game","EvidenceGenesis.exe",exe,"fixture-cancel"):p).ToArray()};
 using(var cancel=new CancellationTokenSource()) {
  await Reject(()=>manager.InstallAsync(Sign(third),null,new InlineProgress(p=>{if(p.Phase=="Downloading")cancel.Cancel();}),cancel.Token),"cancel before activation");
 }
 Assert(File.ReadAllText(Path.Combine(manager.Root,"active.json"))==snapshot,"cancel preserves active pointer");
 Assert(!Directory.GetFiles(manager.Root,"*.part",SearchOption.AllDirectories).Any(),"partial files cleaned");
 var bad=third with{Packages=third.Packages.Select(p=>p.Kind=="game"?p with{Sha256=new string('0',64)}:p).ToArray()};
 await Reject(()=>manager.InstallAsync(Sign(bad),null,null,CancellationToken.None),"archive hash mismatch");
 Assert(File.ReadAllText(Path.Combine(manager.Root,"active.json"))==snapshot,"failure preserves active pointer");
 var badFile=third with{Packages=third.Packages.Select(p=>p.Kind=="game"?p with{Files=p.Files.Select(f=>f with{Sha256=new string('0',64)}).ToArray()}:p).ToArray()};
 await Reject(()=>manager.InstallAsync(Sign(badFile),null,null,CancellationToken.None),"extracted file hash mismatch");
 var traversalPack=Pack("game","../EvidenceGenesis.exe",exe,"fixture-traversal");
 traversalPack=traversalPack with{Files=new[]{new ReleaseFile("EvidenceGenesis.exe",exe.Length,Hash(exe))}};
 await Reject(()=>manager.InstallAsync(Sign(third with{Packages=third.Packages.Select(p=>p.Kind=="game"?traversalPack:p).ToArray()}),null,null,CancellationToken.None),"ZIP traversal rejected");
 await Reject(()=>manager.InstallAsync(Sign(third with{SaveSchema=2}),null,null,CancellationToken.None),"unreviewed save migration blocked");
 using(var locked=new FileStream(Path.Combine(manager.Root,"install.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
  await Reject(()=>manager.RollbackAsync(CancellationToken.None),"concurrent operation locked");
 await manager.RollbackAsync(CancellationToken.None);
 Assert(Metadata.Verify(manager.State().Current!.Envelope,publicKey,offlineInstalled:true).Manifest.Version=="0.3.0","explicit verified rollback");
 Assert(manager.State().HighestSequence==2,"rollback cannot reset anti-replay sequence");
 Assert(File.ReadAllText(Path.Combine(saves,"save.json"))=="personal save","rollback preserves saves");
 var game=await manager.CurrentAsync(CancellationToken.None);File.AppendAllText(game!.Executable,"tamper");
 await Reject(()=>manager.CurrentAsync(CancellationToken.None),"offline tampering detected before Play");
 // Fresh schema4 profile can install and reuse unchanged components on same-schema update.
 var epoch4=new InstallManager(Path.Combine(folder,"schema4"),publicKey,client,saves);var fourth=Fixture("0.4.0",4) with{SaveSchema=4,MinimumHubVersion="0.3.0"};
 await epoch4.InstallAsync(Sign(fourth),null,null,CancellationToken.None);handler.Requests.Clear();
 await epoch4.InstallAsync(Sign(fourth with{Version="0.4.1",Sequence=5}),null,null,CancellationToken.None);
 Assert(handler.Requests.Count==0,"schema4 update reuses all verified components");
 await Reject(()=>epoch4.InstallAsync(Sign(Fixture("0.4.2",6) with{SaveSchema=1}),null,null,CancellationToken.None),"schema4 cannot migrate to epoch1");
 await Reject(()=>Task.Run(()=>InstallManager.RequireSpace(1024,1023)),"low disk policy refuses insufficient staging space");
 InstallManager.RequireSpace(1024,1024);Assert(true,"exact required disk space accepted");
 var interrupted=Path.Combine(epoch4.Root,"staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(interrupted);File.WriteAllText(Path.Combine(interrupted,"orphan"),"partial");
 await epoch4.InstallAsync(Sign(fourth with{Version="0.4.2",Sequence=6}),null,null,CancellationToken.None);
 Assert(!Directory.Exists(interrupted) && epoch4.State().HighestSequence==6,"interrupted staging recovered before activation");
 var nestedImport=Path.Combine(folder,"nested-import");Directory.CreateDirectory(Path.Combine(nestedImport,"EvidenceGenesis"));
 foreach(var package in fourth.Packages)foreach(var file in package.Files){var path=Path.Combine(nestedImport,"EvidenceGenesis",Metadata.Mount(package.Kind),file.Path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var z=new ZipArchive(new MemoryStream(handler.Data[package.Url]));using var input=z.GetEntry(file.Path)!.Open();using var output=File.Create(path);input.CopyTo(output);}
 var nestedManager=new InstallManager(Path.Combine(folder,"nested-managed"),publicKey,client,saves);handler.Requests.Clear();await nestedManager.InstallAsync(Sign(fourth),nestedImport,null,CancellationToken.None);
 Assert(handler.Requests.Count==0,"documented nested import layout reuses verified files");
 var legacy=new InstallManager(Path.Combine(folder,"legacy-forward"),publicKey,client,saves);await legacy.InstallAsync(envelope,null,null,CancellationToken.None);var legacySave=Path.Combine(saves,"workshop.json");File.WriteAllText(legacySave,"legacy original preserved");handler.Requests.Clear();
 await legacy.InstallAsync(Sign(fourth),null,null,CancellationToken.None);
 Assert(Metadata.Verify(legacy.State().Current!.Envelope,publicKey,offlineInstalled:true).Manifest.SaveSchema==4,"reviewed Genesis0.3 epoch1 to0.4 epoch4 forward transition activates");
 Assert(File.ReadAllText(legacySave)=="legacy original preserved" && Directory.GetFiles(Path.Combine(legacy.Root,"save-backups"),"workshop.json",SearchOption.AllDirectories).Length==1,"forward transition backs up and preserves original legacy profile");
 Assert(handler.Requests.Count==1,"legacy forward transition reuses verified unchanged runtime and model");
 await Reject(()=>legacy.RollbackAsync(CancellationToken.None),"forward transition cannot roll back to legacy save epoch");
 await Reject(()=>epoch4.InstallAsync(Sign(fourth with{Version="0.4.3",Sequence=7}),null,null,CancellationToken.None,()=>throw new IOException("Catalog authorization expired before activation")),"final catalog authorization checked before activation");
 Assert(epoch4.State().HighestSequence==6,"failed activation authorization preserves previous pointer and sequence");
 var cacheFile=Path.Combine(epoch4.Root,"cache","test.cache");File.WriteAllText(cacheFile,"cache");
 epoch4.ClearDownloadStorage();Assert(!File.Exists(cacheFile) && epoch4.State().Current is not null && File.Exists(Path.Combine(saves,"save.json")),"storage cleanup preserves active version and personal saves");
 using(var held=new FileStream(Path.Combine(epoch4.Root,"install.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))await Reject(()=>Task.Run(()=>epoch4.ClearDownloadStorage()),"storage cleanup obeys operation lock");
 var malicious=new InstallManager(Path.Combine(folder,"malicious-state"),publicKey,client,saves);Directory.CreateDirectory(malicious.Root);File.WriteAllText(Path.Combine(malicious.Root,"active.json"),"{\"current\":{\"directory\":\"../outside\",\"envelope\":\"untrusted\"},\"highestSequence\":0}");
 await Reject(()=>Task.Run(()=>malicious.ClearDownloadStorage()),"storage cleanup rejects malicious active state");
 // Import a full trusted fixture from a separate folder; reuse by hashes, not an unsigned local version label.
 var importRoot=Path.Combine(folder,"import");Directory.CreateDirectory(importRoot);
 foreach(var package in first.Packages)foreach(var file in package.Files){var path=Path.Combine(importRoot,Metadata.Mount(package.Kind),file.Path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var z=new ZipArchive(new MemoryStream(handler.Data[package.Url]));using var input=z.GetEntry(file.Path)!.Open();using var output=File.Create(path);input.CopyTo(output);}
 var imported=new InstallManager(Path.Combine(folder,"import-managed"),publicKey,client,saves);handler.Requests.Clear();await imported.InstallAsync(envelope,importRoot,progress,CancellationToken.None);
 Assert(handler.Requests.Count==0,"verified imported installation uses no downloads");
 Assert(File.Exists(Path.Combine(importRoot,"EvidenceGenesis.exe")),"import source preserved");
 using(var redirects=new DownloadClient(new RedirectHandler())) await Reject(()=>redirects.MetadataAsync(CancellationToken.None),"untrusted redirect rejected");
 // Exercise the actual publisher tool with licensed fixture files and an ephemeral key.
 Directory.CreateDirectory(Path.Combine(importRoot,"licenses"));
 File.WriteAllText(Path.Combine(importRoot,"licenses","Qwen-LICENSE.txt"),"test license");
 File.WriteAllText(Path.Combine(importRoot,"licenses","llama.cpp-LICENSE.txt"),"test license");
 async Task<string> PackageTool(string destination,string version,long sequence) {
  var info=new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet") {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in new[]{"run","--project","tools/release/ReleaseTool.csproj","-c","Release","--",importRoot,destination,version,sequence.ToString(),new string('a',40),"genesis-v"+version})info.ArgumentList.Add(arg);
  info.Environment["RELEASE_SIGNING_KEY_PEM"]=key.ExportPkcs8PrivateKeyPem();info.Environment["GENESIS_RELEASE_PUBLIC_KEY_PEM"]=publicKey;
  info.Environment.Remove("PREVIOUS_RELEASE_METADATA_PATH");
  using var child=System.Diagnostics.Process.Start(info)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();await child.WaitForExitAsync();
  var output=await stdout;var errors=await stderr;Assert(child.ExitCode==0,"publisher tool executes: "+(child.ExitCode==0?"success":output+errors));
  return File.ReadAllText(Path.Combine(destination,"genesis-stable.json"));
 }
 var packaged=await PackageTool(Path.Combine(folder,"publisher-one"),"0.3.0",1);
 var packagedRelease=Metadata.Verify(packaged,publicKey);
 Assert(packagedRelease.Manifest.Packages.Length==3,"publisher emits signed separate components");
 Assert(packagedRelease.Manifest.SaveSchema==4 && packagedRelease.Manifest.MinimumHubVersion=="0.3.0","publisher binds saveSchema4 and Hub0.3 minimum");
 var packagedAgain=Metadata.Verify(await PackageTool(Path.Combine(folder,"publisher-two"),"0.3.1",2),publicKey);
 Assert(packagedRelease.Manifest.Packages.Select(p=>p.Sha256).SequenceEqual(packagedAgain.Manifest.Packages.Select(p=>p.Sha256)),"unchanged package archive hashes stable across fixture versions");
 var licensedImport=new InstallManager(Path.Combine(folder,"licensed-import"),publicKey,client,saves);handler.Requests.Clear();
 await licensedImport.InstallAsync(packaged,importRoot,null,CancellationToken.None);
 Assert(handler.Requests.Count==0,"publisher package import reuses verified models and license notices");
 var transport=Pack("model","genesis.gguf",RandomNumberGenerator.GetBytes(1000000),"fixture-tls");
 var transportBody=handler.Data[transport.Url];
 using(var https=new HttpsFixture(transportBody)) using(var network=new DownloadClient(https.Handler())) {
  var target=Path.Combine(folder,"tls-package.zip");
  await network.DownloadAsync(transport,target,progress,CancellationToken.None);
  Assert(await InstallManager.Matches(target,transport.Size,transport.Sha256,CancellationToken.None),"real loopback HTTPS download and digest");
 }
 using(var https=new HttpsFixture(transportBody,10)) using(var network=new DownloadClient(https.Handler())) using(var cancel=new CancellationTokenSource()) {
  var target=Path.Combine(folder,"tls-canceled.zip");
  await Reject(()=>network.DownloadAsync(transport,target,new InlineProgress(p=>cancel.Cancel()),cancel.Token),"mid-transfer TLS cancellation");
  Assert(!File.Exists(target)&&!Directory.GetFiles(folder,"*.part").Any(),"canceled TLS transfer leaves no committed artifact");
 }
 using(var https=new HttpsFixture(transportBody,truncate:true)) using(var network=new DownloadClient(https.Handler())) {
  await Reject(()=>network.DownloadAsync(transport,Path.Combine(folder,"tls-truncated.zip"),null,CancellationToken.None),"truncated HTTPS response rejected");
 }
 Console.WriteLine($"UPDATER PASS: {passed} assertions; ephemeral signing key and synthetic package fixtures, not production releases.");
} finally { Directory.Delete(folder,true); }
sealed class InlineProgress(Action<TransferProgress> report) : IProgress<TransferProgress> {public void Report(TransferProgress value)=>report(value);}
sealed class PackagesHandler : HttpMessageHandler {
 public Dictionary<string,byte[]> Data=new();public List<string> Requests=new();
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();var url=request.RequestUri!.ToString();Requests.Add(url);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Data[url])});}
}
sealed class RedirectHandler : HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new Uri("https://evil.invalid/steal");return Task.FromResult(r);}
}
