"""
In order to connect to MongoDB on dicarlo5 server create an ssh tunnel using the
 command below:
 ssh -f -N -L 22334:localhost:22334 bashivan@dicarlo5.mit.edu

"""

from __future__ import print_function
import zmq
import sys
# sys.path.insert(0, '/Users/pouyabashivan/Dropbox (MIT)/Codes/Downloads/ThreeDWorld/ClientTools')
import ast
import argparse
from tdw_client import TDW_Client
from StringIO import StringIO
from PIL import Image
import time
import logging
import numpy as np
import time
import h5py
import re
import pymongo as pm
from nltk.corpus import wordnet as wn
import matplotlib.pyplot as plt
import matplotlib.patches as patches


# Constants for image generation
DEF_NUM_SCENE_SW = 5
DEF_NUM_SCENE_IMG = 50
DEF_IMG_SIZE = [224, 224, 3]
DEF_h5_filename = '/Users/pouyabashivan/Dropbox (MIT)/test_file.hdf5'
DEF_OBJ_DISCARD_TH = 0.1  # Threshold value to discard objects in the image (as % of image occupied by each object)


class TDWGenerator(TDW_Client):
    def __init__(self, host_address,
                 world_objs_filename=None,  # Name of the file containing names of all objects existing in the world
                 queue_port_num="23402",
                 requested_port_num=None,
                 auto_select_port=True,
                 environment_config=None,
                 debug=True,
                 selected_build=None,
                 selected_forward=None,
                 initial_command="",
                 username=None,
                 description=None,
                 num_frames_per_msg=4,
                 get_obj_data=False,
                 send_scene_info=False):

        super(TDWGenerator, self).__init__(host_address,
                                           queue_port_num=queue_port_num,
                                           requested_port_num=requested_port_num,
                                           auto_select_port=auto_select_port,
                                           environment_config=environment_config,
                                           debug=debug,
                                           selected_build=selected_build,
                                           selected_forward=selected_forward,
                                           initial_command=initial_command,
                                           username=username,
                                           description=description,
                                           num_frames_per_msg=num_frames_per_msg,
                                           get_obj_data=get_obj_data,
                                           send_scene_info=send_scene_info)

        # Query object labels from Dicarlo5 MongoDB
        if world_objs_filename is not None:
            self.obj_labels_dic = self.read_world_objects_from_list(world_objs_filename)  # Taken from synset_tree

        logging.basicConfig(filename='image_generator_log.txt', filemode='w-', level=logging.INFO)


    def world_setup(self, config):
        """
        Setup the 3D world.
        :return: server response
        """

        self.load_config(config)
        self.load_profile({'screen_width': DEF_IMG_SIZE[0], 'screen_height': DEF_IMG_SIZE[1]})
        self.run()
        # Receive server response
        return self._rcv_imageset()


    def terminate_connection(self):
        self.sock.send_json({'n': 4, 'msg': {"msg_type": "TERMINATE"}})


    @staticmethod
    def read_world_objects_from_list(filename):
        """
        Creates a association table between objects IDs and lables (synset_tree) given a file containing
        the list of all objects in the world simulation.
        :param      filename: filename which contains the list of all objects existing in the world simulation.
        :return:    association table of IDs <-> labels
        """
        print('Reading world objects from the list...')
        obj_names = []
        p = re.compile('\w+[.]bundle')
        with open(filename, 'r') as f:
            for line in f:
                m = p.search(line)
                if m is not None:
                    obj_names.append(m.group()[:-7])

        # Access the Mongo dabase and read the synset tree labels
        try:

            db_client = pm.MongoClient(port=22334)
            table = db_client['synthetic_generative']['3d_models']
            cursor = table.find({'type': 'shapenet',
                                 'version': 2,
                                 'id': {'$in': obj_names}})
            obj_labels_dic = dict()  # Stores the table for id-synset_tree correspondence
            for doc in cursor:
                obj_labels_dic[doc['id']] = doc['synset_tree']
        except:
            print('Could not connect to DB. Create a SSH tunnel.')
            raise
        print('Finished reading object list! Table created.')
        db_client.close()
        return obj_labels_dic


    @staticmethod
    def create_obj_scale_dictionary(obj_names, cat_scales_dic):
        """
        Creates a dictionary containing association between objects in the list_aws file and their synset names given
        A dictionary of desired category<>scale associations. Category names can be retrieved from the 'shapenet_synset'
        field on the shapenet meta db. Example of such categories are:
        'airplane', 'bag', 'bathtub', 'train'

        :param obj_names: list containing the object names
        :param cat_scales_dic: A dictionary containing scale values for particualr class of objects
        :return: association dictionary with object names as keys
        """
        print('Getting synset offsets from MongoDB...')
        try:
            db_client = pm.MongoClient(port=22334)
            table = db_client['synthetic_generative']['3d_models']
            cursor = table.find({'type': 'shapenet',
                                 'version': 2,
                                 'id': {'$in': obj_names}})
            obj_scale_dic = dict()  # Stores the table for id-scale correspondence
            for doc in cursor:
                # print(doc['shapenet_synset'])
                offset = doc['shapenet_synset']
                synset_name = wn._synset_from_pos_and_offset(offset[0], int(offset[1:])).name()
                basic_name = synset_name.split('.')[0]  # Take the first part of the synset name
                if basic_name in cat_scales_dic:
                    obj_scale_dic["http://threedworld.s3.amazonaws.com/"+doc['id']+'.bundle'] = \
                        {'option': 'Multi_size', 'scale': cat_scales_dic[basic_name]}
                else:
                    obj_scale_dic["http://threedworld.s3.amazonaws.com/"+doc['id']+'.bundle'] = \
                        {'option': 'Multi_size', 'scale': 1}
        except:
            print('Could not connect to DB. Create a SSH tunnel.')
            raise
        print('Table created!')
        db_client.close()
        return obj_scale_dic


    @staticmethod
    def list_2_string(in_list):
        """
        Makes a string representation of a python list.
        Use this function when the variable is a list.
        :param in_list: input list
        :return: list variable in string format
        """
        return ','.join(in_list)  # This is much faster but can't be used for list of lists


    @staticmethod
    def var_2_string(in_var):
        """
        Makes a string representation of a python variable.
        Use this function when the variable is anything other than a list.
        :param in_var: input variable
        :return: variable in string format
        """
        return str(in_var)


    @staticmethod
    def string_2_list(in_string):
        """
        Restore the original list from its string representation.
        Use this function when the orignial variable is a list.
        :param in_string:
        :return: original list variable.
        """
        return in_string.split(',')


    @staticmethod
    def string_2_var(in_string):
        """
        Restore the original variable from its string representation.
        Use this function when the original variable is anything other than a list.
        :param in_string: original variable.
        :return:
        """
        return ast.literal_eval(in_string)


    def generate_random_image(self):
        """
        Generates a random image from the world.
        :return: server response
        """
        self.sock.send_json({'n': 4, 'msg': {"msg_type": "CLIENT_INPUT", "teleport_random": True,
                                             'sendSceneInfo': False,
                                             "ang_vel": np.random.uniform(low=-1, high=1, size=(3,)).tolist()
                                             }})
        return self._rcv_imageset()


    def _rcv_imageset(self):
        """
        Receive the imageset returned from the server.
        :return:
                server_server_response: A dictionary containg server response message
                containing observed objects, avatar, ...
                images: list of images([0]=normals, [1]=segmentations, [2]=image)
        """
        images = []
        # Receive server response
        server_response = self.sock.recv()
        if self.debug:
            print(server_response)
            for i in range(3):
                msg = self.sock.recv()
                images.append(np.array(Image.open(StringIO(msg)).convert('RGB')))
                print('Received message {0}/3'.format(i))
                if i == 2:  # Show the scene image
                    Image.open(StringIO(msg)).convert('RGB').show()
        else:
            logging.info('%' * 20 + ' Server Response ' + '%' * 20)
            logging.info(server_response)
            for i in range(3):
                msg = self.sock.recv()
                images.append(np.array(Image.open(StringIO(msg)).convert('RGB')))
        return ast.literal_eval(server_response), images


    def create_color_obj_table(self, image_info):
        """
        Creates a lookup table dictionary for color<>id correspondence.
        :param image_info: Scene info dictionary received from server.
        :return: dictionary cotaining the lookup table.
        """
        color_obj_table = dict()
        for row in image_info['sceneInfo']:
            m = re.match('http', row[0])
            if m is not None:
                m = re.search('http://\S+[.]bundle', row[0])
                color_obj_table[int(row[1], 16)] = m.group().split('/')[-1][:-7]
                # color_obj_table[row[1]] = m.group()[2:-1]
            else:
                color_obj_table[int(row[1], 16)] = row[0]
                # color_obj_table[row[1]] = row[0]
            self.image_info = image_info
        return color_obj_table


    def find_bbox(self, colors_image, color_num):
        """
        Computes the object bounding box given the label image.
        :param colors_image: label image containing color ids for each pixel.
        :param color_num: color id for the object of interest.
        :return: xy coordinates, width, height of bounding box
        """
        cols, rows = np.nonzero(colors_image == color_num)
        min_row = np.min(rows)
        max_row = np.max(rows)
        min_col = np.min(cols)
        max_col = np.max(cols)

        return (min_row, min_col), max_row-min_row, max_col-min_col


    def plot_bbox(self, image, bboxes):
        """
        Plots the received image and and the object bounding boxes.
        :param image: Image containing the objects.
        :param bboxes: Bounding boxes as: [(x, y), W, H]. This can be computed using find_bbox function.
        :return:    fig: figure handle
                    ax: axes handle
        """
        fig, ax = plt.subplots(2, 1, figsize=(20, 20))

        ax[0].imshow(image)
        ax[1].imshow(image)
        for bbox in bboxes:
            ax[1].add_patch(patches.Rectangle(bbox[0], bbox[1], bbox[2],
                                              color=np.random.rand(1, 3)[0],
                                              linewidth=3,
                                              fill=False))
        return fig, ax


    def process_image_objs(self, images, color_obj_table, discard_obj_th=DEF_OBJ_DISCARD_TH, mode='bbox'):
        """
        Check the pixel area for each object in a central square area of half the size of image and discards the image
        if now large object overlapping the central square can be found in the image.
        :param images: list of images, including [0]=normals [1]=labels [2]=image.
        :param color_obj_table: a dictionary containing color<>object ID correspondence
        :param discard_obj_th: Threshold for screening object sizes in the image
        :param mode: Mode of operation for screening object sizes in the image {'bbox': using object bounding boxes,
                        'pixel': using number of pixels occupied by each object on the screen.}
        :return:    accept_img: Flag for whether to accept image or not.
                    obj_ids: list of object IDs (names)
                    obj_color_codes: list of color codes associated with each object.
                    obj_pixel_counts: list of pixel counts associated with each object.
        """
        # Extract object IDs and color codes from image_info string
        # obj_ids = image_info['observed_objects'].keys()
        # obj_color_codes = [int(e, 16) for e in image_info['observed_objects'].values()]
        bboxes = []
        accept_img = False  # Flag for accepting the image
        labels_img_collapsed = images[1][:, :, 0] * 256 ** 2 + \
                               images[1][:, :, 1] * 256 + \
                               images[1][:, :, 2]
        unique_colors, unique_counts = np.unique(labels_img_collapsed, return_counts=True)
        # Retrieve object IDs corresponding to all unique colors codes
        unique_ids, nonexist_color_ind = [], []
        for i, color_id in enumerate(unique_colors):
            if color_id not in color_obj_table:
                logging.warning('Object color code {0} does not exist in scene info!'.format(color_id))
                nonexist_color_ind.append(i)
                continue
            unique_ids.append(color_obj_table[color_id])

        unique_ids = np.asarray(unique_ids)
        large_obj_ids = []
        if nonexist_color_ind:
            unique_colors = np.delete(unique_colors, nonexist_color_ind)
            unique_counts = np.delete(unique_counts, nonexist_color_ind)
        if mode == 'pixel':
            # obj_pixel_counts = []
            # for i in obj_color_codes:
            #     obj_pixel_counts.append(unique_counts[unique_ids == i])

            # Find objects of size larger than the threshold based on their pixel area
            large_obj_ids = unique_ids[(unique_counts / float(images[1].shape[0] * images[1].shape[1])) > discard_obj_th]

        elif mode == 'bbox':
            for color in unique_colors:
                bboxes.append(self.find_bbox(labels_img_collapsed, color))

                if bboxes[-1][1]*bboxes[-1][2] > discard_obj_th * float(images[1].shape[0] * images[1].shape[1]):    # Compute the area covered by the bbox
                    large_obj_ids.append(color_obj_table[color])
        else:
            raise ValueError("Unrecognized mode {'bbox', 'pixel'}")

        # Check if any of large non-standard object exists in the image
        for obj in large_obj_ids:
            if obj in self.obj_labels_dic:
                accept_img = True
                break

        # Setting the small object pixels to zero
        # tmp_image = labels_img_collapsed.flatten()
        # for i in small_obj_ids:
        #     tmp_image[tmp_image == i] = 0
        # images[1] = tmp_image.reshape(image_shape)

        return accept_img, unique_ids.tolist(), unique_colors.tolist(), unique_counts.tolist(), bboxes


    def extract_labels(self, obj_ids):
        """
        Looks up objects in the world objects table and extracts the labels for each object in the image.
        :param obj_ids: list of objects observed in the image.
        :return:    obj_labels: list of lists of object labels from synset_tree
                    valid_obj_flag: List of boolean values. True if the corresponding object is an object of interest.
        """
        obj_labels, valid_obj_flag = [], []
        for id in obj_ids:
            if id in self.obj_labels_dic:
                obj_labels.append(
                    self.obj_labels_dic[id]
                )
                valid_obj_flag.append(True)
            else:
                obj_labels.append(None)
                valid_obj_flag.append(False)
        return obj_labels, valid_obj_flag

    def switch_scence(self, config):
        """
        Send server request to switch scene.
        :return: server response
        """
        self.sock.send_json({'n': 4, 'msg': {"msg_type": "SCENE_SWITCH",
                                             "config": config,
                                             'send_scene_info': True,
                                             }})
        return self._rcv_imageset()


    def test_timing(self):
        pass


