import numpy as np
import collections

def hr(text='', ch='-', width=80):
    tl = len(text)
    if width-tl-2 < 0: return text
    ll = (width-tl-2)/2
    rl = width-tl-2-ll
    return '%s %s %s\n' % (ch*ll, text, ch*rl)
#end def

class material:
    def __init__(self, density, cost):
        self.density = density
        self.cost    = cost
#end class


class surface:
    unit_h = 0.005
    
    def __init__(self, S, h, m):
        self._S   = S
        self.h    = h
        self.m    = m

    def set_pcs(self, n): self._S *= n
    
    def S(self, scale=1, length=1):
        return self._S*scale**2*length  
    
    def mass(self, scale=1, length=1):
        return self.S(scale, length)*self.h*self.m.density;
    
    def cost(self, scale=1, length=1):
        return self.S(scale, length)*self.h/self.unit_h*self.m.cost;
    
    def __str__(self):
        return '[%sm^2 x %sm], %st/m^3, %st, %sCr' % (self.S(), 
                                                   self.h, self.m.density, 
                                                   self.mass(), self.cost())
#end class


class volume:
    def __init__(self, vol, name, **kwargs):
        #main parameters
        self._V    = float(vol)
        self.name  = name
        #surface and subvolumes
        self._surface = kwargs.get('S', None)
        self._subvolumes = kwargs.get('V', [])
        #counterparts of this volume
        n = kwargs.get('N', 1.0)
        self.set_pcs(n)
        #cost, mass and density
        mat = kwargs.get('material', None)
        if isinstance(mat, material):
            self.d = mat.density
            self._cost = mat.cost
        else:
            self._cost = kwargs.get('C', 0.0)
            mass = float(kwargs.get('M', -1.0))
            d = float(kwargs.get('D', -1.0))
            #recalculate mass and density
            if d < 0 and mass < 0: d = 0.0
            elif d < 0: 
                d = mass*n/self.V()
                self._cost = self._cost*n/self.V() 
            elif mass > 0: print('volume: density was given; mass is ignored')
            self.d = d
    #end def

    def set_pcs(self, n):
        self._V *= n
        if self._surface is not None: self._surface.set_pcs(n)
        map(lambda sv: sv.set_pcs(n), self._subvolumes)
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
            if self.V_mass() > 0 or self.V_cost() > 0: 
                s += '   content: %sm^3, %st/m^3, %st, %sCr\n' % (self.V(), self.d, 
                                                                  self.V_mass(), self.V_cost())
            if len(self._subvolumes) > 0:
                s += '   '+''.join(str(sv).replace('\n', '\n   ') for sv in self._subvolumes)
                s = s[:-3]
        return s
#end class


class part(collections.Iterable):
    _asymptote_slope     = 1.5
    _asymptote_intercept = 1e5
    _exponent_base       = 1.25
    
    def __init__(self, name, volumes, add_mass = 0, add_cost = 0, res_cost = 0, size = 1):
        self.name       = name
        self._volumes   = volumes
        self._add_mass  = add_mass
        self._add_cost  = add_cost
        self._res_cost  = res_cost
        self._size      = size
        self._spec_mass = np.array([self.V_mass(), self.S_mass(), 0, self._add_mass])
        self._init_mass = sum(self._spec_mass)
        self._weights   = self._spec_mass/self._init_mass
        self._spec_cost = np.array([sum(v.full_V_cost() for v in self._volumes), 
                                    sum(v.full_S_cost() for v in self._volumes), 
                                    0, self._add_cost])
        self._cost      = sum(self._spec_cost)
        self._cost_weights = self._spec_cost/self._cost
        self._cost     += res_cost
        print(self)
        print
    #end def
    
    def __iter__(self): return iter(self._volumes)
    
    @property
    def volumes(self): 
        return [volume(self.volume(), self.name, V=self._volumes)]
    #end def
    
    def mass(self, scale=1, length=1):
        scale = scale/self._size
        w = self._spec_mass
        m = ((w[0]*scale + w[1])*scale + w[2])*scale*length
        if len(w) > 3: m += w[3]
        return m
    #end def
    
    def cost(self, scale=1, length=1):
        scale = scale/self._size
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
        s += '//Resources  cost: %.3f Cr\n' % self._res_cost
        s += 'entryCost = %d\n' % np.ceil(self.entry_cost(self._cost-self._res_cost))
        s += 'cost = %d\n' % np.ceil(self._cost)
        s += 'mass = %.6f\n' % self._init_mass
        s += 'specificMass = %s //weights: [ %s ]\n' % (', '.join(str(m) for m in self._spec_mass), 
                                                        ', '.join(str(w) for w in self._weights))
        s += 'specificCost = %s //weights: [ %s ]\n' % (', '.join(str(m) for m in self._spec_cost), 
                                                        ', '.join(str(w) for w in self._cost_weights))
        return s
    #end def
    
    def print_masses(self, _from=0.5, _to=4.0, step=0.5):
        for s in np.arange(_from, _to+step/2.0, step):
            print s, self.mass(s)
        print ''
#end class