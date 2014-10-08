from base_classes import volume, surface, material

class custom_volume(volume):
    _name = 'custom volume'
    _density = 1
    _cost_density = 1
    
    def __init__(self, V):
        volume.__init__(self, V, self._name, self._cost_density, self._density)
        
    def _add_str(self): return ''
    
    def __str__(self):
        s  = volume.__str__(self)
        s += self._add_str()
        return s
#end class

class reaction_wheel(custom_volume):
    _name = 'reaction wheel'
    _density = 0.2/0.21
    _cost_density = 2100/0.21
    
    _spec_torque = 156.25 #torque/t
    _spec_energy = 3.395  #El.u/sec/t
    
    def _add_str(self): 
        m = self.full_mass()
        s  = '   torque = %.0f\n' % (self._spec_torque * m) 
        s += '   rate = %.3f\n' % (self._spec_energy * m)
        return s
#end class

class solar_panel(custom_volume):
    _name = 'solar panels'
    _density = 0
    _cost_density = 0
    
    _thickness = 0.01
    _surface_energy = 1.3479107
    _material = material(2.5894795, 224.65179)
    
    def __init__(self, S):
        self.energy = S*self._surface_energy
        custom_volume.__init__(self, S*self._thickness)
        self._surface = surface(S, self._thickness, self._material)
        
    def _add_str(self):
        return '   chargeRate = %.3f\n' % self.energy
#end class

class battery(custom_volume):
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
    
    def __init__(self, V, energy=-1):
        if V < 0 and energy < 0: 
            raise ValueError("%s: either V or energy should be positive" % self._name)
        #compute energy, energy density and volume
        if        V < 0: V = energy/self._energy_density
        elif energy < 0: energy = V*self._energy_density
        else: 
            self._energy_density = energy/float(V)
            k = self._energy_density/battery._energy_density
            self._density *= k
            self._energy_cost *= k
        self._cost_density = self._energy_cost*energy/V
        self.energy = energy
        #initialize volume
        custom_volume.__init__(self, V)
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