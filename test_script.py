import zmq
from tdw_client import TDW_Client
from StringIO import StringIO
from PIL import Image
import time

tc = TDW_Client("18.93.5.202", username="tester", description="test script", initial_command="request_create_environment", selected_build="TestBuild22.x86_64", get_obj_data=True)
tc.load_config({
	"environment_scene" : "ProceduralGeneration",
	"random_seed": 1, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
	"should_use_standardized_size": False,
	"standardized_size": [1.0, 1.0, 1.0],
	"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
	"permitted_items": [], #["bed1", "sofa_blue", "lamp"],
	"complexity": 20000,
	"num_ceiling_lights": 4,
	"minimum_stacking_base_objects": 5,
	"minimum_objects_to_stack": 100,
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
	"max_placement_attempts": 300,   #Maximum number of failed placements before we consider a room fully fil "grid_size": 0.4    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
})
sock = tc.run()

while True:
	print "\nnext batch:\n"
	print sock.recv()
	for _ in range(3):
		msg = sock.recv()
		Image.open(StringIO(msg)).convert('RGB').show()
	time.sleep(5)
	sock.send_json({'n' : 4, 'msg' : {"msg_type" : "CLIENT_INPUT", "teleport_random": True, "vel" : [0.0, 0.0, 0.0], "ang_vel" : [0.0, 0.0, 0.0]}})
