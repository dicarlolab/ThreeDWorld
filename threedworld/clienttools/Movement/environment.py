import numpy as np
import copy
import pymongo


default_inquery     = {'type': 'shapenetremat', 'has_texture': True, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}


def query_results_to_unity_data(query_results, scale, mass, var = .01, seed = 0):
	item_list = []
	for i in range(10):
		print i
		res = query_results[i]
		item = {}
		item['type'] = res['type']
		item['has_texture'] = res['has_texture']
		item['center_pos'] = res['center_pos']
		item['boundb_pos'] = res['boundb_pos']
		item['isLight'] = res['isLight']
		item['anchor_type'] = res['anchor_type']
		item['aws_address'] = res['aws_address']
		item['mass'] = mass
		item['scale'] = {"option": "Absol_size", "scale": scale, "var": var, "seed": seed, 'apply_to_inst' : True}
		item['_id_str'] = str(res['_id'])
		item_list.append(item)
	return item_list

class environment:
        synset_for_table = [[u'n04379243']]
        # synset_for_table = [[u'n03207941'], [u'n04004475'], [u'n02958343'], [u'n03001627'], [u'n04256520'], [u'n04330267'], [u'n03593526'], [u'n03761084'], [u'n02933112'], [u'n03001627'], [u'n04468005'], [u'n03691459'], [u'n02946921'], [u'n03337140'], [u'n02924116'], [u'n02801938'], [u'n02828884'], [u'n03001627'], [u'n04554684'], [u'n02808440'], [u'n04460130'], [u'n02843684'], [u'n03928116']]
        # synset_for_table = []
        rollie_synsets = [[u'n03991062'], [u'n02880940'], [u'n02946921'], [u'n02876657'], [u'n03593526']]

	# ShapeNet dictionary inquery
	shapenet_inquery = {'type': 'shapenetremat', 'has_texture': True, 'version': 0, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}

	# Dosch dictionary inquery
	dosch_inquery = {'type': 'dosch', 'has_texture': True, 'version': 1, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}

        # Tables
        # q_tables = {'type': 'shapenetremat', 'version': 0, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}, 'synset' : synset_for_table}
        q_tables = {'type': 'shapenetremat', 'version': 0, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}, 'synset' : {'$in' : synset_for_table}}

	conn = pymongo.MongoClient(port=22334)
	coll = conn['synthetic_generative']['3d_models']


	RANDOM_SEED = 57
	COMPLEXITY = 1500
	NUM_LIGHTS = 4
	ROOM_WIDTH = 20.0
	ROOM_LENGTH = 20.0
	NUM_SHAPENET = 200
	NUM_DOSCH = 10
        NUM_STACKABLE = 10

	rng_config = np.random.RandomState(0)

	def __init__(self, seed=0):
		self.rng_config = np.random.RandomState(seed)
		rolly_query = copy.deepcopy(self.shapenet_inquery)
		rolly_query['synset'] = {'$in' : self.rollie_synsets}
		table_query = copy.deepcopy(self.shapenet_inquery)
		table_query['synset'] = {'$in' : self.synset_for_table}
		print('querying')
		table_res = self.coll.find(table_query)
		shapenet_res = self.coll.find(self.shapenet_inquery)
		dosch_res = self.coll.find(self.dosch_inquery)
		print('going through queries')
		self.table_bottom_item_list = query_results_to_unity_data(table_res, 2., 50., var = .01, seed = 0)
		self.regular_stuff = query_results_to_unity_data(shapenet_res, .5, 1., var = .01, seed = 0) 
		# + query_results_to_unity_data(dosch_res, .5, 1., var = .01, seed = 0)
		self.next_config(init=True)
		print('yeah')


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

		items_dict = {
"shape_cons": {"find_argu": self.shapenet_inquery, "choose_mode": "random", "choose_argu": {"number": self.NUM_SHAPENET, "seed": self.RANDOM_SEED}}, 
"dosch":  {"find_argu": self.dosch_inquery, "choose_mode": "random", "choose_argu": {"number": self.NUM_DOSCH, "seed": self.RANDOM_SEED}}, 
		}

		for item in self.rollie_synsets:
			item_query = copy.deepcopy(self.shapenet_inquery)
			item_query['synset'] = item
			items_dict[str(item[0])] = {"find_argu": item_query, "choose_mode": "random", "choose_argu": {"number": self.NUM_STACKABLE, "seed": self.RANDOM_SEED}}


		self.config = {
			"environment_scene" : "ProceduralGeneration",
			"random_seed": self.RANDOM_SEED, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
			"should_use_standardized_size": False,
			"standardized_size": [1.0, 1.0, 1.0],
			"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
			"permitted_items": [""] , #[],["bed1", "sofa_blue", "lamp"]
			# "scale_relat_dict": table_scale_deal,  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
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
			# "global_scale_dict": {"option": "Multi_size", "scale": 1, "var": 0.7, "seed": 0},
			"max_placement_attempts": 300,   #Maximum number of failed placements before we consider a room fully filled.
			"grid_size": 0.4,    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
			"use_mongodb_inter": 1, 
			'rounds' : [{'items' : self.regular_stuff, 'num_items' : 20}, {'items' : self.table_bottom_item_list, 'num_items' : 10}]

# 			{
# "shape_cons": {"find_argu": self.shapenet_inquery, "choose_mode": "random", "choose_argu": {"number": self.NUM_SHAPENET, "seed": self.RANDOM_SEED}}, 
# "dosch":  {"find_argu": self.dosch_inquery, "choose_mode": "random", "choose_argu": {"number": self.NUM_DOSCH, "seed": self.RANDOM_SEED}}, 
# "tables": {"find_argu": self.q_tables, "choose_mode": "random", "choose_argu": {"number": self.NUM_STACKABLE, "seed": self.RANDOM_SEED}}}
# 		}
}