def main(args):
    time_start = time.time()
    # Receive command line args
    NUM_SCENE_SW = args.NUM_SCENE_SW
    NUM_SCENE_IMG = args.NUM_SCENE_IMG
    IMG_SIZE = args.IMG_SIZE
    h5_filename = args.HDF5_file
    obj_filename = args.obj_file
    # Open the HDF5 file
    h5_file = h5py.File(h5_filename, 'w')
    # Create datasets
    dt = h5py.special_dtype(vlen=unicode)  # Define Unicode variable length string type

    ###################################################################################################################
    # ds_images = h5_file.create_dataset('images',
    #                                    tuple([NUM_SCENE_SW * NUM_SCENE_IMG] + [reduce(lambda x,y: x*y, IMG_SIZE)]),
    #                                    dtype=np.uint8)
    # ds_images_segmentations = h5_file.create_dataset('images_segmentations',
    #                                                  tuple([NUM_SCENE_SW * NUM_SCENE_IMG] + [reduce(lambda x,y: x*y, IMG_SIZE)]),
    #                                                  dtype=np.uint8)
    # ds_images_normals = h5_file.create_dataset('images_normals',
    #                                            tuple([NUM_SCENE_SW * NUM_SCENE_IMG] + [reduce(lambda x,y: x*y, IMG_SIZE)]),
    #                                            dtype=np.uint8)
    ###################################################################################################################
    # Define HDF5 datasets
    ds_images = h5_file.create_dataset('images',
                                       tuple([NUM_SCENE_SW * NUM_SCENE_IMG] + IMG_SIZE),
                                       dtype='uint8')
    ds_images_segmentations = h5_file.create_dataset('images_segmentations',
                                                     tuple([NUM_SCENE_SW * NUM_SCENE_IMG] + IMG_SIZE),
                                                     dtype='uint8')
    ds_images_normals = h5_file.create_dataset('images_normals',
                                               tuple([NUM_SCENE_SW * NUM_SCENE_IMG] + IMG_SIZE),
                                               dtype='uint8')
    ds_ids = h5_file.create_dataset('ids',
                                    (NUM_SCENE_SW * NUM_SCENE_IMG,),
                                    dtype=dt)
    ds_labels = h5_file.create_dataset('labels',
                                       (NUM_SCENE_SW * NUM_SCENE_IMG,),
                                       dtype=dt)
    ds_obj_pixel_counts = h5_file.create_dataset('obj_pixel_counts',
                                                 (NUM_SCENE_SW * NUM_SCENE_IMG,),
                                                 dtype=dt)
    ds_color_codes = h5_file.create_dataset('color_codes',
                                            (NUM_SCENE_SW * NUM_SCENE_IMG,),
                                            dtype=dt)
    ds_image_info = h5_file.create_dataset('image_info',
                                           (NUM_SCENE_SW * NUM_SCENE_IMG,),
                                           dtype=dt)
    ds_valid_obj_flag = h5_file.create_dataset('valid_obj_flag',
                                               (NUM_SCENE_SW * NUM_SCENE_IMG,),
                                               dtype=dt)

    # Setup 3D world
    gen = TDWGenerator("18.93.5.202",
                       username="Pouya",
                       world_objs_filename=obj_filename,
                       description="Image_generator",
                       initial_command="request_create_environment",
                       selected_build="TDW-v1.0.0b09.x86_64",
                       get_obj_data=True,
                       send_scene_info=True,
                       debug=False
                       )
    # Check if the objects table exists
    assert gen.obj_labels_dic is not None, 'No world object table exists. Provide list of objects in the world.'

    obj_scale_dic = gen.create_obj_scale_dictionary(gen.obj_labels_dic.keys(),
                                                    {'train': 3,
                                                     'airplane': 3,
                                                     'bus': 3,
                                                     'motorcycle': 2,
                                                     'guitar': 3,
                                                     'chair': 3,
                                                     'sofa': 4
                                                     })

    # Setup the world with predefined configuration
    config = {
        "environment_scene": "ProceduralGeneration",
        "random_seed": 1,
        # Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
        "should_use_standardized_size": False,
        "standardized_size": [5.0, 5.0, 5.0],
        "disabled_items": [],
        # ["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
        "permitted_items": [""],  # [],["bed1", "sofa_blue", "lamp"]
        "scale_relat_dict": obj_scale_dic,
        "complexity": 20000,
        "num_ceiling_lights": 20,
        "minimum_stacking_base_objects": 20,
        "minimum_objects_to_stack": 40,
        "room_width": 50.0,
        "room_height": 25.0,
        "room_length": 50.0,
        "wall_width": 1.0,
        "door_width": 1.5,
        "door_height": 3.0,
        "window_size_width": 5.0,
        "window_size_height": 5.0,
        "window_placement_height": 5.0,
        "window_spacing": 10.0,  # Average spacing between windows on walls
        "wall_trim_height": 0.5,
        "wall_trim_thickness": 0.01,
        "min_hallway_width": 5.0,
        "number_rooms": 1,
        "max_wall_twists": 3,
        "max_placement_attempts": 300,  # Maximum number of failed placements before we consider a room fully filled.
        "grid_size": 0.4
        # Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
    }
    image_info, _ = gen.world_setup(config)
    scene_obj_colors_table = gen.create_color_obj_table(image_info)
    # We have a double loop to generate images. The outer loop restarts the scene with new objects using
    # SCENE_SWITCH option. The inside loop randomly moves the agent and generates images. It also checks
    # if objects exist in the image and discards objects which are occupying a region of the image smaller
    # than a threshold.
    image_num = 0
    discarded_images_count = 0
    print('start up time: {0}'.format(time.time() - time_start))
    time_start = time.time()
    image_times = []
    while image_num < NUM_SCENE_SW * NUM_SCENE_IMG:
        loop_start_time = time.time()
        if (image_num % NUM_SCENE_IMG == 0) & (image_num != 0):
            image_info, _ = gen.switch_scence(config)
            scene_obj_colors_table = gen.create_color_obj_table(image_info)
            # Remove the sceneInfo key for later storage in HDF5 file
            # del image_info['sceneInfo']
        _, imageset = gen.generate_random_image()
        image_times.append(time.time() - loop_start_time)
        # Check whether the image contains objects and discard the labels for objects which are too small.
        image_good, obj_ids, obj_color_codes, obj_pixel_counts = gen.process_image_objs(imageset,
                                                                                        scene_obj_colors_table)
        if image_good:
            # Extract the labels vector from segmentation image.
            obj_labels, obj_valid_flags = gen.extract_labels(obj_ids)

            # Images as flattened arrays
            # ds_images_normals[image_num], \
            # ds_images_segmentations[image_num],\
            # ds_images[image_num] = imageset[0].flatten(), imageset[1].flatten(), imageset[2].flatten()

            # Images as actual arrays
            obj_labels, obj_valid_flags = gen.extract_labels(obj_ids)
            ds_images_normals[image_num], \
            ds_images_segmentations[image_num], \
            ds_images[image_num] = imageset

            # Python dictionaries and lists should be stored as strings in HDF5 file.
            ds_labels[image_num] = gen.var_2_string(obj_labels)
            ds_ids[image_num] = gen.list_2_string(obj_ids)
            ds_obj_pixel_counts[image_num] = gen.var_2_string(obj_pixel_counts)
            ds_color_codes[image_num] = gen.var_2_string(obj_color_codes)
            ds_valid_obj_flag[image_num] = gen.var_2_string(obj_valid_flags)
            ds_image_info[image_num] = gen.var_2_string(image_info)
            image_num += 1
        else:
            logging.info('Discarded image ({0}).'.format(discarded_images_count + 1))
            discarded_images_count += 1
    print('Image times taken: {0}'.format(image_times))
    print('Total generation time: {0}'.format(time.time() - time_start))
    print('Average generation time: {0}'.format(np.mean(image_times)))
    print('Number of generated images: {0}'.format(image_num))
    print('Number of discarded images: {0}'.format(discarded_images_count))
    h5_file.close()
    gen.terminate_connection()
    print('Connection closed!')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Runs R-CNN on fMRI data.')
    parser.add_argument('obj_file', metavar='F', type=str,
                        help='Text file containing the list of all objects in the world simulation.')
    parser.add_argument('--NUM_SCENE_SW', dest='NUM_SCENE_SW', type=int,
                        help='Number of scene switches throughout the simulation.',
                        default=DEF_NUM_SCENE_SW)
    parser.add_argument('--NUM_SCENE_IMG', dest='NUM_SCENE_IMG', type=int,
                        help='Number of images to capture per scene.',
                        default=DEF_NUM_SCENE_IMG)
    parser.add_argument('--IMG_SIZE', dest='IMG_SIZE', type=int, nargs=3,
                        help='Image size (W, H, D).',
                        default=DEF_IMG_SIZE)
    parser.add_argument('--DISCARD_TH', dest='DISCARD_TH', type=int,
                        help='Object discard threshold as % of whole image.',
                        default=DEF_OBJ_DISCARD_TH)
    parser.add_argument('--HDF5_file', dest='HDF5_file', type=str,
                        help='HDF5 file to save the simulation results.',
                        default=DEF_h5_filename)
    main(parser.parse_args())

    # Test for storing list of strings in HDF5
    # import time
    #
    # our_list = ['n%08d' % i for i in range(10)]
    # our_string = ','.join(our_list)
    # start_time = time.time()
    # for i in range(10000):
    #     converted_list = our_string.split(',')
    # print('Elapsed time: {0}'.format(time.time() - start_time))
    #
    # our_string2 = str(our_list)
    # start_time = time.time()
    # for i in range(10000):
    #     converted_list = ast.literal_eval(our_string2)
    # print('Elapsed time: {0}'.format(time.time() - start_time))
