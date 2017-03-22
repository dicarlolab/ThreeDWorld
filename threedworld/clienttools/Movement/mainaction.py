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
import actions.curious2 as curious2 # import make_new_batch
from environment import environment
from threedworld.clienttools.tdw_client import TDW_Client
import curricula

SEED = int(sys.argv[2])
CREATE_HDF5 = False
USE_TDW = False
SCENE_SWITCH = 20
SCREEN_WIDTH = 600
SCREEN_HEIGHT = 256
SELECTED_BUILD = 'one_world.exe'

if USE_TDW:
	raise Exception('Not yet adapted to USE_TDW')

NUM_TIMES_RUN = 5

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

my_curriculum = [
	(curricula.new_curriculum, 'ONE_OBJ', [{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30
		}]),
	(curricula.new_table_curriculum, 'TABLE', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'TABLE',
		'scale' : 2.,
		'mass' : 50.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.controlled_table_curriculum, 'TABLE_CONTROLLED', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'TABLE',
		'scale' : 2.,
		'mass' : 50.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'OBJ_ON_OBJ', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'OTHER_STACKABLE',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.wall_throw_curriculum, 'WALL_THROW', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		}
		]),
	(curricula.new_curriculum, 'ONE_ROLLY', [{
		'type' : 'ROLLY',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30
		}]),
	(curricula.new_table_curriculum, 'ROLLY_ON_TABLE', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'TABLE',
		'scale' : 2.,
		'mass' : 50.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.controlled_table_curriculum, 'ROLLY_ON_TABLE_CONTROLLED', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'TABLE',
		'scale' : 2.,
		'mass' : 50.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'ROLLY_ON_OBJ', [
		{
		'type' : 'ROLLY',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'OTHER_STACKABLE',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'OBJ_ON_ROLLY', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'ROLLY',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'ROLLY_ON_ROLLY', [
		{
		'type' : 'ROLLY',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'ROLLY',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.wall_throw_curriculum, 'ROLLY_WALL_THROW', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		}
		])
]

just_controlled_table_curriculum = [
	(curricula.controlled_table_simple_test, 'TABLE_CONTROLLED', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'TABLE',
		'scale' : 2.,
		'mass' : 50.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
]

just_obj_on_obj_curriculum = [
	(curricula.other_obj_curriculum, 'OBJ_ON_OBJ', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'OTHER_STACKABLE',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'ROLLY_ON_OBJ', [
		{
		'type' : 'ROLLY',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'OTHER_STACKABLE',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'OBJ_ON_ROLLY', [
		{
		'type' : 'SHAPENET',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'ROLLY',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
	(curricula.other_obj_curriculum, 'ROLLY_ON_ROLLY', [
		{
		'type' : 'ROLLY',
		'scale' : .5,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 30,
		},
		{
		'type' : 'ROLLY',
		'scale' : 1.,
		'mass' : 1.,
		'scale_var' : .01,
		'num_items' : 10
		}
		]),
]

ctx = zmq.Context()
def loop():
	my_rng = np.random.RandomState(SEED + 3)
	global sock
	env = environment(my_seed = SEED, unity_seed = SEED + 1)	
	if USE_TDW:
		tc = TDW_Client(host_address,
			initial_command='request_create_environment',
			description="test script",
			selected_build=SELECTED_BUILD,  # or skip to select from UI
			#queue_port_num="23402",
			get_obj_data=True,
			send_scene_info=True
			)
	else:
		print "connecting..." 
		sock = ctx.socket(zmq.REQ)
		sock.connect("tcp://" + host_address + ":5556")
		print "... connected @" + host_address + ":" + "5556"
	if USE_TDW:
		agent.set_screen_width(SCREEN_WIDTH)
		agent.set_screen_height(SCREEN_HEIGHT)

		# print "sending join..."
		# #sock.send_json({"msg_type" : "SWITCH_SCENES", "get_obj_data" : True, "send_scene_info" : True})
		# #sock.send_json({"msg_type" : "CLIENT_JOIN", "get_obj_data" : True, "send_scene_info" : True})
		# #environment.next_config()
		# sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "output_formats": ["png", "png", "jpg"]})
		# print "...join sent"

	bn = 0
	agent = curious2.agent(CREATE_HDF5, path, SEED + 2)
	not_yet_joined = True
	for through_curriculum_num in range(NUM_TIMES_RUN):
		for (agent_directions, descriptor_prefix, scene_info) in just_obj_on_obj_curriculum:
			print 'selecting objects...'
			env.next_config(* scene_info)
			if not_yet_joined:
				if USE_TDW:
					tc.load_config(env.config)
					tc.load_profile({'screen_width': SCREEN_WIDTH, 'screen_height': SCREEN_HEIGHT})
					sock = tc.run()
				else:
					print 'sending join...'
					sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "output_formats": ["png", "png", "jpg"]})
					print '...join sent'
					not_yet_joined = False
			else:
				for i in range(7):
					sock.recv()
				print 'switching scene...'
				scene_switch_msg = {"msg_type" : "SCENE_SWITCH", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "output_formats": ["png", "png", "jpg"]}
				if USE_TDW:
					sock.send_json({"n": 4, "msg": scene_switch_msg})
				else:
					sock.send_json(scene_switch_msg)
			task_order = my_rng.permutation(len(agent_directions))
			for task_idx in task_order:
				task_params = agent_directions[task_idx]
				print 'waiting on messages'
				agent.make_new_batch(bn, sock, path, CREATE_HDF5, USE_TDW, task_params, descriptor_prefix)
				print 'message received'
				bn += 1
	
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
