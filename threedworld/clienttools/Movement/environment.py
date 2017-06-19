import numpy as np
import copy
import pymongo
from bson.objectid import ObjectId


default_inquery     = {'type': 'shapenetremat', 'has_texture': True, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}


default_keys = ['boundb_pos', 'isLight', 'anchor_type', 'aws_address', 'complexity', 'center_pos']







def query_results_to_unity_data(query_results, scale, mass, var = .01, seed = 0):
	item_list = []
	for i in range(len(query_results)):
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

synset_for_table = [[u'n04379243']]
rollie_synsets = [[u'n03991062'], [u'n02880940'], [u'n02946921'], [u'n02876657'], [u'n03593526']]
other_vaguely_stackable_synsets = [[u'n03207941'], [u'n04004475'], [u'n02958343'], [u'n03001627'], [u'n04256520'], [u'n04330267'], [u'n03593526'], [u'n03761084'], [u'n02933112'], [u'n03001627'], [u'n04468005'], [u'n03691459'], [u'n02946921'], [u'n03337140'], [u'n02924116'], [u'n02801938'], [u'n02828884'], [u'n03001627'], [u'n04554684'], [u'n02808440'], [u'n04460130'], [u'n02843684'], [u'n03928116']]

shapenet_inquery = {'type': 'shapenetremat', 'has_texture': True, 'version': 0, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}
dosch_inquery = {'type': 'dosch', 'has_texture': True, 'version': 1, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}


table_query = copy.deepcopy(shapenet_inquery)
table_query['synset'] = {'$in' : synset_for_table}
rolly_query = copy.deepcopy(shapenet_inquery)
rolly_query['synset'] = {'$in' : rollie_synsets}
other_reasonables_query = copy.deepcopy(shapenet_inquery)
other_reasonables_query['synset'] = {'$in' : other_vaguely_stackable_synsets}

query_dict = {'SHAPENET' : shapenet_inquery, 'ROLLY' : rolly_query, 'TABLE' : table_query, 'OTHER_STACKABLE' : other_reasonables_query}

class environment:
	conn = pymongo.MongoClient(port=22334)
	coll = conn['synthetic_generative']['3d_models']
	CACHE = {}

#right now these are being left fixed. I think complexity is being cut out entirely. At least, is irrelevant.
	COMPLEXITY = 1500
	NUM_LIGHTS = 4
	ROOM_WIDTH = 20.0
	ROOM_LENGTH = 20.0

	def __init__(self, my_seed=0, unity_seed = 0):
		self.rng = np.random.RandomState(my_seed)
		self.RANDOM_SEED = unity_seed
		# self.next_config(init=True)


	def get_items(self, q, num_items, scale, mass, var = .01):
		for _k in default_keys:
			if _k not in q:
				q[_k] = {'$exists': True}
		print('first query')
		if not str(q) in self.CACHE:
			idvals = np.array([str(_x['_id']) for _x in list(self.coll.find(q, projection=['_id']))])
			self.CACHE[str(q)] = idvals
			print('new', q, len(idvals))
		idvals = self.CACHE[str(q)]
		num_ava = len(idvals)
		#might want to just initialize this once
		goodidinds = self.rng.permutation(num_ava)[: num_items] 
		goodidvals = idvals[goodidinds]
		goodidvals = list(map(ObjectId, goodidvals))
		keys = copy.deepcopy(default_keys)
		for _k in q:
			if _k not in keys:
				keys.append(_k)
		print('second query')
		query_res = list(self.coll.find({'_id': {'$in': goodidvals}}, projection=keys))
		print('making items')
		return query_results_to_unity_data(query_res, scale, mass, var = var, seed = self.RANDOM_SEED + 1)


        def get_items_local(self, info):
            if 'has_texture' not in info:
                info['has_texture'] = True
            if 'isLight' not in info:
                info['isLight'] = False
            for k in ['type', 'aws_address', 'scale']:
                if k not in info:
                    raise KeyError('Missing key in local object info: %s' % k)
            return [info]

	# update config for next scene switch
	def next_config(self, * round_info):
            rounds = []
            for info in round_info:
                if 'host' in info and info['host'] is 'local':
                    rounds.append({'items': self.get_items_local(info),
                            'num_items': info['num_items']})
                else:
                    rounds.append({
                        'items' : self.get_items(query_dict[info['type']], 
                            info['num_items'] * 4, 
                            info['scale'], info['mass'], 
                            info['scale_var']), 
                        'num_items' : info['num_items']})

		self.config = {
			"environment_scene" : "ProceduralGeneration",
			"random_seed": self.RANDOM_SEED, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
			"should_use_standardized_size": False,
			"standardized_size": [1.0, 1.0, 1.0],
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
			"max_placement_attempts": 300,   #Maximum number of failed placements before we consider a room fully filled.
			"grid_size": 0.4,    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
			"use_mongodb_inter": 1, 
			'rounds' : rounds
			}
