"""
coupled symmetric model with from-below coupling
     --top-down is freely parameterized num-channels but from-below and top-down have same spatial extent 
     --top-down and bottom-up are combined via convolution to the correct num-channel shape:
        I = ReluConv(concat(top_down, bottom_up))
     --error is compuated as:
       (future_bottom_up - current_I)**2
"""
from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

import numpy as np
import tensorflow as tf
import zmq

from curiosity.utils.io import recv_array

ctx = zmq.Context()
sock = None
IMAGE_SIZE = None
NUM_CHANNELS = None
ACTION_LENGTH = None

def initialize(host, port, datapath, keyname):
  global ctx, sock, IMAGE_SIZE, NUM_CHANNELS, ACTION_LENGTH
  sock = ctx.socket(zmq.REQ)
  print("connecting...")
  sock.connect("tcp://%s:%d" % (host, port))
  print("...connected")
  sock.send_json({'batch_size': 1,
                  'batch_num': 0,
                  'path': datapath,
                  'keys': [(keyname, 'images0'), 
                           (keyname, 'actions')]
                 })
  images = recv_array(sock)
  actions = recv_array(sock)
  IMAGE_SIZE = images.shape[1]
  NUM_CHANNELS = images.shape[-1]
  ACTION_LENGTH = actions.shape[1]


def getEncodeDepth(rng, cfg, slippage=0):
  val = None
  if 'encode_depth' in cfg:
    val = cfg['encode_depth']
  elif 'encode' in cfg:
    val = max(cfg['encode'].keys())
  if val is not None and rng.uniform() > slippage:
    return val
  d = rng.choice([1, 2, 3, 4, 5])
  return d

def getEncodeConvFilterSize(i, encode_depth, rng, cfg, prev=None, slippage=0):
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'conv' in cfg['encode'][i]:
      if 'filter_size' in cfg['encode'][i]['conv']:
        val = cfg['encode'][i]['conv']['filter_size']  
  if val is not None and rng.uniform() > slippage:
    return val
  L = [1, 3, 5, 7, 9, 11, 13, 15, 23]
  if prev is not None:
    L = [_l for _l in L if _l <= prev]
  return rng.choice(L)

def getEncodeConvNumFilters(i, encode_depth, rng, cfg, slippage=0):
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'conv' in cfg['encode'][i]:
      if 'num_filters' in cfg['encode'][i]['conv']:
        val = cfg['encode'][i]['conv']['num_filters']
  if val is not None and rng.uniform() > slippage:
    return val
  L = [3, 48, 96, 128, 256, 128]
  return L[i]
  
def getEncodeConvStride(i, encode_depth, rng, cfg, slippage=0):
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'conv' in cfg['encode'][i]:
      if 'stride' in cfg['encode'][i]['conv']:
        val = cfg['encode'][i]['conv']['stride']
  if val is not None and rng.uniform() > slippage:
    return val
  if encode_depth > 1:
    return 2 if i == 1 else 1
  else:
    return 3 if i == 1 else 1

def getEncodeDoPool(i, encode_depth, rng, cfg, slippage=0):
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'do_pool' in cfg['encode'][i]:
      val = cfg['encode'][i]['do_pool']
    elif 'pool' in cfg['encode'][i]:
      val = True
  if val is not None and rng.uniform() > slippage:
    return val
  if i < 3 or i == encode_depth:
    return rng.uniform() < .75
  else:
    return rng.uniform() < .25
    
def getEncodePoolFilterSize(i, encode_depth, rng, cfg, slippage=0):
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'pool' in cfg['encode'][i]:
      if 'filter_size' in cfg['encode'][i]['pool']:
        val = cfg['encode'][i]['pool']['filter_size']
  if val is not None and rng.uniform() > slippage:
    return val
  return rng.choice([2, 3, 4, 5])

def getEncodePoolStride(i, encode_depth, rng, cfg, slippage=0):  
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'pool' in cfg['encode'][i]:
      if 'stride' in cfg['encode'][i]['pool']:
        val = cfg['encode'][i]['pool']['stride']
  if val is not None and rng.uniform() > slippage:
    return val
  return 2

def getEncodePoolType(i, encode_depth, rng, cfg, slippage=0):
  val = None
  if 'encode' in cfg and (i in cfg['encode']):
    if 'pool' in cfg['encode'][i]:
      if 'type' in cfg['encode'][i]['pool']:
        val = cfg['encode'][i]['pool']['type']
  if val is not None and rng.uniform() > slippage:
    return val
  return rng.choice(['max', 'avg'])

def getHiddenDepth(rng, cfg, slippage=0):
  val = None
  if (not rng.uniform() < slippage) and 'hidden_depth' in cfg:
    val = cfg['hidden_depth']
  elif 'hidden' in cfg:
    val = max(cfg['hidden'].keys())
  if val is not None and rng.uniform() > slippage:
    return val
  d = rng.choice([1, 2, 3])
  return d

def getHiddenNumFeatures(i, hidden_depth, rng, cfg, slippage=0):
  val = None
  if 'hidden' in cfg and (i in cfg['hidden']):
    if 'num_features' in cfg['hidden'][i]:
      val = cfg['hidden'][i]['num_features']
  if val is not None and rng.uniform() > slippage:
    return val
  return 1024

def getFilterSeed(rng, cfg):
  if 'filter_seed' in cfg:
    return cfg['filter_seed']
  else:  
    return rng.randint(10000)


