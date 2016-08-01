#QUEUE SYSTEM:
So at the beginning of our network training programs, we need a way to connect to the environment server and send and receive our messages back and forth. To do this, we have a particular script which will manage all the instances of the ThreeDWorlds as we make. This script will be bound to port number 23402 on any given machine that we are using to run environments. However, to make things even more straightforward, there is a python library written called tdw_client which will auto-connect you to the queue, and allow you to use a small selection of commands to either examine the current processes running on the node, reconnect to an environment process, or create a new process. All of these commands will be in the class TDW_Client. So the following would be a typical use of tdw_client:

	from tdw_client import TDW_Client
	import zmq

	tc = TDW_Client(“...some ip...”)
	tc.load_config({...some config file stuff...})
	sock = tc.run()

All that takes place here is that you make an instance of a client, load in a config file in the event that we try to make an environment, and then get a zmq socket back when we run the client module. There are various parameters you can set when initializing the client:
		
	queue_port_num = <str> 
_this defaults to 23402, which is also the default port number the queue will bind to. unless you know for certain the queue is running on a different port, leave this one alone._
	
	environment_config = <dict>
_this is default left as {“environment\_scene” : “Empty”}, as an environment cannot be created without a config file at least mentioning what scene. (see the methods section below for changing the client config file post initialization if you say wanted to create a new environment for the client with a different config, or wanted to run a reset scene with a different config)._

	debug = <bool>
_this defaults to False, but when true will give you important network and message info._


	selected_build = <str>
_this doesn’t have a default, when left blank, you will be required to select a build from the available builds on the server using a menu, if you want to skip this UI step, just set this to the name of the binary file ‘\<build\_name\>.x86_64’_

	initial_command = <str>
_this doesn’t have a default, when left blank or invalid, you will be required to select available commands from a menu. You can type in either “request\_create_environment”, “request_join_environment”, or “request_active_environments”. Whichever command you type, the client will start by running this command._

	requested_port_num = <int>
_this doesn’t have a default, when left blank or invalid, you will be required to type in a port number in the UI, or request to randomly select an available port._

	username = <str>, description = <str>
_for sanity’s sake, a username and description will be required of all environments. either leave this blank and type it in the UI, or fill it out in here._

	num_frames_per_msg = <int>
_a number greater than 1 that equals the number of frames you expect back. defaults to 4_

	get_obj_data = <bool>
_defaults to false. determines whether you want object data or not, it comes in a list_

	send_scene_info = <bool>
_defaults to false, determines whether or not to send scene info_

If you want to avoid using the tdw_client UI, you could do something like the following:

	from tdw_client import TDW_Client
	import zmq

	host_addr = “86.7.5.309”
	port_num = “4321”
	sel_build = “tdw-1.0.0b”
	sel_command = “request_create_environment”
	username = “Joe Smith”
	description = “Running a non-trivial normals prediction test”
	config = {“environment_scene” : “ProceduralGeneration”, ...}
	tc = TDW_Client(host_address,
	requested_port_num=port_num,
	selected_build=sel_build,
	intial_command=sel_command,
	username=username,
	description=description,
	environment_config=config)
	sock = tc.run()

As a note, the above code will take you to the UI if that port number is taken. If that gets incredibly annoying, I will implement an error mode, but not sure how important that is given you will get the feedback in the console anyways, and you can kill the process with a ^C as well.

As for methods, I ask that you make use of the following:

	reconnect()
_Unsure what context to use this in, but it will reconnect you to the port number saved to the client. returns true if succeeds returns false if fails_

	load_config(<dict>)
_going to say this again, when you make a config, it MUST have an “environment\_scene” key in order to work, the code will give you an angry error if you don’t include one. beyond that, check out the configs section for more info on this subject._

	print_environment_output_log()
_I have yet to implement this, but eventually what it will do is just print the environment output\_log to console. Will spec this out amongst other related things before confirming this super convenient function._


#CONFIGS:
To start, here is an example of a config in python syntax:

	{
	"environment_scene" : "ProceduralGeneration", #THIS MUST BE IN YOUR CONFIG FILE
	"random_seed": 1, # Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
	"should_use_standardized_size": False,
	"standardized_size": [1.0, 1.0, 1.0],
	"disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], # A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
	"permitted_items": [], #["bed1", "sofa_blue", "lamp"],
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
	"max_placement_attempts": 300,   # Maximum number of failed placements before we consider a room fully filled.
	"grid_size": 0.4,    # Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the more disorderly objects can look.
	}


Okay, so looking through this, we can see that config files are json files. Of special note, we need to observe that the key “environment_scene” must be inside the config file, or the unity program will default to making an empty environment and make a complaint in its output log.

The next thing to check out is the random seed which can be used to control the seed deciding random actions in the environment, i.e. where objects get placed and how they get placed.

All the seeds excluding “environment_scene”, are all customizable. If you were to write a different environment, you could create a totally different set of keys to expect from the avatar. The base unity code will not care what kind of things you throw into the json config file, so long as you can retrieve them in C# (as a note, make sure this is actually possible as for some special or custom classes, it may actually not be).

Ok so let’s get a little more specific. There are no requirements for any given unity scene to be an environment scene type. Even an empty scene will meet the requirements. However, if you want objects in your generated environment you will have to create this in one of two ways:

