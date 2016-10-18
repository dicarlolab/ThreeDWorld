import zmq
import time
import os
import socket
import multiprocessing
import sys
from PIL import Image
from StringIO import StringIO

#TODO: rather hacky, but works for now  
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.connect(("google.com",80))
host_address = s.getsockname()[0]
s.close()

ctx = zmq.Context()

def loop():
	print "connecting..."
	sock = ctx.socket(zmq.REQ)
	sock.connect("tcp://" + host_address + ":5556")
	print "... connected @" + host_address + ":" + "5556"


	config = {
		"environment_scene" : "ProceduralGeneration",
		"random_seed": 1, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
		"should_use_standardized_size": False,
		"standardized_size": [1.0, 1.0, 1.0],
		"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
		"permitted_items": ["46e777", "46bd9"] , #[],["bed1", "sofa_blue", "lamp"]
                "scale_relat_dict": {"bed": {"option": "Multi_size", "scale": 1}},  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
		"complexity": 5000,
		"num_ceiling_lights": 4,
		"minimum_stacking_base_objects": 5,
		"minimum_objects_to_stack": 5,
		"room_width": 45.0,
		"room_height": 20.0,
		"room_length": 45.0,
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
	sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : config, "get_obj_data" : True, "send_scene_info" : True})
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
	teleport=0
	while True:
		print "waiting on messages"
		msg = [sock.recv() for _ in range(4)]
		print "messages received"
		if teleport < 40:
			teleport += 1
			sock.send_json({"msg_type" : "CLIENT_INPUT", "vel": [0.0, 0.0, 0.0], "ang_vel" : [0.0, 0.0, 0.0], "teleport_random" : True, "get_obj_data" : True, "send_scene_info" : True})
			#time.sleep(3)
		else:
			sock.send_json({"msg_type" : "SCENE_SWITCH", "config" : config})
			time.sleep(6)
			teleport = 0

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
