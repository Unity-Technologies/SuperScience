# Science!  Super Science!

At Unity Labs we perform a great deal of experiments.  These frequently produce 'gems' or small algorithms that are useful to the community by themselves.  This is a repository of those gems.

## Experimental Status
This repository is frequently evolving and expanding.  It is tested against the latest stable version of unity.  However, it is presented on an experimental basis - there is no formal support.

## How to Use ##
Each gem is located in a separate folder in this repository.  They are presented with an example scene (if appropriate) and well-commented code to educate the reader.  

This repository can be put in an empty Unity project or cloned into a child folder of an existing project.  

Use the example scripts directly, or tweak and alter the algorithms inside to fit your needs.  A list of included gems follows:

## Stabilizr : Object Stabilization for XR
"The Fishing Rod Problem" - Virtual objects or rays locked to a controller can shake in an unnatural way.  In the real world, long objects have weight, which gives them stabilization through inertia.  In XR, this lack of stabilization makes objects feel fake and precise selection difficult.

Stabilzr is a solution to this problem.  It smoothes the rotation of virtual objects in three scenarios
- Steady Motion: Holding an object at a precise angle while moving the controller
- Orbiting (endpoint) Motion: Holding the end of an object or ray at a particular spot while moving the controller
- Still Motion: Holding an object at a precise angle while clicking a button on the controller

Stabilzr works without adding lag to large sweeping motions - precise control is enabled while **in the worst case** only diverging from ground truth by 2 degrees for a single frame.

For an example of Stabilzr in action, check out the included 'TestScene'.  A 6 foot broom and 12 foot pointing stick are attached to the right and left XR controllers.  To compare before/after, two additional gameobjects (labeled Non-Stabilized Overlay) can be enabled.  These are non-stabilized copies of the broom and pointer that render of top of the originals.

## Orphaned Assets: Automated project housekeeping
The goal of the Orphaned Assets and Material Dependencies windows is to help you explore a large project and find assets which are no longer referenced by anything important. For example, if you have a bunch of scenes that aren't built anymore, delete them. Then you might see a bunch of prefabs and materials crop up. Delete those. Now you'll see some textures, more materials, maybe some shaders. Delete those, and now you've probably drastically reduced the import time of your project!
It's fun to delete assets!

There are likely types of references that we missed, so feel free to play around with the code, add cleverer way of excluding parts of your project like plugins, and contribute them back to us. Think of this code as a starting point for a project-specific reporting tool.
You are also meant to modify the code to narrow the search folder or exclusion folders. There are some hard-coded rules like excluding auto-generated materials within font assets, they may or may not be applicable to your project.
The Material References tool was useful on a project where the number of materials got out of hand, but similar windows could be created for prefabs, scenes, or any other kind of single-asset-type-centric view.
