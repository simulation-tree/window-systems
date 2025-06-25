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
using Windows.Messages;
using Worlds;

namespace Windows.Systems
{
    [SkipLocalsInit]
    public partial class WindowSystem : SystemBase, IListener<WindowUpdate>
    {
        private readonly World world;
        private readonly Library sdlLibrary;
        private readonly List<uint> windowEntities;
        private readonly List<uint> windowIds;
        private readonly List<SDLWindowState> lastWindowStates;
        private readonly Dictionary<uint, uint> displayEntities;
        private readonly int windowType;
        private readonly int displayType;
        private readonly int destinationType;
        private readonly int transformType;
        private readonly int destinationExtensionType;
        private readonly int surfaceInUseType;
        private readonly int rendererInstanceInUseType;

        public WindowSystem(Simulator simulator, World world) : base(simulator)
        {
            this.world = world;
            sdlLibrary = new();
            windowEntities = new(16);
            windowIds = new(16);
            lastWindowStates = new(16);
            displayEntities = new(16);

            Schema schema = world.Schema;
            windowType = schema.GetComponentType<IsWindow>();
            displayType = schema.GetComponentType<IsDisplay>();
            destinationType = schema.GetComponentType<IsDestination>();
            transformType = schema.GetComponentType<WindowTransform>();
            destinationExtensionType = schema.GetArrayType<DestinationExtension>();
            surfaceInUseType = schema.GetComponentType<SurfaceInUse>();
            rendererInstanceInUseType = schema.GetComponentType<RendererInstanceInUse>();
        }

        public override void Dispose()
        {
            CloseRemainingWindows(windowEntities.AsSpan(), windowIds.AsSpan());

            displayEntities.Dispose();
            lastWindowStates.Dispose();
            windowIds.Dispose();
            windowEntities.Dispose();
            sdlLibrary.Dispose();
        }

        void IListener<WindowUpdate>.Receive(ref WindowUpdate message)
        {
            Span<uint> windowEntities = this.windowEntities.AsSpan();
            Span<uint> windowIds = this.windowIds.AsSpan();
            DestroyWindowsOfDestroyedEntities(windowEntities, windowIds);

            windowEntities = this.windowEntities.AsSpan();
            windowIds = this.windowIds.AsSpan();
            CreateMissingWindows(windowEntities);

            windowEntities = this.windowEntities.AsSpan();
            windowIds = this.windowIds.AsSpan();
            UpdateWindowsToMatchEntities(windowEntities, windowIds);
            UpdateDestinationSizes(windowEntities, windowIds);
            UpdateEntitiesToMatchWindows(windowEntities, windowIds);
        }

