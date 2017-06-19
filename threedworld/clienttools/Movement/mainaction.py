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

try:
    from StringIO import StringIO
except ImportError:
    from io import StringIO

import actions.curious2 as curious2 # import make_new_batch
from environment import environment
from threedworld.clienttools.tdw_client import TDW_Client
import curricula

SEED = int(sys.argv[2])
CREATE_HDF5 = False
USE_TDW = True
SCENE_SWITCH = 20
SCREEN_WIDTH = 170
SCREEN_HEIGHT = 128
SELECTED_BUILD = 'one_world.exe'

NUM_TIMES_RUN = 1
REPEATS = 1000
BATCH_SIZE = 256
TOTAL = NUM_TIMES_RUN * REPEATS * BATCH_SIZE

SHADERS = [{"DisplayNormals": "png"}, {"GetIdentity": "png"}, {"DisplayDepth": "png"}, {"DisplayVelocity": "png"}, {"DisplayAcceleration": "png"}, {"DisplayJerk": "png"}, {"Images": "jpg"}]
HDF5_NAMES = [{"DisplayNormals": "normals"}, {"GetIdentity": "objects"}, {"DisplayDepth": "depths"}, {"DisplayVelocity": "velocities"}, {"DisplayAcceleration": "accelerations"}, {"DisplayJerk": "jerks"}, {"Images": "images"}]

n_cameras = 2
num_frames_per_msg = 1 + n_cameras * len(SHADERS) # +1 because of info frame
print('Exchanging %d messages' % num_frames_per_msg)

os.environ['USER'] = 'nhaber'
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

random_object_throws = [
        (curricula.object_throw_curriculum * REPEATS, 'OBJ_THROW_OBJ', [
                {
                'type' : 'SHAPENET',
                'scale' : 0.4,
                'mass' : 1.,
                'scale_var' : .01,
                'num_items' : 1,
                },
                {
                'type' : 'OTHER_STACKABLE',
                'scale' : 0.4,
                'mass' : 1.,
                'scale_var' : .01,
                'num_items' : 1,
                }
                ])]

object_mash = [
    (curricula.object_mash_curriculum * REPEATS, 'OBJ_MASH_OBJ', [
        {
        'type' : 'SHAPENET',
        'scale' : .4,
        'mass' : 1.,
        'scale_var' : .01,
        'num_items' : 1,
        },
        {
        'type' : 'OTHER_STACKABLE',
        'scale' : .4,
        'mass' : 1.,
        'scale_var' : .01,
        'num_items' : 1,
        }


        ])
]

object_mash_rot = [
    (curricula.object_mash_rot_curriculum * REPEATS, 'OBJ_MASH_OBJ', [
        {
        'type' : 'SHAPENET',
        'scale' : .4,
        'mass' : 1.,
        'scale_var' : .01,
        'num_items' : 1,
        },
        {
        'type' : 'OTHER_STACKABLE',
        'scale' : .4,
        'mass' : 1.,
        'scale_var' : .01,
        'num_items' : 1,
        }
        ])
]


lift_smash_local = [
    (curricula.lift_smash_curriculum * REPEATS, 'LIFT_SMASH', [
        {
        'host' : 'local',
        'aws_address' : 'PrefabDatabase/AssetBundles/Separated/playbox.bundle',
        'type' : 'CONTAINER',
        'scale' : {"option": "Absol_size", 
            "scale": 0.7,
            "var": .0, 
            "seed": SEED, 'apply_to_inst' : True},
        'mass' : 10000.,
        'num_items' : 1,
        }
        ]
    )
]

lift_smash = [
    (curricula.lift_smash_curriculum * REPEATS, 'LIFT_SMASH', [
        {
        'type' : 'SHAPENET',
        'scale' : .4,
        'mass' : 1.,
        'scale_var' : .01,
        'num_items' : 1,
        }
        ]
    )
]

lift_smash_rot = [
    (curricula.lift_smash_rot_curriculum * REPEATS, 'LIFT_SMASH_ROT', [
        {
        'type' : 'SHAPENET',
        'scale' : .4,
        'mass' : 1.,
        'scale_var' : .01,
        'num_items' : 1,
        }
        ]
    )
]



