import os
import multiprocessing

from yamutils import basic


divide_in   = 1
#VHACD = '/home/yamins/v-hacd/build/linux2/test/testVHACD'
VHACD       = '/Users/chengxuz/3Dworld/v-hacd/build/mac/test/testVHACD'
obj_dir     = ''

def do_it(ind):

    cmdtmpl1 = '%s --input "%s" --output "%s" --log log.txt --resolution 500000 --maxNumVerticesPerCH 64' 
    cmdtmpl2 = '%s --input "%s" --output "%s" --log log.txt --resolution 16000000 --concavity 0.001 --maxNumVerticesPerCH 64 --minVolumePerCH 0.0001' 
    
    L = filter(lambda x: x.endswith('.obj'), basic.recursive_file_list('/Users/chengxuz/3Dworld/ThreeDWorld/Assets/Models/dorsh_models/JobPoses'))
    L.sort()
    
    start_indx  = min(ind * divide_in, len(L))
    end_indx    = min((ind+1)*divide_in, len(L))
    #objfiles = L[ind * 100: (ind+1)*100]
    objfiles = L[start_indx: end_indx]
    
    for of in objfiles:
        print('FILE: %s' % of)
        wf = of[:-3] + 'wrl'
        cmd = cmdtmpl1 % (VHACD, of, wf)
        os.system(cmd)
        osize = os.path.getsize(of)
        wsize = os.path.getsize(wf)
        if osize > 100 * wsize:
            cmd = cmdtmpl2 % (VHACD, of, wf)
            os.system(cmd)
            

if __name__ == '__main__':
    #args = range(18)
    args = range(1)
    pool = multiprocessing.Pool(processes=2)
    r = pool.map_async(do_it, args)
    r.get()
    print('done')
       
