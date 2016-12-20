# Copy the project, import shapenet models, create the prefabs, and then transfer them into assetbundles

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

    (options, args) = parser.parse_args()
    #print(options.indexn)
    print(options.startn)
    #print(options.lengthn)

    conn = pymongo.MongoClient(port=22334)
    coll = conn['synthetic_generative']['3d_models']
    test_coll = coll.find({'type': 'shapenet', 'version': 2, 'has_texture':True})

    max_lengthn     = min(test_coll.count(), options.startn + options.lengthn)

    if options.windowsflag==0:
        shapenet_prefix = '/mnt/data/threedworld_related/ShapeNetCore.v2'
        project_path    = '/home/chengxuz/test_empty_all/test_empty_project_' + str(options.indexn) 
        original_proj   = '/home/chengxuz/test_empty_project '
        unity_path      = '/opt/Unity/Editor/Unity'
        bundle_path     = '/home/chengxuz/test_empty_all/bundle_all/'
    else:
        shapenet_prefix = 'C:/Users/threed/Documents/shapenet/ShapeNetCore.v2'
        project_path    = 'C:/Users/threed/ThreeDWorld_related/test_empty_project/test_empty_project_' + str(options.indexn) 
        original_proj   = 'C:/Users/threed/ThreeDWorld_related/test_empty_project/test_empty_project '
        unity_path      = '"' + 'C:/Program Files/Unity/Editor/Unity.exe' + '"'
        bundle_path     = 'C:/Users/threed/ThreeDWorld_related/test_empty_project/all_assetbundles'


    target_prefix   = project_path + '/Assets/Models/sel_objs/temp_for_cmdRun'
    target_bund_pre = project_path + '/Assets/PrefabDatabase/AssetBundles/Separated/'

    os.system('cp -r ' + original_proj + project_path)

    for index_tmp in range(options.startn, max_lengthn):
        shapenet_info_tmp = test_coll[index_tmp]
        new_path    = os.path.join(shapenet_prefix, shapenet_info_tmp['shapenet_synset'][1:], shapenet_info_tmp['id'])
        #print(new_path)
        os.system('cp -r ' + new_path + ' ' + target_prefix)
    
    #cmd_str         = unity_path + ' -batchmode -quit -projectPath ' + project_path + ' -executeMethod CreatePrefabCMD.CreatePrefabFromModel -nographics -logFile -outputDir Assets/Models/sel_objs/temp_for_cmdRun/'
    cmd_str         = unity_path + ' -batchmode -quit -projectPath ' + project_path + ' -executeMethod CreatePrefabCMD.CreatePrefabFromModel -nographics -outputDir Assets/Models/sel_objs/temp_for_cmdRun/'
    print(cmd_str)
    os.system(cmd_str)
    os.system('mv ' + target_bund_pre + '*.bundle ' + bundle_path)
    os.system('rm -r ' + project_path)
