# Using the repo

## Client tools requirements

- `zmq`
- `tabulate`
- `pick`

## Download

`git clone git@github.com:dicarlolab/ThreeDWorld.git`

## Update

`git pull`


# Interface with 3D enviroments

## Queue

At the beginning of our network training programs, we need a way to connect to the environment server and send and receive our messages back and forth. This is done by `ServerTools/tdq_queue.py` which manages all the instances of the ThreeDWorlds as we make. This script will be bound to port number 23402 (by default) on any given machine that we are using to run environments.

To make things more straightforward, use `ClientTools/tdw_client.py` which will auto-connect you to the queue, and allow you to use a small selection of commands to either examine the current processes running on the node, reconnect to an environment process, or create a new process.

## Start a new environment

```python
from tdw_client import TDW_Client
import zmq

# make an instance of a client
tc = TDW_Client('18.93.5.202',
				initial_command='request_create_environment',
				selected_build='TDW-v1.0.0b05.x86_64',  # or skip to select from UI
				get_obj_data=True,
				send_scene_info=True
				)
tc.load_config({
	'environment_scene': 'ProceduralGeneration',
	# other options here
	})
sock = tc.run()  # get a zmq socket for sending and receiving messages
```

### Useful client methods:

- `load_config`: Loads configuration to the environment. See [Scene configuration options](#scene-configuration-options)

- `load_profile`

- `reconnect`: Reconnects you to the port number saved to the client. Returns True if succeeds, returns False if fails.

- `print_environment_output_log`: Prints the environment's output log to console. NOTE: Not implemented yet.


## Scene configuration options

```python

	{
	"environment_scene" : "ProceduralGeneration",  # THIS MUST BE IN YOUR CONFIG FILE
	"random_seed": 1,  # Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
	"should_use_standardized_size": False,
	"standardized_size": [1.0, 1.0, 1.0],
	"disabled_items": [],  # ["SQUIRL", "SNAIL", "STEGOSRS"], # A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
        "permitted_items": ["bed1"] , #[],["bed1", "sofa_blue", "lamp"]
        "scale_relat_dict": {"bed1": {"option": "Absol_size", "scale": 10}},  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
	"complexity": 7500,
	"num_ceiling_lights": 4,
	"minimum_stacking_base_objects": 15,
	"minimum_objects_to_stack": 100,
	"room_width": 10.0,
	"room_height": 20.0,
	"room_length": 10.0,
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
	"grid_size": 0.4,  # Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the more disorderly objects can look.
	}
```

Okay, so looking through this, we can see that config files are json files. Of special note, we need to observe that the key `'environment_scene'` must be inside the config file, or the unity program will default to making an empty environment and make a complaint in its output log.

The next thing to check out is the random seed which can be used to control the seed deciding random actions in the environment, i.e. where objects get placed and how they get placed.

All the seeds excluding `'environment_scene'`, are all customizable. If you were to write a different environment, you could create a totally different set of keys to expect from the avatar. The base unity code will not care what kind of things you throw into the json config file, so long as you can retrieve them in C# (as a note, make sure this is actually possible as for some special or custom classes, it may actually not be).

### About scaling options

In the example config message, there is one dictionary called "scale\_relat\_dict". This dictionary is used to tell the ProceduralGeneration how to scale every object. The key values could be the same as names in permitted\_items or the original filename of that object. Three options are provided:

- "Absol\_size": 
        The object in the scene would be resized to make the longest axis of that to be the "scale" sent in; 
- "Fract\_room" (__NOT Implemented!!!__): 
        The object in the scene would be resized to make the longest axis of that to be the "scale"\*"longest axis of the room"
- "Multi\_size":
        Just multiple the native size by "scale"

## Creating new `enviroment_scene`

There are no requirements for any given Unity scene to be an environment scene type. Even an empty scene will meet the requirements. However, if you want objects in your generated environment you will have to create this in one of two ways:

- METHOD 1:
	You can create fixed scenes by inserting objects using the GUI tool, Unity Editor. Clicking and dragging in objects, and adjusting their transforms can all be done without even writing a single line of code. You can insert scripts wherever needed, but a fixed scene is totally acceptable.

- METHOD 2:
	You can create a scene that is entirely generated. Procedural Generation is a great example of this. The scene contains just a gameobject called Procedural Generation, which runs a script spawning other game objects randomly using data from the config file. You could also make a scene which generates objects in specified locations given information in the config file.

To make these environment scenes, the only requirement is that they be saved under the path `'Assets/Scenes/EnvironmentScenes/<insert scene name>.unity'` in the ThreeDWorld Repo. This way the base scene can locate the scene. When building a new binary to contain your new environment, make sure to check the box labelled with your new scene, or it will not get added to the build.

To build a binary:
 - *File -> Build Settings*
 - select *Standalone*
 - choose *Type: Linux*
 - only check *.x86_64* with none of the check boxes marked

 **Special Note:** Linux binaries must be built on a Mac or Windows system and rsync’ed or an equivalent on to the environment node.

 **IMPORTANT:** please name the builds in the following format: `TDW-v3.2.0b07`, where *b* is for beta which can also be substituted with *a* for alpha. Small bug fixes should increment the beta or alpha counter, big fixes or feature additions should reset the alpha or beta counter and increment the third counter, if major changes are made, increment the second, and use judgement for the first counter. Point of all this is, let's not have duplicate file names lying around in different directories.

Special assets: There is a simple abstract script called SpawnArea. SpawnAreas are used to report locations for Avatars to attempt to spawn. Feel free to write your own extensions of SpawnArea, or use premade prefabs containing SpawnArea extension components. Be sure to save any of the prefab SpawnAreas to the resources folder so the environment can locate and spawn them. (use `Resources.Load<SpawnArea>('Prefabs/<insert name of prefab>')` to acquire prefabs, and `GameObject.Instantiate<SpawnArea>(prefab)` to instantiate them)

The config file can be accessed as a JsonData file under `SimulationManager.argsConfig`. Be sure to import `LitJson.JsonData` to use.

On `dicarlo-3d0world-editor.mit.edu`, the builds are placed under `/home/threed/builds/v1.0.0beta/v1.0.0b06`.

## Starting server side

1. Start mongo: `mongod -port 23502`
2. If done remotely and Ubuntu display manager server is running (e.g., a monitor is connected to that machine):

    - `sudo service lightdm stop`
    - `sudo nvidia-xconfig -a --use-display-device=None --virtual=1280x1024`
    - `sudo /usr/bin/X :0`
    - (To go the opposite way, kill the X server and restart lightdm -- you might have to reboot(?))
3. Start queue: `cd ServerTools && sudo python tdw_queue.py`

Output log is then generated in `ServerTools/output_log.txt`.

## Sending and receiving messages

When communicating with the environment over `zmq`, you will always send a json with `n` and `msg`. `n` contains your frame expectancy, and `msg` contains your actual message. `msg` will contain an entry `msg_type`, i.e.

```python
	{‘n’ : 4, “msg” : {“msg_type” : “CLIENT_INPUT”, ...}}
```

Here are the available message types and what you can put inside them:

- `CLIENT_INPUT` - for regular frame to frame client input, can do nothing

	vel : [double, double, double] //velocity
	ang_vel : [double, double, double] //angular velocity
	teleport_random : bool //teleport next frame to a new randomly chosen location
	send_scene_info : bool //returns info about the scene
	get_obj_data : bool //returns a list of objects and info concerning them
	relationships : list //currently not being used
	actions : dict //for performing magic actions on objects
		ex. {
			id : str //as given from get_obj_data
			force : [double, double, double]
			torque : [double, double, double]
		    }

- `CLIENT_JOIN` - joining for an environment already up

	N/A

- `CLIENT_JOIN_WITH_CONFIG` - joining and creating a new environment

	config : dict //see config section for what to throw in here

- `SCENE_SWITCH` - creating a new environment, can be of the same kind as before

	config : dict //see config section
	
		
		sock.send_json({'n': 4, 'msg' : {"msg_type" : "SCENE_SWITCH", 
	        'config': {"environment_scene" : "ProceduralGeneration"}  # need at leastthis one!
	                  }})
	        

- `SCENE_EDIT` - for moving, duplicating, removing, and other kinds of world editing powers (NOTE: not implemented yet)

Beyond that, this is just a simple zmq REQ REP pattern, that starts with your client having 4 frames on queue. Send a message and then get another 4, and repeat.

Each set of four frames contains the following: A header, normals, objects, real image in that order. The header gives you the position, velocity, and of the avatar as well as object info and scene info on request. The images will be received as png’s by default but can be set to be bmp and can be accessed in python via Pillow’s Image class.


# Using Unity

So tragically, some of making scenes requires the use of the GUI. Luckily it isn’t very complex. Essentially to make a new environment scene, you will run File -> New Scene, save it in “Assets/Scenes/EnvironmentScenes”. Once you have an empty scene, the structure of making a scene is to drag and drop prefabs and meshes into the scene editor, or right click on the heirarchy menu and create new objects. Of particular interest, will be to run Create Empty, and to add components to the empty objects. You can attach scripts to the scene in this manner. Special note, these scripts will not be initialized via a constructor! Instead, unity has callback methods called start, awake, update, fixedUpdate, lateUpdate, etc. Start and Awake are used to initialize attributes to the script. The update methods are used as main loops. To see more as to when these methods get called, see the Unity API. Another important feature to objects, is their transforms. Transforms can be adjusted to change position, rotation, and scale. You can check out the Unity API to investigate other components that can be added to objects.

Prefabs, seemingly confusing subject, but surprisingly simple. Prefabs are hierarchies of objects which can be saved outside a scene. If you want two planes to be positioned to bisect each other, you can position them in the scene editor as so, drag one plane into the other plane in the hierarchy menu, and you will wind up creating a single object with sub parts. If you move the outermost object, the sub parts will move with it. You can run methods in a script to acquire information about children or parents in the hierarchy. This hierarchical object can be fairly powerful. The special thing you can do with said object structures in Unity, is that you can save such hierarchies (which can just be one object with no children by the way) as a file called a prefab. The prefab saves all of the information about the hierarchy and can reproduce it in any scene, any number of times.

## Debugging

- With the Unity GUI open, you need to first open the BaseScene (by default, an untitled scene is up). To get the BaseScene loaded, you need to go to scenes in the project explorer tab and double click the scene.
- Once its loaded, it will show at the top of the hierarchy tab that you have BaseScene instead of Untitled.
- From there you will want to go to a terminal instance and go to `ThreeDWorld/ServerTools`.
- Now you hit play on the Monodevelop app, followed by play in Unity, followed by running `python tdw_editor_mode_client.py`
- The Python script essentially just connects to port 5556 where the GUI process is set to bind, makes a procedural generation scene and loops 40 calls to teleport, followed by a scene switch until you quit either the unity process or the Python process.
- In a proper outcome, when you hit play a scene called Empty will be loaded along with BaseScene. When you run the Python script, the Unity app will pause for a while and then have switched out Empty with ProceduralGeneration. At which point you would watch it cycle through a bunch of generated images.
- If there's an error, you can go to the console tab to see what went wrong.

## Importing objects to Unity

### Metadata

Currently, we mostly use ShapeNet v2 objects. Their metadata has been extracted from the metadata distributed with ShapeNet and is available on our MongoDB:

```python
import pymongo
conn = pymongo.MongoClient(port=22334)
coll = conn['synthetic_generative']['3d_models']
coll.find({'type': 'shapenet', 'version': 2})[0]
```

```
{u'_id': ObjectId('57b31b77f8b11f6bc2b94a7b'),
 u'front': [0.0, -1.0, 0.0],
 u'has_texture': False,
 u'id': u'63c80a4444ba375bc0fa984d9a08f147',
 u'keywords': [u'laser_printer'],
 u'name': u'Desktop laser printer',
 u'shapenet_synset': u'n04004475',
 u'source': u'3dw',
 u'synset': [u'n03643737'],
 u'synset_tree': [u'n03575240',
  u'n04004475',
  u'n00001740',
  u'n03183080',
  u'n03280644',
  u'n00021939',
  u'n03699975',
  u'n00003553',
  u'n00001930',
  u'n03643737',
  u'n00002684'],
 u'type': u'shapenet',
 u'upright': [0.0, 0.0, 1.0],
 u'version': 2}
```

NOTE: ShapeNet v1 entries are available by filtering for `{'info.version': 1}`.

### Downsampling objects

The first step is creating downsampled near-convex decompositions of objects. It turns out that when objects are colliding, usually some very crude approximation of them is used (like a cylinder) to compute when they collide. This is done to speed up computations but is not accurate enough for our purposes. We need physics to work properly in the 3D world. Using the precise object mesh would be too time-consuming so a compromise is to produce a downsampled mesh of the object which would still be sufficiently accurate to compute collisions but also small enough to do this fast.

1. Download V-HACD to produce these meshes: `https://github.com/kmammou/v-hacd`
2. A compiled version is available here: `sebastian-sandbox.mit.edu:/home/yamins/v-hacd/build/linux2/test/testVHACD`
3. An example script that shows the parameters and which parallelizes the conversion process:

    ```python
    import os
    import multiprocessing
    
    from yamutils import basic
    
    
    VHACD = '/home/yamins/v-hacd/build/linux2/test/testVHACD'
    
    def do_it(ind):
    
        cmdtmpl1 = '%s --input "%s" --output "%s" --log log.txt --resolution 500000 --maxNumVerticesPerCH 255'
        cmdtmpl2 = '%s --input "%s" --output "%s" --log log.txt --resolution 16000000 --concavity 0.001 --maxNumVerticesPerCH 255 --minVolumePerCH 0.0001'
    
        L = filter(lambda x: x.endswith('.obj'), basic.recursive_file_list('.'))
        L.sort()
    
        objfiles = L[ind * 100: (ind+1)*100]
    
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
    ```

Running this will produce .wrl files for each input obj. The wrl file format is the for VRML ("virtual reality markup language"), which is different from the Wavefront OBJ format that is the input to the algorithm. 

### Importing objects to Unity

Drag and drop the folder with you objects to *Assets/Models*. **NOTE:** This is a slow process on the order of ~5 sec / object.

### Generating prefabs [_Not in use?_]

First, click the *Play* button in Unity. Why? It starts memory management processes which are necessary for generating complicated scenes but also help when you are working with thousands of objects. Normally generating prefabs for a couple of objects does not require memory management so it is not started when you ask for prefabs. So next click *Procedural Generation / Create prefab model folders*. **NOTE:** This is a very slow process.

**IMPORTANT ISSUES:** It is likely that with each update to Unity all models need to be reimported again...

### For assetbundle

Three steps needed to generate assetbundles from raw .obj files. 

#### Generating prefabs

Similarly, enter the _Play_ mode if speed is desired. Before clicking *Prefab Database/Create Prefabs*, selet those objects folders (the selected folder should contain .obj files directly, e.g. not in its subfolders) (usually the .obj files would be put under _Assets/Models/_). **NOTE:** The generating process is quite slow currently (less than 200/hr) and it seems that selecting lots of objects to create prefabs would make it even slower (it takes 10 hours to generate 500 objects after selecting nearly 2000 objects).

#### Generating assetbundles

The generated prefabs would be put under _Assets/PrefabDatabase/GeneratedPrefabs/_. After selecting those folders needed (similarly, the selected folder should contain the prefabs directly), click *Prefab Database/Build Assetbundles/Separate* to generate assetbundles for those prefabs. The generated assetbundle files could be found under _Assets/PrefabDatabase/AssetBundles/Separated/_.

#### Uploading assetbundles to AWS S3 for Loadfromcacheordownload

All the uploaded assetbundles are stored under bucket _threedworld_. Currently, the access permission is readonly for everyone. To upload it and make it readonly for everyone, one could use the following commands (s3cmd required, and of course, credentials for aws needed):

```
s3cmd put --acl-public --guess-mime-type yourbundlename.bundle s3://threedworld/
```

After uploading, one could append the http url to "Assets/PrefabDatabase/list\_aws.txt".

#### Setting up assetbundles

Just clicking *Prefab Database/Setup Bundles* (if you also want to load the remote assetbundles, please click "Play" before setting up, as Loadfromcacheordownload only works in Play mode). Unity would examine all available bundle files under _Assets/PrefabDatabase/AssetBundles/Separated/_ and remote bundle files from "Assets/PrefabDatabase/list\_aws.txt". **NOTE:** All the assetbundles would be loaded for checking and taking needed information. So this process could be slow depending on how many bundle files there (especially the remote bundle files!). The list of files would be stored in "prefabs" of _Assets/ScenePrefabs/PrefabDatabase_.

# License

Apache 2.0
