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

## GizmoModule: Gizmos for EditorXR/Runtime
The normal Gizmos.Draw[Primitive] and Debug.DrawLine APIs don't work in EditorXR, and don't work in Runtime. The GizmoModule can be loaded alongside EditorXR, or included in a player build to provide similar functionality with pooled primitives.

The module can be accessed staticly using a singleton reference, or referenced like a normal MonoBehaviour to draw rays/lines, spheres, and cubes. It will only instantiate new objects when the number of each primitive type drawn per frame increases. For example, if you have been drawing three cubes, you only create a new cube when you start drawing a fourth cube. If you stop drawing cubes and then draw 3 cubes again, you will re-use those first three. Like the normal Gizmos/Debug API, you must call Draw[Primitive] every frame that you want to see it. The objects are automatically de-activated every frame when draw is not called.

Check out the example scene, which draws spheres on tracked controllers, and a line between them. If you don't have an HMD, don't worry. Just run the scene to see it work.

Here are some advanced examples from EditorXR:

![The blue rays are used to detect objects for snapping](https://github.com/Unity-Technologies/SuperScience/raw/docs-assets/GizmoModule/example-1.png)
![The red ray is showing how close we are to breaking the snapping distance](https://github.com/Unity-Technologies/SuperScience/raw/docs-assets/GizmoModule/example-2.png)
![This example shows that the third ray has encountered an object and shows where the non-snapped object would be](https://github.com/Unity-Technologies/SuperScience/raw/docs-assets/GizmoModule/example-3.png)
