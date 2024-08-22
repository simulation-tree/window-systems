using Rendering;
using Rendering.Components;
using SDL3;
using Simulation;
using System;
using System.Diagnostics;
using System.Numerics;
using Unmanaged;
using Unmanaged.Collections;
using Windows.Components;
using Windows.Events;

namespace Windows.Systems
{
    public class WindowSystem : SystemBase
    {
        public readonly Library library;

        private readonly Query<IsWindow> windowQuery;
        private readonly UnmanagedList<eint> windowEntities;
        private readonly UnmanagedList<uint> windowIds;
        private readonly UnmanagedList<(int, int)> lastWindowPositions;
        private readonly UnmanagedList<(int, int)> lastWindowSizes;
        private readonly UnmanagedList<IsWindow.State> lastWindowState;
        private readonly UnmanagedList<IsWindow.Flags> lastWindowFlags;
        private readonly UnmanagedDictionary<uint, Keyboard> keyboards;
        private readonly UnmanagedDictionary<uint, Mouse> mice;

        public WindowSystem(World world) : base(world)
        {
            library = new();
            windowQuery = new(world);
            windowEntities = new();
            windowIds = new();
            lastWindowPositions = new();
            lastWindowSizes = new();
            lastWindowState = new();
            lastWindowFlags = new();
            keyboards = new();
            mice = new();
            Subscribe<WindowUpdate>(Update);
        }

