import os
from StringIO import StringIO
from PIL import Image
import numpy as np
import h5py
import json
import zmq
import argparse

from curiosity.utils.io import send_array


file = None

def initialize(path):
  """
  Loads the HDF5 file given the path to the file.
  :param path: path to the HDF5 file.
  :return: -
  """
  global file
  if file is None:
    print('initializing...')
    file = h5py.File(path, mode='r')
    print('... on %s' % file.filename)
  else:
    assert path == file.filename, (path, file.filename)
    print('Already initialized to %s' % file.filename)


def rget(f, k):
  if isinstance(k, str) or isinstance(k, unicode):
    return f[k]
  else:
    assert isinstance(k, list), k
    r = dict()
    for _k in k:
      r[_k] = f[_k]
    return r

def main(host, port):
  ctx = zmq.Context()
  sock2 = ctx.socket(zmq.REP)
  sockstr = 'tcp://%s:%d' % (host, port)
  sock2.bind(sockstr)
  print('Bound to %s' % sockstr)

  while True:
    msg = sock2.recv_json()
    print 'Message received! ',
    # print(msg)
    initialize(msg['path'])
    keys = msg['keys']
    # print keys
    if 'size' in msg:  # If client asks for size, return only the size
      send_array(sock2, np.array(rget(file, keys[0]).shape[0]))
      print 'Sending data size...'
      continue

    if 'batch_size' in msg:
      N = rget(file, keys[0]).shape[0]
      bn = msg['batch_num']
      batch_size = msg['batch_size']
      start = (bn * batch_size) % N
      end = ((bn + 1) * batch_size - 1) % N + 1
      sl = slice(start, end)
    else:
      sl = slice(None)
    print("Sending batch %d" % bn)
    for ind, k in enumerate(keys):
      data = rget(file, k)[sl]
      if ind < len(keys) - 1:
        send_array(sock2, data, flags=zmq.SNDMORE)
      else:
        send_array(sock2, data)


if __name__ == '__main__':
  parser = argparse.ArgumentParser()
  parser.add_argument('host', type=str, help="host")
  parser.add_argument('port', type=int, help="port")
  args = vars(parser.parse_args())
  main(**args)
