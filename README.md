# Raymond's Mid-life Crisis.
This repository contains the higher-level code associated with my dissertation work. It probably will not be of use to you.
More general code is contained in the TfmrLibrary and GeomLibrary repos. TfmrLibrary has code somewhat specific to the
transient response of transformer windings, but most of it is fairly general, albeit messy as hell. It's geared toward
generating a turn-by-turn geometry/model of the windings, so that can be useful for other analysis types. The long
term goal is for this to be a more general library to support all sorts of transformer design calculations. GeomLib
contains some supported geometry and mesh primitives. It was mostly developed to support creating meshes to be meshed with
GMSH and analyzed using GetDP. I have since developed (with heavy AI assistance) an FEM package more tailored to my needs
called Noumena (a deep cut for the Kant fans). I can't afford an Ansys or COMSOL license, so I figured a little vibe-coding
was permissable, though it required a lot of steering, some hand-coding, imposing heavy opinion, and lot's of test cases.
The rest of this work is mostly hand coded (and it shows). The only exception was in some specific areas for reading or 
writing TUIs, some of the mesh file formats, or a few areas that were touched by my other vibe-coded work, an as-yet-to-be-named
insulation design review package for calculating transformer insulation margins (mostly UI stuff and I really do NOT mind
turning the UI stuff over to the AIs). I have tried to clearly indicate code that was AI generated or modified.
