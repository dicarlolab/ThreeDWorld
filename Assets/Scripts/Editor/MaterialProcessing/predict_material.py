# This script regresses the mtl material properties given 
# the following 15 dimensional input:
# regressor_pickle_path, Ka.r, Ka.g, Ka.b, Kd.r, Kd.g, Kd.b, 
# Ks.r, Ks.g, Ks.b, Ns, d, has_map_bump, has_map_Kd, has_map_Ks
# The 0-th argument is the script itself hence start counting the arguments
# from 1

import sys
import numpy as np
from sklearn.externals import joblib    

# load from regressor pickle path
reg = joblib.load(sys.argv[1]);
# regress from mtl material properties
mat_input = np.array([float(x) for x in sys.argv[2:]])

res = np.array(reg.predict(mat_input))
# print regressed parameters: specColor(r,g,b,a), glossiness, metallicness
for x in res[0]:
  print(str(x))

