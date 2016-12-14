import numpy as np


class environment:
	# ShapeNet dictionary inquery
	shapenet_inquery = {'type': 'shapenetremat', 'version': 0, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}

	# Dosch dictionary inquery
	dosch_inquery = {'type': 'dosch', 'version': 1, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}

	RANDOM_SEED = 0
	COMPLEXITY = 5000
	NUM_LIGHTS = 4
	ROOM_WIDTH = 20.0
	ROOM_LENGTH = 20.0
	NUM_SHAPENET = 200
	NUM_DOSCH = 10

	rng_config = np.random.RandomState(0)

	def __init__(self, seed=0):
		self.rng_config = np.random.RandomState(seed)
		self.next_config(init=True)

	# update config for next scene switch
	def next_config(self, init=False):
		if not init:
			complexity = np.arange(5000,20001,1000)
			room_width = np.arange(12,26,1)	
			room_length = room_width

			self.RANDOM_SEED = self.rng_config.randint(1000000)
			self.COMPLEXITY = complexity[self.rng_config.randint(len(complexity))]
			self.ROOM_WIDTH = room_width[self.rng_config.randint(len(room_width))]
			self.ROOM_LENGTH = room_length[self.rng_config.randint(len(room_length))]
			if self.ROOM_LENGTH > 20 or self.ROOM_WIDTH > 20:
				self.NUM_LIGHTS = 8
			else:
				self.NUM_LIGHTS = 4
				
		# The environment config to be used
		self.config = {
			"environment_scene" : "ProceduralGeneration",
			"random_seed": self.RANDOM_SEED, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
			"should_use_standardized_size": False,
			"standardized_size": [1.0, 1.0, 1.0],
			"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
			"permitted_items": [""] , #[],["bed1", "sofa_blue", "lamp"]
			"scale_relat_dict": {"http://threedworld.s3.amazonaws.com/46e777a46aa76681f4fb4dee5181bee.bundle": {"option": "Multi_size", "scale": 4}},  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
			"complexity": self.COMPLEXITY,
			"random_materials": True,
			"num_ceiling_lights": self.NUM_LIGHTS,
			"intensity_ceiling_lights": 1,
			"use_standard_shader": True,
			"minimum_stacking_base_objects": 5,
			"minimum_objects_to_stack": 5,
			"disable_rand_stacking": 0,
			"room_width": self.ROOM_WIDTH,
			"room_height": 10.0,
			"room_length": self.ROOM_LENGTH,
			"wall_width": 1.0,
			"door_width": 1.5,
			"door_height": 3.0,
			"window_size_width": (5.0/1.618), # standard window ratio is 1:1.618
			"window_size_height": 5.0,
			"window_placement_height": 2.5,
			"window_spacing": 7.0,  #Average spacing between windows on walls
			"wall_trim_height": 0.5,
			"wall_trim_thickness": 0.01,
			"min_hallway_width": 5.0,
			"number_rooms": 1,
			"max_wall_twists": 3,
			#"enable_global_unit_scale": 1,
			"global_scale_dict": {"option": "Multi_size", "scale": 1, "var": 0.7, "seed": 0},
			"max_placement_attempts": 300,   #Maximum number of failed placements before we consider a room fully filled.
			"grid_size": 0.4,    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
			"use_mongodb_inter": 1, 
			"mongodb_items": {"shape_cons": {"find_argu": self.shapenet_inquery, "choose_mode": "random", "choose_argu": {"number": self.NUM_SHAPENET, "seed": self.RANDOM_SEED}}, "dosch":  {"find_argu": self.dosch_inquery, "choose_mode": "random", "choose_argu": {"number": self.NUM_DOSCH, "seed": self.RANDOM_SEED}}}
		}
