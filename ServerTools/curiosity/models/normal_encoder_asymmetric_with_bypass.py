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

def initialize(host, port, datapath):
  global ctx, sock, NUM_OBJECTS, NUM_CHANNELS, IMAGE_SIZE
  sock = ctx.socket(zmq.REQ)
  print("connecting...")
  sock.connect("tcp://%s:%d" % (host, port))
  print("...connected")
  sock.send_json({'batch_size': 1,
                  'batch_num': 0,
                  'path': datapath,
                  'keys': [('randomperm', 'images')]
                 })
  images = recv_array(sock)
  NUM_CHANNELS = images.shape[-1]
  IMAGE_SIZE = images.shape[1]


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

def getDecodeDepth(rng, cfg, slippage=0):
  val = None
  if 'decode_depth' in cfg:
    val = cfg['decode_depth']
  elif 'decode' in cfg:
    val = max(cfg['decode'].keys())
  if val is not None and rng.uniform() > slippage:
    return val
  d = rng.choice([1, 2, 3])
  return d

def getDecodeNumFilters(i, decode_depth, rng, cfg, slippage=0):
  if i < decode_depth:
    val = None
    if 'decode' in cfg and (i in cfg['decode']):
      if 'num_filters' in cfg['decode'][i]:
        val = cfg['decode'][i]['num_filters']
    if val is not None and rng.uniform() > slippage:
      return val
    return 32
  else:
    return NUM_CHANNELS

def getDecodeFilterSize(i, decode_depth, rng, cfg, slippage=0):
  val = None
  if 'decode' in cfg and (i in cfg['decode']):
     if 'filter_size' in cfg['decode'][i]:
       val = cfg['decode'][i]['filter_size']
  if val is not None and rng.uniform() > slippage:
    return val
  return rng.choice([1, 3, 5, 7, 9, 11])

def getDecodeSize(i, decode_depth, init, final, rng, cfg, slippage=0):
  val = None
  if 'decode' in cfg and (i in cfg['decode']):
    if 'size' in cfg['decode'][i]:
      val = cfg['decode'][i]['size']
  if val is not None and rng.uniform() > slippage:
    return val
  s = np.log2(init)
  e = np.log2(final)
  increment = (e - s) / decode_depth
  l = np.around(np.power(2, np.arange(s, e, increment)))
  if len(l) < decode_depth + 1:
    l = np.concatenate([l, [final]])
  l = l.astype(np.int)
  return l[i]

def getDecodeBypass(i, encode_nodes, decode_size, decode_depth, rng, cfg, slippage=0):
  val = None
  if 'decode' in cfg and (i in cfg['decode']):
    if 'bypass' in cfg['decode'][i]:
      val = cfg['decode'][i]['bypass']
  #prevent error that can occur here if encode is not large enough due to slippage modification?
  if val is not None and rng.uniform() > slippage:
    return val 
  switch = rng.uniform() 
  print('sw', switch)
  if switch < 0.5:
    sdiffs = [e.get_shape().as_list()[1] - decode_size for e in encode_nodes]
    return np.abs(sdiffs).argmin()

def getFilterSeed(rng, cfg):
  if 'filter_seed' in cfg:
    return cfg['filter_seed']
  else:  
    return rng.randint(10000)
  

