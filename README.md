# Science!  Super Science!

At Unity Labs we perform a great deal of experiments.  These frequently produce 'gems' or small algorithms that are useful to the community by themselves.  This is a repository of those gems.

## Experimental Status
This repository is frequently evolving and expanding.  It is tested against the latest stable version of unity.  However, it is presented on an experimental basis - there is no formal support.

## How to Use ##
Each gem is located in a separate folder in this repository.  They are presented with an example scene (if appropriate) and well-commented code to educate the reader.  

This repository can be put in an empty Unity project or cloned into a child folder of an existing project.  Most gems can be used in earlier Unity versions but the sample scenes require 2019.1 or greater.

Use the example scripts directly, or tweak and alter the algorithms inside to fit your needs.  A list of included gems follows:

## Stabilizr : Object Stabilization for XR
"The Fishing Rod Problem" - Virtual objects or rays locked to a controller can shake in an unnatural way.  In the real world, long objects have weight, which gives them stabilization through inertia.  In XR, this lack of stabilization makes objects feel fake and precise selection difficult.

Stabilizr is a solution to this problem.  It smooths the rotation of virtual objects in three scenarios
- Steady Motion: Holding an object at a precise angle while moving the controller
- Orbiting (endpoint) Motion: Holding the end of an object or ray at a particular spot while moving the controller
- Still Motion: Holding an object at a precise angle while clicking a button on the controller

Stabilizr works without adding lag to large sweeping motions - precise control is enabled while **in the worst case** only diverging from ground truth by 2 degrees for a single frame.

For an example of Stabilizr in action, check out the included 'TestScene'.  A 6 foot broom and 12 foot pointing stick are attached to the right and left XR controllers.  To compare before/after, two additional gameobjects (labelled Non-Stabilized Overlay) can be enabled.  These are non-stabilized copies of the broom and pointer that render of top of the originals.

## GizmoModule: Gizmos for EditorXR/Runtime
The normal Gizmos.Draw[Primitive] and Debug.DrawLine APIs don't work in EditorXR, and don't work in Runtime. The GizmoModule can be loaded alongside EditorXR, or included in a player build to provide similar functionality through the Graphics.DrawMesh API.

The module can be accessed statically using a singleton reference, or referenced like a normal MonoBehaviour to draw rays/lines, spheres, and cubes.  Like the normal Gizmos/Debug API, you must call Draw[Primitive] every frame that you want to see it.

Check out the example scene, which draws spheres on tracked controllers, and a line between them. If you don't have an HMD, don't worry. Just run the scene to see it work.

Here are some advanced examples from EditorXR:

