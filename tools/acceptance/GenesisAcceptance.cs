using EvidenceHub.Updates;
using System.Security.Cryptography;
using System.Text.Json;

namespace EvidenceHub.Acceptance;
public record GenesisInputs(string Workspace,string GenesisKey,string? GenesisEnvelope,string Checkpoint,long CancelAtBytes=0);
public static class GenesisAcceptance
{
 public const string Marker="DISPOSABLE GENESIS ACCEPTANCE v1";
 public static async Task RunAsync(string action,GenesisInputs input,string output,DownloadClient downloads)
 {
  if(action is not ("genesis-install" or "genesis-cancel" or "genesis-rollback"))throw new IOException("Unsupported operator action.");
  var workspace=Path.GetFullPath(input.Workspace);InstallManager.RejectLinks(workspace);
  var allowed=Path.Combine(Path.GetTempPath(),"EvidenceNeuterAcceptance");
  if(Path.GetDirectoryName(workspace)!=Path.TrimEndingDirectorySeparator(allowed) || !Guid.TryParseExact(Path.GetFileName(workspace),"N",out _))throw new IOException("Use a dedicated GUID workspace under the OS temporary EvidenceNeuterAcceptance directory.");
  var marker=Path.Combine(workspace,".evidence-acceptance-isolated");InstallManager.RejectLinks(marker);
  if(!File.Exists(marker)||File.ReadAllText(marker).Trim()!=Marker)throw new IOException("Explicit disposable-workspace marker required.");
  if(string.IsNullOrWhiteSpace(input.Checkpoint))throw new IOException("Checkpoint required.");
  RejectTreeLinks(workspace);
  var managed=Path.Combine(workspace,"managed");var saves=Path.Combine(workspace,"saves");InstallManager.RejectLinks(managed);InstallManager.RejectLinks(saves);
  if(!Directory.Exists(saves))throw new IOException("Create the isolated saves child explicitly; personal/default save roots are never used.");
  output=Path.GetFullPath(output);InstallManager.RejectLinks(output);
  var relative=Path.GetRelativePath(workspace,output);
  if(Directory.Exists(output)||File.Exists(output)||relative=="."||(!Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal)))throw new IOException("New evidence output outside the workspace required.");
  var key=Read(input.GenesisKey);if(key.Contains("PRIVATE KEY",StringComparison.Ordinal))throw new IOException("Independently approved public key only.");
  string publicKeySha256;using(var rsa=RSA.Create()){rsa.ImportFromPem(key);if(rsa.KeySize<3072)throw new IOException("RSA trust key too small.");publicKeySha256=Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();}
  var manager=new InstallManager(managed,key,downloads,saves);var before=manager.State();var saveBefore=SaveHashes(saves);
  string? envelope=null;ReleaseManifest? release=null;
  if(action!="genesis-rollback") {envelope=Read(input.GenesisEnvelope);release=manager.VerifyNew(envelope).Manifest;if(release.SaveSchema!=4)throw new IOException("This operator milestone requires signed saveSchema4 metadata.");}
  else {if(before.Current is null||before.Previous is null)throw new IOException("Two actual installed versions required for rollback.");var current=Metadata.Verify(before.Current.Envelope,key,offlineInstalled:true).Manifest;var previous=Metadata.Verify(before.Previous.Envelope,key,offlineInstalled:true).Manifest;if(current.SaveSchema!=4||previous.SaveSchema!=4||current.Version==previous.Version)throw new IOException("Two different sameSchema4 versions required; cross-epoch rollback is never acceptance.");}
  if(action=="genesis-cancel" && input.CancelAtBytes<1)throw new IOException("Positive measured-byte cancellation threshold required.");
  Directory.CreateDirectory(output);using var progressLog=new StreamWriter(new FileStream(Path.Combine(output,"transfers.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){AutoFlush=true};
  using var cancel=new CancellationTokenSource();var downloaded=new Dictionary<string,long>();bool threshold=false;
  var progress=new Inline(value=>{progressLog.WriteLine(JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,progress=value},Metadata.Json));if(value.Phase=="Downloading"){downloaded[value.Component]=Math.Max(downloaded.GetValueOrDefault(value.Component),value.Bytes);if(action=="genesis-cancel"&&downloaded.Values.Sum()>=input.CancelAtBytes){threshold=true;cancel.Cancel();}}});
  string outcome="FAILED";Exception? failure=null;
  try {
   if(action=="genesis-rollback")await manager.RollbackAsync(cancel.Token);
   else await manager.InstallAsync(envelope!,null,progress,cancel.Token,()=>{if(action=="genesis-cancel")throw new IOException("Cancellation threshold was not reached; activation deliberately blocked.");});
   if(action=="genesis-cancel")throw new IOException("Cancellation was not observed; no cancellation acceptance is claimed.");
   await manager.CurrentAsync(CancellationToken.None);var after=manager.State();
   if(action=="genesis-install" && after.Current?.Envelope!=envelope)throw new IOException("Verified target did not activate.");
   if(action=="genesis-rollback" && (after.Current!=before.Previous||after.HighestSequence!=before.HighestSequence))throw new IOException("Rollback pointer/high-water evidence mismatch.");
   if(!Same(saveBefore,SaveHashes(saves)))throw new IOException("Acceptance save hashes changed.");outcome="COMPLETED_CORE_OPERATION";
  }catch(OperationCanceledException ex){if(action=="genesis-cancel"&&threshold&&JsonSerializer.Serialize(manager.State(),Metadata.Json)==JsonSerializer.Serialize(before,Metadata.Json)&&Same(saveBefore,SaveHashes(saves)))outcome="CANCELLED_AT_MEASURED_THRESHOLD_NO_ACTIVATION";else failure=ex;}
  catch(Exception ex){failure=ex;}
  var finalState=manager.State();var saveAfter=SaveHashes(saves);
  RejectTreeLinks(workspace);
  var partials=Directory.Exists(managed)?Directory.EnumerateFiles(managed,"*.part",SearchOption.AllDirectories).ToArray():Array.Empty<string>();
  if(outcome.StartsWith("CANCELLED",StringComparison.Ordinal)&&partials.Length!=0){outcome="FAILED";failure=new IOException("Canceled transfer left partial artifacts.");}
  File.WriteAllText(Path.Combine(output,"operation.json"),JsonSerializer.Serialize(new{schema=1,utc=DateTimeOffset.UtcNow,input.Checkpoint,action,outcome,failure=failure?.Message,workspace,publicKeySha256,manifestEnvelopeSha256=envelope is null?null:Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(envelope))).ToLowerInvariant(),manifest=release,before,after=finalState,saveBefore,saveAfter,downloadedBytes=downloaded,cancelThreshold=input.CancelAtBytes,thresholdReached=threshold,remainingPartials=partials,scope="Operator signed Genesis core only; catalog gate unchanged; game launch/UI/public gate acceptance NOT automatically approved"},new JsonSerializerOptions(Metadata.Json){WriteIndented=true}));
  if(failure is not null)throw new IOException("Operator core operation failed; preserve evidence: "+failure.Message,failure);
 }
 static void RejectTreeLinks(string root){var pending=new Stack<string>();pending.Push(root);while(pending.Count>0){var dir=pending.Pop();InstallManager.RejectLinks(dir);foreach(var entry in Directory.EnumerateFileSystemEntries(dir)){InstallManager.RejectLinks(entry);if(Directory.Exists(entry))pending.Push(entry);}}}
 static string Read(string? path){if(string.IsNullOrWhiteSpace(path))throw new IOException("Signed metadata/public key file required.");InstallManager.RejectLinks(Path.GetFullPath(path));if(new FileInfo(path).Length>Metadata.MaxMetadataBytes)throw new IOException("Input too large.");return File.ReadAllText(path);}
 static Dictionary<string,string> SaveHashes(string root){var result=new Dictionary<string,string>(StringComparer.Ordinal);var pending=new Stack<string>();pending.Push(root);while(pending.Count>0){var dir=pending.Pop();InstallManager.RejectLinks(dir);foreach(var path in Directory.EnumerateFileSystemEntries(dir)){InstallManager.RejectLinks(path);if(Directory.Exists(path))pending.Push(path);else{using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);result.Add(Path.GetRelativePath(root,path),file.Length+":"+Convert.ToHexString(SHA256.HashData(file)));}}}return result;}
 static bool Same(Dictionary<string,string> a,Dictionary<string,string> b)=>a.Count==b.Count&&a.All(pair=>b.GetValueOrDefault(pair.Key)==pair.Value);
 sealed class Inline(Action<TransferProgress> report):IProgress<TransferProgress>{public void Report(TransferProgress value)=>report(value);}
}