        public override void Dispose()
        {
            CloseCurrentWindows();
            mice.Dispose();
            keyboards.Dispose();
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

        private void CloseCurrentWindows()
        {
            for (uint i = 0; i < windowIds.Count; i++)
            {
                SDL3Window window = library.GetWindow(windowIds[i]);
                window.Dispose();
            }

            windowIds.Clear();
        }

        private void Update(WindowUpdate e)
        {
            UpdateLastDeviceStates();
            PollWindowEvents();
            UpdateWindowsToMatchEntities();
            UpdateDestinationSizes();
        }

        /// <summary>
        /// Polls for changes to windows and updates their entities to match if any property
        /// is different from the presentation.
        /// </summary>
        private void PollWindowEvents()
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
                            eint entity = windowEntities[index];
                            int x = sdlEvent.window.data1;
                            int y = sdlEvent.window.data2;
                            ref (int x, int y) currentPosition = ref lastWindowPositions.GetRef(index);
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
                            eint entity = windowEntities[index];
                            int width = sdlEvent.window.data1;
                            int height = sdlEvent.window.data2;
                            ref (int x, int y) currentSize = ref lastWindowSizes.GetRef(index);
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
                        ref IsWindow.Flags lastFlags = ref lastWindowFlags.GetRef(index);
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
                        ref IsWindow.Flags lastFlags = ref lastWindowFlags.GetRef(index);
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
                else if (sdlEvent.type == SDL_EventType.KeyDown)
                {
                    uint keyboardId = (uint)sdlEvent.key.which;
                    Keyboard keyboard = GetOrCreateKeyboard(keyboardId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.key.timestamp);
                    uint control = (uint)sdlEvent.key.scancode;
                    keyboard.SetKeyDown(control, true, timeStamp);
                    world.Submit(DeviceButtonPressed.Create(keyboard, control));
                }
                else if (sdlEvent.type == SDL_EventType.KeyUp)
                {
                    uint keyboardId = (uint)sdlEvent.key.which;
                    if (keyboardId == default || !library.HasKeyboard())
                    {
                        //keyboard no longer available, use the first one
                        //todo: this release event is faulty, it shouldnt happen with another keyboard
                        //the keyboard that was removed should stay in existence until all release events were received
                        keyboardId = keyboards.Keys[0];
                    }

                    Keyboard keyboard = GetOrCreateKeyboard(keyboardId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.key.timestamp);
                    uint control = (uint)sdlEvent.key.scancode;
                    keyboard.SetKeyDown(control, false, timeStamp);
                    world.Submit(DeviceButtonReleased.Create(keyboard, control));
                }
                else if (sdlEvent.type == SDL_EventType.KeyboardAdded)
                {
                    uint keyboardId = (uint)sdlEvent.kdevice.which;
                    Keyboard keyboard = GetOrCreateKeyboard(keyboardId);
                }
                else if (sdlEvent.type == SDL_EventType.KeyboardRemoved)
                {
                    uint keyboardId = (uint)sdlEvent.kdevice.which;
                    if (keyboards.TryGetValue(keyboardId, out Keyboard keyboard))
                    {
                        keyboard.Dispose();
                        keyboards.Remove(keyboardId);
                    }
                }
                else if (sdlEvent.type == SDL_EventType.MouseMotion)
                {
                    uint mouseId = (uint)sdlEvent.motion.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.motion.timestamp);
                    Vector2 position = new(sdlEvent.motion.x, sdlEvent.motion.y);
                    mouse.SetPosition(position, timeStamp);
                }
                else if (sdlEvent.type == SDL_EventType.MouseWheel)
                {
                    uint mouseId = (uint)sdlEvent.wheel.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.wheel.timestamp);
                    Vector2 scroll = new(sdlEvent.wheel.x, sdlEvent.wheel.y);
                    mouse.AddScroll(scroll, timeStamp);
                }
                else if (sdlEvent.type == SDL_EventType.MouseButtonDown)
                {
                    uint mouseId = (uint)sdlEvent.button.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.button.timestamp);
                    uint control = (uint)sdlEvent.button.button;
                    mouse.SetButtonDown(control, true, timeStamp);
                    world.Submit(DeviceButtonPressed.Create(mouse, control));
                }
                else if (sdlEvent.type == SDL_EventType.MouseButtonUp)
                {
                    uint mouseId = (uint)sdlEvent.button.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.button.timestamp);
                    uint control = (uint)sdlEvent.button.button;
                    mouse.SetButtonDown(control, false, timeStamp);
                    world.Submit(DeviceButtonReleased.Create(mouse, control));
                }
                else if (sdlEvent.type == SDL_EventType.MouseAdded)
                {
                    uint mouseId = (uint)sdlEvent.mdevice.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                }
                else if (sdlEvent.type == SDL_EventType.MouseRemoved)
                {
                    uint mouseId = (uint)sdlEvent.mdevice.which;
                    if (mice.TryGetValue(mouseId, out Mouse mouse))
                    {
                        mouse.Dispose();
                        mice.Remove(mouseId);
                    }
                }
            }
        }

        private void UpdateLastDeviceStates()
        {
            foreach (eint keyboardEntity in world.GetAll<IsKeyboard>())
            {
                ref LastKeyboardState lastState = ref world.GetComponentRef<LastKeyboardState>(keyboardEntity);
                KeyboardState currentState = world.GetComponent<IsKeyboard>(keyboardEntity).state;
                lastState = new(currentState);
            }

            foreach (eint mouseEntity in world.GetAll<IsMouse>())
            {
                ref LastMouseState lastState = ref world.GetComponentRef<LastMouseState>(mouseEntity);
                IsMouse mouse = world.GetComponent<IsMouse>(mouseEntity);
                lastState = new(mouse.state);
            }
        }

        private void HandleCloseRequest(uint windowId)
        {
            if (windowIds.TryIndexOf(windowId, out uint index))
            {
                eint windowEntity = windowEntities[index];
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

        private Keyboard GetOrCreateKeyboard(uint keyboardId)
        {
            if (!keyboards.TryGetValue(keyboardId, out Keyboard keyboard))
            {
                keyboard = new(world);
                keyboards.Add(keyboardId, keyboard);
            }

            return keyboard;
        }

        private Mouse GetOrCreateMouse(uint mouseId)
        {
            if (!mice.TryGetValue(mouseId, out Mouse mouse))
            {
                mouse = new(world);
                mice.Add(mouseId, mouse);
            }

            return mouse;
        }

        /// <summary>
        /// Updates window presentations to match entities.
        /// </summary>
        private void UpdateWindowsToMatchEntities()
        {
            DestroyOldWindows();

            //create new windows and update existing ones
            windowQuery.Update();
            foreach (var r in windowQuery)
            {
                eint windowEntity = r.entity;
                IsWindow window = r.Component1;
                if (!windowEntities.Contains(windowEntity))
                {
                    SDL3Window newWindow = CreateWindow(windowEntity, window);
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
                        SDL3Window existingWindow = GetWindow(windowEntity);
                        if (label == "vulkan")
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

                UpdateWindow(r.entity, ref window);
            }
        }

        private void UpdateDestinationSizes()
        {
            foreach (var r in windowQuery)
            {
                SDL3Window window = GetWindow(r.entity);
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

        private void DestroyOldWindows()
        {
            for (uint i = 0; i < windowEntities.Count; i++)
            {
                eint windowEntity = windowEntities[i];
                if (!world.ContainsEntity(windowEntity))
                {
                    SDL3Window sdlWindow = library.GetWindow(windowIds[i]);
                    sdlWindow.Dispose();

                    windowEntities.RemoveAt(i);
                    windowIds.RemoveAt(i);
                    break;
                }
            }
        }

        private SDL3Window CreateWindow(eint entity, IsWindow window)
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

            if (window.state == IsWindow.State.Maximized)
            {
                flags |= SDL_WindowFlags.Maximized;
            }
            else if (window.state == IsWindow.State.Fullscreen)
            {
                flags |= SDL_WindowFlags.Fullscreen;
            }

            Window windowEntity = new(world, entity);
            Span<char> buffer = stackalloc char[FixedString.MaxLength];
            int length = window.title.ToString(buffer);
            if (!world.TryGetComponent(entity, out WindowSize size))
            {
                throw new NullReferenceException($"Window {entity} is missing expected {typeof(WindowSize)} component");
            }

            //add extensions
            IsDestination destination = world.GetComponent<IsDestination>(entity);
            if (destination.rendererLabel != default)
            {
                UnmanagedList<Destination.Extension> extensions = world.GetList<Destination.Extension>(entity);
                if (destination.rendererLabel == "vulkan")
                {
                    flags |= SDL_WindowFlags.Vulkan;
                    FixedString[] sdlVulkanExtensions = library.GetVulkanInstanceExtensions();
                    foreach (FixedString extension in sdlVulkanExtensions)
                    {
                        extensions.Add(new(extension));
                    }
                }
                else if (destination.rendererLabel == "ogl")
                {
                    throw new NotImplementedException();
                }
                else if (destination.rendererLabel == "dx3d")
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new InvalidOperationException($"Unknown renderer label '{destination.rendererLabel}'");
                }
            }

            return new(buffer[..length], size.value, flags);
        }

        public SDL3Window GetWindow(eint entity)
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
        private void UpdateWindow(eint windowEntity, ref IsWindow window)
        {
            uint index = windowEntities.IndexOf(windowEntity);
            SDL3Window sdlWindow = library.GetWindow(windowIds[index]);
            if (world.TryGetComponent(windowEntity, out WindowPosition position))
            {
                ref (int x, int y) lastPosition = ref lastWindowPositions.GetRef(index);
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
                ref (int height, int width) lastSize = ref lastWindowSizes.GetRef(index);
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
                Span<char> buffer = stackalloc char[FixedString.MaxLength];
                int length = window.title.ToString(buffer);
                window.title = new(buffer[..length]);
            }

            lastWindowFlags[index] = window.flags;
            lastWindowState[index] = window.state;
        }
    }
}
