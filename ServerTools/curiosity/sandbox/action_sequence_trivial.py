from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

from StringIO import StringIO
import math
import itertools
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
ENCODE_DIMS = 512
NUM_CHANNELS = 3
PIXEL_DEPTH = 255
SEED = 0  # Set to None for random seed.
BATCH_SIZE = 64
NUM_EPISODES = 1000000
EPISODE_LENGTH = 50
OBSERVATION_LENGTH = 1
ENCODE_DEPTH = 3
PHYSNET_DEPTH = 1
DECODE_DEPTH = 1
MAX_NUM_ACTIONS = 5
ATOMIC_ACTION_LENGTH = 14

rng = np.random.RandomState(0)

tf.app.flags.DEFINE_boolean("self_test", False, "True if running a self test.")
FLAGS = tf.app.flags.FLAGS

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)

print("connecting...")
sock.connect("tcp://18.93.15.188:23042")
print("...connected")
sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))
print("...joined")


def getEpisode(epnum):
  ims = []
  norms = []
  actions = []
  timestep = 0
  first = True 
  chosen = False
  while timestep < EPISODE_LENGTH + 1:
    pf = str(epnum) + '_' + str(timestep)
    info, nstr, ostr, imstr = handle_message(sock) #write=True, outdir='sequences', prefix=pf)
    objarray = np.asarray(Image.open(StringIO(ostr)).convert('RGB'))
    normalsarray = np.asarray(Image.open(StringIO(nstr)).convert('RGB'))
    imarray = np.asarray(Image.open(StringIO(imstr)).convert('RGB'))
    msg = {'n': 4,
           'msg': {"msg_type": "CLIENT_INPUT",
                   "actions": [],
                   "get_obj_data": False}}

    objarray = 256**2 * objarray[:, :, 0] + 256 * objarray[:, :, 1] + objarray[:, :, 2]
    objs = np.unique(objarray) 
    objs = objs[objs > 2] 
    if first or len(objs) == 0:
      print('teleporting at %d ... ' % timestep)
      msg['msg']['teleport_random'] = True
    else:
      if not chosen:
        x, y = choose_action_position(objarray)
        o = objarray[x, y]
        force = [rng.choice([-20, 0, 20]), 40, rng.choice([-20, 0, 20])]
        chosen = True
      if timestep < MAX_NUM_ACTIONS:
        msg['msg']['actions'] = [{'id': str(o),
                                  'action_pos': [x, y],
                                  'force': force,
                                  'torque': [0, 0, 0]}]
      #every few frames, shift around a little 
      if timestep % 10 == 0:
        msg['msg']['teleport_random'] = True
        #msg['msg']['vel'] = [.1 * rng.uniform(), 0.05 * rng.uniform(), 0.1 * rng.uniform()]
      
      timestep += 1
           
      ims.append(imarray)
      norms.append(normalsarray)
      actions.append(copy.deepcopy(msg['msg']))
    first = False
    sock.send_json(msg)

  def norml(x):
    return (x - PIXEL_DEPTH/2.0) / float(PIXEL_DEPTH)

  ims = norml(np.array(ims))
  norms = norml(np.array(norms))
   
  #print(np.mean(np.abs(norms[10] - norms[45])), np.abs(norms[10]).mean())
  batch = {'images': ims,
           'normals': norms,
           'actions': actions}
  return batch


def process_action(action):
   act = action.get('actions', [])
   if len(act) == 0:
     act = {'action_pos': [0, 0], 'force': [0, 0, 0], 'torque': [0, 0, 0]}
   else:
     act = act[0]
     
   actvec = action.get('vel', [0, 0, 0]) + action.get('ang_vel', [0, 0, 0]) + act['action_pos'] + act['force'] + act['torque']

   return actvec 


def getNextBatch(N, epnum):
  batch = getEpisode(epnum)
  normals = batch['normals']
  actions = batch['actions']
  k = len(batch['normals'])
  obss = []
  future_inds = []
  time_diffs = []
  actionss = []
  back_length = max(OBSERVATION_LENGTH, MAX_NUM_ACTIONS)
  for i in range(N):
    j0 = rng.randint(k - back_length)
    #forward_length = 30
    #j1 = rng.randint(low = forward_length + j0 + OBSERVATION_LENGTH, high=k)
    j1 = j0 
    newshape = (IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS * OBSERVATION_LENGTH)
    obs = normals[j0: j0 + OBSERVATION_LENGTH].transpose((1, 2, 3, 0)).reshape(newshape)
    obss.append(obs)
    future_inds.append(j1)
    time_diffs.append(j1 - j0)
    action_seq = list(itertools.chain(*[process_action(actions[_j]) for _j in range(j0, j0 + MAX_NUM_ACTIONS)]))
    actionss.append(action_seq)

  batch = {'observations': np.array(obss),
            'future': normals[future_inds],
            'actions': np.array(actionss),
            'time_diff': np.array(time_diffs)}
  #print(np.abs(batch['observations']).mean())
  #print(np.abs(batch['observations'][:, :, :, :3] - batch['future'].mean() * np.ones(batch['future'].shape)).mean(1).mean(1).mean(1))
  #print(np.abs(batch['observations'] - batch['future']).mean(1).mean(1).mean(1))
  return batch


