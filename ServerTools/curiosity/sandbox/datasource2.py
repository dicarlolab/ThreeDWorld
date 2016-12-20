import os
from StringIO import StringIO
from PIL import Image
import numpy as np
import h5py
import json
import zmq

def handle_message(sock, write=False, outdir='', imtype='png', prefix=''):
    info = sock.recv()
    nstr = sock.recv()
    narray = np.asarray(Image.open(StringIO(nstr)).convert('RGB'))
    ostr = sock.recv()
    oarray = np.asarray(Image.open(StringIO(ostr)).convert('RGB'))
    imstr = sock.recv()
    imarray = np.asarray(Image.open(StringIO(imstr)).convert('RGB'))
    if write:
        if not os.path.exists(outdir):
            os.mkdir(outdir)
        with open(os.path.join(outdir, 'image_%s.%s' % (prefix, imtype)), 'w') as _f:
            _f.write(imstr)
        with open(os.path.join(outdir, 'objects_%s.%s' % (prefix, imtype)), 'w') as _f:
            _f.write(ostr)
        with open(os.path.join(outdir, 'normals_%s.%s' % (prefix, imtype)), 'w') as _f:
            _f.write(nstr)
        with open(os.path.join(outdir, 'info_%s.json' % prefix), 'w') as _f:
            _f.write(info)
    return [info, narray, oarray, imarray]


def send_array(socket, A, flags=0, copy=True, track=False):
    """send a numpy array with metadata"""
    md = dict(
        dtype = str(A.dtype),
        shape = A.shape,
    )
    socket.send_json(md, flags|zmq.SNDMORE)
    return socket.send(A, flags, copy=copy, track=track)

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)

print("connecting...")
sock.connect("tcp://18.93.15.188:23042")
print("...connected")
sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))
print("...joined")

print('creating sock2 ...')
sock2 = ctx.socket(zmq.REP)
sock2.bind('tcp://18.93.3.135:23042')
print('... bound')

N = 1024000

path = '/data2/datasource2'
file = h5py.File(path, mode='a')
valid = file.require_dataset('valid', shape=(N,), dtype=np.bool)
images = file.require_dataset('images', shape=(N, 256, 256, 3), dtype=np.uint8)
normals = file.require_dataset('normals', shape=(N, 256, 256, 3), dtype=np.uint8)
objects = file.require_dataset('objects', shape=(N, 256, 256, 3), dtype=np.uint8)

rng = np.random.RandomState(0)

def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

while True:
    msg = sock2.recv_json()
    print(msg)
    bn = msg['batch_num']
    bsize = msg['batch_size']
    start = (bn * bsize) % N
    end = ((bn + 1) * bsize) % N

    if not valid[start: end].all():
        print("Getting batch %d new" % bn)
        ims = []
        objs = []
        norms = []
        for i in range(bsize):
            info, narray, oarray, imarray = handle_message(sock, 
                                                           write=True, 
                                                           outdir='/data2/datasource2_ims', prefix=str(i))
            msg = {'n': 4,
                   'msg': {"msg_type": "CLIENT_INPUT",
                           "get_obj_data": False,
                           "actions": []}}
            oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
            if i % 5 == 0:
                print('teleporting at %d ... ' % i)
                msg['msg']['teleport_random'] = True
            elif i % 5 == 1:
                a, b, c = [.3 * rng.uniform(), 0.15 * rng.uniform(), 0.3 * rng.uniform()]
                d, e, f = [0, 2 * rng.binomial(1, .5) - 1, 0]
                obs = np.unique(oarray1)
                obs = obs[obs > 2]
                if len(obs) > 0:
                    o = obs[rng.randint(len(obs))]
                else:
                    o = None
            if i % 5 != 0:
                #msg['msg']['vel'] = [a, b, c]
                #msg['msg']['ang_vel'] = [d, e, f]
                if o is not None:
                    print('obj', o)
                    xs, ys = (oarray1 == o).nonzero()
                    if len(xs) > 0:            
                        pos = zip(xs, ys)
                        x, y = map(int, pos[rng.randint(len(pos))])
                        act = {'action_pos': [x, y],
                               'id': str(o), 
                               'force': [0, 0, 0],
                               'torque': [0, 10 * (2 * rng.uniform() - 1), 0]}
                        msg['msg']['actions'].append(act)
            ims.append(imarray)
            norms.append(narray)
            objs.append(oarray)
            sock.send_json(msg) 
        ims = np.array(ims)
        norms = np.array(norms)
        objs = np.array(objs)
        images[start: end] = ims
        normals[start: end] = norms
        objects[start: end] = objs
        valid[start: end] = True
    file.flush()
    print("Sending batch %d" % bn)
    ims = images[start: end] 
    norms = normals[start: end] 
    objs = objects[start: end]
    send_array(sock2, ims, flags=zmq.SNDMORE)
    send_array(sock2, norms, flags=zmq.SNDMORE)
    send_array(sock2, objs)
    

