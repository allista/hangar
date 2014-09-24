import numpy as np
import matplotlib.pyplot as plt
import sys

class material:
    def __init__(self, density, cost):
        self.density = density
        self.cost    = cost
#end class

class surface:
    _unit_h = 0.005
    
    def __init__(self, S, h, m):
        self._S   = S
        self.h    = h
        self.m    = m
    
    def S(self, scale=1, length=1):
        return self._S*scale**2*length    
    
    def mass(self, scale=1, length=1):
        return self.S(scale, length)*self.h*self.m.density;
    
    def cost(self, scale=1, length=1):
        return self.S(scale, length)*self.h/self._unit_h*self.m.cost;
    
    def __str__(self):
        return '[%sm^2 x %sm], %st/m^3, %st, %sCr' % (self.S(), 
                                                   self.h, self.m.density, 
                                                   self.mass(), self.cost())
#end class

class volume:
    def __init__(self, V, d, name, cost, surface=None, subvolumes=[]):
        self._V    = V
        self.d     = d
        self.name  = name
        self._cost = cost
        self._surface = surface
        self._subvolumes = subvolumes
    #end def
        
    #volume and surface area
    def V(self, scale=1, length=1):
        vol = self.full_V(scale, length) - sum(sv.full_V(scale, length) for sv in self._subvolumes)
        assert vol>=0, \
        ("Combined volume of sub-volumes is greater than the volume of '%s'" % self.name)
        return vol
    #end def
    
    def full_V(self, scale=1, length=1):
        return self._V*scale**3*length
    
    def S(self, scale=1, length=1):
        if self._surface is None: return 0 
        return self._surface.S(scale, length)
    
    def full_S(self, scale=1, length=1):
        return (self.S(scale, length) +
                sum(sv.full_S(scale, length) for sv in self._subvolumes))
    
    #cost
    def V_cost(self, scale=1, length=1):
        return self.V(scale, length)*self._cost
    
    def full_V_cost(self, scale=1, length=1):
        return (self.V_cost(scale, length) + 
                sum(sv.full_V_cost(scale, length) for sv in self._subvolumes))

    def S_cost(self, scale=1, length=1):
        if self._surface is None: return 0 
        return self._surface.cost(scale, length)
    
    def full_S_cost(self, scale=1, length=1):
        return (self.S_cost(scale, length) + 
                sum(sv.full_S_cost(scale, length) for sv in self._subvolumes))
        
    def full_cost(self, scale=1, length=1): 
        return self.full_V_cost(scale, length) + self.full_S_cost(scale, length)

    #mass
    def V_mass(self, scale=1, length=1):
        return self.V(scale, length)*self.d;
    
    def full_V_mass(self, scale=1, length=1):
        return (self.V_mass(scale, length) + 
                sum(sv.full_V_mass(scale, length) for sv in self._subvolumes))
        
    def S_mass(self, scale=1, length=1):
        if self._surface is None: return 0 
        return self._surface.mass(scale, length)
    
    def full_S_mass(self, scale=1, length=1):
        return (self.S_mass(scale, length) + 
                sum(sv.full_S_mass(scale, length) for sv in self._subvolumes))
        
    def full_mass(self, scale=1, length=1): 
        return self.full_V_mass(scale, length) + self.full_S_mass(scale, length)
    
    #representation
    def __str__(self):
        simple = self._surface is None and not self._subvolumes
        s  = ''
        s += '%s: %sm^3, %s%st, %sCr\n' % (self.name, self.full_V(),
                                           '%st/m^3 ' % self.d if simple else '', 
                                           self.full_mass(), self.full_cost())
        if not simple:
            if self._surface is not None: 
                s += '   surface: %s\n' % self._surface
            s += '   content: %sm^3, %st/m^3, %st, %sCr\n' % (self.V(), self.d, 
                                                              self.V_mass(), self.V_cost())
            if len(self._subvolumes) > 0:
                s += '   '+''.join(str(sv).replace('\n', '\n   ') for sv in self._subvolumes)
                s = s[:-3]
        return s
#end class

