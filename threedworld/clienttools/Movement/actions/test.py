import numpy as np
import scipy.linalg
import scipy.ndimage.morphology
import json
from curiosity.utils.io import (handle_message,
                                send_array,
                                recv_array)
import h5py
import os

class agent:

	WRITE_FILES = False

        SCREEN_WIDTH = 256 #512
        SCREEN_HEIGHT = 256 #384

	BATCH_SIZE = 25600
	MULTSTART = -1

	achoice = [-5, 0, 5]
	ACTION_LENGTH = 15
	ACTION_WAIT = 15

	N = 256000
	valid = np.zeros((N,1)) 

	rng = np.random.RandomState(0)
        use_stabilization = True

        def __init__(self, CREATE_HDF5, path='', dataset_num=-1):
            if(CREATE_HDF5):
                self.open_hdf5(path, dataset_num)


	def set_screen_width(self, screen_width):
	    self.SCREEN_WIDTH = screen_width
        
        def set_screen_height(self, screen_height):
            self.SCREEN_HEIGHT = screen_height

	def open_hdf5(self, path, dataset_num=-1):
            if dataset_num == -1:
                file_iter = 1
                h5path = os.path.join(path, 'dataset' + str(file_iter) + ".hdf5")
                while os.path.isfile(h5path):
                    file_iter += 1
                    h5path = os.path.join(path, 'dataset' + str(file_iter) + ".hdf5")
            else:
                h5path = os.path.join(path, 'dataset' + str(dataset_num) + ".hdf5")

            self.hdf5 = h5py.File(h5path, mode='a')

        def get_hdf5_handles(self):
            dt = h5py.special_dtype(vlen=str)
            valid = self.hdf5.require_dataset('valid', shape=(self.N,), dtype=np.bool)
            images = self.hdf5.require_dataset('images', shape=(self.N, self.SCREEN_HEIGHT, self.SCREEN_WIDTH, 3), dtype=np.uint8)
            normals = self.hdf5.require_dataset('normals', shape=(self.N, self.SCREEN_HEIGHT, self.SCREEN_WIDTH, 3), dtype=np.uint8)
            objects = self.hdf5.require_dataset('objects', shape=(self.N, self.SCREEN_HEIGHT, self.SCREEN_WIDTH, 3), dtype=np.uint8)
            worldinfos = self.hdf5.require_dataset('worldinfo', shape=(self.N,), dtype=dt)
            agentactions = self.hdf5.require_dataset('actions', shape=(self.N,), dtype=dt)
            return [valid, images, normals, objects, worldinfos, agentactions]

        def choose(self, x):
	  index = self.rng.randint(len(x))
	  return [x[index], index]

	def choose_action_position(self, objarray, chosen_object, slipage=2):

          selection = np.zeros(objarray.shape)
          selection[objarray == chosen_object] = objarray[objarray == chosen_object]

	  # dilate image to add imprecision in selecting actions
	  dialated_selection = scipy.ndimage.grey_dilation(selection, size=(slipage,slipage))
	  xs, ys = dialated_selection.nonzero()
	  pos = zip(xs, ys)
	  if len(pos) == 0:
	      return [np.array([]), chosen_object]
          chosen_pos = pos[self.rng.randint(len(pos))]
	  return [np.array(chosen_pos), objarray[chosen_pos]]

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
	def make_new_batch(self, bn, sock, path, create_hdf5, use_tdw_msg):
	    if(create_hdf5):
                self.valid, images, normals, objects, worldinfos, agentactions = self.get_hdf5_handles()

            # how many frames of action its allowed to take
	    action_length = self.ACTION_LENGTH #(bsize - i) / 3
	    # how long it waits after action end
	    action_wait = self.ACTION_WAIT
	    waiting = False
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
		infs = []
		for i in range(bsize):
		    print(i)
		    info, narray, oarray, imarray = handle_message(sock,
								   write=self.WRITE_FILES,
								   outdir=path, prefix=str(bn) + '_' + str(i))

		    info = json.loads(info)
		    obs_obj = info['observed_objects']

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
				   "send_scene_info" : True,
				   "actions": []}}	    
		    # if agent tilted move it back
		    is_tilted = info['avatar_up'][1] < 0.99
		    
		    
		    if look_around == 0: 
                         look_around = 0#self.rng.randint(5) + 1

		    # teleport and reinitialize
		    if i == 0:
			if bn == 0:
			    msg['msg']['get_obj_data'] = True
			msg['msg']['teleport_random'] = True
			msg['msg']['action_type'] = "TELEPORT"
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
			    msg['msg']['ang_vel'] = self.stabilize(info['avatar_forward'], info['avatar_angvel'], target_axis)
		    	   

			msg['msg']['vel'] = [0, 0, 0]
			msg['msg']['action_type'] = "LOOK"
			print 'LOOK AROUND!'		
                    # stand back up
		    elif is_tilted and self.use_stabilization:
			print('STANDING BACK UP')
			msg['msg']['ang_vel'] = self.stabilize(info['avatar_up'], info['avatar_angvel'])
                        msg['msg']['action_type'] = "STANDUP"
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
			# remove static objects from object list
			valido = []
			for o in info['observed_objects']:
			    if not o[4] and o[1] in obs:
				valido.append(o[1])
			diff = set(valido).symmetric_difference(set(obs))
			obs = np.array(valido)
			# remove static objects from objects image
			for d in diff:
			    oarray1[oarray1 == d] = 0
			
			# random searching for object
			if len(obs) == 0 and not waiting:
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
			    msg['msg']['action_type'] = "SEARCHING"
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
				chosen_o, index_o = self.choose(obs[np.argsort(fracs)[-5:]])								
				chosen_o_name = obs_obj[self.find_in_observed_objects(chosen_o, obs_obj)][0]
				chosen = True
				print 'CHOOSING OBJECT ' + str(chosen_o) + " " + str(chosen_o_name)
				g = 15. * (2 * self.rng.uniform() - 1)
				a = self.achoice[self.rng.randint(len(self.achoice))]
			   	target_distance = self.rng.rand(1)[0] * 2 + 3.0
			    # determine fraction and position of chosen objects
			    if chosen_o not in obs.tolist():
				frac0 = 0
				obs_dist = np.array([])
			    else:
				frac0 = fracs[obs.tolist().index(chosen_o)]
                                obs_idx = self.find_in_observed_objects(chosen_o, obs_obj)
                                if obs_idx != -1:
                                    pos3d = np.array(obs_obj[obs_idx][2])
				    obs_dist = np.linalg.norm(pos3d - np.array(info['avatar_position']))
		            #print('FRAC:', frac0, chosen, chosen_o, action_started, action_ind, action_done)
			    # reset if action is done
			    if action_ind >= action_length + action_wait or action_done and action_started:
				action_done = True
				action_started = False
				action_ind = 0
				waiting = False
			    # if object too far and no action is performed move closer
			    if obs_dist.size != 0 and obs_dist > target_distance and not action_started:
				print 'MOVING CLOSER TO ' + str(chosen_o) + ' ' + str(chosen_o_name)
				print 'TARGET DISTANCE ' + str(target_distance) + ' > ' + str(obs_dist)
				xs, ys = (oarray1 == chosen_o).nonzero()
				pos = np.round(np.array(zip(xs, ys)).mean(0))
				if np.abs(self.SCREEN_WIDTH/2 - pos[1]) < 10:
				    d = 0
				else:
				    d =  -0.1 * np.sign(self.SCREEN_WIDTH/2 - pos[1])
				msg['msg']['vel'] = [0, 0, .25]
				msg['msg']['ang_vel'] = [0, d, 0]
				msg['msg']['action_type'] = "MOVING_CLOSER"
			    # perform action on chosen object
			    else:
				if action_ind == 0:
				    # choose position of action
				    pos, chosen_id  = self.choose_action_position(oarray1, chosen_o)
				print 'ACTION POS: ' + str(chosen_o) + " " + str(chosen_id)
				
				# find object centroid
				xs, ys = (oarray1 == chosen_o).nonzero()
				centroid = np.round(np.array(zip(xs, ys)).mean(0))
			        objpi.append(centroid)
			
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
					    action['force'] = [a, 105 * (2 ** amult), 0]
					    action['torque'] = [0, g, 0]
					    action['id'] = str(chosen_id)
					    action['object'] = str(chosen_o)
					    action['action_pos'] = map(float, pos)
					    msg['msg']['action_type'] = "MOVING_OBJECT"
					    print 'MOVE OBJECT! ' + str(chosen_o)
					elif action_type == 1:
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

						    action['id'] = str(chosen_id)
						    action['object'] = str(chosen_o)
						    action['force'] = mov.tolist()
						    action['torque'] = [0, g, 0]
						    action['action_pos'] = map(float, pos)

						    action2['id'] = str(chosen_id2)
						    action2['object'] = str(chosen_o2)
						    action2['force'] = mov2.tolist()
						    action2['torque'] = [0, g, 0]
						    action2['action_pos'] = map(float, pos2)
						    msg['msg']['action_type'] = "CRASHING"

						    action_ind = action_length
						    print 'CRASH OBJECTS! ' + str(chosen_o) + ' ' + str(chosen_o2) + ' ' + str(mov) + ' ' + str(mov2) + ' ' + str(pos_obj) + ' ' + str(pos_obj2)+ ' ' + obs_obj[idx][0] + ' ' + obs_obj[idx2][0]
						else:
						    print 'CRASH NOT FOUND!'
						    msg['msg']['action_type'] = "NO_CRASH"
                                        elif action_type == 2:
                                            action['force'] = [0, self.rng.rand(1)[0] * 10 + 48, 0]
                                            action['torque'] = [0, 0, 0]
                                            action['id'] = str(chosen_id)
                                            action['object'] = str(chosen_o)
					    action['action_pos'] = map(float, pos)
					    msg['msg']['action_type'] = "LIFTING"
                                            print 'LIFT OBJECT! ' + str(action['force']) + ' ' + str(chosen_o)
					elif action_type == 3:
                                            action['force'] = [0, 0, 0]
                                            action['torque'] = [0, rotation_torque, 0]
                                            action['id'] = str(chosen_id)
                                            action['object'] = str(chosen_o)
                                            action['action_pos'] = map(float, pos)
					    msg['msg']['action_type'] = "ROTATING"
                                            print 'ROTATE OBJECT! ' + str(action['torque']) + ' ' + str(chosen_o)
                                        elif action_type == 4:
                                            action['id'] = str(chosen_id)
                                            msg['msg']['teleport_to'] = {'position': [2, 0.1, 2],
                                                                     'rotation': [0, 0, 0]}
                                            msg['msg']['action_type'] = "TELEPORT_OBJECT"
                                            action_ind = action_length
				    else:
					print('WAITING')
					msg['msg']['vel'] = [0, 0, 0]
					msg['msg']['ang_vel'] = [0, 0, 0]
					msg['msg']['actions'] = []
					msg['msg']['action_type'] = "WAITING"
					waiting = True
				    action_ind += 1
				    if action_done or (action_ind >= action_length + action_wait):
					action_done = True
					chosen = False
					action_started = False
					waiting = False
				# start new action
				elif not action_started:
				    chosen_o2 = chosen_o
				    if len(obs) > 1:
					while (chosen_o2 == chosen_o):
					    chosen_o2, index_o2 = self.choose(obs[np.argsort(fracs)[-10:]])
				    pos2, chosen_id2  = self.choose_action_position(oarray1, chosen_o2)
				    objpi = []
				    objpi2 = []
				    action_ind = 0
				    action_type = 4#self.rng.randint(4)
				    action_started = True
				    if action_type == 0:
					action['id'] = str(chosen_id)
					action['object'] = str(chosen_o)
					action['force'] = [a, 105 * (2 ** amult), 0]
					action['torque'] = [0, g, 0]
					action['action_pos'] = map(float, pos)
					msg['msg']['action_type'] = "MOVING_OBJECT"
					print 'MOVE OBJECT! ' + str(chosen_o)

				    elif action_type == 1:
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

					    action['id'] = str(chosen_id)
					    action['object'] = str(chosen_o)
					    action['force'] = mov.tolist()
					    action['torque'] = [0, g, 0]
					    action['action_pos'] = map(float, pos)

					    action2['id'] = str(chosen_id2)
					    action2['object'] = str(chosen_o2)
					    action2['force'] = mov2.tolist()
					    action2['torque'] = [0, g, 0]
					    action2['action_pos'] = map(float, pos2)
					    msg['msg']['action_type'] = "CRASHING"

					    action_ind = action_length
					    
					    print 'CRASH OBJECTS! ' + str(chosen_o) + ' ' + str(chosen_o2) + ' ' + str(mov) + ' ' + str(mov2) + ' ' + obs_obj[idx][0] + ' ' + obs_obj[idx2][0]
					else:
					    action_done = True
					    print 'CRASH NOT FOUND!'
					    msg['msg']['action_type'] = "NO_CRASH"
				    elif action_type == 2:
				        action['force'] = [0, self.rng.rand(1)[0] * 10 + 48, 0]
				        action['torque'] = [0, 0, 0]
				        action['id'] = str(chosen_id)
				        action['object'] = str(chosen_o)
					action['action_pos'] = map(float, pos)
					msg['msg']['action_type'] = "LIFTING"
				        print 'LIFT OBJECT! ' + str(action['force']) + ' ' + str(chosen_o)
				    elif action_type == 3:
                                        action['force'] = [0, 0, 0]
					rotation_torque = self.rng.rand(1)[0] * 100 - 50
					while abs(rotation_torque) < 30:
					    rotation_torque = self.rng.rand(1)[0] * 100 - 50
                                        action['torque'] = [0, rotation_torque, 0]
                                        action['id'] = str(chosen_id)
                                        action['object'] = str(chosen_o)
                                        action['action_pos'] = map(float, pos)
                                        msg['msg']['action_type'] = "ROTATING"
					print 'ROTATE OBJECT! ' + str(action['torque']) + ' ' + str(chosen_o)
                                    elif action_type == 4:
                                        action['id'] = str(chosen_id)
                                        msg['msg']['teleport_to'] = {'position': [2, 0.1, 2],
                                                                 'rotation': [0, 0, 0]}
                                        msg['msg']['action_type'] = "TELEPORT_OBJECT"
                                        action_ind = action_length
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
		    if(not 'action_type' in msg['msg']):
			print("ERROR! Action not recognized")
		    else:
		        print(msg['msg']['action_type'])
		    infolist.append(json.dumps(msg['msg']))
		    ims.append(imarray)
		    norms.append(narray)
		    infs.append(json.dumps(info))
		    objs.append(oarray)
                    if use_tdw_msg:
                        sock.send_json(msg)
                    else:
		        sock.send_json(msg['msg'])
		ims = np.array(ims)
		norms = np.array(norms)
		objs = np.array(objs)

		if(create_hdf5):
                   #actioninfopath = os.path.join(path, str(bn) + '_action.json')
                   #with open(actioninfopath, 'w') as _f:
                   #    json.dump(infolist, _f)
                   #objectinfopath = os.path.join(path, str(bn) + '_objects.json')
                   #with open(objectinfopath, 'w') as _f:
                   #    json.dump(infs, _f)

		    images[start: end] = ims
		    normals[start: end] = norms
		    objects[start: end] = objs
		    self.valid[start: end] = True
		    worldinfos[start: end] = infs
		    agentactions[start: end] = infolist
	    if(create_hdf5):
	        self.hdf5.flush()