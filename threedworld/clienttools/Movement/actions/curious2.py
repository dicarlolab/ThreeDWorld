import numpy as np
import scipy.linalg
import scipy.ndimage.morphology
import json
from curiosity.utils.io import (handle_message,
                                send_array,
                                recv_array)
from PIL import Image
import h5py
import os

def get_view_vec_from_quaternion(quaternion):
	ang = np.arccos(quaternion[0]) * 2
	dir_unnorm = np.array([np.cos(ang), np.sin( - ang)])
	return dir_unnorm / np.linalg.norm(dir_unnorm)


def get_urand_sphere_point(rng, dim, zero_div_safety = .0001):
	while True:
		vec = rng.randn(dim)
		norm = np.linalg.norm(vec)
		if norm > zero_div_safety:
			return vec / norm

def get_trunc_normal(rng, dim, std_dev, truncate_norm):
	if truncate_norm is None:
		truncate_norm = float('inf')
	while True:
		vec = std_dev * rng.randn(dim)
		norm = np.linalg.norm(vec)
		if norm < truncate_norm:
			return vec

def get_valid_num(valid):
    print('getting valid num')
    print(valid.shape)
    for i in range(valid.shape[0]):
        if not valid[i]:
            print(i)
            return i



def init_msg():
	msg = {'n': 7, 'msg': {"msg_type": "CLIENT_INPUT", "get_obj_data": True, "send_scene_info" : True, "actions": []}}
	msg['msg']['vel'] = [0, 0, 0]
	msg['msg']['ang_vel'] = [0, 0, 0]
	return msg

def valid_pos(pos, room_length, room_width, test_height_too = False):
	return pos[0] > 0 and pos[2] > 0 and pos[0] < room_length and pos[2] < room_width and ((not test_height_too) or (pos[1] > 0 and pos[1] < 5.))

def choose_random_wall_spot(rng):
	wall_num = rng.randint(0, 4)
	if wall_num == 0:
		return np.array([0., 0., -1.]), np.array([rng.uniform(0., 19.), 0., 0.])
	if wall_num == 1:
		return np.array([-1., 0., 0.]), np.array([0., 0., rng.uniform(0., 19.)])
	if wall_num == 2:
		return np.array([0., 0., 1.]), np.array([rng.uniform(0., 19.), 0., 19.])
	return np.array([1., 0., 0.]), np.array([19., 0., rng.uniform(0., 19.)])




#reasonable push magnitudes: x_magnitude 100
#reasonable lift magnitudes: y_magnitude 120, x_magnitude 50
#reasonable rotation: magnitude = 100

def make_const_simple_push(rng, time_len = 3, magnitude = 100):
	return make_const_action_sequences(rng, time_len, f_horiz = magnitude)

def make_const_simple_lift(rng, time_len = 3, x_magnitude = 50, y_magnitude = 120):
	return make_const_action_sequences(rng, time_len, f_horiz = x_magnitude, f_y = y_magnitude)

def make_const_simple_rot(rng, time_len = 3, magnitude = 100):
	return make_const_action_sequences(rng, time_len, tor_y = magnitude)

def make_const_action_sequences(rng, time_len = 3, f_horiz = 0, f_y = 0, tor_horiz = 0, tor_y = 0):
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

def make_const_action_sequences_distinguished_direction(rng, distinct_dir, std_dev_ang = np.pi / 6, time_len = 3, f_horiz = 0, f_y = 0, tor_horiz = 0, tor_y = 0):
	horiz_angle = std_dev_ang * rng.randn()
	horiz_f_sign = 2 * rng.randint(0, 2) - 1
	assert len(distinct_dir) == 2
	distinct_dir_normalized = distinct_dir / np.linalg.norm(distinct_dir)
	distinct_dir_perp = np.array([- distinct_dir_normalized[1], distinct_dir_normalized[0]])
	f_dir = np.cos(horiz_angle) * distinct_dir_perp + np.sin(horiz_angle) * distinct_dir_normalized
	f_dir = horiz_f_sign * f_dir
	f = [f_horiz * f_dir[0], f_y, f_horiz * f_dir[1]]
	while True:
		tor_dir = rng.randn(2)
		tor_norm = np.linalg.norm(tor_dir)
		tor_y_sign = 2 * rng.randint(0, 2) - 1
		if tor_norm > .0001:
			tor_dir = tor_dir / tor_norm
			tor = [tor_horiz * tor_dir[0], tor_y_sign * tor_y, tor_horiz * tor_dir[1]]
			break
	return [f for _ in range(time_len)], [tor for _ in range(time_len)]

