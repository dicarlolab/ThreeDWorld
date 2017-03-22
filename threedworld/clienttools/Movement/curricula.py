'''Some curricula for ya'''

import actions.curious2 as curious2 # import make_new_batch

import numpy as np
#0: 3, 6, 50 - 130
#1: 3-5, 50 - 130
#2: 3-5, 50 - 100
simple_push = [
	('SINGLE_OBJECT', 
		'PUSHING_0', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 5), 'f_horiz_range' : range(50, 100), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		}
		)
]

simple_push_longer = [
	('SINGLE_OBJECT', 'PUSHING_0', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(8, 10), 'f_horiz_range' : range(50, 70), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		}),
]

simple_push_shorter = [
	('SINGLE_OBJECT', 'PUSHING_SHORT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		}),
]


#0 3-5, 20 - 60
#1 3-6, 25 - 60
#2 3-7

#maybe some more basic parabolic motion -- another category that's supposed to be more

simple_lift = [
	('SINGLE_OBJECT', 'LIFTING_0', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 7), 'f_horiz_range' : range(10), 'f_y_range' : range(25, 60), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]

#0 range 1-3
#1 range 1-2, 
#2 f_horiz 0-30,
#3 f_horiz 0-60m f_y 130 - 200

lift_short_fast = [
	('SINGLE_OBJECT', 'LIFTING_1', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]

#just 0

simple_rot = [
	('SINGLE_OBJECT', 
		'ROTATING_0', 
		{
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(15, 20), 'tor_y_range' : range(50, 200), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		})
]

#0: 3-6
#1: 3-5
#2: 3-10, f_horiz 30-50
#3: 3-10, f_horiz 40-70


#faster rotation (or rotation  is fine), slower push

push_rot = [
	('SINGLE_OBJECT', 'PUSH_ROT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 10), 'f_horiz_range' : range(40, 70), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		})
]

push_rot_slower = [
	('SINGLE_OBJECT', 'PUSH_ROT', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		'cut_if_off_screen' : 4
		})
]

#some more parabolic projectile structure

lift_push_rot = [
	('SINGLE_OBJECT', 'LIFTING_1', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(3, 7), 'f_horiz_range' : range(5, 10), 'f_y_range' : range(25, 60), 'tor_horiz_range' : range(10, 40), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]

lift_push_rot_parabolic = [
	('SINGLE_OBJECT', 'LIFTING_1', 
		{'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150), 'std_dev_ang' : np.pi / 6.}, 
		'wait' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}})
]


tab_push_long = [
	('PUSH_OFF_TABLE', 'TAB_PUSH_LONG', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(8, 10), 'f_horiz_range' : range(50, 70)},
		'cut_if_off_screen' : 4
		}),
]

tab_push_short = [
	('PUSH_OFF_TABLE', 'TAB_PUSH_SHORT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400)},
		'cut_if_off_screen' : 4
		}),
]


tab_lift = [
	('PUSH_OFF_TABLE', 'TAB_LIFTING_0', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20},
		 'random_init_rot' : True, 
		 'func' : curious2.make_constant_random_action_sequence, 
		 'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200)}})
]

tab_rot = [
	('PUSH_OFF_TABLE', 'ROTATING_0', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(15, 20), 'tor_y_range' : range(50, 200)},
		'cut_if_off_screen' : 4
		})
]


tab_push_rot = [
	('PUSH_OFF_TABLE', 'PUSH_ROT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150)},
		'cut_if_off_screen' : 4
		})
]

tab_lift_push_rot = [
	('PUSH_OFF_TABLE', 'TAB_LIFT_PUSH_ROT', 
		{'noisy_drop_std_dev' : .25, 'noisy_drop_trunc' : .5, 
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150)}
		})
]


tab_push_noshake = [
	('PUSH_OFF_TABLE', 'TAB_PUSHING_0', 
		{'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(5, 10), 'f_horiz_range' : range(50, 70), 'std_dev_ang' : np.pi / 6.},
		'cut_if_off_screen' : 4
		}),
]

tab_push_down_noshake = [
	('PUSH_OFF_TABLE', 'TAB_PUSHING_0', 
		{
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'random_init_rot' : True, 
		'func' : curious2.make_constant_random_action_sequence, 
		'kwargs' : {'time_len_range' : range(5, 10), 'f_horiz_range' : range(50, 70), 'f_y_range' : range(-100, -50), 'std_dev_ang' : np.pi / 6.},
		'cut_if_off_screen' : 4
		}),
]






new_curriculum = [simple_push_longer, simple_push_shorter, simple_lift, lift_short_fast, simple_rot, push_rot_slower, lift_push_rot, lift_push_rot_parabolic]

#1: y-height  = 2 * table y
#2: same, + random initial rotation

other_obj_curriculum = [tab_push_noshake, tab_push_down_noshake]

new_table_curriculum = [tab_push_long, tab_push_short, tab_lift, tab_rot, tab_push_rot, tab_lift_push_rot]




#np.pi / 6.
#controlled table task


controlled_table_push_long = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_PUSH_LONG',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(8, 10), 'f_horiz_range' : range(50, 70)},
		'cut_if_off_screen' : 4
		}

		)
]


controlled_table_push = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_PUSH',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400)},
		'cut_if_off_screen' : 4
		}

		)
]


controlled_table_push_lowvar = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_PUSH',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : .001, 'time_len_range' : range(1, 2), 'f_horiz_range' : range(100, 400)},
		'cut_if_off_screen' : 4
		}

		)
]


controlled_table_lift = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_LIFT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200)},
		}
		)
]

