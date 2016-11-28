from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

from StringIO import StringIO
import sys
import copy
import numpy as np
import time
import os
import zmq
import struct
import json
from PIL import Image

import gzip

from six.moves import urllib
from six.moves import xrange  # pylint: disable=redefined-builtin
import tensorflow as tf

IMAGE_SIZE = 256
NUM_CHANNELS = 3
PIXEL_DEPTH = 255
BATCH_SIZE = 64
NUM_TRAIN_STEPS = 10000000

tf.app.flags.DEFINE_boolean("self_test", False, "True if running a self test.")
FLAGS = tf.app.flags.FLAGS

OBJSET = None

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)

print("connecting...")
sock.connect("tcp://18.93.15.188:23042")
print("...connected")

sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))
print("...joined")

def norml(x):
   return (x - PIXEL_DEPTH/2.0) / PIXEL_DEPTH


def getNextBatch(N, start, rng):
  ims = []
  norms = []
  print('getting %d-%d' % (start, start + N))
  for i in range(N):
    timestep = start + i
    info, nstr, ostr, imstr = handle_message(sock, write=True, outdir='normal_encoder_simple', prefix=str(i))
    objarray = np.asarray(Image.open(StringIO(ostr)).convert('RGB'))
    normalsarray = np.asarray(Image.open(StringIO(nstr)).convert('RGB'))
    imarray = np.asarray(Image.open(StringIO(imstr)).convert('RGB'))
    msg = {'n': 4,
           'msg': {"msg_type": "CLIENT_INPUT",
                   "get_obj_data": False,
                   "actions": []}}
        

    #every so often moves to a new area
    if i % 5 == 0:
      print('teleporting at %d ... ' % timestep)
      msg['msg']['teleport_random'] = True
      a, b, c = [.3 * rng.uniform(), 0.15 * rng.uniform(), 0.3 * rng.uniform()]
      d, e, f = [0, 2 * rng.binomial(1, .5) - 1, 0]
    else:
      msg['msg']['vel'] = [a, b, c]
      msg['msg']['ang_vel'] = [d, e, f]
 
    ims.append(imarray)
    norms.append(normalsarray)
    sock.send_json(msg)

  batch = {'images': norml(np.array(ims)),
           'normals': norml(np.array(norms))}
  return batch


def error_rate(predictions, imgs):
  """Return the error rate based on dense predictions and sparse labels."""
  return 0.5 * ((predictions - imgs)**2).mean()


def getEncodeDepth(rng, cfg):
  if 'encode_depth' in cfg:
    return cfg['encode_depth']
  elif 'encode' in cfg:
    return len(cfg['encode'])
  else:
    #for i in range(10):
    #  print (rng.choice([1, 2, 3, 4, 5]))
    return rng.choice([1, 2, 3, 4, 5])

def getEncodeConvFilterSize(i, encode_depth, rng, cfg, prev=None):
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'conv' in cfg['encode'][i-1]:
      if 'filter_size' in cfg['encode']['conv']:
        return cfg['encode'][i-1]['con']['filter_size']
  L = [3, 5, 7, 9, 11, 13, 15]
  if prev is not None:
    L = [_l for _l in L if _l <= prev]
  return rng.choice(L)

def getEncodeConvNumFilters(i, encode_depth, rng, cfg):
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'conv' in cfg['encode'][i-1]:
      if 'num_filters' in cfg['encode'][i-1]['conv']:
        return cfg['encode'][i-1]['conv']['num_filters']
  L = [48, 96, 128, 256, 128]
  return L[i-1]

def getEncodeConvStride(i, encode_depth, rng, cfg):
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'conv' in cfg['encode'][i-1]:
      if 'stride' in cfg['encode'][i-1]['conv']:
        return cfg['encode'][i-1]['conv']['stride']
  if encode_depth > 1:
    return 2 if i == 1 else 1
  else:
    return 3 if i == 1 else 1

def getEncodeDoPool(i, encode_depth, rng, cfg):
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'do_pool' in cfg['encode'][i-1]:
      return cfg['encode'][i-1]['do_pool']
    elif 'pool' in cfg['encode'][i-1]:
      return True
  if i < 3 or i == encode_depth:
    return rng.uniform() < .75
  else:
    return rng.uniform() < .25

def getEncodePoolFilterSize(i, encode_depth, rng, cfg):
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'pool' in cfg['encode'][i-1]:
      if 'filter_size' in cfg['encode'][i-1]['pool']:
        return cfg['encode'][i-1]['pool']['filter_size']
  return rng.choice([2, 3, 5])

def getEncodePoolStride(i, encode_depth, rng, cfg):  
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'pool' in cfg['encode'][i-1]:
      if 'stride' in cfg['encode'][i-1]['pool']:
        return cfg['encode'][i-1]['pool']['stride']
  return 2

