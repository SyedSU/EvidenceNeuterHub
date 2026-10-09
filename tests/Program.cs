using EvidenceHub;
using System.Text.Json;
var root = Path.Combine(Path.GetTempPath(), "hub-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
int passed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); passed++; }
void Reject(Action action) { try { action(); } catch (IOException) { passed++; return; } throw new Exception("Expected rejection"); }
try {
 var store = new SettingsStore(Path.Combine(root,"preferences","settings.json"));
 Check(store.Load() == new HubSettings());
 var setting = new HubSettings(root,true); store.Save(setting); Check(store.Load()==setting);
 store.Save(setting with { MinimizeOnPlay=false }); Check(!store.Load().MinimizeOnPlay);
 Reject(()=>Genesis.Detect(Path.Combine(root,"absent"))); Reject(()=>Genesis.Detect(root));
 var exe=Path.Combine(root,"EvidenceGenesis.exe"); File.WriteAllText(exe,"not executable"); Reject(()=>Genesis.Detect(root));
 var pe = new byte[128]; pe[0]=0x4d; pe[1]=0x5a; pe[60]=64; pe[64]=0x50; pe[65]=0x45; pe[68]=0x64; pe[69]=0x86; File.WriteAllBytes(exe,pe);
 var found=Genesis.Detect(root); Check(!found.HasRuntime && !found.HasModel);
 var launch=Genesis.LaunchInfo(found); Check(launch.FileName==exe && launch.WorkingDirectory==root && !launch.UseShellExecute && launch.Arguments=="");
 Directory.CreateDirectory(Path.Combine(root,"brain")); Directory.CreateDirectory(Path.Combine(root,"model"));
 File.WriteAllText(Path.Combine(root,"brain","llama-server.exe"),""); File.WriteAllText(Path.Combine(root,"model","genesis.gguf"),""); Check(Genesis.Detect(root).HasRuntime && Genesis.Detect(root).HasModel);
 var child=Path.Combine(root,"EvidenceGenesis"); Directory.CreateDirectory(child); File.WriteAllBytes(Path.Combine(child,"EvidenceGenesis.exe"),pe); Reject(()=>Genesis.Detect(root));
 File.Delete(exe); Check(Genesis.Detect(root).Folder==child);
 pe[69]=0; File.WriteAllBytes(Path.Combine(child,"EvidenceGenesis.exe"),pe); Reject(()=>Genesis.Detect(root));
 var config=Path.Combine(root,"preferences","settings.json"); File.WriteAllText(config,"broken");
 try {store.Load(); throw new Exception("Expected corrupt settings failure");} catch(JsonException) {Check(File.ReadAllText(config)=="broken");}
 Check(!Directory.GetFiles(Path.GetDirectoryName(config)!,"*.tmp").Any());
 Console.WriteLine($"PASS: {passed} assertions. Synthetic PE fixtures test detection only; no real game launch claimed.");
} finally {Directory.Delete(root,true);}
