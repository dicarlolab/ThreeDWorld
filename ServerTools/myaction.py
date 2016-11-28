import zmq
import time
import os
import socket
import multiprocessing
import sys
import numpy as np
import math
#import h5py
import json
from PIL import Image
from StringIO import StringIO

from curiosity.utils.io import (handle_message,
				send_array,
				recv_array)

SCREEN_WIDTH = 640


BATCH_SIZE = 256
MULTSTART = -1

achoice = [-5, 0, 5]
ACTION_LENGTH = 15
ACTION_WAIT = 15

N = 1024000

#path = 'C:/Users/mrowca/Documents/test'
path = '/Users/damian/Desktop/images'
#infodir = path + '_info'
#if not os.path.exists(infodir):
#    os.makedirs(infodir)

#file = h5py.File(path, mode='a')
#valid = file.require_dataset('valid', shape=(N,), dtype=np.bool)
#images = file.require_dataset('images', shape=(N, 256, 256, 3), dtype=np.uint8)
#normals = file.require_dataset('normals', shape=(N, 256, 256, 3), dtype=np.uint8)
#objects = file.require_dataset('objects', shape=(N, 256, 256, 3), dtype=np.uint8)



rng = np.random.RandomState(0)

def choose(x):
  return x[rng.randint(len(x))]

def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

# bn integer
def make_new_batch(bn):
    # how many frames of action its allowed to take
    action_length = ACTION_LENGTH #(bsize - i) / 3
    # how long it waits after action end
    action_wait = ACTION_WAIT

    bsize = BATCH_SIZE
    start = BATCH_SIZE * bn
    end = BATCH_SIZE * (bn + 1)

    print("Getting new %d-%d" % (start, end))
    ims = []
    objs = []
    norms = []
    infolist = []
    
    chosen = False
    chosen_obj = -1
    chosen_frac = 0
    teleport_counter = 0
    move = 0
    for i in range(bsize):
        #print(i)
        info, narray, oarray, imarray = handle_message(sock,
                                                       write=True,
                                                       outdir=path, prefix=str(bn) + '_' + str(i))
        msg = {'n': 4,
               'msg': {"msg_type": "CLIENT_INPUT",
                       "get_obj_data": False,
                       "actions": []}}
        oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2] 
        


	#move forward for two steps
	if(move < 10):
	    move = move + 1
            msg['msg']['vel'] = [0, 0, .25]

	#turn 
	if(move >= 10 and move < 15):
	    move = move + 1
            msg['msg']['ang_vel'] = [0, .3, 0]  

	if(move >= 15):
            move = 0
	print move
	# choose closest object
	#if (chosen == False):
	#    print 'I am choosing an object'
	#    oarray1 = 256**2 * oarray[:, :, 0] + 256 * oarray[:, :, 1] + oarray[:, :, 2]
        #    obs = np.unique(oarray1)
        #    obs = obs[obs > 20]
        #    if (len(obs) > 0):
        #        fracs = []
        #        for o in obs:
        #            frac = (oarray1 == o).sum() / float(np.prod(oarray.shape))
        #            fracs.append(frac)
        #        max_id = np.argmax(fracs)
	#	chosen_obj = obs[max_id]
	#	chosen_frac = fracs[max_id]
	#	chosen = True
	#    else:
	#	# turn if no object found
	#	msg['msg']['ang_vel'] = [0, .3, 0]	

	# move towards closest object
        #if (chosen == True and chosen_frac < 0.005):
	#    print 'I am moving closer to object ' + str(chosen_obj)
        #    xs, ys = (oarray1 == chosen_obj).nonzero()
        #    pos = np.round(np.array(zip(xs, ys)).mean(0))
	#    print pos	    
	#    
        #    # lost object
	#    if len(pos) == 1:
	#	if math.isnan(pos):
        #            chosen = False
        #            continue	
	#
        #    if np.abs(SCREEN_WIDTH / 2 - pos[1]) < 30:
	#        d = 0
	#    else:
        #        d =  -0.1 * np.sign(SCREEN_WIDTH / 2 - pos[1])
	#    msg['msg']['vel'] = [0, 0, .25]
        #    msg['msg']['ang_vel'] = [0, d, 0]	

	# move object
	#if (chosen == True and chosen_frac > 0.005):
	#    print 'I am pushing object' + str(chosen_obj)
	#    action = {}
	#    action['force'] = [0, 3000, 0]
	#    action['torque'] = [0, 0, 0]
	#    action['id'] = str(chosen_obj)
	#    action['action_pos'] = []
	#    msg['msg']['actions'].append(action)
	#    chosen = False
	# teleport every 10 steps
        #if(teleport_counter == 20):
        #    msg['msg']['teleport_random'] = True
        #    teleport_counter = 0

	#teleport_counter = teleport_counter + 1 
	
        #print(obs)
        #msg['msg']['ang_vel'] = [0, .15, 0]
	#action = {}
        #action['force'] = [3000, 3000, 0]
        #action['torque'] = [0, 10, 0]
        #action['id'] = str(chosen)
        #action['action_pos'] = [] #map(float, chosen)
        #msg['msg']['actions'].append(action)                
        sock.send_json(msg['msg'])