def error_rate(predictions, imgs):
  """Return the error rate based on dense predictions and sparse labels."""
  return 0.5 * ((predictions - imgs)**2).mean()


def getDecodeNumFilters(i, N):
  if i < N:
     return 32
  else:
     return NUM_CHANNELS

def getDecodeFilterSize(i, N):
  return 7

def getDecodeSizes(N, initial_size, final_size):
  s = np.log2(initial_size)
  e = np.log2(final_size)
  increment = (e - s) / N
  l = np.around(np.power(2, np.arange(s, e, increment)))
  if len(l) < N + 1:
    l = np.concatenate([l, [final_size]])
  l = l.astype(np.int) 
  return l
  
def getPhysnetNumFilters(i, N):
  return 1024

def getEncodeNumFilters(i, N):
  L = [48, 64, 128]
  return L[i-1] 

def getEncodeFilterSize(i, N):
  L = [7, 5, 3]
  return L[i-1]

def getEncodeConvStride(i, N):
  return 2 if i == 1 else 1

def getEncodePoolFilterSize(i, N):
  return 2

def getEncodePoolStride(i, N):
  return 2


def main(argv):
  #holder for observation data
  observation_node = tf.placeholder(tf.float32,
                                    shape=(BATCH_SIZE,
                                           IMAGE_SIZE,
                                           IMAGE_SIZE,
                                           NUM_CHANNELS * OBSERVATION_LENGTH))
  #holder for prediction  
  future_node = tf.placeholder(tf.float32,
                               shape=(BATCH_SIZE,
                                      IMAGE_SIZE,
                                      IMAGE_SIZE,
                                      NUM_CHANNELS))
  
  #holder for action space element
  actions_node = tf.placeholder(tf.float32,
                                shape=(BATCH_SIZE, 
                                       ATOMIC_ACTION_LENGTH * MAX_NUM_ACTIONS))
 
  #time forward
  time_node = tf.placeholder(tf.float32,
                             shape=(BATCH_SIZE, 1))


  def model(obs_node, actions_node, time_node):
    """The Model definition."""

    # encoding phase for image-shaped observations == could be empty
    nf0 = NUM_CHANNELS * OBSERVATION_LENGTH
    imsize = IMAGE_SIZE
    for i in range(1, ENCODE_DEPTH + 1):
      cfs = getEncodeFilterSize(i, ENCODE_DEPTH)
      nf = getEncodeNumFilters(i, ENCODE_DEPTH)
      cs = getEncodeConvStride(i, ENCODE_DEPTH)
      pfs = getEncodePoolFilterSize(i, ENCODE_DEPTH)
      ps = getEncodePoolStride(i, ENCODE_DEPTH)
      W = tf.Variable(tf.truncated_normal([cfs, cfs, nf0, nf],
                                           stddev=0.01,
                                           seed=SEED))
      b = tf.Variable(tf.zeros([nf]))
      obs_node = tf.nn.relu(tf.nn.conv2d(obs_node, 
                                         W,
                                         strides=[1, cs, cs, 1],
                                         padding='SAME'))
      obs_node = tf.nn.bias_add(obs_node, b)
      print('Encode conv %d with size %d stride %d num channels %d numfilters %d for shape' % (i, cfs, cs, nf0, nf), obs_node.get_shape().as_list())
      obs_node = tf.nn.max_pool(obs_node, 
                                ksize=[1, pfs, pfs, 1], 
                                strides=[1, ps, ps, 1], 
                                padding='SAME')
      print('Encode pool %d with size %d stride %d for shape' % (i, pfs, ps), obs_node.get_shape().as_list())
      nf0 = nf 
      imsize = imsize // (cs * ps)
      
    #flatten the observations
    obs_shape = obs_node.get_shape().as_list()
    obs_flat = tf.reshape(obs_node, [obs_shape[0], np.prod(obs_shape[1:])])
    print('Flatten to shape %s' % obs_flat.get_shape().as_list())
    
    #concatenate
    concat = tf.concat(1, [obs_flat, actions_node, time_node])
    #apply physics neural network: currently MLP TODO: convnet? recurrent?
    nf0 = imsize * imsize * nf0 + MAX_NUM_ACTIONS * ATOMIC_ACTION_LENGTH + 1
    print('Concatenate actions/time to shape %s' % concat.get_shape().as_list())
    for i in range(1, PHYSNET_DEPTH + 1):
      nf = getPhysnetNumFilters(i, PHYSNET_DEPTH)
      W = tf.Variable(tf.truncated_normal([nf0, nf],
                                          stddev = 0.01,
                                          seed=SEED))
      b = tf.Variable(tf.constant(0.01, shape=[nf]))
      #concat = tf.nn.relu(tf.matmul(concat, W) + b)
      concat = tf.matmul(concat, W) + b
      print('Physnet layer %d %s' % (i, str(concat.get_shape().as_list())))
      nf0 = nf
                    
    #decode
    nf = getDecodeNumFilters(0, DECODE_DEPTH)
    ds0 = IMAGE_SIZE // 2
    dsizes = getDecodeSizes(DECODE_DEPTH, ds0, IMAGE_SIZE)
    ds = dsizes[0]
    if ds * ds * nf != nf0:
      W = tf.Variable(tf.truncated_normal([nf0, ds * ds * nf],
                                          stddev = 0.01,
                                          seed=SEED))
      b = tf.Variable(tf.constant(0.01, shape=[ds * ds * nf]))
      concat = tf.matmul(concat, W) + b
      print("Linear from %d to %d for input size %d" % (nf0, ds * ds * nf, ds))
    decode = tf.reshape(concat, [BATCH_SIZE, ds, ds, nf])
    print("Unflattening to", decode.get_shape().as_list())
    for i in range(1, DECODE_DEPTH + 1):
      nf0 = nf
      ds = dsizes[i]
      if i == DECODE_DEPTH:
         assert ds == IMAGE_SIZE, (ds, IMAGE_SIZE)
      decode = tf.image.resize_images(decode, ds, ds)
      print('Decode resize %d to shape' % i, decode.get_shape().as_list())
      cfs = getDecodeFilterSize(i, DECODE_DEPTH)
      nf = getDecodeNumFilters(i, DECODE_DEPTH)
      if i == DECODE_DEPTH:
         assert nf == NUM_CHANNELS, (nf, NUM_CHANNELS)
      W = tf.Variable(tf.truncated_normal([cfs, cfs, nf0, nf],
                                           stddev=0.01,
                                           seed=SEED))
      b = tf.Variable(tf.zeros([nf]))
      decode = tf.nn.conv2d(decode,
                            W,
                            strides=[1, 1, 1, 1],
                            padding='SAME')
      decode = tf.nn.bias_add(decode, b)
      print('Decode conv %d with size %d stride %d num channels %d numfilters %d for shape' % (i, cfs, cs, nf0, nf), decode.get_shape().as_list())
      #if i < DECODE_DEPTH:  #add relu to all but last ... need this?
      #  decode = tf.nn.relu(decode)
      #else:
      #  decode = tf.minimum(tf.maximum(decode, 0), 1)

    return decode

  train_prediction = model(observation_node, actions_node, time_node)  
  norm = (IMAGE_SIZE**2) * NUM_CHANNELS * BATCH_SIZE
  loss = tf.nn.l2_loss(train_prediction - future_node) / norm

  batch = tf.Variable(0, trainable=False)
  learning_rate = tf.train.exponential_decay(
      1.,                # Base learning rate.
      batch * BATCH_SIZE,  # Current index into the dataset.
      100000,          # Decay step.
      0.95,                # Decay rate.
      staircase=True)

  optimizer = tf.train.MomentumOptimizer(learning_rate, 0.9).minimize(loss, global_step=batch)

  start_time = time.time()
  with tf.Session() as sess:
    tf.initialize_all_variables().run()
    print('Initialized!')
    for episode in xrange(NUM_EPISODES):
      batch_data = getNextBatch(BATCH_SIZE, episode)
      feed_dict = {observation_node: batch_data['observations'],
                   actions_node: batch_data['actions'], 
                   time_node: batch_data['time_diff'][:, np.newaxis], 
                   future_node: batch_data['future']}

      _, l, lr, predictions = sess.run(
          [optimizer, loss, learning_rate, train_prediction],
          feed_dict=feed_dict)
      print(episode, l, lr)


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


def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

if __name__ == '__main__':
  tf.app.run()
