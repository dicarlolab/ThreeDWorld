"""
assuming input is (possibly pre-permuted) hdf5 with images and object id counts
this code converts counts into distributions
"""
import numpy as np
import os
import zmq

from curiosity.utils.image import norml
from curiosity.utils.io import recv_array

ctx = zmq.Context()
sock = None

def initialize(host, port):
  global ctx, sock
  sock = ctx.socket(zmq.REQ)
  print("connecting...")
  sock.connect("tcp://%s:%d" % (host, port))
  print("...connected")


def getNextBatch(batch_num, batch_size, host, port, datapath):
  global sock
  if sock is None:
    initialize(host, port)

  sock.send_json({'batch_num': batch_num,
                  'batch_size': batch_size,
                  'path': datapath,
                  'keys': [('randomperm', 'images'), ('randomperm', 'objectcounts')]})
  images = norml(recv_array(sock))
  counts = recv_array(sock)

  objidvec = np.zeros((batch_size, counts.shape[1] + 1)).astype(np.float)
  objidvec[:, 1:] = counts
  noobj = objidvec.sum(1) == 0
  objidvec[noobj, 0] = 1
  objidvec = objidvec / objidvec.sum(1)[:, np.newaxis]

  batch = {'images': images,        #images
           'object_count_distributions': objidvec     #object-id-present vector
          }

  return batch