def getEncodePoolType(i, encode_depth, rng, cfg):
  if 'encode' in cfg and len(cfg['encode']) >= i:
    if 'pool' in cfg['encode'][i-1]:
      if 'type' in cfg['encode'][i-1]['pool']:
        return cfg['encode'][i-1]['pool']['type']
  return rng.choice(['max', 'avg'])

def getHiddenDepth(rng, cfg):
  if 'hidden_depth' in cfg:
    return cfg['hidden_depth']
  elif 'hidden' in cfg:
    return len(cfg['hidden'])
  else:
    return rng.choice([1, 2])
  
def getHiddenNumFeatures(i, hidden_depth, rng, cfg):
  if 'hidden' in cfg and len(cfg['hidden']) >= i:
    if 'num_features' in cfg['hidden'][i-1]:
      return cfg['hidden'][i-1]['num_features']
  return 1024

def getDecodeDepth(rng, cfg):
  if 'decode_depth' in cfg:
    return cfg['decode_depth']
  elif 'decode' in cfg:
    return len(cfg['decode'])
  else:
    return rng.choice([1, 2, 3])

def getDecodeNumFilters(i, decode_depth, rng, cfg):
  if i < decode_depth:
    if 'decode' in cfg and len(cfg['decode']) >= i+1:
      if 'num_filters' in cfg['decode'][i]:
        return cfg['decode'][i]['num_filters']
    return 32
  else:
    return NUM_CHANNELS

def getDecodeFilterSize(i, decode_depth, rng, cfg):
  if 'decode' in cfg and len(cfg['decode']) >= i+1:
     if 'filter_size' in cfg['decode'][i]:
       return cfg['decode'][i]['filter_size']
  return 7

def getDecodeSize(i, decode_depth, init, final, rng, cfg):
  if 'decode' in cfg and len(cfg['decode']) >= i+1:
    if 'size' in cfg['decode'][i]:
      return cfg['decode'][i]['size']
  s = np.log2(init)
  e = np.log2(final)
  increment = (e - s) / decode_depth
  l = np.around(np.power(2, np.arange(s, e, increment)))
  if len(l) < decode_depth + 1:
    l = np.concatenate([l, [final]])
  l = l.astype(np.int)
  return l[i]

def getDecodeBypass(i, encode_nodes, decode_size, decode_depth, rng, cfg):
  if 'decode' in cfg and len(cfg['decode']) >= i+1:
    if 'bypass' in cfg['decode'][i]:
      return cfg['decode'][i]['bypass']
  switch = rng.uniform() 
  print('sw', switch)
  if switch < .5:
    sdiffs = [e.get_shape().as_list()[1] - decode_size for e in encode_nodes]
    return np.abs(sdiffs).argmin()

def getFilterSeed(rng, cfg):
  if 'filter_seed' in cfg:
    return cfg['filter_seed']
  else:  
    return rng.randint(10000)
  

def model(data, rng, cfg):
  """The Model definition."""
  fseed = getFilterSeed(rng, cfg)
  
  #encoding
  nf0 = NUM_CHANNELS 
  imsize = IMAGE_SIZE
  encode_depth = getEncodeDepth(rng, cfg)
  print('Encode depth: %d' % encode_depth)
  encode_nodes = []
  encode_nodes.append(data)
  cfs0 = None
  for i in range(1, encode_depth + 1):
    cfs = getEncodeConvFilterSize(i, encode_depth, rng, cfg, prev=cfs0)
    cfs0 = cfs
    nf = getEncodeConvNumFilters(i, encode_depth, rng, cfg)
    cs = getEncodeConvStride(i, encode_depth, rng, cfg)
    print(cfs, nf, cs, nf0, encode_nodes[i-1].get_shape().as_list())
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
    do_pool = getEncodeDoPool(i, encode_depth, rng, cfg)
    if do_pool:
      pfs = getEncodePoolFilterSize(i, encode_depth, rng, cfg)
      ps = getEncodePoolStride(i, encode_depth, rng, cfg)
      pool_type = getEncodePoolType(i, encode_depth, rng, cfg)
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
  hidden_depth = getHiddenDepth(rng, cfg)
  hidden = encode_flat
  for i in range(1, hidden_depth + 1):
    nf = getHiddenNumFeatures(i, hidden_depth, rng, cfg)
    W = tf.Variable(tf.truncated_normal([nf0, nf],
                                        stddev = 0.01,
                                        seed=fseed))    
    b = tf.Variable(tf.constant(0.01, shape=[nf]))
    hidden = tf.nn.relu(tf.matmul(hidden, W) + b)
    print('hidden layer %d %s' % (i, str(hidden.get_shape().as_list())))
    nf0 = nf

  #decode
  decode_depth = getDecodeDepth(rng, cfg)
  print('Decode depth: %d' % decode_depth)
  nf = getDecodeNumFilters(0, decode_depth, rng, cfg)
  ds = getDecodeSize(0, decode_depth, enc_shape[1], IMAGE_SIZE, rng, cfg)
  if ds * ds * nf != nf0:
    W = tf.Variable(tf.truncated_normal([nf0, ds * ds * nf],
                                        stddev = 0.01,
                                        seed=fseed))
    b = tf.Variable(tf.constant(0.01, shape=[ds * ds * nf]))
    hidden = tf.matmul(hidden, W) + b
    print("Linear from %d to %d for input size %d" % (nf0, ds * ds * nf, ds))
  decode = tf.reshape(hidden, [BATCH_SIZE, ds, ds, nf])  
  print("Unflattening to", decode.get_shape().as_list())
  for i in range(1, decode_depth + 1):
    nf0 = nf
    ds = getDecodeSize(i, decode_depth, enc_shape[1], IMAGE_SIZE, rng, cfg)
    if i == decode_depth:
       assert ds == IMAGE_SIZE, (ds, IMAGE_SIZE)
    decode = tf.image.resize_images(decode, ds, ds)
    print('Decode resize %d to shape' % i, decode.get_shape().as_list())
    add_bypass = getDecodeBypass(i, encode_nodes, ds, decode_depth, rng, cfg)
    if add_bypass != None:
      bypass_layer = encode_nodes[add_bypass]
      bypass_shape = bypass_layer.get_shape().as_list()
      if bypass_shape[1] != ds:
        bypass_layer = tf.image.resize_images(bypass_layer, ds, ds)
      decode = tf.concat(3, [decode, bypass_layer])
      print('Decode bypass from %d at %d for shape' % (add_bypass, i), decode.get_shape().as_list())
      nf0 = nf0 + bypass_shape[-1]

    print('Decode resize %d to shape' % i, decode.get_shape().as_list())
    cfs = getDecodeFilterSize(i, decode_depth, rng, cfg)
    nf = getDecodeNumFilters(i, decode_depth, rng, cfg)
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
    print('Decode conv %d with size %d stride %d num channels %d numfilters %d for shape' % (i, cfs, \
cs, nf0, nf), decode.get_shape().as_list())

    if i < decode_depth:  #add relu to all but last ... need this?
      decode = tf.nn.relu(decode)

  return decode