#TODO: rather hacky, but works for now  
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.connect(("google.com",80))
host_address = s.getsockname()[0]
s.close()

ctx = zmq.Context()
def loop():

	print "connecting..."
	global sock 
	sock = ctx.socket(zmq.REQ)
	sock.connect("tcp://" + host_address + ":5557")
	print "... connected @" + host_address + ":" + "5557"

	config = {
		"environment_scene" : "ProceduralGeneration",
		"random_seed": 1, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
		"should_use_standardized_size": False,
		"standardized_size": [1.0, 1.0, 1.0],
		"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
		"permitted_items": [""] , #[],["bed1", "sofa_blue", "lamp"]
                "scale_relat_dict": {"http://threedworld.s3.amazonaws.com/46e777a46aa76681f4fb4dee5181bee.bundle": {"option": "Multi_size", "scale": 4}},  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
		"complexity": 500,
		"num_ceiling_lights": 4,
		"minimum_stacking_base_objects": 5,
		"minimum_objects_to_stack": 5,
		"room_width": 10.0,
		"room_height": 5.0,
		"room_length": 10.0,
		"wall_width": 1.0,
		"door_width": 1.5,
		"door_height": 3.0,
		"window_size_width": 5.0,
		"window_size_height": 5.0,
		"window_placement_height": 5.0,
		"window_spacing": 10.0,  #Average spacing between windows on walls
		"wall_trim_height": 0.5,
		"wall_trim_thickness": 0.01,
		"min_hallway_width": 5.0,
		"number_rooms": 1,
		"max_wall_twists": 3,
		"max_placement_attempts": 300,   #Maximum number of failed placements before we consider a room fully filled.
		"grid_size": 0.4    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
	}

	print "sending join..."
	sock.send_json({"msg_type" : "CLIENT_JOIN", "get_obj_data" : True, "send_scene_info" : True})
	print "...join sent"

	'''
	while True:
		print "waiting on messages..."
		msg1 = sock.recv()
		msg2 = sock.recv()
		msg3 = sock.recv()
		msg4 = sock.recv()
		print "...messages received\n\nsending input..."
		print msg1
		img1 = Image.open(StringIO(msg2)).convert('RGB')
		img2 = Image.open(StringIO(msg3)).convert('RGB')
		img3 = Image.open(StringIO(msg4)).convert('RGB')
		img1.show()
		img2.show()
		img3.show()
		time.sleep(10)
		sock.send_json({"msg_type" : "CLIENT_INPUT", "vel": [0.0, 0.0, 0.0], "ang_vel" : [0.0, 0.0, 0.0], "teleport_random" : True, "get_obj_data" : True, "send_scene_info" : True})
		print "...input sent"
	'''
	#teleport=0
	bn = 0
	while True:
		print "waiting on messages"
		#msg = [sock.recv() for _ in range(4)]
		print "messages received"
		make_new_batch(bn)
		bn = bn + 1
		#if teleport < 40:
		#	teleport += 1
		#	sock.send_json({"msg_type" : "CLIENT_INPUT", "vel": [2.0, 0.0, 2.0], "ang_vel" : [0.0, 2.0, 0.0], "teleport_random" : False, "get_obj_data" : True, "send_scene_info" : True})
		#	#time.sleep(3)
		#else:
		#	sock.send_json({"msg_type" : "SCENE_SWITCH", "config" : config})
		#	time.sleep(6)
		#	teleport = 0

def check_port_num(port_num):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
		s.bind((host_address, int(port_num)))
    except socket.error as e:
		s.close()
		if (e.errno == 98):
			return False
		elif (e.errno == 48):
			return False
		else:
			raise e
    s.close()
    return True

def check_if_env_up():
	while True:
		time.sleep(5)
		if (check_port_num(5557)):
			sys.exit()

t1 = multiprocessing.Process(target=loop)
t2 = multiprocessing.Process(target=check_if_env_up)

t1.start()
t2.start()

while True:
	time.sleep(3)
	if (not t2.is_alive()):
		t1.terminate()
		sys.exit()
	elif (not t1.is_alive()):
		t2.terminate()
		sys.exit()

