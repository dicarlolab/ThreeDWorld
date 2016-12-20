
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

IMAGE_SIZE = 256
ENCODE_DIMS = 1024
NUM_CHANNELS = 3
PIXEL_DEPTH = 255
SEED = 0  # Set to None for random seed.
BATCH_SIZE = 64
NUM_TRAIN_STEPS = 1000000

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


def getNextBatch(N, start, outdir=''):
  ims = []
  norms = []
  print('getting %d-%d' % (start, start + N))
  for i in range(N):
    timestep = start + i
    info, nstr, ostr, imstr = handle_message(sock, write=True, outdir=outdir, prefix=str(timestep))
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
                                      'force': [rng.choice([-25, 0, 25]),
                                                50,
                                                rng.choice([-25, 0, 25])],
                                      'torque': [0, 0, 0]})
    #every few seconds, shift around a little
    if timestep % 5 == 0:
      msg['msg']['vel'] = [.3 * rng.uniform(), 0.15 * rng.uniform(), 0.3 * rng.uniform()]
           
    #every so often moves to a new area
    if timestep % 200 == 0 or len(objs) == 0:
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


def main(outdir): 
   for step in range(10):
       getNextBatch(BATCH_SIZE, step * BATCH_SIZE, outdir=outdir)


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
    args = sys.argv
    main(args[1]) 
