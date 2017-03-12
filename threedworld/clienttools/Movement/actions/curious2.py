import numpy as np
import scipy.linalg
import scipy.ndimage.morphology
import json
from curiosity.utils.io import (handle_message,
                                send_array,
                                recv_array)
import h5py
import os

def init_msg():
	msg = {'n': 4, 'msg': {"msg_type": "CLIENT_INPUT", "get_obj_data": True, "send_scene_info" : True, "actions": []}}
	msg['msg']['vel'] = [0, 0, 0]
	msg['msg']['ang_vel'] = [0, 0, 0]
	return msg

#reasonable push magnitudes: x_magnitude 100
#reasonable lift magnitudes: y_magnitude 120, x_magnitude 50
#reasonable rotation: magnitude = 100

def make_const_simple_push(rng, time_len, magnitude):
	return make_const_action_sequences(rng, time_len, f_horiz = magnitude)

def make_const_simple_lift(rng, time_len, x_magnitude, y_magnitude):
	return make_const_action_sequences(rng, time_len, f_horiz = x_magnitude, f_y = y_magnitude)

def make_const_simple_rot(rng, time_len, magnitude):
	return make_const_action_sequences(rng, time_len, tor_y = magnitude)

def make_const_action_sequences(rng, time_len, f_horiz = 0, f_y = 0, tor_horiz = 0, tor_y = 0):
	while True:
		f_dir = rng.randn(2)
		f_norm = np.linalg.norm(f_dir)
		tor_dir = rng.randn(2)
		tor_norm = np.linalg.norm(tor_dir)
		tor_y_sign = 2 * rng.randint(0, 2) - 1
		if f_norm > .0001 and tor_norm > .0001:
			f_dir = f_dir / f_norm
			tor_dir = tor_dir / tor_norm
			f = [f_horiz * f_dir[0], f_y, f_horiz * f_dir[1]]
			tor = [tor_horiz * tor_dir[0], tor_y_sign * tor_y, tor_horiz * tor_dir[1]]
			break
	return [f for _ in range(time_len)], [tor for _ in range(time_len)]



