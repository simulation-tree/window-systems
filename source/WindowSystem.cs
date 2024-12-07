using Collections;
using Rendering;
using Rendering.Components;
using SDL3;
using Simulation;
using System;
using System.Diagnostics;
using Unmanaged;
using Windows.Components;
using Worlds;

namespace Windows.Systems
{
    public readonly partial struct WindowSystem : ISystem
    {
        private readonly Library library;
        private readonly List<Window> windowEntities;
        private readonly List<uint> windowIds;
        private readonly List<WindowState> lastWindowStates;
        private readonly Dictionary<uint, Entity> displayEntities;

        private WindowSystem(Library library, List<Window> windowEntities, List<uint> windowIds, List<WindowState> lastWindowStates, Dictionary<uint, Entity> displayEntities)
        {
            this.library = library;
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
                List<WindowState> lastWindowStates = new();
                Dictionary<uint, Entity> displayEntities = new();
                systemContainer.Write(new WindowSystem(library, windowEntities, windowIds, lastWindowStates, displayEntities));
            }
        }

        void ISystem.Update(in SystemContainer systemContainer, in World world, in TimeSpan delta)
        {
            if (systemContainer.World == world)
            {
                DestroyWindowsOfDestroyedEntities();
            }

            UpdateWindowsToMatchEntities(world);
            UpdateDestinationSizes(world);

            if (systemContainer.World == world)
            {
                UpdateEntitiesToMatchWindows();
            }
        }

        void ISystem.Finish(in SystemContainer systemContainer, in World world)
        {
            CloseRemainingWindows(world);

            if (systemContainer.World == world)
            {
                displayEntities.Dispose();
                lastWindowStates.Dispose();
                windowIds.Dispose();
                windowEntities.Dispose();
                library.Dispose();
            }
        }

        private readonly void CloseRemainingWindows(World world)
        {
            ComponentQuery<IsWindow> query = new(world);
            foreach (var r in query)
            {
                ref IsWindow component = ref r.component1;
                Window windowEntity = new(world, r.entity);
                if (windowEntities.TryIndexOf(windowEntity, out uint index))
                {
                    SDLWindow sdlWindow = library.GetWindow(windowIds[index]);
                    sdlWindow.Dispose();
                }
            }
        }

