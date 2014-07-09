import numpy as np
import matplotlib.pyplot as plt

class ship:
    def __init__(self, name, weights, volume, hangar_volume, density):
        if len(weights) < 3: raise ValueError('You should provide 3 or 4 weights')
        self.name          = name
        self.weights       = weights
        self.total_volume  = volume
        self.hangar_volume = hangar_volume
        self.density       = density
        self.spec_mass     = self._spec_mass()
        self.init_mass     = self.mass() 
    #end def
    
    def mass(self, scale=1, length=1):
        w = self.spec_mass
        m = ((w[0]*scale + w[1])*scale + w[2])*scale*length
        if len(self.weights) > 3: m += w[4]
        return m
    #end def
    
    def true_mass(self, scale=1, length=1):
        return (self.total_volume-self.hangar_volume)*scale**3*length*self.density
    
    def volume(self, scale=1, length=1):
        return (self.total_volume-self.hangar_volume)*scale**3*length;
    
    def _spec_mass(self):
        return self.weights*(self.total_volume-self.hangar_volume)*self.density

    def __str__(self):
        s  = '=== %s ===\n' % self.name
        s += '//Volume: %s - %s = %s\n' % (self.total_volume, self.hangar_volume, self.total_volume-self.hangar_volume)
        s += '//Density = %s\n' % self.density
        s += '//true mass = %s\n' % self.init_mass
        s += 'mass = %s\n' % self.init_mass
        s += 'specificMass = %s, 0 //weights: %s\n' % (', '.join(str(m) for m in self.spec_mass), self.weights)
        return s
    #end def
#end class
         
        
def format_data(x, ys, w=None):
    s = ''
    if w is None: w = range(len(x))
    max_wx  = max(len(str(x[i])) for i in w)
    max_wys = [max(len(str(y[i])) for i in w) for y in ys]
    for i in w: 
        s += '%s%s ' % (x[i], ' '*(max_wx-len(str(x[i]))))
        for y, myw in zip(ys, max_wys): 
            s += ': %s%s ' % (y[i], ' '*(myw-len(str(y[i]))))
        s += '\n'
    return s
#end def

if __name__ == '__main__':
    scales = np.arange(0.5, 4.1, 0.5)
    
    inline1   = ship('InlineHangar',   np.array([0.8, 0.2, 0.0]), 9.4+12.5, 12.5, 0.292)
    inline2   = ship('InlineHangar2',   np.array([0.9, 0.1, 0.0]), 98.08+99.94, 99.94, 0.258)
    spaceport = ship('Spaceport', np.array([1.0, 0.0, 0.0]), 366.046+466.941, 466.941, 0.241)
    
    small     = ship('SmallHangar',   np.array([0.8, 0.2, 0.0]), 13.82+34.036, 34.036, 0.281)
    big       = ship('BigHangar', np.array([0.9, 0.1, 0.0]), 527.4+1166.7, 1166.7, 0.248)
    
    l1 = 1.44
    l2 = 1.1
    inline1_m   = np.fromiter((inline1.mass(s, l1) for s in scales), float)
    inline2_m   = np.fromiter((inline2.mass(s/2, l2) for s in scales), float)
    spaceport_m = np.fromiter((spaceport.mass(s/3, 1) for s in scales), float)
    
    inline1_tm   = np.fromiter((inline1.true_mass(s, l1) for s in scales), float)
    inline2_tm   = np.fromiter((inline2.true_mass(s/2, l2) for s in scales), float)
    spaceport_tm = np.fromiter((spaceport.true_mass(s/3, 1) for s in scales), float)
    
    inline1_v   = np.fromiter((inline1.volume(s, l1) for s in scales), float)
    inline2_v   = np.fromiter((inline2.volume(s/2, l2) for s in scales), float)
    spaceport_v = np.fromiter((spaceport.volume(s/3, 1) for s in scales), float)
    
    lg1 = 1.4134105
    small_m  = np.fromiter((small.mass(s, lg1) for s in scales), float)
    big_m    = np.fromiter((big.mass(s/3, 1) for s in scales), float)
    small_tm = np.fromiter((small.true_mass(s, lg1) for s in scales), float)
    big_tm   = np.fromiter((big.true_mass(s/3, 1) for s in scales), float)
    small_v  = np.fromiter((small.volume(s, lg1) for s in scales), float)
    big_v    = np.fromiter((big.volume(s/3, 1) for s in scales), float)
    
    
    print(inline1); print('length: %s' % l1)
    print(format_data(scales, (inline1_m, inline1_tm, inline1_v)))
    print(inline2); print('length: %s' % l2)   
    print(format_data(scales, (inline2_m, inline2_tm, inline2_v), np.where(scales/2 >= 1)[0]))
    print(spaceport);
    print(format_data(scales, (spaceport_m, spaceport_tm, spaceport_v), np.where(scales/3 >= 1)[0]))
    print(small); print('length: %s' % lg1)
    print(format_data(scales, (small_m, small_tm, small_v)))
    print(big);
    print(format_data(scales, (big_m, big_tm, big_v), np.where(scales/3 >= 1)[0]))
    
    plt.xlim(0.5, 4)
    plt.plot(scales, inline1_m, '.-', label=inline1.name)
#     plt.plot(scales, inline1_tm, 'o-', label=inline1.name)
    plt.plot(scales[np.where(scales/2 >= 1)], inline2_m[np.where(scales/2 >= 1)], '.-', label=inline2.name)
#     plt.plot(scales[np.where(scales/2 >= 1)], inline2_tm[np.where(scales/2 >= 1)], 'o-', label=inline2.name)
    plt.plot(scales[np.where(scales/3 >= 1)], spaceport_m[np.where(scales/3 >= 1)], '.-', label=spaceport.name)
#     plt.plot(scales[np.where(scales/3 >= 1)], spaceport_tm[np.where(scales/3 >= 1)], 'o-', label=spaceport.name)
    plt.plot(scales, small_m, '.-', label=small.name)
    plt.plot(scales[np.where(scales/3 >= 1)], big_m[np.where(scales/3 >= 1)], '.-', label=big.name)
    plt.legend(loc=2)
    plt.show()    
