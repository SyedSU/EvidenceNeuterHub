"""Delete only explicitly reviewed obsolete Actions artifacts; never runs or releases."""
import argparse
import datetime
import json
import os
from pathlib import Path
import urllib.error
import urllib.request

REPOSITORY='SyedSU/EvidenceNeuterHub'
NAME='EvidenceNeuterHub-Windows-0.3.0-test-evidence'


def validate_artifact(actual,approved,protected,now):
    artifact_id=approved.get('id')
    if type(artifact_id) is not int or artifact_id<=0 or artifact_id in protected:
        raise ValueError('Artifact is invalid or protected')
    for field in ('id','name','size_in_bytes','created_at','digest','workflow_run'):
        if actual.get(field)!=approved.get(field):raise ValueError('Artifact identity changed: '+field)
    if actual['name']!=NAME or actual['workflow_run']['head_branch']!='feat/coordinated-hub':
        raise ValueError('Only reviewed superseded coordinated-Hub test evidence can be cleaned')
    created=datetime.datetime.fromisoformat(actual['created_at'].replace('Z','+00:00'))
    if now-created<datetime.timedelta(days=7):raise ValueError('Artifact is too recent')
    if not isinstance(actual.get('digest'),str) or not actual['digest'].startswith('sha256:'):
        raise ValueError('Reviewed archive digest missing')


def cleanup(config,api,apply=False,now=None):
    if config.get('schema')!=1 or config.get('repository')!=REPOSITORY:
        raise ValueError('Cleanup is restricted to the reviewed Hub repository')
    now=now or datetime.datetime.now(datetime.timezone.utc)
    protected=set(config['protectedArtifactIds']);seen=set();ready=[];missing=[]
    # Validate the entire batch before the first deletion.
    for approved in config['approved']:
        artifact_id=approved.get('id')
        if type(artifact_id) is not int or artifact_id<=0 or artifact_id in seen or artifact_id in protected:
            raise ValueError('Duplicate, invalid or protected artifact ID')
        seen.add(artifact_id)
        actual=api('GET',artifact_id)
        if actual is None:missing.append(artifact_id);continue
        validate_artifact(actual,approved,protected,now);ready.append(approved)
    deleted=[]
    if apply:
        for approved in ready:
            actual=api('GET',approved['id'])
            if actual is None:missing.append(approved['id']);continue
            validate_artifact(actual,approved,protected,now)
            api('DELETE',approved['id']);deleted.append(approved['id'])
            print(json.dumps({'deletedArtifactId':approved['id'],'bytes':approved['size_in_bytes']}),flush=True)
    return {'repository':REPOSITORY,'dryRun':not apply,'reviewedArtifactIds':[item['id'] for item in ready],
            'deletedArtifactIds':deleted,'alreadyMissingIds':missing,
            'deletedBytes':sum(item['size_in_bytes'] for item in ready if item['id'] in deleted),
            'protectedArtifactIds':sorted(protected)}


def github_api(method,artifact_id):
    if method not in ('GET','DELETE') or type(artifact_id) is not int:raise ValueError('Unsupported cleanup operation')
    token=os.environ.get('GH_TOKEN')
    if not token:raise ValueError('Actions-scoped token required')
    url=f'https://api.github.com/repos/{REPOSITORY}/actions/artifacts/{artifact_id}'
    request=urllib.request.Request(url,method=method,headers={'Authorization':'Bearer '+token,
        'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','User-Agent':'EvidenceHub-reviewed-artifact-cleanup'})
    try:
        with urllib.request.urlopen(request,timeout=30) as response:
            if method=='DELETE':
                if response.status!=204:raise ValueError('Deletion was not acknowledged')
                return None
            return json.load(response)
    except urllib.error.HTTPError as error:
        if error.code==404 and method=='GET':return None
        raise


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('config',type=Path);parser.add_argument('--apply',action='store_true')
    args=parser.parse_args()
    if args.apply and (os.environ.get('GITHUB_REPOSITORY')!=REPOSITORY or os.environ.get('GITHUB_REF')!='refs/heads/main'):
        raise SystemExit('Deletion requires the reviewed repository main workflow')
    print(json.dumps(cleanup(json.loads(args.config.read_text()),github_api,args.apply),indent=2))
