# This script will generate assetbundle files from .obj with .mtl (as well as texture images if needed) and upload files to AWS and related information to mongodb

import os
import multiprocessing
import pymongo
import sys
from yamutils import basic
from optparse import OptionParser
import numpy as np
import hashlib
from bson.objectid import ObjectId

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

def get_pos(split_lines):
    pos_list    = []
    pos_list.append(float(split_lines[0].split('(')[1]))
    pos_list.append(float(split_lines[1]))
    pos_list.append(float(split_lines[2][:-1]))
    return pos_list

if __name__=='__main__':

    parser = OptionParser()
    parser.add_option("-i", "--inputdir", dest="inputdir", default ="Assets/Models/dorsch_models/JobPoses_test", type=str, help = "The relative directory of all the models (relative to the project path), if --parallel is 1, this then should be the absolute path to the models")
    parser.add_option("-p", "--projectdir", dest="projectdir", default ="/home/chengxuz/ThreeDWorld", type=str, help = "The absolute path to the Unity project, if --parallel is 1, then the project path should not exist (we would copy the empty prjoect there)")
    parser.add_option("-v", "--vhacd", dest="vhacd", default = "/home/chengxuz/ThreeDworld_related/v-hacd/build/linux2/test/testVHACD", type=str, help = "The path to the vhacd generating executable file testVHACD")
    parser.add_option("-o", "--portn", dest="portn", default = 22334, type=int, help = "The port for mongodb connected to the database on dicarlo5")
    parser.add_option("-n", "--processn", dest="processn", default = 2, type=int, help = "The number of processes used to compute the VHACD files")
    parser.add_option("-t", "--type", dest="type", default = "dosch", type=str, help = "The value of the variable type to be set in mongodb")
    parser.add_option("-e", "--version", dest="version", default = 1, type=int, help = "The value of the variable version to be set in mongodb")
    parser.add_option("-m", "--mapn", dest="mapn", default = 10, type=int, help = "The number in each division used to compute the VHACD files")
    parser.add_option("--forcevhacd", dest="forcevhacd", default = 0, type=int, help = "0 for judging whether use the current wrl files, 1 for generating anyway")
    parser.add_option("--forcebundle", dest="forcebundle", default = 0, type=int, help = "0 for judging whether use the current bundles in the dataset, 1 for generating and uploading anything anyway")
    parser.add_option("-a", "--tmpname", dest="tmpname", default = "~tmp_id.txt", type = str, help = "The temporary file for storing path, _id")
    parser.add_option("-u", "--unity", dest="unity", default = "/opt/Unity/Editor/Unity", type = str, help = "The path of unity")
    parser.add_option("--tmpnameunity", dest="tmpnameunity", default = "~tmp_info.txt", type = str, help = "The temporary file for storing information extracted")
    parser.add_option("--url", dest = "urlPrefix", default = "http://threedworld.s3.amazonaws.com/", type = str, help = "The url prefix for aws_address")
    parser.add_option("--parallel", dest = "parallel", default = 0, type = int, help = "Flag whether we should create a new project, put the assetbundles there, and compute there, which would make the computation parallelable")
    parser.add_option("--emptyproject", dest = "emptyproject", default = "/home/chengxuz/ThreeDWorld/ServerTools/cmd_related/empty_project", help = "The absolute path to the empty project template")

    (options, args) = parser.parse_args()

    ### For parallel computing, copy the empty project and then copy the 
    if options.parallel==1:
        if os.path.exists(options.projectdir):
            #os.system('rm -r ' + options.projectdir)
            pass
        os.system("cp -r " + options.emptyproject + " " + options.projectdir)
        os.system("rm -r " + os.path.join(options.projectdir, "Assets/Scripts"))
        os.system("cp -r ../Assets/Scripts " + os.path.join(options.projectdir, "Assets/Scripts"))
        os.system("cp -r "+ options.inputdir + " " + os.path.join(options.projectdir, "Assets/Models/script_gen"))
        os.system("mkdir -p " + os.path.join(options.projectdir, 'ServerTools'))

        options.inputdir    = "Assets/Models/script_gen"

    conn = pymongo.MongoClient(port=options.portn)
    coll = conn['synthetic_generative']['3d_models']

    ### Generate the wrl files using VHACD
    file_list = get_file_list()

    '''
    if options.parallel==1:
        print(len(file_list))
        exit()
    '''

    #print(len(file_list))

    if options.forcevhacd==0:
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
    if options.forcebundle==0:
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
    print(test_coll.count())
    exist_doc_list  = list(test_coll[:])
    for exist_doc in exist_doc_list:
        if exist_doc['obj_file_path'] in file_list:
            _id_dict[exist_doc['obj_file_path']]    = str(exist_doc['_id'])

    if len(file_list)==0:
        exit()

    #print(_id_dict)
    if options.parallel==0:
        fout    = open(options.tmpname, 'w')
    else:
        fout    = open(os.path.join(options.projectdir, "ServerTools", options.tmpname), 'w')

    for file_name in file_list:
        if file_name in _id_dict:
            fout.write(file_name[len(options.projectdir)+1:] + "," + _id_dict[file_name] + '\n')
    fout.close()
    #exit()

    ### Run the unity command 

    cmd_tmp_unity   = "%s -batchmode -quit -projectPath %s -executeMethod CreatePrefabCMD.CreatePrefabFromModel_script -nographics -inputFile %s -outputFile %s"

    cmd_str         = cmd_tmp_unity % (options.unity, options.projectdir, "ServerTools/" + options.tmpname, "ServerTools/" + options.tmpnameunity)
    print("Run " + cmd_str)
    os.system(cmd_str)

    ### Upload to aws server and update the information in mongodb

    upload_cmd_tmp  = "s3cmd put --acl-public --guess-mime-type %s s3://threedworld/"
    bundle_prefix   = os.path.join(options.projectdir, 'Assets/PrefabDatabase/AssetBundles/Separated/')

    if options.parallel==0:
        fin     = open(options.tmpnameunity, 'r')
    else:
        fin     = open(os.path.join(options.projectdir, "ServerTools", options.tmpnameunity), 'r')
    lines   = fin.readlines()
    all_ids     = _id_dict.values()

    for line in lines:

        split_lines     = line.split(',')
        file_now        = split_lines[0]
        id_current      = file_now.split('.')[0]
        cur_complexity  = int(split_lines[1])
        center_pos      = get_pos(split_lines[2:5])
        boundb_pos      = get_pos(split_lines[5:8])
        isLight         = split_lines[8]
        anchor_type     = split_lines[9][:-1]

        if not id_current in all_ids:
            continue

        curr_url        = options.urlPrefix + file_now
        print(curr_url)

        curr_path       = bundle_prefix + file_now
        curr_cmd_str    = upload_cmd_tmp % (curr_path)
        print(curr_cmd_str)
        os.system(curr_cmd_str)

        coll.update_one({
            '_id': ObjectId(id_current)
        },{
          '$set': {
            'complexity': cur_complexity,
            'center_pos': center_pos,
            'boundb_pos': boundb_pos,
            'isLight': isLight,
            'anchor_type': anchor_type,
            'aws_address': curr_url
          }
        }, upsert=True)

    fin.close()
    if options.parallel==0:
        os.system('rm ' + options.tmpname)
        os.system('rm ' + options.tmpnameunity)
