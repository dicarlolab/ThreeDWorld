import numpy as np
import scipy.linalg
import json
from curiosity.utils.io import (handle_message,
                                send_array,
                                recv_array)
import h5py
import os

class agent:
        is_init = True

        SCREEN_WIDTH = 256 #512

	BATCH_SIZE = 256
	MULTSTART = -1

	achoice = [-5, 0, 5]
	ACTION_LENGTH = 15
	ACTION_WAIT = 15

	N = 1024000
	valid = np.zeros((N,1)) 

	rng = np.random.RandomState(0)
        use_stabilization = True

	def open_hdf5(self, path):
            h5path = os.path.join(path, 'dataset1')
            self.hdf5 = h5py.File(h5path, mode='a')
	    self.valid = self.hdf5.require_dataset('valid', shape=(self.N,), dtype=np.bool)
	    self.images = self.hdf5.require_dataset('images', shape=(self.N, 256, 256, 3), dtype=np.uint8)
	    self.normals = self.hdf5.require_dataset('normals', shape=(self.N, 256, 256, 3), dtype=np.uint8)
	    self.objects = self.hdf5.require_dataset('objects', shape=(self.N, 256, 256, 3), dtype=np.uint8)

	def choose(self, x):
	  index = self.rng.randint(len(x))
	  return [x[index], index]

	def choose_action_position(self, objarray):
	  xs, ys = (objarray > 2).nonzero()
	  pos = zip(xs, ys)
	  return pos[self.rng.randint(len(pos))]

	def find_in_observed_objects(self, idx, obs_obj):
	    for i in range(len(obs_obj)):
		if obs_obj[i][1] == idx:
		    return i
	    return -1

	def stabilize(self, rotation, angvel, target_axis=[0,1,0], stability=0.3, speed=0.5):
	    norm = np.linalg.norm(angvel)
            if(norm == 0):
		norm = np.array(1)
	    ang = np.linalg.norm(angvel) * stability / speed;
	    rot = scipy.linalg.expm3(np.cross(np.eye(3), angvel / norm * ang))
	    target_axis = np.array(target_axis)
	    target_pred = np.dot(rot, np.array(rotation))
	    torque = np.cross(target_pred, target_axis)
	    return (torque * speed * speed).tolist()

        def rotate_smooth(self, current_up, current_angvel, target_rot, speed = 0.01):
	    for i in range(len(target_rot)):
	        if target_rot[i] > 360:
		    target_rot[i] -= 360
                if target_rot[i] < 0:
		    target_rot[i] += 360
	    print 'TARGET_ROT ' + str(target_rot)
	#    direction = (np.array(target_rot) - np.array(current_rot))
	#    print str(target_rot)
	#    print str(current_rot)
        #   direction = speed * direction

	    target_rot = np.array(target_rot)
	    target_rot = np.deg2rad(target_rot)
	    # x axis rotation
	    th = target_rot[0]
	    rx = np.array([[1, 0, 0], [0, np.cos(th), np.sin(th)], [0, -np.sin(th), np.cos(th)]])
	    # y axis rotation
	    th = target_rot[1]
	    ry = np.array([[np.cos(th), 0, -np.sin(th)], [0, 1, 0], [np.sin(th), 0, np.cos(th)]])
	    # z axis rotation
	    th = target_rot[2]
	    rz = np.array([[np.cos(th), np.sin(th), 0], [-np.sin(th), np.cos(th), 0], [0, 0, 1]])

	    target_axis = np.matmul(np.matmul(np.matmul(rx,ry), rz), current_up)
 
	    # z rotation only does not work with [0, 0, 1] have to rotate around other axis
            #if(target_axis == np.array([0, 0, 1])).all():
            #    current_up = [0, 1, 0]
	    #	target_axis = np.matmul(np.matmul(np.matmul(rx,ry), rz), current_up)
            return target_axis #self.stabilize(current_up, current_angvel, target_axis)
	# bn integer
	def make_new_batch(self, bn, sock, path, create_hdf5):
	    if(self.is_init and create_hdf5):
                self.open_hdf5(path)
                is_init = False
            # how many frames of action its allowed to take
	    action_length = self.ACTION_LENGTH #(bsize - i) / 3
	    # how long it waits after action end
	    action_wait = self.ACTION_WAIT
	    # how long it waits when it gets stuck before it turns away
	    init_y_pos = 0

	    # look around variables 
            look_init_direction = [0, 0, 0]
            look_length = 25
	    look_around = 0
            look_is_looking = 1
            look_i = 0

	    # turn variables
  	    turn_on = False
	    turn_length = 5

	    bsize = self.BATCH_SIZE
	    start = self.BATCH_SIZE * bn
	    end = self.BATCH_SIZE * (bn + 1)
	    if not self.valid[start: end].all():
		print("Getting new %d-%d" % (start, end))
		ims = []
		objs = []
		norms = []
		infolist = []
		for i in range(bsize):
		    print(i)
		    info, narray, oarray, imarray = handle_message(sock,
								   write=True,
								   outdir=path, prefix=str(bn) + '_' + str(i))

		    info = json.loads(info)

		    #Print object information 
		    #print '................'
		    #print 'observed object 0'
		    #print info['observed_objects'][0][0] #name
		    #print info['observed_objects'][0][1] #ID
		    #print info['observed_objects'][0][2] #position
		    #print info['observed_objects'][0][3] #rotation
		    #print info['observed_objects'][0][4] #isStatic
		    print '................'
		    #print 'avatar'
		    #print info['avatar_position']
		    #print info['avatar_rotation']
		    #print info['avatar_up']
		    #print '................'

		    msg = {'n': 4,
			   'msg': {"msg_type": "CLIENT_INPUT",
				   "get_obj_data": True,
				   "actions": []}}	    
		    # if agent tilted move it back
		    is_tilted = info['avatar_up'][1] < 0.99
		    
		    
		    if look_around == 0: 
                         look_around = self.rng.randint(5) + 1
			 print look_around

		    # teleport and reinitialize
		    if i == 0:
			if bn == 0:
			    msg['msg']['get_obj_data'] = True
			msg['msg']['teleport_random'] = True
			chosen = False
			action_started = False
			action_done = False
			action_ind = 0
			objpi = []
			objpi2 = []
			aset = self.achoice[:]
			amult = self.MULTSTART
			# initial y-pos of agent
			init_y_pos = info['avatar_position'][1]
		    elif look_around == 1:
                        # first look sequence - select target angle
			if look_i == 0: 
			    # store init looking direction
			    if look_is_looking == 1:
                                look_init_direction = info['avatar_rotation']
			    target_angle = []
			    random_angle = (self.rng.randint(30) - 15)
                            target_angle.append(random_angle)
			    random_angle = self.rng.randint(360) - 180;
			    while(abs(random_angle) < 140):
				 random_angle = (self.rng.randint(360) - 180);
                            target_angle.append(random_angle)
                            target_angle.append(0)
			    target_axis = self.rotate_smooth(info['avatar_forward'], info['avatar_angvel'], target_angle)
			    #if look_is_looking % 2 != 0: 
			    #    target_angle = look_init_direction
			look_i = look_i + 1
			# end of one looking sequence
                        if look_i == look_length:
                            look_i = 0
			    look_is_looking += 1
		
		        if look_is_looking % 2 == 0:
			    msg['msg']['ang_vel'] = self.stabilize(info['avatar_up'], info['avatar_angvel'])
			    #print "STAB: " + str(info['avatar_forward'])
			else:
	 		    print 'TARGET_ANGLE ' + str(target_angle)
			    print 'AVATER ROT ' + str(info['avatar_rotation'])
			    print 'IS LOOKING ' + str(look_is_looking)
   			    print 'TARGET_AXIS ' + str(target_axis)
			    msg['msg']['ang_vel'] = self.stabilize(info['avatar_forward'], info['avatar_angvel'], target_axis)
		    	   

			msg['msg']['vel'] = [0, 0, 0]
			print 'LOOK AROUND!'		
                    # stand back up
		    elif is_tilted and self.use_stabilization:
			print('standing back up')
			msg['msg']['ang_vel'] = self.stabilize(info['avatar_up'], info['avatar_angvel'])
                        #action_done = False
                        #action_started = False
                        #action_ind = 0
                        #objpi = []
                        #aset = self.achoice[:]
                        #amult = self.MULTSTART
                        #chosen = False
                        #g = 7.5 * (2 * self.rng.uniform() - 1)
		    # object interactions 
		    else:
			oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
			obs = np.unique(oarray1)
			valido = []
			for o in info['observed_objects']:
			    if not o[4] and o[1] in obs:
				valido.append(o[1])
			obs = np.array(valido)
			# random searching for object
			if len(obs) == 0:
			    print('turning at %d ... ' % i)			    
       		    
			    if not turn_on:
			        turn_on = True
                                t = 0
				target_angle = []
				target_angle.append(0)
				random_angle = self.rng.randint(360) - 180;
				while(abs(random_angle) < 140):
				    random_angle = (self.rng.randint(360) - 180);
				target_angle.append(random_angle)
				target_angle.append(0)
				target_axis = self.rotate_smooth(info['avatar_forward'], info['avatar_angvel'], target_angle)
			        target_velocity = 2 * (2 * self.rng.uniform() - 1)
			    if t == turn_length:
			        turn_on = False
			    t += 1
			    
			    #msg['msg']['ang_vel'] = [0, 10 * (2 * self.rng.uniform() - 1), 0]
                            msg['msg']['ang_vel'] = self.stabilize(info['avatar_forward'], info['avatar_angvel'], target_axis)
			    msg['msg']['vel'] = [0, 0, 0.1 * target_velocity]
			    action_done = False
			    action_started = False
			    action_ind = 0
			    objpi = []
			    aset = self.achoice[:]
			    amult = self.MULTSTART
			    chosen = False
			    g = 7.5 * (2 * self.rng.uniform() - 1) 
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
				aset = self.achoice[:]
				amult = self.MULTSTART
				chosen_o, index_o = self.choose(obs[np.argsort(fracs)[-10:]])
				chosen = True
				print('Choosing object', chosen_o)
				g = 15. * (2 * self.rng.uniform() - 1)
				a = self.achoice[self.rng.randint(len(self.achoice))]
			    # determine fraction of chosen objects
			    if chosen_o not in obs.tolist():
				frac0 = 0
			    else:
				frac0 = fracs[obs.tolist().index(chosen_o)]
			    #print('FRAC:', frac0, chosen, chosen_o, action_started, action_ind, action_done)
			    # reset if action is done
			    if action_ind >= action_length + action_wait or action_done and action_started:
				action_done = True
				action_started = False
				action_ind = 0
			    # if object too far and no action is performed move closer
			    if frac0 < 0.015 and not action_started:
				xs, ys = (oarray1 == chosen_o).nonzero()
				pos = np.round(np.array(zip(xs, ys)).mean(0))
				if np.abs(self.SCREEN_WIDTH/2 - pos[1]) < 10:
				    d = 0
				else:
				    d =  -0.1 * np.sign(self.SCREEN_WIDTH/2 - pos[1])
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
						aset = ((2 ** (amult)) * np.array(self.achoice)).tolist()
					    a = aset[self.rng.randint(len(aset))]
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
					elif action_type == 1:
						obs_obj = info['observed_objects']
						idx = self.find_in_observed_objects(chosen_o, obs_obj)
						idx2 = self.find_in_observed_objects(chosen_o2, obs_obj)

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
					    chosen_o2, index_o2 = self.choose(obs[np.argsort(fracs)[-10:]])

				    objpi = []
				    objpi2 = []
				    action_ind = 0
				    action_type = self.rng.randint(2)
				    action_started = True
				    if action_type == 0:
					action['id'] = str(chosen_o)
					action['force'] = [a, 100 * (2 ** amult), 0]
					action['torque'] = [0, g, 0]
					action['action_pos'] = map(float, pos)
					print 'MOVE OBJECT! ' + str(chosen_o)

				    elif action_type == 1:
					obs_obj = info['observed_objects']
					idx = self.find_in_observed_objects(chosen_o, obs_obj)
					idx2 = self.find_in_observed_objects(chosen_o2, obs_obj)
					
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
		    if not is_tilted and init_y_pos != info['avatar_position'][1]:
			#print('moving up/down')
                        gravity = info['avatar_position'][1] - init_y_pos
			if 'vel' in msg['msg']:
			    msg['msg']['vel'][1] = -0.1 * gravity
			else:
			    msg['msg']['vel'] = [0, -0.1 * gravity, 0]
		    
		    #msg['msg']['output_formats'] = ["png", "png", "jpg"]
		    infolist.append(msg['msg'])
		    ims.append(imarray)
		    norms.append(narray)
		    objs.append(oarray)
		    sock.send_json(msg['msg'])
		ims = np.array(ims)
		norms = np.array(norms)
		objs = np.array(objs)

		if(create_hdf5):
                    actioninfopath = os.path.join(path, str(bn) + '_action.json')
		    with open(actioninfopath, 'w') as _f:
		        json.dump(infolist, _f)
                    objectinfopath = os.path.join(path, str(bn) + '_objects.json')
                    with open(objectinfopath, 'w') as _f:
                        json.dump(info, _f)

		    self.images[start: end] = ims
		    self.normals[start: end] = norms
		    self.objects[start: end] = objs
		    self.valid[start: end] = True
	    if(create_hdf5):
	        self.hdf5.flush()
