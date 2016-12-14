# Copy shapenet models, run the script_gen

import pymongo
import sys
import os
from optparse import OptionParser
import subprocess

if __name__ == "__main__":
    parser = OptionParser()
    parser.add_option("-n", "--indexn", dest="indexn", default =0, type=int)
    parser.add_option("-s", "--startn", dest="startn", default =0, type=int)
    parser.add_option("-l", "--lengthn", dest="lengthn", default = 1000, type=int)
    parser.add_option("-w", "--windowsflag", dest="windowsflag", default = 0, type=int)
    parser.add_option("-c", "--check", dest="check", default = 0, type=int) # For checking has_texture = True?

    (options, args) = parser.parse_args()
    #print(options.indexn)
    print(options.startn)
    #print(options.lengthn)

    conn = pymongo.MongoClient(port=22334)
    coll = conn['synthetic_generative']['3d_models']
    test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True})

    max_lengthn     = min(test_coll.count(), options.startn + options.lengthn)
    list_test_coll  = list(test_coll[:])

    if options.check:
        test_coll_  = coll.find({'type': 'shapenetremat', 'has_texture': True})
        print(test_coll_.count())
        list_coll_  = list(test_coll_[:])
        with_texture_dict   = {}
        for docu in list_coll_:
            with_texture_dict[docu['id']]   = 0


    if options.windowsflag==0: # 0 for kanefsky
        shapenet_prefix = '/mnt/data/threedworld_related/ShapeNetCore.v2'
        project_path    = '/home/chengxuz/test_empty_all/test_empty_project_' + str(options.indexn) 
        original_proj   = '/home/chengxuz/ThreeDWorld/ServerTools/cmd_related/empty_project'
        unity_path      = '/opt/Unity/Editor/Unity'
        #target_prefix   = '/home/chengxuz/ThreeDWorld/Assets/Models/sel_objs/subsample'
        target_prefix   = '/home/chengxuz/test_empty_all/temp_objs/now_objs_' + str(options.indexn)
        #bundle_path     = '/home/chengxuz/test_empty_all/bundle_all/'
    elif options.windowsflag==1: # 1 for windows
        shapenet_prefix = 'C:/Users/threed/Documents/shapenet/ShapeNetCore.v2'
        project_path    = 'C:/Users/threed/ThreeDWorld_related/test_empty_project/test_empty_project_' + str(options.indexn) 
        original_proj   = 'C:/Users/threed/ThreeDWorld_related/test_empty_project/test_empty_project '
        unity_path      = '"' + 'C:/Program Files/Unity/Editor/Unity.exe' + '"'
        #bundle_path     = 'C:/Users/threed/ThreeDWorld_related/test_empty_project/all_assetbundles'
    elif options.windowsflag==2: # 2 for my mac
        shapenet_prefix = '/mnt/data/threedworld_related/ShapeNetCore.v2'
        project_path    = '/Users/chengxuz/3Dworld/test_empty_project_' + str(options.indexn) 
        original_proj   = '/Users/chengxuz/3Dworld/ThreeDWorld/ServerTools/cmd_related/empty_project'
        unity_path      = '/Applications/Unity/Unity.app/Contents/MacOS/Unity'
        target_prefix   = '/Users/chengxuz/3Dworld/ThreeDWorld/Assets/Models/sel_objs/test_mine'
    elif options.windowsflag==3: # 3 for freud
        shapenet_prefix = '/home/chengxuz/data/threedworld_related/ShapeNetCore.v2'
        project_path    = '/home/chengxuz/test_empty_all/test_empty_project_' + str(options.indexn) 
        original_proj   = '/home/chengxuz/ThreeDWorld/ServerTools/cmd_related/empty_project'
        unity_path      = '/opt/Unity/Editor/Unity'
        target_prefix   = '/home/chengxuz/test_empty_all/temp_objs/now_objs_' + str(options.indexn)

    #target_prefix   = project_path + '/Assets/Models/sel_objs/temp_for_cmdRun'
    #target_bund_pre = project_path + '/Assets/PrefabDatabase/AssetBundles/Separated/'

    #os.system('cp -r ' + original_proj + project_path)

    if os.path.exists(target_prefix):
        os.system('rm -r ' + target_prefix)
    os.system('mkdir -p ' + target_prefix)

    number_new  = 0
    for index_tmp in range(options.startn, max_lengthn):
        shapenet_info_tmp = list_test_coll[index_tmp]
        if options.check:
            if shapenet_info_tmp['id'] in with_texture_dict:
                continue
        number_new  = number_new + 1
        new_path    = os.path.join(shapenet_prefix, shapenet_info_tmp['shapenet_synset'][1:], shapenet_info_tmp['id'])
        #print(new_path)
        os.system('cp -r ' + new_path + ' ' + target_prefix)
    print(number_new)
    
    #cmd_str     = "python script_obj2bundle.py --inputdir %s --projectdir %s --type shapenetremat --version 0 --unity %s --parallel 1 --emptyproject %s --forcebundle 1"
    cmd_str     = "python script_obj2bundle.py --inputdir %s --projectdir %s --type shapenetremat --version 0 --unity %s --parallel 1 --emptyproject %s --forcebundle 0"
    #cmd_str     = "python script_obj2bundle.py --inputdir %s --projectdir %s --type shapenettest --version 0 --unity %s --parallel 1 --emptyproject %s --forcebundle 1"
    now_cmd     = cmd_str % (target_prefix, project_path, unity_path, original_proj)
    print(now_cmd)
    os.system(now_cmd)
    os.system('rm -r ' + project_path)