METHOD 1:
	You can create fixed scenes by inserting objects using the GUI tool, Unity Editor. Clicking and dragging in objects, and adjusting their transforms can all be done without even writing a single line of code. You can insert scripts wherever needed, but a fixed scene is totally acceptable.



METHOD 2:
	You can create a scene that is entirely generated. Procedural Generation is a great example of this. The scene contains just a gameobject called Procedural Generation, which runs a script spawning other game objects randomly using data from the config file. You could also make a scene which generates objects in specified locations given information in the config file.

To make these environment scenes, the only requirement, is that they be saved under the path “Assets/Scenes/EnvironmentScenes/\<insert scene name\>.unity” in the ThreeDWorld Repo. This way the base scene can locate the scene. When building a new binary to contain your new environment, make sure to check the box labelled with your new scene, or it will not get added to the build. (To build a binary, go to File -> Build Settings, select Standalone, choose Type: Linux, and only check .x86_64 with none of the check boxes marked [Special Note: Linux binaries must be built on a Mac or Windows system and rsync’ed or an equivalent on to the environment node]). IMPORTANT: please name the builds in the following format:

	TDW-v3.2.0b07

Where b is for beta which can also be substituted with a for alpha. Small bug fixes should increment the beta or alpha counter, big fixes or feature additions should reset the alpha or beta counter and increment the third counter, if major changes are made, increment the second, and use judgement for the first counter. Point of all this is, let's not have duplicate file names lying around in different directories.

SPECIAL ASSETS:
	There is a simple abstract script called SpawnArea. SpawnAreas are used to report locations for Avatars to attempt to spawn. Feel free to write your own extensions of SpawnArea, or use premade prefabs containing SpawnArea extension components. Be sure to save any of the prefab SpawnAreas to the resources folder so the environment can locate and spawn them. (use Resources.Load\<SpawnArea\>(“Prefabs/\<insert name of prefab\>”) to acquire prefabs, and GameObject.Instantiate\<SpawnArea\>(prefab) to instantiate them)

The config file can be accessed as a JsonData file under SimulationManager.argsConfig. Be sure to import LitJson.JsonData to use.

#HOW TO UNITY:

So tragically, some of making scenes requires the use of the GUI. Luckily it isn’t very complex. Essentially to make a new environment scene, you will run File -> New Scene, save it in “Assets/Scenes/EnvironmentScenes”. Once you have an empty scene, the structure of making a scene is to drag and drop prefabs and meshes into the scene editor, or right click on the heirarchy menu and create new objects. Of particular interest, will be to run Create Empty, and to add components to the empty objects. You can attach scripts to the scene in this manner. Special note, these scripts will not be initialized via a constructor! Instead, unity has callback methods called start, awake, update, fixedUpdate, lateUpdate, etc. Start and Awake are used to initialize attributes to the script. The update methods are used as main loops. To see more as to when these methods get called, see the Unity API. Another important feature to objects, is their transforms. Transforms can be adjusted to change position, rotation, and scale. You can check out the Unity API to investigate other components that can be added to objects.

Prefabs, seemingly confusing subject, but surprisingly simple. Prefabs are hierarchies of objects which can be saved outside a scene. If you want two planes to be positioned to bisect each other, you can position them in the scene editor as so, drag one plane into the other plane in the hierarchy menu, and you will wind up creating a single object with sub parts. If you move the outermost object, the sub parts will move with it. You can run methods in a script to acquire information about children or parents in the hierarchy. This hierarchical object can be fairly powerful. The special thing you can do with said object structures in Unity, is that you can save such hierarchies (which can just be one object with no children by the way) as a file called a prefab. The prefab saves all of the information about the hierarchy and can reproduce it in any scene, any number of times.

#MESSAGE SYSTEM:
When communicating with the environment over zmq, you will always send a json with an entry n and msg. n contains your frame expectancy, and msg contains your actual message. msg will contain an entry msg_type i.e.
	
	{‘n’ : 4, “msg” : {“msg_type” : “CLIENT_INPUT”, ...}}

Here are the available message types and what you can put inside them:

CLIENT_INPUT - _for regular frame to frame client input, can do nothing_
	
	vel : [double, double, double] //velocity
	ang_vel : [double, double, double] //angular velocity
	teleport_random : bool //teleport next frame to a new randomly chosen location
	sendSceneInfo : bool //returns info about the scene
	get_obj_data : bool //returns a list of objects and info concerning them
	relationships : list //currently not being used
	actions : dict //for performing magic actions on objects
		ex. {
			id : str //as given from get_obj_data
			force : [double, double, double]
			torque : [double, double, double]
		    }

CLIENT_JOIN - _joining for an environment already up_
	
	N/A

CLIENT_JOIN_WITH_CONFIG - _joining and creating a new environment_
	
	config : dict //see config section for what to throw in here

SCENE_SWITCH - _creating a new environment, can be of the same kind as before_
	
	config : dict //see config section

and coming soon…
SCENE_EDIT - _for moving, duplicating, removing, and other kinds of world editing powers_

Beyond that, this is just a simple zmq REQ REP pattern, that starts with your client having 4 frames on queue. Send a message and then get another 4, and repeat.

Each set of four frames contains the following: A header, normals, objects, real image in that order. The header gives you the position, velocity, and of the avatar as well as object info and scene info on request. The images will be received as png’s by default but can be set to be bmp and can be accessed in python via Pillow’s Image class.