controlled_table_rot = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_ROT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(15, 20), 'tor_y_range' : range(50, 200)},
		'cut_if_off_screen' : 4
		}
		)
]

controlled_table_push_rot = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_PUSH_ROT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(3, 10), 'f_horiz_range' : range(50, 60), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150)},
		'cut_if_off_screen' : 4		
		}
		)
]

controlled_table_lift_push_rot = [
	('CONTROLLED_TABLE_TASK',
		'CONTROLLED_LIFT_PUSH_ROT',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(60), 'f_y_range' : range(130, 200), 'tor_horiz_range' : range(40, 80), 'tor_y_range' : range(50, 150)},
		}

		)
]

controlled_table_curriculum = [controlled_table_push_long, controlled_table_push, controlled_table_lift, controlled_table_rot, controlled_table_push_rot, controlled_table_lift_push_rot]

controlled_table_simple_test = [controlled_table_push_lowvar]

wall_throw = [
	('WALL_THROW',
		'WALL_THROW',
		{'func' : curious2.controlled_constant_action_sequences_distinguished_direction,
		'wait_before' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'wait_after' : {'threshold' : .01, 'time_window' : 3, 'max_time' : 20}, 
		'kwargs' : {'std_dev_ang' : np.pi / 6., 'time_len_range' : range(1, 2), 'f_horiz_range' : range(130, 300), 'f_y_range' : range(130, 200)}
		}


		)

]

wall_throw_curriculum = [wall_throw]






test_task_params = [
	('PUSHING', {'func' : curious2.make_const_simple_push, 'kwargs' : {'time_len' : 3, 'magnitude' : 100}, 'wait' : 20}),
	('LIFTING', {'func' : curious2.make_const_simple_lift, 'kwargs' : {'time_len' : 3, 'x_magnitude' : 50, 'y_magnitude' : 120}, 'wait' : 20})
]

lifting_params = [
	('LIFTING_0', {'func' : curious2.make_const_simple_lift, 'kwargs' : {'time_len' : 3, 'x_magnitude' : 50, 'y_magnitude' : 120}, 'wait' : 20})
]

pushing_params = [
	('PUSHING_0', {'func' : curious2.make_const_simple_push, 'kwargs' : {'time_len' : 3, 'magnitude' : 100}, 'wait' : 20}),
]

rotating_params = [
	('ROTATING_0', {'func' : curious2.make_const_simple_rot, 'kwargs' : {'time_len' : 10, 'magnitude' : 100}, 'wait' : 5})
]

lift_1_params = [
	('LIFTING_1', {'func' : curious2.make_const_action_sequences, 'kwargs' : {'time_len' : 4, 'f_horiz' : 60, 'f_y' : 120, 'tor_horiz' : 20, 'tor_y' : 100}, 'wait' : 20})
]

push_1_params = [
	('PUSHING_1', {'func' : curious2.make_const_action_sequences, 'kwargs' : {'time_len' : 4, 'f_horiz' : 100, 'f_y' : 0, 'tor_horiz' : 20, 'tor_y' : 100}, 'wait' : 20})
]

rot_1_params = [
	('ROTATING_1', {'func' : curious2.make_const_action_sequences, 'kwargs' : {'time_len' : 4, 'f_horiz' : 0, 'f_y' : 50, 'tor_horiz' : 20, 'tor_y' : 100}, 'wait' : 20})
]

simple_curriculum = [lifting_params, pushing_params, rotating_params, lift_1_params, push_1_params, rot_1_params]





lifting_params_r = [
	('SINGLE_OBJECT', 'LIFTING_0', {'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(1, 4), 'f_horiz_range' : range(50), 'f_y_range' : range(50, 150)}, 'wait' : 20})
]

pushing_params_r = [
	('SINGLE_OBJECT', 'PUSHING_0', {'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(1, 4), 'f_horiz_range' : range(50, 130)}, 'wait' : 20}),
]

rotating_params_r = [
	('SINGLE_OBJECT', 'ROTATING_0', {'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(5, 10), 'tor_y_range' : range(50, 150)}, 'wait' : 5})
]
 
lift_1_params_r = [
	('SINGLE_OBJECT', 'LIFTING_1', {'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(3, 6), 'f_horiz_range' : range(30, 60), 'f_y_range' : range(50, 150), 'tor_horiz_range' : range(10, 40), 'tor_y_range' : range(50, 150)}, 'wait' : 20})
]

push_1_params_r = [
	('SINGLE_OBJECT', 'PUSHING_1', {'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(3, 6), 'f_horiz_range' : range(80, 120), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 130)}, 'wait' : 20})
]

rot_1_params_r = [
	('SINGLE_OBJECT', 'ROTATING_1', {'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(3, 6), 'f_y_range' : range(10, 50), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 150)}, 'wait' : 20})
]


simple_curriculum_r = [lifting_params_r, pushing_params_r, rotating_params_r, lift_1_params_r, push_1_params_r, rot_1_params_r]

push_table = [
	('PUSH_OFF_TABLE', 'PUSHING_0T', {'wait_before' : 10, 'wait_after' : 10, 'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(1, 4), 'f_horiz_range' : range(50, 130)}})
]

push_1_table = [
	('PUSH_OFF_TABLE', 'PUSHING_1T', {'wait_before' : 10, 'wait_after' : 10, 'func' : curious2.make_constant_random_action_sequence, 'kwargs' : {'time_len_range' : range(3, 6), 'f_horiz_range' : range(80, 120), 'tor_horiz_range' : range(10, 30), 'tor_y_range' : range(50, 130)}})
]

simple_table_curr = [push_table, push_1_table]
