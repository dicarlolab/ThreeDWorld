import os
from StringIO import StringIO
from PIL import Image
import numpy as np
import h5py
import json
import zmq

BATCH_SIZE = 256
MULTSTART = -1

achoice = [-5, 0, 5]
ACTION_LENGTH = 15
ACTION_WAIT = 15

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
sock.connect("tcp://18.93.5.202:23044")
print("...connected")
sock.send(json.dumps({'n': 4, 'msg': {"msg_type": "CLIENT_JOIN"}}))
print("...joined")

print('creating sock2 ...')
sock2 = ctx.socket(zmq.REP)
sock2.bind('tcp://18.93.3.135:23044')
print('... bound')

N = 1024000

path = '/data2/datasource6'
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


def make_new_batch(bn):
    action_length = ACTION_LENGTH #(bsize - i) / 3
    action_wait = ACTION_WAIT

    bsize = BATCH_SIZE
    start = BATCH_SIZE * bn
    end = BATCH_SIZE * (bn + 1)
    if not valid[start: end].all():
        print("Getting new %d-%d" % (start, end))
        ims = []
        objs = []
        norms = []
        infolist = []
        for i in range(bsize):
            print(i)
            info, narray, oarray, imarray = handle_message(sock, 
                                                           write=True, 
                                                           outdir=path + '_ims', prefix=str(i))
            msg = {'n': 4,
                   'msg': {"msg_type": "CLIENT_INPUT",
                           "get_obj_data": False,
                           "actions": []}}
            if i == 0:
                #print('turning at %d ... ' % i)
                #msg['msg']['ang_vel'] = [0, 10 * (rng.uniform() - 1), 0]
                #msg['msg']['vel'] = [0, 0, 1]
                msg['msg']['teleport_random'] = True
                chosen = False
                action_started = False
                action_done = False
                action_ind = 0
                objpi = []
                aset = achoice[:]
                amult = MULTSTART
            else:
                oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
                obs = np.unique(oarray1)
                obs = obs[obs > 2]
                if len(obs) == 0:
                    print('turning at %d ... ' % i)
                    msg['msg']['ang_vel'] = [0, 10 * (2 * rng.uniform() - 1), 0]
                    msg['msg']['vel'] = [0, 0, 2 * (2 * rng.uniform() - 1)]
                    action_done = False
                    action_started = False
                    action_ind = 0
                    objpi = []
                    aset = achoice[:]
                    amult = MULTSTART
                    chosen = False
                    g = 7.5 * (2 * rng.uniform() - 1)
                else:
                    fracs = []
                    for o in obs:
                        frac = (oarray1 == o).sum() / float(np.prod(oarray.shape))
                        fracs.append(frac)
                    if not chosen or (chosen_o not in obs and ((not action_started) or action_done)):
                        action_started = False
                        action_done = False
                        action_ind = 0
                        objpi = []
                        aset = achoice[:]
                        amult = MULTSTART
                        chosen_o = choose(obs[np.argsort(fracs)[-10:]])
                        chosen = True
                        print('Choosing object', chosen_o)
                        g = 15. * (2 * rng.uniform() - 1)
                        a = achoice[rng.randint(len(achoice))]
                    if chosen_o not in obs.tolist():
                        frac0 = 0
                    else:
                        frac0 = fracs[obs.tolist().index(chosen_o)]
                    print('FRAC:', frac0, chosen, chosen_o, action_started, action_ind, action_done)
                    if action_ind >= action_length + action_wait:
                        action_done = True
                        action_started = False
                        action_ind = 0
                    if frac0 < .005 and not action_started:
                        xs, ys = (oarray1 == chosen_o).nonzero()
                        pos = np.round(np.array(zip(xs, ys)).mean(0))
                        if np.abs(128 - pos[1]) < 3:
                            d = 0
                        else:
                            d =  -0.1 * np.sign(128 - pos[1])
                        msg['msg']['vel'] = [0, 0, .25]
                        msg['msg']['ang_vel'] = [0, d, 0]
                        print(pos, d)
                    else:
                        xs, ys = (oarray1 == chosen_o).nonzero()
                        pos = np.round(np.array(zip(xs, ys)).mean(0))
                        objpi.append(pos)
                        action = {}
                        if action_started and not action_done:
                            if chosen_o not in obs.tolist() and action_ind < action_length:
                                action_ind = action_length
                            if action_ind < action_length and len(objpi) > 2:
                                dist = np.sqrt(((objpi[-2] - objpi[-1])**2).sum())
                                print('dist', dist)
                                if dist < 1:
                                    action_ind = 0
                                    aset.remove(a)
                                    if len(aset) == 0:
                                        amult += 1
                                        aset = ((2 ** (amult)) * np.array(achoice)).tolist()
                                    a = aset[rng.randint(len(aset))]
                                    if amult > 10:
                                        action_done = True
                                        action_started = False
                                    print('choosing "a"', dist, a)
                                action['force'] = [a, 30 * (2 ** amult), 0]
                                action['torque'] = [0, g, 0]
                                action['id'] = str(chosen_o)
                                action['action_pos'] = map(float, objpi[-1])
                            else:
                                print(action_ind, i, 'waiting')
                            action_ind += 1
                            if action_done or (action_ind >= action_length + action_wait):
                                action_done = True
                                chosen = False                                
                                action_started = False
                        elif not action_started:
                            objpi = []
                            action_ind = 0
                            action_started = True
                            action['id'] = str(chosen_o)
                            action['force'] = [a, 30 * (2 ** amult), 0]
                            action['torque'] = [0, g, 0]
                            action['action_pos'] = map(float, pos)
                        msg['msg']['actions'].append(action)
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


while True:
    msg = sock2.recv_json()
    print(msg)
    if 'command' in msg and msg['command'] == 'get_valid':
        va = np.asarray(valid)
        undone = (va == 0).nonzero()[0]
        if len(undone) > 0:
            bn = undone[0] / BATCH_SIZE
            make_new_batch(bn)
        va = np.asarray(valid).nonzero()[0]
        batches = np.unique(np.floor(va / BATCH_SIZE)).astype(np.int)
        send_array(sock2, batches)
    else:      
        bn = msg['batch_num']
        bsize = BATCH_SIZE
        bn1 = bn % (N / bsize)
        start = (bn * bsize) % N
        end = ((bn + 1) * bsize - 1) % N + 1
        make_new_batch(bn)

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
    

