"""Read-only guard: never dispatch jobs into auto-created, unprotected environments."""
import json,subprocess,sys

def validate(value,policies,name):
 if value.get('name')!=name:raise ValueError('Expected existing environment')
 rules=[r for r in value.get('protection_rules',[]) if r.get('type')=='required_reviewers']
 if len(rules)!=1 or not rules[0].get('reviewers') or rules[0].get('prevent_self_review') is not True:raise ValueError('Independent human review required')
 if value.get('can_admins_bypass') is not False:raise ValueError('Admin bypass must be disabled')
 if value.get('deployment_branch_policy')!={'protected_branches':False,'custom_branch_policies':True}:raise ValueError('Explicit branch policy required')
 entries=policies.get('branch_policies',[])
 if len(entries)!=1 or entries[0].get('name')!='main' or entries[0].get('type')!='branch':raise ValueError('Main-only environment required')
 return True

if __name__=='__main__':
 for name in sys.argv[1:]:
  base='repos/SyedSU/EvidenceNeuterHub/environments/'+name
  def api(path):return json.loads(subprocess.check_output(['gh','api',path],text=True))
  validate(api(base),api(base+'/deployment-branch-policies'),name)
  print('Verified existing protected environment:',name)
