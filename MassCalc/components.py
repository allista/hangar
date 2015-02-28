from base_classes import volume, surface, material
import numpy as np

steel      = material(8.05,  2.0)
aluminium  = material(2.7,   8.0)
Al_Li      = material(2.63, 12.0)
composits  = material(1.9,  20.0)
compositsL = material(1.3,  18.0)
lavsan     = material(300e-6/0.001, 1)

class _custom_volume(volume):
    _name = 'custom volume'
    _density = 1
    _cost_density = 1
    
    def __init__(self, vol):
        volume.__init__(self, vol, self._name, C=self._cost_density, D=self._density)
        
    def _add_str(self): return ''
    
    def __str__(self):
        s  = volume.__str__(self)
        s += self._add_str()
        return s
#end class


class reaction_wheel_real(_custom_volume):
    '''
    This is an accurate model of the series of the stock reaction wheels.
    Useless until I implement custom polynomial curves for each parameter.
    '''

    _name = 'reaction wheel'
    _density = 0.0
    _cost_density = 0.0
    
    _hd_ratio = 5.0
    
    @staticmethod
    def cylV(d, h): return np.pi*d**2/4.0*h
    
    @classmethod
    def cylS(cls, v):
        d=(v/np.pi*4.0*cls._hd_ratio)**(1/3.0)
        h=d/cls._hd_ratio
        return np.pi*d*h
    
    @classmethod
    def _vol2mass(cls, v):
        return 0.0897259823*v**(2/3.0)+0.0370799071
    
    @classmethod
    def _vol2torque(cls, v):
        return 19.5152925063*v**0.48
    
    @classmethod
    def _vol2cost(cls, v):
        return 1170.13502*v**0.48 +300.922491
    
    @classmethod
    def _vol2energy(cls, v):
        return 1.0295852148*v**(0.1)-0.5253809288
    
    def __init__(self, vol):
        _custom_volume.__init__(self, vol)
        mat = material(steel.density, self._vol2cost(vol))
        s = self.cylS(vol)
        h = self._vol2mass(vol)/mat.density/s
        mat.cost = mat.cost/s/h*surface.unit_h
        volume.__init__(self, vol, self._name, C=0, D=0,
                        S=surface(s, h, mat))
        
    @property
    def torque(self): return self._vol2torque(self._V)
    
    @property
    def energy(self): return self._vol2energy(self._V)
    
    def _add_str(self):
        s  = '   torque = %.0f\n' % (self._vol2torque(self._V)) 
        s += '   rate = %.3f\n' % (self._vol2energy(self._V))
        return s
#end class

class reaction_wheel(_custom_volume):
#     s: 0.05 t / 5 trq / 0.25 ec / 600 cr / 0.0592276609 m3 / D= 0.8442001463542519
#     m: 0.1 t / 15 trq / 0.45 ec / 1200 cr / 0.572555261117 m3 / D= 0.1746556302790925 / S = 1.48814376854
#     l: 0.2 t / 30 trq / 0.6 ec / 2100 cr / 2.45436926062 m3 / D= 0.08148733086295168
    
    _name = 'reaction wheel'
    _density = 0
    _cost_density = 0
    
    _spec_torque = 150 #torque/t
    _spec_energy = 4.5  #El.u/sec/t
    
    _thickness = 0.05
    _material = material(0.1/1.48814376854/_thickness, 
                         1200/1.48814376854/_thickness*surface.unit_h)
    
    _hd_ratio = 5.0
    
    @staticmethod
    def cylV(d, h): return np.pi*d**2/4.0*h
    
    @classmethod
    def cylS(cls, v):
        d = (v/np.pi*4.0*cls._hd_ratio)**(1/3.0)
        h = d/cls._hd_ratio
        return np.pi*d*h
    
    @classmethod
    def S2V(cls, s):
        d = np.sqrt(s*cls._hd_ratio/np.pi)
        return cls.cylV(d, d/cls._hd_ratio)
    
    def __init__(self, **kwargs):
        V = kwargs.get('V', -1.0)
        T = kwargs.get('T', -1.0)
        if V < 0 and T < 0: 
            raise ValueError("%s: either volume or torque should be provided." % self._name)
        if V < 0: V = self.S2V(T/(self._thickness*self._material.density*self._spec_torque))
        _custom_volume.__init__(self, V)
        volume.__init__(self, V, self._name, C=0, D=0,
                        S=surface(self.cylS(V), self._thickness, self._material))
         
    @property
    def torque(self): return self._spec_torque*self.full_mass()
     
    @property
    def energy(self): return self._spec_energy*self.full_mass()
     
    def _add_str(self):
        m = self.full_mass()
        s  = '   torque = %.0f\n' % (self._spec_torque*m) 
        s += '   rate = %.3f\n' % (self._spec_energy*m)
        return s
