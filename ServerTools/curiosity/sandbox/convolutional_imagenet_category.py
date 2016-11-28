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

IMAGE_SIZE = 256
NUM_CHANNELS = 3
PIXEL_DEPTH = 255
SEED = 66478  # Set to None for random seed.
BATCH_SIZE = 256
NUM_EPOCHS = 10
NUM_VALIDATION_BATCHES = 12
NUM_TEST_BATCHES = 12
EVAL_BATCH_SIZE = 64
EVAL_FREQUENCY = 100  # Number of steps between evaluations.
NUM_LABELS = 999

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


def error_rate(predictions, labels):
  """Return the error rate based on dense predictions and sparse labels."""
  return 100.0 - (
      100.0 *
      np.sum(np.argmax(predictions, 1) == labels) /
      predictions.shape[0])


def main(argv=None):  # pylint: disable=unused-argument
    # Get the data.

  # Extract it into np arrays.
  hdf5source = '/data/imagenet_dataset/hdf5_cached_from_om7/data.raw'
  sourcelist = ['data', 'labels']
  preprocess = {'labels': hdf5provider.get_unique_labels}
  norml = lambda x: (x - (PIXEL_DEPTH/2.0)) / PIXEL_DEPTH
  postprocess = {'data': lambda x, _: norml(x).reshape((x.shape[0], 3, 256, 256)).swapaxes(1, 2).swapaxes(2, 3)}
  train_slice = np.zeros(1290129).astype(np.bool); train_slice[:1000000] = True
  _N = BATCH_SIZE * NUM_VALIDATION_BATCHES
  validation_slice = np.zeros(1290129).astype(np.bool); validation_slice[1000000: 1000000 + _N] = True
  _M = BATCH_SIZE  * NUM_TEST_BATCHES
  test_slice = np.zeros(1290129).astype(np.bool); test_slice[1000000 + _N: 1000000 + _N + _M] = True
  train_data = hdf5provider.HDF5DataProvider(hdf5source, sourcelist, BATCH_SIZE,
                                             preprocess=preprocess,
                                             postprocess=postprocess, 
                                             subslice = train_slice,
                                             pad=True)
  validation_dp = hdf5provider.HDF5DataProvider(hdf5source, sourcelist, BATCH_SIZE,
                                                preprocess=preprocess,
                                                postprocess=postprocess, 
                                                subslice = validation_slice,
                                                pad=True)
  validation_data = []
  validation_labels = []
  for i in range(NUM_VALIDATION_BATCHES):
    b = validation_dp.getBatch(i)
    validation_data.append(b['data'])
    validation_labels.append(b['labels'])
  validation_data = np.row_stack(validation_data)
  validation_labels = np.concatenate(validation_labels)

  test_dp = hdf5provider.HDF5DataProvider(hdf5source, sourcelist, BATCH_SIZE,
                                            preprocess=preprocess,
                                            postprocess=postprocess, 
                                            subslice = test_slice, pad=True)
  test_data = []
  test_labels = []
  for i in range(NUM_TEST_BATCHES):
    b = test_dp.getBatch(i)
    test_data.append(b['data'])
    test_labels.append(b['labels'])
  test_data = np.row_stack(test_data)
  test_labels = np.concatenate(test_labels)

  num_epochs = NUM_EPOCHS
  train_size = train_data.sizes['data'][0]

  train_data_node = tf.placeholder(
      tf.float32,
      shape=(BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))
  train_labels_node = tf.placeholder(tf.int64, shape=(BATCH_SIZE,))
  eval_data = tf.placeholder(
      tf.float32,
      shape=(EVAL_BATCH_SIZE, IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS))

  conv1_weights = tf.Variable(
      tf.truncated_normal([7, 7, NUM_CHANNELS, 64],  
                          stddev=0.01,
                          seed=SEED),
      name = 'conv1w' )
  conv1_biases = tf.Variable(tf.zeros([64]), name='conv1b')

  conv2_weights = tf.Variable(
      tf.truncated_normal([5, 5, 64, 256],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv2w' )
  conv2_biases = tf.Variable(tf.zeros([256]), name='conv2b')

  conv3_weights = tf.Variable(
      tf.truncated_normal([3, 3, 256, 512],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv2w' )
  conv3_biases = tf.Variable(tf.zeros([512]), name='conv2b')

  conv4_weights = tf.Variable(
      tf.truncated_normal([3, 3, 512, 1024],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv2w' )
  conv4_biases = tf.Variable(tf.zeros([1024]), name='conv2b')

  conv5_weights = tf.Variable(
      tf.truncated_normal([3, 3, 1024, 512],  # 5x5 filter, depth 32.
                          stddev=0.01,
                          seed=SEED),
      name = 'conv2w' )
  conv5_biases = tf.Variable(tf.zeros([512]), name='conv2b')

  fc1_weights = tf.Variable(  # fully connected, depth 512.
      tf.truncated_normal(
          [7 * 7 * 512, 4096],
          stddev=0.01,
          seed=SEED), name='fc1w')
  fc1_biases = tf.Variable(tf.constant(0.01, shape=[4096]), name='fc1b')

  fc2_weights = tf.Variable(  # fully connected, depth 512.
      tf.truncated_normal(
          [4096, 4096],
          stddev=0.01,
          seed=SEED), name='fc2w')
  fc2_biases = tf.Variable(tf.constant(0.01, shape=[4096]), name='fc2b')

  fc_out_weights = tf.Variable(  # fully connected, depth 512.
      tf.truncated_normal(
          [4096, 999],
          stddev=0.01,
          seed=SEED), name='fcoutw')
  fc_out_biases = tf.Variable(tf.constant(0.1, shape=[999]), name='fc1outb')

  def model(data, train=False):
    """The Model definition."""

    conv1 = tf.nn.conv2d(data,
                        conv1_weights,
                        strides=[1, 4, 4, 1],
                        padding='SAME')
    conv1 = tf.nn.relu(tf.nn.bias_add(conv1, conv1_biases))

    pool1 = tf.nn.max_pool(conv1,
                          ksize=[1, 3, 3, 1],
                          strides=[1, 2, 2, 1],
                          padding='VALID')

    conv2 = tf.nn.conv2d(pool1,
                        conv2_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv2 = tf.nn.relu(tf.nn.bias_add(conv2, conv2_biases))

    pool2 = tf.nn.max_pool(conv2,
                          ksize=[1, 3, 3, 1],
                          strides=[1, 2, 2, 1],
                          padding='VALID')

    conv3 = tf.nn.conv2d(pool2,
                        conv3_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv3 = tf.nn.relu(tf.nn.bias_add(conv3, conv3_biases))

    conv4 = tf.nn.conv2d(conv3,
                        conv4_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv4 = tf.nn.relu(tf.nn.bias_add(conv4, conv4_biases))

    conv5 = tf.nn.conv2d(conv4,
                        conv5_weights,
                        strides=[1, 1, 1, 1],
                        padding='SAME')
    conv5 = tf.nn.relu(tf.nn.bias_add(conv5, conv5_biases))

    pool5 = tf.nn.max_pool(conv5,
                          ksize=[1, 3, 3, 1],
                          strides=[1, 2, 2, 1],
                          padding='VALID')


    pool5_shape = pool5.get_shape().as_list()
    reshape = tf.reshape(pool5, [pool5_shape[0], np.prod(pool5_shape[1:])])
    
    fc1 = tf.nn.relu(tf.matmul(reshape, fc1_weights) + fc1_biases)
    if train:
      fc1 = tf.nn.dropout(fc1, 0.5, seed=SEED)
    fc2 = tf.nn.relu(tf.matmul(fc1, fc2_weights) + fc2_biases)
    if train:
      fc2 = tf.nn.dropout(fc2, 0.5, seed=SEED)
    fc_out = tf.matmul(fc2, fc_out_weights) + fc_out_biases
    return fc_out

  logits = model(train_data_node, True)  
  loss = tf.reduce_mean(tf.nn.sparse_softmax_cross_entropy_with_logits(logits, 
                                                  train_labels_node))
  #loss = tf.mul(loss, 1./100)

  batch = tf.Variable(0, trainable=False)
  learning_rate = tf.train.exponential_decay(
      .05,                # Base learning rate.
      batch * BATCH_SIZE,  # Current index into the dataset.
      train_size,          # Decay step.
      0.95,                # Decay rate.
      staircase=True)

  #optimizer = tf.train.AdamOptimizer(learning_rate).minimize(loss)
  optimizer = tf.train.MomentumOptimizer(learning_rate, 0.9).minimize(loss, global_step=batch) 

  train_prediction = tf.nn.softmax(logits) 
  eval_prediction = tf.nn.softmax(model(eval_data))

  def eval_in_batches(data, sess):
    """Get all predictions for a dataset by running it in small batches."""
    size = data.shape[0]
    if size < EVAL_BATCH_SIZE:
      raise ValueError("batch size for evals larger than dataset: %d" % size)
    predictions = np.ndarray(shape=(size, NUM_LABELS), dtype=np.float32)
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
    tf.initialize_all_variables().run()
    print('Initialized!')
    # Loop through training steps.
    for step in xrange(int(num_epochs * train_size) // BATCH_SIZE):
      batchd = train_data.getNextBatch()
      batch_data = batchd['data']
      batch_labels = batchd['labels']
      feed_dict = {train_data_node: batch_data,
                   train_labels_node: batch_labels }
      # Run the graph and fetch some of the nodes.
      _, l, lr, predictions = sess.run(
          [optimizer, loss, learning_rate, train_prediction],
          feed_dict=feed_dict)
      print(step, l)
      if step % EVAL_FREQUENCY == 0:
        elapsed_time = time.time() - start_time
        start_time = time.time()
        print('Step %d (epoch %.2f), %.1f ms' %
              (step, float(step) * BATCH_SIZE / train_size,
               1000 * elapsed_time / EVAL_FREQUENCY))
        print('Minibatch loss: %.6f, learning rate: %.6f' % (l, lr))
        print('Minibatch error: %.6f' % error_rate(predictions, batch_labels))
        print('Validation error: %.6f' % error_rate(
               eval_in_batches(validation_data, sess), validation_labels))
        sys.stdout.flush()
    # Finally print the result!
    test_error = error_rate(eval_in_batches(test_data, sess), test_labels)
    print('Test error: %.4f' % test_error)
    if FLAGS.self_test:
      print('test_error', test_error)
      assert test_error == 0.0, 'expected 0.0 test_error, got %.2f' % (
          test_error,)


if __name__ == '__main__':
  tf.app.run()
