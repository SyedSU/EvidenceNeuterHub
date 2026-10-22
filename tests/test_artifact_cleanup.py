import copy
import datetime
import importlib.util
from pathlib import Path
import unittest

path=Path(__file__).parents[1]/'tools/release/artifact_cleanup.py'
spec=importlib.util.spec_from_file_location('artifact_cleanup',path)
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)


class ArtifactCleanupTests(unittest.TestCase):
 def setUp(self):
  self.item={'id':1,'name':module.NAME,'size_in_bytes':100,'created_at':'2026-10-01T00:00:00Z',
             'digest':'sha256:'+'a'*64,'workflow_run':{'head_branch':'feat/coordinated-hub','head_sha':'b'*40,'id':2}}
  self.config={'schema':1,'repository':module.REPOSITORY,'protectedArtifactIds':[3],'approved':[copy.deepcopy(self.item)]}
  self.now=datetime.datetime(2026,10,23,tzinfo=datetime.timezone.utc);self.deleted=[]
 def api(self,method,artifact_id):
  if method=='DELETE':self.deleted.append(artifact_id);return None
  return copy.deepcopy(self.item)
 def test_default_is_read_only_and_apply_deletes_exact_reviewed_identity(self):
  report=module.cleanup(self.config,self.api,now=self.now)
  self.assertTrue(report['dryRun']);self.assertEqual(self.deleted,[])
  report=module.cleanup(self.config,self.api,apply=True,now=self.now)
  self.assertEqual(self.deleted,[1]);self.assertEqual(report['deletedBytes'],100)
 def test_changed_identity_recent_protected_and_other_repo_fail_without_deletion(self):
  for field,value in [('name','release-installer'),('digest','sha256:'+'c'*64),('created_at','2026-10-22T00:00:00Z'),('size_in_bytes',101)]:
   with self.subTest(field=field):
    actual=copy.deepcopy(self.item);actual[field]=value
    with self.assertRaises(ValueError):module.cleanup(self.config,lambda method,identity:actual,apply=True,now=self.now)
  self.config['protectedArtifactIds']=[1]
  with self.assertRaises(ValueError):module.cleanup(self.config,self.api,apply=True,now=self.now)
  self.config['repository']='other/repository'
  with self.assertRaises(ValueError):module.cleanup(self.config,self.api,apply=True,now=self.now)
  self.assertEqual(self.deleted,[])
 def test_entire_batch_validated_before_any_deletion(self):
  second=copy.deepcopy(self.item);second['id']=2;self.config['approved'].append(second)
  with self.assertRaises(ValueError):module.cleanup(self.config,self.api,apply=True,now=self.now)
  self.assertEqual(self.deleted,[])
 def test_already_removed_artifacts_are_reported_without_delete(self):
  result=module.cleanup(self.config,lambda method,identity:None,apply=True,now=self.now)
  self.assertEqual(result['deletedArtifactIds'],[]);self.assertEqual(result['alreadyMissingIds'],[1])
 def test_identity_change_after_preflight_prevents_deletion(self):
  calls=[]
  def api(method,identity):
   calls.append(method)
   if len(calls)==1:return copy.deepcopy(self.item)
   altered=copy.deepcopy(self.item);altered['digest']='sha256:'+'c'*64
   return altered
  with self.assertRaises(ValueError):module.cleanup(self.config,api,apply=True,now=self.now)
  self.assertEqual(calls,['GET','GET'])
 def test_even_an_explicitly_listed_recent_archive_is_preserved(self):
  self.item['created_at']='2026-10-22T00:00:00Z'
  self.config['approved']=[copy.deepcopy(self.item)]
  with self.assertRaisesRegex(ValueError,'too recent'):module.cleanup(self.config,self.api,apply=True,now=self.now)
  self.assertEqual(self.deleted,[])
