import numpy as np
import matplotlib.pyplot as plt
import sys

from base_classes import material, surface, volume, ship
from components import battery, generator, reaction_wheel


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
    lavsan    = material(300e-6/0.001, 1)

    #inline
    inline1   = ship('InlineHangar',
                     volumes=[volume(9.4, 'hull', 1, 0.02, -1,
                                     surface(66.444, 0.005, Al_Li),
                                     [volume(3.93, 'machinery', 100, 0.227)]),
                              volume(0.659*2, 'doors', 1, 0.02, -1,
                                     surface(9.32*2, 0.005, Al_Li)),
                              ],
                     add_mass=0,
                     add_cost=200) #docking port
    
    inline2   = ship('InlineHangar2',
                     volumes=[volume(98.08, 'hull', 1, 0.02, -1,
                                     surface(268.11, 0.006, Al_Li),
                                     [volume(53.66, 'machinery', 80, 0.143, -1,
                                             subvolumes=[volume(40.45, 'cabins', 220, 0.153)])]), #like Hitchhikers container
                              volume(4.05*2, 'doors', 1, 0.02, -1,
                                     surface(35.94*2, 0.006, Al_Li)),
                              ], 
                     add_mass=0,
                     add_cost=280) #docking port

    #spaceport
    spaceport = ship('Spaceport', 
                     volumes=[volume(366.046, 'hull', 2, 0.01, -1,
                                     surface(960.55, 0.007, composits),
                                     [volume(46.92, 'machinery room', 15, 0.01, -1,
                                             subvolumes=[battery(V=-1, energy=20000),
                                                         reaction_wheel(0.95),
                                                         generator(V=-1, energy=6.75),
                                                         volume(2, 'monopropellent tank', 0, 0, -1,
                                                                surface(11.04, 0.006, aluminium))]),
                                      volume(112.64*2, 'cabins', 200, 0.168), #density of the SpaceX Dragon vessel
                                      volume(1.5*2+8.7, 'coridors', 1, 0.001),
                                      ]),
                              volume(1.64*2, 'doors', 2, 0.01, -1,
                                     surface(28.94*2, 0.007, composits)),
                              ],
                     add_mass=2+6+0.08,  #cockpit, machinery, probe core
                     add_cost=980 + 300 + 4000 + 600,  #DockPort + Light + Cockpit + probe core
                     res_cost=2400) #Monoprop

    #landers
    lander     = ship('RoverLander', 
                      volumes=[volume(9.536, 'hull', 2, 0.01, -1,
                                      surface(92.1, 0.004, Al_Li),
                                      [volume(3.498, 'machinery', 220, 0.12, -1,
                                              subvolumes=[reaction_wheel(0.17)]),
                                       volume(2.225, 'base', 40, 0.29)]),
                               volume(0.62*2+0.47*2+0.0138*4, 'doors', 1, 0.02, -1,
                                      surface(14.19*2+13.45*2, 0.003, Al_Li),
                                      [volume(0.0138*4, 'ramp side walls', 8, 2.7),]),
                               volume(0.045, 'clamp', 600, 0.98),
                               battery(V=0.444*2, energy=2000),
                               volume(0.186*6, 'fuel tanks', 0, 0, -1,
                                      surface(2.39*6, 0.006, aluminium),),
                               volume(0.0225*4, 'outer hydraulic cylinders', 0, 0, -1,
                                      surface(0.721*4, 0.008, aluminium),
                                      [volume(0.012*4, 'inner hydraulic cylinders', 3, 0.8, -1,  #hydraulic oil is ~0.8
                                              surface(0.543*4, 0.003, steel))]),
                               volume(0.002*8, 'hinges', 8, 2.7),
                               ],
                     add_mass=0.04, #probe core
                     add_cost=200 + 480, #Light + probe core
                     res_cost=324 + 89.1 + 240) #LF+Ox+MP

    #ground hangars
    small     = ship('SmallHangar',
                     volumes=[volume(13.82, 'hull', 1, 0.02, -1,
                                     surface(145.7, 0.006, aluminium),
                                     [volume(4.7, 'machinery', 350, -1, 0.938,
                                             subvolumes=[battery(V=1.7, energy=4000)])]),
                              volume(0.74, 'doors', 1, 0.02, -1,
                                     surface(14.43, 0.006, aluminium)),
                              volume(0.18,'clamp', 300, 0.78)
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=100 + 480) #Light + probe core
    
    big       = ship('BigHangar',
                     volumes=[volume(527.4, 'hull', 2, 0.01, -1,
                                     surface(1667.79, 0.01, composits),
                                     [volume(218.99, 'cabins', 150, 0.183, -1,
                                             subvolumes=[volume(38.15, 'machinery', 2460, -1, 6.25, 
                                                                subvolumes=[battery(V=-1, energy=40000),
                                                                            generator(V=-1, energy=10)])])]),
                              volume(17.89, 'doors', 2, 0.01, -1,
                                     surface(124.08, 0.01, composits)),
                              volume(4.34, 'clamp', 300, 0.78),
                              ],
                     add_mass=0.04, #probe core
                     add_cost=300 + 480) #Light + probe core
    
    inflatable1 = ship('InflatableHangar1',
                     volumes=[volume(0.469, 'hull', 1, 0.02, -1,
                                     surface(11.82, 0.01, aluminium)),
                              volume(0.019*4, 'doors', 1, 0.02, -1,
                                     surface(1.32*4, 0.005, aluminium)),
                              battery(V=0.02245*2, energy=200),
                              volume(0.00002*8, 'hinges', 8, 2.7),
                              volume(6.96, 'hangar', 1, 0.0012, -1,
                                     surface(136.24, 0.001, lavsan)),
                              volume(0.67, 'hangar-door', 1, 0.0012, -1,
                                     surface(15.06, 0.001, lavsan)),
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=480) #probe core
    
    inflatable2 = ship('InflatableHangar2',
                     volumes=[volume(0.469, 'hull', 1, 0.02, -1,
                                     surface(11.82, 0.01, aluminium)),
                              volume(0.019*4, 'doors', 1, 0.02, -1,
                                     surface(1.32*4, 0.005, aluminium)),
                              battery(V=0.02245*2, energy=200),
                              generator(V=0.00396*4),
                              volume(0.00002*8, 'hinges', 8, 2.7),
                              volume(6.96, 'hangar', 1, 0.0012, -1,
                                     surface(136.24, 0.001, lavsan)),
                              volume(0.67, 'hangar-door', 1, 0.0012, -1,
                                     surface(15.06, 0.001, lavsan)),
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=480) #Light + probe core
    
    #utilities
    adapter  = ship('Adapter', 
                     volumes=[volume(2.845, 'hull', 50, 0, -1,
                                     surface(13.02, 0.006, composits))], 
                     add_mass=0,
                     add_cost=0)
    
    r_adapter2 = ship('Radial Adapter 2', 
                     volumes=[volume(2.09+0.163*2, 'hull', 50, 0.01, -1,
                                     surface(10.01+1.86*2, 0.006, Al_Li))], 
                     add_mass=0,
                     add_cost=0)
    
    r_adapter1 = ship('Radial Adapter 1', 
                     volumes=[volume(1.24+0.213+0.163, 'hull', 50, 0.01, -1,
                                     surface(6.37+2.37+1.86, 0.006, Al_Li))], 
                     add_mass=0,
                     add_cost=0)
    
    station_hub = ship('Station Hub', 
                     volumes=[volume(7.49, 'hull', 80, 0.0122, -1,
                                     surface(29.76, 0.018, Al_Li))], 
                     add_mass=0,
                     add_cost=0)
    
    docking_port = ship('Docking Port', 
                     volumes=[volume(0.635, 'hull', 1380, 0.1, -1, 
                                     surface(13.89, 0.005, aluminium))], 
                     add_mass=0,
                     add_cost=0)
    
    rcs       = ship('SpaceportRCS', 
                     volumes=[volume(0.36, 'machinery', 4760, 0.3, -1, 
                                     surface(4.77, 0.007, composits))],
                     add_mass=0,
                     add_cost=0)
    
    heatshield = ship('Square Heatshield', 
                     volumes=[volume(3.8, 'hull', 20, 0.01, -1,
                                     surface(40.7, 0.005, aluminium))], 
                     add_mass=0,
                     add_cost=0)
    
    recycler   = ship('Recycler', 
                     volumes=[volume(4.39, 'hull', 10, 0.001, -1,
                                     surface(14.9, 0.005, aluminium),
                                     [volume(2.3, 'machinery', 1000, 0.317)]),
                              volume(0.18,'clamp', 3000, 0.78)], 
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
    
    print(inflatable1);
    print(inflatable2);
    
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