        /// <summary>
        /// Polls for changes to windows and updates their entities to match if any property
        /// is different from the presentation.
        /// </summary>
        private readonly void UpdateEntitiesToMatchWindows()
        {
            while (library.PollEvent(out SDL_Event sdlEvent))
            {
                if (sdlEvent.type == SDL_EventType.WindowMoved)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        IsWindow.State state = window.State;
                        if (state == IsWindow.State.Windowed)
                        {
                            int x = sdlEvent.window.data1;
                            int y = sdlEvent.window.data2;
                            ref WindowState lastState = ref lastWindowStates[index];
                            if (lastState.x != x || lastState.y != y)
                            {
                                lastState.x = x;
                                lastState.y = y;
                                ref WindowTransform transform = ref window.AsEntity().TryGetComponent<WindowTransform>(out bool contains);
                                if (!contains)
                                {
                                    transform = ref window.AsEntity().AddComponent<WindowTransform>();
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
                        IsWindow.State state = window.State;
                        if (state == IsWindow.State.Windowed)
                        {
                            int width = sdlEvent.window.data1;
                            int height = sdlEvent.window.data2;
                            ref WindowState lastState = ref lastWindowStates[index];
                            if (lastState.width != width || lastState.height != height)
                            {
                                lastState.width = width;
                                lastState.height = height;
                                ref WindowTransform transform = ref window.AsEntity().TryGetComponent<WindowTransform>(out bool contains);
                                if (!contains)
                                {
                                    transform = ref window.AsEntity().AddComponent<WindowTransform>();
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
                        ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                        component.state = IsWindow.State.Fullscreen;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowLeaveFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                        component.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMaximized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                        component.state = IsWindow.State.Maximized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMinimized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                        component.flags |= IsWindow.Flags.Minimized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowRestored)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                        component.flags &= ~IsWindow.Flags.Minimized;
                        component.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusGained)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref WindowState lastState = ref lastWindowStates[index];
                        if (!lastState.flags.HasFlag(IsWindow.Flags.Focused))
                        {
                            Window window = windowEntities[index];
                            ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                            lastState.flags |= IsWindow.Flags.Focused;
                            component.flags |= IsWindow.Flags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusLost)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref WindowState lastState = ref lastWindowStates[index];
                        if (lastState.flags.HasFlag(IsWindow.Flags.Focused))
                        {
                            Window window = windowEntities[index];
                            ref IsWindow component = ref window.AsEntity().GetComponent<IsWindow>();
                            lastState.flags &= ~IsWindow.Flags.Focused;
                            component.flags &= ~IsWindow.Flags.Focused;
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
                    Trace.WriteLine($"The close button of window `{window}` has no callback, nothing will happen");
                }
            }
            else
            {
                throw new InvalidOperationException($"Window with ID `{windowId}` is not known to the window system");
            }
        }

        private readonly void UpdateWindowsToMatchEntities(World world)
        {
            ComponentQuery<IsWindow> query = new(world);
            foreach (var r in query)
            {
                ref IsWindow window = ref r.component1;
                Window windowEntity = new(world, r.entity);
                if (!windowEntities.Contains(windowEntity))
                {
                    SDLWindow newWindow = CreateWindow(windowEntity, window);
                    windowEntities.Add(windowEntity);
                    windowIds.Add(newWindow.ID);

                    (int x, int y) = newWindow.GetRealPosition();
                    (int width, int height) = newWindow.GetRealSize();
                    lastWindowStates.Add(new(x, y, width, height, window.state, window.flags));
                }
                else
                {
                    //create the surface
                    if (!windowEntity.TryGetSurfaceReference(out _) && windowEntity.TryGetRenderSystemInUse(out RenderSystemInUse renderer))
                    {
                        FixedString label = windowEntity.GetRendererLabel();
                        SDLWindow existingWindow = GetWindow(windowEntity);
                        if (label.Equals("vulkan"))
                        {
                            nint address = existingWindow.CreateVulkanSurface(renderer.address);
                            windowEntity.AddComponent(new SurfaceReference(address));
                            Trace.WriteLine($"Created surface `{address}` for window `{windowEntity}` using renderer `{label}`");
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

        private readonly void UpdateDestinationSizes(World world)
        {
            ComponentQuery<IsWindow, IsDestination> query = new(world);
            foreach (var r in query)
            {
                ref IsDestination destination = ref r.component2;
                Window windowEntity = new(world, r.entity);
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

        private readonly void DestroyWindowsOfDestroyedEntities()
        {
            for (uint i = windowEntities.Count - 1; i != uint.MaxValue; i--)
            {
                Window windowEntity = windowEntities[i];
                if (windowEntity.IsDestroyed())
                {
                    SDLWindow sdlWindow = library.GetWindow(windowIds[i]);
                    sdlWindow.Dispose();

                    windowEntities.RemoveAt(i);
                    windowIds.RemoveAt(i);
                    break;
                }
            }
        }

        private readonly SDLWindow CreateWindow(Window window, IsWindow component)
        {
            SDL_WindowFlags flags = default;
            if ((component.flags & IsWindow.Flags.Borderless) != 0)
            {
                flags |= SDL_WindowFlags.Borderless;
            }

            if ((component.flags & IsWindow.Flags.Resizable) != 0)
            {
                flags |= SDL_WindowFlags.Resizable;
            }

            if ((component.flags & IsWindow.Flags.Minimized) != 0)
            {
                flags |= SDL_WindowFlags.Minimized;
            }

            if ((component.flags & IsWindow.Flags.AlwaysOnTop) != 0)
            {
                flags |= SDL_WindowFlags.AlwaysOnTop;
            }

            if ((component.flags & IsWindow.Flags.Transparent) != 0)
            {
                flags |= SDL_WindowFlags.Transparent;
            }

            if (component.state == IsWindow.State.Maximized)
            {
                flags |= SDL_WindowFlags.Maximized;
            }
            else if (component.state == IsWindow.State.Fullscreen)
            {
                flags |= SDL_WindowFlags.Fullscreen;
            }

            ref WindowTransform transform = ref window.AsEntity().TryGetComponent<WindowTransform>(out bool containsTransform);
            if (!containsTransform)
            {
                throw new NullReferenceException($"Window `{window}` is missing expected `{typeof(WindowTransform)}` component");
            }

            //add extensions
            FixedString rendererLabel = window.GetRendererLabel();
            if (rendererLabel != default)
            {
                if (rendererLabel.Equals("vulkan"))
                {
                    //add sdl extensions that describe vulkan
                    flags |= SDL_WindowFlags.Vulkan;
                    FixedString[] sdlVulkanExtensions = library.GetVulkanInstanceExtensions();
                    USpan<DestinationExtension> extensions = window.AsEntity().GetArray<DestinationExtension>();
                    uint previousLength = extensions.Length;
                    extensions = window.AsEntity().ResizeArray<DestinationExtension>(previousLength + (uint)sdlVulkanExtensions.Length);
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

            USpan<char> buffer = stackalloc char[(int)FixedString.Capacity];
            uint length = component.title.CopyTo(buffer);
            SDLWindow sdlWindow = new(buffer.Slice(0, length), transform.size, flags);

            if ((component.flags & IsWindow.Flags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            return sdlWindow;
        }

        private readonly SDLWindow GetWindow(Window window)
        {
            if (windowEntities.TryIndexOf(window, out uint index))
            {
                return library.GetWindow(windowIds[index]);
            }

            throw new InvalidOperationException($"Entity `{window}` is not a known SDL window");
        }

        /// <summary>
        /// Updates the SDL window to match the entity.
        /// </summary>
        private readonly void UpdateWindowToMatchEntity(Window window, ref IsWindow component)
        {
            uint index = windowEntities.IndexOf(window);
            SDLWindow sdlWindow = library.GetWindow(windowIds[index]);
            ref WindowTransform transform = ref window.AsEntity().TryGetComponent<WindowTransform>(out bool containsTransform);
            ref WindowState lastState = ref lastWindowStates[index];
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

            bool borderless = (component.flags & IsWindow.Flags.Borderless) != 0;
            bool resizable = (component.flags & IsWindow.Flags.Resizable) != 0;
            bool minimized = (component.flags & IsWindow.Flags.Minimized) != 0;
            bool alwaysOnTop = (component.flags & IsWindow.Flags.AlwaysOnTop) != 0;
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

            sdlWindow.IsAlwaysOnTop = alwaysOnTop;

            if ((component.flags & IsWindow.Flags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            bool isMaximized = sdlWindow.IsMaximized;
            bool isFullscreen = sdlWindow.IsFullscreen;
            if (component.state == IsWindow.State.Maximized && !isMaximized)
            {
                sdlWindow.Maximize();
            }
            else if (component.state == IsWindow.State.Fullscreen && !isFullscreen)
            {
                sdlWindow.IsFullscreen = true;
            }
            else if (component.state == IsWindow.State.Windowed && (isMaximized || isFullscreen))
            {
                sdlWindow.Restore();
            }

            //make sure name of window matches entity
            if (!component.title.Equals(component.title))
            {
                USpan<char> buffer = stackalloc char[(int)FixedString.Capacity];
                uint length = component.title.CopyTo(buffer);
                component.title = new(buffer.Slice(0, length));
            }

            lastState.flags = component.flags;
            lastState.state = component.state;

            //update referenced display
            SDLDisplay display = sdlWindow.Display;
            World world = window.GetWorld();
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
                displayEntity = new(world);
                displayEntities.Add(displayId, displayEntity);
                displayEntity.AddComponent<IsDisplay>();
            }

            return displayEntity;
        }
    }
}