        private void CloseRemainingWindows(Span<uint> windowEntities, Span<uint> windowIds)
        {
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                if (chunk.Definition.ContainsComponent(windowType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    ComponentEnumerator<IsWindow> components = chunk.GetComponents<IsWindow>(windowType);
                    for (int i = 0; i < components.length; i++)
                    {
                        ref IsWindow component = ref components[i];
                        uint windowEntity = entities[i];
                        if (windowEntities.TryIndexOf(windowEntity, out int index))
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
        private void UpdateEntitiesToMatchWindows(Span<uint> windowEntities, Span<uint> windowIds)
        {
            while (sdlLibrary.PollEvent(out SDL_Event sdlEvent))
            {
                if (sdlEvent.type == SDL_EventType.WindowMoved)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        WindowState state = world.GetComponent<IsWindow>(windowEntity, windowType).windowState;
                        if (state == WindowState.Windowed)
                        {
                            int x = sdlEvent.window.data1;
                            int y = sdlEvent.window.data2;
                            ref SDLWindowState lastState = ref lastWindowStates[index];
                            if (lastState.x != x || lastState.y != y)
                            {
                                lastState.x = x;
                                lastState.y = y;
                                ref WindowTransform transform = ref world.TryGetComponent<WindowTransform>(windowEntity, transformType, out bool contains);
                                if (!contains)
                                {
                                    transform = ref world.AddComponent<WindowTransform>(windowEntity, transformType);
                                }

                                transform.position = new(x, y);
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowResized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        WindowState state = world.GetComponent<IsWindow>(windowEntity, windowType).windowState;
                        if (state == WindowState.Windowed)
                        {
                            int width = sdlEvent.window.data1;
                            int height = sdlEvent.window.data2;
                            ref SDLWindowState lastState = ref lastWindowStates[index];
                            if (lastState.width != width || lastState.height != height)
                            {
                                lastState.width = width;
                                lastState.height = height;
                                ref WindowTransform transform = ref world.TryGetComponent<WindowTransform>(windowEntity, transformType, out bool contains);
                                if (!contains)
                                {
                                    transform = ref world.AddComponent<WindowTransform>(windowEntity, transformType);
                                }

                                transform.size = new(width, height);
                            }
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowEnterFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
                        component.windowState = WindowState.Fullscreen;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowLeaveFullscreen)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
                        component.windowState = WindowState.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMaximized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
                        component.windowState = WindowState.Maximized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowMinimized)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
                        component.windowFlags |= WindowFlags.Minimized;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowRestored)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        uint windowEntity = windowEntities[index];
                        ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
                        component.windowFlags &= ~WindowFlags.Minimized;
                        component.windowState = WindowState.Windowed;
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusGained)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        ref SDLWindowState lastState = ref lastWindowStates[index];
                        if (!lastState.flags.HasFlag(WindowFlags.Focused))
                        {
                            uint windowEntity = windowEntities[index];
                            ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
                            lastState.flags |= WindowFlags.Focused;
                            component.windowFlags |= WindowFlags.Focused;
                        }
                    }
                }
                else if (sdlEvent.type == SDL_EventType.WindowFocusLost)
                {
                    if (windowIds.TryIndexOf((uint)sdlEvent.window.windowID, out int index))
                    {
                        ref SDLWindowState lastState = ref lastWindowStates[index];
                        if (lastState.flags.HasFlag(WindowFlags.Focused))
                        {
                            uint windowEntity = windowEntities[index];
                            ref IsWindow component = ref world.GetComponent<IsWindow>(windowEntity, windowType);
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

        private void HandleCloseRequest(uint windowId)
        {
            if (windowIds.TryIndexOf(windowId, out int index))
            {
                uint windowEntity = windowEntities[index];
                WindowCloseCallback closeCallback = world.GetComponent<IsWindow>(windowEntity, windowType).closeCallback;
                if (closeCallback != default)
                {
                    closeCallback.Invoke(Entity.Get<Window>(world, windowEntity));
                }
                else
                {
                    world.DestroyEntity(windowEntity);
                }
            }
            else
            {
                throw new InvalidOperationException($"Window with ID `{windowId}` is not known to the window system");
            }
        }

        private void CreateMissingWindows(Span<uint> windowEntities)
        {
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                if (chunk.Definition.ContainsComponent(windowType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    ComponentEnumerator<IsWindow> components = chunk.GetComponents<IsWindow>(windowType);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        ref IsWindow window = ref components[i];
                        uint windowEntity = entities[i];
                        if (!windowEntities.Contains(windowEntity))
                        {
                            SDLWindow newWindow = CreateSDLWindow(windowEntity, ref window);
                            this.windowEntities.Add(windowEntity);
                            windowIds.Add(newWindow.ID);

                            (int x, int y) = newWindow.GetRealPosition();
                            (int width, int height) = newWindow.GetRealSize();
                            lastWindowStates.Add(new(x, y, width, height, window.windowState, window.windowFlags));
                            Trace.WriteLine($"Created SDL window `{newWindow.ID}` for entity `{windowEntity}`");
                        }
                    }
                }
            }
        }

        private void UpdateWindowsToMatchEntities(Span<uint> windowEntities, Span<uint> windowIds)
        {
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            Span<SDLWindowState> lastWindowStatesSpan = lastWindowStates.AsSpan();
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                if (chunk.Definition.ContainsComponent(windowType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    ComponentEnumerator<IsWindow> components = chunk.GetComponents<IsWindow>(windowType);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        ref IsWindow window = ref components[i];
                        uint windowEntity = entities[i];

                        //create a surface if necessary
                        int index = windowEntities.IndexOf(windowEntity);
                        SDLWindow sdlWindow = sdlLibrary.GetWindow(windowIds[index]);
                        if (!world.ContainsComponent(windowEntity, surfaceInUseType) && world.TryGetComponent(windowEntity, rendererInstanceInUseType, out RendererInstanceInUse instance))
                        {
                            IsDestination destination = world.GetComponent<IsDestination>(windowEntity, destinationType);
                            if (destination.rendererLabel.Equals("vulkan"))
                            {
                                MemoryAddress surface = sdlWindow.CreateVulkanSurface(instance.value);
                                world.AddComponent(windowEntity, surfaceInUseType, new SurfaceInUse(surface));
                                Trace.WriteLine($"Created surface `{surface}` for window `{windowEntity}`");
                            }
                            else
                            {
                                throw new NotImplementedException($"Unknown renderer label '{destination.rendererLabel}', not able to create a surface");
                            }
                        }

                        //do the updating
                        ref SDLWindowState lastState = ref lastWindowStatesSpan[index];
                        UpdateWindowToMatchEntity(windowEntity, ref window, ref lastState, sdlWindow);
                    }
                }
            }
        }

        /// <summary>
        /// Updates <see cref="IsDestination"/> components to match their windows.
        /// </summary>
        private void UpdateDestinationSizes(Span<uint> windowEntities, Span<uint> windowIds)
        {
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                Definition key = chunk.Definition;
                if (key.ContainsComponent(windowType) && key.ContainsComponent(destinationType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    ComponentEnumerator<IsDestination> destinationComponents = chunk.GetComponents<IsDestination>(destinationType);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        ref IsDestination destination = ref destinationComponents[i];
                        uint windowEntity = entities[i];
                        SDLWindow sdlWindow = GetWindow(windowEntity, windowEntities, windowIds);
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

        private void DestroyWindowsOfDestroyedEntities(Span<uint> windowEntities, Span<uint> windowIds)
        {
            for (int i = windowEntities.Length - 1; i >= 0; i--)
            {
                uint windowEntity = windowEntities[i];
                if (!world.ContainsEntity(windowEntity))
                {
                    SDLWindow sdlWindow = sdlLibrary.GetWindow(windowIds[i]);
                    sdlWindow.Dispose();

                    this.windowEntities.RemoveAt(i);
                    this.windowIds.RemoveAt(i);
                    lastWindowStates.RemoveAt(i);
                    Trace.WriteLine($"Destroyed SDL window for entity `{windowEntity}`");
                }
            }
        }

        private SDLWindow CreateSDLWindow(uint windowEntity, ref IsWindow window)
        {
            SDL_WindowFlags flags = default;
            if ((window.windowFlags & WindowFlags.Borderless) != 0)
            {
                flags |= SDL_WindowFlags.Borderless;
            }

            if ((window.windowFlags & WindowFlags.Resizable) != 0)
            {
                flags |= SDL_WindowFlags.Resizable;
            }

            if ((window.windowFlags & WindowFlags.Minimized) != 0)
            {
                flags |= SDL_WindowFlags.Minimized;
            }

            if ((window.windowFlags & WindowFlags.AlwaysOnTop) != 0)
            {
                flags |= SDL_WindowFlags.AlwaysOnTop;
            }

            if ((window.windowFlags & WindowFlags.Transparent) != 0)
            {
                flags |= SDL_WindowFlags.Transparent;
            }

            if (window.windowState == WindowState.Maximized)
            {
                flags |= SDL_WindowFlags.Maximized;
            }
            else if (window.windowState == WindowState.Fullscreen)
            {
                flags |= SDL_WindowFlags.Fullscreen;
            }

            ref WindowTransform transform = ref world.TryGetComponent<WindowTransform>(windowEntity, transformType, out bool containsTransform);
            if (!containsTransform)
            {
                throw new NullReferenceException($"Window `{windowEntity}` is missing expected `{typeof(WindowTransform)}` component");
            }

            //add extensions
            IsDestination destination = world.GetComponent<IsDestination>(windowEntity, destinationType);
            if (destination.rendererLabel != default)
            {
                if (destination.rendererLabel.Equals("vulkan"))
                {
                    //add sdl extensions that describe vulkan
                    flags |= SDL_WindowFlags.Vulkan;
                    ASCIIText256[] sdlVulkanExtensions = sdlLibrary.GetVulkanInstanceExtensions();
                    Values<DestinationExtension> extensions = world.GetArray<DestinationExtension>(windowEntity, destinationExtensionType);
                    for (int i = 0; i < sdlVulkanExtensions.Length; i++)
                    {
                        extensions.Add(new(sdlVulkanExtensions[i]));
                    }
                }
                else
                {
                    Trace.WriteLine($"Unknown renderer label `{destination.rendererLabel}`, not able to add extensions for SDL window");
                }
            }
            else
            {
                //no render label assigned? what do?
            }

            Span<char> titleBuffer = stackalloc char[window.title.Length];
            window.title.CopyTo(titleBuffer);
            SDLWindow sdlWindow = new(titleBuffer, transform.size, flags);

            if ((window.windowFlags & WindowFlags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            window.id = sdlWindow.ID;
            return sdlWindow;
        }

        private SDLWindow GetWindow(uint windowEntity, Span<uint> windowEntities, Span<uint> windowIds)
        {
            ThrowIfWindowIsMissing(windowEntity);

            int index = windowEntities.IndexOf(windowEntity);
            return sdlLibrary.GetWindow(windowIds[index]);
        }

        [Conditional("DEBUG")]
        private void ThrowIfWindowIsMissing(uint windowEntity)
        {
            if (!windowEntities.Contains(windowEntity))
            {
                throw new InvalidOperationException($"Entity `{windowEntity}` is not a known SDL window");
            }
        }

        /// <summary>
        /// Updates the SDL window to match the entity.
        /// </summary>
        private void UpdateWindowToMatchEntity(uint windowEntity, ref IsWindow window, ref SDLWindowState lastState, SDLWindow sdlWindow)
        {
            SDLDisplay sdlDisplay = sdlWindow.Display;
            ref WindowTransform transform = ref world.TryGetComponent<WindowTransform>(windowEntity, transformType, out bool containsTransform);
            if (containsTransform)
            {
                Vector2 position = transform.position;
                position.X += sdlDisplay.Width * transform.anchor.X;
                position.Y += sdlDisplay.Height * transform.anchor.Y;
                int x = (int)position.X;
                int y = (int)position.Y;
                if (lastState.x != x || lastState.y != y)
                {
                    lastState.x = x;
                    lastState.y = y;
                    sdlWindow.Position = position;
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

            bool borderless = (window.windowFlags & WindowFlags.Borderless) != 0;
            bool resizable = (window.windowFlags & WindowFlags.Resizable) != 0;
            bool minimized = (window.windowFlags & WindowFlags.Minimized) != 0;
            bool alwaysOnTop = (window.windowFlags & WindowFlags.AlwaysOnTop) != 0;
            bool focused = (window.windowFlags & WindowFlags.Focused) != 0;
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
                bool cursorVisible = window.cursorState == CursorState.Normal;
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

            bool hiddenAndConfined = window.cursorState == CursorState.HiddenAndConfined;
            if (sdlWindow.IsRelativeMouseMode != hiddenAndConfined)
            {
                sdlWindow.IsRelativeMouseMode = hiddenAndConfined;
            }

            Vector4 mouseArea = sdlWindow.MouseArea;
            if (mouseArea != window.cursorArea)
            {
                sdlWindow.MouseArea = window.cursorArea;
            }

            sdlWindow.IsAlwaysOnTop = alwaysOnTop;

            if ((window.windowFlags & WindowFlags.Transparent) != 0)
            {
                sdlWindow.SetTransparency(0f);
            }

            bool isMaximized = sdlWindow.IsMaximized;
            bool isFullscreen = sdlWindow.IsFullscreen;
            if (window.windowState == WindowState.Maximized && !isMaximized)
            {
                sdlWindow.Maximize();
            }
            else if (window.windowState == WindowState.Fullscreen && !isFullscreen)
            {
                sdlWindow.IsFullscreen = true;
            }
            else if (window.windowState == WindowState.Windowed && (isMaximized || isFullscreen))
            {
                sdlWindow.Restore();
            }

            //make sure name of window matches entity
            if (!window.title.Equals(sdlWindow.Title))
            {
                Span<char> title = stackalloc char[window.title.Length];
                window.title.CopyTo(title);
                sdlWindow.SetTitle(title);
            }

            lastState.flags = window.windowFlags;
            lastState.state = window.windowState;

            //update referenced display
            ref IsDisplay display = ref GetOrCreateDisplayEntity(sdlDisplay, out uint displayEntity);
            display.width = sdlDisplay.Width;
            display.height = sdlDisplay.Height;
            display.refreshRate = sdlDisplay.RefreshRate;

            if (window.displayReference == default)
            {
                window.displayReference = world.AddReference(windowEntity, displayEntity);
            }
        }

        private ref IsDisplay GetOrCreateDisplayEntity(SDLDisplay display, out uint displayEntity)
        {
            uint displayId = display.ID;
            if (!displayEntities.TryGetValue(displayId, out displayEntity))
            {
                displayEntity = world.CreateEntity(new BitMask(displayType));
                displayEntities.Add(displayId, displayEntity);
            }

            //todo: this could be cached?
            return ref world.GetComponent<IsDisplay>(displayEntity, displayType);
        }
    }
}