import time
import zmq
import time
import os
import socket
#import multiprocessing
import sys
import numpy as np
#import h5py
import json
from PIL import Image
from StringIO import StringIO

from curiosity.utils.io import (handle_message,
				send_array,
				recv_array)
from tdw_client import TDW_Client


SCREEN_WIDTH = 256 #640

BATCH_SIZE = 256
MULTSTART = -1

achoice = [-5, 0, 5]
ACTION_LENGTH = 15
ACTION_WAIT = 15

N = 1024000

global number_of_frames
number_of_frames = 0

global reset
reset = True

global tstart

#path = 'C:/Users/mrowca/Documents/test'
path = '/home/mrowca/Desktop/images'
#infodir = path + '_info'
#if not os.path.exists(infodir):
#    os.makedirs(infodir)

#file = h5py.File(path, mode='a')
#valid = file.require_dataset('valid', shape=(N,), dtype=np.bool)
#images = file.require_dataset('images', shape=(N, 256, 256, 3), dtype=np.uint8)
#normals = file.require_dataset('normals', shape=(N, 256, 256, 3), dtype=np.uint8)
#objects = file.require_dataset('objects', shape=(N, 256, 256, 3), dtype=np.uint8)

valid = np.zeros((N,1))

rng = np.random.RandomState(0)

def choose(x):
  return x[rng.randint(len(x))]

def choose_action_position(objarray):
  xs, ys = (objarray > 2).nonzero()
  pos = zip(xs, ys)
  return pos[rng.randint(len(pos))]

# bn integer
def make_new_batch(bn):
    global number_of_frames
    global reset
    global tstart
    # how many frames of action its allowed to take
    action_length = ACTION_LENGTH #(bsize - i) / 3
    # how long it waits after action end
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
            number_of_frames = number_of_frames + 1;
            print(i)
            info, narray, oarray, imarray = handle_message(sock,
                                                           write=True,
                                                           outdir=path, prefix=str(bn) + '_' + str(i))
            if(reset == True):
              	tstart = time.time()
                reset = False
            
            msg = {'n': 4,
                   'msg': {"msg_type": "CLIENT_INPUT",
                           "get_obj_data": False,
                           "actions": []}}

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
                        if np.abs(SCREEN_WIDTH/2 - pos[1]) < 3:
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






#TODO: rather hacky, but works for now  
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.connect(("google.com",80))
host_address = s.getsockname()[0]
s.close()

port_num = 23402


ctx = zmq.Context()
def loop():
        global tstart
	print "connecting..."
	global sock 
	#sock = ctx.socket(zmq.REQ)
	#sock.connect("tcp://" + host_address + ":5556")
	print "... connected @" + host_address + ":" + "5556"

	config = {
		"environment_scene" : "ProceduralGeneration",
		"random_seed": 1, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
		"should_use_standardized_size": False,
		"standardized_size": [1.0, 1.0, 1.0],
		"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
		"permitted_items": [""] , #[],["bed1", "sofa_blue", "lamp"]
                "scale_relat_dict": {"http://threedworld.s3.amazonaws.com/46e777a46aa76681f4fb4dee5181bee.bundle": {"option": "Multi_size", "scale": 4}},  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
		"complexity": 10,
		"num_ceiling_lights": 4,
		"minimum_stacking_base_objects": 5,
		"minimum_objects_to_stack": 5,
		"room_width": 20.0,
		"room_height": 10.0,
		"room_length": 20.0,
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

	global tc
	tc = TDW_Client(host_address,
                initial_command='request_create_environment',
                #selected_build='test_none.x86_64',  # or skip to select from UI
                #queue_port_num="23402",
		get_obj_data=True,
                send_scene_info=True
                )

	tc.load_config(config)
	tc.load_profile({'screen_width': 256, 'screen_height': 256})	
	sock = tc.run()

	#Receive answer
	print "receiving initial answer..."
 	for i in range(3):
 		msg = sock.recv()
		print i
	print "...received\n"
	print "sending join..."
	#handle_message(sock, write=True, outdir=path, prefix=str(0) + '_' + str(0))

	sock.send_json({"msg_type" : "CLIENT_JOIN", "get_obj_data" : True, "send_scene_info" : True})
        #sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : config, "get_obj_data" : True, "send_scene_info" : True})
	#sock.send_json({'n': 4, 'msg': {"msg_type": "SCENE_SWITCH", "config": config, 'send_scene_info': True}})
	#msg = {'n': 4, 'msg': {"msg_type": "CLIENT_INPUT", "get_obj_data": True, "actions": []}}
	#sock.send_json(msg)	
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
		end = time.time()
		FPS = number_of_frames / (end - tstart)
		
                print('--------TIME----------')
                print(end - tstart)
                print(number_of_frames)
                print('--------FPS-----------')
                print(FPS)
                print('----------------------')
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
		if (check_port_num(5556)):
			sys.exit()

#t1 = multiprocessing.Process(target=loop)
#t2 = multiprocessing.Process(target=check_if_env_up)

#t1.start()
#t2.start()

#while True:
#	time.sleep(3)
#	if (not t2.is_alive()):
#		t1.terminate()
#		sys.exit()
#	elif (not t1.is_alive()):
#		t2.terminate()
#		sys.exit()

loop()
