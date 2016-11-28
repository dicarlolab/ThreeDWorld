from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

import argparse
import time
import os
import json
from importlib import import_module

import pymongo as pm
import numpy as np

from six.moves import xrange
import tensorflow as tf

from curiosity.utils import error
from curiosity.utils.loadsave import (get_checkpoint_path,
                                      preprocess_config,
                                      postprocess_config)


def isint(x):
  try:
    int(x)
  except:
    return False
  else:
    return True


def run(dbname,
        colname,
        experiment_id,
        model_func,
        model_func_kwargs,
        data_func,
        data_func_kwargs,
        num_train_steps,
        batch_size,
        slippage=0,
        slippage_error=False,
        seed=0,
        cfgfile=None,
        savedir='.',
        dosave=True,
        base_learningrate=1.0,
        decaystep=100000,
        decayrate=0.95,
        loss_threshold=100,
        test_frequency=20,
        save_multiple=1,
        erase_earlier=None,
        additional_metrics=None):
  conn = pm.MongoClient('localhost', 29101)
  db = conn[dbname]
  coll = db[colname]
  r = coll.find_one({"experiment_id": experiment_id, 'saved_filters': True})
  if r:
    init = False
    r = coll.find_one({'experiment_id': experiment_id, 'step': -1})
    assert r
    cfg1 = postprocess_config(r['cfg'])
    seed = r['seed']
    cfg0 = postprocess_config(r['cfg0'])
  else:
    init = True
    cfg1 = None
    if cfgfile is not None:
      cfg0 = postprocess_config(json.load(open(cfgfile)))
    else:
      cfg0 = {}

  rng = np.random.RandomState(seed=seed)

  outnodedict, innodedict, cfg = model_func(rng, batch_size, cfg0, slippage, slippage_error, **model_func_kwargs)
  assert 'loss' in outnodedict
  outnodenames, outnodes = map(list, zip(*outnodedict.items()))

  if not init:
    assert cfg1 == cfg, (cfg1, cfg)
  else:
    assert not coll.find_one({'experiment_id': experiment_id, 'saved_filters': True})
    rec = {'experiment_id': experiment_id,
           'cfg': preprocess_config(cfg),
           'seed': seed,
           'cfg0': preprocess_config(cfg0),
           'step': -1}
    coll.insert(rec)

  batch = tf.Variable(0, trainable=False)
  learning_rate = tf.train.exponential_decay(
      base_learningrate,                # Base learning rate.
      batch * batch_size,  # Current index into the dataset.
      decaystep,          # Decay step.
      decayrate,                # Decay rate.
      staircase=True)

  optimizer = tf.train.MomentumOptimizer(learning_rate, 
                                         0.9).minimize(outnodedict['loss'], 
                                                       global_step=batch)

  outnodenames1 = outnodenames + ['learning_rate', 'optimizer']
  outnodes1 = outnodes + [learning_rate, optimizer]

  sdir = os.path.join(savedir, dbname, colname, experiment_id)
  if not os.path.exists(sdir):
    os.makedirs(sdir)

  start_time = time.time()
  with tf.Session() as sess:
    if init or not dosave:
      tf.initialize_all_variables().run()
      print('Initialized!')
      step0 = -1
    else:
      step0 = max(coll.find({'experiment_id': experiment_id,
                             'saved_filters': True}).distinct('step'))
      Vars = tf.all_variables()
      for v in Vars:
        pth = get_checkpoint_path(sdir, v.name.replace('/', '__'), step0)
        val = np.load(pth)
        sess.run(v.assign(val))
      print("Restored from %s at timestep %d" % (sdir, step0))

    for step in xrange(step0 + 1, num_train_steps // batch_size):
      batch_data = data_func(step, batch_size, **data_func_kwargs)
      feed_dict = {innodedict[k]: batch_data[k] for k in innodedict}
      outvals = sess.run(outnodes1, feed_dict=feed_dict)
      outval_dict = dict(zip(outnodenames1[:-1], outvals[:-1]))
      lossval = outval_dict['loss']
      learning_rate_val = outval_dict['learning_rate']
      print('Step: %d, loss: %f, learning rate: %f' % (step, 
                                                       lossval,
                                                       learning_rate_val))
      if lossval > loss_threshold:
        raise error.HiLossError("Loss: %.3f, Thres: %.3f" % (lossval, loss_threshold))

      for outnodename, outnodeval in outval_dict.items():
        spath = os.path.join(sdir, '%s.npy' % outnodename)
        np.save(spath, outnodeval)
      bfile = os.path.join(sdir, 'batchfile.txt')
      with open(bfile, 'w') as _f:
        _f.write(str(step))
      
      if additional_metrics is None:
        additional_metrics = {}
      metrics = {}
      for metric_name, metric_func in additional_metrics.items():
        metric_val = metric_func(batch_data, outval_dict)
        metrics[metric_name] = metric_val
      if additional_metrics:
        print(metrics)

      if step % test_frequency == 0:
        if dosave and (step % (test_frequency * save_multiple) == 0):
          Vars = tf.all_variables()
          for v in Vars:
            pth = get_checkpoint_path(sdir, v.name.replace('/', '__'), step)
            val = v.eval()
            np.save(pth, val)
            if erase_earlier:
              dirn = os.path.split(pth)[0]
              L = os.listdir(dirn)
              nL = [int(_l[:-4]) for _l in L if _l.endswith('.npy') and isint(_l[:-4])]
              nL.sort()
              for _l in nL[:-erase_earlier]:
                delpth = os.path.join(dirn, str(_l) + '.npy')
                os.remove(delpth)            
          saved_filters = True
        else:
          saved_filters = False
        rec = {'experiment_id': experiment_id,
               'cfg': preprocess_config(cfg),
               'saved_filters': saved_filters,
               'step': step,
               'loss': float(lossval),
               'learning_rate': float(learning_rate_val)}
        if metrics:
          rec['metrics'] = metrics
        coll.insert(rec)


def get_cli():
  parser = argparse.ArgumentParser()
  parser.add_argument('dbname', type=str, help="dbname string value")
  parser.add_argument('colname', type=str, help="colname string value")
  parser.add_argument('experiment_id', type=str, help="Experiment ID string value")
  parser.add_argument('model_func', type=str, help="Model Func Path")  
  parser.add_argument('--model_func_kwargs', type=json.loads, help="Model Func Kwargs", default='{}')  
  parser.add_argument('data_func', type=str, help="Data Func Path")
  parser.add_argument('--data_func_kwargs', type=json.loads, help="Data Func Kwargs", default='{}')  
  parser.add_argument('num_train_steps', type=int, help="Number of training steps")
  parser.add_argument('batch_size', type=int, help="Batch Size")
  parser.add_argument('--seed', type=int, help='seed for config', default=0)
  parser.add_argument('--cfgfile', type=str, help="Config to load model specs from")
  parser.add_argument('--slippage', type=float, default=0., help="slippage to depart from given config")
  parser.add_argument('--slippage_error', type=bool, default=False, help="raise error if slippage is nonzero but changes made")
  parser.add_argument('--savedir', type=str, default='.')
  parser.add_argument('--dosave', type=int, default=1)
  parser.add_argument('--base_learningrate', type=float, default=1.)
  parser.add_argument('--decaystep', type=int, default=100000)
  parser.add_argument('--decayrate', type=float, default=0.95)
  parser.add_argument('--num_train_steps', type=int, default=2048000)
  parser.add_argument('--erase_earlier', type=int, default=0)
  return parser
  

def main():
  parser = get_cli()
  args = vars(parser.parse_args())
  model_func_module, model_func_obj = args['model_func'].rsplit('.', 1)
  model_func_module = import_module(model_func_module)
  model_func = getattr(model_func_module, model_func_obj)
  args['model_func'] = model_func
  data_func_module, data_func_obj = args['data_func'].rsplit('.', 1)
  data_func_module = import_module(data_func_module)
  data_func = getattr(data_func_module, data_func_obj)
  args['data_func'] = data_func
  run(**args)
  
