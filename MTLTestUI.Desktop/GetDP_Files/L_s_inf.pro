
Flag_Axi = 1;
Flag_FrequencyDomain = 1;
//Freq = 60;

Function {
  mu0 = 4.e-7 * Pi;
  nu [ Region[{Air, Turns, Surface_Inf}] ] = 1. / mu0;
  mu [ Region[{Air, Turns, Surface_Inf}] ] = mu0;
  Sc[Region[{Turns}]] = SurfaceArea[] ; // area of coil cross section
  sigma[Region[{Turns}]] = 5.80e7; //Conductivity of annealed copper
  CoefGeos[Region[{Air, Turns, Surface_Inf}]] = 2*Pi; // planar model, 1 meter thick
}

Constraint{
  { Name MagneticVectorPotential_2D;
    Case {
      { Region Axis; Value 0; }
      //{ Region Surface_Inf; Value 0; }
    }
  }

  { Name Current_2D;
    Case {
        // Amplitude of the phasor is set to "Current"
        { Region TurnPos; Value 1.0; }
        { Region TurnZero; Value 0.0; }
        { Region TurnNeg; Value -1.0; }
    }
  }
  //{ Name Voltage_2D;
  //  Case {
  //    { Region TurnZero; Value 0; }
  //  }
  //}

}

Include "./Lib_Magnetodynamics2D_Simple.pro";

PostOperation {
  { Name dyn; NameOfPostProcessing Magnetodynamics2D_av;
    Operation {
      //Print[ az, OnElementsOf Vol_Mag, File "az.pos" ];
      //Print[ b, OnElementsOf Vol_Mag, File "b.pos" ];
      //Print[ j, OnElementsOf Vol_Mag, File "j.pos" ];
      //Print[ norm_of_h, OnElementsOf Vol_Mag, File "normh.pos" ];
      //Print[ Flux, OnElementsOf Vol_C_Mag, File "flux.pos"];
      //Print[ U, OnElementsOf Vol_C_Mag, File "U.pos"];
      //Print[ I, OnElementsOf Vol_C_Mag, File "I.pos"];
      Print[ L_fromMagEnergy [Vol_Mag], OnGlobal, File "out1.txt",
	    Format Table, SendToServer "Output/Global/Inductance_FromEnergy [H]" ];
      Print[ L, OnRegion Vol_C_Mag, File "out.txt",
	    Format Table, SendToServer "Output/Global/Inductance [H]" ];
      //Print[ Inductance_from_Flux [Vol_C_Mag], OnRegion Vol_C_Mag, File "out2.txt",
	    //Format Table, SendToServer "Output/Global/Inductance2 [H]" ];
    }
  }
}

