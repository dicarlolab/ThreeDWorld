# Copyright 2015 Google Inc. All Rights Reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
# ==============================================================================

"""Simple, end-to-end, LeNet-5-like convolutional MNIST model example.

This should achieve a test error of 0.7%. Please keep this model as simple and
linear as possible, it is meant as a tutorial for simple convolutional models.
Run with --self_test on the command line to execute a short self-test.
"""
from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

import gzip
import os
import sys
import time

import numpy as np
from six.moves import urllib
from six.moves import xrange  # pylint: disable=redefined-builtin
import tensorflow as tf

from curiosity.utils import hdf5provider

IMAGE_SIZE = 128
ENCODE_DIMS = 1024
NUM_CHANNELS = 3
PIXEL_DEPTH = 255
SEED = 0  # Set to None for random seed.
BATCH_SIZE = 64
NUM_EPOCHS = 5
EVAL_BATCH_SIZE = 64
EVAL_FREQUENCY = 100  # Number of steps between evaluations.
NUM_VALIDATION_BATCHES = 5
NUM_TEST_BATCHES = 5

tf.app.flags.DEFINE_boolean("self_test", False, "True if running a self test.")
FLAGS = tf.app.flags.FLAGS


def extract_data(filename, num_images):
  """Extract the images into a 4D tensor [image index, y, x, channels].

  Values are rescaled from [0, 255] down to [-0.5, 0.5].
  """
  print('Extracting', filename)
  with gzip.open(filename) as bytestream:
    bytestream.read(16)
    buf = bytestream.read(IMAGE_SIZE * IMAGE_SIZE * num_images)
    data = np.frombuffer(buf, dtype=np.uint8).astype(np.float32)
    data = (data - (PIXEL_DEPTH / 2.0)) / PIXEL_DEPTH
    data = data.reshape(num_images, IMAGE_SIZE, IMAGE_SIZE, 1)
    return data


def error_rate(predictions, imgs):
  """Return the error rate based on dense predictions and sparse labels."""
  return 0.5 * ((predictions - imgs)**2).mean()


