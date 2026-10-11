using EvidenceHub.Updates;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

try {
 if(args.Length==3 && args[0] is "genesis-install" or "genesis-cancel" or "genesis-rollback"){
  InstallManager.RejectLinks(Path.GetFullPath(args[1]));
  var operatorInput=JsonSerializer.Deserialize<EvidenceHub.Acceptance.GenesisInputs>(Read(args[1]),Metadata.Json) ?? throw new IOException("Missing operator inputs.");
  using var downloads=new DownloadClient();await EvidenceHub.Acceptance.GenesisAcceptance.RunAsync(args[0],operatorInput,args[2],downloads);
  Console.WriteLine("Operator core evidence collected; catalog gate not changed and release acceptance not automatically approved.");return;
 }
 if(args.Length!=3 || args[0]!="snapshot")throw new IOException("Usage: Acceptance snapshot <config.json> <new evidence directory>");
 var configPath=Path.GetFullPath(args[1]);InstallManager.RejectLinks(configPath);
 var config=JsonSerializer.Deserialize<Inputs>(File.ReadAllText(configPath),Metadata.Json) ?? throw new IOException("Missing inputs.");
 var output=Path.GetFullPath(args[2]);InstallManager.RejectLinks(output);
 if(Directory.Exists(output)||File.Exists(output))throw new IOException("Evidence destination must be new; existing evidence is never overwritten.");
 if(string.IsNullOrWhiteSpace(config.Checkpoint)||config.Roots is null||config.Roots.Length==0)throw new IOException("A checkpoint and at least one dedicated acceptance root are required.");
 var trees=new List<object>();
 foreach(var input in config.Roots){
  var root=Path.GetFullPath(input);InstallManager.RejectLinks(root);
  var relativeOutput=Path.GetRelativePath(root,output);
  if(relativeOutput=="." || (!Path.IsPathRooted(relativeOutput) && relativeOutput!=".." && !relativeOutput.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal)))throw new IOException("Evidence destination must be outside measured roots.");
  if(!Directory.Exists(root))throw new IOException("Measured root does not exist: "+root);
  var files=new List<object>();var pending=new Stack<string>();pending.Push(root);
  while(pending.Count>0){var dir=pending.Pop();InstallManager.RejectLinks(dir);foreach(var entry in Directory.EnumerateFileSystemEntries(dir)){InstallManager.RejectLinks(entry);if(Directory.Exists(entry))pending.Push(entry);else{using var stream=new FileStream(entry,FileMode.Open,FileAccess.Read,FileShare.Read);files.Add(new{path=Path.GetRelativePath(root,entry),size=stream.Length,sha256=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()});}}}
  trees.Add(new{root,files});
 }
 var metadata=new List<object>();
 if(config.CatalogEnvelope is not null || config.GenesisEnvelope is not null || config.HubEnvelope is not null){
  var catalog=Catalog.Verify(Read(config.CatalogEnvelope),Public(config.CatalogKey),config.CatalogSequence);
  metadata.Add(new{kind="catalog",version="schema1",sequence=catalog.Sequence,expires=catalog.Expires,envelopeSha256=Digest(Read(config.CatalogEnvelope))});
  if(config.GenesisEnvelope is not null){var game=Metadata.Verify(Read(config.GenesisEnvelope),Public(config.GenesisKey),config.GenesisSequence).Manifest;Catalog.MatchGenesis(catalog,game);metadata.Add(new{kind="genesis",version=game.Version,sequence=game.Sequence,saveSchema=game.SaveSchema,sourceCommit=game.SourceCommit,packages=game.Packages.Select(p=>new{p.Kind,p.Url,p.Size,p.Sha256}),envelopeSha256=Digest(Read(config.GenesisEnvelope))});}
  if(config.HubEnvelope is not null){var hub=HubUpdate.Verify(Read(config.HubEnvelope),Public(config.HubKey),config.PublisherSha256 ?? "",config.HubSequence,Environment.OSVersion.Version.Build);Catalog.MatchHub(catalog,hub);if(config.Installer is null)throw new IOException("Actual candidate installer required.");InstallManager.RejectLinks(Path.GetFullPath(config.Installer));await HubUpdate.VerifyInstallerAsync(hub,config.Installer,CancellationToken.None);metadata.Add(new{kind="hub",version=hub.Version,sequence=hub.Sequence,sourceCommit=hub.SourceCommit,installer=hub.Installer,envelopeSha256=Digest(Read(config.HubEnvelope)),authenticode="verified using supplied independently approved publisher pin"});}
 }
 var processes=new List<object>();foreach(var name in new[]{"EvidenceNeuterHub","EvidenceHubBootstrap","EvidenceGenesis"})foreach(var process in Process.GetProcessesByName(name))using(process){processes.Add(new{name,pid=process.Id,startTimeUtc=process.StartTime.ToUniversalTime(),path=process.MainModule?.FileName});}
 Directory.CreateDirectory(output);
 File.WriteAllText(Path.Combine(output,"snapshot.json"),JsonSerializer.Serialize(new{schema=1,checkpoint=config.Checkpoint,utc=DateTimeOffset.UtcNow,toolHubVersion=Metadata.HubVersion,platform=System.Runtime.InteropServices.RuntimeInformation.OSDescription,metadataVerified=metadata.Count>0,metadata,trees,processes,acceptance="NOT AUTOMATICALLY APPROVED; operator must review scenario evidence"},new JsonSerializerOptions(Metadata.Json){WriteIndented=true}));
 Console.WriteLine("Evidence collected. No installation, launch, save rewrite or release approval performed.");
}catch(Exception ex){Console.Error.WriteLine("Evidence collection stopped: "+ex.Message);Environment.ExitCode=1;}
static string Read(string? path){if(string.IsNullOrWhiteSpace(path))throw new IOException("Required metadata/public-key input missing.");InstallManager.RejectLinks(Path.GetFullPath(path));if(new FileInfo(path).Length>Metadata.MaxMetadataBytes)throw new IOException("Metadata/public-key input too large.");return File.ReadAllText(path);}
static string Public(string? path){var pem=Read(path);if(pem.Contains("PRIVATE KEY",StringComparison.Ordinal))throw new IOException("Public-key files only; never supply signing credentials.");return pem;}
static string Digest(string value)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
record Inputs(string Checkpoint,string[] Roots,string? CatalogEnvelope=null,string? CatalogKey=null,long CatalogSequence=0,string? GenesisEnvelope=null,string? GenesisKey=null,long GenesisSequence=0,string? HubEnvelope=null,string? HubKey=null,long HubSequence=0,string? PublisherSha256=null,string? Installer=null);
