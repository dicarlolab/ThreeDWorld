import sys
sys.path.insert(0, '/Users/chengxuz/3Dworld/ThreeDWorld/ClientTools')
from tdw_client import TDW_Client
import zmq
from StringIO import StringIO
from PIL import Image
import numpy as np
import socket

tc = TDW_Client('171.64.40.116', # "18.93.5.202",
                username="Pouya",
#                 queue_port_num="5556",
                description="Test_movements",
                initial_command="request_create_environment",
                selected_build="TDW-v1.0.0b05.x86_64",
#                 selected_build="TDW_mac_test",                
                get_obj_data=True,
               send_scene_info=True)
config = {
    "environment_scene": "ProceduralGeneration",
    "random_seed": 1,
    # Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
    "should_use_standardized_size": False,
    "standardized_size": [1.0, 1.0, 1.0],
    "disabled_items": [],
    # ["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
    "permitted_items": [],  # ["bed1", "sofa_blue", "lamp"],
    "complexity": 5000,
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
    "window_spacing": 10.0,  # Average spacing between windows on walls
    "wall_trim_height": 0.5,
    "wall_trim_thickness": 0.01,
    "min_hallway_width": 5.0,
    "number_rooms": 1,
    "max_wall_twists": 3,
    "max_placement_attempts": 300,
    # Maximum number of failed placements before we consider a room fully fil "grid_size": 0.4    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
}
tc.load_config(config)
sock = tc.run()
print(sock.recv())
for _ in range(3):
    msg = sock.recv()
    print('Message received!')
    if _ == 2:
        Image.open(StringIO(msg)).convert('RGB').show()
        pass
