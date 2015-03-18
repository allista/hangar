import numpy as np
import sys

from base_classes import material, surface, volume, part
from components import *


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
    
    inline1   = part('InlineHangar',
                     [volume(9.4, 'hull', C=1, D=0.02, 
                             S=surface(66.444, 0.004, Al_Li),
                             V=[volume(3.93, 'machinery', C=850, M=0.430)]),
                      volume(0.659*2, 'doors', C=1, D=0.02,
                             S=surface(9.32*2, 0.003, Al_Li)),
                      ],
                     size = 2,
                     add_mass=0,
                     add_cost=200) #docking port
    
    inline2   = part('InlineHangar2',
                     [volume(98.08, 'hull', C=1, D=0.02,
                             S=surface(268.11, 0.005, Al_Li),
                             V=[volume(53.66, 'top compartment', C=20, D=0.05,
                                       V=[volume(7.34, 'cabins', C=4000, M=0.35*3, N=2,
                                                 S=surface(25.19, 0.003, Al_Li)),
                                          volume(15.5, 'corridor', C=2, D=0.0012,
                                                 S=surface(41.33, 0.003, Al_Li)),
                                          volume(9.17, 'machinery', C=1900, M=1.2)])]),
                      volume(4.05, 'doors', C=1, D=0.02, N=2,
                             S=surface(35.94, 0.004, Al_Li)),
                      ], 
                     size = 2,
                     add_mass=0,
                     add_cost=280) #docking port
    
    radial_hangar = part('RadialHangar',
                     [volume(19.36, 'hull', C=1, D=0.02,
                             S=surface(224.795, 0.004, Al_Li),
                             V=[volume(10.38, 'machinery', C=1100, M=0.9)]),
                      volume(4.58, 'base', C=1, D=0.02,
                             S=surface(26.47, 0.004, Al_Li)),
                      volume(0.55, 'doors', C=1, D=0.02, N=2,
                             S=surface(16.08, 0.004, Al_Li)),
                      ], 
                         size = 2,
                     add_mass=0,
                     add_cost=0)
    
    inline1.print_masses()
    inline2.print_masses()
    radial_hangar.print_masses()
    sys.exit()

    spaceport = part('Spaceport', 
                     [volume(366.046, 'hull', C=2, D=0.01,
                             S=surface(960.55, 0.007, composits),
                             V=[volume(46.92, 'machinery room', C=3500, M=4,
                                       V=[battery(E=20000),
                                          reaction_wheel(T=140),
                                          generator(E=6.75),
                                          volume(2, 'monopropellent tank', 
                                                 S=surface(11.04, 0.003, Al_Li))]),
                              volume(112.64, 'side-space', C=20, D=0.05, N=2,
                                     V=[volume(2.88, 'cabins', C=2000, M=0.35, N=5,
                                               S=surface(12.8, 0.01, compositsL)),
                                        volume(27.25, 'corridors', C=1, D=0.0012,
                                               S=surface(93.66, 0.01, compositsL)),
                                        volume(8.79, 'doors machinery', C=800, M=0.35,
                                               S=surface(26.44, 0.01, compositsL))]),
                              volume(1.5*2+8.7, 'corridors', C=1, D=0.0012)
                              ]),
                      volume(1.64, 'doors', C=2, D=0.01, N=2,
                             S=surface(28.94, 0.007, composits)),
                              ],
                     size = 3,
                     add_mass=4+0.08, #cockpit, probe core
                     add_cost=980 + 300 + 4000 + 3400,  #DockPort + Light + Cockpit + probe core
                     res_cost=2400) #Monoprop

    lander     = part('RoverLander', 
                      [volume(9.536, 'hull', C=2, D=0.01,
                              S=surface(92.1, 0.004, Al_Li),
                              V=[volume(3.498, 'machinery', C=920, M=0.30),
                                 volume(2.225, 'base', C=40, D=0.3,
                                        V=[reaction_wheel(T=25)])]),
                       volume(0.58+0.0138*2, 'doors', C=1, D=0.02, N=2,
                              S=surface(13.9, 0.003, Al_Li),
                              V=[volume(0.0138, 'ramp side walls', N=2, material=Al_Li),]),
                       volume(0.47+0.016*2, 'legs', C=1, D=0.02, N=2,
                              S=surface(13.425, 0.003, Al_Li),
                              V=[volume(0.016, 'ribs', C=1, D=0.02,
                                        S=surface(1.13, 0.003, Al_Li))]),
                       volume(0.045, 'clamp', C=600, D=0.98),
                       battery(V=0.444*2, E=2000),
                       volume(0.186, 'fuel tanks', N=6,
                              S=surface(2.39, 0.002, Al_Li)),
                       volume(0.0225, 'hydraulic cylinders', N=4,
                              S=surface(0.721, 0.008, aluminium),
                              V=[volume(0.012, 'inner hydraulic cylinders', C=3, D=0.8, #hydraulic oil is ~0.8
                                      S=surface(0.543, 0.006, aluminium))]),
                       volume(0.002, 'hinges', N=8, material=aluminium),
                       ],
                     add_mass=0.04, #probe core
                     add_cost=200 + 1480, #Light + probe core
                     res_cost=324 + 89.1 + 240, #LF+Ox+MP
                     size=2)

    #ground hangars
    small     = part('SmallHangar',
                     [volume(9.62, 'hull', C=1, D=0.02, 
                             S=surface(149.24, 0.004, aluminium),
                             V=[volume(4.7, 'machinery', C=550, M=0.530,
                                       V=[battery(E=2000)])]),
                      solar_panel(2.413),#4.825),
                      volume(0.182, 'doors', C=1, D=0.02,
                             S=surface(15.17, 0.004, aluminium)),
                      volume(0.18,'clamp', C=300, D=0.78)
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=100 + 300) #Light + probe core
    
    big       = part('BigHangar',
                     [volume(377.84, 'hull', C=2, D=0.01,
                             S=surface(1709.32, 0.01, composits),
                             V=[volume(218.99, 'rear-compartment', C=150, D=0.02,
                                       V=[volume(12.126, 'cabins', C=3000, M=1, N=6,
                                                 S=surface(31.88, 0.01, composits)),
                                          volume(11.12, 'corridors', C=150, D=0.0012, N=3,
                                                 S=surface(40.6, 0.01, composits)),
                                          volume(67.57, 'machinery', C=4260, M=5.4, 
                                                 V=[battery(V=20, E=40000),
                                                    generator(E=10)])])]),
                      volume(6.07, 'doors', C=2, D=0.01,
                             S=surface(132.66, 0.01, composits)),
                      volume(4.34, 'clamp', C=300, D=0.78),
                              ],
                     add_mass=0.04, #probe core
                     add_cost=300 + 300) #Light + probe core
    
    #inflatables
    inflatable1 = part('InflatableHangar1',
                     [volume(0.444, 'hull', C=1, D=0.02,
                             S=surface(11.95, 0.01, Al_Li)),
                      volume(0.019, 'doors', C=1, D=0.02, N=4,
                             S=surface(1.32, 0.005, Al_Li)),
                      battery(V=0.02245*2, E=200),
                      volume(0.00002, 'hinges', N=8, material=aluminium),
                      volume(6.96, 'hangar', C=1, D=0.0012, 
                             S=surface(136.24, 0.001, lavsan)),
                      volume(0.67, 'hangar-door', C=1, D=0.0012,
                             S=surface(15.06, 0.001, lavsan)),
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=300) #probe core
    
    inflatable2 = part('InflatableHangar2',
                     [volume(0.444, 'hull', C=1, D=0.02,
                             S=surface(11.95, 0.01, composits)),
                      volume(0.019*4, 'doors', C=1, D=0.02,
                             S=surface(1.32*4, 0.005, composits)),
                      battery(V=0.02245, E=100),
                      generator(V=0.00595),
                      volume(0.067, 'compressor-motor', C=1200, M=0.1),
                      volume(0.0063, 'compressor-cylinders', C=15000, D=0.81, N=2),
                      volume(0.00054, 'compressor-fixers', N=2,
                             S=surface(0.087, 0.003, Al_Li)),
                      volume(0.00002, 'hinges', N=8, material=aluminium),
                      volume(6.96, 'hangar', C=1, D=0.0012,
                             S=surface(136.24, 0.001, lavsan)),
                      volume(0.67, 'hangar-door', C=1, D=0.0012,
                             S=surface(15.06, 0.001, lavsan)),
                              ], 
                     add_mass=0.04, #probe core
                     add_cost=300) #probe core
    
    mk3_hangar   = part('Mk3Hangar',
                     [volume(130.43, 'hull', C=100, D=0.02,
                             S=surface(549.29, 0.002, Al_Li),
                             V=[volume(5, 'machinery', C=1200, M=0.7),
                                generator(E=1.5),
                                battery(E=5000),
                                reaction_wheel(T=120),
                                volume(0.011, 'hydraulics', N=2, 
                                       material=aluminium),
                                volume(100, 'tanks', C=20)]),
                      volume(3.96, 'doors', C=100, D=0.02,
                             S=surface(70.86, 0.002, Al_Li)),
                      ], 
                     add_mass=0,
                     add_cost=0)
    
    fairings_octo = part('FairingsOcto',
                     [volume(1.11, 'hull', C=1, D=0.02,
                             S=surface(35.398 , 0.001, Al_Li),
                             V=[volume(0.427, 'base', C=1, D=0.02,
                                       S=surface(4.543, 0.002, Al_Li),
                                       V=[
                                          reaction_wheel(T=10),
                                          battery(E=50),
                                          volume(0.1, 'jettison-charge', C=400, M=0.05)])]),
                      volume(0.149, 'petals', N=2,
                             S=surface(6.096, 0.002, composits)),
                      ], 
                     add_mass=0.04,
                     add_cost=450)
    
    #utilities
    adapter  = part('Adapter', 
                     [volume(2.845, 'hull', C=50,
                             S=surface(13.02, 0.006, composits))])
    
    r_adapter2 = part('Radial Adapter 2', 
                     [volume(2.09+0.163*2, 'hull', C=50, D=0.01,
                             S=surface(10.01+1.86*2, 0.006, Al_Li))])
    
    r_adapter1 = part('Radial Adapter 1', 
                     [volume(1.24+0.213+0.163, 'hull', C=50, D=0.01,
                             S=surface(6.37+2.37+1.86, 0.006, Al_Li))])
    
    station_hub = part('Station Hub', 
                     [volume(7.49, 'hull', C=80, D=0.002,
                             S=surface(29.76, 0.01, Al_Li))])
    
    docking_port = part('Docking Port', 
                     [volume(0.635, 'hull', C=1380, D=0.1, 
                             S=surface(13.89, 0.005, aluminium))])
    
    rcs       = part('SpaceportRCS', 
                     [volume(0.36, 'machinery', C=4760, D=0.3, 
                             S=surface(4.77, 0.007, composits))])

    small_heatshield = part('SquareHeatshield2', 
                     [volume(0.0627, 'hull', C=500, D=0.75,
                             S=surface(3.44, 0.005, aluminium))])
    
    heatshield = part('SquareHeatshield', 
                     [volume(3.8, 'hull', C=20, D=0.01,
                             S=surface(40.7, 0.005, aluminium))])
    
    srf_tail   = part('SurfaceTail', 
                     [volume(0.6, 'hull', C=20, D=0.01,
                             S=surface(6.85, 0.003, Al_Li))])
    
    airbrake   = part('Airbrake',
                          [volume(0.19, 'hull', C=2000, D=0.01,
                                  S=surface(6.5, 0.002, Al_Li)),
                           volume(0.0051, 'outer-cylinder', material=Al_Li,
                                  V=[volume(0.0012, 'inner_cylinder', material=Al_Li)]),
                           volume(0.001+0.00092+0.00064+0.0008+0.0022, 
                                  'hinges-axis', material=Al_Li),
                           volume(0.039, 'brake', 
                                  S=surface(3.97, 0.002, Al_Li)),
                           ])
    
    krent700   = part('Krent700', 
                     [volume(20.17, 'hose', C=200, D=0.02,
                             S=surface(62.916, 0.001, Al_Li),
                             V=[volume(10.47, 'engine', M=5.54, C=1000)]),
                      volume(1.206, 'fixer', C=100, D=0.1)])
    
    rad_sabre  = part('RadialSabre', 
                     [volume(3.727, 'hull', C=200, D=0.02,
                             S=surface(26.07, 0.003, Al_Li),
                             V=[volume(3, 'engines', M=1.3, C=8000)])])
    
    rad_heavy  = part('RadialHeavyEngine', 
                     [volume(1.46, 'hull', C=200, D=0.02,
                             S=surface(10.75, 0.003, Al_Li),
                             V=[volume(0.8, 'engines', M=0.95, C=2000)])])
    
    hover_fan  = part('HoverFan', 
                     [volume(0.076, 'base', C=200, D=0.01,
                             S=surface(1.228, 0.003, Al_Li)),
                      volume(0.01, 'motor-fixer', C=200, D=0.01,
                             S=surface(0.524, 0.003, Al_Li)),
                      volume(0.023, 'central-fixer', C=200, D=0.01,
                             S=surface(1.59, 0.003, Al_Li)),
                      volume(0.118, 'stator', C=200, D=0.01,
                             S=surface(7.227, 0.003, composits)),
                      volume(0.08, 'motor', C=2000, M=0.2),
                      volume(0.002, 'blades', C=200, D=0.01, N=4,
                             S=surface(0.42, 0.001, composits)),
                      ])
    
#     def hover_takeoff_weight(thrust, k, size, netto=True):
#         m = hover_fan.mass(size) if netto else 0
#         return (thrust*k*size**2/9.81-m)*4/2.0 
#     
#     def hover_w_vs_s(thrust, k=0.8):
#         print np.array([(s, hover_takeoff_weight(thrust, k, s), hover_takeoff_weight(thrust, k, s, False)) for s in np.arange(0.5, 4.5, 0.5)])
#         print
#         
#     hover_w_vs_s(50); hover_w_vs_s(40); hover_w_vs_s(30); hover_w_vs_s(20);
#     
#     sys.exit()
    
    turbogen   = part('TurboGenerator', 
                     [volume(1.958, 'hull', C=200, D=0.01,
                             S=surface(13.13, 0.001, Al_Li),
                             V=[volume(1.5, 'turboshaft', M=0.327, C=1000)]),
                      volume(0.095, 'compressor', C=200, D=0.01,
                             S=surface(0.525, 0.003, Al_Li)),
                      ])
    
    #extensions
    extension  = part('HangarExtension',
                     [volume(19.43, 'hull', C=500, M=0.2,
                             S=surface(41.56, 0.006, Al_Li),
                             V=[volume(19.43*0.9**3, 'storage')])])
    
    extensionL = part('HangarExtensionL',
                     [volume(68.47, 'hull', C=500, M=0.3,
                             S=surface(94.59, 0.006, Al_Li),
                             V=[volume(47.33, 'storage')])])
    
    extensionXL = part('HangarExtensionXL',
                     [volume(117.98, 'hull', C=1200, M=0.4,
                             S=surface(129.39, 0.006, Al_Li),
                             V=[volume(88.43, 'storage')])])
    
    #ExLP
    recycler   = part('Recycler', 
                     [volume(4.39, 'hull', C=10, D=0.01,
                             S=surface(14.9, 0.005, aluminium),
                             V=[volume(2.3, 'machinery', C=1000, D=0.317),
                                volume(2, 'metal-tank', C=20)]),
                      volume(0.18,'clamp', C=3000, D=0.78)])
    

    
    #asteroid hangars
    struct_grapple = part('StructuralGrapple',
                          [volume(2.76, 'hull', C=200, D=0.01,
                                  S=surface(23.83, 0.006, Al_Li),
                                  V=[battery(E=1000),
                                     volume(1.0, 'machinery', C=500, D=0.3)]),
                           volume(0.026, 'outer-cylinders', N=4, material=aluminium,
                                  V=[volume(0.0025, 'inner_cylinders', material=aluminium)]),
                           volume(0.008+0.0007+0.0009+0.0036+0.0018+0.001+0.006, 
                                  'levers-axis', material=aluminium, N=4),
                           volume(0.039, 'clinches', C=1000, D=0.9, N=4),
                           volume(0.012, 'clinch-caps', C=100, D=0.1, N=4,
                                  S=surface(0.97, 0.006, Al_Li)),
                           ])
    
    asteroid_port = part('SquarePort',
                         [volume(7.99, 'hull', C=3180, M=1.2, 
                                 S=surface(94.89, 0.003, steel))
                          ])
    
    asteroid_port_adapter = part('SquarePortAdater',
                          [volume(3.96, 'hatch-port', C=2053, M=0.8, 
                                  S=surface(38.78, 0.006, Al_Li)),
                           volume(0.276, 'hatch-port-support', C=100, D=0.1, N=4, 
                                  S=surface(4.93, 0.006, Al_Li)),
                           volume(2.3, 'S2-port', C=100, D=0.1, 
                                  S=surface(13.62, 0.006, Al_Li),
                                  V=[battery(E=2000),
                                     reaction_wheel(T=32)])
                           ],
                          add_cost=400) #light
    
    asteroid_hatch = part('AsteroidHatch',
                          [volume(1.45, 'frames', C=1000, D=0.9, N=2,
                                  S=surface(27.34, 0.005, steel),
                                  V=[battery(E=1000)]),
                           volume(0.027, 'outer-cylinders', N=4, material=steel,
                                  V=[volume(0.023, 'bolts', material=steel)]),
                           volume(0.36, 'clamps', C=1000, D=0.9, N=4, 
                                  S=surface(8.77, 0.005, steel)),
                           volume(0.031, 'hinges', C=500, D=0.7, N=8),
                           ])
    
    asteroid_drill = part('AsteroidDrill',
                         [volume(143.63, 'base', C=2, D=0.05,
                                 S=surface(171.22, 0.005, steel),
                                 V=[volume(120, 'machinery', C=8350, M=12),
                                    volume(10, 'rock-tank', C=20),
                                    volume(5, 'rcs-tank', C=20),
                                    reaction_wheel(V=7),
                                    generator(E=15)]),
                          volume(43.08, 'main-drill', C=23780, M=6,
                                 S=surface(81.66, 0.005, steel)),
                          volume(0.68, 'drill-support', C=2, D=0.01, N=4,
                                 S=surface(9.16, 0.01, steel)),
                          volume(2.92, 'motors', C=1370, M=4, N=4),
                          battery(V=1.45*4),
                          ]+asteroid_port.volumes,
                         add_mass=0.04, #probe core
                         add_cost=3400) #probe core
    
    asteroid_gateway = part('AsteroidGateway', 
                         [volume(24.24, 'hull', C=100, D=0.1,
                                 S=surface(219.04, 0.007, Al_Li),
                                 V=[battery(E=10000),
                                    reaction_wheel(V=6.0),
                                    generator(E=6.0),
                                    volume(5, 'monopropellent tank', C=20)]),
                          volume(1.54, 'doors', C=2, D=0.01, N=2,
                                 S=surface(32.2, 0.007, Al_Li)),
                          volume(22.62, 'cabins', C=100, D=0.1, N=2, 
                                 S=surface(52.17, 0.007, Al_Li),
                                 V=[volume(4.23, 'corridor', C=1, D=0.0012,
                                           S=surface(18.26, 0.001, composits)),
                                    volume(2.3, 'cabins', C=2000, M=0.35, N=6,
                                           S=surface(10.54, 0.001, composits)),
                                    volume(0.0009, 'lamp-fixer', material=aluminium),
                                    volume(0.013, 'lamp', C=10000, D=0.5,
                                           S=surface(0.41, 0.0004, aluminium)), 
                                    volume(0.18, 'door', C=100, D=0.1,
                                           S=surface(0.71, 0.007, Al_Li)),
                                    volume(0.0017+0.0027+0.002+0.0008, 'ladder', material=aluminium),
                                    volume(0.0013, 'ladder-fixer', material=steel),
                                    ]),
                          ]+asteroid_port.volumes,
                         add_mass=0.08, #probe core
                         add_cost=600 + 3400,  #Light + probe core
                         res_cost=0)
    
    ore_converter = part('RockOreConverter',
		                 [volume(12.46, 'hull', C=1, D=0.02,
		                         S=surface(29.98, 0.006, Al_Li),
		                         V=[volume(12.0, 'machinery', C=2850, M=1.530)])])
    
    mobile_smelter = part('MobileSmelter',
		                 [volume(12.46, 'hull', C=1, D=0.02,
		                         S=surface(29.98, 0.003, steel),
		                         V=[volume(12.0, 'machinery', C=6970, M=0.730)])])
    
    mobile_smelter = part('SubstrateMixer',
                         [volume(12.46, 'hull', C=1, D=0.02,
                                 S=surface(29.98, 0.006, Al_Li),
                                 V=[volume(2.0, 'machinery', C=1730, M=0.330),
                                    volume(10, 'tanks', C=20)])])
    
    small_tank    = part('*TankS',
		                 [volume(19.43/2, 'hull', 
		                         S=surface(25.56, 0.006, Al_Li))])
    
    radial_tank   = part('RadialTank',
		                 [volume(0.25, 'hull', C=1, D=0.2,
                                 S=surface(2.23, 0.005, Al_Li),
		                         V=[volume(0.241, 'container')]),
                          volume(0.006, 'door', C=12, D=2.63)])
    
    print('//:mode=c#:') #for JEdit, Vim and others
