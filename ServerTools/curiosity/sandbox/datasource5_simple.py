import os
from StringIO import StringIO
from PIL import Image
import numpy as np
import h5py
import json
import zmq

achoice = [-5, 0, 5]

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
sock.connect("tcp://18.93.5.202:23043")
print("...connected")
sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))
print("...joined")

print('creating sock2 ...')
sock2 = ctx.socket(zmq.REP)
sock2.bind('tcp://18.93.3.135:23043')
print('... bound')

N = 1024000

path = '/data2/datasource5_simple'
infodir = path + '_info'
if not os.path.exists(infodir):
    os.makedirs(infodir)

file = h5py.File(path, mode='a')
valid = file.require_dataset('valid', shape=(N,), dtype=np.bool)
images = file.require_dataset('images', shape=(N, 256, 256, 3), dtype=np.uint8)
normals = file.require_dataset('normals', shape=(N, 256, 256, 3), dtype=np.uint8)
objects = file.require_dataset('objects', shape=(N, 256, 256, 3), dtype=np.uint8)

rng = np.random.RandomState(0)

def choose(x):
  return x[rng.randint(len(x))]

def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

while True:
    msg = sock2.recv_json()
    print(msg)
    bn = msg['batch_num']
    bsize = msg['batch_size']
    bn1 = bn % (N / bsize)
    start = (bn * bsize) % N
    end = ((bn + 1) * bsize - 1) % N + 1

    if not valid[start: end].all():
        print("Getting batch %d new" % bn)
        ims = []
        objs = []
        norms = []
        infolist = []
        for i in range(bsize):
            info, narray, oarray, imarray = handle_message(sock, 
                                                           write=True, 
                                                           outdir='/data2/datasource5_simple_ims', prefix=str(i))
            msg = {'n': 4,
                   'msg': {"msg_type": "CLIENT_INPUT",
                           "get_obj_data": False,
                           "actions": []}}
            if i % 10 == 0:
                print('teleporting at %d ... ' % i)
                msg['msg']['teleport_random'] = True                
                chosen = False
                action_started = False
                action_done = False
                action_ind = 0
                objpi = []
            else:
                oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
                obs = np.unique(oarray1)
                obs = obs[obs > 2]
                if len(obs) == 0:
                    print('teleporting at %d ... ' % i)
                    msg['msg']['teleport_random'] = True
                    action_done = False
                    action_started = False
                    action_ind = 0
                    objpi = []
                    aset = achoice[:]
                    chosen = False
                else:
                    fracs = []
                    for o in obs:
                        frac = (oarray1 == o).sum() / float(np.prod(oarray.shape))
                        fracs.append(frac)
                    if not chosen or chosen_o not in obs:
                        action_started = False
                        action_done = False
                        action_ind = 0
                        objpi = []
                        aset = achoice[:]
                        chosen_o = choose(obs[np.argsort(fracs)[-10:]])
                        chosen = True
                        print('Choosing object', chosen_o)
                    frac0 = fracs[obs.tolist().index(chosen_o)]
                    print('FRAC:', frac0)
                    xs, ys = (oarray1 == chosen_o).nonzero()
                    pos = np.round(np.array(zip(xs, ys)).mean(0))
                    if np.abs(128 - pos[1]) < 3:
                        d = 0
                    else:
                        d =  -0.1 * np.sign(128 - pos[1])
                    msg['msg']['vel'] = [0, 0, .25]
                    msg['msg']['ang_vel'] = [0, d, 0]
                    print(pos, d)
            print(i)
            infolist.append(msg['msg'])
            ims.append(imarray)
            norms.append(narray)
            objs.append(oarray)
            sock.send_json(msg) 
        ims = np.array(ims)
        norms = np.array(norms)
        objs = np.array(objs)
        
        infopath = os.path.join(infodir, str(bn) + '.json')
        with open(infopath, 'w') as _f:
            json.dump(infolist, _f)
            
        images[start: end] = ims
        normals[start: end] = norms
        objects[start: end] = objs
        valid[start: end] = True
    file.flush()
    print("Sending batch %d (%d)" % (bn, bn1))
    ims = images[start: end] 
    norms = normals[start: end] 
    objs = objects[start: end]
    infopath = os.path.join(infodir, str(bn1) + '.json')
    infolist = json.loads(open(infopath).read())
    sock2.send_json(infolist, flags=zmq.SNDMORE)
    send_array(sock2, ims, flags=zmq.SNDMORE)
    send_array(sock2, norms, flags=zmq.SNDMORE)
    send_array(sock2, objs)
    

