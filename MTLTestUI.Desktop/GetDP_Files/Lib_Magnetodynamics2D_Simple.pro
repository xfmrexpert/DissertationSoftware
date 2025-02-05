// Lib_Magnetodynamics2D_av_Cir.pro
//
// Template library for 2D magnetostatic and magnetodynamic problems in terms
// of the magnetic vector potential a (potentially coupled with the electric
// scalar potential v), with optional circuit coupling.

// Default definitions of constants, groups and functions that can/should be
// redefined from outside the template:

DefineConstant[
  modelPath = "", // default path of the model
  resPath = StrCat[modelPath, "res/"], // path for post-operation files
  CoefPower = 0.5, // coefficient for power calculations
  Freq = 60, // frequency (for harmonic simulations)
  FE_Order = 1 // finite element order,
  Val_Rint = 0, // interior radius of annulus shell transformation region (Vol_Inf_Mag)
  Val_Rext = 0, // exterior radius of annulus shell  transformation region (Vol_Inf_Mag)
  Val_Cx = 0, // x-coordinate of center of Vol_Inf_Mag
  Val_Cy = 0, // y-coordinate of center of Vol_Inf_Mag
  Val_Cz = 0 // z-coordinate of center of Vol_Inf_Mag
];

Group {
  DefineGroup[
    // The full magnetic domain:
    Vol_Mag,

    // Subsets of Vol_Mag:
    Vol_C_Mag, // massive conductors
    Vol_Inf_Mag //outer infinite boundary domain
  ];
}

Function {
  DefineFunction[
    nu, // reluctivity (in Vol_Mag)
    sigma, // conductivity (in Vol_C_Mag and Vol_S_Mag)
    CoefGeos // geometrical coefficient for 2D or 2D axi model (in Vol_Mag)
  ];
}

// End of definitions.

Group{
  // all volumes + surfaces on which integrals will be computed
  Dom_Mag = Region[ {Vol_Mag} ];
  DomainDummy = Region[ 12345 ] ; // Dummy region number for postpro with functions
}

Jacobian {
  { Name Vol;
    Case {
      { Region Vol_Inf_Mag; Jacobian VolAxiSquSphShell{Val_Rint, Val_Rext, Val_Cx, Val_Cy, Val_Cz}; }
      { Region All; Jacobian VolAxiSqu; }
    }
  }
  { Name Sur;
    Case {
      { Region All; Jacobian SurAxi; }
    }
  }
}

Integration {
  { Name Gauss_v;
    Case {
      { Type Gauss;
        Case {
          { GeoElement Point; NumberOfPoints  1; }
          { GeoElement Line; NumberOfPoints  5; }
          { GeoElement Triangle; NumberOfPoints  7; }
          { GeoElement Quadrangle; NumberOfPoints  4; }
          { GeoElement Tetrahedron; NumberOfPoints 15; }
          { GeoElement Hexahedron; NumberOfPoints 14; }
          { GeoElement Prism; NumberOfPoints 21; }
        }
      }
    }
  }
}

// Same FunctionSpace for both static and dynamic formulations
FunctionSpace {
  { Name Hcurl_a_2D; Type Form1P; // 1-form (circulations) on edges
                                  // perpendicular to the plane of study
    BasisFunction {
      // \vec{a}(x) = \sum_{n \in N(Domain)} a_n \vec{s}_n(x)
      //   without nodes on perfect conductors (where a is constant)
      { Name s_n; NameOfCoef a_n; Function BF_PerpendicularEdge;
        Support Dom_Mag; Entity NodesOf[All]; }

      // additional basis functions for 2nd order interpolation
      If(FE_Order == 2)
        { Name s_e; NameOfCoef a_e; Function BF_PerpendicularEdge_2E;
          Support Vol_Mag; Entity EdgesOf[All]; }
      EndIf
    }
    Constraint {
      { NameOfCoef a_n;
        EntityType NodesOf; NameOfConstraint MagneticVectorPotential_2D; }

      If(FE_Order == 2)
        { NameOfCoef a_e;
          EntityType EdgesOf; NameOfConstraint MagneticVectorPotential_2D_0; }
      EndIf
    }
  }
}

FunctionSpace {
  // Gradient of Electric scalar potential (2D)
  { Name Hregion_u_2D; Type Form1P; // same as for \vec{a}
    BasisFunction {
      { Name sr; NameOfCoef ur; Function BF_RegionZ;
        // constant vector (over the region) with nonzero z-component only
        Support Region[{Vol_C_Mag}];
        Entity Region[{Vol_C_Mag}]; }
    }
    GlobalQuantity {
      { Name U; Type AliasOf; NameOfCoef ur; }
      { Name I; Type AssociatedWith; NameOfCoef ur; }
    }
    Constraint {
      { NameOfCoef I;
        EntityType Region; NameOfConstraint Current_2D; }
    }
  }
}

