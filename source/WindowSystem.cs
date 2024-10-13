using Rendering;
using Rendering.Components;
using SDL3;
using Simulation;
using System;
using System.Diagnostics;
using Unmanaged;
using Unmanaged.Collections;
using Windows.Components;
using Windows.Events;

namespace Windows.Systems
{
    public class WindowSystem : SystemBase
    {
        public readonly Library library;

        private readonly ComponentQuery<IsWindow> windowQuery;
        private readonly UnmanagedList<uint> windowEntities;
        private readonly UnmanagedList<uint> windowIds;
        private readonly UnmanagedList<(int, int)> lastWindowPositions;
        private readonly UnmanagedList<(int, int)> lastWindowSizes;
        private readonly UnmanagedList<IsWindow.State> lastWindowState;
        private readonly UnmanagedList<IsWindow.Flags> lastWindowFlags;
        private readonly UnmanagedDictionary<uint, uint> displayEntities;

        public WindowSystem(World world) : base(world)
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
            Subscribe<WindowUpdate>(Update);
        }

        public override void Dispose()
        {
            CloseRemainingWindows();
            displayEntities.Dispose();
            lastWindowFlags.Dispose();
            lastWindowState.Dispose();
            lastWindowSizes.Dispose();
            lastWindowPositions.Dispose();
            windowIds.Dispose();
            windowEntities.Dispose();
            windowQuery.Dispose();
            library.Dispose();
            base.Dispose();
        }

        private void CloseRemainingWindows()
        {
            windowQuery.Update(world);
            foreach (var r in windowQuery)
            {
                uint windowEntity = r.entity;
                if (windowEntities.TryIndexOf(windowEntity, out uint index))
                {
                    SDLWindow sdlWindow = library.GetWindow(windowIds[index]);
                    sdlWindow.Dispose();
                }
            }

            windowIds.Clear();
        }

        private void Update(WindowUpdate e)
        {
            DestroyWindowsOfDestroyedEntities();
            UpdateEntitiesToMatchWindows();
            UpdateWindowsToMatchEntities();
            UpdateDestinationSizes();
        }

