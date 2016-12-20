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
ENCODE_DIMS = 1024
NUM_CHANNELS = 3
PIXEL_DEPTH = 255
SEED = 0  # Set to None for random seed.
BATCH_SIZE = 64
NUM_TRAIN_STEPS = 1000000

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

rng = np.random.RandomState(0)

def norml(x):
   return (x - PIXEL_DEPTH/2.0) / PIXEL_DEPTH


def getNextBatch(N, start):
  ims = []
  norms = []
  print('getting %d-%d' % (start, start + N))
  for i in range(N):
    timestep = start + i
    info, nstr, ostr, imstr = handle_message(sock)
    objarray = np.asarray(Image.open(StringIO(ostr)).convert('RGB'))
    normalsarray = np.asarray(Image.open(StringIO(nstr)).convert('RGB'))
    imarray = np.asarray(Image.open(StringIO(imstr)).convert('RGB'))
    msg = {'n': 4,
           'msg': {"msg_type": "CLIENT_INPUT",
                   "get_obj_data": False,
                   "actions": []}}
        
    objs = 256**2 * objarray[:, :, 0] + 256 * objarray[:, :, 1] + objarray[:, :, 2]
    objs = np.unique(objs)
    objs = objs[objs > 2] 
    global OBJSET
    if OBJSET is None or timestep % 20 == 0:
        OBJSET = objs[rng.permutation(len(objs))[:3]]
        print("OBJSET:", OBJSET)

    #poking for 5 steps every 20 steps
    if timestep % 20 < 5:
      for o in OBJSET:
        msg['msg']['actions'].append({'id': str(o),
                                      'force': [rng.choice([-10, 0, 10]),
                                                20,
                                                rng.choice([-10, 0, 10])],
                                      'torque': [0, 0, 0]})
    #every few seconds, shift around a little
    if timestep % 5 == 0:
      msg['msg']['vel'] = [.3 * rng.uniform(), 0.15 * rng.uniform(), 0.3 * rng.uniform()]
           
    #every so often moves to a new area
    if timestep % 10 == 0 or len(objs) == 0:
      print('teleporting at %d ... ' % timestep)
      msg['msg']['teleport_random'] = True

    ims.append(imarray)
    norms.append(normalsarray)
    sock.send_json(msg)

  batch = {'images': norml(np.array(ims)),
           'normals': norml(np.array(norms))}
  return batch


def error_rate(predictions, imgs):
  """Return the error rate based on dense predictions and sparse labels."""
  return 0.5 * ((predictions - imgs)**2).mean()


def main(argv=None):  # pylint: disable=unused-argument
    # Get the data.

  train_data_node = tf.placeholder(
      tf.float32,
      shape=(BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  normals_node = tf.placeholder(
        tf.float32,
      shape=(BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  conv1_weights = tf.Variable(
      tf.truncated_normal([7, 7, NUM_CHANNELS, 48],  
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1_biases = tf.Variable(tf.zeros([48]), name='conv1b')

  conv1a_weights = tf.Variable(
      tf.truncated_normal([5, 5, 48, 64],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1a_biases = tf.Variable(tf.zeros([64]), name='conv1b')

  conv1b_weights = tf.Variable(
      tf.truncated_normal([3, 3, 64, 128],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1b_biases = tf.Variable(tf.zeros([128]), name='conv1b')

  conv2_weights = tf.Variable(
      tf.truncated_normal([7, 7, 32, NUM_CHANNELS],  # 5x5 filter, depth 32.
                          stddev=0.1,
                          seed=SEED),
      name = 'conv2w' )
  conv2_biases = tf.Variable(tf.zeros([NUM_CHANNELS]), name='conv2b')

  fc1_weights = tf.Variable(  # fully connected, depth 512.
      tf.truncated_normal(
          [IMAGE_SIZE // 16 * IMAGE_SIZE // 16 * 128, ENCODE_DIMS],
          stddev=0.01,
          seed=SEED), name='fc1w')
  fc1_biases = tf.Variable(tf.constant(0.01, shape=[ENCODE_DIMS]), name='fc1b')
  
  fc2_weights = tf.Variable(  # fully connected, depth 512.
      tf.truncated_normal(
          [ENCODE_DIMS, 32 * IMAGE_SIZE//2 * IMAGE_SIZE//2],
          stddev=0.01,
          seed=SEED), name='fc1w')
  fc2_biases = tf.Variable(tf.constant(0.01, shape=[32 * IMAGE_SIZE//2 * IMAGE_SIZE//2]), name='fc1b')

  def model(data, train=False):
    """The Model definition."""
    # 2D convolution, with 'SAME' padding (i.e. the output feature map has
    # the same size as the input). Note that {strides} is a 4D array whose

    conv = tf.nn.conv2d(data,
                        conv1_weights,
                        strides=[1, 2, 2, 1],
                        padding='SAME')
    conv = tf.nn.relu(tf.nn.bias_add(conv, conv1_biases))

    pool = tf.nn.max_pool(conv,
                          ksize=[1, 2, 2, 1],
                          strides=[1, 2, 2, 1],
                          padding='SAME')

    conv = tf.nn.conv2d(pool,
                        conv1a_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv = tf.nn.relu(tf.nn.bias_add(conv, conv1a_biases))

    pool = tf.nn.max_pool(conv,
                          ksize=[1, 2, 2, 1],
                          strides=[1, 2, 2, 1],
                          padding='SAME')

    conv = tf.nn.conv2d(pool,
                        conv1b_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv = tf.nn.relu(tf.nn.bias_add(conv, conv1b_biases))

    pool = tf.nn.max_pool(conv,
                          ksize=[1, 2, 2, 1],
                          strides=[1, 2, 2, 1],
                          padding='SAME')


    pool_shape = pool.get_shape().as_list()
    flatten = tf.reshape(pool, [pool_shape[0], pool_shape[1] * pool_shape[2] * pool_shape[3]])

    encode = tf.matmul(flatten, fc1_weights) + fc1_biases

    hidden = tf.matmul(encode, fc2_weights) + fc2_biases

    hidden_shape = hidden.get_shape().as_list()
    unflatten = tf.reshape(hidden, [hidden_shape[0], IMAGE_SIZE//2, IMAGE_SIZE//2, 32])

    unpool = tf.image.resize_images(unflatten, IMAGE_SIZE, IMAGE_SIZE)
    
    conv = tf.nn.conv2d(unpool,
                        conv2_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv = tf.nn.bias_add(conv, conv2_biases)


    return conv

  train_prediction = model(train_data_node, True)  
  norm = (IMAGE_SIZE**2) * NUM_CHANNELS * BATCH_SIZE
  loss = tf.nn.l2_loss(train_prediction - normals_node) / norm

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
    for step in xrange(NUM_TRAIN_STEPS // BATCH_SIZE):
      batch_data = getNextBatch(BATCH_SIZE, step * BATCH_SIZE)
      feed_dict = {train_data_node: batch_data['images'],
                   normals_node: batch_data['normals']}
      # Run the graph and fetch some of the nodes.
      _, l, lr, predictions = sess.run(
          [optimizer, loss, learning_rate, train_prediction],
          feed_dict=feed_dict)
      print(step, l, lr)


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
  tf.app.run()
