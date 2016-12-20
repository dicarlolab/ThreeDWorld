import os
import copy


def get_checkpoint_path(dirn, vname, step):
  cdir = os.path.join(dirn, vname)
  if not os.path.exists(cdir):
    os.makedirs(cdir)
  return os.path.join(cdir, '%d.npy' % step)


def preprocess_config(cfg):
  cfg = copy.deepcopy(cfg)
  for k in ['encode', 'decode', 'hidden']:
    if k in cfg:
      ks = cfg[k].keys()
      for _k in ks:
        assert isinstance(_k, int), _k
        cfg[k][str(_k)] = cfg[k].pop(_k)
  return cfg


def postprocess_config(cfg):
  cfg = copy.deepcopy(cfg)
  for k in ['encode', 'decode', 'hidden']:
    if k in cfg:
      ks = cfg[k].keys()
      for _k in ks:
        cfg[k][int(_k)] = cfg[k].pop(_k)
  return cfg
