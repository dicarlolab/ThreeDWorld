## Group the info and upload it to mongodb

import pymongo
from optparse import OptionParser

parser = OptionParser()
parser.add_option("-s", "--startn", dest="startn", default =0, type=int)
parser.add_option("-l", "--length", dest="length", default =1000, type=int)
parser.add_option("-f", "--flag", dest="flag", default=0, type=int)

(options, args) = parser.parse_args()

conn = pymongo.MongoClient(port=22334)
coll = conn['synthetic_generative']['3d_models']
#test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True})

# Do the original upload information thing
if options.flag==0:

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
else:
    # Do the update remat information

    test_coll_new   = coll.find({'type' : 'shapenetremat', 'complexity': {'$exists': True}, 'center_pos': {'$exists': True}, 'boundb_pos': {'$exists': True}, 'isLight': {'$exists': True}, 'anchor_type': {'$exists': True}, 'aws_address': {'$exists': True}, 'has_texture': {'$exists': False}})
    list_coll_new   = list(test_coll_new[:])

    print("New info got!")

    test_coll_ori   = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True})
    list_coll_ori   = list(test_coll_ori[:])

    print("Original info got!")

    start_n         = options.startn
    end_n           = min(len(list_coll_new), start_n + options.length)

    for indx_now in xrange(start_n, end_n):
        now_info    = list_coll_new[indx_now]
        info_id     = now_info["obj_file_path"].split("/")[-2]
        #print(now_info)
        #print(info_id)
        found_list  = filter(lambda x:x['id']==info_id, list_coll_ori)
        old_info    = found_list[0]
        #print(len(found_list))
        #print(found_list[0])
        update_d    = {}

        for old_key in old_info:
            if old_key not in now_info:
                update_d[old_key]   = old_info[old_key]
        #print(update_d)

        coll.update_one({
            '_id': now_info['_id']
        },{
          '$set': update_d
        }, upsert=False)

    pass

