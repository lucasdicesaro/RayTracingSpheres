# Ray Tracing Spheres

A ray tracing renderer built in Unity as a full-screen fragment shader, based on Sebastian Lague's ray tracing video tutorials. Renders spheres with diffuse shading and multiple bounce lighting.

## What it does

- Real-time ray tracing in a fragment shader, applied as a camera image effect (`OnRenderImage` + `Graphics.Blit`)
- Supports multiple spheres with configurable material properties
- Progressive rendering — accumulates samples over frames for anti-aliasing
- Multiple shader variants: first hit, diffuse, and sphere-specific passes

## Requirements

- Unity 2022.3 or later
- GPU with Shader Model 5 support (DirectX 11 / Metal)

## Rendering pipeline note

This project uses Unity's **Built-in renderer** — URP is intentionally disabled in `Project Settings → Graphics`. The renderer runs in `OnRenderImage`, the camera image-effect hook, which URP never calls. This is already committed in `ProjectSettings/`, so no manual change is needed after cloning.

To disable URP by hand (for example if it gets re-enabled):

1. **Edit → Project Settings → Graphics** → set **Default Render Pipeline** to **None**
2. **Edit → Project Settings → Quality** → for each quality level, set **Render Pipeline Asset** to **None**
3. Press Play; the Game view should show the ray traced image instead of the plain scene

## How to open

1. Clone the repo
2. Open **Unity Hub** → **Add project from disk** → select the repo root folder
3. In the Project panel, double-click `Assets/Scenes/SampleScene.unity` ← required on first open; Unity remembers it after that
4. Press Play

No manual setup needed — scene, parameters, and project settings are all committed.

## References

- [Sebastian Lague — Coding Adventure: Ray Tracing](https://www.youtube.com/watch?v=Qz0KTGYJjUs)
