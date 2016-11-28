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

print "starting"

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)

print "connecting..."
sock.connect("tcp://18.93.15.188:23042")
print "...connected"

sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))
print "...joined"

n_starts = 5
n_actions = 1
action_len = 200

def main(outdir, ext, seed=0):
    rng = np.random.RandomState(seed)
    step = 0
    basemsg = {'n': 4,
           'msg': {"msg_type": "CLIENT_INPUT", 
                   "get_obj_data": False,
                   "actions": []}
          }
    scene_info = None
    for ns in range(n_starts):   #for each "restart"
        for acnum in range(n_actions):  #for each action
            for acstep in range(action_len):   #for each step 
                #handle message from last step
                info, nstr, ostr, imstr = handle_message(sock, write=True, outdir=outdir, imtype=ext, prefix=str(step))
                #prepare message for next step
                msg = copy.deepcopy(basemsg)
                #if it's the very beginning, send scene info
                if step == 0:
                    msg['msg']['sendSceneInfo'] = True
                #teleport if it's the beginning of the action sequence within the restart
                if acnum == 0 and acstep == 0:
                    msg['msg']['teleport_random'] = True
                else:
                    #else, if its the first step in the action, decide whether to move the avatar or an object
                    if (acstep - 1) % 25 == 0 :
                        r = rng.uniform()
                        action = 'avatar' if r < 0.0 else 'object'
                        if action == 'object':
                            objs = np.asarray(Image.open(StringIO(ostr)))
                            objids = 256**2 * objs[:, :, 0] + 256 * objs[:, :, 1] + objs[:, :, 2]
                            objids = np.unique(objids)
                            objids = objids[objids > 2]
                            objset = objids[rng.permutation(len(objids))[:20]]
                            #oid = int(objids[rng.randint(len(objids))])  #int() to make the numpy.int JSON serializiable
                            a = rng.choice([0])   #right-left 
                            b = rng.choice([0])   #up-down
                            c = rng.choice([0]) #out-in 
                    #create the correct message
                    if action == 'object':
                        print('moving object %s' % str(objset))
                        for o in objset:
                            msg['msg']['actions'].append({"id": str(o),
                                                          "force": [a, b, c],
                                                          "torque": [0, 1, 1]})
                    if acstep < 10:
                    #print('moving avatar')
                        msg['msg']['vel'] = [0, .1, 0]
                        #msg['msg']['ang_vel'] = [0.25, -0.05, 0]
                #increment step counter and send the message
                step += 1
                sock.send_json(msg)


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
    outdir = args[1]
    if len(args) > 2:
        ext = args[2]
    else:
        ext = 'png'
    if len(args) > 3:
        seed = int(args[3])
    else:
        seed = 0
    main(outdir, ext=ext, seed=seed)
