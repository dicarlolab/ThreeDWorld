import pymongo
import time

conn = pymongo.MongoClient(port=22334)
coll = conn['synthetic_generative']['3d_models']
#test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True, 'complexity': {'exist': True}, 'center_pos': {'exist': True}, 'boundb_pos': {'exist': True}, 'isLight': {'exist': True}, 'anchor_type': {'exist': True}, 'aws_address': {'exist': True}})
test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True, 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}}, {'synset_tree':0, 'keywords':0})

print(test_coll.count())
#print(test_coll[0].pop('_id'))

start_time = time.time()
test_coll.batch_size(100000)
'''
for i in range(100):
    #print(test_coll[i]['id'])
    test_tmp    = test_coll[i]
    print(i)
'''
test_tmp    = list(test_coll[:])

for i in range(100):
    #print(test_coll[i]['id'])
    test_tmp_   = test_tmp[i]
    print(i)
#print(test_tmp[0])
print("--- %s seconds ---" % (time.time() - start_time))
#print(test_coll[0])
