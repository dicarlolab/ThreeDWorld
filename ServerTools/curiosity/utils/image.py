PIXEL_DEPTH = 255

def norml(x):
  return (x - PIXEL_DEPTH/2.0) / PIXEL_DEPTH


def l2_error_rate(prediction, actual):
  return 0.5 * ((prediction - actual)**2).mean()
