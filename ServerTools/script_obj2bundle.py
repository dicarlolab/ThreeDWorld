# This script will generate assetbundle files from .obj with .mtl (as well as texture images if needed) and upload files to AWS and related information to mongodb

import os
import multiprocessing
import pymongo
import sys
from yamutils import basic
from optparse import OptionParser
import numpy as np
import hashlib

file_list   = []
options     = []

def vhacd_it(ind):
    global file_list
    global options

    cmdtmpl1 = '%s --input "%s" --output "%s" --log log.txt --resolution 500000 --maxNumVerticesPerCH 64' 
    cmdtmpl2 = '%s --input "%s" --output "%s" --log log.txt --resolution 16000000 --concavity 0.001 --maxNumVerticesPerCH 64 --minVolumePerCH 0.0001' 

    start_indx  = min(ind * options.mapn, len(file_list))
    end_indx    = min((ind+1)*options.mapn, len(file_list))
    objfiles    = file_list[start_indx: end_indx]
    
    for of in objfiles:
        print('FILE: %s' % of)
        wf = of[:-3] + 'wrl'
        cmd = cmdtmpl1 % (options.vhacd, of, wf)
        os.system(cmd)
        osize = os.path.getsize(of)
        wsize = os.path.getsize(wf)
        if osize > 100 * wsize:
            cmd = cmdtmpl2 % (options.vhacd, of, wf)
            os.system(cmd)

def get_file_list():
    global options

    file_list = filter(lambda x: x.endswith('.obj'), basic.recursive_file_list(os.path.join(options.projectdir, options.inputdir)))
    file_list = filter(lambda x: os.path.isfile(x[:-3] + 'mtl'), file_list)
    file_list.sort()

    return file_list

if __name__=='__main__':

    parser = OptionParser()
    #parser.add_option("-i", "--inputdir", dest="inputdir", default ="Assets/Models/dorsch_models/JobPoses", type=str, help = "The relative directory of all the models (relative to the project path)")
    parser.add_option("-i", "--inputdir", dest="inputdir", default ="Assets/Models/dorsch_models/JobPoses_test", type=str, help = "The relative directory of all the models (relative to the project path)")
    parser.add_option("-p", "--projectdir", dest="projectdir", default ="/home/chengxuz/ThreeDWorld", type=str, help = "The absolute path to the Unity project")
    parser.add_option("-v", "--vhacd", dest="vhacd", default = "/home/chengxuz/ThreeDworld_related/v-hacd/build/linux2/test/testVHACD", type=str, help = "The path to the vhacd generating executable file testVHACD")
    parser.add_option("-o", "--portn", dest="portn", default = 22334, type=int, help = "The port for mongodb connected to the database on dicarlo5")
    parser.add_option("-n", "--processn", dest="processn", default = 2, type=int, help = "The number of processes used to compute the VHACD files")
    parser.add_option("-t", "--type", dest="type", default = "dorsh", type=str, help = "The value of the variable type to be set in mongodb")
    parser.add_option("-e", "--version", dest="version", default = 0, type=int, help = "The value of the variable version to be set in mongodb")
    parser.add_option("-m", "--mapn", dest="mapn", default = 10, type=int, help = "The number in each division used to compute the VHACD files")
    parser.add_option("-f", "--force", dest="force", default = 0, type=int, help = "0 for judging whether continue, 1 for generating and uploading anything anyway")

    (options, args) = parser.parse_args()

    conn = pymongo.MongoClient(port=options.portn)
    coll = conn['synthetic_generative']['3d_models']

    ### Generate the wrl files using VHACD
    file_list = get_file_list()

    if options.force==0:
        new_file_list   = []
        for file_name in file_list:
            wrl_path    = file_name[:-3] + 'wrl'
            #print(wrl_path + str(os.path.isfile(wrl_path)))
            if not os.path.isfile(wrl_path):
                new_file_list.append(file_name)
        file_list   = new_file_list

    args = range(int(np.ceil(len(file_list)*1.0/options.mapn)))
    pool = multiprocessing.Pool(processes=2)
    r = pool.map_async(vhacd_it, args)
    r.get()
    print('VHACD done!')

    ### Add to mongodb database and get _id if needed
    file_list = get_file_list()

    #### Get the md5 sum
    md5_dict    = {}
    md5_dict_i  = {}
    suffix_list = ['obj', 'wrl', 'mtl']
    for file_name in file_list:
        md5_tmp     = ""
        for suffix_indx in range(len(suffix_list)):
            suffix_now      = suffix_list[suffix_indx]
            now_file_name   = file_name[:-3] + suffix_now
            md5_tmp         = md5_tmp +  "_" + str(hashlib.md5(open(now_file_name, 'rb').read()).hexdigest())
        md5_dict[file_name]     = md5_tmp
        md5_dict_i[md5_tmp]     = file_name
    print('md5 computed')

    #### Ask whether this item exists, if not, create the item and get the _id; otherwise, ignore it

    _id_dict    = {}
    md5_values_list     = md5_dict.values()
    if options.force==0:
        test_coll = coll.find({'type': options.type, 'version': options.version, 'md5_value': {'$in': md5_values_list}})
        #print(test_coll.count())
        exist_doc_list  = list(test_coll[:])

        for exist_doc in exist_doc_list:
            if 'aws_address' in exist_doc:
                file_name_now   = md5_dict_i[exist_doc['md5_value']]
                file_list.remove(file_name_now)

    #print(len(file_list))
    print('Register the models')
    for file_name in file_list:
        coll.update_one({
            'type': options.type, 'version': options.version, 'md5_value': md5_dict[file_name]
        },{
          '$set': {
            'type': options.type,
            'version': options.version,
            'md5_value': md5_dict[file_name],
            'obj_file_path': file_name,
          }
        }, upsert=True)
    
    
    # Get the _id
    test_coll = coll.find({'type': options.type, 'version': options.version, 'md5_value': {'$in': md5_values_list}})
    #print(test_coll.count())
    exist_doc_list  = list(test_coll[:])
    for exist_doc in exist_doc_list:
        if exist_doc['obj_file_path'] in file_list:
            _id_dict[exist_doc['obj_file_path']]    = str(exist_doc['_id'])

    
