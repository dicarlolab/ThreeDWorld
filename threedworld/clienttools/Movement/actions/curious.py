import numpy as np
import json
from curiosity.utils.io import (handle_message,
                                send_array,
                                recv_array)

SCREEN_WIDTH = 256 #640

BATCH_SIZE = 256
MULTSTART = -1

achoice = [-5, 0, 5]
ACTION_LENGTH = 15
ACTION_WAIT = 15

N = 1024000 
valid = np.zeros((N,1)) 

rng = np.random.RandomState(0)

def choose(x):
  return x[rng.randint(len(x))]

def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

# bn integer
def make_new_batch(bn, sock, path):
    # how many frames of action its allowed to take
    action_length = ACTION_LENGTH #(bsize - i) / 3
    # how long it waits after action end
    action_wait = ACTION_WAIT
    # how long it waits when it gets stuck before it turns away
    is_stuck = False
    x_torque_prev = 360
    z_torque_prev = 360

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
                                                           write=False,
                                                           outdir=path, prefix=str(bn) + '_' + str(i))

            info = json.loads(info)

            #Print object information 
            #print '................'
            #print 'observed object 0'
            #print info['observed_objects'][0][0] #name
            #print info['observed_objects'][0][1] #ID
            #print info['observed_objects'][0][2] #position
            #print info['observed_objects'][0][3] #rotation

            print '................'
            print 'avatar'
            print info['avatar_position']
            print info['avatar_rotation']
            print '................'

            msg = {'n': 4,
                   'msg': {"msg_type": "CLIENT_INPUT",
                           "get_obj_data": True,
                           "actions": []}}

	    # if agent tilted move it back
	    x_angle = info['avatar_rotation'][0]
	    y_angle = info['avatar_rotation'][1]
	    z_angle = info['avatar_rotation'][2]

	    if i == 0:
                if bn == 0:
                    msg['msg']['get_obj_data'] = True
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
            elif (x_angle > 1 and x_angle < 359) or (z_angle > 1 and z_angle < 359):
		print('standing back up')
		x_torque = -0.01 * x_angle
		z_torque = -0.01 * z_angle
		x_torque = min(x_torque, -0.1)
		z_torque = min(z_torque, -0.1)
		
		if x_angle > 180:
		    x_torque = 0.01 * (360 - x_angle)
		    x_torque = max(x_torque, 0.1)
		
		if z_angle > 180:
		    z_torque = 0.01 * (360 - z_angle)
		    z_torque = max(z_torque, 0.1)

		if x_torque > x_torque_prev or z_torque > z_torque_prev:
		    is_stuck = True

		# if the agent is stuck rotate randomly
		if is_stuck:
		    print('teleport')
		    msg['msg']['teleport_random'] = True
        	    is_stuck = False
		    x_torque_prev = 360
		    z_torque_prev = 360
		else:
		    print('x-turn and z-turn')
		    msg['msg']['ang_vel'] = [x_torque, 0 , z_torque]
		msg['msg']['vel'] = [0, 0, 0]
		x_torque_prev = x_torque
		z_torque_prev = z_torque
            else:
                oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
                obs = np.unique(oarray1)
                obs = obs[obs > 18]
                obs = obs[obs < 256]
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
                    if frac0 < 0.005 and not action_started:
                        xs, ys = (oarray1 == chosen_o).nonzero()
                        pos = np.round(np.array(zip(xs, ys)).mean(0))
                        if np.abs(SCREEN_WIDTH/2 - pos[1]) < 10:
                            d = 0
                        else:
                            d =  -0.1 * np.sign(SCREEN_WIDTH/2 - pos[1])
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
                                action['force'] = [a, 100 * (2 ** amult), 0]
                                action['torque'] = [0, g, 0]
                                action['id'] = str(chosen_o)
                                action['action_pos'] = map(float, objpi[-1])
                                print 'MOVE OBJECT! ' + str(chosen_o)
                            else:
                                print(action_ind, i, 'waiting')
                                msg['msg']['vel'] = [0, 0, 0]
                                msg['msg']['ang_vel'] = [0, 0, 0]
                                msg['msg']['actions'] = []
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
                            action['force'] = [a, 100 * (2 ** amult), 0]
                            action['torque'] = [0, g, 0]
                            action['action_pos'] = map(float, pos)
                            print 'MOVE OBJECT! ' + str(chosen_o)
                        if 'id' in action:
                            msg['msg']['actions'].append(action)
            infolist.append(msg['msg'])
            ims.append(imarray)
            norms.append(narray)
            objs.append(oarray)
            sock.send_json(msg['msg'])
        ims = np.array(ims)
        norms = np.array(norms)
        objs = np.array(objs)

        #infopath = os.path.join(infodir, str(bn) + '.json')
        #with open(infopath, 'w') as _f:
        #    json.dump(infolist, _f)

        #images[start: end] = ims
        #normals[start: end] = norms
        #objects[start: end] = objs
        valid[start: end] = True
    #file.flush()