def main(seed=0, cfgfile=None):

  rng = np.random.RandomState(seed=seed)

  if cfgfile is not None:
    cfg = json.load(open(cfgfile))
  else:
    cfg = {}

  image_node = tf.placeholder(
      tf.float32,
      shape=(BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  normals_node = tf.placeholder(
        tf.float32,
      shape=(BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))
  
  train_prediction = model(image_node, rng=rng, cfg=cfg)
  norm = (IMAGE_SIZE**2) * NUM_CHANNELS * BATCH_SIZE
  loss = tf.nn.l2_loss(train_prediction - normals_node) / norm

  batch = tf.Variable(0, trainable=False)

  learning_rate = tf.train.exponential_decay(
      1.0,                # Base learning rate.
      batch * BATCH_SIZE,  # Current index into the dataset.
      100000,          # Decay step.
      0.95,                # Decay rate.
      staircase=True)

  optimizer = tf.train.MomentumOptimizer(learning_rate, 0.9).minimize(loss, global_step=batch)

  start_time = time.time()
  with tf.Session() as sess:
    tf.initialize_all_variables().run()
    print('Initialized!')
    for step in xrange(NUM_TRAIN_STEPS // BATCH_SIZE):
      batch_data = getNextBatch(BATCH_SIZE, step * BATCH_SIZE, rng)
      feed_dict = {image_node: batch_data['images'],
                   normals_node: batch_data['normals']}
      # Run the graph and fetch some of the nodes.
      _, l, lr, predictions = sess.run(
          [optimizer, loss, learning_rate, train_prediction],
          feed_dict=feed_dict)
      print(step, l, lr)
      np.save('normal_encoder_simple/prediction.npy', predictions)


def handle_message(sock, write=False, outdir='', imtype='png', prefix=''):
    t0 = time.time()
    msg = sock.recv()
    img0 = sock.recv()
    img1 = sock.recv()
    img2 = sock.recv()
    t1 = time.time()
    if write:
        if not os.path.exists(outdir):
            os.mkdir(outdir)
        with open(os.path.join(outdir, 'image_%s.%s' % (prefix, imtype)), 'w') as _f:
            _f.write(img2)
        with open(os.path.join(outdir, 'objects_%s.%s' % (prefix, imtype)), 'w') as _f:
            _f.write(img1)
        with open(os.path.join(outdir, 'normals_%s.%s' % (prefix, imtype)), 'w') as _f:
            _f.write(img0)
        with open(os.path.join(outdir, 'info_%s.json' % prefix), 'w') as _f:
            _f.write(msg)
    return [msg, img0, img1, img2]


if __name__ == '__main__':
  argv = sys.argv
  seed = int(argv[1])
  if len(argv) > 2:
    cfgfile = arvg[2]
  else:
    cfgfile = None
  main(seed=seed, cfgfile=cfgfile) 
