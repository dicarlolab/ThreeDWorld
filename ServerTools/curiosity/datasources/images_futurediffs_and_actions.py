"""
assuming input is pre-permuated hdf5 with images, normals, objects, and object id counts
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


def normalize(x):
  x = x - x.min()
  x = x / x.max()
  x = (255 * x).astype(np.uint8)
  return x


def getNextBatch(batch_num, batch_size, host, port, datapath, keyname):
  global sock
  if sock is None:
    initialize(host, port)

  sock.send_json({'batch_num': batch_num,
                  'batch_size': batch_size,
                  'path': datapath,
                  'keys': [(keyname, 'images0'), 
                           (keyname, 'images1'),
                           (keyname, 'actions'),
                           (keyname, 'timediff')]})
  images = recv_array(sock)
  futures = recv_array(sock)
  futurediffs = normalize(images.astype('float') - futures.astype('float'))

  images = norml(images)   
  futurediffs = norml(futurediffs)
  actions = recv_array(sock)
  timediff = recv_array(sock)

  batch = {'current': images,
           'future': futurediffs,
           'actions': actions,
           'timediff': timediff[:, np.newaxis]
          }

  return batch
