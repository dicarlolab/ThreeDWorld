# Upload the generated assetbundles to the AWS server

import sys
import os
from optparse import OptionParser

if __name__ == "__main__":
    parser = OptionParser()
    parser.add_option("-s", "--startn", dest="startn", default = 0, type=int)
    parser.add_option("-l", "--lengthn", dest="lengthn", default = 1000, type=int)
    parser.add_option("-n", "--whichn", dest="whichn", default = 0, type=int)
    parser.add_option("-t", "--stride", dest="stride", default = 1, type = int)

    (options, args) = parser.parse_args()

    if options.whichn==0:
        target_direc    = '/home/chengxuz/test_empty_all/bundle_all/'
    else:
        #target_direc    = '/home/chengxuz/test_empty_all/all_assetbundles/'
        target_direc    = '/home/chengxuz/test_empty_all/bundle_all/'
        target_file     = 'list_aws.txt'
        url_prefix      = 'http://threedworld.s3.amazonaws.com/'
        file_open       = open(target_file, 'a')

    file_list           = os.listdir(target_direc)
    len_file            = len(file_list)
    print(len_file)
    file_list.sort()

    upl_len     = min(len_file, options.startn + options.lengthn)

    for indx_tmp in range(options.startn, upl_len, options.stride):
        now_name    = file_list[indx_tmp]
        now_path    = os.path.join(target_direc, now_name)
        print(now_path + ' indx_tmp:' + str(indx_tmp))
        if options.whichn==0:
            cmd_str     = 's3cmd put --acl-public --guess-mime-type ' + now_path + ' s3://threedworld/'
            os.system(cmd_str)
        else:
            url_now     = url_prefix + now_name
            file_open.write(url_now + '\n')

    if options.whichn==1:
        file_open.close()