![The blue rays are used to detect objects for snapping](https://github.com/Unity-Technologies/SuperScience/raw/docs-assets/GizmoModule/example-1.png)
![The red ray is showing how close we are to breaking the snapping distance](https://github.com/Unity-Technologies/SuperScience/raw/docs-assets/GizmoModule/example-2.png)
![This example shows that the third ray has encountered an object and shows where the non-snapped object would be](https://github.com/Unity-Technologies/SuperScience/raw/docs-assets/GizmoModule/example-3.png)

## PhysicsTracker: Bridging the gap between game code and physics simulation
One of the more difficult problems in games is translating motion from non-physics sources, like animations, custom scripts, and XR input, into the physics simulation.  An additional concern on the input side is that while some XR Input devices provide physics data, others do not.  Tracked objects in AR usually do not have the velocity or other physics data associated with them.

The conventional approach to this problem is to integrate velocity by looking at how an object has moved frame to frame.  This data can vary pretty wildly (especially at low speeds) and 'feel' incorrect for things like rotational motion, changing directions, shaking, and so on.  Presenting something that looks and feels like motion the player intended usually requires a lot of trial and error, tweaks, and hacks.

The PhysicsTracker provides a solution.  It works by separating the problem of tracking velocity into tracking speed and direction separately.  It smooths and predicts these values and how they change, and then recombines them into a final velocity, acceleration, and angular velocity.

The PhysicsTracker is versatile;  it is a class that can be used inside and outside monobehaviours.  They can follow any kind of changing set of positions and rotations and output appropriate physics data.  Do you need to get the velocity at the end of a bat?  Stick a PhysicsTracker there.  Rotation of an XR Input device?  Stick a PhysicsTracker there.  Want to get some physics values for that 'monster attack' animation in your game?  Stick a PhysicsTracker there.

The included 'TestScene' for PhysicsTracker shows the smooth physics data being generated for the left and right hands.  An attached component 'DrawPhysicsData' on each hand does the tracking and drawing of data.  Various pieces of data can be visualized - velocity, acceleration, angular velocity, and direct integrated velocity (for reference).  I recommend only having one or two options at a time - the data can get too busy with them all active at once.  Velocity is drawn in blue, Acceleration in green, Angular Velocity in white, and Direct Integration in Red.

To use the PhysicsTracker in your own scripts, just create a new 'PhysicsTracker' in your script, and call the 'Update' method with the most recent position and rotation values of the object you wish to track.  The physics tracker then will calculate up to date values for Speed, Velocity, Acceleration, Direction, Angular Speed, and Angular Velocity.

For smoothest visual results, use a fixed or smooth delta time with the PhysicsTracker update functions.  For single-frame use (gameplay-to-physics events), delta time is fine.

## RunInEditHelper: Manage what is running in edit mode
The runInEditMode flag is a relatively new feature of MonoBehaviour which allows scripts to decide whether or not a given MonoBehaviour gets lifecycle callbacks (like Start and Update) in edit mode. This handy Editor Window allows users to modify the RunInEdit state of selected objects, and lists which objects are currently running in edit mode. The window is helpful when working on or debugging RunInEdit-based workflows to provide manual control and oversight while creating the systems which modify this flag. It is sometimes unclear whether an object had the flag set or unset, because it is not exposed in the inspector (regular or Debug).

The reason why we manually set enabled to false when stopping behaviors is to ensure that they get their OnDisable call and can clean up any state modified in OnEnable. There is no other reason why this is necessary to disable runInEditMode, and in fact if the desired behavior is just to "pause" the behavior, and not trigger OnDisable/OnEnable, another button could be added to simply toggle the state or set it to false.

If you want to continuously update your running behaviors while in edit mode (as if in Play mode), click Start/Stop Player Loop. You can try this out in the sample scene. If you start the Cube/Rotator, you will notice that it only updates every other frame, and only while selected. If you click Run Player Loop, you should see the cube smoothly update, regardless of selection.

## HiddenHierarchy: Find hidden scene objects
Sometimes it is necessary for Unity systems to add hidden objects to the user's scene. Either the object should not be selected and modified, should not be included in Player builds, or needs to be hidden for other reasons.

The HiddenHierarchy window shows a Hierarchy-like view of the currently open scenes, preview scenes, and "free objects" which exist outside of scenes. This is useful for debugging systems involving hidden objects, in case new objects "leak" into the scene or the system somehow fails to destroy a hidden object.

## HiddenInspector: Edit hidden components and properties
Likewise with hidden GameObjects, some Components may be hidden. The HiddenInspector window displays the currently selected GameObject and its full list of components, including those which are hidden from the normal inspector. Each component (as well as the GameObject's properties) will contain a raw list of properties, similar to the Debug Inspector.

It is possible to show even more properties by enabling Show Hidden Properties. This will show non-visible properties as well as the hideFlags field which can be used to make component or objects visible to the normal hierarchy and inspector, and enable editing on read-only objects. Naturally, this can have detrimental consequences and may have adverse effects on Unity systems. Similarly, destroying hidden objects or components with this view can case errors or adverse effects.

## ModificationResponse
This is an example of how to hook into Undo.postprocessModifications and Undo.undoRedoPerformed to respond to property modifications in a Scene.  It uses a short timer that is reset and started when a change is detected, and it only triggers the response when the timer finishes.  This pattern is useful when you have a complex response that you don't want to happen constantly as a continuous property is changed (for example, as a user drags a slider in the Inspector).

## SceneMetadata
One way to store metadata for a Scene is by keeping it in a ScriptableObject Asset, in which case you need to make sure the Asset is kept in sync with the Scene. This example shows how to use the OnWillSaveAssets callback in AssetModificationProcessor to ensure that a metadata Asset gets saved with the Scene.

## EditorDelegates
It is sometimes necessary to reference Editor code in your runtime assembly.  For example, a MonoBehaviour may exist only for the purpose of edit-time functionality, but it must live in a runtime assembly due to the rule against MonoBehaviours in Editor assemblies.  In this case, it is often useful to define some static delegate fields inside of an '#if UNITY_EDITOR' directive.  An Editor class can assign its own methods to those delegates, providing access to itself in the runtime assembly.

EditorDelegatesExampleWindow provides functionality to EditorDelegates for checking if the mouse is over the window and firing callbacks when the window is focused and unfocused. The MonoBehaviour EditorDelegatesUser is then able to use this functionality even though it is in the runtime assembly.

## MissingReferences: Track down references to missing assets or methods
The goal of the MissingReferences windows is to identify assets in your project or objects in loaded scenes that may be missing their dependencies. It can identify the following problematic situations:
- A script on a scene object prefab is missing
- An object field on an asset or scene object is missing its reference
- A prefab instance in a loaded scene is missing its prefab asset
- Serialized UnityEvent properties are missing their target object or method, or references a method which doesn't exist

Note that the Missing Project References window will load all of the assets in your project, synchronously, when you hit Refresh. In large projects, this can crash Unity, so use this window at your own risk! If you want to use this with large projects, replace the call to `AssetDatabase.GetAllAssetPaths()` with a call to `AssetDatabase.FindAssets()` and some narrower search, or refactor the script to work on the current selection.

## Solid Color Textures: Optimize texture memory by shrinking solid color textures
Sometimes materials which are generated by digital content creation software contain textures which are just a solid color. Often these are generated for normal or smoothness channels, where details can be subtle, so it is difficult to be sure whether or not the texture is indeed a solid color. Sometimes the detail is "hidden" in the alpha channel or too subtle to see in the texture preview inspector.

Thankfully, this is what computers are for! This utility will scan all of the textures in your project and report back on textures where every single pixel is the same color. It even gives you a handy summary in the left column of _what color_ these textures are, and groups them by color. The panel to the right shows a collapsible tree view of each texture, grouped by location, as well as a button for each texture or location to shrink the texture(s) down to the smallest possible size (32x32). This is the quick-and-dirty way to optimize the memory and cache efficiency of these textures, without risking any missing references. Of course, the most optimal way to handle these textures is with a custom shader that uses a color value instead of a texture. Short of that, you should try to cut back to just a _single_ solid color texture per-color. The summary in the left panel should only show one texture for each unique color.

The scan process can take a long time, especially for large projects. Also, since most textures in your project will not have the `isReadable` flag set, we check a 128x128 preview (generated by `AssetPreview.GetAssetPreview`) of the texture instead. This turns out to be the best way to get access to an unreadable texture, and proves to be a handy performance optimization as well. It is _possible_ that there are textures with very subtle detail which _perfectly_ filters out to a solid color texture at this scale, but this corner case is pretty unlikely. Still, you should look out for this in case shrinking these textures ends up making a noticeable effect.

You may be wondering, "why is it so bad to have solid color textures?"
- Textures occupy space in video memory, which can be in short supply on some platforms, especially mobile.
- Even though the asset in the project may be small (solid color PNGs are small regardless of dimensions), the texture that is included in your final build can be much larger. GPU texture compression doesn't work the same way as PNG or JPEG compression, and it is the GPU-compatible texture data which is included in Player builds. This means that your 4096x4096 solid-black PNG texture may occupy only 5KB in the Assets folder, but will be a whopping 5.3MB (>1000x larger!) in the build.
- Sampling from a texture in a shader takes significantly more time than reading a color value from a shader property.
- Looking up colors in a _large_ texture can lead to cache misses. Even with mipmaps enabled, the renderer isn't smart enough to know that it can use the smallest mip level for these textures. If you have a solid color texture at 4096x4096 that occupies the whole screen, the GPU is going to spend a lot of wasted time sampling pixels that all return the same value.

## `Color32` To Int: Convert colors to and from a single integer value as fast as possible
This one simple trick will save your CPU millions of cycles! Read on to learn more.

The `Color32` struct in Unity is designed to be easily converted to and from an `int`. It does this by storing each color value in a `byte`. You can concatenate 4 `bytes` to form an `int`, and then you can do operations like add, subtract, and compare on these values _one time_ instead of repeating the same operation _four times_. Thus, for an application like the Solid Color Textures window, this reduces the time to process each pixel by a factor of 4. The conversion is _basically free_. The only CPU work needed is to set a field on a struct.

This works by taking advantage of the `[FieldOffset]` attribute in C# which can be applied to value type fields. This allows us to manually specify how many bytes from the beginning of the struct a field should start. Note that your struct also needs the `[StructLayout(LayoutKind.Explicit)]` attribute in order to use `[FieldOffset]`.

In this case, we define a struct (`Color32ToInt`) with both an `int` field and a `Color32` field to both have a field offset of `0`. This means that they both occupy the same space in memory, and because they are both of equal size (4 bytes) they will fully overwrite each other when either one is set. If we set a value of `32` into the `int` field, we will read a color with `32` in the `alpha` channel, and `0` in all other channels from the `Color32` field. If we set a value of `new Color32(0, 0, 32, 0)` to the `Color32` field, we will read a `8,192` (`0x00002000`) from the `int` field. Pretty neat, huh? I bet you thought you could only pull off this kind of hack in C++. We don't even need unsafe code! In fact, if you look at the [source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Color32.cs) for `Color32`, you can see that we also take advantage of this trick internally, though we don't expose the int value.

Note that you can't perform any operation on the `int` version of a color and expect it to work the same as doing that operation on each individual channel. For example, multiplying two colors that were converted to `ints` will not have the same result as multiplying the values of each channel individually.

One final tip, left as an exercise for the reader: this trick also works on arrays (of equal length), and any other value types where you can align their fields with equivalent primitives. It works for floating point values as well, but you can't concatenate or decompose them them like integer types.