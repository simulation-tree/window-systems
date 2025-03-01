using Collections;
using Collections.Generic;
using Rendering;
using Rendering.Components;
using SDL3;
using Simulation;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Unmanaged;
using Windows.Components;
using Windows.Functions;
using Worlds;

namespace Windows.Systems
{
    [SkipLocalsInit]
    public readonly partial struct WindowSystem : ISystem
    {
        private readonly Library sdlLibrary;
        private readonly List<Window> windowEntities;
        private readonly List<uint> windowIds;
        private readonly List<SDLWindowState> lastWindowStates;
        private readonly Dictionary<uint, Entity> displayEntities;

        private WindowSystem(Library library, List<Window> windowEntities, List<uint> windowIds, List<SDLWindowState> lastWindowStates, Dictionary<uint, Entity> displayEntities)
        {
            this.sdlLibrary = library;
            this.windowEntities = windowEntities;
            this.windowIds = windowIds;
            this.lastWindowStates = lastWindowStates;
            this.displayEntities = displayEntities;
        }

        void ISystem.Start(in SystemContainer systemContainer, in World world)
        {
            if (systemContainer.World == world)
            {
                Library library = new();
                List<Window> windowEntities = new();
                List<uint> windowIds = new();
                List<SDLWindowState> lastWindowStates = new();
                Dictionary<uint, Entity> displayEntities = new();
                systemContainer.Write(new WindowSystem(library, windowEntities, windowIds, lastWindowStates, displayEntities));
            }
        }

        void ISystem.Update(in SystemContainer systemContainer, in World world, in TimeSpan delta)
        {
            ComponentType windowType = world.Schema.GetComponentType<IsWindow>();
            ComponentType destinationType = world.Schema.GetComponentType<IsDestination>();

            if (systemContainer.World == world)
            {
                DestroyWindowsOfDestroyedEntities();
            }

            UpdateWindowsToMatchEntities(world, windowType);
            UpdateDestinationSizes(world, windowType, destinationType);

            if (systemContainer.World == world)
            {
                UpdateEntitiesToMatchWindows();
            }
        }

        void ISystem.Finish(in SystemContainer systemContainer, in World world)
        {
            ComponentType windowType = world.Schema.GetComponentType<IsWindow>();
            CloseRemainingWindows(world, windowType);

            if (systemContainer.World == world)
            {
                displayEntities.Dispose();
                lastWindowStates.Dispose();
                windowIds.Dispose();
                windowEntities.Dispose();
                sdlLibrary.Dispose();
            }
        }

        private readonly void CloseRemainingWindows(World world, ComponentType windowType)
        {
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.ContainsComponent(windowType))
                {
                    USpan<uint> entities = chunk.Entities;
                    USpan<IsWindow> components = chunk.GetComponents<IsWindow>(windowType);
                    for (uint i = 0; i < components.Length; i++)
                    {
                        ref IsWindow component = ref components[i];
                        Window windowEntity = new Entity(world, entities[i]).As<Window>();
                        if (windowEntities.TryIndexOf(windowEntity, out uint index))
                        {
                            SDLWindow sdlWindow = sdlLibrary.GetWindow(windowIds[index]);
                            sdlWindow.Dispose();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Polls for changes to windows and updates their entities to match if any property
        /// is different from the presentation.
        /// </summary>
        private readonly void UpdateEntitiesToMatchWindows()
        {
            while (sdlLibrary.PollEvent(out SDL_Event sdlEvent))
            {
                if (sdlEvent.type == SDL_EventType.WindowMoved)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        WindowState state = window.State;
                        if (state == WindowState.Windowed)
                        {
                            int x = sdlEvent.window.data1;
                            int y = sdlEvent.window.data2;
                            ref SDLWindowState lastState = ref lastWindowStates[index];
                            if (lastState.x != x || lastState.y != y)
                            {
                                lastState.x = x;
                                lastState.y = y;
                                ref WindowTransform transform = ref window.TryGetComponent<WindowTransform>(out bool contains);
                                if (!contains)
                                {
                                    transform = ref window.AddComponent<WindowTransform>();
                                }

                                transform.position = new(x, y);
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowResized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        WindowState state = window.State;
                        if (state == WindowState.Windowed)
                        {
                            int width = sdlEvent.window.data1;
                            int height = sdlEvent.window.data2;
                            ref SDLWindowState lastState = ref lastWindowStates[index];
                            if (lastState.width != width || lastState.height != height)
                            {
                                lastState.width = width;
                                lastState.height = height;
                                ref WindowTransform transform = ref window.TryGetComponent<WindowTransform>(out bool contains);
                                if (!contains)
                                {
                                    transform = ref window.AddComponent<WindowTransform>();
                                }

                                transform.size = new(width, height);
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowEnterFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.GetComponent<IsWindow>();
                        component.windowState = WindowState.Fullscreen;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowLeaveFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.GetComponent<IsWindow>();
                        component.windowState = WindowState.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMaximized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.GetComponent<IsWindow>();
                        component.windowState = WindowState.Maximized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMinimized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.GetComponent<IsWindow>();
                        component.windowFlags |= WindowFlags.Minimized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowRestored)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.GetComponent<IsWindow>();
                        component.windowFlags &= ~WindowFlags.Minimized;
                        component.windowState = WindowState.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusGained)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref SDLWindowState lastState = ref lastWindowStates[index];
                        if (!lastState.flags.HasFlag(WindowFlags.Focused))
                        {
                            Window window = windowEntities[index];
                            ref IsWindow component = ref window.GetComponent<IsWindow>();
                            lastState.flags |= WindowFlags.Focused;
                            component.windowFlags |= WindowFlags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusLost)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref SDLWindowState lastState = ref lastWindowStates[index];
                        if (lastState.flags.HasFlag(WindowFlags.Focused))
                        {
                            Window window = windowEntities[index];
                            ref IsWindow component = ref window.GetComponent<IsWindow>();
                            lastState.flags &= ~WindowFlags.Focused;
                            component.windowFlags &= ~WindowFlags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowCloseRequested)
                {
                    HandleCloseRequest((uint)sdlEvent.window.windowID);
                }
            }
        }

        private readonly void HandleCloseRequest(uint windowId)
        {
            if (windowIds.TryIndexOf(windowId, out uint index))
            {
                Window window = windowEntities[index];
                WindowCloseCallback closeCallback = window.CloseCallback;
                if (closeCallback != default)
                {
                    closeCallback.Invoke(window);
                }
                else
                {
                    window.Dispose();
                }
            }
            else
            {
                throw new InvalidOperationException($"Window with ID `{windowId}` is not known to the window system");
            }
        }

        private readonly void UpdateWindowsToMatchEntities(World world, ComponentType windowType)
        {
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.ContainsComponent(windowType))
                {
                    USpan<uint> entities = chunk.Entities;
                    USpan<IsWindow> components = chunk.GetComponents<IsWindow>(windowType);
                    for (uint i = 0; i < entities.Length; i++)
                    {
                        ref IsWindow window = ref components[i];
                        Window windowEntity = new Entity(world, entities[i]).As<Window>();
                        if (!windowEntities.Contains(windowEntity))
                        {
                            SDLWindow newWindow = CreateWindow(windowEntity, ref window);
                            windowEntities.Add(windowEntity);
                            windowIds.Add(newWindow.ID);

                            (int x, int y) = newWindow.GetRealPosition();
                            (int width, int height) = newWindow.GetRealSize();
                            lastWindowStates.Add(new(x, y, width, height, window.windowState, window.windowFlags));
                            Trace.WriteLine($"Created window `{windowEntity}` with ID `{newWindow.ID}`");
                        }
                        else
                        {
                            //create the surface
                            if (!windowEntity.TryGetSurfaceInUse(out _) && windowEntity.TryGetRendererInstanceInUse(out Allocation instance))
                            {
                                FixedString label = windowEntity.RendererLabel;
                                SDLWindow existingWindow = GetWindow(windowEntity);
                                if (label.Equals("vulkan"))
                                {
                                    Allocation surface = existingWindow.CreateVulkanSurface(instance);
                                    windowEntity.AddComponent(new SurfaceInUse(surface));
                                    Trace.WriteLine($"Created surface `{surface}` for window `{windowEntity}` using renderer `{label}`");
                                }
                                else
                                {
                                    throw new NotImplementedException($"Unknown renderer label '{label}', not able to create a surface");
                                }
                            }
                        }

                        UpdateWindowToMatchEntity(windowEntity, ref window);
                    }
                }
            }
        }

        private readonly void UpdateDestinationSizes(World world, ComponentType windowType, ComponentType destinationType)
        {
            foreach (Chunk chunk in world.Chunks)
            {
                Definition key = chunk.Definition;
                if (key.ContainsComponent(windowType) && key.ContainsComponent(destinationType))
                {
                    USpan<uint> entities = chunk.Entities;
                    USpan<IsDestination> destinationComponents = chunk.GetComponents<IsDestination>(destinationType);
                    for (uint i = 0; i < entities.Length; i++)
                    {
                        ref IsDestination destination = ref destinationComponents[i];
                        Window windowEntity = new Entity(world, entities[i]).As<Window>();
                        SDLWindow sdlWindow = GetWindow(windowEntity);
                        (int width, int height) = sdlWindow.GetRealSize();
                        if (sdlWindow.IsMinimized)
                        {
                            destination.width = 0;
                            destination.height = 0;
                        }
                        else
                        {
                            destination.width = (uint)width;
                            destination.height = (uint)height;
                        }
                    }
                }
            }
        }

        private readonly void DestroyWindowsOfDestroyedEntities()
        {
            for (uint i = windowEntities.Count - 1; i != uint.MaxValue; i--)
            {
                Window windowEntity = windowEntities[i];
                if (windowEntity.IsDestroyed)
                {
                    SDLWindow sdlWindow = sdlLibrary.GetWindow(windowIds[i]);
                    sdlWindow.Dispose();

                    windowEntities.RemoveAt(i);
                    windowIds.RemoveAt(i);
                    lastWindowStates.RemoveAt(i);
                    Trace.WriteLine($"Destroyed window `{windowEntity}`");
                }
            }
        }

        private readonly SDLWindow CreateWindow(Window window, ref IsWindow component)
        {
            SDL_WindowFlags flags = default;
            if ((component.windowFlags & WindowFlags.Borderless) != 0)
            {
                flags |= SDL_WindowFlags.Borderless;
            }

            if ((component.windowFlags & WindowFlags.Resizable) != 0)
            {
                flags |= SDL_WindowFlags.Resizable;
            }

            if ((component.windowFlags & WindowFlags.Minimized) != 0)
            {
                flags |= SDL_WindowFlags.Minimized;
            }

            if ((component.windowFlags & WindowFlags.AlwaysOnTop) != 0)
            {
                flags |= SDL_WindowFlags.AlwaysOnTop;
            }

            if ((component.windowFlags & WindowFlags.Transparent) != 0)
            {
                flags |= SDL_WindowFlags.Transparent;
            }

            if (component.windowState == WindowState.Maximized)
            {
                flags |= SDL_WindowFlags.Maximized;
            }
            else if (component.windowState == WindowState.Fullscreen)
            {
                flags |= SDL_WindowFlags.Fullscreen;
            }

            ref WindowTransform transform = ref window.TryGetComponent<WindowTransform>(out bool containsTransform);
            if (!containsTransform)
            {
                throw new NullReferenceException($"Window `{window}` is missing expected `{typeof(WindowTransform)}` component");
            }

            //add extensions
            FixedString rendererLabel = window.RendererLabel;
            if (rendererLabel != default)
            {
                if (rendererLabel.Equals("vulkan"))
                {
                    //add sdl extensions that describe vulkan
                    flags |= SDL_WindowFlags.Vulkan;
                    FixedString[] sdlVulkanExtensions = sdlLibrary.GetVulkanInstanceExtensions();
                    Values<DestinationExtension> extensions = window.GetArray<DestinationExtension>();
                    uint previousLength = extensions.Length;
                    extensions.Length += (uint)sdlVulkanExtensions.Length;
                    for (uint i = 0; i < sdlVulkanExtensions.Length; i++)
                    {
                        extensions[previousLength + i] = new(sdlVulkanExtensions[i]);
                    }
                }
                else
                {
                    Trace.WriteLine($"Unknown renderer label `{rendererLabel}`, not able to add extensions for SDL window");
                }
            }

            USpan<char> titleBuffer = stackalloc char[component.title.Length];
            component.title.CopyTo(titleBuffer);
            SDLWindow sdlWindow = new(titleBuffer, transform.size, flags);

            if ((component.windowFlags & WindowFlags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            component.id = sdlWindow.ID;
            return sdlWindow;
        }

        private readonly SDLWindow GetWindow(Window window)
        {
            if (windowEntities.TryIndexOf(window, out uint index))
            {
                return sdlLibrary.GetWindow(windowIds[index]);
            }

            throw new InvalidOperationException($"Entity `{window}` is not a known SDL window");
        }

        /// <summary>
        /// Updates the SDL window to match the entity.
        /// </summary>
        private readonly void UpdateWindowToMatchEntity(Window window, ref IsWindow component)
        {
            uint index = windowEntities.IndexOf(window);
            SDLWindow sdlWindow = sdlLibrary.GetWindow(windowIds[index]);
            ref WindowTransform transform = ref window.TryGetComponent<WindowTransform>(out bool containsTransform);
            ref SDLWindowState lastState = ref lastWindowStates[index];
            if (containsTransform)
            {
                int x = (int)transform.position.X;
                int y = (int)transform.position.Y;
                if (lastState.x != x || lastState.y != y)
                {
                    lastState.x = x;
                    lastState.y = y;
                    sdlWindow.Position = transform.position;
                }

                int width = (int)transform.size.X;
                int height = (int)transform.size.Y;
                if (lastState.width != width || lastState.height != height)
                {
                    lastState.width = width;
                    lastState.height = height;
                    sdlWindow.Size = transform.size;
                }
            }

            bool borderless = (component.windowFlags & WindowFlags.Borderless) != 0;
            bool resizable = (component.windowFlags & WindowFlags.Resizable) != 0;
            bool minimized = (component.windowFlags & WindowFlags.Minimized) != 0;
            bool alwaysOnTop = (component.windowFlags & WindowFlags.AlwaysOnTop) != 0;
            bool focused = (component.windowFlags & WindowFlags.Focused) != 0;
            if (sdlWindow.IsBorderless != borderless)
            {
                sdlWindow.IsBorderless = borderless;
            }

            if (sdlWindow.IsResizable != resizable)
            {
                sdlWindow.IsResizable = resizable;
            }

            if (sdlWindow.IsMinimized != minimized)
            {
                sdlWindow.IsMinimized = minimized;
            }

            if (focused)
            {
                bool cursorVisible = component.cursorState == CursorState.Normal;
                if (sdlLibrary.IsCursorVisible != cursorVisible)
                {
                    if (cursorVisible)
                    {
                        sdlLibrary.ShowCursor();
                    }
                    else
                    {
                        sdlLibrary.HideCursor();
                    }
                }
            }

            bool hiddenAndConfined = component.cursorState == CursorState.HiddenAndConfined;
            if (sdlWindow.IsRelativeMouseMode != hiddenAndConfined)
            {
                sdlWindow.IsRelativeMouseMode = hiddenAndConfined;
            }

            Vector4 mouseArea = sdlWindow.MouseArea;
            if (mouseArea != component.cursorArea)
            {
                sdlWindow.MouseArea = component.cursorArea;
            }

            sdlWindow.IsAlwaysOnTop = alwaysOnTop;

            if ((component.windowFlags & WindowFlags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            bool isMaximized = sdlWindow.IsMaximized;
            bool isFullscreen = sdlWindow.IsFullscreen;
            if (component.windowState == WindowState.Maximized && !isMaximized)
            {
                sdlWindow.Maximize();
            }
            else if (component.windowState == WindowState.Fullscreen && !isFullscreen)
            {
                sdlWindow.IsFullscreen = true;
            }
            else if (component.windowState == WindowState.Windowed && (isMaximized || isFullscreen))
            {
                sdlWindow.Restore();
            }

            //make sure name of window matches entity
            if (!component.title.Equals(sdlWindow.Title))
            {
                USpan<char> buffer = stackalloc char[component.title.Length];
                component.title.CopyTo(buffer);
                sdlWindow.Title = buffer.ToString();
            }

            lastState.flags = component.windowFlags;
            lastState.state = component.windowState;

            //update referenced display
            SDLDisplay display = sdlWindow.Display;
            World world = window.world;
            Entity displayEntity = GetOrCreateDisplayEntity(world, display);
            IsDisplay displayComponent = new(display.Width, display.Height, display.RefreshRate);
            displayEntity.SetComponent(displayComponent);

            if (component.displayReference == default)
            {
                component.displayReference = window.AddReference(displayEntity);
            }
        }

        private readonly Entity GetOrCreateDisplayEntity(World world, SDLDisplay display)
        {
            uint displayId = display.ID;
            if (!displayEntities.TryGetValue(displayId, out Entity displayEntity))
            {
                displayEntity = new Display(world, display.Width, display.Height, display.RefreshRate);
                displayEntities.Add(displayId, displayEntity);
            }

            return displayEntity;
        }
    }
}
