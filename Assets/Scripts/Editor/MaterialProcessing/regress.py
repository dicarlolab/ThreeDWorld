# This script trains various regressors on mtl file properties and
# its manually adjusted Unity materials counterparts. 
# These properties are stored in the matparam.txt which is generated when
# MaterialPropertyExtractor.cs is run in Unity
# Each row corresponds to one mtl - Unity Material pair with the following paramters:
# Ka.r, Ka.g, Ka.b, Kd.r, Kd.g, Kd.b, 
# Ks.r, Ks.g, Ks.b, Ns, d, has_map_bump, has_map_Kd, has_map_Ks,
# UnityColor.r UnityColor.g, UnityColor.b, UnityColor.a, 
# UnitySpecColor.r UnitySpecColor.g, UnitySpecColor.b, UnitySpecColor.a, 
# UnityGlossiness, UnityMetallicness

import numpy as np
from scipy.optimize import curve_fit
import matplotlib.pyplot as plt
from sklearn import linear_model
from sklearn.svm import SVR
from sklearn.model_selection import cross_val_score
from sklearn.externals import joblib
import warnings

warnings.filterwarnings(action="ignore", module="scipy", message="^internal gelsd")

def func(x, a, b):
    return .5 + .5 * (a - b/x)
    
def func(x, a, b):
    return 1 - b * np.exp(-a * x)
    
def func(x, a, b, c, d):
    return a * np.exp(-b * x)/ (c + np.exp(-d * x))

def func(x, m, std):
    return (1 / np.sqrt(2 * (std ** 2) * np.pi)) * np.exp( - ( (x - m) ** 2 ) / (2 * (std ** 2)))
    
f = open("matparam.txt", "r")
l = np.array([ map(float,line.split(',')) for line in f ])

# Train logistic regression
reg = linear_model.LinearRegression()
reg.fit(l[:,0:14], l[:,18:24])


# Train support vector regressor
c = 10.0
eps = 0.2
k = 'rbf'

reg1 = SVR(kernel=k, C=c, epsilon = eps)
reg1.fit(l[:,0:14], l[:,18])
scores1 = cross_val_score(reg1, l[:,0:14], l[:,23], scoring='neg_mean_absolute_error', cv=5)

reg2 = SVR(kernel=k, C=c, epsilon = eps)
reg2.fit(l[:,0:14], l[:,19])

reg3 = SVR(kernel=k, C=c, epsilon = eps)
reg3.fit(l[:,0:14], l[:,20])

reg4 = SVR(kernel=k, C=c, epsilon = eps)
reg4.fit(l[:,0:14], l[:,21])

reg5 = SVR(kernel=k, C=c, epsilon = eps)
reg5.fit(l[:,0:14], l[:,22])

reg6 = SVR(kernel=k, C=c, epsilon = eps)
reg6.fit(l[:,0:14], l[:,23])

# When the following line is uncommented matregression.pkl will be overwritten 
# and a new regression pickle for MaterialProcessor.cs will be created 
#joblib.dump(reg, 'matregression.pkl')



############## DEBUGGING AND TESTING ###############

# Print example predictions and compare to ground truth 
i = 120
exmp = l[i,0:14]

#exmp = np.array([0, 0, 0, 1, 0.964706, 0.0117647, 1, 0.964706, 0.0117647, 8, 0, 0, 0, 0])
#exmp = np.array([0, 0, 0, 0.968627, 0.776471, 0, 1, 0.801619, 0, 8, 0, 0, 0, 0])
#exmp = np.array([0.329412, 0.329412, 0.329412, 0.329412, 0.329412, 0.329412, 0.898039, 0.898039, 0.898039, 11.3137, 0, 0, 0, 0])

Z = reg.predict(exmp)

r1 = reg1.predict(exmp)
r2 = reg2.predict(exmp)
r3 = reg3.predict(exmp)
r4 = reg4.predict(exmp)
r5 = reg5.predict(exmp)
r6 = reg6.predict(exmp)

print "\nlinear regression"
print Z
print "SVR"
print str(r1) + " " + str(r2) + " " + str(r3) + " " + str(r4) + " " + str(r5) + " " + str(r6)
print "ground truth"
print l[i,18:24]
print "stats glossiness: "
print np.std(l[:,22])
print np.mean(l[:,22])
print "stats metallic: "
print np.std(l[:,23])
print np.mean(l[:,23])

# Debugging plotting functions
#plt.hist(l[:,22])
#plt.show()
#popt, pcov = curve_fit(func, xdata, ydata)
