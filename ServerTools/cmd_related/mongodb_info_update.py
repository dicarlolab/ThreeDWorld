## Group the info and upload it to mongodb

import pymongo
from optparse import OptionParser

parser = OptionParser()
parser.add_option("-s", "--startn", dest="startn", default =0, type=int)
parser.add_option("-l", "--length", dest="length", default =1000, type=int)

(options, args) = parser.parse_args()

conn = pymongo.MongoClient(port=22334)
coll = conn['synthetic_generative']['3d_models']
#test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True})

all_data_dict   = {}

## Get the uploaded model list

fin_name    = '../list_aws.txt'
fin         = open(fin_name, 'r')
fin_lines   = fin.readlines()

for lines in fin_lines:
    id_current  = lines.split('/')[-1].split('.')[0]
    #print(id_current)
    #print(lines[:-1])
    all_data_dict[id_current]   = lines[:-1]

## Get the info and upload it

fin_name    = '../list_info_aws.txt'
fin         = open(fin_name, 'r')
fin_lines   = fin.readlines()

def get_pos(split_lines):
    pos_list    = []
    pos_list.append(float(split_lines[0].split('(')[1]))
    pos_list.append(float(split_lines[1]))
    pos_list.append(float(split_lines[2][:-1]))
    return pos_list

uploaded_num    = 0

start_n         = options.startn
end_n           = min(len(fin_lines), start_n + options.length)

for lines in fin_lines[start_n:end_n]:

    split_lines     = lines.split(',')
    id_current      = split_lines[0].split('.')[0]
    cur_complexity  = int(split_lines[1])
    center_pos      = get_pos(split_lines[2:5])
    boundb_pos      = get_pos(split_lines[5:8])
    isLight         = split_lines[8]
    anchor_type     = split_lines[9][:-1]

    if not id_current in all_data_dict:
        continue
    #continue

    #print(id_current, cur_complexity, center_pos, boundb_pos, isLight, anchor_type)
    #print(split_lines)
    #test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True, 'id': id_current})
    #print(test_coll[0])
    coll.update_one({
        'type': 'shapenet', 'version': 2, 'has_texture':True, 'id': id_current
    },{
      '$set': {
        'complexity': cur_complexity,
        'center_pos': center_pos,
        'boundb_pos': boundb_pos,
        'isLight': isLight,
        'anchor_type': anchor_type,
        'aws_address': all_data_dict[id_current]
      }
    }, upsert=False)

    #uploaded_num    = uploaded_num + 1
    #if uploaded_num%100==0:
    #    print(uploaded_num)
    #test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True, 'id': id_current})
    #print(test_coll[0])
    #pass
    #break
