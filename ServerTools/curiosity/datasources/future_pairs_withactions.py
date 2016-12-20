"""
asymmetric model with bypass
"""
from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

import numpy as np
import os
import zmq

from curiosity.utils.image import norml 

IMAGE_SIZE = 256
NUM_CHANNELS = 3
OBSERVATION_LENGTH = 2
ATOMIC_ACTION_LENGTH = 14
MAX_NUM_ACTIONS = 10

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)

print("connecting...")
sock.connect("tcp://18.93.3.135:23043")
print("...connected")

def process_action(action):
   act = action.get('actions', [])
   if len(act) == 0:
     act = {'action_pos': [0, 0], 'force': [0, 0, 0], 'torque': [0, 0, 0]}
   else:
     act = act[0]
   actvec = action.get('vel', [0, 0, 0]) + action.get('ang_vel', [0, 0, 0]) + act.get('action_pos', [0, 0]) + act.get('force', [0, 0, 0]) + act.get('torque', [0, 0, 0])
   return actvec


def getNextBatch(N, rng, batch_size):
  sock.send_json({'batch_num': N, 
                  'batch_size': 128})
  info = sock.recv_json()
  ims = norml(recv_array(sock))
  norms = norml(recv_array(sock))
  objs = recv_array(sock)

  obss = []
  time_diffs = []
  actionss = []
  future_inds = []  
  while (len(obss) < batch_size):
    i = rng.randint(len(ims))
    if i < OBSERVATION_LENGTH - 1:
      continue
    _b = [info[j].get('teleport_random', False) for j in range(i-OBSERVATION_LENGTH+1, i+1)]
    if any(_b):
      continue
    for j in range(i, min(i+1+MAX_NUM_ACTIONS, len(info))):
      if 'teleport_random' in info[j]s and info[j]['teleport_random']:
        break
    if j == i:
      continue

    t = rng.randint(i+1, j+1)

    if (t, t-i) in zip(future_inds, time_diffs):
      continue

    future_inds.append(t)
    time_diffs.append(t - i)
    
    newshape = (IMAGE_SIZE, IMAGE_SIZE, NUM_CHANNELS * OBSERVATION_LENGTH)
    obs = ims[i - OBSERVATION_LENGTH: i].transpose((1, 2, 3, 0)).reshape(newshape)
    obss.append(obs)
    
    action_seq = []
    for _j in range(i, i + MAX_NUM_ACTIONS):
      if _j < len(info):
         action_seq.extend(process_action(info[_j]))
      else:
         action_seq.extend([0] * ATOMIC_ACTION_LENGTH)
    actionss.append(action_seq)

  batch = {'observations': np.array(obss),
           'future_normals': norms[future_inds],
           'actions': np.array(actionss),
           'time_diff': np.array(time_diffs) }

  return batch
