import numpy as np
import matplotlib.pyplot as plt

class ship:
    def __init__(self, name, weights, volume, hangar_volume, density):
        if len(weights) < 3: raise ValueError('You should provide 3 or 4 weights')
        self.name          = name
        self.weights       = weights
        self.volume        = volume
        self.hangar_volume = hangar_volume
        self.density       = density
        self.spec_mass     = self._spec_mass()
        self.init_mass     = self.mass(1, 1) 
    #end def
    
    def mass(self, scale, length):
        w = self.spec_mass
        m = ((w[0]*scale + w[1]*length)*scale + w[2])*scale
        if len(self.weights) > 3: m += w[4]
        return m
    #end def
    
    def _spec_mass(self):
        return self.weights*(self.volume-self.hangar_volume)*self.density

    def __str__(self):
        s  = '=== %s ===\n' % self.name
        s += '//Volume: %f - %f = %f\n' % (self.volume, self.hangar_volume, self.volume-self.hangar_volume)
        s += '//Density = %f\n' % self.density
        s += 'mass = %f\n' % self.init_mass
        s += 'specificMass = %s, 0 //weights: %s\n' % (', '.join(str(m) for m in self.spec_mass), self.weights)
        return s
    #end def
#end class


class _ship:
    def __init__(self, name, dimensions, hangar_dimensions, density, v_factor = 1):
        if len(dimensions) != 3 or len(hangar_dimensions) != 3: 
            raise ValueError('Dimensions shoud have 3 elements')
        self.name      = name
        self.D         = dimensions
        self.hD        = hangar_dimensions
        self.density   = density
        self.v_factor  = v_factor
        self.init_mass = self.mass()
    #end def
    
    def _V(self, D, scale=1, length=1):
        return D[0]*scale * D[1]*scale*length * D[2]*scale * self.v_factor
    
    def mass(self, scale=1, length=1):
        V  = self._V(self.D, scale, length)
        hV = self._V(self.hD, scale, length)
        return self.density*(V-hV)
    #end def
    
    def __str__(self):
        V  = self._V(self.D)
        hV = self._V(self.hD)
        s  = '=== %s ===\n' % self.name
        s += 'Volume: %f - %f = %f\n' % (V, hV, V-hV)
        s += 'mass = %f\n' % self.init_mass
        s += 'Density = %f\n' % self.density
        s += 'VolumeFactor = %f\n' % self.v_factor
        return s
    #end def
#end class
         
        
def format_data(x, y, w=None):
    s = ''
    if w is None: w = range(len(x))
    max_wx = max(len(str(x[i])) for i in w)
    for i in w: s += '%.1f%s : %.2f\n' % (x[i], ' '*(max_wx-len(str(x[i]))), y[i])
    return s
#end def

if __name__ == '__main__':
    scales = np.arange(0.5, 4.1, 0.5)
    
    inline1   = ship('inline1',   np.array([0.20, 0.60, 0.2]), 31.25, 16.039648, 0.287)
    inline2   = ship('inline2',   np.array([0.15, 0.60, 0.25]), 279.225, 128.34989, 0.168)
    spaceport = ship('spaceport', np.array([0.05, 0.65, 0.3]), 1414.8185, 541.4, 0.116)
    
    small     = ship('small',   np.array([0.05, 0.60, 0.35]), 51.882188, 34.035978, 0.52)
    big       = ship('big', np.array([0.1, 0.5, 0.4]), 1789.8007, 1166.7095, 0.172)

    #V(cube)/V(cylinder) = 0.78539816
#     inline1   = ship('inline1', np.array([2.5, 5.0, 2.5]), np.array([2.0, 3.4, 2.4]), 0.287, 0.78539816)
#     inline2   = ship('inline2', np.array([5.2, 11.2, 5.1]), np.array([3.9, 6.8, 4.8]), 0.208, 0.78539816)
#     spaceport = ship('spaceport', np.array([9.7, 22.2, 8.0]), np.array([5.6, 18.1, 5.3]), 0.186, 0.78539816)
#     
#     small     = ship('small', np.array([3.8, 6.1, 2.3]), np.array([3.3, 4.3, 2.0]), 0.325)
#     big       = ship('big', np.array([11.9, 24.6, 6.3]), np.array([10.3, 18.1, 5.6]), 0.210)

    inline1_d0  = np.fromiter((inline1.mass(s, 1) for s in scales), float)
    inline2_d0  = np.fromiter((inline2.mass(s/2, 1) for s in scales), float)
    
    inline1_d   = np.fromiter((inline1.mass(s, 1.8) for s in scales), float)
    inline2_d   = np.fromiter((inline2.mass(s/2, 1.8) for s in scales), float)
    spaceport_d = np.fromiter((spaceport.mass(s/3, 1) for s in scales), float)
    
    small_d = np.fromiter((small.mass(s, 1.334) for s in scales), float)
    big_d   = np.fromiter((big.mass(s/3, 1) for s in scales), float)
    
    
    print(inline1);   print(format_data(scales, inline1_d))
    print(inline2);   print(format_data(scales, inline2_d, np.where(scales/2 >= 1)[0]))
    print(spaceport); print(format_data(scales, spaceport_d, np.where(scales/3 >= 1)[0]))
    print(small); print(format_data(scales, small_d))
    print(big);   print(format_data(scales, big_d, np.where(scales/3 >= 1)[0]))
    
    plt.xlim(0.5, 4)
    plt.plot(scales, inline1_d0, '.-', label=inline1.name+'_0')
    plt.plot(scales[np.where(scales/2 >= 1)], inline2_d0[np.where(scales/2 >= 1)], '.-', label=inline2.name+'_0')
    plt.plot(scales, inline1_d, '.-', label=inline1.name)
    plt.plot(scales[np.where(scales/2 >= 1)], inline2_d[np.where(scales/2 >= 1)], '.-', label=inline2.name)
    plt.plot(scales[np.where(scales/3 >= 1)], spaceport_d[np.where(scales/3 >= 1)], '.-', label=spaceport.name)
    plt.plot(scales, small_d, '.-', label=small.name)
    plt.plot(scales[np.where(scales/3 >= 1)], big_d[np.where(scales/3 >= 1)], '.-', label=big.name)
    plt.legend(loc=2)
    plt.show()    