just_object_throws = [
        (curricula.object_throw_curriculum, 'OBJ_THROW_OBJ', [
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
        (curricula.object_throw_curriculum, 'ROLLY_THROW_OBJ', [
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
                ])
]

just_object_throws = just_object_throws * 4
my_curriculum = my_curriculum + just_object_throws
#HERE to edit which currlculum you're doing, or add them together. lift_smash, lift_smash_rot, object_mash, object_mash_rot
my_curriculum = lift_smash

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

just_random_table_curriculum = [
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
                ])
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
                ])
]



just_wall_throws = [
(curricula.wall_throw_curriculum, 'WALL_THROW', [
                {
                'type' : 'SHAPENET',
                'scale' : .5,
                'mass' : 1.,
                'scale_var' : .01,
                'num_items' : 30,
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

num_types = 0
for (agent_directions, pfx, scene_info) in my_curriculum:
    num_types += len(agent_directions)

print ('num types in my curriculum: ' + str(num_types))

ctx = zmq.Context()
def loop():
        my_rng = np.random.RandomState(SEED + 3)
        global sock
        env = environment(my_seed = SEED, unity_seed = SEED + 1)
        agent = curious2.agent(
                CREATE_HDF5, 
                num_frames_per_msg, 
                n_cameras, 
                SHADERS, 
                HDF5_NAMES, 
                BATCH_SIZE, 
                TOTAL, 
                SCREEN_WIDTH, 
                SCREEN_HEIGHT, 
                path, 
                dataset_num = int(sys.argv[3]), 
                continue_writing = True)
        if USE_TDW:
                tc = TDW_Client(host_address,
                        initial_command='request_create_environment',
                        description="test script",
                        selected_build=SELECTED_BUILD,  # or skip to select from UI
                        #queue_port_num="23402",
                        get_obj_data=True,
                        send_scene_info=True,
                        num_frames_per_msg=num_frames_per_msg,
                        )
        else:
                print ("connecting...")
                sock = ctx.socket(zmq.REQ)
                sock.connect("tcp://" + host_address + ":5556")
                print ("... connected @" + host_address + ":" + "5556")
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
        not_yet_joined = True
        for through_curriculum_num in range(NUM_TIMES_RUN):
                print('beginning curriculum: ' + str(through_curriculum_num))
                for (agent_directions, descriptor_prefix, scene_info) in my_curriculum:
                        print('selecting objects...')
                        env.next_config(* scene_info)
                        scene_start = True
                        if not_yet_joined:
                                if USE_TDW:
                                        tc.load_config(env.config)
                                        tc.load_profile({'screen_width': SCREEN_WIDTH, 'screen_height': SCREEN_HEIGHT})
                                        sock = tc.run()
                                else:
                                        print('sending join...')
                                        sock.send_json({"msg_type" : "CLIENT_JOIN_WITH_CONFIG", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "shaders": SHADERS})
                                        print('...join sent')
                                not_yet_joined = False
                        else:
                                for i in range(num_frames_per_msg):
                                        sock.recv()
                                print('switching scene...')
                                scene_switch_msg = {"msg_type" : "SCENE_SWITCH", "config" : env.config, "get_obj_data" : True, "send_scene_info" : True, "shaders": SHADERS}
                                if USE_TDW:
                                        sock.send_json({"n": num_frames_per_msg, "msg": scene_switch_msg})
                                else:
                                        sock.send_json(scene_switch_msg)
                        task_order = my_rng.permutation(len(agent_directions))
                        for (order, task_idx) in enumerate(task_order):
                                print('task time ' + str(order))
                                task_params = agent_directions[task_idx]
                                print('waiting on messages')
                                prefix_plus_order = descriptor_prefix + ':' + str(order)
                                print('about to start batch ' + str(bn))
                                agent.make_new_batch(bn, sock, path, CREATE_HDF5, USE_TDW, task_params, prefix_plus_order, scene_start = scene_start)
                                print('batch completed')
                                scene_start = False
                                print('message received')
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
#       time.sleep(3)
#       if (not t2.is_alive()):
#               t1.terminate()
#               sys.exit()
#       elif (not t1.is_alive()):
#               t2.terminate()
#               sys.exit()

loop()