// Dynamic Formulation (eddy currents)
Formulation {
  { Name Magnetodynamics2D_av; Type FemEquation;
    Quantity {
      { Name a; Type Local; NameOfSpace Hcurl_a_2D; }

      { Name ur; Type Local; NameOfSpace Hregion_u_2D; }
      { Name I; Type Global; NameOfSpace Hregion_u_2D [I]; }
      { Name U; Type Global; NameOfSpace Hregion_u_2D [U]; }
    }
    Equation {
      Integral { [ nu[] * Dof{d a} , {d a} ];
        In Vol_Mag; Jacobian Vol; Integration Gauss_v; }

      // Electric field e = -Dt[{a}]-{ur},
      // with {ur} = Grad v constant in each region of Vol_C_Mag
      Integral { DtDof [ sigma[] * Dof{a} , {a} ];
        In Vol_C_Mag; Jacobian Vol; Integration Gauss_v; }
      Integral { [ sigma[] * Dof{ur} / (2*Pi) , {a} ];
        In Vol_C_Mag; Jacobian Vol; Integration Gauss_v; }

      // When {ur} act as a test function, one obtains the circuits relations,
      // relating the voltage and the current of each region in Vol_C_Mag
      Integral { DtDof [ sigma[] * Dof{a} , {ur} ];
        In Vol_C_Mag; Jacobian Vol; Integration Gauss_v; }
      Integral { [ sigma[] * Dof{ur} / (2*Pi) , {ur} ];
        In Vol_C_Mag; Jacobian Vol; Integration Gauss_v; }
      GlobalTerm { [ Dof{I} , {U} ]; In Vol_C_Mag; }

      // Attention: CoefGeo[.] = 2*Pi for Axi

    }
  }
}

Resolution {
  { Name Magnetodynamics2D_av;
    System {
      { Name A; NameOfFormulation Magnetodynamics2D_av;
        Type ComplexValue; Frequency Freq;
      }
    }
    Operation {
        CreateDirectory[resPath];
        InitSolution[A];
        Generate[A]; 
        //For j In {0:10}
        j=1;
          SetFrequency[A, j * 60];
          Solve[A]; 
          SaveSolution[A];
        //EndFor
    }
  }
}

// Same PostProcessing for both static and dynamic formulations (both refer to
// the same FunctionSpace from which the solution is obtained)
PostProcessing {
  { Name Magnetodynamics2D_av; NameOfFormulation Magnetodynamics2D_av;
    PostQuantity {
      // In 2D, a is a vector with only a z-component: (0,0,az)
      { Name a; Value {
          Term { [ {a} ]; In Vol_Mag; Jacobian Vol; }
        }
      }
      // The equilines of az are field lines (giving the magnetic field direction)
      { Name az; Value {
          Term { [ CompZ[{a}] ]; In Vol_Mag; Jacobian Vol; }
        }
      }
      { Name b; Value {
          Term { [ {d a} ]; In Vol_Mag; Jacobian Vol; }
        }
      }
      { Name h; Value {
          Term { [ nu[] * {d a} ]; In Vol_Mag; Jacobian Vol; }
        }
      }
      { Name j; Value {
          Term { [ CompZ[-sigma[] * (Dt[{a}]+{ur}/(2*Pi)) ]]; In Vol_C_Mag; Jacobian Vol; }
          Term { [ 0 ]; In Vol_Mag; Jacobian Vol; }
          // Current density in A/m
        }
      }
      { Name JouleLosses; Value {
          Integral { [ CoefPower * sigma[]*SquNorm[Dt[{a}]+{ur}/(2*Pi)] ];
            In Vol_C_Mag; Jacobian Vol; Integration Gauss_v; }
	      }
      }
      { Name Flux ; Value {
          Integral { [ CompZ[{a}] ] ;
            In Vol_C_Mag; Jacobian Vol ; Integration Gauss_v ; }
        }
      }
      { Name L_fromMagEnergy; Value { Integral{ [ nu[] * Norm[{d a}]*Norm[{d a}]] ; In Vol_Mag; Jacobian Vol; Integration Gauss_v;} } }
      { Name L; 
        Value { 
          Term { [ -Im [ {U}/(2*Pi*Freq)/(2*Pi)]]; In Vol_C_Mag; }
        } }
      { Name U; Value {
          Term { [ Cart2Pol [ {U} ] ]; In Vol_C_Mag; }
        }
      }
      { Name I; Value {
          Term { [ {I} ]; In Vol_C_Mag; }
        }
      }
      { Name Inductance_from_Flux ; Value { Term { Type Global; [ $Flux ]; In DomainDummy ; } } }
    }
  }
}
