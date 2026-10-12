"""Read-only collector checks using synthetic files; never production acceptance."""
import json, pathlib, subprocess, sys, tempfile
with tempfile.TemporaryDirectory() as d:
    base=pathlib.Path(d); root=base/'isolated';root.mkdir();save=root/'save.json';save.write_bytes(b'original synthetic save')
    cfg=base/'inputs.json'
    def run(inputs, output):
        cfg.write_text(json.dumps(inputs));return subprocess.run([sys.argv[1],sys.argv[2],'snapshot',str(cfg),str(output)],capture_output=True,text=True)
    valid={'checkpoint':'synthetic-before','roots':[str(root)]}
    report=base/'report';result=run(valid,report)
    assert result.returncode==0,result.stderr
    data=json.loads((report/'snapshot.json').read_text());assert not data['metadataVerified'];assert data['trees'][0]['files'][0]['size']==23
    assert save.read_bytes()==b'original synthetic save'
    result=run(valid,report);assert result.returncode!=0 and 'must be new' in result.stderr
    result=run(valid,root/'nested');assert result.returncode!=0 and 'outside measured roots' in result.stderr
    result=run(dict(valid,roots=[base.anchor]),base/'volume-report');assert result.returncode!=0 and 'outside measured roots' in result.stderr
    envelope=base/'catalog.json';envelope.write_text('{}');key=base/'key.pem';key.write_text('-----BEGIN PRIVATE KEY-----')
    result=run(dict(valid,catalogEnvelope=str(envelope),catalogKey=str(key)),base/'private');assert result.returncode!=0 and 'Public-key files only' in result.stderr;assert not (base/'private').exists()
    result=run(dict(valid,catalogEnvelope=str(envelope)),base/'unsigned');assert result.returncode!=0 and 'input missing' in result.stderr
    link=root/'linked';link.symlink_to(save)
    result=run(valid,base/'linked-report');assert result.returncode!=0 and not (base/'linked-report').exists()
print('PASS 7 collector checks: read-only snapshot, overwrite/nested-output/volume-root-output/private-key/missing-trust/link rejection; synthetic fixtures only.')
