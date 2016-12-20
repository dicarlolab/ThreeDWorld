# Module environment

config = {
    "environment_scene" : "ProceduralGeneration",
    "random_seed": 1, #Omit and it will just choose one at random. Chosen seeds are output into the log(under warning or log level).
    "should_use_standardized_size": False,
    "standardized_size": [1.0, 1.0, 1.0],
    "disabled_items": [], #["SQUIRL", "SNAIL", "STEGOSRS"], // A list of item names to not use, e.g. ["lamp", "bed"] would exclude files with the word "lamp" or "bed" in their file path
    "permitted_items": [""] , #[],["bed1", "sofa_blue", "lamp"]
                "scale_relat_dict": {"http://threedworld.s3.amazonaws.com/46e777a46aa76681f4fb4dee5181bee.bundle": {"option": "Multi_size", "scale": 4}},  # option: "Absol_size", "Fract_room", "Multi_size"; TODO: implement "Fract_room"
    "complexity": 1000,
    "num_ceiling_lights": 4,
    "minimum_stacking_base_objects": 5,
    "minimum_objects_to_stack": 5,
    "room_width": 45.0,
    "room_height": 25.0,
    "room_length": 45.0,
    "wall_width": 1.0,
    "door_width": 1.5,
    "door_height": 3.0,
    "window_size_width": 5.0,
    "window_size_height": 5.0,
    "window_placement_height": 5.0,
    "window_spacing": 10.0,  #Average spacing between windows on walls
    "wall_trim_height": 0.5,
    "wall_trim_thickness": 0.01,
    "min_hallway_width": 5.0,
    "number_rooms": 1,
    "max_wall_twists": 3,
    "max_placement_attempts": 300,   #Maximum number of failed placements before we consider a room fully filled.
    "grid_size": 0.4    #Determines how fine tuned a grid the objects are placed on during Proc. Gen. Smaller the number, the
}

class Environment:
    
    def __init__(self):
        self.config = config

    def set_config(self, config):
        self.config = config