def controlled_constant_action_sequences_distinguished_direction(rng, distinct_dir, std_dev_ang = np.pi / 6, time_len_range = [5], f_horiz_range = [0], f_y_range = [0], tor_horiz_range = [0], tor_y_range = [0]):
	select_args = [('time_len', time_len_range), ('f_horiz', f_horiz_range), ('f_y', f_y_range), ('tor_horiz', tor_horiz_range), ('tor_y', tor_y_range)]
	magnitudes = dict((desc, my_range[rng.randint(len(my_range))]) for (desc, my_range) in select_args)
	f_horiz = magnitudes['f_horiz']
	f_y = magnitudes['f_y']
	tor_horiz = magnitudes['tor_horiz']
	tor_y = magnitudes['tor_y']
	time_len = magnitudes['time_len']
	assert len(distinct_dir) == 3
	distinct_dir_normalized = distinct_dir / np.linalg.norm(distinct_dir)
	distinct_dir_perp = np.array([distinct_dir_normalized[2], 0, - distinct_dir_normalized[0]])
	horiz_angle = std_dev_ang * rng.randn()
	#opposite of the above because we are specifying the direction the force should bias towards, not the viewing direction
	f_dir = np.cos(horiz_angle) * distinct_dir_normalized + np.sin(horiz_angle) * distinct_dir_perp
	f = [magnitudes['f_horiz'] * f_dir[0], f_y, magnitudes['f_horiz'] * f_dir[2]]
	while True:
		tor_dir = rng.randn(2)
		tor_norm = np.linalg.norm(tor_dir)
		tor_y_sign = 2 * rng.randint(0, 2) - 1
		if tor_norm > .0001:
			tor_dir = tor_dir / tor_norm
			tor = [tor_horiz * tor_dir[0], tor_y_sign * tor_y, tor_horiz * tor_dir[1]]
			break
	return [f for _ in range(time_len)], [tor for _ in range(time_len)]



def make_constant_random_action_sequence(rng, distinct_dir = None, std_dev_ang = np.pi / 6, time_len_range = [3], f_horiz_range = [0], f_y_range = [0], tor_horiz_range = [0], tor_y_range = [0]):
	select_args = [('time_len', time_len_range), ('f_horiz', f_horiz_range), ('f_y', f_y_range), ('tor_horiz', tor_horiz_range), ('tor_y', tor_y_range)]
	my_kwargs = dict((desc, my_range[rng.randint(len(my_range))]) for (desc, my_range) in select_args)
	if distinct_dir is None:
		return make_const_action_sequences(rng,**my_kwargs)
	return make_const_action_sequences_distinguished_direction(rng, distinct_dir, std_dev_ang = std_dev_ang, **my_kwargs)