class agent:

	WRITE_FILES = False

        SCREEN_WIDTH = 256 #512
        SCREEN_HEIGHT = 256 #384

	BATCH_SIZE = 256
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
	    raise Exception('Object not in there!')
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


	def rectify(self, msg):
		#TODO implement
		return None

	def send_msg(self, msg):
		if(not 'action_type' in msg['msg']):
			print("ERROR! Action not recognized")
		else:
			print(msg['msg']['action_type'])
		self.in_batch_counter += 1
		print('counter ' + str(self.in_batch_counter))
		if self.use_tdw_msg:
			self.sock.send_json(msg)
		else:
			self.sock.send_json(msg['msg'])

	def teleport_random(self):
		msg = init_msg()
		msg['msg']['teleport_random'] = True
		msg['msg']['action_type'] = 'TELEPORT'
		print('TELEPORT')
		self.init_y_pos = self.info['avatar_position'][1]
		self.send_msg(msg)

	def observe_world(self):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return
		info, self.narray, self.oarray, self.imarray = handle_message(self.sock, write=self.WRITE_FILES, outdir=self.path, prefix=str(self.bn) + '_' + str(self.in_batch_counter))
		self.info = json.loads(info)
		self.oarray1 = 256**2 * self.oarray[:, :, 0] + 256 * self.oarray[:, :, 1] + self.oarray[:, :, 2]

	def select_object_in_view(self):
		#might just be replaced with selecting a random object
		obs = np.unique(self.oarray1)
		valido = []
		for o in self.info['observed_objects']:
		    if not o[4] and o[1] in obs:
			valido.append(o[1])
		diff = set(valido).symmetric_difference(set(obs))
		obs = np.array(valido)
		for d in diff:
		    self.oarray1[self.oarray1 == d] = 0
		if len(obs) == 0:
			raise Exception('Not yet implemented if no objects seen!')
		fracs = [(self.oarray1 == o).sum() / float(np.prod(self.oarray.shape)) for o in obs]
		chosen_o, index_o = self.choose(obs[np.argsort(fracs)[-5:]])
		return chosen_o

	def select_random_object(self):
		valid_objects = [o for o in self.info['observed_objects'] if not o[4] ]
		return valid_objects[self.rng.randint(len(valid_objects))]

	def teleport_to_object(self, chosen_o, distance_from = 2):
		#TODO implement random position around object
		#TODO implement looking-from-above
		if self.in_batch_counter >= self.BATCH_SIZE:
			return
		pos = chosen_o[2]
		if pos[0] < 2:
			print('exception spot')
			target_pos = [pos[0] + distance_from, self.init_y_pos, pos[2]]
			target_rot = [-1, 0, 0]
		else:
			target_pos = [pos[0] - distance_from, self.init_y_pos, pos[2]]
			target_rot = [1, 0, 0]
		msg = init_msg()
		msg['msg']['teleport_to'] = {'position' : target_pos, 'rotation' : target_rot}
		msg['msg']['action_type'] = 'TELE_TO_OBJ'
		self.send_msg(msg)

	def wait(self, waiting_time):
		t = 0
		while self.in_batch_counter < self.BATCH_SIZE and t < waiting_time:
			self.observe_world()
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			t += 1



	def get_to_object(self, chosen_o, target_distance = 2.):
		#might just be replaced with teleporting towards an object
		obs_obj = self.info['observed_objects']
		obs_idx = self.find_in_observed_objects(chosen_o, obs_obj)
		chosen_o_name = obs_obj[obs_idx][0]
		print 'Object chosen: ' + str(chosen_o) + ' ' + str(chosen_o_name)
		pos3d = np.array(obs_obj[obs_idx][2])
		obs_dist = np.linalg.norm(pos3d - np.array(self.info['avatar_position']))
		while obs_dist > target_distance and self.in_batch_counter < self.BATCH_SIZE:
			print 'MOVING CLOSER TO ' + str(chosen_o)
			print 'TARGET DISTANCE ' + str(target_distance) + ' > ' + str(obs_dist)
			xs, ys = (self.oarray1 == chosen_o).nonzero()
			pos = np.round(np.array(zip(xs, ys)).mean(0))
			if np.abs(self.SCREEN_WIDTH/2 - pos[1]) < 10:
			    d = 0
			else:
			    d =  -0.1 * np.sign(self.SCREEN_WIDTH/2 - pos[1])
			msg = init_msg()
			msg['msg']['vel'] = [0, 0, .25]
			msg['msg']['ang_vel'] = [0, d, 0]
			msg['msg']['action_type'] = "MOVING_CLOSER"
			self.send_msg(msg)
			self.observe_world()
			#TODO object might not be observed still, somehow! Fix this.
			obs_obj = self.info['observed_objects']
			obs_idx = self.find_in_observed_objects(chosen_o, obs_obj)
			pos3d = np.array(obs_obj[obs_idx][2])
			obs_dist = np.linalg.norm(pos3d - np.array(self.info['avatar_position']))


	def push_object(self, chosen_o, time_len_apply, time_len_wait, const_force):
		chosen_o = chosen_o[1]
		# self.get_to_object(chosen_o)
		t = 0
		objpi = []
		while self.in_batch_counter < self.BATCH_SIZE and t < time_len_apply:
			pos, chosen_id = self.choose_action_position(self.oarray1, chosen_o, slipage = 0)
			xs, ys = (self.oarray1 == chosen_o).nonzero()
			obs_obj = self.info['observed_objects']
			obs_idx = self.find_in_observed_objects(chosen_o, obs_obj)
			chosen_o_name = obs_obj[obs_idx][0]
			print 'Applying force to ' + str(chosen_o_name)
			centroid = np.round(np.array(zip(xs, ys)).mean(0))
			objpi.append(centroid)
			action = {}
			msg = init_msg()
			action['force'] = const_force
			action['torque'] = [0,0,0]
			action['id'] = str(chosen_id)
			action['object'] = str(chosen_o)
			action['action_pos'] = map(float, pos)
			msg['msg']['actions'].append(action)
			msg['msg']['action_type'] = 'PUSHING'
			self.send_msg(msg)
			t += 1
			if self.in_batch_counter < self.BATCH_SIZE and t < time_len_apply:
				self.observe_world()
		self.wait(time_len_wait)

	def apply_action(self, chosen_o, f_sequence, tor_sequence, act_descriptor):
		if not (self.in_batch_counter < self.BATCH_SIZE):
			return
		assert len(f_sequence) == len(tor_sequence)
		for (t, (f, tor)) in enumerate(zip(f_sequence, tor_sequence)):
			pos, chosen_id = self.choose_action_position(self.oarray1, chosen_o[1], slipage = 0)
			xs, ys = (self.oarray1 == chosen_o[1]).nonzero()
			chosen_o_name = chosen_o[0]
			print('Applying force to ' + str(chosen_o_name))
			centroid = np.round(np.array(zip(xs, ys)).mean(0))
			action = {}
			msg = init_msg()
			action['force'] = f
			action['torque'] = tor
			action['id'] = str(chosen_id)
			action['object'] = str(chosen_o[1])
			action['action_pos'] = map(float, pos)
			msg['msg']['actions'].append(action)
			msg['msg']['action_type'] = act_descriptor
			self.send_msg(msg)
			if self.in_batch_counter < self.BATCH_SIZE and t < len(f_sequence) - 1:
				self.observe_world()
			else:
				return








	def make_new_batch(self, bn, sock, path, create_hdf5, use_tdw_msg):
		self.bn, self.sock, self.path, self.create_hdf5, self.use_tdw_msg = bn, sock, path, create_hdf5, use_tdw_msg
		self.in_batch_counter = 0
		while self.in_batch_counter < self.BATCH_SIZE:
			self.observe_world()
			print(self.in_batch_counter)
			if self.in_batch_counter == 0:
				self.init_y_pos = self.info['avatar_position'][1]
			obj = self.select_random_object()
			self.teleport_to_object(obj, distance_from = 2)
			self.observe_world()
			f_seq, tor_seq = make_const_simple_rot(self.rng, 10, magnitude = 100)
			self.apply_action(obj, f_seq, tor_seq, 'ROTATING')
			self.wait(20)
			# for f in [[100, 0, 0], [0, 0, 100]]:
			# 	self.observe_world()
			# 	if self.in_batch_counter == 0:
			# 		self.init_y_pos = self.info['avatar_position'][1]
			# 	obj = self.select_random_object()
			# 	self.teleport_to_object(obj, distance_from = 2)
			# 	self.observe_world()
			# 	f_list = [f for _ in range(3)]
			# 	tor_list = [[0, 0, 0] for _ in range(3)]
			# 	self.apply_action(obj, f_list, tor_list, 'PUSHING')
			# 	self.wait(20)