#end class

class solar_panel(_custom_volume):
    _name = 'solar panels'
    _density = 0
    _cost_density = 0
    
    _thickness = 0.01
    _surface_energy = 1.3479107
    _material = material(2.5894795, 224.65179)
    
    def __init__(self, S):
        self.energy = S*self._surface_energy
        _custom_volume.__init__(self, S*self._thickness)
        self._surface = surface(S, self._thickness, self._material)
        
    def _add_str(self):
        return '   chargeRate = %.3f\n' % self.energy
#end class

class battery(_custom_volume):
    #stock densities:
    #(0.005/0.033, 0.01/0.083, 0.02/0.1, 0.05/0.3, 0.2/1.6) = [0.15151515, 0.12048193, 0.2, 0.16666667, 0.125]
    #stock costs per volume:
    #(80/0.033, 360/0.083, 550/0.1, 880/0.3, 4500/1.6) = [2424.2424, 4337.3494, 5500, 2933.3333, 2812.5]
    #per mass:
    #(80/0.005, 360/0.01, 550/0.02, 880/0.05, 4500/0.2) = [16000, 36000, 27500, 17600, 22500]
    #per energy:
    #(80/100, 360/200, 550/400, 880/1000, 4500/4000) = [0.8, 1.8, 1.375, 0.88, 1.125]
    #stock energy density:
    #(100/0.033, 200/0.083, 400/0.1, 1000/0.3, 4000/1.6) = [3030.303, 2409.6386, 4000, 3333.3333, 2500]
    #batteries of RoverLander: 0.444m^3, 0.5t, 1000El.u
    _name = 'batteries'
    _density        = 0.2    #t/m^3
    _energy_cost    = 1.375  #Cr/El.u
    _energy_density = 4000.0 #El.u/m^3
    
    def __init__(self, **kwargs):
        V = kwargs.get('V', -1.0)
        E = kwargs.get('E', -1.0)
        if V < 0 and E < 0: 
            raise ValueError("%s: either volume or energy should be provided" % self._name)
        #compute energy, energy density and volume
        if   V < 0: V = E/self._energy_density
        elif E < 0: E = V*self._energy_density
        else: 
            self._energy_density = E/float(V)
            k = self._energy_density/battery._energy_density
            self._density *= k
            self._energy_cost *= k
        self._cost_density = self._energy_cost*E/V
        self.energy = E
        #initialize volume
        _custom_volume.__init__(self, V)
    #end def
    
    def _add_str(self):
        return '   energy amount = %.1f\n' % self.energy 
#end class


class generator(battery):
    #PB-NUK: V=0.01227, M=0.08, d=0.08/0.01227=6.5199674
    _name = 'generator'
    _density         = 6.5199674  #t/m^3
    _energy_cost     = 4400       #Cr/(El.u/s)
    _energy_density  = 61.124694  #(El.u/s)/m^3

    def _add_str(self):
        return '   energy rate = %.3f\n' % self.energy 
#end class