        /// <summary>
        /// Polls for changes to windows and updates their entities to match if any property
        /// is different from the presentation.
        /// </summary>
        private void UpdateEntitiesToMatchWindows()
        {
            while (library.PollEvent(out SDL_Event sdlEvent))
            {
                if (sdlEvent.type == SDL_EventType.WindowMoved)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        if (window.state == IsWindow.State.Windowed)
                        {
                            uint entity = windowEntities[index];
                            int x = sdlEvent.window.data1;
                            int y = sdlEvent.window.data2;
                            ref (int x, int y) currentPosition = ref lastWindowPositions[index];
                            if (currentPosition.x != x || currentPosition.y != y)
                            {
                                WindowPosition position = new(new(x, y));
                                currentPosition = (x, y);
                                if (world.ContainsComponent<WindowPosition>(entity))
                                {
                                    world.SetComponent(entity, position);
                                }
                                else
                                {
                                    world.AddComponent(entity, position);
                                }
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowResized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        if (window.state == IsWindow.State.Windowed)
                        {
                            uint entity = windowEntities[index];
                            int width = sdlEvent.window.data1;
                            int height = sdlEvent.window.data2;
                            ref (int x, int y) currentSize = ref lastWindowSizes[index];
                            if (currentSize.x != width || currentSize.y != height)
                            {
                                WindowSize size = new(new(width, height));
                                currentSize = (width, height);
                                if (world.ContainsComponent<WindowSize>(entity))
                                {
                                    world.SetComponent(entity, size);
                                }
                                else
                                {
                                    world.AddComponent(entity, size);
                                }
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowEnterFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.state = IsWindow.State.Fullscreen;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowLeaveFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMaximized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.state = IsWindow.State.Maximized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMinimized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.flags |= IsWindow.Flags.Minimized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowRestored)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.flags &= ~IsWindow.Flags.Minimized;
                        window.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusGained)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        ref IsWindow.Flags lastFlags = ref lastWindowFlags[index];
                        if (!lastFlags.HasFlag(IsWindow.Flags.Focused))
                        {
                            lastFlags |= IsWindow.Flags.Focused;
                            window.flags |= IsWindow.Flags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusLost)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        ref IsWindow.Flags lastFlags = ref lastWindowFlags[index];
                        if (lastFlags.HasFlag(IsWindow.Flags.Focused))
                        {
                            lastFlags &= ~IsWindow.Flags.Focused;
                            window.flags &= ~IsWindow.Flags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowCloseRequested)
                {
                    HandleCloseRequest((uint)sdlEvent.window.windowID);
                }
            }
        }

        private void HandleCloseRequest(uint windowId)
        {
            if (windowIds.TryIndexOf(windowId, out uint index))
            {
                uint windowEntity = windowEntities[index];
                if (world.TryGetComponent(windowEntity, out WindowCloseCallback callback))
                {
                    callback.Invoke(world, windowEntity);
                }
                else
                {
                    Debug.WriteLine($"The close button of window {windowEntities} has no callback, nothing will happen");
                }
            }
        }

        /// <summary>
        /// Updates window presentations to match entities.
        /// </summary>
        private void UpdateWindowsToMatchEntities()
        {
            //create new windows and update existing ones
            windowQuery.Update(world);
            foreach (var r in windowQuery)
            {
                uint windowEntity = r.entity;
                ref IsWindow window = ref r.Component1;
                if (!windowEntities.Contains(windowEntity))
                {
                    SDLWindow newWindow = CreateWindow(windowEntity, window);
                    windowEntities.Add(windowEntity);
                    windowIds.Add(newWindow.ID);
                    lastWindowPositions.Add(newWindow.GetRealPosition());
                    lastWindowSizes.Add(newWindow.GetRealSize());
                    lastWindowState.Add(window.state);
                    lastWindowFlags.Add(window.flags);
                }
                else
                {
                    //create the surface
                    if (!world.ContainsComponent<SurfaceReference>(windowEntity) && world.TryGetComponent(windowEntity, out RenderSystemInUse renderer))
                    {
                        FixedString label = world.GetComponent<IsDestination>(windowEntity).rendererLabel;
                        SDLWindow existingWindow = GetWindow(windowEntity);
                        if (label.Equals("vulkan"))
                        {
                            nint address = existingWindow.CreateVulkanSurface(renderer.address);
                            world.AddComponent(windowEntity, new SurfaceReference(address));
                            Console.WriteLine($"Created surface {address} for window {windowEntity} using renderer `{label}`");
                        }
                        else
                        {
                            throw new NotImplementedException($"Unknown renderer label '{label}', not able to create a surface.");
                        }
                    }
                }

                UpdateWindowToMatchEntity(r.entity, ref window);
            }
        }

        private void UpdateDestinationSizes()
        {
            foreach (var r in windowQuery)
            {
                SDLWindow window = GetWindow(r.entity);
                (int width, int height) = window.GetRealSize();
                ref IsDestination destination = ref world.GetComponentRef<IsDestination>(r.entity);
                if (window.IsMinimized)
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

        private void DestroyWindowsOfDestroyedEntities()
        {
            for (uint i = 0; i < windowEntities.Count; i++)
            {
                uint windowEntity = windowEntities[i];
                if (!world.ContainsEntity(windowEntity))
                {
                    SDLWindow sdlWindow = library.GetWindow(windowIds[i]);
                    sdlWindow.Dispose();

                    windowEntities.RemoveAt(i);
                    windowIds.RemoveAt(i);
                    break;
                }
            }
        }

        private SDLWindow CreateWindow(uint entity, IsWindow window)
        {
            SDL_WindowFlags flags = default;
            if ((window.flags & IsWindow.Flags.Borderless) != 0)
            {
                flags |= SDL_WindowFlags.Borderless;
            }

            if ((window.flags & IsWindow.Flags.Resizable) != 0)
            {
                flags |= SDL_WindowFlags.Resizable;
            }

            if ((window.flags & IsWindow.Flags.Minimized) != 0)
            {
                flags |= SDL_WindowFlags.Minimized;
            }

            if ((window.flags & IsWindow.Flags.AlwaysOnTop) != 0)
            {
                flags |= SDL_WindowFlags.AlwaysOnTop;
            }

            if ((window.flags & IsWindow.Flags.Transparent) != 0)
            {
                flags |= SDL_WindowFlags.Transparent;
            }

            if (window.state == IsWindow.State.Maximized)
            {
                flags |= SDL_WindowFlags.Maximized;
            }
            else if (window.state == IsWindow.State.Fullscreen)
            {
                flags |= SDL_WindowFlags.Fullscreen;
            }

            Window windowEntity = new(world, entity);
            if (!world.TryGetComponent(entity, out WindowSize size))
            {
                throw new NullReferenceException($"Window {entity} is missing expected {typeof(WindowSize)} component");
            }

            //add extensions
            IsDestination destination = world.GetComponent<IsDestination>(entity);
            if (destination.rendererLabel != default)
            {
                if (destination.rendererLabel.Equals("vulkan"))
                {
                    flags |= SDL_WindowFlags.Vulkan;
                    FixedString[] sdlVulkanExtensions = library.GetVulkanInstanceExtensions();
                    USpan<Destination.Extension> extensions = world.GetArray<Destination.Extension>(entity);
                    uint previousLength = extensions.Length;
                    extensions = world.ResizeArray<Destination.Extension>(entity, (uint)(previousLength + sdlVulkanExtensions.Length));
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

            USpan<char> buffer = stackalloc char[(int)FixedString.MaxLength];
            uint length = window.title.CopyTo(buffer);
            SDLWindow sdlWindow = new(buffer.Slice(0, length), size.value, flags);

            if ((window.flags & IsWindow.Flags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            return sdlWindow;
        }

        public SDLWindow GetWindow(uint entity)
        {
            if (windowEntities.TryIndexOf(entity, out uint index))
            {
                return library.GetWindow(windowIds[index]);
            }

            throw new InvalidOperationException($"Entity {entity} is not a window");
        }

        /// <summary>
        /// Updates the SDL window to match the entity.
        /// </summary>
        private void UpdateWindowToMatchEntity(uint windowEntity, ref IsWindow window)
        {
            uint index = windowEntities.IndexOf(windowEntity);
            SDLWindow sdlWindow = library.GetWindow(windowIds[index]);
            if (world.TryGetComponent(windowEntity, out WindowPosition position))
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

            if (world.TryGetComponent(windowEntity, out WindowSize size))
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

            bool borderless = (window.flags & IsWindow.Flags.Borderless) != 0;
            bool resizable = (window.flags & IsWindow.Flags.Resizable) != 0;
            bool minimized = (window.flags & IsWindow.Flags.Minimized) != 0;
            bool alwaysOnTop = (window.flags & IsWindow.Flags.AlwaysOnTop) != 0;
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

            if ((window.flags & IsWindow.Flags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            bool isMaximized = sdlWindow.IsMaximized;
            bool isFullscreen = sdlWindow.IsFullscreen;
            if (window.state == IsWindow.State.Maximized && !isMaximized)
            {
                sdlWindow.Maximize();
            }
            else if (window.state == IsWindow.State.Fullscreen && !isFullscreen)
            {
                sdlWindow.IsFullscreen = true;
            }
            else if (window.state == IsWindow.State.Windowed && (isMaximized || isFullscreen))
            {
                sdlWindow.Restore();
            }

            //make sure name of window matches entity
            if (!window.title.Equals(window.title))
            {
                USpan<char> buffer = stackalloc char[(int)FixedString.MaxLength];
                uint length = window.title.CopyTo(buffer);
                window.title = new(buffer.Slice(0, length));
            }

            lastWindowFlags[index] = window.flags;
            lastWindowState[index] = window.state;

            //update referenced display
            SDLDisplay display = sdlWindow.Display;
            uint displayEntity = GetOrCreateDisplayEntity(display);
            ref IsDisplay displayComponent = ref world.GetComponentRef<IsDisplay>(displayEntity);
            displayComponent.width = display.Width;
            displayComponent.height = display.Height;
            displayComponent.refreshRate = display.RefreshRate;

            if (window.displayReference == default)
            {
                window.displayReference = world.AddReference(windowEntity, displayEntity);
            }
        }

        private uint GetOrCreateDisplayEntity(SDLDisplay display)
        {
            uint displayId = display.ID;
            if (!displayEntities.TryGetValue(displayId, out uint displayEntity))
            {
                displayEntity = world.CreateEntity();
                displayEntities.Add(displayId, displayEntity);
                world.AddComponent<IsDisplay>(displayEntity);
            }

            return displayEntity;
        }
    }
}