class agent:
	global_counter = 0

	WRITE_FILES = False

        SCREEN_WIDTH = 600 #512
        SCREEN_HEIGHT = 256 #384

	BATCH_SIZE = 256
	MULTSTART = -1

	achoice = [-5, 0, 5]
	ACTION_LENGTH = 15
	ACTION_WAIT = 15

	N = 70 * 256 * 14
	valid = np.zeros((N,1)) 

	rng = np.random.RandomState(0)
        use_stabilization = True

        def __init__(self, CREATE_HDF5, path='', dataset_num=-1, continue_writing = False):
            if(CREATE_HDF5):
                self.open_hdf5(path, dataset_num)
                self.continue_writing = continue_writing


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
            images2 = self.hdf5.require_dataset('images2', shape = (self.N, self.SCREEN_HEIGHT, self.SCREEN_WIDTH, 3), dtype = np.uint8)
            normals2 = self.hdf5.require_dataset('normals2', shape = (self.N, self.SCREEN_HEIGHT, self.SCREEN_WIDTH, 3), dtype = np.uint8)
            objects2 = self.hdf5.require_dataset('objects2', shape = (self.N, self.SCREEN_HEIGHT, self.SCREEN_WIDTH, 3), dtype = np.uint8)
            return [valid, images, normals, objects, worldinfos, agentactions, images2, normals2, objects2]

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
	    return None

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
		msg['msg']['action_type'] = self.desc_prefix + ':' + msg['msg']['action_type']
		if(not 'action_type' in msg['msg']):
			print("ERROR! Action not recognized")
		else:
			print(msg['msg']['action_type'])
		self.in_batch_counter += 1
		self.global_counter += 1
		print('counter ' + str(self.in_batch_counter))
		if self.use_tdw_msg:
			self.sock.send_json(msg)
		else:
			self.sock.send_json(msg['msg'])
		if self.create_hdf5:
			self.infolist.append(json.dumps(msg['msg']))
			self.ims.append(self.imarray)
			self.norms.append(self.narray)
			self.infs.append(json.dumps(self.info))
			self.objs.append(self.oarray)
                        self.ims2.append(self.imarray2)
                        self.norms2.append(self.narray2)
                        self.objs2.append(self.oarray2)



	def teleport_random(self):
		msg = init_msg()
		msg['msg']['teleport_random'] = True
		msg['msg']['action_type'] = 'TELEPORT'
		print('TELEPORT')
		self.init_y_pos = self.info['avatar_position'][1]
		self.send_msg(msg)

	def observe_world(self, * objects_to_track):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return False, [None for _ in objects_to_track]
		counter_str = str(self.global_counter)
		while len(counter_str) < 4:
			counter_str  = '0' + counter_str
                print 'about to handle message'
		info, self.narray, self.oarray, self.imarray, self.narray2, self.oarray2, self.imarray2 = handle_message(self.sock, write=self.WRITE_FILES, outdir=self.temp_im_path, prefix=counter_str)
                print 'message handled'
                self.info = json.loads(info)
		self.oarray1 = 256**2 * self.oarray[:, :, 0] + 256 * self.oarray[:, :, 1] + self.oarray[:, :, 2]
		if self.global_counter == 0:
			self.init_y_pos = self.info['avatar_position'][1]
			print('init y pos set: ' + str(self.init_y_pos))		
		# if self.global_counter > 9999:
		# 	raise Exception('Did not mean to make a movie that long!')
		# pic_filename = 'pic' + counter_str + '.png'
		# pic_filename = os.path.join(self.temp_im_path, pic_filename)
		# im = Image.fromarray(self.imarray)
		# im.save(pic_filename)
		return True, [self.update_object(obj) for obj in objects_to_track]




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

	def select_random_object(self, not_table = True):
		valid_objects = [o for o in self.info['observed_objects'] if not o[4] and int(o[1]) != -1 and (not not_table or not o[5])]
		if not len(valid_objects):
			return None
		for i in range(5000):
			obj = valid_objects[self.rng.randint(len(valid_objects))]
			if valid_pos(obj[2], 19., 19., test_height_too = True):
				return obj
		return None

	# def teleport_to_object(self, chosen_o, distance_from = 2):
	# 	#TODO implement random position around object
	# 	#TODO implement looking-from-above
	# 	if self.in_batch_counter >= self.BATCH_SIZE:
	# 		return
	# 	pos = chosen_o[2]
	# 	if pos[0] < distance_from:
	# 		print('exception spot')
	# 		target_pos = [pos[0] + distance_from, self.init_y_pos, pos[2]]
	# 		target_rot = [-1, 0, 0]
	# 	else:
	# 		target_pos = [pos[0] - distance_from, self.init_y_pos, pos[2]]
	# 		target_rot = [1, 0, 0]
	# 	msg = init_msg()
	# 	msg['msg']['teleport_to'] = {'position' : target_pos, 'rotation' : target_rot}
	# 	msg['msg']['action_type'] = 'TELE_TO_OBJ'
	# 	self.send_msg(msg)

	def teleport_to_object(self, chosen_o, distance_from = 2):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return
		pos = [chosen_o[2][0], 0., chosen_o[2][2]]
		while True:
			rand_horiz = self.rng.randn(2)
			rand_yangle = self.rng.uniform(0, np.pi / 3)
			norm = np.linalg.norm(rand_horiz)
			if norm < .0001:
				continue
			rand_normalized = np.array([rand_horiz[0] * distance_from * np.cos(rand_yangle)/ norm, distance_from * np.sin(rand_yangle), rand_horiz[1] * distance_from * np.cos(rand_yangle) / norm])
			tgt_pos = list(np.array(pos) + rand_normalized)
			#right now making sure the angle is between 0 and 60
			if valid_pos(tgt_pos, 19., 19., test_height_too = True):
				break
		tgt_rot = list(- rand_normalized)
		msg = init_msg()
		msg['msg']['teleport_to'] = {'position' : tgt_pos, 'rotation' : tgt_rot}
		msg['msg']['action_type'] = 'TELE_TO_OBJ'
		self.send_msg(msg)
		return  - rand_normalized[[0,2]]


	def teleport_object(self, obj, pos):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return	
		msg = init_msg()
		msg['msg']['action_type'] = 'TELE_OBJ'
		action = {}
		action['use_absolute_coordinates'] = True
		action['id'] = str(obj[1])
		action['teleport_to'] = {'position' : pos, 'rotation' : [0, 0, 0]}
		msg['msg']['actions'].append(action)
		self.send_msg(msg)

	def teleport_to_wall(self, obj, distance_from = 1., obj_dist_from = .5):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return
		while True:
			wall_dir, wall_pos = choose_random_wall_spot(self.rng)
			wall_dir_perp = np.array([wall_dir[2], 0, - wall_dir[0]])
			# looking_ang = self.rng.uniform(-np.pi / 6, np.pi / 6)
			looking_ang = 0.
			looking_dir = np.cos(looking_ang) * wall_dir + np.sin(looking_ang) * wall_dir_perp
			tele_pos = wall_pos - distance_from * looking_dir
			tele_pos[1] = .01
			if valid_pos(tele_pos, 19., 19., test_height_too = True):
				break
		msg = init_msg()
		tele_rot = looking_dir
		msg['msg']['teleport_to'] = {'position' : list(tele_pos), 'rotation' : list(tele_rot)}
		msg['msg']['action_type'] = 'CONTROLLED_STACK_TELE'
		obj_tele_pos = tele_pos + obj_dist_from * looking_dir
		init_rot = list(get_urand_sphere_point(self.rng, 3))
		action = {}
		action['use_absolute_coordinates'] = True
		action['id'] = str(obj[1])
		action['teleport_to'] = {'position' : list(obj_tele_pos), 'rotation' : list(init_rot)}
		msg['msg']['actions'].append(action)
		self.send_msg(msg)
		return wall_dir


	def teleport_for_collision(self, big_object, little_object, distance_scale = 1):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return
		big_pos = big_object[2]
		big_pos_floor = np.array([big_pos[0], 0, big_pos[2]])
		height_vec = np.array([0., little_object[6][1], 0.])
		for t in range(5000):
			action_dir = get_urand_sphere_point(self.rng, 2, zero_div_safety = .0001)
			action_dir = np.array([action_dir[0], 0, action_dir[1]])
			little_pos = big_pos_floor - distance_scale * action_dir + height_vec
			orientation_sign = 2 * self.rng.randint(0, 2) - 1
			action_dir_perp = orientation_sign * np.array([action_dir[2], 0., - action_dir[0]])
			view_yang = self.rng.uniform(0, np.pi / 3)
			ava_diff = np.cos(view_yang) * action_dir_perp + np.sin(view_yang) * np.array([0., 1., 0.])
			avatar_pos = big_pos_floor - (distance_scale / 2.) * action_dir + ava_diff
			if valid_pos(little_pos, 19., 19., test_height_too = True) and valid_pos(avatar_pos, 19., 19., test_height_too = True):
				msg = init_msg()
				msg['msg']['teleport_to'] = {'position' : list(avatar_pos), 'rotation' : list(- ava_diff)}
				msg['msg']['action_type'] = 'COLLIDE_TELE'
				init_rot = list(get_urand_sphere_point(self.rng, 3))
				action = {}
				action['id'] = str(little_object[1])
				action['use_absolute_coordinates'] = True
				action['teleport_to'] = {'position' : list(little_pos), 'rotation' : list(init_rot)}
				msg['msg']['actions'].append(action)
				self.send_msg(msg)
				return action_dir
		return



	def controlled_teleport_on_top_of(self, under_object, over_object, height_above = None, distance_from = 2, off_center_camera_magnitude = .6, off_center_object_magnitude = .2, random_init_rot = True, drop = True):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return
		under_pos = under_object[2]
		under_extents = under_object[6]
		under_center = under_object[7]
		under_rot = under_object[3]
		view_dir = get_view_vec_from_quaternion(under_rot)
		view_yang = self.rng.uniform(0, np.pi / 3)
		view_3d = np.array([view_dir[0] * distance_from * np.cos(view_yang), distance_from * np.sin(view_yang), view_dir[1] * distance_from * np.cos(view_yang)])
		center_rot = np.array([under_center[0] * view_dir[0], under_center[1], under_center[2] * view_dir[1]])
		tabletop_pos = np.array(under_pos) + np.array(under_center) + np.array([0., under_extents[1], 0.])
		horiz_sign = 2 * self.rng.randint(0, 2) - 1
		view_horiz_perp = horiz_sign * np.array([view_dir[1], 0, -view_dir[0]])
		tgt_pos = list(tabletop_pos + view_3d + off_center_camera_magnitude * view_horiz_perp)
		tgt_rot = list(- view_3d)
		msg = init_msg()
		msg['msg']['teleport_to'] = {'position' : tgt_pos, 'rotation' : tgt_rot}
		msg['msg']['action_type'] = 'CONTROLLED_STACK_TELE'
		if height_above is None:
			if drop:		
				height_above = over_object[6][1] + .5
			else:
				height_above = .01 - tabletop_pos[1]
		tgt_obj_pos = list(tabletop_pos + off_center_object_magnitude * view_horiz_perp + np.array([0., height_above, 0.]))
		action = {}
		action['id'] = str(over_object[1])
		if random_init_rot:
			init_rot = list(get_urand_sphere_point(self.rng, 3))
		else:
			init_rot = [0,0,0]
		action['use_absolute_coordinates'] = True
		action['teleport_to'] = {'position' : tgt_obj_pos, 'rotation' : init_rot}
		msg['msg']['actions'].append(action)
		self.send_msg(msg)
		return view_horiz_perp

	def teleport_on_top_of(self, under_object, over_object, height_above = None, distance_from = 2, random_init_rot = False, noisy_drop_std_dev = None, noisy_drop_trunc = None, drop = True):
		if self.in_batch_counter >= self.BATCH_SIZE:
			return		
		under_pos = under_object[2]
		under_extents = under_object[6]
		under_center = under_object[7]
		tabletop_pos = np.array(under_pos) + np.array(under_center) + np.array([0., under_extents[1], 0.])

		if height_above is None:
			height_above = over_object[6][1] + .5
		if noisy_drop_std_dev is not None:
			drop_translate = list(get_trunc_normal(self.rng, 2, noisy_drop_std_dev, noisy_drop_trunc))
			print 'noisy drop: ' + str(drop_translate)
		else:
			drop_translate = [0, 0]
		if drop:
			y_level = height_above + tabletop_pos[1]
		else:
			print 'not dropping!'
			y_level = .01
		tgt_obj_pos = [under_pos[0] + drop_translate[0], y_level, under_pos[2] + drop_translate[1]]
		# tabletop_pos = [under_pos[0], under_pos[1] + under_extents[1] + under_center[1], under_pos[2]]
		# if self.init_y_pos > distance_from + tabletop_pos[1]:
		# 	min_y_pos = 0
		# else:
		# 	min_y_pos = self.init_y_pos
		while True:
			rand_horiz = self.rng.randn(2)
			rand_yangle = self.rng.uniform(0, np.pi / 3)
			norm = np.linalg.norm(rand_horiz)
			if norm < .0001:
				continue
			rand_normalized = np.array([rand_horiz[0] * distance_from * np.cos(rand_yangle)/ norm, distance_from * np.sin(rand_yangle), rand_horiz[1] * distance_from * np.cos(rand_yangle) / norm])
			tgt_pos = list(np.array(tabletop_pos) + rand_normalized)
			#right now making sure the angle is between 0 and 60
			if valid_pos(tgt_pos, 19., 19.):
				break
		print('random y angle: ' + str(rand_yangle))
		tgt_rot = list(- rand_normalized)
		msg = init_msg()
		msg['msg']['teleport_to'] = {'position' : tgt_pos, 'rotation' : tgt_rot}
		msg['msg']['action_type'] = 'STACK_TELE'
		action = {}
		action['id'] = str(over_object[1])
		if random_init_rot:
			init_rot = list(get_urand_sphere_point(self.rng, 3))
		else:
			init_rot = [0,0,0]
		# print('init rot' + str(init_rot))
		action['use_absolute_coordinates'] = True
		action['teleport_to'] = {'position' : tgt_obj_pos, 'rotation' : init_rot}
		print 'teleport action info'
		print action
		msg['msg']['actions'].append(action)
		self.send_msg(msg)
		return - rand_normalized[[0,2]]



	def wait(self, waiting_time, desc = 'WAITING'):
		t = 0
		while self.in_batch_counter < self.BATCH_SIZE and t < waiting_time:
			self.observe_world()
			msg = init_msg()
			msg['msg']['action_type'] = desc
			self.send_msg(msg)
			t += 1

	def wait_until_stops(self, obj_of_interest, threshold, time_window, max_time, desc = 'WAITING', cut_if_off_screen = None):
		window = [float('inf')] * time_window
		old_pos = np.array(obj_of_interest[2])
		old_rot = np.array(obj_of_interest[3])
		num_consecutive_off_screen = 0
		for t in range(max_time):
		 	if self.in_batch_counter >= self.BATCH_SIZE:
		 		return
		 	did_update, obj_updated_list = self.observe_world(obj_of_interest)
		 	obj_updated = obj_updated_list[0]
		 	if not did_update:
		 		return
		 	if obj_updated is None:
				window[t % time_window] = 0.0
			elif cut_if_off_screen is not None and len((self.oarray1 == obj_updated[1]).nonzero()[0]) == 0:
				num_consecutive_off_screen += 1
				if num_consecutive_off_screen >= cut_if_off_screen:
					msg = init_msg()
					msg['msg']['action_type'] = desc
					self.send_msg(msg)
					return
			else:
				obj_of_interest = obj_updated
				pos = np.array(obj_of_interest[2])
				rot = np.array(obj_of_interest[3])
				window[t % time_window] = np.linalg.norm(pos - old_pos) + np.linalg.norm(rot - old_rot)
				old_pos = pos
				old_rot = rot
			msg = init_msg()
			msg['msg']['action_type'] = desc
			self.send_msg(msg)
			if sum(window) < threshold:
				return



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
			action['use_absolute_coordinates'] = True
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

	def apply_action(self, chosen_o, f_sequence, tor_sequence, act_descriptor, cut_if_off_screen = None):
		if not (self.in_batch_counter < self.BATCH_SIZE):
			return
		num_object_gone = 0
		assert len(f_sequence) == len(tor_sequence)
		for (t, (f, tor)) in enumerate(zip(f_sequence, tor_sequence)):
			pos, chosen_id = self.choose_action_position(self.oarray1, chosen_o[1], slipage = 0)
			xs, ys = (self.oarray1 == chosen_o[1]).nonzero()
			if len(xs) == 0:
				num_object_gone += 1
			else:
				num_object_gone = 0
			chosen_o_name = chosen_o[0]
			print('Applying force to ' + str(chosen_o_name))
			centroid = np.round(np.array(zip(xs, ys)).mean(0))
			action = {}
			msg = init_msg()
			action['use_absolute_coordinates'] = True
			action['force'] = f
			action['torque'] = tor
			action['id'] = str(chosen_id)
			action['object'] = str(chosen_o[1])
			action['action_pos'] = map(float, pos)
			msg['msg']['actions'].append(action)
			msg['msg']['action_type'] = act_descriptor
			self.send_msg(msg)
			if self.in_batch_counter < self.BATCH_SIZE and t < len(f_sequence) - 1 and ((cut_if_off_screen is None) or (num_object_gone < cut_if_off_screen)):
				self.observe_world()
			elif self.in_batch_counter >= self.BATCH_SIZE or ((cut_if_off_screen is not None) and (num_object_gone >= cut_if_off_screen)):
				return


	def get_tables_and_not_tables_lists(self):
		tables =  [o for o in self.info['observed_objects'] if o[5] and int(o[1]) != -1 and not o[4]]
		not_tables = [o for o in self.info['observed_objects'] if not o[5] and int(o[1]) != -1 and not o[4]]
		print 'table and not table lengths'
		print (len(tables), len(not_tables))
		return tables, not_tables

	def select_random_table_not_table(self):
		tables, not_tables = self.get_tables_and_not_tables_lists()
		if (not len(tables)) or (not len(not_tables)):
			return None, None
		for t in range(5000):
			table = tables[self.rng.randint(len(tables))]
			if valid_pos(table[2], 19., 19., test_height_too = True):
				break
		for t in range(5000):
			not_table = not_tables[self.rng.randint(len(not_tables))]
			if valid_pos(not_table[2], 19., 19., test_height_too = True):
				return table, not_table
		return None, None

	def update_object(self, old_obj):
		'''
		When observe_world has been called, and there's an object from a previous frame that you want up-to-date info on. Returns None if the
		object is not in observed_objects.
		'''
		obs_obj = self.info['observed_objects']
		obs_idx = self.find_in_observed_objects(old_obj[1], obs_obj)
		if obs_idx is None:
			return None
		else:
			return obs_obj[obs_idx]

	def do_one_object_task(self, act_desc, act_params):
		self.observe_world()
		if self.in_batch_counter == 0:
			self.init_y_pos = self.info['avatar_position'][1]
		obj = self.select_random_object()
		if obj is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		looking_dir = self.teleport_to_object(obj, distance_from = 1)
		did_update, obj_list = self.observe_world(obj)
		if not did_update:
			return
		obj = obj_list[0]
		if obj is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		if 'std_dev_ang' in act_params['kwargs']:
			f_seq, tor_seq = act_params['func'](self.rng, distinct_dir = looking_dir, **act_params['kwargs'])
		else:
			f_seq, tor_seq = act_params['func'](self.rng, **act_params['kwargs'])
		self.apply_action(obj, f_seq, tor_seq, act_desc, cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if 'wait' in act_params:
			# self.wait(act_params['wait'])
			#technically should update the object, but not sure this is so important right now
			self.wait_until_stops(obj, act_params['wait']['threshold'], act_params['wait']['time_window'], act_params['wait']['max_time'], cut_if_off_screen = act_params.get('cut_if_off_screen'))

	def do_wall_throw(self, act_desc, act_params, clean_up_after = True):
		self.observe_world()
		obj = self.select_random_object()
		old_pos = obj[2]
		if obj is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		wall_dir = self.teleport_to_wall(obj, distance_from = 1, obj_dist_from = .5)
		if 'wait_before' in act_params:
			self.wait_until_stops(obj, act_params['wait_before']['threshold'], act_params['wait_before']['time_window'], act_params['wait_before']['max_time'], desc = 'DROPPING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		did_update, obj_after_teleport_list = self.observe_world(obj)
		if not did_update:
			return
		obj_after_teleport = obj_after_teleport_list[0]
		if obj_after_teleport is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		f_seq, tor_seq = act_params['func'](self.rng, distinct_dir = wall_dir, ** act_params['kwargs'])
		self.apply_action(obj_after_teleport, f_seq, tor_seq, act_desc, cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if 'wait_after' in act_params:
			self.wait_until_stops(obj_after_teleport, act_params['wait_after']['threshold'], act_params['wait_after']['time_window'], act_params['wait_after']['max_time'], desc = 'WAITING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if clean_up_after:
			did_update, obj_after_act_list = self.observe_world(obj)
			if not did_update:
				return
			obj_after_act = obj_after_act_list[0]
			if obj_after_act is None:
				msg = init_msg()
				msg['msg']['action_type'] = 'WAITING'
				self.send_msg(msg)
				return
			self.teleport_object(obj_after_act, old_pos)

	def do_throw_at_object(self, act_desc, act_params):
		self.observe_world()
		big_obj, little_obj = self.select_random_table_not_table()
		if little_obj is None or big_obj is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		horiz_action_dir = self.teleport_for_collision(big_obj, little_obj, distance_scale = 1)
		if horiz_action_dir is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		if 'wait_before' in act_params:
			wait_max_time = act_params['wait_before'].get('no_drop_max_time', 3)
			self.wait_until_stops(little_obj, act_params['wait_before']['threshold'], act_params['wait_before']['time_window'], wait_max_time, desc = 'DROPPING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		did_update, obj_after_teleport_list = self.observe_world(little_obj)
		if not did_update:
			return
		little_obj_after_teleport = obj_after_teleport_list[0]
		if little_obj_after_teleport is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		f_seq, tor_seq = act_params['func'](self.rng, horiz_action_dir, **act_params['kwargs'])
		self.apply_action(little_obj_after_teleport, f_seq, tor_seq, act_desc, cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if 'wait_after' in act_params:
			self.wait_until_stops(little_obj_after_teleport, act_params['wait_after']['threshold'], act_params['wait_after']['time_window'], act_params['wait_after']['max_time'], desc = 'WAITING', cut_if_off_screen = act_params.get('cut_if_off_screen'))


	def do_controlled_table_task(self, act_desc, act_params, clean_up_table = True, drop = True):
		self.observe_world()
		under_obj, obj = self.select_random_table_not_table()
		if under_obj is None or obj is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		old_pos = obj[2]
		horiz_action_dir = self.controlled_teleport_on_top_of(under_obj, obj, height_above = None, distance_from = 1, drop = drop)
		if 'wait_before' in act_params:
			if drop:
				wait_max_time = act_params['wait_before']['max_time']
			else:
				wait_max_time = act_params['wait_before'].get('no_drop_max_time', 3)
			self.wait_until_stops(obj, act_params['wait_before']['threshold'], act_params['wait_before']['time_window'], wait_max_time, desc = 'DROPPING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		did_update, obj_after_teleport_list = self.observe_world(obj)
		if not did_update:
			return
		obj_after_teleport = obj_after_teleport_list[0]
		if obj_after_teleport is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		f_seq, tor_seq = act_params['func'](self.rng, horiz_action_dir, **act_params['kwargs'])
		self.apply_action(obj_after_teleport, f_seq, tor_seq, act_desc, cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if 'wait_after' in act_params:
			self.wait_until_stops(obj_after_teleport, act_params['wait_after']['threshold'], act_params['wait_after']['time_window'], act_params['wait_after']['max_time'], desc = 'WAITING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if clean_up_table:
			did_update, obj_after_act_list = self.observe_world(obj)
			if not did_update:
				return
			obj_after_act = obj_after_act_list[0]
			if obj_after_act is None:
				msg = init_msg()
				msg['msg']['action_type'] = 'WAITING'
				self.send_msg(msg)
				return
			self.teleport_object(obj_after_act, old_pos)


	def do_table_drop_task(self, act_desc, act_params, clean_up_table = True, drop = True):
		self.observe_world()
		under_obj, obj = self.select_random_table_not_table()
		if under_obj is None or obj is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		old_pos = obj[2]
		looking_dir = self.teleport_on_top_of(under_obj, obj, height_above = None, distance_from = 1, random_init_rot = act_params.get('random_init_rot'), noisy_drop_std_dev = act_params.get('noisy_drop_std_dev'), noisy_drop_trunc = act_params.get('noisy_drop_trunc'), drop = drop)
		if 'wait_before' in act_params:
			if drop:
				wait_max_time = act_params['wait_before']['max_time']
			else:
				wait_max_time = act_params['wait_before'].get('no_drop_max_time', 3)
			#again, object is not immediately updated, but should not matter in this case.
			self.wait_until_stops(obj, act_params['wait_before']['threshold'], act_params['wait_before']['time_window'], wait_max_time, desc = 'DROPPING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		did_update, obj_after_teleport_list = self.observe_world(obj)
		if not did_update:
			return
		obj_after_teleport = obj_after_teleport_list[0]
		if obj_after_teleport is None:
			msg = init_msg()
			msg['msg']['action_type'] = 'WAITING'
			self.send_msg(msg)
			return
		if 'std_dev_ang' in act_params['kwargs']:
			f_seq, tor_seq = act_params['func'](self.rng, distinct_dir = looking_dir, **act_params['kwargs'])
		else:
			f_seq, tor_seq = act_params['func'](self.rng, **act_params['kwargs'])
		self.apply_action(obj_after_teleport, f_seq, tor_seq, act_desc, cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if 'wait_after' in act_params:
			self.wait_until_stops(obj_after_teleport, act_params['wait_after']['threshold'], act_params['wait_after']['time_window'], act_params['wait_after']['max_time'], desc = 'WAITING', cut_if_off_screen = act_params.get('cut_if_off_screen'))
		if clean_up_table:
			did_update, obj_after_act_list = self.observe_world(obj)
			if not did_update:
				return
			obj_after_act = obj_after_act_list[0]
			if obj_after_act is None:
				msg = init_msg()
				msg['msg']['action_type'] = 'WAITING'
				self.send_msg(msg)
				return
			self.teleport_object(obj_after_act, old_pos)



	def make_new_batch(self, bn, sock, path, create_hdf5, use_tdw_msg, task_params, descriptor_prefix, scene_start = False):
                print('Batch num: ' + str(bn))
                self.bn, self.sock, self.path, self.create_hdf5, self.use_tdw_msg, self.desc_prefix = bn, sock, path, create_hdf5, use_tdw_msg, descriptor_prefix
		self.in_batch_counter = 0
		if self.WRITE_FILES:
			self.temp_im_path = os.path.join(self.path, 'object_throw_test')
			if not os.path.exists(self.temp_im_path):
				os.mkdir(self.temp_im_path)
		else:
			self.temp_im_path = None
		if self.create_hdf5:
                        print 'creating instances for save'
			self.ims = []
			self.objs = []
			self.norms = []
                        self.ims2 = []
                        self.objs2 = []
                        self.norms2 = []
			self.infolist = []
			self.infs = []
			self.valid, images, normals, objects, worldinfos, agentactions, images2, normals2, objects2 = self.get_hdf5_handles()
                        if self.valid[self.BATCH_SIZE * bn : self.BATCH_SIZE * (bn + 1)].all():
                            print('Skipping batch')
                            return
                mode, act_desc, act_params = task_params[0]
		if mode == 'PUSH_OFF_TABLE' or mode == 'CONTROLLED_TABLE_TASK':
			drop = (self.rng.rand() < .5)
			if drop:
				self.desc_prefix = self.desc_prefix + ':DROP'
				print 'dropping!'
			else:
				self.desc_prefix = self.desc_prefix + ':NODROP'
				print 'not dropping!'
                if scene_start:
                    self.wait(10)		
		while self.in_batch_counter < self.BATCH_SIZE:
			print(self.in_batch_counter)
			for (mode, act_desc, act_params) in task_params:
				if mode == 'SINGLE_OBJECT':
					self.do_one_object_task(act_desc, act_params)
				elif mode == 'PUSH_OFF_TABLE':
					self.do_table_drop_task(act_desc, act_params, clean_up_table = False, drop = drop)
				elif mode == 'CONTROLLED_TABLE_TASK':
					self.do_controlled_table_task(act_desc, act_params, clean_up_table = False, drop = drop)
				elif mode == 'WALL_THROW':
					self.do_wall_throw(act_desc, act_params, clean_up_after = False)
				elif mode == 'COLLISION':
					self.do_throw_at_object(act_desc, act_params)
				else:
					raise Exception('Batch mode not implemented')
		if self.create_hdf5:
                        print 'prepping for hdf5 write'
			start = self.BATCH_SIZE * bn
			end = self.BATCH_SIZE * (bn + 1)
			self.ims = np.array(self.ims)
			self.norms = np.array(self.norms)
			self.objs = np.array(self.objs)
                        self.ims2 = np.array(self.ims2)
                        self.norms2 = np.array(self.norms2)
                        self.objs2 = np.array(self.objs2)
			images[start: end] = self.ims
			normals[start: end] = self.norms
			objects[start: end] = self.objs
                        images2[start:end] = self.ims2
                        normals2[start:end] = self.norms2
                        objects2[start:end] = self.objs2
			self.valid[start: end] = True
			worldinfos[start: end] = self.infs
			agentactions[start: end] = self.infolist
                        print 'flushing'
			self.hdf5.flush()
                        print 'flushed'




test_task_params = [
	('PUSHING', {'func' : make_const_simple_push, 'kwargs' : {'time_len' : 3, 'magnitude' : 100}, 'wait' : 20}),
	('LIFTING', {'func' : make_const_simple_lift, 'kwargs' : {'time_len' : 3, 'x_magnitude' : 50, 'y_magnitude' : 120}, 'wait' : 20})
]







