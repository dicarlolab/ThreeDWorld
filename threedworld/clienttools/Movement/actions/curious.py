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
  index = rng.randint(len(x))
  return [x[index], index]

def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

def find_in_observed_objects(idx, obs_obj):
    for i in range(len(obs_obj)):
	if obs_obj[i][1] == idx:
	    return i
    return -1

# bn integer
def make_new_batch(bn, sock, path):
    # how many frames of action its allowed to take
    action_length = ACTION_LENGTH #(bsize - i) / 3
    # how long it waits after action end
    action_wait = ACTION_WAIT
    # how long it waits when it gets stuck before it turns away
    is_stuck = False
    time_stuck = 2
    direction_stuck = 1;
    x_torque_prev = 360
    z_torque_prev = 360
    init_y_pos = 0

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
            #print 'avatar'
            #print info['avatar_position']
            #print info['avatar_rotation']
            #print '................'

            msg = {'n': 4,
                   'msg': {"msg_type": "CLIENT_INPUT",
                           "get_obj_data": True,
                           "actions": []}}
	    	    
	    # if agent tilted move it back
	    x_angle = info['avatar_rotation'][0]
	    y_angle = info['avatar_rotation'][1]
	    z_angle = info['avatar_rotation'][2]
	    
	    is_tilted = (x_angle > 5 and x_angle < 355) or (z_angle > 5 and z_angle < 355)
	    # teleport and reinitialize
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
                objpi2 = []
		aset = achoice[:]
                amult = MULTSTART
		# initial y-pos of agent
		init_y_pos = info['avatar_position'][1]
	    # stand back up
	    elif is_tilted or is_stuck:
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
		    print('angle set')
		    is_stuck = True

		# if the agent is stuck rotate randomly
		if is_stuck:
		    print('move backwards')
		    #msg['msg']['teleport_random'] = False
        	    if time_stuck > 0:
		    	time_stuck = time_stuck - 1;
		    else:
			direction_stuck = np.sign(2 * rng.uniform() - 1);
			time_stuck = 2;
		    	is_stuck = False
		    	x_torque_prev = 360
		     	z_torque_prev = 360
		    	chosen = False
		    	action_started = False
		    	action_done = False 
		    msg['msg']['ang_vel'] = [0, 0.1 * direction_stuck, 0]
		    msg['msg']['vel'] = [0, 0.1, -0.1]
		elif x_torque < 0.01 or z_torque < 0.01:
		    msg['msg']['set_ang'] = [0, y_angle, 0]
		else:
		    print('x-turn and z-turn')
		    msg['msg']['ang_vel'] = [x_torque, 0 , z_torque]
		    msg['msg']['vel'] = [0, 0, 0]
		x_torque_prev = x_torque
		z_torque_prev = z_torque
	    # object interactions 
	    else:
                oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
                obs = np.unique(oarray1)
                obs = obs[obs > 18]
                obs = obs[obs < 256]
		# random searching for object
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
                # object selection
		else:
                    fracs = []
                    for o in obs:
                        frac = (oarray1 == o).sum() / float(np.prod(oarray.shape))
                        fracs.append(frac)
		    # choose new objects
                    if not chosen or (chosen_o not in obs and ((not action_started) or action_done)):
                        action_started = False
                        action_done = False
                        action_ind = 0
                        objpi = []
                        aset = achoice[:]
                        amult = MULTSTART
                        chosen_o, index_o = choose(obs[np.argsort(fracs)[-10:]])
			chosen = True
                        print('Choosing object', chosen_o)
                        g = 15. * (2 * rng.uniform() - 1)
                        a = achoice[rng.randint(len(achoice))]
                    # determine fraction of chosen objects
		    if chosen_o not in obs.tolist():
                        frac0 = 0
                    else:
                        frac0 = fracs[obs.tolist().index(chosen_o)]
                    print('FRAC:', frac0, chosen, chosen_o, action_started, action_ind, action_done)
                    # reset if action is done
		    if action_ind >= action_length + action_wait or action_done and action_started:
                        action_done = True
                        action_started = False
                        action_ind = 0
		    # if object too far and no action is performed move closer
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
		    # perform action on chosen object
                    else:
                        xs, ys = (oarray1 == chosen_o).nonzero()
                        pos = np.round(np.array(zip(xs, ys)).mean(0))
                        objpi.append(pos)
                
			action = {}
			action2 = {}
                        # continue action
			if action_started and not action_done:
                            # if object was lost, then wait  
			    if chosen_o not in obs.tolist() and action_ind < action_length:
                                action_ind = action_length
			    # if action is running and object is there, then perform action
                            if action_ind < action_length and len(objpi) > 2:
                                dist = np.sqrt(((objpi[-2] - objpi[-1])**2).sum())
                                print('dist', dist)
				# if object has not moved, reset action and remove already performed force from action set
                                if dist < 1:
                                    action_ind = 0
                                    aset.remove(a)
                                    # increase force
				    if len(aset) == 0:
                                        amult += 1
                                        aset = ((2 ** (amult)) * np.array(achoice)).tolist()
                                    a = aset[rng.randint(len(aset))]
				    # marked as done if object still hasn't moved with biggest force
                                    if amult > 10:
                                        action_done = True
                                        action_started = False
                                    print('choosing "a"', dist, a)
                                if action_type == 0:
				    action['force'] = [a, 100 * (2 ** amult), 0]
                                    action['torque'] = [0, g, 0]
                                    action['id'] = str(chosen_o)
                                    action['action_pos'] = map(float, objpi[-1])
                                    print 'MOVE OBJECT! ' + str(chosen_o)
				elif action_type == 1 or action_type == 2:
					obs_obj = info['observed_objects']
					idx = find_in_observed_objects(chosen_o, obs_obj)
					idx2 = find_in_observed_objects(chosen_o2, obs_obj)

					if(idx != -1 and idx2 != -1 and idx != idx2):
					    pos_obj = np.array(obs_obj[idx][2])
					    pos_obj2 = np.array(obs_obj[idx2][2])

					    mov = pos_obj2 - pos_obj
					    mov[1] = 0
					    #mov = mov / np.linalg.norm(mov)
					    mov = 200 * (2 ** amult) * mov
					    mov2 = -mov

					    action['id'] = str(chosen_o)
					    action['force'] = mov.tolist()
					    action['torque'] = [0, g, 0]
					    action['action_pos'] = map(float, pos)

					    action2['id'] = str(chosen_o2)
					    action2['force'] = mov2.tolist()
					    action2['torque'] = [0, g, 0]
					    action2['action_pos'] = map(float, pos)

					    action_ind = action_length
					    print 'CRASH OBJECTS! ' + str(chosen_o) + ' ' + str(chosen_o2) + ' ' + str(mov) + ' ' + str(mov2) + ' ' + str(pos_obj) + ' ' + str(pos_obj2)+ ' ' + obs_obj[idx][0] + ' ' + obs_obj[idx2][0]
					else:
					    print 'CRASH NOT FOUND!'

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
			# start new action
                        elif not action_started:
			    chosen_o2 = chosen_o
			    if len(obs) > 1:
				while (chosen_o2 == chosen_o):
				    chosen_o2, index_o2 = choose(obs[np.argsort(fracs)[-10:]])

                            objpi = []
                            objpi2 = []
			    action_ind = 0
                            action_type = rng.randint(2)
			    action_started = True
			    if action_type == 0 or action_type == 2:
                                action['id'] = str(chosen_o)
                                action['force'] = [a, 100 * (2 ** amult), 0]
                                action['torque'] = [0, g, 0]
                                action['action_pos'] = map(float, pos)
                                print 'MOVE OBJECT! ' + str(chosen_o)

                    	    elif action_type == 1:
				obs_obj = info['observed_objects']
				idx = find_in_observed_objects(chosen_o, obs_obj)
				idx2 = find_in_observed_objects(chosen_o2, obs_obj)
				
				if(idx != -1 and idx2 != -1 and idx != idx2):
				    pos_obj = np.array(obs_obj[idx][2])
				    pos_obj2 = np.array(obs_obj[idx2][2])

				    mov = pos_obj2 - pos_obj
				    mov[1] = 0
				    #mov = mov / np.linalg.norm(mov)
				    mov = 200 * (2 ** amult) * mov
				    mov2 = -mov

				    action['id'] = str(chosen_o)
				    action['force'] = mov.tolist()
				    action['torque'] = [0, g, 0]
				    action['action_pos'] = map(float, pos)

				    action2['id'] = str(chosen_o2)
				    action2['force'] = mov2.tolist()
				    action2['torque'] = [0, g, 0]
				    action2['action_pos'] = map(float, pos)

				    action_ind = action_length
				    
				    print 'CRASH OBJECTS! ' + str(chosen_o) + ' ' + str(chosen_o2) + ' ' + str(mov) + ' ' + str(mov2) + ' ' + obs_obj[idx][0] + ' ' + obs_obj[idx2][0]
				else:
				    action_done = True
				    print 'CRASH NOT FOUND!'
		        # add action if new action defined
			if 'id' in action:
                            msg['msg']['actions'].append(action)
			if 'id' in action2:
			    msg['msg']['actions'].append(action2)
            
	    # move down 
       	    if not (is_tilted or is_stuck) and init_y_pos + 0.01 < info['avatar_position'][1]:
		print('moving down')
		if 'vel' in msg['msg']:
		    msg['msg']['set_ang'] = [0, y_angle, 0]
		    msg['msg']['vel'][1] = -0.1
		else:
		    msg['msg']['set_ang'] = [0, y_angle, 0]
		    msg['msg']['vel'] = [0, -0.1, 0]

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