def model(current_node, future_node, actions_node, rng, cfg, slippage=0, slippage_error=False):
  """The Model definition."""
  cfg0 = {} 

  fseed = getFilterSeed(rng, cfg)
  
  #encoding
  nf0 = NUM_CHANNELS
  imsize = IMAGE_SIZE
  encode_depth = getEncodeDepth(rng, cfg, slippage=slippage)
  cfg0['encode_depth'] = encode_depth
  print('Encode depth: %d' % encode_depth)
  encode_nodes_current = []
  encode_nodes_current.append(current_node)
  encode_nodes_future = []
  encode_nodes_future.append(future_node)
  cfs0 = None
  cfg0['encode'] = {}
  for i in range(1, encode_depth + 1):
    cfg0['encode'][i] = {}
    cfs = getEncodeConvFilterSize(i, encode_depth, rng, cfg, prev=cfs0, slippage=slippage)
    cfg0['encode'][i]['conv'] = {'filter_size': cfs}
    cfs0 = cfs
    nf = getEncodeConvNumFilters(i, encode_depth, rng, cfg, slippage=slippage)
    cfg0['encode'][i]['conv']['num_filters'] = nf
    cs = getEncodeConvStride(i, encode_depth, rng, cfg, slippage=slippage)
    cfg0['encode'][i]['conv']['stride'] = cs
    W = tf.Variable(tf.truncated_normal([cfs, cfs, nf0, nf],
                                        stddev=0.01,
                                        seed=fseed))
    new_encode_node_current = tf.nn.conv2d(encode_nodes_current[i-1], W,
                               strides = [1, cs, cs, 1],
                               padding='SAME')
    new_encode_node_current = tf.nn.relu(new_encode_node_current)
    new_encode_node_future = tf.nn.conv2d(encode_nodes_future[i-1], W,
                               strides = [1, cs, cs, 1],
                               padding='SAME')
    new_encode_node_future = tf.nn.relu(new_encode_node_future)
    b = tf.Variable(tf.zeros([nf]))
    new_encode_node_current = tf.nn.bias_add(new_encode_node_current, b)
    new_encode_node_future = tf.nn.bias_add(new_encode_node_future, b)
    imsize = imsize // cs
    print('Encode conv %d with size %d stride %d num channels %d numfilters %d for shape' % (i, cfs, cs, nf0, nf), new_encode_node_current.get_shape().as_list())    
    do_pool = getEncodeDoPool(i, encode_depth, rng, cfg, slippage=slippage)
    if do_pool:
      pfs = getEncodePoolFilterSize(i, encode_depth, rng, cfg, slippage=slippage)
      cfg0['encode'][i]['pool'] = {'filter_size': pfs}
      ps = getEncodePoolStride(i, encode_depth, rng, cfg, slippage=slippage)
      cfg0['encode'][i]['pool']['stride'] = ps
      pool_type = getEncodePoolType(i, encode_depth, rng, cfg, slippage=slippage)
      cfg0['encode'][i]['pool']['type'] = pool_type
      if pool_type == 'max':
        pfunc = tf.nn.max_pool
      elif pool_type == 'avg':
        pfunc = tf.nn.avg_pool
      new_encode_node_current = pfunc(new_encode_node_current,
                          ksize = [1, pfs, pfs, 1],
                          strides = [1, ps, ps, 1],
                          padding='SAME')
      new_encode_node_future = pfunc(new_encode_node_future,
                          ksize = [1, pfs, pfs, 1],
                          strides = [1, ps, ps, 1],
                          padding='SAME')                        
      print('Encode %s pool %d with size %d stride %d for shape' % (pool_type, i, pfs, ps),
                    new_encode_node_current.get_shape().as_list())
      imsize = imsize // ps
    nf0 = nf

    encode_nodes_current.append(new_encode_node_current)   
    encode_nodes_future.append(new_encode_node_future)

  encode_node = encode_nodes_current[-1]
  enc_shape = encode_node.get_shape().as_list()
  encode_flat = tf.reshape(encode_node, [enc_shape[0], np.prod(enc_shape[1:])])
  print('Flatten to shape %s' % encode_flat.get_shape().as_list())

  encode_flat = tf.concat(1, [encode_flat, actions_node]) 
  #hidden
  nf0 = encode_flat.get_shape().as_list()[1]
  hidden_depth = getHiddenDepth(rng, cfg, slippage=slippage)
  cfg0['hidden_depth'] = hidden_depth
  hidden = encode_flat
  cfg0['hidden'] = {}
  for i in range(1, hidden_depth + 1):
    nf = getHiddenNumFeatures(i, hidden_depth, rng, cfg, slippage=slippage)
    cfg0['hidden'][i] = {'num_features': nf}
    W = tf.Variable(tf.truncated_normal([nf0, nf],
                                        stddev = 0.01,
                                        seed=fseed))    
    b = tf.Variable(tf.constant(0.01, shape=[nf]))
    hidden = tf.nn.relu(tf.matmul(hidden, W) + b)
    print('hidden layer %d %s' % (i, str(hidden.get_shape().as_list())))
    nf0 = nf

 
   
 
  return loss, pred, cfg0


def get_model(rng, batch_size, cfg, slippage, slippage_error,
              host, port, datapath, keyname,
              loss_multiple=1, diff_gated=False, diff_diff=0.1, diff_power=None):
  global sock
  if sock is None:
    initialize(host, port, datapath, keyname)

  current_node = tf.placeholder(
      tf.float32,
      shape=(batch_size, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  future_node = tf.placeholder(
        tf.float32,
      shape=(batch_size, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  actions_node = tf.placeholder(tf.float32,
                                shape=(batch_size,
                                       ACTION_LENGTH))

  loss, train_prediction, cfg = model(current_node, future_node, 
                                      actions_node,
                                      rng=rng, cfg=cfg, 
                                      slippage=slippage, 
                                      slippage_error=slippage_error)

  innodedict = {'current': current_node,
                'future': future_node,
                'actions': actions_node}

  outnodedict = {'train_prediction': train_prediction,
                 'loss': loss}
                
  return outnodedict, innodedict, cfg  