def model(data, rng, cfg, slippage=0, slippage_error=False):
  """The Model definition."""

  cfg0 = {} 

  fseed = getFilterSeed(rng, cfg)
  
  #encoding
  nf0 = NUM_CHANNELS 
  imsize = IMAGE_SIZE
  encode_depth = getEncodeDepth(rng, cfg, slippage=slippage)
  cfg0['encode_depth'] = encode_depth
  print('Encode depth: %d' % encode_depth)
  encode_nodes = []
  encode_nodes.append(data)
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
    new_encode_node = tf.nn.conv2d(encode_nodes[i-1], W,
                               strides = [1, cs, cs, 1],
                               padding='SAME')
    new_encode_node = tf.nn.relu(new_encode_node)
    b = tf.Variable(tf.zeros([nf]))
    new_encode_node = tf.nn.bias_add(new_encode_node, b)
    imsize = imsize // cs
    print('Encode conv %d with size %d stride %d num channels %d numfilters %d for shape' % (i, cfs, cs, nf0, nf), new_encode_node.get_shape().as_list())    
    do_pool = getEncodeDoPool(i, encode_depth, rng, cfg, slippage=slippage)
    if do_pool:
      pfs = getEncodePoolFilterSize(i, encode_depth, rng, cfg)
      cfg0['encode'][i]['pool'] = {'filter_size': pfs}
      ps = getEncodePoolStride(i, encode_depth, rng, cfg, slippage=slippage)
      cfg0['encode'][i]['pool']['stride'] = ps
      pool_type = getEncodePoolType(i, encode_depth, rng, cfg, slippage=slippage)
      cfg0['encode'][i]['pool']['type'] = pool_type
      if pool_type == 'max':
        pfunc = tf.nn.max_pool
      elif pool_type == 'avg':
        pfunc = tf.nn.avg_pool
      new_encode_node = pfunc(new_encode_node,
                          ksize = [1, pfs, pfs, 1],
                          strides = [1, ps, ps, 1],
                          padding='SAME')
      print('Encode %s pool %d with size %d stride %d for shape' % (pool_type, i, pfs, ps),
                    new_encode_node.get_shape().as_list())
      imsize = imsize // ps
    nf0 = nf

    encode_nodes.append(new_encode_node)   

  encode_node = encode_nodes[-1]
  enc_shape = encode_node.get_shape().as_list()
  encode_flat = tf.reshape(encode_node, [enc_shape[0], np.prod(enc_shape[1:])])
  print('Flatten to shape %s' % encode_flat.get_shape().as_list())

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

  #decode
  decode_depth = getDecodeDepth(rng, cfg, slippage=slippage)
  cfg0['decode_depth'] = decode_depth
  print('Decode depth: %d' % decode_depth)
  nf = getDecodeNumFilters(0, decode_depth, rng, cfg, slippage=slippage)
  cfg0['decode'] = {0: {'num_filters': nf}}
  ds = getDecodeSize(0, decode_depth, enc_shape[1], IMAGE_SIZE, rng, cfg, slippage=slippage)
  cfg0['decode'][0]['size'] = ds
  if ds * ds * nf != nf0:
    W = tf.Variable(tf.truncated_normal([nf0, ds * ds * nf],
                                        stddev = 0.01,
                                        seed=fseed))
    b = tf.Variable(tf.constant(0.01, shape=[ds * ds * nf]))
    hidden = tf.matmul(hidden, W) + b
    print("Linear from %d to %d for input size %d" % (nf0, ds * ds * nf, ds))
  decode = tf.reshape(hidden, [enc_shape[0], ds, ds, nf])  
  print("Unflattening to", decode.get_shape().as_list())
  for i in range(1, decode_depth + 1):
    nf0 = nf
    ds = getDecodeSize(i, decode_depth, enc_shape[1], IMAGE_SIZE, rng, cfg, slippage=slippage)
    cfg0['decode'][i] = {'size': ds}
    if i == decode_depth:
       assert ds == IMAGE_SIZE, (ds, IMAGE_SIZE)
    decode = tf.image.resize_images(decode, ds, ds)
    print('Decode resize %d to shape' % i, decode.get_shape().as_list())
    add_bypass = getDecodeBypass(i, encode_nodes, ds, decode_depth, rng, cfg, slippage=slippage)
    if add_bypass != None:
      bypass_layer = encode_nodes[add_bypass]
      bypass_shape = bypass_layer.get_shape().as_list()
      if bypass_shape[1] != ds:
        bypass_layer = tf.image.resize_images(bypass_layer, ds, ds)
      decode = tf.concat(3, [decode, bypass_layer])
      print('Decode bypass from %d at %d for shape' % (add_bypass, i), decode.get_shape().as_list())
      nf0 = nf0 + bypass_shape[-1]
      cfg0['decode'][i]['bypass'] = add_bypass
    cfs = getDecodeFilterSize(i, decode_depth, rng, cfg, slippage=slippage)
    cfg0['decode'][i]['filter_size'] = cfs
    nf = getDecodeNumFilters(i, decode_depth, rng, cfg, slippage=slippage)
    cfg0['decode'][i]['num_filters'] = nf
    if i == decode_depth:
      assert nf == NUM_CHANNELS, (nf, NUM_CHANNELS)
    W = tf.Variable(tf.truncated_normal([cfs, cfs, nf0, nf],
                                        stddev=0.1,
                                        seed=fseed))
    b = tf.Variable(tf.zeros([nf]))
    decode = tf.nn.conv2d(decode,
                          W,
                          strides=[1, 1, 1, 1],
                          padding='SAME')
    decode = tf.nn.bias_add(decode, b)
    print('Decode conv %d with size %d num channels %d numfilters %d for shape' % (i, cfs, nf0, nf), decode.get_shape().as_list())

    if i < decode_depth:  #add relu to all but last ... need this?
      decode = tf.nn.relu(decode)

  return decode, cfg0


def get_model(rng, batch_size, cfg, slippage, slippage_error, host, port, datapath):
  global sock
  if sock is None:
    initialize(host, port, datapath)

  image_node = tf.placeholder(tf.float32,
                              shape=(batch_size, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  normals_node = tf.placeholder(tf.float32,
                                shape=(batch_size, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))
  
  train_prediction, cfg = model(image_node, rng, cfg, slippage=slippage, slippage_error=slippage_error)

  norm = (IMAGE_SIZE**2) * NUM_CHANNELS * batch_size
  loss = tf.nn.l2_loss(train_prediction - normals_node) / norm

  innodedict = {'images': image_node,
                'normals': normals_node}

  outnodedict = {'train_predictions': train_prediction,
                 'loss': loss}

  return outnodedict, innodedict, cfg
