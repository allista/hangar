import numpy as np
import matplotlib.pyplot as plt
import sys


class volume:
    def __init__(self, V, d, name, cost):
        self._V    = V
        self.d     = d
        self.name  = name
        self._cost = cost
        
    def V(self, scale=1, length=1):
        return self._V*scale**3*length;
    
    def cost(self, scale=1, length=1):
        return self.V(scale, length)*self._cost;

    def mass(self, scale=1, length=1):
        return self.V(scale, length)*self.d;
    
    def __str__(self):
        return '(%s: %sm^3, %st/m^3, %st, %sCr)' % (self.name, self.V(), self.d, 
                                                    self.mass(), self.cost())
#end class

class material:
    def __init__(self, density, cost):
        self.density = density
        self.cost    = cost
#end class

class surface:
    _unit_h = 0.005
    
    def __init__(self, S, h, m, name):
        self._S   = S
        self.h    = h
        self.m    = m
        self.name = name
    
    def S(self, scale=1, length=1):
        return self._S*scale**2*length    
    
    def mass(self, scale=1, length=1):
        return self.S(scale, length)*self.h*self.m.density;
    
    def cost(self, scale=1, length=1):
        return self.S(scale, length)*self.h/self._unit_h*self.m.cost;
    
    def __str__(self):
        return '(%s: %sm^2, %sm, %st/m^3, %st, %sCr)' % (self.name, self.S(), 
                                                         self.h, self.m.density, 
                                                         self.mass(), self.cost())
#end class