class ship:
    _asymptote_slope     = 1.5
    _asymptote_intercept = 1e5
    _exponent_base       = 1.25
    
    def __init__(self, name, volumes, add_mass = 0, add_cost = 0, res_cost = 0):
        self.name       = name
        self._volumes   = volumes
        self._add_mass  = add_mass
        self._add_cost  = add_cost
        self._res_cost  = res_cost
        self._spec_mass = np.array([self.V_mass(), self.S_mass(), 0, self._add_mass])
        self._init_mass = sum(self._spec_mass)
        self._weights   = self._spec_mass/self._init_mass
        self._spec_cost = np.array([sum(v.full_V_cost() for v in self._volumes), 
                                    sum(v.full_S_cost() for v in self._volumes), 
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
    
    @classmethod
    def entry_cost(cls, c):
        return c*cls._asymptote_slope + (1-cls._exponent_base**(-10*c/cls._asymptote_intercept))*cls._asymptote_intercept
    #end def
    
    def true_mass(self, scale=1, length=1):
        return self.V_mass(scale, length)+self.S_mass(scale, length)+self._add_mass
    #end def
    
    def volume(self, scale=1, length=1):
        return sum(v.full_V(scale, length) for v in self._volumes)
    
    def surface(self, scale=1, length=1):
        return sum(v.full_S(scale, length) for v in self._volumes)
    
    def V_mass(self, scale=1, length=1):
        return sum(v.full_V_mass(scale, length) for v in self._volumes)
    
    def S_mass(self, scale=1, length=1):
        return sum(v.full_S_mass(scale, length) for v in self._volumes)
    #end def

    def __str__(self):
        s  = '//'+hr(self.name, '=')
        s += '//'+'\n//'.join(str(v).replace('\n', '\n//') for v in self._volumes)[:-2]
        s += '//'+hr()
        s += '//Total volume:    %.3f m^3, %.6f t\n' % (self.volume(), self.V_mass())
        s += '//Total surface:   %.3f m^2, %.6f t\n' % (self.surface(), self.S_mass())
        s += '//Additional mass: %.6f t\n' % self._add_mass
        s += '//Additional cost: %.3f Cr\n' % self._add_cost
        s += '//Resources cost:  %.3f Cr\n' % self._res_cost
        s += 'entryCost = %.3f\n' % self.entry_cost(self._cost)
        s += 'cost = %.3f\n' % self._cost
        s += 'mass = %.6f\n' % self._init_mass
        s += 'specificMass = %s //weights: [ %s ]\n' % (', '.join(str(m) for m in self._spec_mass), 
                                                        ', '.join(str(w) for w in self._weights))
        s += 'specificCost = %s //weights: [ %s ]\n' % (', '.join(str(m) for m in self._spec_cost), 
                                                        ', '.join(str(w) for w in self._cost_weights))
        return s
    #end def
#end class
         

def hr(text='', ch='-', width=80):
    tl = len(text)
    if width-tl-2 < 0: return text
    ll = (width-tl-2)/2
    rl = width-tl-2-ll
    return '%s %s %s\n' % (ch*ll, text, ch*rl)
#end def

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
    
    steel     = material(8.05,  2.0)
    aluminium = material(2.7,   8.0)
    Al_Li     = material(2.63, 12.0)
    composits = material(1.9,  20.0)

    #inline
    inline1   = ship('InlineHangar',
                     volumes=[volume(9.4, 0.02, 'hull', 1,
                                     surface(66.444, 0.005, Al_Li),
                                     [volume(3.93, 0.227, 'machinery', 100)]),
                              volume(0.659*2, 0.02, 'doors', 1,
                                     surface(9.32*2, 0.005, Al_Li)),
                              ],
                     add_mass=0,
                     add_cost=200) #docking port
    
    inline2   = ship('InlineHangar2',
                     volumes=[volume(98.08, 0.02, 'hull', 1,
                                     surface(268.11, 0.006, Al_Li),
                                     [volume(53.66, 0.143, 'machinery', 80,
                                             subvolumes=[volume(40.45, 0.153, 'cabins', 220), #like Hitchhikers container
                                                         ]),
                                      ]),
                              volume(4.05*2, 0.02,'doors', 1,
                                     surface(35.94*2, 0.006, Al_Li)),
                              ], 
                     add_mass=0,
                     add_cost=280) #docking port

    #spaceport
    spaceport = ship('Spaceport', 
                     volumes=[volume(366.046, 0.01, 'hull', 2,
                                     surface(960.55, 0.007, composits),
                                     [volume(46.92, 0.01, 'machinery room', 15,
                                             subvolumes=[volume(1.5, 0.75, 'battery', 22500/1.5),
                                                         volume(0.95, 0.2/0.21, 'reaction wheel', 2100/0.21),
                                                         volume(1, 0.72, 'generator', 29700),
                                                         volume(2, 0.0, 'monopropellent tank', 0,
                                                                surface(11.04, 0.006, aluminium))]),
                                      volume(112.64*2, 0.168, 'cabins', 200), #density of the SpaceX Dragon vessel
                                      volume(1.5*2+8.7, 0.001, 'coridors', 1),
                                      ]),
                              volume(1.64*2, 0.01,'doors', 2,
                                     surface(28.94*2, 0.007, composits)),
                              ],
                     add_mass=2+6+0.08,  #cockpit, machinery, probe core
                     add_cost=980 + 300 + 4000 + 600,  #DockPort + Light + Cockpit + probe core
                     res_cost=2400) #Monoprop

    #landers
    lander     = ship('RoverLander', 
                      volumes=[volume(9.536, 0.01, 'hull', 2,
                                      surface(92.1, 0.004, Al_Li),
                                      [volume(3.498, 0.12, 'machinery', 220,
                                              subvolumes=[volume(0.17, 0.2/0.21, 'reaction wheel', 2100/0.21)]),
                                       volume(2.225, 0.29, 'base', 40)]),
                               volume(0.62*2+0.47*2+0.0138*4, 0.02, 'doors', 1,
                                      surface(14.19*2+13.45*2, 0.003, Al_Li),
                                      [volume(0.0138*4, 2.7, 'ramp side walls', 8),]),
                               volume(0.045, 0.98,'clamp', 600),
                               volume(0.444*2, 0.05/0.444, 'batteries', 880/0.444),
                               volume(0.186*6, 0, 'fuel tanks', 0,
                                      surface(2.39*6, 0.006, aluminium),),
                               volume(0.0225*4, 0, 'outer hydraulic cylinders', 0,
                                      surface(0.721*4, 0.008, aluminium),
                                      [volume(0.012*4, 0.8, 'inner hydraulic cylinders', 3, #hydraulic oil is ~0.8
                                              surface(0.543*4, 0.003, steel))]),
                               volume(0.002*8, 2.7, 'hinges', 8),
                               ],
                     add_mass=0.04, #probe core
                     add_cost=200 + 480, #Light + probe core
                     res_cost=324 + 89.1 + 240) #LF+Ox+MP

    #ground hangars
    small     = ship('SmallHangar',
                     volumes=[volume(13.82, 0.02, 'hull', 1,
                                     surface(145.7, 0.006, aluminium),
                                     [volume(4.7, 0.213, 'machinery', 80,
                                             subvolumes=[volume(0.3, 0.75, 'battery', 4500/0.3)])]),
                              volume(0.74, 0.02, 'doors', 1,
                                     surface(14.43, 0.006, aluminium)),
                              volume(0.18, 0.78,'clamp', 300)
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=100 + 200 + 480) #Light + DockPort + probe core
    
    big       = ship('BigHangar', 
                     volumes=[volume(527.4, 0.01, 'hull', 2,
                                     surface(1667.79, 0.01, composits),
                                     [volume(218.99, 0.183, 'cabins', 150,
                                             subvolumes=[volume(27.75, 0.246, 'machinery', 80,
                                                                subvolumes=[volume(1.5, 0.75, 'battery', 22500/1.5),
                                                                            volume(1.14, 0.72, 'generator', 29700)])])]),
                              volume(17.89, 0.01,'doors', 2,
                                     surface(124.08, 0.01, composits)),
                              volume(4.34, 0.78,'clamp', 300),
                              ],
                     add_mass=0.04, #probe core
                     add_cost=280 + 300 + 480) #DockPort +  Light + probe core
    
    #utilities
    adapter  = ship('Adapter', 
                     volumes=[volume(2.845, 0.0, 'hull', 50,
                                     surface(13.02, 0.006, composits))], 
                     add_mass=0,
                     add_cost=0)
    
    r_adapter2 = ship('Radial Adapter 2', 
                     volumes=[volume(2.09+0.163*2, 0.01, 'hull', 50,
                                     surface(10.01+1.86*2, 0.006, Al_Li))], 
                     add_mass=0,
                     add_cost=0)
    
    r_adapter1 = ship('Radial Adapter 1', 
                     volumes=[volume(1.24+0.213+0.163, 0.01, 'hull', 50,
                                     surface(6.37+2.37+1.86, 0.006, Al_Li))], 
                     add_mass=0,
                     add_cost=0)
    
    station_hub = ship('Station Hub', 
                     volumes=[volume(7.49, 0.0122, 'hull', 80,
                                     surface(29.76, 0.018, Al_Li))], 
                     add_mass=0,
                     add_cost=0)
    
    docking_port = ship('Docking Port', 
                     volumes=[volume(0.635, 0.1, 'hull', 1380,
                                     surface(13.89, 0.005, aluminium))], 
                     add_mass=0,
                     add_cost=0)
    
    rcs       = ship('SpaceportRCS', 
                     volumes=[volume(0.36, 0.3, 'machinery', 4760,
                                     surface(4.77, 0.007, composits))],
                     add_mass=0,
                     add_cost=0)
    
    heatshield = ship('Square Heatshield', 
                     volumes=[volume(3.8, 0.01, 'hull', 20,
                                     surface(40.7, 0.005, aluminium))], 
                     add_mass=0,
                     add_cost=0)
    
    recycler   = ship('Recycler', 
                     volumes=[volume(4.39, 0.001, 'hull', 10,
                                     surface(14.9, 0.005, aluminium),
                                     [volume(2.3, 0.317, 'machinery', 1000)]),
                              volume(0.18, 0.78,'clamp', 3000)], 
                     add_mass=0,
                     add_cost=0,
                     res_cost=24920)
    
    l1 = 1#.27624
    l2 = 1#.03
    inline1_m   = np.fromiter((inline1.mass(s/2, l1) for s in scales), float)
    inline2_m   = np.fromiter((inline2.mass(s/2, l2) for s in scales), float)
    spaceport_m = np.fromiter((spaceport.mass(s/3, 1) for s in scales), float)
     
    inline1_sm   = np.fromiter((inline1.S_mass(s/2, l1) for s in scales), float)
    inline2_sm   = np.fromiter((inline2.S_mass(s/2, l2) for s in scales), float)
    spaceport_sm = np.fromiter((spaceport.S_mass(s/3, 1) for s in scales), float)
    
    inline1_v   = np.fromiter((inline1.volume(s/2, l1) for s in scales), float)
    inline2_v   = np.fromiter((inline2.volume(s/2, l2) for s in scales), float)
    spaceport_v = np.fromiter((spaceport.volume(s/3, 1) for s in scales), float)
    
    inline1_c   = np.fromiter((inline1.cost(s/2, l1) for s in scales), float)
    inline2_c   = np.fromiter((inline2.cost(s/2, l2) for s in scales), float)
    spaceport_c = np.fromiter((spaceport.cost(s/3, 1) for s in scales), float)
    
    lander_m    = np.fromiter((lander.mass(s/2, 1) for s in scales), float)
    lander_sm   = np.fromiter((lander.S_mass(s/2, 1) for s in scales), float)
    lander_v    = np.fromiter((lander.volume(s/2, 1) for s in scales), float)
    lander_c    = np.fromiter((lander.cost(s/2, 1) for s in scales), float)
     
    lg1 = 1#.3981227
    small_m  = np.fromiter((small.mass(s/2, lg1) for s in scales), float)
    big_m    = np.fromiter((big.mass(s/3, 1) for s in scales), float)

    small_sm = np.fromiter((small.S_mass(s/2, lg1) for s in scales), float)
    big_sm   = np.fromiter((big.S_mass(s/3, 1) for s in scales), float)
    
    small_v  = np.fromiter((small.volume(s/2, lg1) for s in scales), float)
    big_v    = np.fromiter((big.volume(s/3, 1) for s in scales), float)
    
    small_c  = np.fromiter((small.cost(s/2, lg1) for s in scales), float)
    big_c    = np.fromiter((big.cost(s/3, 1) for s in scales), float)
    
    adapter_m  = np.fromiter((adapter.mass(s, lg1) for s in scales), float)
    adapter_sm = np.fromiter((adapter.S_mass(s, lg1) for s in scales), float)
    adapter_v  = np.fromiter((adapter.volume(s, lg1) for s in scales), float)
    adapter_c  = np.fromiter((adapter.cost(s, lg1) for s in scales), float)
    
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
    print(format_data(scales, (lander_m, lander_sm, lander_v, lander_c)))
    
    print(small); print('length: %s' % lg1)
    print(format_data(scales, (small_m, small_sm, small_v, small_c), np.where(scales/2 >= 1)[0]))

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
    
    print(recycler)
    
    sys.exit(0)
    
    c = np.arange(1, 2e5, 1e3)
#     plt.plot(c, np.fromiter((ship.entry_cost(ci) for ci in c), dtype=float))
    plt.plot(c, np.fromiter((ship.entry_cost(ci)/ci for ci in c), dtype=float))
    plt.show()

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