def main(argv=None):  # pylint: disable=unused-argument
    # Get the data.

  # Extract it into np arrays.
  hdf5source = '/data/imagenet_dataset/hdf5_cached_from_om7/data.raw'
  sourcelist = ['data']
  norml = lambda x: (x - (PIXEL_DEPTH/2.0)) / PIXEL_DEPTH
  postprocess = {'data': lambda x, _: norml(x).reshape((x.shape[0], 3, 256, 256)).swapaxes(1, 2).swapaxes(2, 3)[:, ::2][:, :, ::2]}
  train_slice = np.zeros(1290129).astype(np.bool); train_slice[:1000000] = True
  _N = NUM_VALIDATION_BATCHES * BATCH_SIZE
  validation_slice = np.zeros(1290129).astype(np.bool); validation_slice[1000000: 1000000 + _N] = True
  _M = NUM_TEST_BATCHES * BATCH_SIZE
  test_slice = np.zeros(1290129).astype(np.bool); test_slice[1000000 + _N: 1000000 + _N + _M] = True
  train_data = hdf5provider.HDF5DataProvider(hdf5source, sourcelist, BATCH_SIZE,
                                             postprocess=postprocess, 
                                             subslice = train_slice,
                                             pad=True)
  validation_data = hdf5provider.HDF5DataProvider(hdf5source, sourcelist, BATCH_SIZE,
                                     postprocess=postprocess, subslice = validation_slice)
  validation_data = np.row_stack([validation_data.getBatch(i)['data'] for i in range(NUM_VALIDATION_BATCHES)])
  test_data = hdf5provider.HDF5DataProvider(hdf5source, sourcelist, BATCH_SIZE,
                                     postprocess=postprocess, subslice = test_slice)
  test_data = np.row_stack([test_data.getBatch(i)['data'] for i in range(NUM_TEST_BATCHES)]) 


  num_epochs = NUM_EPOCHS
  train_size = train_data.sizes['data'][0]

  train_data_node = tf.placeholder(
      tf.float32,
      shape=(BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))
  eval_data = tf.placeholder(
      tf.float32,
      shape=(EVAL_BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  conv1_weights = tf.Variable(
      tf.truncated_normal([7, 7, NUM_CHANNELS, 32],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1_biases = tf.Variable(tf.zeros([32]), name='conv1b')

  conv1a_weights = tf.Variable(
      tf.truncated_normal([5, 5, 32, 32],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1a_biases = tf.Variable(tf.zeros([32]), name='conv1b')

  conv1b_weights = tf.Variable(
      tf.truncated_normal([3, 3, 32, 32],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1b_biases = tf.Variable(tf.zeros([32]), name='conv1b')


  conv2_weights = tf.Variable(
      tf.truncated_normal([7, 7, 32, NUM_CHANNELS],  # 5x5 filter, depth 32.
                          stddev=0.1,
                          seed=SEED),
      name = 'conv2w' )
  conv2_biases = tf.Variable(tf.zeros([NUM_CHANNELS]), name='conv2b')

  fc1_weights = tf.Variable(  # fully connected, depth 512.
      tf.truncated_normal(
          [IMAGE_SIZE // 16 * IMAGE_SIZE // 16 * 32, ENCODE_DIMS],
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
  loss = tf.nn.l2_loss(train_prediction - train_data_node) / (IMAGE_SIZE*IMAGE_SIZE*NUM_CHANNELS*BATCH_SIZE)
  #loss = tf.mul(loss, 1./100000000000)

  #regularizers = tf.nn.l2_loss(fc1_weights) + tf.nn.l2_loss(fc1_biases)
  #loss += 5e-4 * regularizers

  batch = tf.Variable(0, trainable=False)

  learning_rate = tf.train.exponential_decay(
      1.,                # Base learning rate.
      batch * BATCH_SIZE,  # Current index into the dataset.
      train_size,          # Decay step.
      0.95,                # Decay rate.
      staircase=True)

  optimizer = tf.train.MomentumOptimizer(learning_rate, 0.9).minimize(loss, global_step=batch)
  #optimizer = tf.train.AdamOptimizer(learning_rate).minimize(loss, global_step=batch)

  eval_prediction = model(eval_data)


  def eval_in_batches(data, sess):
    """Get all predictions for a dataset by running it in small batches."""
    size = data.shape[0]
    if size < EVAL_BATCH_SIZE:
      raise ValueError("batch size for evals larger than dataset: %d" % size)
    predictions = np.ndarray(shape=(size, IMAGE_SIZE, IMAGE_SIZE, 3), dtype=np.float32)
    for begin in xrange(0, size, EVAL_BATCH_SIZE):
      end = begin + EVAL_BATCH_SIZE
      if end <= size:
        predictions[begin:end, :] = sess.run(
            eval_prediction,
            feed_dict={eval_data: data[begin:end, ...]})
      else:
        batch_predictions = sess.run(
            eval_prediction,
            feed_dict={eval_data: data[-EVAL_BATCH_SIZE:, ...]})
        predictions[begin:, :] = batch_predictions[begin - size:, :]
    return predictions

  # Create a local session to run the training.
  start_time = time.time()
  with tf.Session() as sess:
    # Run all the initializers to prepare the trainable parameters.
    #tf.initialize_all_variables().run()
    saver = tf.train.Saver()
    saver.restore(sess, 'shallow6savedir/55800.ckpt')
    print('Initialized!')
    # Loop through training steps.
    # Finally print the result!
    preds = eval_in_batches(test_data, sess)
    test_error = error_rate(preds, test_data)
    print('Test error: %.4f' % test_error)
    np.save('shallow6npy/results.npy', preds)


if __name__ == '__main__':
  tf.app.run()
