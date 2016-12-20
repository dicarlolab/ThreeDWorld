# Action module

# moves the agent in the scene
def move(msg, velocity, angular_velocity):
    msg['msg']['vel'] = velocity
    msg['msg']['ang_vel'] = angular_velocity
    return msg

# lets the agent move objects in the scene
def push(msg, force, torque, obj, action_position):
    action['force'] = force
    action['torque'] = torque
    action['id'] = obj
    action['action_pos'] = action_position
    msg['msg']['actions'].append(action)
    return msg

# teleports the agent to a new random location
def teleport_random():
    msg['msg']['teleport_random'] = True
    return msg
 
class Action: 
    def __init__():
        print 'Action system not implemented yet'
