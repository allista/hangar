'''
Created on Mar 6, 2015

@author: Allis Tauri
'''

import sys
import numpy as np
import matplotlib.pyplot as plt

from path import path
from Current_map import Map

def draw_map(M):
    lat = M[:,:,0]; lon = M[:,:,1]; h = M[:,:,2];
    ext = [lon.min(), lon.max(), lat.min(), lat.max()]
    img = plt.imshow(h, extent=ext, origin='lower')
    img.set_cmap('spectral')
    plt.colorbar()
    return ext

if __name__ == '__main__':
    ext = draw_map(Map)

    plt.plot(path[1,1], path[1,0], 'oy')
    plt.plot(path[-1,1], path[-1,0], 'og')
    plt.plot(path[1:-1,1], path[1:-1,0], '-', color='black', linewidth=0.2)
    plt.plot(path[-2,1], path[-2,0], 'o', color='#ff88ff')
    plt.xlim(ext[0:2])
    plt.ylim(ext[2:4])
    plt.show()
    
#    from Kerbin_map import Map as M
#    ext = draw_map(M)
#    plt.show()
