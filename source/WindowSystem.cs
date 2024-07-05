using Simulation;
using SDL;
using System;
using System.Diagnostics;
using System.Numerics;
using Unmanaged.Collections;
using Windows.Components;
using Windows.Events;

namespace Windows.Systems
{
    public class WindowSystem : SystemBase
    {
        public readonly SDL3Library library;

        private readonly Query<IsWindow> windowQuery;
        private readonly UnmanagedList<EntityID> windowEntities;
        private readonly UnmanagedList<uint> windowIds;
        private readonly UnmanagedList<WindowPosition> lastWindowPositions;
        private readonly UnmanagedList<WindowSize> lastWindowSizes;
        private readonly UnmanagedList<IsWindow.State> lastWindowState;
        private readonly UnmanagedList<IsWindow.Flags> lastWindowFlags;
        private readonly UnmanagedDictionary<SDL_KeyboardID, Keyboard> keyboards;
        private readonly UnmanagedDictionary<SDL_MouseID, Mouse> mice;

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
            UpdateWindows();
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
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        if (window.state == IsWindow.State.Windowed)
                        {
                            EntityID entity = windowEntities[index];
                            int x = sdlEvent.window.data1;
                            int y = sdlEvent.window.data2;
                            WindowPosition position = new(x, y);
                            ref WindowPosition currentPosition = ref lastWindowPositions.GetRef(index);
                            if (currentPosition != position)
                            {
                                currentPosition = position;
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
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        if (window.state == IsWindow.State.Windowed)
                        {
                            EntityID entity = windowEntities[index];
                            uint width = (uint)sdlEvent.window.data1;
                            uint height = (uint)sdlEvent.window.data2;
                            WindowSize size = new(width, height);
                            ref WindowSize currentSize = ref lastWindowSizes.GetRef(index);
                            if (currentSize != size)
                            {
                                currentSize = size;
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
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.state = IsWindow.State.Fullscreen;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowLeaveFullscreen)
                {
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMaximized)
                {
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.state = IsWindow.State.Maximized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMinimized)
                {
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.flags |= IsWindow.Flags.Hidden;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowRestored)
                {
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
                    {
                        ref IsWindow window = ref world.GetComponentRef<IsWindow>(windowEntities[index]);
                        window.flags &= ~IsWindow.Flags.Hidden;
                        window.state = IsWindow.State.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusGained)
                {
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
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
                    if (windowIds.TryIndexOf(sdlEvent.window.windowID.Value, out uint index))
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
                    HandleCloseRequest(sdlEvent.window.windowID);
                }
                else if (sdlEvent.type == SDL_EventType.KeyDown)
                {
                    SDL_KeyboardID keyboardId = sdlEvent.key.which;
                    Keyboard keyboard = GetOrCreateKeyboard(keyboardId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.key.timestamp);
                    uint control = (uint)sdlEvent.key.keysym.scancode;
                    keyboard.SetKeyDown(control, true, timeStamp);
                    world.Submit(DeviceButtonPressed.Create(keyboard, control));
                }
                else if (sdlEvent.type == SDL_EventType.KeyUp)
                {
                    SDL_KeyboardID keyboardId = sdlEvent.key.which;
                    if (keyboardId == default || !library.HasKeyboard())
                    {
                        //keyboard no longer available, use the first one
                        keyboardId = keyboards.Keys[0];
                    }

                    Keyboard keyboard = GetOrCreateKeyboard(keyboardId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.key.timestamp);
                    uint control = (uint)sdlEvent.key.keysym.scancode;
                    keyboard.SetKeyDown(control, false, timeStamp);
                    world.Submit(DeviceButtonReleased.Create(keyboard, control));
                }
                else if (sdlEvent.type == SDL_EventType.KeyboardAdded)
                {
                    SDL_KeyboardID keyboardId = sdlEvent.kdevice.which;
                    Keyboard keyboard = GetOrCreateKeyboard(keyboardId);
                }
                else if (sdlEvent.type == SDL_EventType.KeyboardRemoved)
                {
                    SDL_KeyboardID keyboardId = sdlEvent.kdevice.which;
                    if (keyboards.TryGetValue(keyboardId, out Keyboard keyboard))
                    {
                        keyboard.Dispose();
                        keyboards.Remove(keyboardId);
                    }
                }
                else if (sdlEvent.type == SDL_EventType.MouseMotion)
                {
                    SDL_MouseID mouseId = sdlEvent.motion.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.motion.timestamp);
                    Vector2 position = new(sdlEvent.motion.x, sdlEvent.motion.y);
                    mouse.SetPosition(position, timeStamp);
                }
                else if (sdlEvent.type == SDL_EventType.MouseWheel)
                {
                    SDL_MouseID mouseId = sdlEvent.wheel.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.wheel.timestamp);
                    Vector2 scroll = new(sdlEvent.wheel.x, sdlEvent.wheel.y);
                    mouse.AddScroll(scroll, timeStamp);
                }
                else if (sdlEvent.type == SDL_EventType.MouseButtonDown)
                {
                    SDL_MouseID mouseId = sdlEvent.button.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.button.timestamp);
                    uint control = (uint)sdlEvent.button.button;
                    mouse.SetButtonDown((Mouse.Button)control, true, timeStamp);
                    world.Submit(DeviceButtonPressed.Create(mouse, control));
                }
                else if (sdlEvent.type == SDL_EventType.MouseButtonUp)
                {
                    SDL_MouseID mouseId = sdlEvent.button.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                    TimeSpan timeStamp = TimeSpan.FromMilliseconds(sdlEvent.button.timestamp);
                    uint control = (uint)sdlEvent.button.button;
                    mouse.SetButtonDown((Mouse.Button)control, false, timeStamp);
                    world.Submit(DeviceButtonReleased.Create(mouse, control));
                }
                else if (sdlEvent.type == SDL_EventType.MouseAdded)
                {
                    SDL_MouseID mouseId = sdlEvent.mdevice.which;
                    Mouse mouse = GetOrCreateMouse(mouseId);
                }
                else if (sdlEvent.type == SDL_EventType.MouseRemoved)
                {
                    SDL_MouseID mouseId = sdlEvent.mdevice.which;
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
            foreach (EntityID keyboardEntity in world.GetAll<IsKeyboard>())
            {
                ref LastKeyboardState lastState = ref world.GetComponentRef<LastKeyboardState>(keyboardEntity);
                KeyboardState currentState = world.GetComponent<IsKeyboard>(keyboardEntity).state;
                lastState = new(currentState);
            }

            foreach (EntityID mouseEntity in world.GetAll<IsMouse>())
            {
                ref LastMouseState lastState = ref world.GetComponentRef<LastMouseState>(mouseEntity);
                IsMouse mouse = world.GetComponent<IsMouse>(mouseEntity);
                lastState = new(mouse.state);
            }
        }

        private void HandleCloseRequest(SDL_WindowID windowId)
        {
            if (windowIds.TryIndexOf(windowId, out uint index))
            {
                EntityID windowEntity = windowEntities[index];
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

        private Keyboard GetOrCreateKeyboard(SDL_KeyboardID keyboardId)
        {
            if (!keyboards.TryGetValue(keyboardId, out Keyboard keyboard))
            {
                keyboard = new(world);
                keyboards.Add(keyboardId, keyboard);
            }

            return keyboard;
        }

        private Mouse GetOrCreateMouse(SDL_MouseID mouseId)
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
        private void UpdateWindows()
        {
            DestroyWindowsForDestroyedEntities();

            //create new windows and update existing ones
            windowQuery.Fill();
            foreach (Query<IsWindow>.Result result in windowQuery)
            {
                ref IsWindow window = ref result.Component1;
                if (!windowEntities.Contains(result.entity))
                {
                    SDL3Window newWindow = CreateWindow(result.entity, window);
                    windowEntities.Add(result.entity);
                    windowIds.Add(newWindow.ID);
                    lastWindowPositions.Add(newWindow.Position);
                    lastWindowSizes.Add(newWindow.Size);
                    lastWindowState.Add(window.state);
                    lastWindowFlags.Add(window.flags);
                }

                UpdateWindow(result.entity, ref window);
            }
        }

        private void DestroyWindowsForDestroyedEntities()
        {
            for (uint i = 0; i < windowEntities.Count; i++)
            {
                EntityID windowEntity = windowEntities[i];
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

        private SDL3Window CreateWindow(EntityID entity, IsWindow window)
        {
            SDL_WindowFlags flags = default;
            if (window.IsBorderless)
            {
                flags |= SDL_WindowFlags.Borderless;
            }

            if (window.IsResizable)
            {
                flags |= SDL_WindowFlags.Resizable;
            }

            if (window.IsHidden)
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
            Span<char> buffer = stackalloc char[window.title.Length];
            window.title.CopyTo(buffer);
            if (!world.TryGetComponent(entity, out WindowSize size))
            {
                throw new NullReferenceException($"Window {entity} is missing expected {typeof(WindowSize)} component");
            }

            return new(buffer[..window.title.Length], size.width, size.height, flags);
        }

        public SDL3Window GetWindow(EntityID entity)
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
        private void UpdateWindow(EntityID windowEntity, ref IsWindow window)
        {
            uint index = windowEntities.IndexOf(windowEntity);
            SDL3Window sdlWindow = library.GetWindow(windowIds[index]);
            if (world.TryGetComponent(windowEntity, out WindowPosition position))
            {
                sdlWindow.Position = position;
                lastWindowPositions[index] = position;
            }

            if (world.TryGetComponent(windowEntity, out WindowSize size))
            {
                sdlWindow.Size = size;
                lastWindowSizes[index] = size;
            }

            if (sdlWindow.IsBorderless != window.IsBorderless)
            {
                sdlWindow.IsBorderless = window.IsBorderless;
            }

            if (sdlWindow.IsResizable != window.IsResizable)
            {
                sdlWindow.IsResizable = window.IsResizable;
            }

            if (sdlWindow.IsHidden != window.IsHidden)
            {
                sdlWindow.IsHidden = window.IsHidden;
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
                Span<char> buffer = stackalloc char[window.title.Length];
                window.title.CopyTo(buffer);
                window.title = new(buffer[..window.title.Length]);
            }

            lastWindowFlags[index] = window.flags;
            lastWindowState[index] = window.state;
        }
    }
}
