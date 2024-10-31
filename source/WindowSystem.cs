using Collections;
using Rendering;
using Rendering.Components;
using SDL3;
using Simulation;
using Simulation.Functions;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unmanaged;
using Windows.Components;

namespace Windows.Systems
{
    public readonly struct WindowSystem : ISystem
    {
        private readonly Library library;
        private readonly ComponentQuery<IsWindow> windowQuery;
        private readonly List<Window> windowEntities;
        private readonly List<uint> windowIds;
        private readonly List<(int, int)> lastWindowPositions;
        private readonly List<(int, int)> lastWindowSizes;
        private readonly List<IsWindow.State> lastWindowState;
        private readonly List<IsWindow.Flags> lastWindowFlags;
        private readonly Dictionary<uint, Entity> displayEntities;

        readonly unsafe InitializeFunction ISystem.Initialize => new(&Initialize);
        readonly unsafe IterateFunction ISystem.Update => new(&Update);
        readonly unsafe FinalizeFunction ISystem.Finalize => new(&Finalize);

        [UnmanagedCallersOnly]
        private static void Initialize(SystemContainer container, World world)
        {
        }

        [UnmanagedCallersOnly]
        private static void Update(SystemContainer container, World world, TimeSpan delta)
        {
            ref WindowSystem system = ref container.Read<WindowSystem>();
            if (container.World == world)
            {
                system.DestroyWindowsOfDestroyedEntities();
            }

            system.Update(world);

            if (container.World == world)
            {
                system.UpdateEntitiesToMatchWindows();
            }
        }

        [UnmanagedCallersOnly]
        private static void Finalize(SystemContainer container, World world)
        {
            ref WindowSystem system = ref container.Read<WindowSystem>();
            system.CloseRemainingWindows(world);

            if (container.World == world)
            {
                system.CleanUp();
            }
        }

        public WindowSystem()
        {
            library = new();
            windowQuery = new();
            windowEntities = new();
            windowIds = new();
            lastWindowPositions = new();
            lastWindowSizes = new();
            lastWindowState = new();
            lastWindowFlags = new();
            displayEntities = new();
        }

        private void CleanUp()
        {
            displayEntities.Dispose();
            lastWindowFlags.Dispose();
            lastWindowState.Dispose();
            lastWindowSizes.Dispose();
            lastWindowPositions.Dispose();
            windowIds.Dispose();
            windowEntities.Dispose();
            windowQuery.Dispose();
            library.Dispose();
        }

        private readonly void CloseRemainingWindows(World world)
        {
            windowQuery.Update(world);
            foreach (var r in windowQuery)
            {
                Window window = new(world, r.entity);
                if (windowEntities.TryIndexOf(window, out uint index))
                {
                    SDLWindow sdlWindow = library.GetWindow(windowIds[index]);
                    sdlWindow.Dispose();
                }
            }

            windowIds.Clear();
        }

        private readonly void Update(World world)
        {
            UpdateWindowsToMatchEntities(world);
            UpdateDestinationSizes(world);
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
                            ref (int x, int y) currentPosition = ref lastWindowPositions[index];
                            if (currentPosition.x != x || currentPosition.y != y)
                            {
                                WindowPosition position = new(new(x, y));
                                currentPosition = (x, y);
                                if (window.AsEntity().ContainsComponent<WindowPosition>())
                                {
                                    window.SetComponent(position);
                                }
                                else
                                {
                                    window.AddComponent(position);
                                }
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
                            ref (int x, int y) currentSize = ref lastWindowSizes[index];
                            if (currentSize.x != width || currentSize.y != height)
                            {
                                WindowSize size = new(new(width, height));
                                currentSize = (width, height);
                                if (window.AsEntity().ContainsComponent<WindowSize>())
                                {
                                    window.SetComponent(size);
                                }
                                else
                                {
                                    window.AddComponent(size);
                                }
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowEnterFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                        component.state = IsWindow.State.Fullscreen;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowLeaveFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                        component.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMaximized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                        component.state = IsWindow.State.Maximized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMinimized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                        component.flags |= IsWindow.Flags.Minimized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowRestored)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        Window window = windowEntities[index];
                        ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                        component.flags &= ~IsWindow.Flags.Minimized;
                        component.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusGained)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow.Flags lastFlags = ref lastWindowFlags[index];
                        if (!lastFlags.HasFlag(IsWindow.Flags.Focused))
                        {
                            Window window = windowEntities[index];
                            ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                            lastFlags |= IsWindow.Flags.Focused;
                            component.flags |= IsWindow.Flags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusLost)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow.Flags lastFlags = ref lastWindowFlags[index];
                        if (lastFlags.HasFlag(IsWindow.Flags.Focused))
                        {
                            Window window = windowEntities[index];
                            ref IsWindow component = ref window.AsEntity().GetComponentRef<IsWindow>();
                            lastFlags &= ~IsWindow.Flags.Focused;
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
                if (window.AsEntity().TryGetComponent(out WindowCloseCallback callback))
                {
                    callback.Invoke(window);
                }
                else
                {
                    Debug.WriteLine($"The close button of window `{window}` has no callback, nothing will happen");
                }
            }
            else
            {
                throw new InvalidOperationException($"Window with ID `{windowId}` is not known to the window system");
            }
        }

        /// <summary>
        /// Updates window presentations to match entities.
        /// </summary>
        private readonly void UpdateWindowsToMatchEntities(World world)
        {
            //create new windows and update existing ones
            windowQuery.Update(world);
            foreach (var r in windowQuery)
            {
                Window window = new(world, r.entity);
                ref IsWindow component = ref r.Component1;
                if (!windowEntities.Contains(window))
                {
                    SDLWindow newWindow = CreateWindow(window, component);
                    windowEntities.Add(window);
                    windowIds.Add(newWindow.ID);
                    lastWindowPositions.Add(newWindow.GetRealPosition());
                    lastWindowSizes.Add(newWindow.GetRealSize());
                    lastWindowState.Add(component.state);
                    lastWindowFlags.Add(component.flags);
                }
                else
                {
                    //create the surface
                    if (!window.AsEntity().ContainsComponent<SurfaceReference>() && window.AsEntity().TryGetComponent(out RenderSystemInUse renderer))
                    {
                        FixedString label = window.AsEntity().GetComponent<IsDestination>().rendererLabel;
                        SDLWindow existingWindow = GetWindow(window);
                        if (label.Equals("vulkan"))
                        {
                            nint address = existingWindow.CreateVulkanSurface(renderer.address);
                            window.AddComponent(new SurfaceReference(address));
                            Console.WriteLine($"Created surface `{address}` for window `{window}` using renderer `{label}`");
                        }
                        else
                        {
                            throw new NotImplementedException($"Unknown renderer label '{label}', not able to create a surface");
                        }
                    }
                }

                UpdateWindowToMatchEntity(window, ref component);
            }
        }

        private readonly void UpdateDestinationSizes(World world)
        {
            windowQuery.Update(world);
            foreach (var r in windowQuery)
            {
                Window window = new(world, r.entity);
                SDLWindow sdlWindow = GetWindow(window);
                (int width, int height) = sdlWindow.GetRealSize();
                ref IsDestination destination = ref world.GetComponentRef<IsDestination>(r.entity);
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

            if (!window.AsEntity().TryGetComponent(out WindowSize size))
            {
                throw new NullReferenceException($"Window `{window}` is missing expected {typeof(WindowSize)} component");
            }

            //add extensions
            IsDestination destination = window.AsEntity().GetComponent<IsDestination>();
            if (destination.rendererLabel != default)
            {
                if (destination.rendererLabel.Equals("vulkan"))
                {
                    flags |= SDL_WindowFlags.Vulkan;
                    FixedString[] sdlVulkanExtensions = library.GetVulkanInstanceExtensions();
                    USpan<Destination.Extension> extensions = window.AsEntity().GetArray<Destination.Extension>();
                    uint previousLength = extensions.Length;
                    extensions = window.AsEntity().ResizeArray<Destination.Extension>((uint)(previousLength + sdlVulkanExtensions.Length));
                    for (uint i = 0; i < sdlVulkanExtensions.Length; i++)
                    {
                        extensions[previousLength + i] = new(sdlVulkanExtensions[i]);
                    }
                }
                else if (destination.rendererLabel.Equals("ogl"))
                {
                    throw new NotImplementedException();
                }
                else if (destination.rendererLabel.Equals("dx3d"))
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new InvalidOperationException($"Unknown renderer label '{destination.rendererLabel}'");
                }
            }

            USpan<char> buffer = stackalloc char[(int)FixedString.Capacity];
            uint length = component.title.CopyTo(buffer);
            SDLWindow sdlWindow = new(buffer.Slice(0, length), size.value, flags);

            if ((component.flags & IsWindow.Flags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            return sdlWindow;
        }

        public readonly SDLWindow GetWindow(Window window)
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
            if (window.AsEntity().TryGetComponent(out WindowPosition position))
            {
                ref (int x, int y) lastPosition = ref lastWindowPositions[index];
                int x = (int)position.value.X;
                int y = (int)position.value.Y;
                if (lastPosition.x != x || lastPosition.y != y)
                {
                    lastPosition = (x, y);
                    sdlWindow.Position = position.value;
                }
            }

            if (window.AsEntity().TryGetComponent(out WindowSize size))
            {
                ref (int height, int width) lastSize = ref lastWindowSizes[index];
                int width = (int)size.value.X;
                int height = (int)size.value.Y;
                if (lastSize.width != width || lastSize.height != height)
                {
                    lastSize = (width, height);
                    sdlWindow.Size = size.value;
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

            lastWindowFlags[index] = component.flags;
            lastWindowState[index] = component.state;

            //update referenced display
            SDLDisplay display = sdlWindow.Display;
            World world = window.GetWorld();
            Entity displayEntity = GetOrCreateDisplayEntity(world, display);
            ref IsDisplay displayComponent = ref displayEntity.GetComponentRef<IsDisplay>();
            displayComponent.width = display.Width;
            displayComponent.height = display.Height;
            displayComponent.refreshRate = display.RefreshRate;

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
