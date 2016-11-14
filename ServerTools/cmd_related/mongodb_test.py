import pymongo
conn = pymongo.MongoClient(port=22334)
coll = conn['synthetic_generative']['3d_models']
test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True})

for i in range(10):
    print(test_coll[i]['id'])
print(test_coll[0])