class ship:
    def __init__(self, name, surfaces, volumes, add_mass = 0, add_cost = 0, res_cost = 0):
        self.name       = name
        self._surfaces  = surfaces
        self._volumes   = volumes
        self._add_mass  = add_mass
        self._add_cost  = add_cost
        self._res_cost  = res_cost
        self._spec_mass = np.array([self.V_mass(), self.S_mass(), 0, self._add_mass])
        self._init_mass = sum(self._spec_mass)
        self._weights   = self._spec_mass/self._init_mass
        self._spec_cost = np.array([sum(v.cost() for v in self._volumes), 
                                    sum(s.cost() for s in self._surfaces), 
                                    0, self._add_cost])
        self._cost      = sum(self._spec_cost)
        self._cost_weights = self._spec_cost/self._cost
        self._cost     += res_cost
    #end def
    
    def mass(self, scale=1, length=1):
        w = self._spec_mass
        m = ((w[0]*scale + w[1])*scale + w[2])*scale*length
        if len(w) > 3: m += w[3]
        return m
    #end def
    
    def cost(self, scale=1, length=1):
        w = self._spec_cost
        c = ((w[0]*scale + w[1])*scale + w[2])*scale*length
        if len(w) > 3: c += w[3]
        c += self._res_cost
        return c
    #end def
    
    def true_mass(self, scale=1, length=1):
        return self.V_mass(scale, length)+self.S_mass(scale, length)+self._add_mass
    #end def
    
    def volume(self, scale=1, length=1):
        return sum(v.V(scale, length) for v in self._volumes)
    
    def surface(self, scale=1, length=1):
        return sum(s.S(scale, length) for s in self._surfaces)
    
    def V_mass(self, scale=1, length=1):
        return sum(v.mass(scale, length) for v in self._volumes)
    
    def S_mass(self, scale=1, length=1):
        return sum(s.mass(scale, length) for s in self._surfaces)
    #end def

    def __str__(self):
        s  = '=== %s ===\n' % self.name
        s += '//Volumes: [ %s ]\n' % ' '.join(str(v) for v in self._volumes)
        s += '//Total volume: %s m^3\n' % self.volume()
        s += '//V mass: %s t\n' % self.V_mass()
        s += '//Shell: [ %s ]\n' % ' '.join(str(s) for s in self._surfaces)
        s += '//Total surface: %s m^2\n' % self.surface()
        s += '//S mass: %s t\n' % self.S_mass()
        s += '//Additional mass: %s t\n' % self._add_mass
        s += '//Additional cost: %s Cr\n' % self._add_cost
        s += '//Resources cost: %s Cr\n' % self._res_cost
        s += 'entryCost = %s\n' % (self._cost*2)
        s += 'cost = %s\n' % self._cost
        s += 'mass = %s\n' % self._init_mass
        s += 'specificMass = %s //weights: [ %s ]\n' % (', '.join(str(m) for m in self._spec_mass), 
                                                        ', '.join(str(w) for w in self._weights))
        s += 'specificCost = %s //weights: [ %s ]\n' % (', '.join(str(m) for m in self._spec_cost), 
                                                        ', '.join(str(w) for w in self._cost_weights))
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
    
    aluminium = material(2.7, 8.0)
    composits = material(1.9, 20.0)
    
    #inline
    inline1   = ship('InlineHangar', 
                     surfaces=[surface(62.44, 0.005, aluminium, 'hull'), 
                               surface(9.32*2, 0.005, aluminium, 'doors')],
                     volumes=[volume(9.4-3.93, 0.02, 'hull', 1),
                              volume(3.93, 0.317, 'machinery', 100),
                              volume(0.659*2, 0.02, 'doors', 1)], 
                     add_mass=0,
                     add_cost=200) #docking port
    
    inline2   = ship('InlineHangar2', 
                     surfaces=[surface(268.11, 0.006, aluminium, 'hull'), 
                               surface(35.94*2, 0.006, aluminium,'doors')],
                     volumes=[volume(98.08-53.66, 0.02, 'hull', 1),
                              volume(53.66-40.45, 0.343, 'machinery', 80), 
                              volume(40.45, 0.153, 'cabins', 220), #like Hitchhikers container
                              volume(4.05*2, 0.02,'doors', 1)],
                     add_mass=0,
                     add_cost=280) #docking port
    
    #spaceport
    spaceport = ship('Spaceport', 
                     surfaces=[surface(960.55, 0.007, composits, 'hull'), 
                               surface(28.94*2, 0.007, composits,'doors'),
                               surface(11.04, 0.006, aluminium, 'monoprop tank')],
                     volumes=[volume(366.046-46.92-112.64*2-1.5*2-8.7, 0.01, 'hull', 2), 
                              volume(46.92, 0.01, 'machinery room', 15),
                              volume(112.64*2, 0.168, 'cabins', 200), #density of the SpaceX Dragon vessel
                              volume(1.5*2+8.7, 0.001, 'coridors', 1),
                              volume(1.64*2, 0.01,'doors', 2),
                              volume(1.5, 0.75, 'battery', 22500/1.5),
                              volume(0.95, 0.2/0.21, 'reaction wheel', 2100/0.21),
                              ],
                     add_mass=2+0.72+6+0.08,  #cockpit, generator, machinery, probe core
                     add_cost=980 + 300 + 29700 + 4000 + 600,  #DockPort + Light + Gen + Cockpit + probe core
                     res_cost=2400) #Monoprop
    
    #landers
    lander     = ship('RoverLander', 
                     surfaces=[surface(92.1, 0.004, aluminium, 'hull'), 
                               surface(14.19*2+13.45*2, 0.003, aluminium, 'doors'),
                               surface(2.39*6, 0.006, aluminium, 'fuel tanks')],
                     volumes=[volume(7, 0.20, 'base', 40),
                              volume(6.36, 0.110, 'machinery', 80),
                              volume(0.045, 0.98,'clamp', 600),
                              volume(0.62*2+0.47*2, 0.02, 'doors', 1),
                              volume(0.444*2, 0.05/0.444, 'batteries', 880/0.444),
                              volume(0.17, 0.2/0.21, 'reaction wheel', 2100/0.21),
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=200 + 480, #Light + probe core
                     res_cost=324 + 89.1 + 240) #LF+Ox+MP

    #ground hangars
    small     = ship('SmallHangar', 
                     surfaces=[surface(145.7, 0.006, aluminium, 'hull'), 
                               surface(14.43, 0.006, aluminium, 'doors')],
                     volumes=[volume(13.82-4.7, 0.02, 'hull', 1),
                              volume(4.7, 0.213, 'machinery', 80),
                              volume(0.18, 0.78,'clamp', 300),
                              volume(0.74, 0.02, 'doors', 1),
                              volume(2, 0.2/2, 'battery', 4500/2),
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=100 + 200 + 480) #Light + DockPort + probe core
    
    big       = ship('BigHangar', 
                     surfaces=[surface(1667.79, 0.01, composits, 'hull'), 
                               surface(124.08, 0.01, composits,'doors')],
                     volumes=[volume(527.4-218.99, 0.01, 'hull', 2), 
                              volume(17.89, 0.01,'doors', 2),
                              volume(4.34, 0.78,'clamp', 300),
                              volume(218.99-27.75, 0.183, 'cabins', 150),
                              volume(27.75, 0.246, 'machinery', 80),
                              volume(1.5, 0.75, 'battery', 22500/1.5),
                              ],
                     add_mass=0.72+0.04, #generator, probe core
                     add_cost=280 + 300 + 29700 + 480) #DockPort +  Light + generator + probe core
    
    #utilities
    adapter  = ship('Adapter', 
                     surfaces=[surface(13.02, 0.006, composits, 'hull')],
                     volumes=[volume(2.845, 0.0, 'hull', 50)], 
                     add_mass=0,
                     add_cost=0)
    
    r_adapter2 = ship('Radial Adapter 2', 
                     surfaces=[surface(10.01+1.86*2, 0.006, aluminium, 'hull')],
                     volumes=[volume(2.09+0.163*2, 0.01, 'hull', 50)], 
                     add_mass=0,
                     add_cost=0)
    
    r_adapter1 = ship('Radial Adapter 1', 
                     surfaces=[surface(6.37+2.37+1.86, 0.006, aluminium, 'hull')],
                     volumes=[volume(1.24+0.213+0.163, 0.01, 'hull', 50)], 
                     add_mass=0,
                     add_cost=0)
    
    station_hub = ship('Station Hub', 
                     surfaces=[surface(29.76, 0.01, aluminium, 'hull')],
                     volumes=[volume(7.49, 0.1, 'hull', 80)], 
                     add_mass=0,
                     add_cost=0)
    
    docking_port = ship('Docking Port', 
                     surfaces=[surface(13.89, 0.005, aluminium, 'hull')],
                     volumes=[volume(0.635, 0.1, 'hull', 1380)], 
                     add_mass=0,
                     add_cost=0)
    
    rcs       = ship('SpaceportRCS', 
                     surfaces=[surface(4.77, 0.007, composits, 'hull'),],
                     volumes=[volume(0.36, 0.48, 'machinery', 4760)], 
                     add_mass=0,
                     add_cost=0)
    
    heatshield = ship('Square Heatshield', 
                     surfaces=[surface(40.7, 0.005, aluminium, 'hull'),],
                     volumes=[volume(3.8, 0.01, 'hull', 20)], 
                     add_mass=0,
                     add_cost=0)
    
    l1 = 1#.27624
    l2 = 1#.03
    inline1_m   = np.fromiter((inline1.mass(s, l1) for s in scales), float)
    inline2_m   = np.fromiter((inline2.mass(s/2, l2) for s in scales), float)
    spaceport_m = np.fromiter((spaceport.mass(s/3, 1) for s in scales), float)
     
    inline1_sm   = np.fromiter((inline1.S_mass(s, l1) for s in scales), float)
    inline2_sm   = np.fromiter((inline2.S_mass(s/2, l2) for s in scales), float)
    spaceport_sm = np.fromiter((spaceport.S_mass(s/3, 1) for s in scales), float)
    
    inline1_v   = np.fromiter((inline1.volume(s, l1) for s in scales), float)
    inline2_v   = np.fromiter((inline2.volume(s/2, l2) for s in scales), float)
    spaceport_v = np.fromiter((spaceport.volume(s/3, 1) for s in scales), float)
    
    inline1_c   = np.fromiter((inline1.cost(s, l1) for s in scales), float)
    inline2_c   = np.fromiter((inline2.cost(s/2, l2) for s in scales), float)
    spaceport_c = np.fromiter((spaceport.cost(s/3, 1) for s in scales), float)
    
    lander_m    = np.fromiter((lander.mass(s/2, 1) for s in scales), float)
    lander_sm   = np.fromiter((lander.S_mass(s/2, 1) for s in scales), float)
    lander_v    = np.fromiter((lander.volume(s/2, 1) for s in scales), float)
    lander_c    = np.fromiter((lander.cost(s/2, 1) for s in scales), float)
     
    lg1 = 1#.3981227
    small_m  = np.fromiter((small.mass(s, lg1) for s in scales), float)
    big_m    = np.fromiter((big.mass(s/3, 1) for s in scales), float)

    small_sm = np.fromiter((small.S_mass(s, lg1) for s in scales), float)
    big_sm   = np.fromiter((big.S_mass(s/3, 1) for s in scales), float)
    
    small_v  = np.fromiter((small.volume(s, lg1) for s in scales), float)
    big_v    = np.fromiter((big.volume(s/3, 1) for s in scales), float)
    
    small_c  = np.fromiter((small.cost(s, lg1) for s in scales), float)
    big_c    = np.fromiter((big.cost(s/3, 1) for s in scales), float)
    
    adapter_m  = np.fromiter((adapter.mass(s/3, lg1) for s in scales), float)
    adapter_sm = np.fromiter((adapter.S_mass(s/3, lg1) for s in scales), float)
    adapter_v  = np.fromiter((adapter.volume(s/3, lg1) for s in scales), float)
    adapter_c  = np.fromiter((adapter.cost(s/3, lg1) for s in scales), float)
    
    rcs_m  = np.fromiter((rcs.mass(s/3, lg1) for s in scales), float)
    rcs_sm = np.fromiter((rcs.S_mass(s/3, lg1) for s in scales), float)
    rcs_v  = np.fromiter((rcs.volume(s/3, lg1) for s in scales), float)
    rcs_c  = np.fromiter((rcs.cost(s/3, lg1) for s in scales), float)
    
    print(inline1); print('length: %s' % l1)
    print(format_data(scales, (inline1_m, inline1_sm, inline1_v, inline1_c)))
    print(inline2); print('length: %s' % l2)   
    print(format_data(scales, (inline2_m, inline2_sm, inline2_v, inline2_c), np.where(scales/2 >= 1)[0]))
    print(spaceport);

    print(lander);
    print(format_data(scales, (lander_m, lander_sm, lander_v, lander_c), np.where(scales/2 >= 1)[0]))
    
    print(small); print('length: %s' % lg1)
    print(format_data(scales, (small_m, small_sm, small_v, small_c)))

    print(big);
    print(format_data(scales, (big_m, big_sm, big_v, big_c), np.where(scales/3 >= 1)[0]))
    
    print(adapter);
    print(format_data(scales, (adapter_m, adapter_sm, adapter_v, adapter_c)))
    
    print(r_adapter2);
    print(r_adapter1);
    print(station_hub);
    print(docking_port);

    print(rcs);
    print(format_data(scales, (rcs_m, rcs_sm, rcs_v, rcs_c)))#, np.where(scales/3 >= 1)[0]))
    
    print(heatshield)

    sys.exit(0)
    
    plt.xlim(0.5, 4)
    plt.plot(scales, inline1_m, '.-', label=inline1.name)
    plt.plot(scales[np.where(scales/2 >= 1)], inline2_m[np.where(scales/2 >= 1)], '.-', label=inline2.name)
    plt.plot(scales[np.where(scales/3 == 1)], spaceport_m[np.where(scales/3 == 1)], 'o', label=spaceport.name)
    plt.plot(scales, small_m, '.-', label=small.name)
    plt.plot(scales[np.where(scales/3 >= 1)], big_m[np.where(scales/3 >= 1)], '.-', label=big.name)
    plt.legend(loc=2)
    plt.show()
    plt.plot(scales, inline1_c, '.-', label=inline1.name)
    plt.plot(scales[np.where(scales/2 >= 1)], inline2_c[np.where(scales/2 >= 1)], '.-', label=inline2.name)
    plt.plot(scales[np.where(scales/3 == 1)], spaceport_c[np.where(scales/3 == 1)], 'o', label=spaceport.name)
    plt.plot(scales, small_c, '.-', label=small.name)
    plt.plot(scales[np.where(scales/3 >= 1)], big_c[np.where(scales/3 >= 1)], '.-', label=big.name)
    plt.legend(loc=2)
    plt.show()