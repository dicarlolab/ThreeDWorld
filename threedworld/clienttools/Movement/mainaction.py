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
import actions.curious # import make_new_batch
from environment import environment
from threedworld.clienttools.tdw_client import TDW_Client

SEED = int(sys.argv[2])
CREATE_HDF5 = True
USE_TDW = True
SCENE_SWITCH = 20
SCREEN_WIDTH = 512
SCREEN_HEIGHT = 384
SELECTED_BUILD = 'one_world.exe'

os.environ['USER'] = 'mrowca'
#path = 'C:/Users/mrowca/Documents/test'
#path = 'F:\one_world_dataset'
#path = '/home/mrowca/Desktop/images'
#path = '/Users/damian/Desktop/test_images'
path = sys.argv[1]

#TODO: rather hacky, but works for now  
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.connect(("google.com",80))
host_address = s.getsockname()[0]
s.close()

ctx = zmq.Context()
def loop():
	global sock
	env = environment(SEED)	
	if USE_TDW:
		env.next_config()
		tc = TDW_Client(host_address,
			initial_command='request_create_environment',
			description="test script",
			selected_build=SELECTED_BUILD,  # or skip to select from UI
			#queue_port_num="23402",
			get_obj_data=True,
			send_scene_info=True
			)
		tc.load_config(env.config)
		tc.load_profile({'screen_width': SCREEN_WIDTH, 'screen_height': SCREEN_HEIGHT})
		sock = tc.run()
	else:
		print "connecting..." 
		sock = ctx.socket(zmq.REQ)
		sock.connect("tcp://" + host_address + ":5556")
		print "... connected @" + host_address + ":" + "5556"

		print "sending join..."
		#sock.send_json({"msg_type" : "SWITCH_SCENES", "get_obj_data" : True, "send_scene_info" : True})
		#sock.send_json({"msg_type" : "CLIENT_JOIN", "get_obj_data" : True, "send_scene_info" : True})
		#environment.next_config()
		sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "output_formats": ["png", "png", "jpg"]})
		print "...join sent"

	bn = 0
	agent = actions.curious.agent(CREATE_HDF5, path, SEED)
	if USE_TDW:
		agent.set_screen_width(SCREEN_WIDTH)
                agent.set_screen_height(SCREEN_HEIGHT)
	while True:
		if(bn != 0 and SCENE_SWITCH != 0 and bn % SCENE_SWITCH == 0):
			print "switching scene..."
			for i in range(4):
			    sock.recv();
			env.next_config()
			scene_switch_msg = {"msg_type" : "SCENE_SWITCH", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "output_formats": ["png", "png", "jpg"]}
			if USE_TDW:
                            sock.send_json({"n": 4, "msg": scene_switch_msg})
			else:
			    sock.send_json(scene_switch_msg)
			print "scene switched..."
		print "waiting on messages"
                agent.make_new_batch(bn, sock, path, CREATE_HDF5, USE_TDW)
		print "messages received"
		bn = bn + 1
	
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
