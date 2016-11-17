# combine the separately extracted information into one text file

import sys
import os
from optparse import OptionParser

if __name__ == "__main__":
    parser = OptionParser()
    parser.add_option("-s", "--startn", dest="startn", default = 0, type=int)
    parser.add_option("-l", "--lengthn", dest="lengthn", default = 1000, type=int)
    #parser.add_option("-n", "--whichn", dest="whichn", default = 0, type=int)
    parser.add_option("-m", "--maxn", dest="maxn", default = 32, type = int)

    (options, args) = parser.parse_args()

    all_file_line_list  = []

    output_path     = 'list_info_aws.txt'
    input_prefix    = '/home/chengxuz/test_empty_all/text_all/Info_for_'
    input_suffix    = '_' + str(options.lengthn) + '.txt'

    for indx_tmp in range(options.startn, options.maxn):
        now_input   = input_prefix + str(indx_tmp * options.lengthn) + input_suffix
        
        fin_tmp     = open(now_input, 'r')

        tmp_file_line_list  = fin_tmp.readlines()

        repeat_time     = 0
        for indx_tmp_in in range(len(tmp_file_line_list)-1):
            if tmp_file_line_list[indx_tmp_in]==tmp_file_line_list[indx_tmp_in+1]:
                repeat_time     = repeat_time + 1

        all_file_line_list.extend(tmp_file_line_list)
        print(len(tmp_file_line_list), now_input, repeat_time)

        fin_tmp.close()
    print(len(all_file_line_list))
    all_file_line_list.sort()
    repeat_time     = 0
    for indx_tmp_in in range(len(all_file_line_list)-1):
        if all_file_line_list[indx_tmp_in]==all_file_line_list[indx_tmp_in+1]:
            repeat_time     = repeat_time + 1
    print(repeat_time)
    print(all_file_line_list[:10])
    print(all_file_line_list[-10:])

    # Check the information is the same as the list_aws.txt 
    check_path      = 'list_aws.txt'
    fin_check       = open(check_path, 'r')
    all_lines_check = fin_check.readlines()
    fin_check.close()
    not_match       = 0

    for indx_check in range(len(all_lines_check)-1):
        name_info_tmp   = all_file_line_list[indx_check]
        name_chec_tmp   = all_lines_check[indx_check]

        name_info_tmp   = name_info_tmp.split(',')[0]
        if (name_info_tmp not in name_chec_tmp) and (name_info_tmp not in all_lines_check[indx_check+1]):
            not_match   = not_match + 1

    print(not_match)

    # Write to output_path

    fout    = open(output_path, 'w')

    for line in all_file_line_list:
        fout.write(line)
    fout.close()
