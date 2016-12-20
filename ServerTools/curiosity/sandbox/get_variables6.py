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
ENCODE_DIMS = 256
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


def main(argv=None):  # pylint: disable=unused-argument
    # Get the data.

  # Extract it into np arrays.

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

  # Create a local session to run the training.
  start_time = time.time()
  with tf.Session() as sess:
    # Run all the initializers to prepare the trainable parameters.
    tf.initialize_all_variables().run()
    saver = tf.train.Saver()
    saver.restore(sess, 'shallow6savedir/20800.ckpt')
    r = conv1_weights.value().eval()
    np.save('shallow6savedir/conv1_weights.npy', r)
    r = conv1a_weights.value().eval()
    np.save('shallow6savedir/conv1a_weights.npy', r)
    r = conv1b_weights.value().eval()
    np.save('shallow6savedir/conv1b_weights.npy', r)
    r = conv2_weights.value().eval()
    np.save('shallow6savedir/conv2w_weights.npy', r)


if __name__ == '__main__':
  tf.app.run()
