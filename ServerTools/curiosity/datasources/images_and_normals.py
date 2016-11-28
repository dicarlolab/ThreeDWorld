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


def getNextBatch(batch_num, batch_size, host, port, datapath):
  global sock
  if sock is None:
    initialize(host, port)

  sock.send_json({'batch_num': batch_num,
                  'batch_size': batch_size,
                  'path': datapath,
                  'keys': [('randomperm', 'images'), ('randomperm', 'normals')]})
  images = norml(recv_array(sock))
  normals = norml(recv_array(sock))

  batch = {'images': images,        #images
           'normals': normals
          }

  return batch
