import importlib.util,unittest,copy
from pathlib import Path
p=Path(__file__).parents[1]/'tools/release/check-environment.py'
spec=importlib.util.spec_from_file_location('guard',p);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class GuardTests(unittest.TestCase):
 def setUp(self):
  self.environment={'name':'hub-signing','can_admins_bypass':False,'protection_rules':[{'type':'required_reviewers','prevent_self_review':True,'reviewers':[{'id':1}]}],'deployment_branch_policy':{'protected_branches':False,'custom_branch_policies':True}}
  self.policies={'branch_policies':[{'name':'main','type':'branch'}]}
 def test_exact_reviewed_main_environment(self):self.assertTrue(module.validate(self.environment,self.policies,'hub-signing'))
 def test_missing_or_weakened_approval_rejected(self):
  for changes in [{'name':'other'},{'protection_rules':[]},{'can_admins_bypass':True},{'deployment_branch_policy':None}]:
   with self.assertRaises(ValueError):module.validate(dict(self.environment,**changes),self.policies,'hub-signing')
 def test_self_review_or_wildcard_rejected(self):
  self.environment['protection_rules'][0]['prevent_self_review']=False
  with self.assertRaises(ValueError):module.validate(self.environment,self.policies,'hub-signing')
  self.setUp();self.policies['branch_policies'][0]['name']='*'
  with self.assertRaises(ValueError):module.validate(self.environment,self.policies,'hub-signing')
